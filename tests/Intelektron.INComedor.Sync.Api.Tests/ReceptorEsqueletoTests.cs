using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Intelektron.INComedor.Sync.Api.Tests;

/// <summary>
/// El receptor expone sólo las operaciones de lotes del OpenAPI: una ruta que no declara responde 404, también sin
/// configuración (D1 y D14 de recibir-lotes-idempotente).
/// </summary>
public class ReceptorEsqueletoTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact(DisplayName = "Spec sincronizacion-lotes · Ruta no declarada: 404")]
    public async Task Ruta_no_declarada_responde_404()
    {
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/incomedor/v1/no-declarada", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
