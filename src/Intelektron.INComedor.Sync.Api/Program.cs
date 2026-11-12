using Intelektron.INComedor.Sync.Api;
using Intelektron.INComedor.Sync.Api.Ingesta;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Receptor de lotes (TK01.1): las dos operaciones del OpenAPI, sólo para equipos autenticados por certificado (D2).
const string RutaLotes = "/api/incomedor/v1/sync-batches";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel((contexto, kestrel) =>
{
    kestrel.ConfigureHttpsDefaults(https =>
    {
        // Pide el certificado de equipo sin cortar el handshake: la cadena, la vigencia y el registro los valida
        // FiltroEquipo, para responder 401 o 403 en lugar de cerrar la conexión sin respuesta (D2).
        https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
        https.AllowAnyClientCertificate();
    });
    if (DemostracionReceptor.Activa(contexto.Configuration, args))
    {
        DemostracionReceptor.Escuchar(kestrel, contexto.Configuration);
    }
});
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddSingleton(servicios =>
{
    var configuracion = servicios.GetRequiredService<IConfiguration>();
    return DemostracionReceptor.Activa(configuracion, args)
        ? DemostracionReceptor.Configuracion(configuracion)
        : new ConfiguracionReceptor(configuracion);
});
builder.Services.AddSingleton<IngestaLotes>();

var app = builder.Build();

// Con --demostracion: certificados, base y puesto registrado, antes de que Kestrel cargue el certificado (D12).
if (DemostracionReceptor.Activa(app.Configuration, args))
{
    DemostracionReceptor.Preparar(app.Configuration);
}

// Una excepción no prevista responde 503 SERVICE_UNAVAILABLE con Retry-After: el puesto reintenta el mismo lote, y
// eso es inocuo porque el lote es idempotente. El registro lleva el correlationId y el tipo, nunca el cuerpo (D8).
app.UseExceptionHandler(rama => rama.Run(async contexto =>
{
    var problema = ProblemasReceptor.Crear("SERVICE_UNAVAILABLE");
    var tipo = contexto.Features.Get<IExceptionHandlerFeature>()?.Error.GetType().FullName;
    contexto.RequestServices.GetRequiredService<ILogger<Program>>()
        .LogError("Excepción no prevista {Tipo}. CorrelationId {CorrelationId}", tipo, problema.CorrelationId);
    await problema.ExecuteAsync(contexto);
}));

if (app.Services.GetRequiredService<ConfiguracionReceptor>().CadenaConexion is { } cadenaConexion)
{
    MigracionReceptor.Aplicar(cadenaConexion);
}

var lotes = app.MapGroup(RutaLotes).AddEndpointFilter<FiltroEquipo>();
lotes.MapPost("", async (HttpContext contexto, IngestaLotes ingesta) =>
{
    var cuerpo = await LeerCuerpoAsync(contexto.Request, IngestaLotes.TopeCuerpo);
    if (cuerpo is null)
    {
        return ProblemasReceptor.Crear("INVALID_REQUEST");
    }

    var clave = contexto.Request.Headers["Idempotency-Key"];
    return await ingesta.ProcesarAsync(FiltroEquipo.PuestoAutenticado(contexto), clave.Count == 1 ? clave[0] : null, cuerpo, contexto.RequestAborted);
});
lotes.MapGet("/{batchId}", async Task<IResult> (string batchId, HttpContext contexto, IngestaLotes ingesta) =>
{
    if (!Guid.TryParseExact(batchId, "D", out var lote))
    {
        return ProblemasReceptor.Crear("INVALID_REQUEST");
    }

    return await ingesta.ConsultarAsync(FiltroEquipo.PuestoAutenticado(contexto), lote, contexto.RequestAborted) is { } acuse
        ? new RespuestaAcuse(acuse, StatusCodes.Status200OK)
        : ProblemasReceptor.Crear("BATCH_NOT_FOUND");
});

app.Run();

// El cuerpo con un tope propio de 1 MiB (D5): más grande, o cortado por Kestrel, es null y el pedido responde 400.
static async Task<byte[]?> LeerCuerpoAsync(HttpRequest pedido, int tope)
{
    if (pedido.ContentLength > tope)
    {
        return null;
    }

    using var memoria = new MemoryStream();
    var bloque = new byte[16 * 1024];
    try
    {
        int leidos;
        while ((leidos = await pedido.Body.ReadAsync(bloque, pedido.HttpContext.RequestAborted)) > 0)
        {
            if (memoria.Length + leidos > tope)
            {
                return null;
            }

            memoria.Write(bloque, 0, leidos);
        }
    }
    catch (BadHttpRequestException)
    {
        return null;
    }

    return memoria.ToArray();
}

public partial class Program;
