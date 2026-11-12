using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Intelektron.INComedor.Sync.Api.Tests;

/// <summary>
/// Lotes y eventos sintéticos con la forma del OpenAPI (SyncBatchRequest y los cuatro tipos de SyncEvent). Son nodos
/// JSON para que cada prueba pueda cambiar lo que le interesa: una fecha, una propiedad extra, otro contenido.
/// </summary>
public static class LotesDePrueba
{
    public const string RutaLotes = "/api/incomedor/v1/sync-batches";
    public const string FechaNegocio = "2026-10-30";

    public static JsonObject ConsumoRegistrado(Guid? eventId = null, Guid? consumptionId = null, int menuVersion = 1) => new()
    {
        ["EventId"] = (eventId ?? Guid.NewGuid()).ToString(),
        ["EventType"] = "CONSUMPTION_RECORDED",
        ["PayloadVersion"] = 1,
        ["IntentId"] = Guid.NewGuid().ToString(),
        ["DecisionId"] = Guid.NewGuid().ToString(),
        ["ConsumptionId"] = (consumptionId ?? Guid.NewGuid()).ToString(),
        ["TicketId"] = Guid.NewGuid().ToString(),
        ["PersonId"] = Guid.NewGuid().ToString(),
        ["MenuItemId"] = Guid.NewGuid().ToString(),
        ["MenuVersion"] = menuVersion,
        ["Quantity"] = 1,
        ["BusinessDate"] = FechaNegocio,
        ["AppliedQuotaCharges"] = new JsonArray(Carga(), Carga()),
        ["LocalCommittedAtUtc"] = "2026-10-30T15:00:02.000Z",
        ["ClientSequence"] = 1,
        ["ConfigurationVersion"] = 1,
        ["CatalogVersion"] = 1,
        ["PolicyVersion"] = 1,
        ["PriceSnapshot"] = new JsonObject { ["Currency"] = "ARS", ["Scale"] = 2, ["EmployeeChargeMinor"] = 150000 },
    };

    public static JsonObject ConsumoDenegado(Guid? eventId = null) => new()
    {
        ["EventId"] = (eventId ?? Guid.NewGuid()).ToString(),
        ["EventType"] = "CONSUMPTION_DENIED",
        ["PayloadVersion"] = 1,
        ["IntentId"] = Guid.NewGuid().ToString(),
        ["DecisionId"] = Guid.NewGuid().ToString(),
        ["ReasonCode"] = "LOCAL_LIMIT_REACHED",
        ["LocalCommittedAtUtc"] = "2026-10-30T15:00:02.000Z",
        ["ConfigurationVersion"] = 1,
    };

    public static JsonObject EventoImpresion(Guid consumptionId, Guid? eventId = null) => new()
    {
        ["EventId"] = (eventId ?? Guid.NewGuid()).ToString(),
        ["EventType"] = "PRINT_EVENT_RECORDED",
        ["PayloadVersion"] = 1,
        ["TicketId"] = Guid.NewGuid().ToString(),
        ["ConsumptionId"] = consumptionId.ToString(),
        ["CopyNumber"] = 0,
        ["PrintState"] = "SUBMITTED",
        ["OccurredAtUtc"] = "2026-10-30T15:00:03.000Z",
    };

    public static JsonObject IncidenciaConectividad(Guid? eventId = null) => new()
    {
        ["EventId"] = (eventId ?? Guid.NewGuid()).ToString(),
        ["EventType"] = "CONNECTIVITY_INCIDENT_RECORDED",
        ["PayloadVersion"] = 1,
        ["IncidentId"] = Guid.NewGuid().ToString(),
        ["Phase"] = "OPENED",
        ["OccurredAtUtc"] = "2026-10-30T15:00:00.000Z",
        ["ConfigurationVersion"] = 1,
        ["MultiKioskOfflinePolicy"] = "CONTINUE_LOCAL",
        ["FailureKind"] = "OUTBOUND",
    };

    public static JsonObject Lote(params JsonObject[] eventos) => new()
    {
        ["BatchId"] = Guid.NewGuid().ToString(),
        ["SchemaVersion"] = 1,
        ["CreatedAtUtc"] = "2026-10-30T15:10:00.000Z",
        ["Events"] = new JsonArray(eventos.Select(e => (JsonNode)e.DeepClone()).ToArray()),
    };

    public static Guid BatchId(JsonObject lote) => Guid.Parse((string)lote["BatchId"]!);

    public static Guid EventId(JsonNode evento) => Guid.Parse((string)evento["EventId"]!);

    /// <summary>Envía el lote con Idempotency-Key igual a su BatchId, salvo que se indique otra clave.</summary>
    public static Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, JsonObject lote, string? clave = null) =>
        EnviarTextoAsync(cliente, lote.ToJsonString(), clave ?? (string)lote["BatchId"]!);

    public static async Task<HttpResponseMessage> EnviarTextoAsync(HttpClient cliente, string cuerpo, string? clave)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, RutaLotes)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, new MediaTypeHeaderValue("application/json")),
        };
        if (clave is not null)
        {
            pedido.Headers.Add("Idempotency-Key", clave);
        }

        return await cliente.SendAsync(pedido, TestContext.Current.CancellationToken);
    }

    /// <summary>Los resultados del acuse por EventId; si un EventId se repite, la prueba lo ve al contar.</summary>
    public static List<(Guid EventId, string Status, string ReasonCode)> Resultados(JsonDocument acuse) =>
        acuse.RootElement.GetProperty("Results").EnumerateArray()
            .Select(r => (r.GetProperty("EventId").GetGuid(), r.GetProperty("Status").GetString()!, r.GetProperty("ReasonCode").GetString()!))
            .ToList();

    public static (string Status, string ReasonCode) ResultadoDe(JsonDocument acuse, JsonNode evento) =>
        Resultados(acuse).Where(r => r.EventId == EventId(evento)).Select(r => (r.Status, r.ReasonCode)).Single();

    public static async Task<JsonDocument> LeerAsync(HttpResponseMessage respuesta) =>
        JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    private static JsonObject Carga() => new()
    {
        ["QuotaScopeId"] = Guid.NewGuid().ToString(),
        ["QuotaBucketId"] = Guid.NewGuid().ToString(),
        ["Units"] = 1,
        ["BusinessDate"] = FechaNegocio,
    };
}
