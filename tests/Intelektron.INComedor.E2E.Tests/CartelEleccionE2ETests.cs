using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// El cartel de la elección con el puesto compilado (confirmar-eleccion-en-cartel): por CDP y en un área de 1024 × 768, lo
/// que bUnit no ve. Sin opción elegida no hay «Confirmar»; al tocar una, el foco del WebView2 está en «Confirmar», el cartel
/// y sus dos botones miden al menos 56 px y entran en la ventana; Escape, que sube desde el botón enfocado hasta la raíz, y
/// «Elegir otra» lo cierran; y confirmar desde el cartel llega a «Consumo registrado».
/// </summary>
public class CartelEleccionE2ETests : IAsyncLifetime
{
    private const string Almuerzo = "Almuerzo común";
    private const string Exito = "Consumo registrado. Retirá tu comprobante";

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("cartel");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "atencion-puesto · Elegir abre el cartel con lo elegido (puesto compilado): en 1024 × 768, sin opción elegida no hay «Confirmar»; al tocar una, el foco está en «Confirmar», el cartel y sus dos botones miden al menos 56 px y entran en la ventana; Escape y «Elegir otra» lo cierran, y confirmar desde el cartel llega a «Consumo registrado»")]
    public async Task El_cartel_en_el_puesto_compilado()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024,
            ["height"] = 768,
            ["deviceScaleFactor"] = 1,
            ["mobile"] = false,
        });
        await pagina.Locator("#codigo").FillAsync("0001");
        await pagina.Locator("#ingresar").ClickAsync();
        var opcion = pagina.Locator(".opcion", new() { HasText = Almuerzo });
        await Assertions.Expect(opcion).ToBeVisibleAsync();
        await Assertions.Expect(pagina.Locator("#confirmar")).ToHaveCountAsync(0);

        await opcion.ClickAsync();

        var cartel = pagina.Locator("#cartel-eleccion");
        await Assertions.Expect(cartel).ToBeVisibleAsync();
        await Assertions.Expect(cartel.Locator("#eleccion")).ToContainTextAsync(Almuerzo);
        await Assertions.Expect(pagina.Locator("#confirmar")).ToBeFocusedAsync();
        var medida = await Medir(pagina);
        _escenario.Registrar($"1024 × 768, cartel abierto: {medida}");
        await _escenario.CapturarAsync(pagina, "cartel-1024x768");
        Assert.All([("cartel", medida.Cartel), ("Confirmar", medida.Confirmar), ("Elegir otra", medida.ElegirOtra)], c =>
        {
            Assert.True(c.Item2.Alto >= 56, $"{c.Item1}: {c.Item2.Alto} px de alto");
            Assert.True(c.Item2.Izquierda >= 0 && c.Item2.Arriba >= 0 && c.Item2.Derecha <= medida.Ancho && c.Item2.Abajo <= medida.AltoVentana,
                $"{c.Item1}: {c.Item2} no entra en la ventana de {medida.Ancho} × {medida.AltoVentana}");
        });

        await pagina.Keyboard.PressAsync("Escape");
        await Assertions.Expect(cartel).ToHaveCountAsync(0);
        await Assertions.Expect(opcion).ToHaveAttributeAsync("aria-pressed", "true");
        _escenario.Registrar("Escape con el foco en «Confirmar»: el cartel se cerró y la opción sigue elegida");

        await opcion.ClickAsync();
        await pagina.Locator("#elegir-otra").ClickAsync();
        await Assertions.Expect(cartel).ToHaveCountAsync(0);
        await Assertions.Expect(pagina.Locator("#saludo")).ToContainTextAsync("Persona sintética 01");

        await opcion.ClickAsync();
        await pagina.Locator("#confirmar").ClickAsync();

        await Assertions.Expect(pagina.Locator("#mensaje")).ToHaveTextAsync(Exito);
        _escenario.Registrar("confirmado desde el cartel: «Consumo registrado»");
    }

    /// <summary>La ventana y las cajas del cartel, de «Confirmar» y de «Elegir otra».</summary>
    private static async Task<Medida> Medir(IPage pagina)
    {
        var json = await pagina.EvaluateAsync<string>(
            """
            () => {
              const caja = s => { const r = document.querySelector(s).getBoundingClientRect(); return { Arriba: r.top, Abajo: r.bottom, Izquierda: r.left, Derecha: r.right, Alto: r.height }; };
              return JSON.stringify({
                Ancho: innerWidth,
                AltoVentana: innerHeight,
                Cartel: caja('#cartel-eleccion'),
                Confirmar: caja('#cartel-eleccion #confirmar'),
                ElegirOtra: caja('#cartel-eleccion #elegir-otra'),
              });
            }
            """);
        return JsonSerializer.Deserialize<Medida>(json)!;
    }

    private sealed record Caja(double Arriba, double Abajo, double Izquierda, double Derecha, double Alto)
    {
        public override string ToString() => $"de {Izquierda:F1} a {Derecha:F1} px y de {Arriba:F1} a {Abajo:F1} px, {Alto:F1} px de alto";
    }

    private sealed record Medida(double Ancho, double AltoVentana, Caja Cartel, Caja Confirmar, Caja ElegirOtra)
    {
        public override string ToString() => $"ventana {Ancho:F0} × {AltoVentana:F0}; cartel {Cartel}; «Confirmar» {Confirmar}; «Elegir otra» {ElegirOtra}";
    }
}
