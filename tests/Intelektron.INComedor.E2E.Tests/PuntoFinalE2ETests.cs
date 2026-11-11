using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// El punto final no parte una línea (puntuar-la-interfaz, riesgo del design): con el puesto compilado, por CDP y a
/// 1024 × 768, el título del reposo, el mensaje de un consumo registrado y el aviso de la clave sin validar, al lado del
/// título de Configuración, siguen en una sola línea, y los avisos terminan en punto.
/// </summary>
public class PuntoFinalE2ETests : IAsyncLifetime
{
    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("punto-final");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "presentacion-puesto · Las oraciones de la pantalla terminan en punto (puesto compilado): a 1024 × 768, el título del reposo sin punto, y el mensaje del consumo y el aviso de la clave sin validar con punto, cada uno en una sola línea")]
    public async Task El_punto_no_parte_una_linea()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        var titulo = await Lineas(pagina, ".atencion h2");

        await pagina.Locator("#codigo").FillAsync("0001");
        await pagina.Locator("#ingresar").ClickAsync();
        await pagina.Locator(".opcion").First.ClickAsync();
        await pagina.Locator("#confirmar").ClickAsync();
        await Assertions.Expect(pagina.Locator("#listo")).ToBeVisibleAsync();
        var mensaje = await Lineas(pagina, "#mensaje");
        await pagina.Locator("#listo").ClickAsync();
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await Assertions.Expect(pagina.Locator("#aviso-clave-sin-validar")).ToBeVisibleAsync();
        var aviso = await Lineas(pagina, "#aviso-clave-sin-validar");
        _escenario.Registrar($"1024 × 768: título {titulo} · mensaje {mensaje} · aviso de la clave {aviso}");
        await _escenario.CapturarAsync(pagina, "aviso-clave-1024x768");

        Assert.False(titulo.Texto.EndsWith('.'), $"El título termina en punto: «{titulo.Texto}»");
        Assert.True(mensaje.Texto.EndsWith('.'), $"El mensaje no termina en punto: «{mensaje.Texto}»");
        Assert.True(aviso.Texto.EndsWith('.'), $"El aviso no termina en punto: «{aviso.Texto}»");
        Assert.All(new[] { titulo, mensaje, aviso }, l => Assert.True(l.Lineas == 1, $"«{l.Texto}» ocupa {l.Lineas} líneas"));
    }

    /// <summary>El texto de un elemento y cuántas líneas ocupa, contadas por los rectángulos de su contenido.</summary>
    private static async Task<Medida> Lineas(IPage pagina, string selector)
    {
        var json = await pagina.EvaluateAsync<string>(
            """
            selector => {
              const e = document.querySelector(selector);
              const rango = document.createRange();
              rango.selectNodeContents(e);
              const tops = [...rango.getClientRects()].filter(r => r.width > 0).map(r => Math.round(r.top));
              return JSON.stringify({ Texto: e.textContent.trim(), Lineas: new Set(tops).size });
            }
            """, selector);
        return JsonSerializer.Deserialize<Medida>(json)!;
    }

    private sealed record Medida(string Texto, int Lineas)
    {
        public override string ToString() => $"«{Texto}» en {Lineas} línea(s)";
    }
}
