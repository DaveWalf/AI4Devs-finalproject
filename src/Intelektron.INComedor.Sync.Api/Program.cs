using Intelektron.INComedor.Sync.Api;
using Intelektron.INComedor.Sync.Api.Ingesta;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Receptor de lotes (TK01.1): las dos operaciones del OpenAPI, sólo para equipos autenticados por certificado (D2).
const string RutaLotes = "/api/incomedor/v1/sync-batches";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(kestrel => kestrel.ConfigureHttpsDefaults(https =>
{
    // Pide el certificado de equipo sin cortar el handshake: la cadena, la vigencia y el registro los valida
    // FiltroEquipo, para responder 401 o 403 en lugar de cerrar la conexión sin respuesta (D2).
    https.ClientCertificateMode = ClientCertificateMode.AllowCertificate;
    https.AllowAnyClientCertificate();
}));
builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ConfiguracionReceptor>();

var app = builder.Build();

if (app.Services.GetRequiredService<ConfiguracionReceptor>().CadenaConexion is { } cadenaConexion)
{
    MigracionReceptor.Aplicar(cadenaConexion);
}

var lotes = app.MapGroup(RutaLotes).AddEndpointFilter<FiltroEquipo>();
lotes.MapPost("", () => Results.Problem(statusCode: 501, title: "Ingesta de lotes no implementada"));
lotes.MapGet("/{batchId}", () => Results.Problem(statusCode: 501, title: "Ingesta de lotes no implementada"));

app.Run();

public partial class Program;
