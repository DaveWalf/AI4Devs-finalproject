namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Intención que llega a confirmarse; la sesión de identidad llega ya abierta (D12).</summary>
public sealed record SolicitudConfirmacion(
    Guid IntentId,
    Guid IdentitySessionId,
    Guid PersonId,
    Guid MenuItemId,
    int MenuVersion,
    int Quantity);

public enum TipoResultado
{
    Autorizada,
    Denegada,
    Conflicto,
    CatalogoCambiado,
    SesionNoUtilizable,
}

/// <summary>
/// Resultado de una confirmación. Autorizada y Denegada son decisiones durables y se guardan como
/// <c>ResultPayload</c>; los demás tipos no escriben nada.
/// </summary>
public sealed record ResultadoConfirmacion(
    TipoResultado Tipo,
    Guid? DecisionId = null,
    Guid? ConsumptionId = null,
    Guid? TicketId = null,
    string? ReasonCode = null);
