using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography.X509Certificates;

namespace Intelektron.INComedor.Sync.Api.Tests;

/// <summary>
/// El resultado de cada evento del lote (D5, D6 y D7 de recibir-lotes-idempotente): deduplicación global por EventId,
/// un consumo consolidado por ConsumptionId, validación por aplicación (AT50) y eventos que esperan una referencia o
/// una revisión. Ningún resultado autoriza, deniega ni revierte un consumo (RB16).
/// </summary>
public class ResultadoPorEventoTests(ReceptorConKestrel receptor) : IClassFixture<ReceptorConKestrel>
{
    private static readonly string[] EstadosDelContrato = ["ACCEPTED", "DUPLICATE", "CONFLICT", "REJECTED", "NEEDS_REVIEW"];
    private static readonly string[] MotivosDelContrato =
        ["RECORDED", "ALREADY_RECORDED", "EVENT_PAYLOAD_CONFLICT", "INVALID_EVENT", "UNKNOWN_CONFIGURATION_VERSION", "MULTI_KIOSK_POLICY_REVIEW", "RELATED_EVENT_PENDING"];

    [Fact(DisplayName = "AT27 · RNF01 · Un consumo nuevo se consolida una vez: ACCEPTED/RECORDED y un consumo consolidado con ese ConsumptionId")]
    public async Task Un_consumo_nuevo_se_consolida()
    {
        var consumo = LotesDePrueba.ConsumoRegistrado();

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, consumo);

        Assert.Equal(("ACCEPTED", "RECORDED"), LotesDePrueba.ResultadoDe(acuse, consumo));
        Assert.Equal(1, await ConsolidadosAsync(consumo));
        Assert.Equal("PROJECTED", await EstadoEntradaAsync(consumo));
    }

    [Fact(DisplayName = "AT27 · HU02-D · El mismo evento en otro lote: DUPLICATE/ALREADY_RECORDED y sigue habiendo un solo consumo consolidado")]
    public async Task El_mismo_evento_en_otro_lote_es_duplicado()
    {
        var consumo = LotesDePrueba.ConsumoRegistrado();
        using var primero = await EnviarAsync(receptor.Certificados.Puesto, consumo);

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, consumo);

        Assert.Equal(("DUPLICATE", "ALREADY_RECORDED"), LotesDePrueba.ResultadoDe(acuse, consumo));
        Assert.Equal(1, await ConsolidadosAsync(consumo));
    }

    [Fact(DisplayName = "SubmitSyncBatch · El mismo EventId con otra MenuVersion: CONFLICT/EVENT_PAYLOAD_CONFLICT, el evento y el consumo originales iguales y un caso con el contenido nuevo")]
    public async Task El_mismo_event_id_con_otro_contenido_es_conflicto()
    {
        var consumo = LotesDePrueba.ConsumoRegistrado();
        using var primero = await EnviarAsync(receptor.Certificados.Puesto, consumo);
        var entradaAntes = await EntradaAsync(consumo);
        var consolidadoAntes = await ConsolidadoAsync(consumo);
        var otro = (JsonObject)consumo.DeepClone();
        otro["MenuVersion"] = 2;

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, otro);

        Assert.Equal(("CONFLICT", "EVENT_PAYLOAD_CONFLICT"), LotesDePrueba.ResultadoDe(acuse, otro));
        Assert.Equal(entradaAntes, await EntradaAsync(consumo));
        Assert.Equal(consolidadoAntes, await ConsolidadoAsync(consumo));
        var (tipo, estado, evidencia) = await CasoAsync(consumo);
        Assert.Equal("EVENT_PAYLOAD_CONFLICT", tipo);
        Assert.Equal("OPEN", estado);
        using var caso = JsonDocument.Parse(evidencia);
        Assert.Equal(2, caso.RootElement.GetProperty("Evento").GetProperty("MenuVersion").GetInt32());
        Assert.Equal(LotesDePrueba.EventId(consumo), caso.RootElement.GetProperty("Evento").GetProperty("EventId").GetGuid());
    }

    [Fact(DisplayName = "HU02.2 · AT13 · Un EventId que ya mandó otro puesto, con el mismo contenido: CONFLICT, no DUPLICATE, y se abre un caso")]
    public async Task El_mismo_evento_desde_otro_puesto_es_conflicto()
    {
        var consumo = LotesDePrueba.ConsumoRegistrado();
        using var primero = await EnviarAsync(receptor.Certificados.Puesto, consumo);

        using var acuse = await EnviarAsync(receptor.Certificados.OtroPuesto, consumo);

        Assert.Equal(("CONFLICT", "EVENT_PAYLOAD_CONFLICT"), LotesDePrueba.ResultadoDe(acuse, consumo));
        Assert.Equal("EVENT_PAYLOAD_CONFLICT", (await CasoAsync(consumo)).Tipo);
        Assert.Equal(1, await ConsolidadosAsync(consumo));
    }

    [Fact(DisplayName = "SubmitSyncBatch · Otro evento con un ConsumptionId ya consolidado: CONFLICT/EVENT_PAYLOAD_CONFLICT y sigue habiendo un solo consumo consolidado")]
    public async Task Otro_evento_con_el_mismo_consumption_id_es_conflicto()
    {
        var consumo = LotesDePrueba.ConsumoRegistrado();
        using var primero = await EnviarAsync(receptor.Certificados.Puesto, consumo);
        var otro = LotesDePrueba.ConsumoRegistrado(consumptionId: Guid.Parse((string)consumo["ConsumptionId"]!));

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, otro);

        Assert.Equal(("CONFLICT", "EVENT_PAYLOAD_CONFLICT"), LotesDePrueba.ResultadoDe(acuse, otro));
        Assert.Equal(1, await ConsolidadosAsync(consumo));
        Assert.Equal(0, await receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorEventosEntrada WHERE comevCodigo = @ev", ("@ev", LotesDePrueba.EventId(otro))));
        Assert.Equal("CONSUMPTION_ID_CONFLICT", (await CasoAsync(consumo)).Tipo);
    }

    [Fact(DisplayName = "ConsumptionDenied · Una denegación se incorpora sin consumo: ACCEPTED/RECORDED y ningún consumo consolidado")]
    public async Task Una_denegacion_se_incorpora_sin_consumo()
    {
        var denegacion = LotesDePrueba.ConsumoDenegado();
        var antes = await receptor.ContarAsync("tbComedorConsumosConsolidados");

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, denegacion);

        Assert.Equal(("ACCEPTED", "RECORDED"), LotesDePrueba.ResultadoDe(acuse, denegacion));
        Assert.Equal(antes, await receptor.ContarAsync("tbComedorConsumosConsolidados"));
        Assert.Equal("PROJECTED", await EstadoEntradaAsync(denegacion));
    }

    [Fact(DisplayName = "AT27 · RB16 · Un conflicto no niega la comida: el consumo consolidado sigue con su contenido original y ningún resultado lleva un estado o un motivo de denegación")]
    public async Task Un_conflicto_no_niega_la_comida()
    {
        var consumo = LotesDePrueba.ConsumoRegistrado();
        using var primero = await EnviarAsync(receptor.Certificados.Puesto, consumo);
        var original = await ConsolidadoAsync(consumo);
        var otro = (JsonObject)consumo.DeepClone();
        otro["MenuVersion"] = 3;

        var denegacion = LotesDePrueba.ConsumoDenegado();

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, otro, denegacion);

        Assert.Equal("OPEN", (await CasoAsync(consumo)).Estado);
        Assert.Equal(original, await ConsolidadoAsync(consumo));
        Assert.Equal(("CONFLICT", "EVENT_PAYLOAD_CONFLICT"), LotesDePrueba.ResultadoDe(acuse, otro));
        Assert.Equal(("ACCEPTED", "RECORDED"), LotesDePrueba.ResultadoDe(acuse, denegacion));
        Assert.All(LotesDePrueba.Resultados(acuse), r =>
        {
            Assert.Contains(r.Status, EstadosDelContrato);
            Assert.Contains(r.ReasonCode, MotivosDelContrato);
        });
    }

    [Fact(DisplayName = "SubmitSyncBatch · Una incidencia en revisión reenviada en otro lote vuelve a ser NEEDS_REVIEW/MULTI_KIOSK_POLICY_REVIEW, nunca DUPLICATE")]
    public async Task Una_incidencia_reenviada_sigue_en_revision()
    {
        var incidencia = LotesDePrueba.IncidenciaConectividad();
        using var primero = await EnviarAsync(receptor.Certificados.Puesto, incidencia);

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, incidencia);

        Assert.Equal(("NEEDS_REVIEW", "MULTI_KIOSK_POLICY_REVIEW"), LotesDePrueba.ResultadoDe(acuse, incidencia));
        Assert.Equal("IN_REVIEW", await EstadoEntradaAsync(incidencia));
    }

    [Fact(DisplayName = "AT50 · Una carga con otra fecha de negocio: REJECTED/INVALID_EVENT, el evento queda guardado sin consolidarse y el otro evento del lote resulta ACCEPTED")]
    public async Task Una_carga_con_otra_fecha_se_rechaza()
    {
        var consumo = LotesDePrueba.ConsumoRegistrado();
        consumo["AppliedQuotaCharges"]![1]!["BusinessDate"] = "2026-10-31";
        var valido = LotesDePrueba.ConsumoDenegado();

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, consumo, valido);

        Assert.Equal(("REJECTED", "INVALID_EVENT"), LotesDePrueba.ResultadoDe(acuse, consumo));
        Assert.Equal(("ACCEPTED", "RECORDED"), LotesDePrueba.ResultadoDe(acuse, valido));
        Assert.Equal("REJECTED", await EstadoEntradaAsync(consumo));
        Assert.Equal(0, await ConsolidadosAsync(consumo));
    }

    [Fact(DisplayName = "AT50 · Todas las cargas con la misma fecha que el consumo: ACCEPTED/RECORDED")]
    public async Task Todas_las_cargas_con_la_misma_fecha_se_aceptan()
    {
        var consumo = LotesDePrueba.ConsumoRegistrado();
        Assert.Equal(2, ((JsonArray)consumo["AppliedQuotaCharges"]!).Count);

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, consumo);

        Assert.Equal(("ACCEPTED", "RECORDED"), LotesDePrueba.ResultadoDe(acuse, consumo));
    }

    [Theory(DisplayName = "OWASP A08 · El esquema del tipo de evento del OpenAPI: un evento que no lo cumple resulta REJECTED/INVALID_EVENT y no se consolida")]
    [InlineData("una propiedad que el contrato no declara")]
    [InlineData("falta un requerido")]
    [InlineData("Quantity 2")]
    [InlineData("PayloadVersion 2")]
    [InlineData("un UUID ilegible")]
    [InlineData("una fecha de negocio sin formato")]
    [InlineData("un instante sin Z")]
    [InlineData("sin cargas")]
    [InlineData("una carga con una propiedad extra")]
    [InlineData("una moneda en minúsculas")]
    [InlineData("un EventType desconocido")]
    [InlineData("un motivo de denegación fuera del enum")]
    [InlineData("un estado de impresión fuera del enum")]
    public async Task Un_evento_fuera_del_esquema_se_rechaza(string caso)
    {
        var evento = caso switch
        {
            "un motivo de denegación fuera del enum" => LotesDePrueba.ConsumoDenegado(),
            "un estado de impresión fuera del enum" => LotesDePrueba.EventoImpresion(Guid.NewGuid()),
            _ => LotesDePrueba.ConsumoRegistrado(),
        };
        switch (caso)
        {
            case "una propiedad que el contrato no declara": evento["Credencial"] = "000123"; break;
            case "falta un requerido": evento.Remove("TicketId"); break;
            case "Quantity 2": evento["Quantity"] = 2; break;
            case "PayloadVersion 2": evento["PayloadVersion"] = 2; break;
            case "un UUID ilegible": evento["PersonId"] = "persona-1"; break;
            case "una fecha de negocio sin formato": evento["BusinessDate"] = "30/10/2026"; break;
            case "un instante sin Z": evento["LocalCommittedAtUtc"] = "2026-10-30T15:00:02.000-03:00"; break;
            case "sin cargas": evento["AppliedQuotaCharges"] = new JsonArray(); break;
            case "una carga con una propiedad extra": evento["AppliedQuotaCharges"]![0]!["Precio"] = 1; break;
            case "una moneda en minúsculas": evento["PriceSnapshot"]!["Currency"] = "ars"; break;
            case "un EventType desconocido": evento["EventType"] = "CONSUMPTION_REVERSED"; break;
            case "un motivo de denegación fuera del enum": evento["ReasonCode"] = "SERVER_DENIED"; break;
            case "un estado de impresión fuera del enum": evento["PrintState"] = "PRINTED"; break;
        }

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, evento);

        Assert.Equal(("REJECTED", "INVALID_EVENT"), LotesDePrueba.ResultadoDe(acuse, evento));
        Assert.Equal("REJECTED", await EstadoEntradaAsync(evento));
        if (evento["ConsumptionId"] is { } consumo && caso != "un estado de impresión fuera del enum")
        {
            Assert.Equal(0, await receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorConsumosConsolidados WHERE comevCodigoOrigen = @ev",
                ("@ev", LotesDePrueba.EventId(evento))));
        }
    }

    [Fact(DisplayName = "EventAck · Reenvío de un evento rechazado en otro lote: vuelve a ser REJECTED/INVALID_EVENT y sigue sin consolidarse")]
    public async Task Un_rechazado_reenviado_sigue_rechazado()
    {
        var consumo = LotesDePrueba.ConsumoRegistrado();
        consumo["Extra"] = true;
        using var primero = await EnviarAsync(receptor.Certificados.Puesto, consumo);

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, consumo);

        Assert.Equal(("REJECTED", "INVALID_EVENT"), LotesDePrueba.ResultadoDe(acuse, consumo));
        Assert.Equal(0, await ConsolidadosAsync(consumo));
    }

    [Fact(DisplayName = "PrintEventRecorded · HU03.1 · Evento de impresión de un consumo conocido del mismo puesto: ACCEPTED/RECORDED")]
    public async Task La_impresion_de_un_consumo_conocido_se_acepta()
    {
        var consumo = LotesDePrueba.ConsumoRegistrado();
        using var primero = await EnviarAsync(receptor.Certificados.Puesto, consumo);
        var impresion = LotesDePrueba.EventoImpresion(Guid.Parse((string)consumo["ConsumptionId"]!));

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, impresion);

        Assert.Equal(("ACCEPTED", "RECORDED"), LotesDePrueba.ResultadoDe(acuse, impresion));
        Assert.Equal("PROJECTED", await EstadoEntradaAsync(impresion));
    }

    [Fact(DisplayName = "SubmitSyncBatch · Evento de impresión antes que su consumo: las dos veces NEEDS_REVIEW/RELATED_EVENT_PENDING, nunca DUPLICATE")]
    public async Task La_impresion_antes_que_su_consumo_queda_en_revision()
    {
        var impresion = LotesDePrueba.EventoImpresion(Guid.NewGuid());

        using var primero = await EnviarAsync(receptor.Certificados.Puesto, impresion);
        using var segundo = await EnviarAsync(receptor.Certificados.Puesto, impresion);

        Assert.Equal(("NEEDS_REVIEW", "RELATED_EVENT_PENDING"), LotesDePrueba.ResultadoDe(primero, impresion));
        Assert.Equal(("NEEDS_REVIEW", "RELATED_EVENT_PENDING"), LotesDePrueba.ResultadoDe(segundo, impresion));
        Assert.Equal("PENDING_REFERENCE", await EstadoEntradaAsync(impresion));
    }

    [Fact(DisplayName = "SubmitSyncBatch · Llega la referencia: la impresión queda proyectada y el primer lote conserva su acuse original con NEEDS_REVIEW/RELATED_EVENT_PENDING")]
    public async Task Al_llegar_el_consumo_la_impresion_se_proyecta()
    {
        var consumo = LotesDePrueba.ConsumoRegistrado();
        var impresion = LotesDePrueba.EventoImpresion(Guid.Parse((string)consumo["ConsumptionId"]!));
        var primerLote = LotesDePrueba.Lote(impresion);
        using var cliente = await ClienteAsync(receptor.Certificados.Puesto);
        using var primera = await LotesDePrueba.EnviarAsync(cliente, primerLote);
        var acuseOriginal = await primera.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, consumo);

        Assert.Equal(("ACCEPTED", "RECORDED"), LotesDePrueba.ResultadoDe(acuse, consumo));
        Assert.Equal("PROJECTED", await EstadoEntradaAsync(impresion));
        using var reenvio = await LotesDePrueba.EnviarAsync(cliente, primerLote);
        Assert.Equal(acuseOriginal, await reenvio.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        using var original = JsonDocument.Parse(acuseOriginal);
        Assert.Equal(("NEEDS_REVIEW", "RELATED_EVENT_PENDING"), LotesDePrueba.ResultadoDe(original, impresion));
    }

    [Fact(DisplayName = "D-C24 · Incidencia de conectividad válida: NEEDS_REVIEW/MULTI_KIOSK_POLICY_REVIEW y queda guardada")]
    public async Task Una_incidencia_queda_en_revision()
    {
        var incidencia = LotesDePrueba.IncidenciaConectividad();

        using var acuse = await EnviarAsync(receptor.Certificados.Puesto, incidencia);

        Assert.Equal(("NEEDS_REVIEW", "MULTI_KIOSK_POLICY_REVIEW"), LotesDePrueba.ResultadoDe(acuse, incidencia));
        Assert.Equal("IN_REVIEW", await EstadoEntradaAsync(incidencia));
    }

    private async Task<HttpClient> ClienteAsync(X509Certificate2 certificado)
    {
        await receptor.AsegurarPuestoAsync(certificado);
        return receptor.CrearCliente(certificado);
    }

    /// <summary>Envía un lote nuevo con esos eventos y devuelve el acuse, que tiene que ser 201.</summary>
    private async Task<JsonDocument> EnviarAsync(X509Certificate2 certificado, params JsonObject[] eventos)
    {
        using var cliente = await ClienteAsync(certificado);
        using var respuesta = await LotesDePrueba.EnviarAsync(cliente, LotesDePrueba.Lote(eventos));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await LotesDePrueba.LeerAsync(respuesta);
    }

    private Task<int> ConsolidadosAsync(JsonNode consumo) =>
        receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorConsumosConsolidados WHERE comconCodigo = @con", ("@con", Guid.Parse((string)consumo["ConsumptionId"]!)));

    private Task<string> EstadoEntradaAsync(JsonNode evento) =>
        receptor.EscalarAsync<string>("SELECT comeveEstadoProyeccion FROM tbComedorEventosEntrada WHERE comevCodigo = @ev", ("@ev", LotesDePrueba.EventId(evento)));

    private Task<string> EntradaAsync(JsonNode evento) =>
        receptor.EscalarAsync<string>(
            "SELECT CONCAT(compueCodigo, '|', CONVERT(varchar(64), comeveHashContenido, 2), '|', comeveContenido, '|', comeveEstadoProyeccion) FROM tbComedorEventosEntrada WHERE comevCodigo = @ev",
            ("@ev", LotesDePrueba.EventId(evento)));

    private Task<string> ConsolidadoAsync(JsonNode consumo) =>
        receptor.EscalarAsync<string>(
            "SELECT CONCAT(comevCodigoOrigen, '|', compueCodigo, '|', comccoConsumoOriginal) FROM tbComedorConsumosConsolidados WHERE comconCodigo = @con",
            ("@con", Guid.Parse((string)consumo["ConsumptionId"]!)));

    private async Task<(string Tipo, string Estado, string Evidencia)> CasoAsync(JsonNode eventoOriginal)
    {
        var fila = await receptor.EscalarAsync<string>(
            "SELECT TOP 1 CONCAT(comcasTipo, '|', comcasEstado, '|', comcasEvidencia) FROM tbComedorCasosConciliacion WHERE comevCodigo = @ev",
            ("@ev", LotesDePrueba.EventId(eventoOriginal)));
        var partes = fila.Split('|', 3);
        return (partes[0], partes[1], partes[2]);
    }
}
