using System.Text.Json;

namespace Intelektron.INComedor.Sync.Api;

/// <summary>
/// Errores con la forma de ProblemDetails del contrato (D8): type, title fijo por código, status, code y un
/// correlationId nuevo repetido en X-Correlation-Id. Nunca llevan detail ni nada del pedido.
/// </summary>
internal sealed class ProblemasReceptor : IResult
{
    private static readonly Dictionary<string, (int Estado, string Titulo)> Codigos = new()
    {
        ["INVALID_REQUEST"] = (400, "El pedido no es válido"),
        ["IDEMPOTENCY_KEY_MISMATCH"] = (400, "La clave de idempotencia no es el BatchId"),
        ["EQUIPMENT_AUTH_REQUIRED"] = (401, "Se requiere la credencial del equipo"),
        ["EQUIPMENT_FORBIDDEN"] = (403, "El equipo no tiene habilitada la comunicación"),
        ["BATCH_NOT_FOUND"] = (404, "El lote no existe para este equipo"),
        ["BATCH_PAYLOAD_CONFLICT"] = (409, "El lote ya existe con otro contenido"),
        ["INVALID_BATCH"] = (422, "El lote no cumple el contrato"),
        ["SERVICE_UNAVAILABLE"] = (503, "El receptor no está disponible"),
    };

    private ProblemasReceptor(string codigo)
    {
        Codigo = codigo;
        (Estado, Titulo) = Codigos[codigo];
    }

    public string Codigo { get; }

    public int Estado { get; }

    public string Titulo { get; }

    public Guid CorrelationId { get; } = Guid.NewGuid();

    public static ProblemasReceptor Crear(string codigo) => new(codigo);

    public async Task ExecuteAsync(HttpContext contexto)
    {
        contexto.Response.StatusCode = Estado;
        contexto.Response.ContentType = "application/problem+json";
        contexto.Response.Headers["X-Correlation-Id"] = CorrelationId.ToString();
        if (Estado == StatusCodes.Status503ServiceUnavailable)
        {
            contexto.Response.Headers.RetryAfter = "2";
        }

        await JsonSerializer.SerializeAsync(contexto.Response.Body, new
        {
            type = "https://api.incomedor.example/problems/" + Codigo.ToLowerInvariant().Replace('_', '-'),
            title = Titulo,
            status = Estado,
            code = Codigo,
            correlationId = CorrelationId,
        }, cancellationToken: contexto.RequestAborted);
    }
}
