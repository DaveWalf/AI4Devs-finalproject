using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Intelektron.INComedor.Sync.Api.Tests;

/// <summary>
/// Mientras la ingesta no exista, el receptor no acusa ningún lote (RB16): sólo responde 501 problem+json.
/// </summary>
public class ReceptorEsqueletoTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string RutaLotes = "/api/incomedor/v1/sync-batches";
    private const string TipoProblema = "application/problem+json";

    [Fact(DisplayName = "TK01.1 · AT27 (RB16) · Envío de un lote: 501 problem+json y ningún 2xx")]
    public async Task Envio_de_un_lote_responde_501_problem_json()
    {
        using var cliente = fabrica.CreateClient();
        using var cuerpo = new StringContent("""{"BatchId":"00000000-0000-4000-8000-000000000001","Items":[]}""", Encoding.UTF8, "application/json");

        using var respuesta = await cliente.PostAsync(RutaLotes, cuerpo, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotImplemented, respuesta.StatusCode);
        Assert.Equal(TipoProblema, respuesta.Content.Headers.ContentType?.MediaType);
        Assert.False(respuesta.IsSuccessStatusCode);
    }

    [Fact(DisplayName = "TK01.1 · Consulta de un lote: 501 problem+json")]
    public async Task Consulta_de_un_lote_responde_501_problem_json()
    {
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.GetAsync(
            $"{RutaLotes}/00000000-0000-4000-8000-000000000002", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotImplemented, respuesta.StatusCode);
        Assert.Equal(TipoProblema, respuesta.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "OWASP A03 y A05 · Cuerpo que no es JSON: 501, no 500, y la respuesta no refleja la entrada")]
    public async Task Cuerpo_que_no_es_json_responde_501_sin_reflejarlo()
    {
        const string CadenaReconocible = "CADENA-RECONOCIBLE-7F3A9C";
        using var cliente = fabrica.CreateClient();
        using var cuerpo = new StringContent($"<<{CadenaReconocible}", Encoding.UTF8, "application/json");

        using var respuesta = await cliente.PostAsync(RutaLotes, cuerpo, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotImplemented, respuesta.StatusCode);
        Assert.Equal(TipoProblema, respuesta.Content.Headers.ContentType?.MediaType);
        var texto = await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(CadenaReconocible, texto);
    }

    [Fact(DisplayName = "Spec sincronizacion-lotes · Ruta no declarada: 404")]
    public async Task Ruta_no_declarada_responde_404()
    {
        using var cliente = fabrica.CreateClient();

        using var respuesta = await cliente.GetAsync("/api/incomedor/v1/no-declarada", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
