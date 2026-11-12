using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Intelektron.INComedor.Contracts;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Un lote guardado: su BatchId, el cuerpo canónico que se envía tal cual y sus eventos, en orden.</summary>
public sealed record LoteSincronizacion(Guid BatchId, byte[] Cuerpo, IReadOnlyList<Guid> EventIds)
{
    /// <summary>Se armó en esta llamada a Preparar: nadie pudo haberlo enviado antes, así que no hace falta consultarlo.</summary>
    public bool Nuevo { get; init; }
}

public enum ResultadoRegistroAcuse
{
    Registrado,
    AcuseInvalido,
    YaRegistrado,
}

/// <summary>
/// La cola de salida del puesto (D10 de recibir-lotes-idempotente): arma lotes durables con los eventos del outbox,
/// devuelve el mismo lote hasta que llega un acuse durable y válido, y sólo cambia lotes, estados del outbox y
/// bitácora. Nunca toca intenciones, consumos, cupo, comprobantes ni trabajos de impresión (RB16).
/// </summary>
public sealed partial class ColaSincronizacion
{
    public const int MaximoEventosPorLote = 100;

    // Estado del evento del outbox según el resultado del acuse; los motivos válidos por estado son los del EventAck.
    private static readonly Dictionary<string, (string Estado, string[] Motivos)> ResultadosDelContrato = new()
    {
        ["ACCEPTED"] = ("ACKNOWLEDGED", ["RECORDED"]),
        ["DUPLICATE"] = ("ACKNOWLEDGED", ["ALREADY_RECORDED"]),
        ["CONFLICT"] = ("CONFLICT", ["EVENT_PAYLOAD_CONFLICT"]),
        ["REJECTED"] = ("REJECTED", ["INVALID_EVENT"]),
        ["NEEDS_REVIEW"] = ("NEEDS_REVIEW", ["UNKNOWN_CONFIGURATION_VERSION", "MULTI_KIOSK_POLICY_REVIEW", "RELATED_EVENT_PENDING"]),
    };

    private readonly BaseComedor _baseComedor;
    private readonly TimeProvider _reloj;
    private readonly RegistroBitacora _bitacora = new("Infrastructure.Sqlite.ColaSincronizacion");

    public ColaSincronizacion(BaseComedor baseComedor, TimeProvider reloj)
    {
        _baseComedor = baseComedor;
        _reloj = reloj;
    }

    /// <summary>
    /// Devuelve el lote sin acuse tal cual se guardó o, si no hay, guarda uno nuevo con hasta 100 eventos pendientes
    /// en el orden en que se registraron. Sin nada pendiente devuelve null y no escribe.
    /// </summary>
    public LoteSincronizacion? Preparar()
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);

        var pendiente = LeerLotePendiente(conexion, transaccion);
        if (pendiente is not null)
        {
            transaccion.Commit();
            return pendiente;
        }

        var eventos = LeerEventosSinLote(conexion, transaccion);
        if (eventos.Count == 0)
        {
            transaccion.Commit();
            return null;
        }

        var batchId = Guid.NewGuid();
        var cuerpo = ArmarCuerpo(batchId, _reloj.GetUtcNow(), eventos.Select(e => e.Contenido));
        ComandosSqlite.Ejecutar(conexion, transaccion,
            """
            INSERT INTO tbComedorLotesSincronizacion (comlotCodigo, comlotContenidoLote, comlotHashContenidoLote, comlotEstado, comlotContenidoAcuse)
            VALUES ($lot, $contenido, $hash, 'PENDING', NULL)
            """,
            ("$lot", batchId), ("$contenido", Encoding.UTF8.GetString(cuerpo)), ("$hash", SHA256.HashData(cuerpo)));
        foreach (var (id, _) in eventos)
        {
            ComandosSqlite.Ejecutar(conexion, transaccion,
                "INSERT INTO tbComedorLotesSincronizacion_Eventos (comlotCodigo, comevCodigo) VALUES ($lot, $ev)",
                ("$lot", batchId), ("$ev", id));
        }

        transaccion.Commit();
        return new LoteSincronizacion(batchId, cuerpo, eventos.Select(e => e.Id).ToList()) { Nuevo = true };
    }

    /// <summary>Eventos del outbox que esperan acuse y eventos retenidos para revisión (CONFLICT, REJECTED o NEEDS_REVIEW).</summary>
    public (int Pendientes, int Retenidos) LeerConteos()
    {
        using var conexion = _baseComedor.AbrirConexion();
        var pendientes = ComandosSqlite.EscalarLong(conexion, null,
            "SELECT COUNT(*) FROM tbComedorEventosSalida WHERE comevsEstado = 'PENDING'");
        var retenidos = ComandosSqlite.EscalarLong(conexion, null,
            "SELECT COUNT(*) FROM tbComedorEventosSalida WHERE comevsEstado IN ('CONFLICT', 'REJECTED', 'NEEDS_REVIEW')");
        return ((int)pendientes, (int)retenidos);
    }

    /// <summary>
    /// Registra un acuse sólo si es COMPLETED, del mismo BatchId y con exactamente un resultado válido por evento del
    /// lote. Si no, no cambia el lote ni el outbox y deja SYNC_ACK_INVALID en la bitácora.
    /// </summary>
    public ResultadoRegistroAcuse RegistrarAcuse(Guid batchId, string contenidoAcuse)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var puesto = PuestoLocal.Leer(conexion, transaccion);
        var ahora = _reloj.GetUtcNow();

        using var estado = ComandosSqlite.Crear(conexion, transaccion,
            "SELECT comlotEstado FROM tbComedorLotesSincronizacion WHERE comlotCodigo = $lot", ("$lot", batchId));
        var estadoLote = estado.ExecuteScalar() as string;
        if (estadoLote == "COMPLETED")
        {
            transaccion.Commit();
            return ResultadoRegistroAcuse.YaRegistrado;
        }

        List<(Guid EventId, string Status)>? resultados = null;
        var motivo = estadoLote is null
            ? "LOTE_DESCONOCIDO"
            : Validar(batchId, contenidoAcuse, LeerEventosDelLote(conexion, transaccion, batchId), out resultados);
        if (motivo is not null)
        {
            _bitacora.Insertar(conexion, transaccion, puesto, ahora, "SYNC_ACK_INVALID", new { BatchId = batchId, Motivo = motivo });
            transaccion.Commit();
            return ResultadoRegistroAcuse.AcuseInvalido;
        }

        foreach (var (eventId, status) in resultados!)
        {
            ComandosSqlite.Ejecutar(conexion, transaccion,
                "UPDATE tbComedorEventosSalida SET comevsEstado = $estado WHERE comevCodigo = $ev AND comevsEstado = 'PENDING'",
                ("$estado", ResultadosDelContrato[status].Estado), ("$ev", eventId));
        }

        ComandosSqlite.Ejecutar(conexion, transaccion,
            "UPDATE tbComedorLotesSincronizacion SET comlotEstado = 'COMPLETED', comlotContenidoAcuse = $acuse WHERE comlotCodigo = $lot",
            ("$acuse", contenidoAcuse), ("$lot", batchId));

        int Contar(string status) => resultados!.Count(r => r.Status == status);
        _bitacora.Insertar(conexion, transaccion, puesto, ahora, "SYNC_BATCH_ACKNOWLEDGED", new
        {
            BatchId = batchId,
            Accepted = Contar("ACCEPTED"),
            Duplicate = Contar("DUPLICATE"),
            Conflict = Contar("CONFLICT"),
            Rejected = Contar("REJECTED"),
            NeedsReview = Contar("NEEDS_REVIEW"),
        });

        transaccion.Commit();
        return ResultadoRegistroAcuse.Registrado;
    }

    /// <summary>
    /// Deja en la bitácora un envío que no acusó (el código HTTP o NETWORK, y el code del problema si vino), sin tocar
    /// el lote ni el outbox.
    /// </summary>
    public void RegistrarError(Guid batchId, string codigo, string? codigoProblema = null)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var puesto = PuestoLocal.Leer(conexion, transaccion);

        // El code llega del receptor: entra a la bitácora sólo con la forma de un código del contrato (OWASP A09).
        var problema = codigoProblema is not null && CodigoDelContrato().IsMatch(codigoProblema) ? codigoProblema : null;
        _bitacora.Insertar(conexion, transaccion, puesto, _reloj.GetUtcNow(), "SYNC_BATCH_FAILED",
            new { BatchId = batchId, Codigo = codigo, CodigoProblema = problema });

        transaccion.Commit();
    }

    private static LoteSincronizacion? LeerLotePendiente(SqliteConnection conexion, SqliteTransaction transaccion)
    {
        Guid batchId;
        string contenido;
        using (var comando = ComandosSqlite.Crear(conexion, transaccion,
            "SELECT comlotCodigo, comlotContenidoLote FROM tbComedorLotesSincronizacion WHERE comlotEstado = 'PENDING' ORDER BY rowid LIMIT 1"))
        using (var lector = comando.ExecuteReader())
        {
            if (!lector.Read())
            {
                return null;
            }

            batchId = lector.GetGuid(0);
            contenido = lector.GetString(1);
        }

        return new LoteSincronizacion(batchId, Encoding.UTF8.GetBytes(contenido), LeerEventosDelLote(conexion, transaccion, batchId));
    }

    private static List<Guid> LeerEventosDelLote(SqliteConnection conexion, SqliteTransaction transaccion, Guid batchId)
    {
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            "SELECT comevCodigo FROM tbComedorLotesSincronizacion_Eventos WHERE comlotCodigo = $lot ORDER BY rowid", ("$lot", batchId));
        using var lector = comando.ExecuteReader();
        var eventos = new List<Guid>();
        while (lector.Read())
        {
            eventos.Add(lector.GetGuid(0));
        }

        return eventos;
    }

    private static List<(Guid Id, string Contenido)> LeerEventosSinLote(SqliteConnection conexion, SqliteTransaction transaccion)
    {
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            """
            SELECT e.comevCodigo, e.comevsContenido
            FROM tbComedorEventosSalida e
            WHERE e.comevsEstado = 'PENDING'
              AND NOT EXISTS (SELECT 1 FROM tbComedorLotesSincronizacion_Eventos i WHERE i.comevCodigo = e.comevCodigo)
            ORDER BY e.rowid
            LIMIT $maximo
            """,
            ("$maximo", MaximoEventosPorLote));
        using var lector = comando.ExecuteReader();
        var eventos = new List<(Guid, string)>();
        while (lector.Read())
        {
            eventos.Add((lector.GetGuid(0), lector.GetString(1)));
        }

        return eventos;
    }

    /// <summary>SyncBatchRequest con SchemaVersion 1, en su forma canónica RFC 8785 (D4): esos bytes son el cuerpo.</summary>
    private static byte[] ArmarCuerpo(Guid batchId, DateTimeOffset ahora, IEnumerable<string> eventos)
    {
        var salida = new ArrayBufferWriter<byte>();
        using (var escritor = new Utf8JsonWriter(salida))
        {
            escritor.WriteStartObject();
            escritor.WriteString("BatchId", batchId);
            escritor.WriteNumber("SchemaVersion", 1);
            escritor.WriteString("CreatedAtUtc", FormatoInstante.Texto(ahora));
            escritor.WriteStartArray("Events");
            foreach (var contenido in eventos)
            {
                escritor.WriteRawValue(contenido);
            }

            escritor.WriteEndArray();
            escritor.WriteEndObject();
        }

        return JsonCanonico.Canonicalizar(salida.WrittenSpan);
    }

    private static string? Validar(Guid batchId, string contenidoAcuse, List<Guid> eventosDelLote, out List<(Guid EventId, string Status)>? resultados)
    {
        resultados = null;
        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(contenidoAcuse);
        }
        catch (JsonException)
        {
            return "NO_ES_JSON";
        }

        using (documento)
        {
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object
                || !raiz.TryGetProperty("Status", out var status) || status.ValueKind != JsonValueKind.String
                || status.GetString() != "COMPLETED")
            {
                return "STATUS";
            }

            if (!raiz.TryGetProperty("BatchId", out var lote) || lote.ValueKind != JsonValueKind.String
                || !lote.TryGetGuid(out var loteAcusado) || loteAcusado != batchId)
            {
                return "BATCH_ID";
            }

            if (!raiz.TryGetProperty("Results", out var lista) || lista.ValueKind != JsonValueKind.Array)
            {
                return "RESULTADOS";
            }

            var leidos = new List<(Guid, string)>();
            foreach (var resultado in lista.EnumerateArray())
            {
                if (resultado.ValueKind != JsonValueKind.Object
                    || !resultado.TryGetProperty("EventId", out var evento) || evento.ValueKind != JsonValueKind.String
                    || !evento.TryGetGuid(out var eventId)
                    || !resultado.TryGetProperty("Status", out var estado) || estado.ValueKind != JsonValueKind.String
                    || !resultado.TryGetProperty("ReasonCode", out var motivo) || motivo.ValueKind != JsonValueKind.String
                    || !ResultadosDelContrato.TryGetValue(estado.GetString()!, out var contrato)
                    || !contrato.Motivos.Contains(motivo.GetString()))
                {
                    return "RESULTADO";
                }

                leidos.Add((eventId, estado.GetString()!));
            }

            // Exactamente un resultado por EventId del lote, sin extraños ni omitidos.
            if (leidos.Count != eventosDelLote.Count
                || leidos.Select(r => r.Item1).Distinct().Count() != leidos.Count
                || !leidos.Select(r => r.Item1).ToHashSet().SetEquals(eventosDelLote))
            {
                return "RESULTADOS";
            }

            resultados = leidos;
            return null;
        }
    }

    [GeneratedRegex("^[A-Z_]{1,40}$")]
    private static partial Regex CodigoDelContrato();
}
