using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Operar sólo con la pantalla, con el puesto compilado (operar-solo-con-la-pantalla, D1 a D5): por CDP y a 1024 × 768,
/// con toques reales de la pantalla táctil (Input.dispatchTouchEvent), no clics de mouse. El código de reposo y una casilla
/// de Mails se escriben sólo con el teclado en pantalla; «Ocultar teclado» dura lo que la sesión de Configuración.
/// </summary>
public class OperacionTactilE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("tactil");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "operacion-tactil · Una casilla sólo con la pantalla (puesto compilado): a 1024 × 768 y con toques, el código 0001 y la casilla p@e.com se escriben con el teclado en pantalla, que deja el campo a la vista, y «Ocultar teclado» dura lo que la sesión de Configuración")]
    public async Task Operar_solo_con_la_pantalla()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await cdp.SendAsync("Emulation.setTouchEmulationEnabled", new Dictionary<string, object> { ["enabled"] = true, ["maxTouchPoints"] = 1 });

        // El código de reposo, sólo con toques.
        await TocarAsync(cdp, pagina, "#codigo");
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToBeVisibleAsync();
        foreach (var tecla in new[] { "0", "0", "0", "1" })
        {
            await TocarAsync(cdp, pagina, $".teclado-general [data-tecla=\"{tecla}\"]");
        }

        await Assertions.Expect(pagina.Locator("#codigo")).ToHaveValueAsync("0001");
        var teclado = (await MedirAsync(pagina, ".teclado-general"))[0];
        var teclas = await MedirAsync(pagina, ".teclado-general .tecla");
        var codigo = (await MedirAsync(pagina, "#codigo"))[0];
        _escenario.Registrar($"1024 × 768, reposo con teclado: el teclado de y {teclado.Arriba:F1} a {teclado.Arriba + teclado.Alto:F1}, {teclas.Length} teclas; el código de y {codigo.Arriba:F1} a {codigo.Arriba + codigo.Alto:F1}");
        await _escenario.CapturarAsync(pagina, "tactil-reposo-1024x768");
        Assert.True(teclado.Arriba + teclado.Alto <= 768 + Tolerancia, $"El teclado termina en {teclado.Arriba + teclado.Alto}");
        Assert.True(codigo.Arriba + codigo.Alto <= teclado.Arriba, $"El código termina en {codigo.Arriba + codigo.Alto} y el teclado empieza en {teclado.Arriba}");
        Assert.All(teclas, t => Assert.True(t.Alto >= 56 - Tolerancia, $"Una tecla de {t.Alto} px"));
        Assert.Equal(0, await pagina.Locator(".teclado-general [data-tecla=\"OCULTAR\"]").CountAsync());

        await TocarAsync(cdp, pagina, "#ingresar");
        await Assertions.Expect(pagina.Locator(".opcion").First).ToBeVisibleAsync();
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToHaveCountAsync(0);
        await pagina.Locator("#cancelar").ClickAsync();

        // Una casilla de Mails, con Configuración acortada arriba del teclado.
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-mails").ClickAsync();
        await Assertions.Expect(pagina.Locator("#remitente-correo")).ToBeVisibleAsync();

        // Con el mouse no aparece: quien tiene un mouse tiene un teclado físico.
        await pagina.Locator("#cliente-correo").ClickAsync();
        await pagina.WaitForTimeoutAsync(300);
        Assert.Equal(0, await pagina.Locator(".teclado-general").CountAsync());

        await TocarAsync(cdp, pagina, "#remitente-correo");
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToBeVisibleAsync();
        foreach (var tecla in new[] { "P", "@", "E", ".", "C", "O", "M" })
        {
            await TocarAsync(cdp, pagina, $".teclado-general [data-tecla=\"{tecla}\"]");
        }

        await Assertions.Expect(pagina.Locator("#remitente-correo")).ToHaveValueAsync("p@e.com");
        var dialogo = (await MedirAsync(pagina, "[role=\"dialog\"]"))[0];
        var remitente = (await MedirAsync(pagina, "#remitente-correo"))[0];
        teclado = (await MedirAsync(pagina, ".teclado-general"))[0];
        _escenario.Registrar($"1024 × 768, Mails con teclado: el diálogo de y {dialogo.Arriba:F1} a {dialogo.Arriba + dialogo.Alto:F1}; el remitente de y {remitente.Arriba:F1} a {remitente.Arriba + remitente.Alto:F1}; el teclado desde y {teclado.Arriba:F1}");
        await _escenario.CapturarAsync(pagina, "tactil-mails-1024x768");
        Assert.True(dialogo.Arriba + dialogo.Alto <= teclado.Arriba + Tolerancia, $"El diálogo termina en {dialogo.Arriba + dialogo.Alto} y el teclado empieza en {teclado.Arriba}");
        Assert.True(remitente.Arriba >= dialogo.Arriba && remitente.Arriba + remitente.Alto <= teclado.Arriba, $"El remitente de y {remitente.Arriba} a {remitente.Arriba + remitente.Alto}");

        // «Ocultar teclado» dura lo que la sesión.
        await TocarAsync(cdp, pagina, ".teclado-general [data-tecla=\"OCULTAR\"]");
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToHaveCountAsync(0);
        await TocarAsync(cdp, pagina, "#servidor-correo");
        await pagina.WaitForTimeoutAsync(300);
        Assert.Equal(0, await pagina.Locator(".teclado-general").CountAsync());
        await pagina.Locator("#cancelar-configuracion").ClickAsync();
        await Assertions.Expect(pagina.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);

        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-mails").ClickAsync();
        await Assertions.Expect(pagina.Locator("#servidor-correo")).ToBeVisibleAsync();
        await TocarAsync(cdp, pagina, "#servidor-correo");
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToBeVisibleAsync();
    }

    /// <summary>Un toque de la pantalla táctil en el centro del elemento, como un dedo: no un clic de mouse.</summary>
    private static async Task TocarAsync(ICDPSession cdp, IPage pagina, string selector)
    {
        await pagina.Locator(selector).First.ScrollIntoViewIfNeededAsync();
        var caja = (await MedirAsync(pagina, selector))[0];
        var punto = new Dictionary<string, object> { ["x"] = caja.Izquierda + caja.Ancho / 2, ["y"] = caja.Arriba + caja.Alto / 2 };
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = "touchStart", ["touchPoints"] = new[] { punto } });
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = "touchEnd", ["touchPoints"] = Array.Empty<object>() });
        await pagina.WaitForTimeoutAsync(60);
    }

    private static async Task<Caja[]> MedirAsync(IPage pagina, string selector) =>
        JsonSerializer.Deserialize<Caja[]>(await pagina.EvaluateAsync<string>(
            """
            s => JSON.stringify([...document.querySelectorAll(s)].map(e => {
              const b = e.getBoundingClientRect();
              return { Id: e.id, Arriba: b.top, Izquierda: b.left, Ancho: b.width, Alto: b.height };
            }))
            """, selector))!;

    private sealed record Caja(string Id, double Arriba, double Izquierda, double Ancho, double Alto);
}
