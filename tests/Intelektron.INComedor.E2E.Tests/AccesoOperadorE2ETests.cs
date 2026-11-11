using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// La entrada a Configuración con el teclado en pantalla, en el puesto compilado (entrar-a-configuracion-con-teclado, D3 a D6):
/// por CDP y en un área de 1024 × 768, lo que bUnit no mide. Cada tecla del teclado de la clave mide al menos 56 px y entra en
/// la ventana, y el campo pide no abrir el teclado táctil del sistema; 9001 abre Configuración con tres pestañas, y la clave
/// del día, con las ocho y el aviso de clave sin validar. Que el teclado táctil de Windows no aparezca lo ve David.
/// </summary>
public class AccesoOperadorE2ETests : IAsyncLifetime
{
    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("operador");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "acceso-puesto · El teclado en pantalla y la entrada de un operador (puesto compilado): en 1024 × 768 cada tecla mide al menos 56 px y entra en la ventana, el campo tiene inputmode none, 9001 abre Configuración con tres pestañas y la clave del día con las ocho y el aviso de clave sin validar")]
    public async Task El_teclado_y_el_operador_en_el_puesto_compilado()
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
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        await pagina.Locator(".engranaje").ClickAsync();
        await Assertions.Expect(pagina.Locator("#dialogo-clave")).ToBeVisibleAsync();
        await Assertions.Expect(pagina.Locator("#clave-del-dia")).ToHaveAttributeAsync("inputmode", "none");
        var teclas = await MedirTeclas(pagina);
        _escenario.Registrar($"1024 × 768, teclado de la clave: {string.Join("; ", teclas.Select(t => t.ToString()))}");
        await _escenario.CapturarAsync(pagina, "teclado-clave-1024x768");
        Assert.Equal(14, teclas.Count);
        Assert.All(teclas, t =>
        {
            Assert.True(t.Alto >= 56 && t.Ancho >= 56, $"{t.Id}: {t.Ancho} × {t.Alto} px");
            Assert.True(t.Izquierda >= 0 && t.Arriba >= 0 && t.Derecha <= 1024 && t.Abajo <= 768, $"{t.Id} no entra en la ventana: {t}");
        });

        await EntradaConfiguracionE2E.TipearAsync(pagina, "9001");
        await Assertions.Expect(pagina.Locator("[role=\"tab\"]")).ToHaveCountAsync(3);
        Assert.Equal(["pestana-consumos", "pestana-personas", "pestana-impresora"],
            await pagina.Locator("[role=\"tab\"]").EvaluateAllAsync<string[]>("ts => ts.map(t => t.id)"));
        await Assertions.Expect(pagina.Locator("#operador-configuracion")).ToHaveTextAsync("Operador: Operador sintético 01");
        await Assertions.Expect(pagina.Locator("#aviso-clave-sin-validar")).ToHaveCountAsync(0);
        _escenario.Registrar("9001: Configuración con Consumos, Personas e Impresora");
        await pagina.Locator("#cancelar-configuracion").ClickAsync();

        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await Assertions.Expect(pagina.Locator("[role=\"tab\"]")).ToHaveCountAsync(8);
        await Assertions.Expect(pagina.Locator("#aviso-clave-sin-validar")).ToHaveTextAsync("Entraste con una clave del día sin validar: esta versión acepta cualquier número.");
        _escenario.Registrar("clave del día: Configuración con las ocho pestañas y el aviso de clave sin validar");
    }

    /// <summary>Las cajas de las teclas del teclado de la clave y de «Cancelar», respecto de la ventana.</summary>
    private static async Task<List<Tecla>> MedirTeclas(IPage pagina)
    {
        var json = await pagina.EvaluateAsync<string>(
            """
            () => JSON.stringify([...document.querySelectorAll('.teclado-numerico button, #cancelar-clave')].map(t => {
              const r = t.getBoundingClientRect();
              return { Id: t.id, Izquierda: r.left, Arriba: r.top, Derecha: r.right, Abajo: r.bottom, Ancho: r.width, Alto: r.height };
            }))
            """);
        return JsonSerializer.Deserialize<List<Tecla>>(json)!;
    }

    private sealed record Tecla(string Id, double Izquierda, double Arriba, double Derecha, double Abajo, double Ancho, double Alto)
    {
        public override string ToString() => $"{Id} {Ancho:F0} × {Alto:F0} de ({Izquierda:F0}, {Arriba:F0}) a ({Derecha:F0}, {Abajo:F0})";
    }
}
