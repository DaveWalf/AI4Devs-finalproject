using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Sync.Api.Tests;

/// <summary>
/// El receptor real con Kestrel en el puerto 0 y TLS mutuo, sobre una base temporal por clase que se borra al terminar
/// (D1, D2 y D13 de recibir-lotes-idempotente). La instancia sale de INCOMEDOR_SQLSERVER, con LocalDB por defecto (D1).
/// La autoridad llega como archivo público temporal, igual que la lee el receptor fuera de las pruebas.
/// </summary>
public class ReceptorConKestrel : IAsyncLifetime
{
    public static readonly string Instancia =
        Environment.GetEnvironmentVariable("INCOMEDOR_SQLSERVER") is { Length: > 0 } instancia ? instancia : @"(localdb)\MSSQLLocalDB";

    private readonly string _archivoAutoridad =
        Path.Combine(Path.GetTempPath(), "incomedor-tests", $"autoridad-{Guid.NewGuid():N}.cer");

    private WebApplicationFactory<Program>? _fabrica;

    public CertificadosDePrueba Certificados { get; } = new();

    public string NombreBase { get; } = $"INComedorPruebas_{Guid.NewGuid():N}";

    public string CadenaConexion => $"Server={Instancia};Database={NombreBase};Integrated Security=true;Encrypt=false;Connect Timeout=60";

    public Uri Direccion { get; private set; } = null!;

    public IServiceProvider Servicios => _fabrica!.Services;

    public async ValueTask InitializeAsync()
    {
        await EjecutarEnMasterAsync($"CREATE DATABASE [{NombreBase}]");
        Directory.CreateDirectory(Path.GetDirectoryName(_archivoAutoridad)!);
        await File.WriteAllBytesAsync(_archivoAutoridad, Certificados.AutoridadPublica);

        _fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(constructor =>
        {
            constructor.UseSetting("ConnectionStrings:Receptor", CadenaConexion);
            constructor.UseSetting("Receptor:Autoridad", _archivoAutoridad);
            constructor.ConfigureTestServices(ConfigurarServicios);
        });
        _fabrica.UseKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, escucha => escucha.UseHttps(Certificados.Servidor, https =>
        {
            // Lo mismo que el receptor fija por defecto: pide el certificado de equipo sin cortar el handshake, y
            // la cadena, la vigencia y el registro los valida la aplicación (D2).
            https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
            https.AllowAnyClientCertificate();
        })));
        _fabrica.StartServer();

        var direcciones = _fabrica.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>();
        Direccion = new Uri(direcciones.Addresses.Single());
    }

    public async ValueTask DisposeAsync()
    {
        if (_fabrica is not null)
        {
            await _fabrica.DisposeAsync();
        }

        SqlConnection.ClearAllPools();
        await EjecutarEnMasterAsync(
            $"IF DB_ID(N'{NombreBase}') IS NOT NULL BEGIN ALTER DATABASE [{NombreBase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{NombreBase}]; END");
        File.Delete(_archivoAutoridad);
        Certificados.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Un cliente HTTPS que presenta ese certificado de equipo (o ninguno) y confía sólo en la autoridad de prueba.</summary>
    public HttpClient CrearCliente(X509Certificate2? certificado)
    {
        var opciones = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (_, servidor, _, errores) => ServidorValido(servidor, errores),
        };
        if (certificado is not null)
        {
            opciones.ClientCertificates = [certificado];
            opciones.LocalCertificateSelectionCallback = (_, _, _, _, _) => certificado;
        }

        return new HttpClient(new SocketsHttpHandler { SslOptions = opciones }) { BaseAddress = Direccion };
    }

    /// <summary>Registra el certificado como identidad del puesto en tbComedorPuestos.</summary>
    public Task RegistrarPuestoAsync(Guid kioskId, X509Certificate2 certificado) =>
        EjecutarAsync(
            "INSERT INTO tbComedorPuestos (compueCodigo, comInstalacionCodigo, comOrganizacionCodigo, compueIdentidadCertificado) VALUES (@pue, @sitio, @org, @huella)",
            ("@pue", kioskId), ("@sitio", Guid.NewGuid()), ("@org", Guid.NewGuid()), ("@huella", CertificadosDePrueba.Huella(certificado)));

    /// <summary>El KioskId registrado para ese certificado; si no lo está, lo registra con un KioskId nuevo.</summary>
    public async Task<Guid> AsegurarPuestoAsync(X509Certificate2 certificado)
    {
        await using (var conexion = new SqlConnection(CadenaConexion))
        {
            await conexion.OpenAsync();
            await using var comando = Crear(conexion, "SELECT compueCodigo FROM tbComedorPuestos WHERE compueIdentidadCertificado = @huella",
                [("@huella", CertificadosDePrueba.Huella(certificado))]);
            if (await comando.ExecuteScalarAsync() is Guid existente)
            {
                return existente;
            }
        }

        var nuevo = Guid.NewGuid();
        await RegistrarPuestoAsync(nuevo, certificado);
        return nuevo;
    }

    public async Task<T> EscalarAsync<T>(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = Crear(conexion, sql, parametros);
        var valor = await comando.ExecuteScalarAsync();
        return valor is T ya ? ya : (T)Convert.ChangeType(valor!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task EjecutarAsync(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = Crear(conexion, sql, parametros);
        await comando.ExecuteNonQueryAsync();
    }

    public Task<int> ContarAsync(string tabla) => EscalarAsync<int>($"SELECT COUNT(*) FROM {tabla}");

    /// <summary>Lo que cada clase agrega al receptor de prueba; por defecto, nada.</summary>
    protected virtual void ConfigurarServicios(IServiceCollection servicios)
    {
    }

    private static SqlCommand Crear(SqlConnection conexion, string sql, (string Nombre, object Valor)[] parametros)
    {
        var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor);
        }

        return comando;
    }

    private static async Task EjecutarEnMasterAsync(string sql)
    {
        await using var conexion = new SqlConnection($"Server={Instancia};Database=master;Integrated Security=true;Encrypt=false;Connect Timeout=60");
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        comando.CommandTimeout = 120;
        await comando.ExecuteNonQueryAsync();
    }

    private bool ServidorValido(X509Certificate? servidor, SslPolicyErrors errores)
    {
        if (servidor is null || (errores & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
        {
            return false;
        }

        using var cadena = new X509Chain();
        cadena.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        cadena.ChainPolicy.CustomTrustStore.Add(Certificados.Autoridad);
        cadena.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return cadena.Build(servidor as X509Certificate2 ?? X509CertificateLoader.LoadCertificate(servidor.GetRawCertData()));
    }
}

/// <summary>El receptor de prueba con un endpoint extra que devuelve la huella del certificado de cliente que vio Kestrel.</summary>
public sealed class ReceptorConEndpointDePrueba : ReceptorConKestrel
{
    public const string Ruta = "/prueba/tls";
    public const string SinCertificado = "sin certificado";

    protected override void ConfigurarServicios(IServiceCollection servicios) =>
        servicios.AddSingleton<IStartupFilter, EndpointDePrueba>();

    private sealed class EndpointDePrueba : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => aplicacion =>
        {
            aplicacion.Map(Ruta, rama => rama.Run(async contexto =>
            {
                var certificado = await contexto.Connection.GetClientCertificateAsync();
                await contexto.Response.WriteAsync(certificado is null ? SinCertificado : CertificadosDePrueba.Huella(certificado));
            }));
            siguiente(aplicacion);
        };
    }
}
