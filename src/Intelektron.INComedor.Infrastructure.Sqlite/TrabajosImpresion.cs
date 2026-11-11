using System.Globalization;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>
/// Lo que recibe la impresora: los datos guardados del comprobante, con el nombre del comensal y sin su código ni su
/// credencial, la leyenda de la copia y, en una reimpresión, el nombre del operador que la habilitó (D1 y D5 de
/// enriquecer-comprobante) o, si se hizo con la clave del día, la marca SIN VALIDEZ en su lugar (D2 de
/// reimprimir-sin-codigo): una copia lleva una de las dos, nunca las dos.
/// </summary>
public sealed record ComprobanteAImprimir(
    Guid TrabajoId, Guid TicketId, int NumeroCopia, string DatosImpresion, string? Leyenda, string? Habilito = null, bool SinValidez = false);

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

/// <summary>Cómo quedó un trabajo después de un envío y, si falló, el código de la impresora (PAPER_OUT, PRINT_FAILED).</summary>
public sealed record ResultadoEnvio(Guid TrabajoId, Guid TicketId, int NumeroCopia, EstadoTrabajoImpresion Estado, string? CodigoError = null);

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
                trabajo.TrabajoId, trabajo.TicketId, trabajo.NumeroCopia, trabajo.DatosImpresion, Leyenda(trabajo.NumeroCopia), trabajo.Habilito,
                trabajo.SinValidez));

            var (estado, error) = respuesta.Envio switch
            {
                RespuestaEnvio.Enviado => ("SUBMITTED", null),
                RespuestaEnvio.Fallido => ("FAILED", respuesta.CodigoError ?? "PRINT_FAILED"),
                _ => ("UNKNOWN", (string?)null),
            };
            Cambiar(trabajo, "UNKNOWN", estado, error);
            resultados.Add(new ResultadoEnvio(trabajo.TrabajoId, trabajo.TicketId, trabajo.NumeroCopia, Estado(estado), error));
        }

        return resultados;
    }

    /// <summary>
    /// La copia 0 es el original y no lleva leyenda; las demás, «REIMPRESIÓN · copia N» (RB08, D5 de enriquecer-comprobante).
    /// </summary>
    internal static string? Leyenda(int numeroCopia) =>
        numeroCopia == 0 ? null : string.Create(CultureInfo.InvariantCulture, $"REIMPRESIÓN · copia {numeroCopia}");

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
            SELECT imp.comimpCodigo, imp.comcmpCodigo, imp.comimpNumeroCopia, cmp.comcmpDatosImpresion,
                   CASE WHEN imp.comimpNumeroCopia > 0 THEN (
                       SELECT per.comperNombreVisible
                       FROM tbComedorBitacora bit
                       JOIN tbComedorPersonas per ON lower(per.comperCodigo) = lower(json_extract(bit.combitContexto, '$.PersonId'))
                       JOIN tbComedorVersionesConfiguracion ver ON ver.comvcCodigo = per.comvcCodigo
                       WHERE bit.combitTipoEvento = 'TICKET_REPRINTED'
                         AND lower(json_extract(bit.combitContexto, '$.PrintJobId')) = lower(imp.comimpCodigo)
                       ORDER BY per.comvcCodigo = (SELECT comvcCodigoActiva FROM tbComedorConfiguracionPuestos LIMIT 1) DESC,
                                ver.comvcVersionConfiguracion DESC
                       LIMIT 1) END,
                   CASE WHEN imp.comimpNumeroCopia > 0 AND EXISTS (
                       SELECT 1 FROM tbComedorBitacora bit
                       WHERE bit.combitTipoEvento = 'TICKET_REPRINTED'
                         AND lower(json_extract(bit.combitContexto, '$.PrintJobId')) = lower(imp.comimpCodigo)
                         AND json_extract(bit.combitContexto, '$.Access') = 'DAILY_KEY') THEN 1 ELSE 0 END
            FROM tbComedorTrabajosImpresion imp
            JOIN tbComedorComprobantes cmp ON cmp.comcmpCodigo = imp.comcmpCodigo
            WHERE imp.comimpEstado = 'QUEUED'
            ORDER BY imp.comimpFechaActualizacionUtc, imp.comimpNumeroCopia
            """);
        using var lector = comando.ExecuteReader();
        var trabajos = new List<TrabajoEnCola>();
        while (lector.Read())
        {
            trabajos.Add(new TrabajoEnCola(lector.GetGuid(0), lector.GetGuid(1), lector.GetInt32(2), lector.GetString(3),
                lector.IsDBNull(4) ? null : lector.GetString(4), lector.GetInt64(5) == 1));
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

    /// <summary>
    /// Un trabajo en cola; el de una copia, con el nombre del operador que la habilitó, leído del TICKET_REPRINTED de la
    /// bitácora de la base, escrito en la misma transacción que el trabajo (D5 de enriquecer-comprobante), o con la marca
    /// SIN VALIDEZ si ese TICKET_REPRINTED dice Access DAILY_KEY (D2 de reimprimir-sin-codigo).
    /// </summary>
    private sealed record TrabajoEnCola(Guid TrabajoId, Guid TicketId, int NumeroCopia, string DatosImpresion, string? Habilito, bool SinValidez);
}
