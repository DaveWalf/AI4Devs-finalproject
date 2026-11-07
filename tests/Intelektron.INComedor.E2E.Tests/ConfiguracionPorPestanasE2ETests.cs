using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Configuración por pestañas con el puesto compilado (configurar-puesto-por-pestanas): por CDP y a 1024 × 768, lo que bUnit
/// no mide. Usa los ayudantes del E2E sin cambiarlos; la captura 10 sale en JPEG, en la carpeta de resultados.
/// </summary>
public class ConfiguracionPorPestanasE2ETests : IAsyncLifetime
{
    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("pestanas");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "configuracion-puesto · Las pestañas entran en 1024 × 768: las nueve en una fila, cada una de al menos 56 px de alto, sin desplazar la pantalla")]
    public async Task Las_pestanas_entran_en_1024_x_768()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await FijarArea(pagina, 1024, 768);
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        await pagina.Locator(".engranaje").ClickAsync();
        await Assertions.Expect(pagina.Locator("[role=\"tablist\"]")).ToBeVisibleAsync();

        var medida = await pagina.EvaluateAsync<JsonElement>(
            """
            () => {
                const pestanas = [...document.querySelectorAll('[role="tablist"] [role="tab"]')].map(p => p.getBoundingClientRect());
                const raiz = document.scrollingElement;
                return {
                    Cantidad: pestanas.length,
                    Arriba: pestanas.map(p => p.top),
                    Alto: Math.min(...pestanas.map(p => p.height)),
                    Derecha: Math.max(...pestanas.map(p => p.right)),
                    Izquierda: Math.min(...pestanas.map(p => p.left)),
                    Ancho: window.innerWidth,
                    AltoVentana: window.innerHeight,
                    AnchoDocumento: raiz.scrollWidth,
                    AltoDocumento: raiz.scrollHeight
                };
            }
            """);
        _escenario.Registrar($"pestañas a 1024 × 768: {medida}");

        Assert.Equal(9, medida.GetProperty("Cantidad").GetInt32());
        var arriba = medida.GetProperty("Arriba").EnumerateArray().Select(a => a.GetDouble()).ToList();
        Assert.True(arriba.Max() - arriba.Min() < 1, $"Las pestañas no están en una fila: arriba de {arriba.Min()} a {arriba.Max()} px.");
        Assert.True(medida.GetProperty("Alto").GetDouble() >= 56, $"La pestaña más baja mide {medida.GetProperty("Alto").GetDouble()} px.");
        Assert.True(medida.GetProperty("Izquierda").GetDouble() >= 0 && medida.GetProperty("Derecha").GetDouble() <= medida.GetProperty("Ancho").GetDouble(),
            $"Las pestañas salen del área: de {medida.GetProperty("Izquierda").GetDouble()} a {medida.GetProperty("Derecha").GetDouble()} px.");
        Assert.True(medida.GetProperty("AnchoDocumento").GetDouble() <= medida.GetProperty("Ancho").GetDouble()
            && medida.GetProperty("AltoDocumento").GetDouble() <= medida.GetProperty("AltoVentana").GetDouble(), "La pantalla se desplaza con Configuración abierta.");

        await Capturar(pagina, "10-configuracion");
    }

    [Fact(DisplayName = "comprobante-local · Reposo sin «Asistencia» a 1024 × 768: el campo del código e «Ingresar» a la vista, y ningún botón «Asistencia»")]
    public async Task Reposo_sin_asistencia()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await FijarArea(pagina, 1024, 768);

        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        await Assertions.Expect(pagina.Locator("#ingresar")).ToBeVisibleAsync();
        await Assertions.Expect(pagina.Locator("#asistencia")).ToHaveCountAsync(0);
        await Assertions.Expect(pagina.Locator("main button", new() { HasText = "Asistencia" })).ToHaveCountAsync(0);

        await Capturar(pagina, "06-reposo-tactil");
    }

    private static async Task FijarArea(IPage pagina, int ancho, int alto)
    {
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = ancho, ["height"] = alto, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
    }

    /// <summary>JPEG a la escala CSS, como las capturas de docs/capturas, en la carpeta de resultados de la prueba.</summary>
    private async Task Capturar(IPage pagina, string nombre)
    {
        var ruta = Path.Combine(_escenario.Resultados, $"{nombre}.jpg");
        await pagina.ScreenshotAsync(new() { Path = ruta, Type = ScreenshotType.Jpeg, Quality = 85, Scale = ScreenshotScale.Css });
        _escenario.Registrar($"captura {ruta}");
    }
}
