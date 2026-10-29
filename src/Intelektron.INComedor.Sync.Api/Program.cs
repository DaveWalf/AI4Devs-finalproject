// Esqueleto del receptor de lotes (TK01.1): declara las dos operaciones del OpenAPI y todavía no ingiere nada.
const string RutaLotes = "/api/incomedor/v1/sync-batches";

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost(RutaLotes, () => Results.Problem(statusCode: 501, title: "Ingesta de lotes no implementada"));
app.MapGet(RutaLotes + "/{batchId:guid}", () => Results.Problem(statusCode: 501, title: "Ingesta de lotes no implementada"));

app.Run();

public partial class Program;
