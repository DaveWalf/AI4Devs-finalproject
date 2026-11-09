using System.IO;
using System.Net;
using System.Net.Sockets;
using Intelektron.INComedor.Kiosk.Sincronizacion;

namespace Intelektron.INComedor.Kiosk.Red;

/// <summary>Las cuatro etapas de la prueba de conexión con el receptor (D12).</summary>
public enum EtapaConexion
{
    Nombre,
    Conexion,
    Tls,
    Receptor,
}

/// <summary>El resultado de una etapa.</summary>
public enum EstadoEtapa
{
    Funciono,
    Fallo,
    NoProbada,
}

/// <summary>Una etapa con su resultado y, si falló, el motivo.</summary>
public sealed record ResultadoEtapa(EtapaConexion Etapa, EstadoEtapa Estado, string? Motivo = null);

/// <summary>El resultado de una prueba de conexión: la dirección, si faltaban los certificados y las cuatro etapas.</summary>
public sealed record ResultadoConexion(Uri Direccion, bool SinCertificados, IReadOnlyList<ResultadoEtapa> Etapas)
{
    public bool Funciono => !SinCertificados && Etapas.All(e => e.Estado == EstadoEtapa.Funciono);

    /// <summary>El contexto de <c>CONNECTIVITY_TESTED</c>: la dirección y las etapas, sin certificados ni claves (RB21).</summary>
    public object Registro(string acceso) => new
    {
        Address = Direccion.ToString(),
        Result = SinCertificados ? "NO_CERTIFICATES" : Funciono ? "PASSED" : "FAILED",
        Stages = Etapas.Select(e => new
        {
            Stage = e.Etapa switch
            {
                EtapaConexion.Nombre => "NAME",
                EtapaConexion.Conexion => "CONNECTION",
                EtapaConexion.Tls => "MUTUAL_TLS",
                _ => "RECEPTOR",
            },
            Result = e.Estado switch { EstadoEtapa.Funciono => "PASSED", EstadoEtapa.Fallo => "FAILED", _ => "NOT_TESTED" },
            Reason = e.Motivo,
        }).ToList(),
        Access = acceso,
        ActorPersonId = (Guid?)null,
        ActorReason = "NO_AUTHENTICATION",
    };
}

/// <summary>
/// La prueba de conexión con el receptor configurado (D12): el nombre, la conexión, el TLS mutuo con los certificados del
/// puesto y la respuesta del receptor a la consulta de un lote que no existe. No envía ningún lote ni toca la cola.
/// </summary>
public sealed class PruebaConexion
{
    public PruebaConexion(Uri direccion, string carpetaCertificados)
    {
        Direccion = direccion;
        CarpetaCertificados = carpetaCertificados;
    }

    public Uri Direccion { get; }

    public string CarpetaCertificados { get; }

    private static readonly TimeSpan EsperaConexion = TimeSpan.FromSeconds(5);

    /// <summary>Los mismos archivos que pide <see cref="ClienteReceptorHttp.DesdeCarpetaDemostracion"/>.</summary>
    public bool TieneCertificados =>
        File.Exists(Path.Combine(CarpetaCertificados, "puesto.pfx")) && File.Exists(Path.Combine(CarpetaCertificados, "autoridad.pfx"));

    /// <summary>
    /// Las cuatro etapas en orden; la primera que falla deja las siguientes sin probar. Sin certificados no prueba nada.
    /// </summary>
    public async Task<ResultadoConexion> ProbarAsync(CancellationToken cancelacion)
    {
        if (!TieneCertificados)
        {
            return Resultado(true);
        }

        IPAddress[] direcciones;
        try
        {
            direcciones = await Dns.GetHostAddressesAsync(Direccion.DnsSafeHost, cancelacion);
        }
        catch (SocketException e)
        {
            return Resultado(false, Fallo(EtapaConexion.Nombre, $"no se resolvió {Direccion.DnsSafeHost} ({e.SocketErrorCode})"));
        }

        if (direcciones.Length == 0)
        {
            return Resultado(false, Fallo(EtapaConexion.Nombre, $"{Direccion.DnsSafeHost} no tiene direcciones"));
        }

        var nombre = new ResultadoEtapa(EtapaConexion.Nombre, EstadoEtapa.Funciono);
        using (var socket = new Socket(SocketType.Stream, ProtocolType.Tcp))
        using (var espera = CancellationTokenSource.CreateLinkedTokenSource(cancelacion))
        {
            espera.CancelAfter(EsperaConexion);
            try
            {
                await socket.ConnectAsync(direcciones, Direccion.Port, espera.Token);
            }
            catch (SocketException e)
            {
                return Resultado(false, nombre, Fallo(EtapaConexion.Conexion, $"el puerto {Direccion.Port} no aceptó la conexión ({e.SocketErrorCode})"));
            }
            catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
            {
                return Resultado(false, nombre, Fallo(EtapaConexion.Conexion, $"el puerto {Direccion.Port} no respondió en {EsperaConexion.TotalSeconds:0} s"));
            }
        }

        var conexion = new ResultadoEtapa(EtapaConexion.Conexion, EstadoEtapa.Funciono);
        using var cliente = ClienteReceptorHttp.DesdeCarpetaDemostracion(CarpetaCertificados, Direccion);
        if (cliente is null)
        {
            return Resultado(true);
        }

        var sonda = await cliente.ProbarAsync(cancelacion);
        if (!sonda.TlsEstablecido)
        {
            return Resultado(false, nombre, conexion, Fallo(EtapaConexion.Tls, $"no se estableció la conexión segura ({sonda.Error})"));
        }

        var tls = new ResultadoEtapa(EtapaConexion.Tls, EstadoEtapa.Funciono);
        var receptor = sonda switch
        {
            { Estado: 404, Codigo: "BATCH_NOT_FOUND" } => new ResultadoEtapa(EtapaConexion.Receptor, EstadoEtapa.Funciono),
            { Estado: { } estado } => Fallo(EtapaConexion.Receptor, $"respondió {estado}{(sonda.Codigo is null ? "" : " " + sonda.Codigo)}"),
            _ => Fallo(EtapaConexion.Receptor, $"no respondió ({sonda.Error})"),
        };
        return Resultado(false, nombre, conexion, tls, receptor);
    }

    private static ResultadoEtapa Fallo(EtapaConexion etapa, string motivo) => new(etapa, EstadoEtapa.Fallo, motivo);

    /// <summary>Las etapas dadas y, después, las que faltan como no probadas.</summary>
    private ResultadoConexion Resultado(bool sinCertificados, params ResultadoEtapa[] etapas) =>
        new(Direccion, sinCertificados, Enum.GetValues<EtapaConexion>()
            .Select(e => etapas.FirstOrDefault(r => r.Etapa == e) ?? new ResultadoEtapa(e, EstadoEtapa.NoProbada)).ToList());
}
