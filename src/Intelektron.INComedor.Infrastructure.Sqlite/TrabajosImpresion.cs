using System.Globalization;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Lo que recibe la impresora: el JSON del comprobante, sin persona ni código, y la leyenda de una copia.</summary>
public sealed record ComprobanteAImprimir(Guid TrabajoId, Guid TicketId, int NumeroCopia, string DatosImpresion, string? Leyenda);

public enum RespuestaEnvio
{
    Enviado,
    Fallido,
    Incierto,
}

/// <summary>La respuesta de la impresora; un fallo trae su código (por ejemplo, PAPER_OUT).</summary>
public sealed record RespuestaImpresora(RespuestaEnvio Envio, string? CodigoError)
{
    public static RespuestaImpresora Enviado() => new(RespuestaEnvio.Enviado, null);

    public static RespuestaImpresora Fallido(string codigo) => new(RespuestaEnvio.Fallido, codigo);

    public static RespuestaImpresora Incierto() => new(RespuestaEnvio.Incierto, null);
}

/// <summary>Puerto de la impresora (D7): la simulada del Kiosk y el doble de las pruebas lo implementan.</summary>
public interface IImpresoraComprobantes
{
    RespuestaImpresora Enviar(ComprobanteAImprimir comprobante);
}

/// <summary>PrintState del contrato, sin SENDING (D7).</summary>
public enum EstadoTrabajoImpresion
{
    Queued,
    Unknown,
    Submitted,
    Failed,
}

/// <summary>Cómo quedó un trabajo después de un envío.</summary>
public sealed record ResultadoEnvio(Guid TrabajoId, Guid TicketId, int NumeroCopia, EstadoTrabajoImpresion Estado);

/// <summary>
/// Envía los trabajos en QUEUED después del commit de RB06, sincrónicamente (D7 de atender-seleccion-y-confirmacion):
/// cada uno pasa a UNKNOWN en su propia transacción antes de llamar a la impresora, y con la respuesta a SUBMITTED o
/// FAILED. Un trabajo en UNKNOWN nunca se reenvía solo. Nada de esto toca consumo, cupo ni comprobante (RB17).
/// </summary>
public sealed class TrabajosImpresion
{
    private readonly BaseComedor _baseComedor;
    private readonly TimeProvider _reloj;
    private readonly IImpresoraComprobantes _impresora;
    private readonly RegistroBitacora _bitacora = new("Infrastructure.Sqlite.TrabajosImpresion");

    public TrabajosImpresion(BaseComedor baseComedor, TimeProvider reloj, IImpresoraComprobantes impresora)
    {
        _baseComedor = baseComedor;
        _reloj = reloj;
        _impresora = impresora;
    }

    public IReadOnlyList<ResultadoEnvio> EnviarPendientes()
    {
        var resultados = new List<ResultadoEnvio>();
        foreach (var trabajo in LeerEnCola())
        {
            if (!Cambiar(trabajo, "QUEUED", "UNKNOWN", null))
            {
                continue;
            }

            var respuesta = _impresora.Enviar(new ComprobanteAImprimir(
                trabajo.TrabajoId, trabajo.TicketId, trabajo.NumeroCopia, trabajo.DatosImpresion, Leyenda(trabajo.NumeroCopia)));

            var (estado, error) = respuesta.Envio switch
            {
                RespuestaEnvio.Enviado => ("SUBMITTED", null),
                RespuestaEnvio.Fallido => ("FAILED", respuesta.CodigoError ?? "PRINT_FAILED"),
                _ => ("UNKNOWN", (string?)null),
            };
            Cambiar(trabajo, "UNKNOWN", estado, error);
            resultados.Add(new ResultadoEnvio(trabajo.TrabajoId, trabajo.TicketId, trabajo.NumeroCopia, Estado(estado)));
        }

        return resultados;
    }

    /// <summary>La copia 0 es el original y no lleva leyenda; las demás, «DUPLICADO · copia N» (RB08).</summary>
    internal static string? Leyenda(int numeroCopia) =>
        numeroCopia == 0 ? null : string.Create(CultureInfo.InvariantCulture, $"DUPLICADO · copia {numeroCopia}");

    internal static EstadoTrabajoImpresion Estado(string estado) => estado switch
    {
        "QUEUED" => EstadoTrabajoImpresion.Queued,
        "UNKNOWN" => EstadoTrabajoImpresion.Unknown,
        "SUBMITTED" => EstadoTrabajoImpresion.Submitted,
        "FAILED" => EstadoTrabajoImpresion.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null),
    };

    private List<TrabajoEnCola> LeerEnCola()
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var comando = ComandosSqlite.Crear(conexion, null,
            """
            SELECT imp.comimpCodigo, imp.comcmpCodigo, imp.comimpNumeroCopia, cmp.comcmpDatosImpresion
            FROM tbComedorTrabajosImpresion imp
            JOIN tbComedorComprobantes cmp ON cmp.comcmpCodigo = imp.comcmpCodigo
            WHERE imp.comimpEstado = 'QUEUED'
            ORDER BY imp.comimpFechaActualizacionUtc, imp.comimpNumeroCopia
            """);
        using var lector = comando.ExecuteReader();
        var trabajos = new List<TrabajoEnCola>();
        while (lector.Read())
        {
            trabajos.Add(new TrabajoEnCola(lector.GetGuid(0), lector.GetGuid(1), lector.GetInt32(2), lector.GetString(3)));
        }

        return trabajos;
    }

    /// <summary>Cambia el estado si el trabajo sigue en el anterior, y lo registra en la misma transacción.</summary>
    private bool Cambiar(TrabajoEnCola trabajo, string anterior, string nuevo, string? error)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var ahora = _reloj.GetUtcNow();

        using (var comando = ComandosSqlite.Crear(conexion, transaccion,
                   """
                   UPDATE tbComedorTrabajosImpresion
                   SET comimpEstado = $nuevo, comimpCodigoUltimoError = $error, comimpFechaActualizacionUtc = $ahora
                   WHERE comimpCodigo = $job AND comimpEstado = $anterior
                   """,
                   ("$nuevo", nuevo), ("$error", error), ("$ahora", FormatoInstante.Texto(ahora)),
                   ("$job", trabajo.TrabajoId), ("$anterior", anterior)))
        {
            if (comando.ExecuteNonQuery() == 0)
            {
                return false;
            }
        }

        _bitacora.Insertar(conexion, transaccion, PuestoLocal.Leer(conexion, transaccion), ahora, "PRINT_JOB_UPDATED",
            IdentificacionPorCodigo.ContextoSinNulos(
                ("PrintJobId", trabajo.TrabajoId), ("TicketId", trabajo.TicketId), ("CopyNumber", trabajo.NumeroCopia),
                ("PreviousState", anterior), ("State", nuevo), ("ErrorCode", error)));
        transaccion.Commit();
        return true;
    }

    private sealed record TrabajoEnCola(Guid TrabajoId, Guid TicketId, int NumeroCopia, string DatosImpresion);
}
