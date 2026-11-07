using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Sincronizacion;

/// <summary>
/// El receptor central por HTTPS con TLS mutuo (D11 de recibir-lotes-idempotente): presenta el certificado del puesto,
/// acepta sólo un servidor emitido por la autoridad configurada y clasifica cada respuesta para el sincronizador. Nunca
/// interpreta un acuse: eso lo hace la cola al registrarlo.
/// </summary>
public sealed partial class ClienteReceptorHttp : IReceptorLotes, IDisposable
{
    private const string RutaLotes = "api/incomedor/v1/sync-batches";
    private const int TopeRespuesta = 1024 * 1024;

    private static readonly HttpRequestOptionsKey<MarcaTls> TlsEstablecido = new("INComedor.TlsEstablecido");

    private readonly HttpClient _http;
    private readonly List<X509Certificate2> _propios = [];

    public ClienteReceptorHttp(Uri direccion, X509Certificate2 certificadoPuesto, X509Certificate2 autoridad, TimeSpan? espera = null)
    {
        var tls = new SslClientAuthenticationOptions
        {
            ClientCertificates = [certificadoPuesto],
            LocalCertificateSelectionCallback = (_, _, _, _, _) => certificadoPuesto,
            RemoteCertificateValidationCallback = (_, servidor, _, errores) => ServidorDeLaAutoridad(servidor, errores, autoridad),
        };
        // Sin redirecciones: el puesto sólo habla con la dirección configurada (OWASP A10).
        // El filtro corre una vez establecido el TLS mutuo, antes de HTTP: la prueba de conexión lo usa para saber en qué
        // etapa se cortó un pedido (D12 de configurar-puesto-por-pestanas). Devuelve el mismo flujo.
        _http = new HttpClient(new SocketsHttpHandler
        {
            SslOptions = tls,
            AllowAutoRedirect = false,
            PlaintextStreamFilter = (contexto, _) =>
            {
                if (contexto.InitialRequestMessage.Options.TryGetValue(TlsEstablecido, out var marca))
                {
                    marca.Listo = true;
                }

                return ValueTask.FromResult(contexto.PlaintextStream);
            },
        })
        {
            BaseAddress = direccion,
            Timeout = espera ?? TimeSpan.FromSeconds(10),
        };
    }

    /// <summary>
    /// El cliente de la demostración (D12): sólo si la carpeta tiene puesto.pfx y autoridad.pfx, que genera el receptor
    /// con --demostracion; de la autoridad se usa sólo la parte pública. Sin ellos, null: el puesto atiende sin receptor.
    /// </summary>
    public static ClienteReceptorHttp? DesdeCarpetaDemostracion(string carpeta, Uri direccion)
    {
        var rutaPuesto = Path.Combine(carpeta, "puesto.pfx");
        var rutaAutoridad = Path.Combine(carpeta, "autoridad.pfx");
        if (!File.Exists(rutaPuesto) || !File.Exists(rutaAutoridad))
        {
            return null;
        }

        var puesto = X509CertificateLoader.LoadPkcs12FromFile(rutaPuesto, null);
        X509Certificate2 autoridad;
        using (var conClave = X509CertificateLoader.LoadPkcs12FromFile(rutaAutoridad, null))
        {
            autoridad = X509CertificateLoader.LoadCertificate(conClave.RawData);
        }

        var cliente = new ClienteReceptorHttp(direccion, puesto, autoridad);
        cliente._propios.AddRange([puesto, autoridad]);
        return cliente;
    }

    public Task<RespuestaReceptor> EnviarAsync(LoteSincronizacion lote, CancellationToken cancelacion)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, RutaLotes) { Content = new ByteArrayContent(lote.Cuerpo) };
        pedido.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        pedido.Headers.Add("Idempotency-Key", lote.BatchId.ToString());
        return PedirAsync(pedido, cancelacion);
    }

    public Task<RespuestaReceptor> ConsultarAsync(Guid batchId, CancellationToken cancelacion) =>
        PedirAsync(new HttpRequestMessage(HttpMethod.Get, $"{RutaLotes}/{batchId}"), cancelacion);

    /// <summary>
    /// La consulta de un lote que no existe, para la prueba de conexión (D12 de configurar-puesto-por-pestanas): si se
    /// estableció el TLS mutuo y, si el receptor respondió, su estado y su código. No envía ningún lote ni toca la cola.
    /// </summary>
    public async Task<SondaReceptor> ProbarAsync(CancellationToken cancelacion)
    {
        var marca = new MarcaTls();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, $"{RutaLotes}/{Guid.NewGuid()}");
        pedido.Options.Set(TlsEstablecido, marca);
        try
        {
            using var respuesta = await _http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, cancelacion);
            return new SondaReceptor(true, (int)respuesta.StatusCode, await CodigoDelProblemaAsync(respuesta, cancelacion), null);
        }
        catch (HttpRequestException e)
        {
            return new SondaReceptor(marca.Listo, null, null, e.HttpRequestError.ToString());
        }
        catch (IOException)
        {
            return new SondaReceptor(marca.Listo, null, null, "IO");
        }
        catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            return new SondaReceptor(marca.Listo, null, null, "TIMEOUT");
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        foreach (var certificado in _propios)
        {
            certificado.Dispose();
        }
    }

    private async Task<RespuestaReceptor> PedirAsync(HttpRequestMessage pedido, CancellationToken cancelacion)
    {
        using (pedido)
        {
            try
            {
                using var respuesta = await _http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, cancelacion);
                var estado = (int)respuesta.StatusCode;
                if (estado is 200 or 201)
                {
                    return await LeerAcotadoAsync(respuesta, cancelacion) is { } acuse ? new Acusado(acuse) : new SinRespuesta();
                }

                if (estado == 404 && pedido.Method == HttpMethod.Get)
                {
                    return new NoEncontrado();
                }

                return new Rechazado(estado, await CodigoDelProblemaAsync(respuesta, cancelacion));
            }
            catch (HttpRequestException)
            {
                return new SinRespuesta();
            }
            catch (IOException)
            {
                return new SinRespuesta();
            }
            catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
            {
                // El tiempo de espera del cliente: no se sabe si el receptor guardó el lote.
                return new SinRespuesta();
            }
        }
    }

    /// <summary>El cuerpo como texto, con un tope: una respuesta más grande no es un acuse de este contrato.</summary>
    private static async Task<string?> LeerAcotadoAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        await using var flujo = await respuesta.Content.ReadAsStreamAsync(cancelacion);
        using var memoria = new MemoryStream();
        var bloque = new byte[16 * 1024];
        int leidos;
        while ((leidos = await flujo.ReadAsync(bloque, cancelacion)) > 0)
        {
            if (memoria.Length + leidos > TopeRespuesta)
            {
                return null;
            }

            memoria.Write(bloque, 0, leidos);
        }

        return System.Text.Encoding.UTF8.GetString(memoria.ToArray());
    }

    /// <summary>El code de un problem+json, sólo si tiene la forma de un código del contrato; si no, null (OWASP A09).</summary>
    private static async Task<string?> CodigoDelProblemaAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        if (respuesta.Content.Headers.ContentType?.MediaType != "application/problem+json"
            || await LeerAcotadoAsync(respuesta, cancelacion) is not { } texto)
        {
            return null;
        }

        try
        {
            using var problema = JsonDocument.Parse(texto);
            return problema.RootElement.ValueKind == JsonValueKind.Object
                && problema.RootElement.TryGetProperty("code", out var codigo) && codigo.ValueKind == JsonValueKind.String
                && CodigoDelContrato().IsMatch(codigo.GetString()!)
                    ? codigo.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool ServidorDeLaAutoridad(X509Certificate? servidor, SslPolicyErrors errores, X509Certificate2 autoridad)
    {
        // El nombre tiene que coincidir; la cadena se arma contra la autoridad configurada y nada más (OWASP A07).
        if (servidor is null || (errores & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
        {
            return false;
        }

        using var cadena = new X509Chain();
        cadena.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        cadena.ChainPolicy.CustomTrustStore.Add(autoridad);
        cadena.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        cadena.ChainPolicy.DisableCertificateDownloads = true;
        using var certificado = X509CertificateLoader.LoadCertificate(servidor.GetRawCertData());
        return cadena.Build(certificado);
    }

    [GeneratedRegex("^[A-Z_]{1,40}$")]
    private static partial Regex CodigoDelContrato();

    private sealed class MarcaTls
    {
        public bool Listo { get; set; }
    }
}

/// <summary>Lo que vio la sonda: si se estableció el TLS mutuo y, si el receptor respondió, su estado y su código.</summary>
public sealed record SondaReceptor(bool TlsEstablecido, int? Estado, string? Codigo, string? Error);
