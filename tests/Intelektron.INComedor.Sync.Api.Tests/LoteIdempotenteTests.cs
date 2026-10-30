using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Intelektron.INComedor.Sync.Api.Tests;

/// <summary>
/// Lote idempotente por BatchId y lote inválido sin efectos (D3, D4 y D5 de recibir-lotes-idempotente), contra el
/// receptor real con TLS mutuo y SQL Server. Cada prueba mira sólo sus lotes y sus eventos, porque la base es de la clase.
/// </summary>
public class LoteIdempotenteTests(ReceptorConKestrel receptor) : IClassFixture<ReceptorConKestrel>
{
    [Fact(DisplayName = "AT27 · HU02-C · Lote nuevo: 201 COMPLETED con exactamente un resultado por EventId, y X-Correlation-Id igual al CorrelationId del cuerpo")]
    public async Task Un_lote_nuevo_responde_201_con_un_resultado_por_evento()
    {
        using var cliente = await ClienteDelPuestoAsync();
        var lote = LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado(), LotesDePrueba.ConsumoDenegado());

        using var respuesta = await LotesDePrueba.EnviarAsync(cliente, lote);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal("application/json", respuesta.Content.Headers.ContentType?.MediaType);
        using var acuse = await LotesDePrueba.LeerAsync(respuesta);
        Assert.Equal("COMPLETED", acuse.RootElement.GetProperty("Status").GetString());
        Assert.Equal(LotesDePrueba.BatchId(lote), acuse.RootElement.GetProperty("BatchId").GetGuid());
        Assert.EndsWith("Z", acuse.RootElement.GetProperty("ServerAcknowledgedAtUtc").GetString());
        var esperados = ((JsonArray)lote["Events"]!).Select(e => LotesDePrueba.EventId(e!)).Order();
        Assert.Equal(esperados, LotesDePrueba.Resultados(acuse).Select(r => r.EventId).Order());
        Assert.Equal(acuse.RootElement.GetProperty("CorrelationId").GetGuid().ToString(), Assert.Single(respuesta.Headers.GetValues("X-Correlation-Id")));
        Assert.Equal(1, await RecibosAsync(lote));
    }

    [Fact(DisplayName = "AT27 · HU02-D · Reenvío tras perder el acuse: 200 con el mismo acuse byte por byte, sin más recibos, eventos ni consumos")]
    public async Task El_reenvio_devuelve_el_acuse_original()
    {
        using var cliente = await ClienteDelPuestoAsync();
        var consumo = LotesDePrueba.ConsumoRegistrado();
        var lote = LotesDePrueba.Lote(consumo, LotesDePrueba.ConsumoDenegado());
        using var primera = await LotesDePrueba.EnviarAsync(cliente, lote);
        var original = await primera.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var antes = await ConteosAsync();

        using var segunda = await LotesDePrueba.EnviarAsync(cliente, lote);

        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.Equal(original, await segunda.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(antes, await ConteosAsync());
        using var acuse = JsonDocument.Parse(original);
        Assert.Equal(acuse.RootElement.GetProperty("CorrelationId").GetGuid().ToString(), Assert.Single(segunda.Headers.GetValues("X-Correlation-Id")));
    }

    [Fact(DisplayName = "AT27 · SubmitSyncBatch (RFC 8785) · Mismo contenido con otro orden de propiedades y otros espacios: 200 con el acuse original")]
    public async Task Otro_orden_y_otros_espacios_son_el_mismo_lote()
    {
        using var cliente = await ClienteDelPuestoAsync();
        var lote = LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado());
        using var primera = await LotesDePrueba.EnviarAsync(cliente, lote);
        var original = await primera.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var reordenado = new JsonObject();
        foreach (var propiedad in lote.Reverse())
        {
            reordenado[propiedad.Key] = propiedad.Value!.DeepClone();
        }

        using var segunda = await LotesDePrueba.EnviarTextoAsync(cliente,
            reordenado.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), (string)lote["BatchId"]!);

        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.Equal(original, await segunda.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = "TK01.1 · Misma clave con otro cuerpo: 409 BATCH_PAYLOAD_CONFLICT y el recibo, su acuse y los eventos originales quedan iguales")]
    public async Task Misma_clave_con_otro_cuerpo_es_conflicto()
    {
        using var cliente = await ClienteDelPuestoAsync();
        var consumo = LotesDePrueba.ConsumoRegistrado();
        var lote = LotesDePrueba.Lote(consumo);
        using var primera = await LotesDePrueba.EnviarAsync(cliente, lote);
        var acuseAntes = await AcuseGuardadoAsync(lote);
        var otro = LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado());
        otro["BatchId"] = (string)lote["BatchId"]!;

        using var respuesta = await LotesDePrueba.EnviarAsync(cliente, otro);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("BATCH_PAYLOAD_CONFLICT", await CodigoAsync(respuesta));
        Assert.Equal(acuseAntes, await AcuseGuardadoAsync(lote));
        Assert.Equal(1, await EventosAsync(consumo));
        Assert.Equal(0, await EventosAsync(((JsonArray)otro["Events"]!)[0]!));
    }

    [Fact(DisplayName = "TK01.1 · Clave distinta del BatchId: 400 IDEMPOTENCY_KEY_MISMATCH y nada guardado")]
    public async Task Clave_distinta_del_batch_id_es_400()
    {
        using var cliente = await ClienteDelPuestoAsync();
        var consumo = LotesDePrueba.ConsumoRegistrado();
        var lote = LotesDePrueba.Lote(consumo);

        using var respuesta = await LotesDePrueba.EnviarAsync(cliente, lote, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_MISMATCH", await CodigoAsync(respuesta));
        Assert.Equal(0, await RecibosAsync(lote));
        Assert.Equal(0, await EventosAsync(consumo));
    }

    [Fact(DisplayName = "AT27 · RB16 · El mismo lote dos veces a la vez: un solo recibo y un solo consumo consolidado, y las dos respuestas llevan el mismo acuse")]
    public async Task El_mismo_lote_en_paralelo_se_ingiere_una_vez()
    {
        using var cliente = await ClienteDelPuestoAsync();
        var consumo = LotesDePrueba.ConsumoRegistrado();
        var lote = LotesDePrueba.Lote(consumo);

        var respuestas = await Task.WhenAll(LotesDePrueba.EnviarAsync(cliente, lote), LotesDePrueba.EnviarAsync(cliente, lote));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Created], respuestas.Select(r => r.StatusCode).Order());
        var cuerpos = await Task.WhenAll(respuestas.Select(r => r.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(cuerpos[0], cuerpos[1]);
        Assert.Equal(1, await RecibosAsync(lote));
        Assert.Equal(1, await ConsolidadosAsync(consumo));
        foreach (var respuesta in respuestas)
        {
            respuesta.Dispose();
        }
    }

    [Theory(DisplayName = "SyncBatchRequest · Lote inválido sin efectos: 422 INVALID_BATCH y nada guardado")]
    [InlineData("EventId repetido dentro del lote")]
    [InlineData("SchemaVersion 2")]
    [InlineData("501 eventos")]
    [InlineData("sin eventos")]
    [InlineData("un evento sin EventId legible")]
    [InlineData("una propiedad que SyncBatchRequest no declara")]
    public async Task Un_lote_invalido_no_guarda_nada(string caso)
    {
        using var cliente = await ClienteDelPuestoAsync();
        var consumo = LotesDePrueba.ConsumoRegistrado();
        var lote = caso switch
        {
            "EventId repetido dentro del lote" => LotesDePrueba.Lote(consumo, LotesDePrueba.ConsumoDenegado(LotesDePrueba.EventId(consumo))),
            "501 eventos" => LotesDePrueba.Lote([consumo, .. Enumerable.Range(0, 500).Select(_ => LotesDePrueba.ConsumoDenegado())]),
            "sin eventos" => LotesDePrueba.Lote(),
            _ => LotesDePrueba.Lote(consumo),
        };
        switch (caso)
        {
            case "SchemaVersion 2":
                lote["SchemaVersion"] = 2;
                break;
            case "un evento sin EventId legible":
                ((JsonArray)lote["Events"]!).Add(new JsonObject { ["EventId"] = "no-es-un-uuid", ["EventType"] = "CONSUMPTION_DENIED" });
                break;
            case "una propiedad que SyncBatchRequest no declara":
                lote["Items"] = new JsonArray();
                break;
        }

        using var respuesta = await LotesDePrueba.EnviarAsync(cliente, lote);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        Assert.Equal("INVALID_BATCH", await CodigoAsync(respuesta));
        Assert.Equal(0, await RecibosAsync(lote));
        Assert.Equal(0, await EventosAsync(consumo));
    }

    private async Task<HttpClient> ClienteDelPuestoAsync()
    {
        await receptor.AsegurarPuestoAsync(receptor.Certificados.Puesto);
        return receptor.CrearCliente(receptor.Certificados.Puesto);
    }

    private Task<int> RecibosAsync(JsonObject lote) =>
        receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorAcusesLotes WHERE comlotCodigo = @lot", ("@lot", LotesDePrueba.BatchId(lote)));

    private Task<int> EventosAsync(JsonNode evento) =>
        receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorEventosEntrada WHERE comevCodigo = @ev", ("@ev", LotesDePrueba.EventId(evento)));

    private Task<int> ConsolidadosAsync(JsonNode consumo) =>
        receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorConsumosConsolidados WHERE comconCodigo = @con", ("@con", Guid.Parse((string)consumo["ConsumptionId"]!)));

    private Task<string> AcuseGuardadoAsync(JsonObject lote) =>
        receptor.EscalarAsync<string>("SELECT comaclContenidoAcuse FROM tbComedorAcusesLotes WHERE comlotCodigo = @lot", ("@lot", LotesDePrueba.BatchId(lote)));

    private async Task<(int Recibos, int Eventos, int Consolidados)> ConteosAsync() =>
        (await receptor.ContarAsync("tbComedorAcusesLotes"), await receptor.ContarAsync("tbComedorEventosEntrada"), await receptor.ContarAsync("tbComedorConsumosConsolidados"));

    private static async Task<string?> CodigoAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        using var problema = await LotesDePrueba.LeerAsync(respuesta);
        return problema.RootElement.GetProperty("code").GetString();
    }
}
