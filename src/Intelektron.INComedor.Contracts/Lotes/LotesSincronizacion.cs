using System.Text.Json;
using System.Text.Json.Serialization;

namespace Intelektron.INComedor.Contracts.Lotes;

// Records públicos con los nombres de SyncBatchRequest, CompletedBatchAck y EventAck del OpenAPI. Los
// eventos del lote quedan como JsonElement: el receptor valida cada uno contra el esquema de su tipo
// (D5) y el puesto los copia tal como los guardó su outbox.

public sealed record SyncBatchRequest(
    [property: JsonPropertyName("BatchId")] Guid BatchId,
    [property: JsonPropertyName("SchemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("CreatedAtUtc")] string CreatedAtUtc,
    [property: JsonPropertyName("Events")] IReadOnlyList<JsonElement> Events);

public sealed record CompletedBatchAck(
    [property: JsonPropertyName("BatchId")] Guid BatchId,
    [property: JsonPropertyName("Status")] string Status,
    [property: JsonPropertyName("ServerAcknowledgedAtUtc")] string ServerAcknowledgedAtUtc,
    [property: JsonPropertyName("Results")] IReadOnlyList<EventAck> Results,
    [property: JsonPropertyName("CorrelationId")] Guid CorrelationId);

public sealed record EventAck(
    [property: JsonPropertyName("EventId")] Guid EventId,
    [property: JsonPropertyName("Status")] string Status,
    [property: JsonPropertyName("ReasonCode")] string ReasonCode);
