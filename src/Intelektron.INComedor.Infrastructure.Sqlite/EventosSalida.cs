using System.Text.Json.Serialization;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

// Records internos con los nombres de ConsumptionRecorded, AppliedQuotaCharge, PriceSnapshot y
// ConsumptionDenied del OpenAPI, PayloadVersion 1 (D9). Ninguno lleva la credencial ni el código.

internal sealed record PriceSnapshotEvento(
    [property: JsonPropertyName("Currency")] string Currency,
    [property: JsonPropertyName("Scale")] int Scale,
    [property: JsonPropertyName("EmployeeChargeMinor")] long EmployeeChargeMinor);

internal sealed record AppliedQuotaChargeEvento(
    [property: JsonPropertyName("QuotaScopeId")] Guid QuotaScopeId,
    [property: JsonPropertyName("QuotaBucketId")] Guid QuotaBucketId,
    [property: JsonPropertyName("Units")] int Units,
    [property: JsonPropertyName("BusinessDate")] string BusinessDate);

internal sealed record ConsumptionRecordedEvento(
    [property: JsonPropertyName("EventId")] Guid EventId,
    [property: JsonPropertyName("EventType")] string EventType,
    [property: JsonPropertyName("PayloadVersion")] int PayloadVersion,
    [property: JsonPropertyName("IntentId")] Guid IntentId,
    [property: JsonPropertyName("DecisionId")] Guid DecisionId,
    [property: JsonPropertyName("ConsumptionId")] Guid ConsumptionId,
    [property: JsonPropertyName("TicketId")] Guid TicketId,
    [property: JsonPropertyName("PersonId")] Guid PersonId,
    [property: JsonPropertyName("MenuItemId")] Guid MenuItemId,
    [property: JsonPropertyName("MenuVersion")] int MenuVersion,
    [property: JsonPropertyName("Quantity")] int Quantity,
    [property: JsonPropertyName("BusinessDate")] string BusinessDate,
    [property: JsonPropertyName("AppliedQuotaCharges")] IReadOnlyList<AppliedQuotaChargeEvento> AppliedQuotaCharges,
    [property: JsonPropertyName("LocalCommittedAtUtc")] string LocalCommittedAtUtc,
    [property: JsonPropertyName("ClientSequence")] long ClientSequence,
    [property: JsonPropertyName("ConfigurationVersion")] int ConfigurationVersion,
    [property: JsonPropertyName("CatalogVersion")] int CatalogVersion,
    [property: JsonPropertyName("PolicyVersion")] int PolicyVersion,
    [property: JsonPropertyName("PriceSnapshot")] PriceSnapshotEvento PriceSnapshot);

internal sealed record ConsumptionDeniedEvento(
    [property: JsonPropertyName("EventId")] Guid EventId,
    [property: JsonPropertyName("EventType")] string EventType,
    [property: JsonPropertyName("PayloadVersion")] int PayloadVersion,
    [property: JsonPropertyName("IntentId")] Guid IntentId,
    [property: JsonPropertyName("DecisionId")] Guid DecisionId,
    [property: JsonPropertyName("ReasonCode")] string ReasonCode,
    [property: JsonPropertyName("LocalCommittedAtUtc")] string LocalCommittedAtUtc,
    [property: JsonPropertyName("ConfigurationVersion")] int ConfigurationVersion);
