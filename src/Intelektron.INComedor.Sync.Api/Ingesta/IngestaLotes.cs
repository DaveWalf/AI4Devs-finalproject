using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Intelektron.INComedor.Contracts;
using Intelektron.INComedor.Contracts.Lotes;
using Microsoft.Data.SqlClient;

namespace Intelektron.INComedor.Sync.Api.Ingesta;

/// <summary>
/// Ingesta de un lote en una transacción con un bloqueo de aplicación (D3): el mismo lote devuelve el acuse original y
/// otro cuerpo con la misma clave es 409. Cada evento se decide por su EventId global y su contenido canónico (D6 y
/// D7). Ningún resultado autoriza, deniega ni revierte un consumo: el receptor sólo deduplica y consolida (RB16).
/// </summary>
internal sealed class IngestaLotes(ConfiguracionReceptor configuracion, TimeProvider reloj)
{
    public const int TopeCuerpo = 1024 * 1024;

    public async Task<IResult> ProcesarAsync(Guid puesto, string? clave, byte[] cuerpo, CancellationToken cancelacion)
    {
        byte[] canonico;
        try
        {
            canonico = JsonCanonico.Canonicalizar(cuerpo);
        }
        catch (JsonCanonicoException)
        {
            return ProblemasReceptor.Crear("INVALID_REQUEST");
        }

        using var documento = JsonDocument.Parse(canonico);
        var raiz = documento.RootElement;
        if (ValidadorLote.BatchIdLegible(raiz) is not { } batchId)
        {
            return ProblemasReceptor.Crear("INVALID_BATCH");
        }

        if (clave is null)
        {
            return ProblemasReceptor.Crear("INVALID_REQUEST");
        }

        if (!Guid.TryParseExact(clave, "D", out var claveLote) || claveLote != batchId)
        {
            return ProblemasReceptor.Crear("IDEMPOTENCY_KEY_MISMATCH");
        }

        var hash = SHA256.HashData(canonico);
        await using var conexion = new SqlConnection(configuracion.CadenaConexion);
        await conexion.OpenAsync(cancelacion);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancelacion);
        var sql = new Sql(conexion, transaccion, cancelacion);
        await TomarBloqueoAsync(sql);

        if (await LeerReciboAsync(sql, puesto, batchId) is { } recibo)
        {
            await transaccion.CommitAsync(cancelacion);
            return recibo.Hash.AsSpan().SequenceEqual(hash)
                ? new RespuestaAcuse(recibo.Acuse, StatusCodes.Status200OK)
                : ProblemasReceptor.Crear("BATCH_PAYLOAD_CONFLICT");
        }

        if (!ValidadorLote.LoteValido(raiz))
        {
            await transaccion.RollbackAsync(cancelacion);
            return ProblemasReceptor.Crear("INVALID_BATCH");
        }

        var ahora = reloj.GetUtcNow();
        var resultados = new List<EventAck>();
        foreach (var evento in raiz.GetProperty("Events").EnumerateArray())
        {
            resultados.Add(await DecidirAsync(sql, puesto, batchId, evento, ahora));
        }

        var acuse = JsonSerializer.Serialize(new CompletedBatchAck(batchId, "COMPLETED", Instante(ahora), resultados, Guid.NewGuid()));
        await sql.EjecutarAsync(
            """
            INSERT INTO dbo.tbComedorAcusesLotes (compueCodigo, comlotCodigo, comaclHashContenido, comaclEstado, comaclContenidoAcuse)
            VALUES (@pue, @lot, @hash, 'COMPLETED', @acuse)
            """,
            ("@pue", puesto), ("@lot", batchId), ("@hash", hash), ("@acuse", acuse));
        await transaccion.CommitAsync(cancelacion);
        return new RespuestaAcuse(acuse, StatusCodes.Status201Created);
    }

    private static async Task<EventAck> DecidirAsync(Sql sql, Guid puesto, Guid batchId, JsonElement evento, DateTimeOffset ahora)
    {
        var eventId = evento.GetProperty("EventId").GetGuid();
        var canonico = JsonCanonico.Canonicalizar(evento);
        var hash = SHA256.HashData(canonico);
        var contenido = Encoding.UTF8.GetString(canonico);

        if (await LeerEntradaAsync(sql, eventId) is { } entrada)
        {
            if (entrada.Puesto == puesto && entrada.Hash.AsSpan().SequenceEqual(hash))
            {
                return entrada.Estado switch
                {
                    "PROJECTED" => Resultado(eventId, "DUPLICATE", "ALREADY_RECORDED"),
                    "REJECTED" => Resultado(eventId, "REJECTED", "INVALID_EVENT"),
                    "PENDING_REFERENCE" => Resultado(eventId, "NEEDS_REVIEW", "RELATED_EVENT_PENDING"),
                    _ => Resultado(eventId, "NEEDS_REVIEW", "MULTI_KIOSK_POLICY_REVIEW"),
                };
            }

            await AbrirCasoAsync(sql, eventId, "EVENT_PAYLOAD_CONFLICT", puesto, batchId, ahora, contenido);
            return Resultado(eventId, "CONFLICT", "EVENT_PAYLOAD_CONFLICT");
        }

        if (!ValidadorLote.EventoValido(evento))
        {
            await InsertarEntradaAsync(sql, eventId, puesto, hash, contenido, ahora, "REJECTED");
            return Resultado(eventId, "REJECTED", "INVALID_EVENT");
        }

        switch (evento.GetProperty("EventType").GetString())
        {
            case "CONSUMPTION_RECORDED":
                var consumo = evento.GetProperty("ConsumptionId").GetGuid();
                if (await sql.EscalarAsync("SELECT comevCodigoOrigen FROM dbo.tbComedorConsumosConsolidados WHERE comconCodigo = @con", ("@con", consumo)) is Guid origen)
                {
                    await AbrirCasoAsync(sql, origen, "CONSUMPTION_ID_CONFLICT", puesto, batchId, ahora, contenido);
                    return Resultado(eventId, "CONFLICT", "EVENT_PAYLOAD_CONFLICT");
                }

                await InsertarEntradaAsync(sql, eventId, puesto, hash, contenido, ahora, "PROJECTED");
                await sql.EjecutarAsync(
                    "INSERT INTO dbo.tbComedorConsumosConsolidados (comconCodigo, comevCodigoOrigen, compueCodigo, comccoConsumoOriginal) VALUES (@con, @ev, @pue, @contenido)",
                    ("@con", consumo), ("@ev", eventId), ("@pue", puesto), ("@contenido", contenido));

                // Las impresiones del mismo puesto que esperaban este consumo se proyectan ahora; sus acuses no cambian (D7).
                await sql.EjecutarAsync(
                    """
                    UPDATE dbo.tbComedorEventosEntrada SET comeveEstadoProyeccion = 'PROJECTED'
                    WHERE compueCodigo = @pue AND comeveEstadoProyeccion = 'PENDING_REFERENCE'
                      AND JSON_VALUE(comeveContenido, '$.EventType') = 'PRINT_EVENT_RECORDED'
                      AND TRY_CONVERT(uniqueidentifier, JSON_VALUE(comeveContenido, '$.ConsumptionId')) = @con
                    """,
                    ("@pue", puesto), ("@con", consumo));
                return Resultado(eventId, "ACCEPTED", "RECORDED");

            case "PRINT_EVENT_RECORDED":
                var conocido = await sql.EscalarAsync(
                    "SELECT 1 FROM dbo.tbComedorConsumosConsolidados WHERE comconCodigo = @con AND compueCodigo = @pue",
                    ("@con", evento.GetProperty("ConsumptionId").GetGuid()), ("@pue", puesto)) is not null;
                await InsertarEntradaAsync(sql, eventId, puesto, hash, contenido, ahora, conocido ? "PROJECTED" : "PENDING_REFERENCE");
                return conocido
                    ? Resultado(eventId, "ACCEPTED", "RECORDED")
                    : Resultado(eventId, "NEEDS_REVIEW", "RELATED_EVENT_PENDING");

            case "CONNECTIVITY_INCIDENT_RECORDED":
                await InsertarEntradaAsync(sql, eventId, puesto, hash, contenido, ahora, "IN_REVIEW");
                return Resultado(eventId, "NEEDS_REVIEW", "MULTI_KIOSK_POLICY_REVIEW");

            case "CONSUMPTION_DENIED":
                await InsertarEntradaAsync(sql, eventId, puesto, hash, contenido, ahora, "PROJECTED");
                return Resultado(eventId, "ACCEPTED", "RECORDED");

            default:
                throw new InvalidOperationException("ValidadorLote aceptó un EventType que la ingesta no conoce.");
        }
    }

    private static Task TomarBloqueoAsync(Sql sql) =>
        sql.EjecutarAsync(
            """
            DECLARE @resultado int;
            EXEC @resultado = sp_getapplock @Resource = N'ingesta-lotes', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 10000;
            IF @resultado < 0 THROW 50001, N'No se obtuvo el bloqueo de la ingesta.', 1;
            """);

    private static async Task<(byte[] Hash, string Acuse)?> LeerReciboAsync(Sql sql, Guid puesto, Guid batchId)
    {
        await using var comando = sql.Crear(
            "SELECT comaclHashContenido, comaclContenidoAcuse FROM dbo.tbComedorAcusesLotes WHERE compueCodigo = @pue AND comlotCodigo = @lot",
            ("@pue", puesto), ("@lot", batchId));
        await using var lector = await comando.ExecuteReaderAsync(sql.Cancelacion);
        return await lector.ReadAsync(sql.Cancelacion) ? ((byte[])lector[0], lector.GetString(1)) : null;
    }

    private static async Task<(Guid Puesto, byte[] Hash, string Estado)?> LeerEntradaAsync(Sql sql, Guid eventId)
    {
        await using var comando = sql.Crear(
            "SELECT compueCodigo, comeveHashContenido, comeveEstadoProyeccion FROM dbo.tbComedorEventosEntrada WHERE comevCodigo = @ev",
            ("@ev", eventId));
        await using var lector = await comando.ExecuteReaderAsync(sql.Cancelacion);
        return await lector.ReadAsync(sql.Cancelacion) ? (lector.GetGuid(0), (byte[])lector[1], lector.GetString(2)) : null;
    }

    private static Task InsertarEntradaAsync(Sql sql, Guid eventId, Guid puesto, byte[] hash, string contenido, DateTimeOffset ahora, string estado) =>
        sql.EjecutarAsync(
            """
            INSERT INTO dbo.tbComedorEventosEntrada (comevCodigo, compueCodigo, comeveHashContenido, comeveContenido, comeveFechaRecepcionUtc, comeveEstadoProyeccion)
            VALUES (@ev, @pue, @hash, @contenido, @recibido, @estado)
            """,
            ("@ev", eventId), ("@pue", puesto), ("@hash", hash), ("@contenido", contenido), ("@recibido", ahora.UtcDateTime), ("@estado", estado));

    /// <summary>Un caso abierto que conserva la evidencia nueva sin tocar la original (D6); nunca guarda el certificado.</summary>
    private static Task AbrirCasoAsync(Sql sql, Guid eventoOriginal, string tipo, Guid puesto, Guid batchId, DateTimeOffset ahora, string contenido)
    {
        var evidencia = new JsonObject
        {
            ["KioskId"] = puesto.ToString(),
            ["BatchId"] = batchId.ToString(),
            ["RecibidoUtc"] = Instante(ahora),
            ["Evento"] = JsonNode.Parse(contenido),
        };
        return sql.EjecutarAsync(
            "INSERT INTO dbo.tbComedorCasosConciliacion (comcasCodigo, comevCodigo, comcasTipo, comcasEstado, comcasEvidencia) VALUES (@cas, @ev, @tipo, 'OPEN', @evidencia)",
            ("@cas", Guid.NewGuid()), ("@ev", eventoOriginal), ("@tipo", tipo), ("@evidencia", evidencia.ToJsonString()));
    }

    private static EventAck Resultado(Guid eventId, string estado, string motivo) => new(eventId, estado, motivo);

    private static string Instante(DateTimeOffset instante) =>
        instante.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>SQL siempre parametrizado, dentro de la transacción de la ingesta cuando la hay.</summary>
    private sealed record Sql(SqlConnection Conexion, SqlTransaction? Transaccion, CancellationToken Cancelacion)
    {
        public SqlCommand Crear(string texto, params (string Nombre, object Valor)[] parametros)
        {
            var comando = Conexion.CreateCommand();
            comando.Transaction = Transaccion;
            comando.CommandText = texto;
            foreach (var (nombre, valor) in parametros)
            {
                comando.Parameters.Add(valor is DateTime instante
                    ? new SqlParameter(nombre, SqlDbType.DateTime2) { Value = instante }
                    : new SqlParameter(nombre, valor));
            }

            return comando;
        }

        public async Task EjecutarAsync(string texto, params (string Nombre, object Valor)[] parametros)
        {
            await using var comando = Crear(texto, parametros);
            await comando.ExecuteNonQueryAsync(Cancelacion);
        }

        public async Task<object?> EscalarAsync(string texto, params (string Nombre, object Valor)[] parametros)
        {
            await using var comando = Crear(texto, parametros);
            var valor = await comando.ExecuteScalarAsync(Cancelacion);
            return valor is DBNull ? null : valor;
        }
    }
}

/// <summary>El acuse tal cual se guardó, con X-Correlation-Id igual a su CorrelationId (D3).</summary>
internal sealed class RespuestaAcuse(string acuse, int estado) : IResult
{
    public async Task ExecuteAsync(HttpContext contexto)
    {
        using var documento = JsonDocument.Parse(acuse);
        contexto.Response.StatusCode = estado;
        contexto.Response.ContentType = "application/json";
        contexto.Response.Headers["X-Correlation-Id"] = documento.RootElement.GetProperty("CorrelationId").GetString();
        await contexto.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(acuse), contexto.RequestAborted);
    }
}
