using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Intelektron.INComedor.Contracts;
using Intelektron.INComedor.Contracts.Lotes;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// La cola de lotes del puesto sin red (D10 de recibir-lotes-idempotente): lote durable antes de enviar, avance sólo
/// con un acuse durable y ningún cambio en las decisiones. El receptor se simula con el texto del acuse.
/// </summary>
public class ColaSincronizacionTests : ConfirmacionTestBase
{
    // Letras fuera del hexadecimal, para que ningún UUID del lote pueda contener el código por azar.
    private const string CodigoDelLote = "ZQXLOTEKJ77";

    private static readonly string[] TablasDeDecision =
    [
        "tbComedorIntencionesConsumo",
        "tbComedorConsumos",
        "tbComedorConsumosConocidos",
        "tbComedorCargosCupoConocidos",
        "tbComedorUsosCupo",
        "tbComedorComprobantes",
        "tbComedorTrabajosImpresion",
    ];

    private readonly ColaSincronizacion _cola;

    public ColaSincronizacionTests()
    {
        _cola = new ColaSincronizacion(Base, Reloj);
    }

    [Fact(DisplayName = "HU02-C · Eventos pendientes: el lote guardado lleva los dos eventos, su cuerpo canónico y el SHA-256 de ese cuerpo, y cumple SyncBatchRequest con SchemaVersion 1")]
    public void Prepara_un_lote_durable_con_los_eventos_pendientes()
    {
        Assert.Equal(TipoResultado.Autorizada, Confirmar(Fixture.AlmuerzoComun).Tipo);
        Assert.Equal(TipoResultado.Denegada, Confirmar(Fixture.AlmuerzoComun).Tipo);
        var eventos = EventosEnOrden();

        var lote = _cola.Preparar();

        Assert.NotNull(lote);
        Assert.Equal(eventos.Select(e => e.Id), lote.EventIds);
        using var conexion = Abrir();
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorLotesSincronizacion"));
        var guardado = Sql.Escalar<string>(conexion, "SELECT comlotContenidoLote FROM tbComedorLotesSincronizacion WHERE comlotCodigo = $lot", ("$lot", lote.BatchId));
        var hash = Sql.Escalar<byte[]>(conexion, "SELECT comlotHashContenidoLote FROM tbComedorLotesSincronizacion WHERE comlotCodigo = $lot", ("$lot", lote.BatchId));
        Assert.Equal("PENDING", Sql.Escalar<string>(conexion, "SELECT comlotEstado FROM tbComedorLotesSincronizacion WHERE comlotCodigo = $lot", ("$lot", lote.BatchId)));
        Assert.Equal(lote.Cuerpo, Encoding.UTF8.GetBytes(guardado));
        Assert.Equal(SHA256.HashData(lote.Cuerpo), hash);
        Assert.Equal(lote.Cuerpo, JsonCanonico.Canonicalizar(lote.Cuerpo));
        Assert.Equal(2L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorLotesSincronizacion_Eventos WHERE comlotCodigo = $lot", ("$lot", lote.BatchId)));
        Assert.All(eventos, e => Assert.Equal("PENDING", EstadoDe(e.Id)));

        using var cuerpo = JsonDocument.Parse(lote.Cuerpo);
        var raiz = cuerpo.RootElement;
        Assert.Equal(["BatchId", "CreatedAtUtc", "Events", "SchemaVersion"], raiz.EnumerateObject().Select(p => p.Name));
        Assert.Equal(lote.BatchId, raiz.GetProperty("BatchId").GetGuid());
        Assert.Equal(1, raiz.GetProperty("SchemaVersion").GetInt32());
        Assert.EndsWith("Z", raiz.GetProperty("CreatedAtUtc").GetString());
        Assert.Equal(Reloj.Ahora, raiz.GetProperty("CreatedAtUtc").GetDateTimeOffset());
        var enviados = raiz.GetProperty("Events").EnumerateArray().ToList();
        Assert.Equal(eventos.Select(e => e.Id), enviados.Select(e => e.GetProperty("EventId").GetGuid()));
        Assert.Equal(eventos.Select(e => Canonico(e.Contenido)), enviados.Select(e => JsonCanonico.Canonicalizar(e)));
    }

    [Fact(DisplayName = "AT27 · RB17 · Caída después de guardar el lote y antes de enviarlo: al reiniciar se prepara el mismo BatchId y el mismo cuerpo, byte por byte, sin otro lote")]
    public void Despues_de_una_caida_prepara_el_mismo_lote()
    {
        Confirmar(Fixture.AlmuerzoComun);
        var primero = _cola.Preparar()!;
        Confirmar(Fixture.Refrigerio);

        var reiniciada = new ColaSincronizacion(BaseComedor.Abrir(Base.Ruta), Reloj);
        Reloj.Ahora = Reloj.Ahora.AddMinutes(5);
        var segundo = reiniciada.Preparar()!;

        Assert.Equal(primero.BatchId, segundo.BatchId);
        Assert.Equal(primero.Cuerpo, segundo.Cuerpo);
        Assert.Equal(primero.EventIds, segundo.EventIds);
        using var conexion = Abrir();
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorLotesSincronizacion"));
    }

    [Fact(DisplayName = "HU02-C · Nada pendiente: sin eventos en PENDING ni lotes sin acuse, no se guarda ningún lote")]
    public void Sin_nada_pendiente_no_prepara_ningun_lote()
    {
        var lote = _cola.Preparar();

        Assert.Null(lote);
        using var conexion = Abrir();
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorLotesSincronizacion"));
    }

    [Fact(DisplayName = "HU02-C · Más de 100 eventos pendientes: el lote lleva los 100 primeros en el orden en que se registraron y el restante va en el lote siguiente")]
    public void Con_101_eventos_el_lote_lleva_los_100_primeros()
    {
        var registrados = new List<Guid>();
        using (var conexion = Abrir())
        {
            for (var i = 0; i < 101; i++)
            {
                var id = Guid.NewGuid();
                Sql.Ejecutar(conexion,
                    "INSERT INTO tbComedorEventosSalida VALUES ($ev, NULL, NULL, NULL, 'CONSUMPTION_DENIED', 1, $contenido, zeroblob(32), 'PENDING')",
                    ("$ev", id), ("$contenido", $$"""{"EventId":"{{id}}","EventType":"CONSUMPTION_DENIED"}"""));
                registrados.Add(id);
            }
        }

        var primero = _cola.Preparar()!;

        Assert.Equal(registrados.Take(100), primero.EventIds);
        using var cuerpo = JsonDocument.Parse(primero.Cuerpo);
        Assert.Equal(registrados.Take(100), cuerpo.RootElement.GetProperty("Events").EnumerateArray().Select(e => e.GetProperty("EventId").GetGuid()));
        Assert.Equal("PENDING", EstadoDe(registrados[100]));

        Assert.Equal(ResultadoRegistroAcuse.Registrado,
            _cola.RegistrarAcuse(primero.BatchId, Acuse(primero.BatchId, primero.EventIds.Select(e => (e, "ACCEPTED", "RECORDED")).ToArray())));
        var segundo = _cola.Preparar()!;

        Assert.NotEqual(primero.BatchId, segundo.BatchId);
        Assert.Equal([registrados[100]], segundo.EventIds);
    }

    [Fact(DisplayName = "AT27 · Acuse completo: con ACCEPTED/RECORDED para los dos, los eventos quedan incorporados y el lote completo con su acuse guardado")]
    public void Un_acuse_completo_incorpora_los_eventos()
    {
        Confirmar(Fixture.AlmuerzoComun);
        Confirmar(Fixture.AlmuerzoComun);
        var lote = _cola.Preparar()!;
        var acuse = Acuse(lote.BatchId, lote.EventIds.Select(e => (e, "ACCEPTED", "RECORDED")).ToArray());

        var resultado = _cola.RegistrarAcuse(lote.BatchId, acuse);

        Assert.Equal(ResultadoRegistroAcuse.Registrado, resultado);
        Assert.All(lote.EventIds, e => Assert.Equal("ACKNOWLEDGED", EstadoDe(e)));
        using var conexion = Abrir();
        Assert.Equal("COMPLETED", Sql.Escalar<string>(conexion, "SELECT comlotEstado FROM tbComedorLotesSincronizacion WHERE comlotCodigo = $lot", ("$lot", lote.BatchId)));
        Assert.Equal(acuse, Sql.Escalar<string>(conexion, "SELECT comlotContenidoAcuse FROM tbComedorLotesSincronizacion WHERE comlotCodigo = $lot", ("$lot", lote.BatchId)));
    }

    [Fact(DisplayName = "SubmitSyncBatch · Un evento en conflicto o rechazado: el aceptado queda incorporado, los otros dos retenidos con su estado, no se reenvían y siguen en la base")]
    public void Conflicto_y_rechazo_quedan_retenidos()
    {
        Confirmar(Fixture.AlmuerzoComun);
        Confirmar(Fixture.AlmuerzoComun);
        Confirmar(Fixture.Refrigerio);
        var lote = _cola.Preparar()!;
        Assert.Equal(3, lote.EventIds.Count);

        var resultado = _cola.RegistrarAcuse(lote.BatchId, Acuse(lote.BatchId,
            (lote.EventIds[0], "ACCEPTED", "RECORDED"),
            (lote.EventIds[1], "CONFLICT", "EVENT_PAYLOAD_CONFLICT"),
            (lote.EventIds[2], "REJECTED", "INVALID_EVENT")));

        Assert.Equal(ResultadoRegistroAcuse.Registrado, resultado);
        Assert.Equal("ACKNOWLEDGED", EstadoDe(lote.EventIds[0]));
        Assert.Equal("CONFLICT", EstadoDe(lote.EventIds[1]));
        Assert.Equal("REJECTED", EstadoDe(lote.EventIds[2]));
        Assert.Null(_cola.Preparar());
        using var conexion = Abrir();
        Assert.Equal(3L, Sql.Contar(conexion, "tbComedorEventosSalida"));
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorLotesSincronizacion"));
        using var contexto = JsonDocument.Parse(Sql.Escalar<string>(conexion,
            "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = 'SYNC_BATCH_ACKNOWLEDGED'"));
        Assert.Equal(lote.BatchId, contexto.RootElement.GetProperty("BatchId").GetGuid());
        Assert.Equal(1, contexto.RootElement.GetProperty("Accepted").GetInt32());
        Assert.Equal(1, contexto.RootElement.GetProperty("Conflict").GetInt32());
        Assert.Equal(1, contexto.RootElement.GetProperty("Rejected").GetInt32());
        Assert.Equal(0, contexto.RootElement.GetProperty("Duplicate").GetInt32());
        Assert.Equal(0, contexto.RootElement.GetProperty("NeedsReview").GetInt32());
    }

    [Theory(DisplayName = "CompletedBatchAck · Acuse incompleto o que no es del lote: ningún evento cambia de estado, el lote sigue sin acuse y queda SYNC_ACK_INVALID en la bitácora")]
    [InlineData("un solo resultado")]
    [InlineData("un EventId que no está en el lote")]
    [InlineData("un resultado repetido")]
    [InlineData("otro BatchId")]
    [InlineData("Status PENDING de un 202")]
    [InlineData("un estado fuera del contrato")]
    [InlineData("texto que no es JSON")]
    public void Un_acuse_invalido_no_cambia_nada(string caso)
    {
        Confirmar(Fixture.AlmuerzoComun);
        Confirmar(Fixture.AlmuerzoComun);
        var lote = _cola.Preparar()!;
        var (a, b) = (lote.EventIds[0], lote.EventIds[1]);
        var acuse = caso switch
        {
            "un solo resultado" => Acuse(lote.BatchId, (a, "ACCEPTED", "RECORDED")),
            "un EventId que no está en el lote" => Acuse(lote.BatchId, (a, "ACCEPTED", "RECORDED"), (Guid.NewGuid(), "ACCEPTED", "RECORDED")),
            "un resultado repetido" => Acuse(lote.BatchId, (a, "ACCEPTED", "RECORDED"), (a, "ACCEPTED", "RECORDED")),
            "otro BatchId" => Acuse(Guid.NewGuid(), (a, "ACCEPTED", "RECORDED"), (b, "ACCEPTED", "RECORDED")),
            "Status PENDING de un 202" => $$"""{"BatchId":"{{lote.BatchId}}","Status":"PENDING","CorrelationId":"{{Guid.NewGuid()}}"}""",
            "un estado fuera del contrato" => Acuse(lote.BatchId, (a, "ACCEPTED", "RECORDED"), (b, "DENIED", "LOCAL_LIMIT_REACHED")),
            _ => "<html>no es JSON</html>",
        };
        var antes = FotoDeLaCola();

        var resultado = _cola.RegistrarAcuse(lote.BatchId, acuse);

        Assert.Equal(ResultadoRegistroAcuse.AcuseInvalido, resultado);
        Assert.Equal(antes, FotoDeLaCola());
        using var conexion = Abrir();
        Assert.Equal(1L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = 'SYNC_ACK_INVALID'"));
        Assert.Equal(lote.Cuerpo, _cola.Preparar()!.Cuerpo);
    }

    [Theory(DisplayName = "AT27 · RB16 · Respuestas que no acusan (202, 409, 422, 503 o la red): ningún evento cambia, el lote sigue sin acuse con su BatchId y el error queda en la bitácora")]
    [InlineData("202", null)]
    [InlineData("409", "BATCH_PAYLOAD_CONFLICT")]
    [InlineData("422", "INVALID_BATCH")]
    [InlineData("503", "SERVICE_UNAVAILABLE")]
    [InlineData("NETWORK", null)]
    public void Una_respuesta_que_no_acusa_no_cambia_nada(string codigo, string? codigoProblema)
    {
        Confirmar(Fixture.AlmuerzoComun);
        var lote = _cola.Preparar()!;
        var antes = FotoDeLaCola();

        _cola.RegistrarError(lote.BatchId, codigo, codigoProblema);

        Assert.Equal(antes, FotoDeLaCola());
        var otra = _cola.Preparar()!;
        Assert.Equal(lote.BatchId, otra.BatchId);
        Assert.Equal(lote.Cuerpo, otra.Cuerpo);
        using var conexion = Abrir();
        using var contexto = JsonDocument.Parse(Sql.Escalar<string>(conexion,
            "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = 'SYNC_BATCH_FAILED'"));
        Assert.Equal(lote.BatchId, contexto.RootElement.GetProperty("BatchId").GetGuid());
        Assert.Equal(codigo, contexto.RootElement.GetProperty("Codigo").GetString());
        Assert.Equal(codigoProblema, contexto.RootElement.GetProperty("CodigoProblema").GetString());
    }

    [Fact(DisplayName = "RB16 · AT27 · Un conflicto no revierte el consumo: preparar y registrar un CONFLICT dejan iguales, byte por byte, intenciones, consumos, usos de cupo, comprobantes y trabajos")]
    public void Un_conflicto_no_revierte_el_consumo()
    {
        Assert.Equal(TipoResultado.Autorizada, Confirmar(Fixture.AlmuerzoComun).Tipo);
        var antes = FotoDeDecisiones();

        var lote = _cola.Preparar()!;
        _cola.RegistrarAcuse(lote.BatchId, Acuse(lote.BatchId, (lote.EventIds[0], "CONFLICT", "EVENT_PAYLOAD_CONFLICT")));

        Assert.Equal("CONFLICT", EstadoDe(lote.EventIds[0]));
        Assert.Equal(antes, FotoDeDecisiones());
        Assert.Contains(antes, fila => fila.StartsWith("tbComedorConsumos:", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "AGENTS.md · El lote no lleva el código: ni el cuerpo guardado ni el acuse ni la bitácora de la cola contienen el código con que la persona se identificó")]
    public void El_lote_no_lleva_el_codigo()
    {
        using (var conexion = Abrir())
        {
            Sql.Ejecutar(conexion,
                "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, NULL, NULL)",
                ("$vc", Fixture.SnapshotId), ("$cre", Guid.NewGuid()), ("$per", Fixture.Habilitada.Id), ("$valor", CodigoDelLote));
        }

        var sesion = new IdentificacionPorCodigo(Base, Reloj).Identificar(CodigoDelLote, PropositoSesion.Consumption);
        Assert.True(sesion.Abierta);
        var confirmacion = Unidad.Confirmar(new SolicitudConfirmacion(
            Guid.NewGuid(), sesion.IdentitySessionId!.Value, sesion.PersonId!.Value, Fixture.AlmuerzoComun.Id, Fixture.AlmuerzoComun.Version, 1));
        Assert.Equal(TipoResultado.Autorizada, confirmacion.Tipo);

        var lote = _cola.Preparar()!;
        _cola.RegistrarAcuse(lote.BatchId, Acuse(lote.BatchId, (lote.EventIds[0], "ACCEPTED", "RECORDED")));

        using var conexion2 = Abrir();
        Assert.Equal(0L, Sql.Escalar<long>(conexion2,
            "SELECT COUNT(*) FROM tbComedorLotesSincronizacion WHERE instr(comlotContenidoLote, $codigo) > 0 OR instr(comlotContenidoAcuse, $codigo) > 0",
            ("$codigo", CodigoDelLote)));
        Assert.Equal(0L, Sql.Escalar<long>(conexion2,
            "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento LIKE 'SYNC_%' AND instr(combitContexto, $codigo) > 0",
            ("$codigo", CodigoDelLote)));
        Assert.DoesNotContain(CodigoDelLote, Encoding.UTF8.GetString(lote.Cuerpo), StringComparison.Ordinal);
    }

    private ResultadoConfirmacion Confirmar(OpcionFixture opcion) => Unidad.Confirmar(Solicitud(Fixture.Habilitada, opcion));

    private List<(Guid Id, string Contenido)> EventosEnOrden()
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT comevCodigo, comevsContenido FROM tbComedorEventosSalida ORDER BY rowid";
        using var lector = comando.ExecuteReader();
        var eventos = new List<(Guid, string)>();
        while (lector.Read())
        {
            eventos.Add((lector.GetGuid(0), lector.GetString(1)));
        }

        return eventos;
    }

    private string EstadoDe(Guid evento)
    {
        using var conexion = Abrir();
        return Sql.Escalar<string>(conexion, "SELECT comevsEstado FROM tbComedorEventosSalida WHERE comevCodigo = $ev", ("$ev", evento));
    }

    private static byte[] Canonico(string json) => JsonCanonico.Canonicalizar(Encoding.UTF8.GetBytes(json));

    private static string Acuse(Guid batchId, params (Guid EventId, string Status, string ReasonCode)[] resultados) =>
        JsonSerializer.Serialize(new CompletedBatchAck(
            batchId, "COMPLETED", "2026-10-19T15:00:03.000Z",
            resultados.Select(r => new EventAck(r.EventId, r.Status, r.ReasonCode)).ToList(),
            Guid.NewGuid()));

    /// <summary>Lotes, ítems y estados del outbox, como texto comparable.</summary>
    private List<string> FotoDeLaCola() =>
        Foto(["tbComedorLotesSincronizacion", "tbComedorLotesSincronizacion_Eventos", "tbComedorEventosSalida"]);

    /// <summary>Cada fila de las tablas que la decisión escribió, byte por byte.</summary>
    private List<string> FotoDeDecisiones() => Foto(TablasDeDecision);

    private List<string> Foto(IEnumerable<string> tablas)
    {
        using var conexion = Abrir();
        var foto = new List<string>();
        foreach (var tabla in tablas)
        {
            using var comando = conexion.CreateCommand();
            comando.CommandText = $"SELECT * FROM {tabla} ORDER BY rowid";
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                var valores = Enumerable.Range(0, lector.FieldCount).Select(i => lector.IsDBNull(i)
                    ? "NULL"
                    : lector.GetValue(i) is byte[] bytes ? Convert.ToHexString(bytes) : Convert.ToString(lector.GetValue(i), System.Globalization.CultureInfo.InvariantCulture));
                foto.Add(tabla + ": " + string.Join(" | ", valores));
            }
        }

        return foto;
    }
}
