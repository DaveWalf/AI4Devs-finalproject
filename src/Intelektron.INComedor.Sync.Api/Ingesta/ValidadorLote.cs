using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Intelektron.INComedor.Sync.Api.Ingesta;

/// <summary>
/// Validación propia contra api/openapi.yaml, en dos niveles (D5). El lote como un todo: si no cumple SyncBatchRequest,
/// 422 INVALID_BATCH sin escribir nada. Cada evento: el esquema de su tipo (discriminador, requeridos,
/// additionalProperties: false, constantes, enums, formatos y rangos) más la igualdad de BusinessDate de AT50; si no
/// cumple, REJECTED/INVALID_EVENT. El nivel del pedido (JSON legible, sin propiedades repetidas, sólo enteros) ya lo
/// resolvió JsonCanonico.
/// </summary>
internal static partial class ValidadorLote
{
    public const int MaximoEventos = 500;

    private const long MayorEntero = 9007199254740991;

    private static readonly Campo[] CamposLote =
    [
        new("BatchId", Uuid),
        new("SchemaVersion", Constante(1)),
        new("CreatedAtUtc", Instante),
        new("Events", e => e.ValueKind == JsonValueKind.Array),
    ];

    private static readonly Campo[] CamposCarga =
    [
        new("QuotaScopeId", Uuid),
        new("QuotaBucketId", Uuid),
        new("Units", Entero(1)),
        new("BusinessDate", Fecha),
    ];

    private static readonly Campo[] CamposPrecio =
    [
        new("Currency", e => Texto(e) is { } s && Moneda().IsMatch(s)),
        new("Scale", Entero(0, 6)),
        new("EmployeeChargeMinor", Entero(0)),
    ];

    private static readonly Dictionary<string, (Campo[] Requeridos, Campo[] Opcionales)> Esquemas = new()
    {
        ["CONSUMPTION_RECORDED"] = (
        [
            new("EventId", Uuid), new("EventType", Constante("CONSUMPTION_RECORDED")), new("PayloadVersion", Constante(1)),
            new("IntentId", Uuid), new("DecisionId", Uuid), new("ConsumptionId", Uuid), new("TicketId", Uuid),
            new("PersonId", Uuid), new("MenuItemId", Uuid), new("MenuVersion", Entero(1)), new("Quantity", Constante(1)),
            new("BusinessDate", Fecha),
            new("AppliedQuotaCharges", e => e.ValueKind == JsonValueKind.Array && e.GetArrayLength() >= 1
                && e.EnumerateArray().All(c => Objeto(c, CamposCarga, []))),
            new("LocalCommittedAtUtc", Instante), new("ClientSequence", Entero(1)), new("ConfigurationVersion", Entero(1)),
            new("CatalogVersion", Entero(1)), new("PolicyVersion", Entero(1)),
            new("PriceSnapshot", e => Objeto(e, CamposPrecio, [])),
        ],
        [new("ConnectivityIncidentId", Uuid)]),

        ["CONSUMPTION_DENIED"] = (
        [
            new("EventId", Uuid), new("EventType", Constante("CONSUMPTION_DENIED")), new("PayloadVersion", Constante(1)),
            new("IntentId", Uuid), new("DecisionId", Uuid),
            new("ReasonCode", Uno("LOCAL_LIMIT_REACHED", "PERSON_INACTIVE", "CREDENTIAL_REVOKED", "CREDENTIAL_EXPIRED", "RULE_DENIED",
                "SERVICE_NOT_ENABLED", "IDENTITY_NOT_RESOLVED", "CATALOG_UNAVAILABLE", "MULTI_KIOSK_COMMUNICATION_REQUIRED")),
            new("LocalCommittedAtUtc", Instante), new("ConfigurationVersion", Entero(1)),
        ],
        []),

        ["PRINT_EVENT_RECORDED"] = (
        [
            new("EventId", Uuid), new("EventType", Constante("PRINT_EVENT_RECORDED")), new("PayloadVersion", Constante(1)),
            new("TicketId", Uuid), new("ConsumptionId", Uuid), new("CopyNumber", Entero(0)),
            new("PrintState", Uno("QUEUED", "SUBMITTED", "FAILED", "UNKNOWN")), new("OccurredAtUtc", Instante),
        ],
        []),

        ["CONNECTIVITY_INCIDENT_RECORDED"] = (
        [
            new("EventId", Uuid), new("EventType", Constante("CONNECTIVITY_INCIDENT_RECORDED")), new("PayloadVersion", Constante(1)),
            new("IncidentId", Uuid), new("Phase", Uno("OPENED", "RECOVERED")), new("OccurredAtUtc", Instante),
            new("ConfigurationVersion", Entero(1)), new("MultiKioskOfflinePolicy", Uno("CONTINUE_LOCAL", "SUSPEND_NEW_CONSUMPTIONS")),
            new("FailureKind", Uno("OUTBOUND", "PEER_FEED", "BOTH")),
        ],
        []),
    };

    /// <summary>El BatchId, si el cuerpo es un objeto con un BatchId UUID; hace falta antes de validar el resto (D3).</summary>
    public static Guid? BatchIdLegible(JsonElement raiz) =>
        raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("BatchId", out var lote) && Uuid(lote)
            ? lote.GetGuid()
            : null;

    /// <summary>
    /// SyncBatchRequest como un todo: las cuatro propiedades y ninguna otra, SchemaVersion 1, CreatedAtUtc con Z, entre 1
    /// y 500 eventos, y cada evento un objeto con un EventId UUID que no se repite en el lote.
    /// </summary>
    public static bool LoteValido(JsonElement raiz)
    {
        if (!Objeto(raiz, CamposLote, []))
        {
            return false;
        }

        var eventos = raiz.GetProperty("Events");
        if (eventos.GetArrayLength() is < 1 or > MaximoEventos)
        {
            return false;
        }

        var vistos = new HashSet<Guid>();
        foreach (var evento in eventos.EnumerateArray())
        {
            if (evento.ValueKind != JsonValueKind.Object || !evento.TryGetProperty("EventId", out var id) || !Uuid(id) || !vistos.Add(id.GetGuid()))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>El evento cumple el esquema de su tipo y, si es un consumo, cada carga tiene la BusinessDate del consumo (AT50).</summary>
    public static bool EventoValido(JsonElement evento)
    {
        if (!evento.TryGetProperty("EventType", out var tipo) || Texto(tipo) is not { } nombre
            || !Esquemas.TryGetValue(nombre, out var esquema) || !Objeto(evento, esquema.Requeridos, esquema.Opcionales))
        {
            return false;
        }

        if (nombre != "CONSUMPTION_RECORDED")
        {
            return true;
        }

        var fecha = evento.GetProperty("BusinessDate").GetString();
        return evento.GetProperty("AppliedQuotaCharges").EnumerateArray().All(c => c.GetProperty("BusinessDate").GetString() == fecha);
    }

    private static bool Objeto(JsonElement elemento, Campo[] requeridos, Campo[] opcionales)
    {
        if (elemento.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var presentes = 0;
        foreach (var propiedad in elemento.EnumerateObject())
        {
            var campo = requeridos.FirstOrDefault(c => c.Nombre == propiedad.Name);
            if (campo is not null)
            {
                presentes++;
            }
            else
            {
                campo = opcionales.FirstOrDefault(c => c.Nombre == propiedad.Name);
            }

            if (campo is null || !campo.Valido(propiedad.Value))
            {
                return false;
            }
        }

        return presentes == requeridos.Length;
    }

    private static string? Texto(JsonElement elemento) => elemento.ValueKind == JsonValueKind.String ? elemento.GetString() : null;

    private static bool Uuid(JsonElement elemento) => Texto(elemento) is { } s && Guid.TryParseExact(s, "D", out _);

    private static bool Fecha(JsonElement elemento) =>
        Texto(elemento) is { } s && FechaIso().IsMatch(s) && DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool Instante(JsonElement elemento) =>
        Texto(elemento) is { } s && InstanteUtc().IsMatch(s)
        && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _);

    private static Func<JsonElement, bool> Entero(long minimo, long maximo = MayorEntero) =>
        e => e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var valor) && valor >= minimo && valor <= maximo;

    private static Func<JsonElement, bool> Constante(long valor) => e => e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var v) && v == valor;

    private static Func<JsonElement, bool> Constante(string valor) => e => Texto(e) == valor;

    private static Func<JsonElement, bool> Uno(params string[] valores) => e => Texto(e) is { } s && valores.Contains(s, StringComparer.Ordinal);

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex FechaIso();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$")]
    private static partial Regex InstanteUtc();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex Moneda();

    private sealed record Campo(string Nombre, Func<JsonElement, bool> Valido);
}
