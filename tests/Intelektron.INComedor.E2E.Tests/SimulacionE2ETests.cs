using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// El modo simulación con el puesto compilado (simular-identificacion-por-lista): por CDP, se enciende en General, el combo
/// aparece en la pantalla de identificación, una credencial vencida deja el combo sin persona elegida en el DOM real del
/// WebView2, y elegir a la persona de 0001 abre su selección sin que el texto de la página tenga su código. Lo que bUnit no
/// ve: el evento change del select nativo y la vuelta del combo a la opción vacía.
/// </summary>
public class SimulacionE2ETests : IAsyncLifetime
{
    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("simulacion");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "atencion-puesto · Elegir a una persona abre la selección (puesto compilado): encender la simulación en General, cerrar Configuración, elegir a la persona de 0001 en el combo y llegar a «Elegí tu servicio de hoy» con su nombre, sin 0001 en el texto de la página")]
    public async Task Elegir_en_el_combo_abre_la_seleccion()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        await Assertions.Expect(pagina.Locator("#simulacion-persona")).ToHaveCountAsync(0);

        await pagina.Locator(".engranaje").ClickAsync();
        await pagina.Locator("#encender-simulacion").ClickAsync();
        await Assertions.Expect(pagina.Locator("#modo-simulacion")).ToHaveTextAsync("Simulación: encendida");
        await pagina.Locator("#cancelar-configuracion").ClickAsync();
        var combo = pagina.Locator("#simulacion-persona");
        await Assertions.Expect(combo).ToBeVisibleAsync();
        _escenario.Registrar("simulación encendida en General: el combo está en la pantalla de identificación");

        // Una credencial vencida: el aviso, y el combo vuelve a la opción vacía en el DOM, no sólo en el componente.
        await combo.SelectOptionAsync(new SelectOptionValue { Label = "Persona sintética 02" });
        await Assertions.Expect(pagina.Locator("#aviso")).ToHaveTextAsync("Esta credencial está vencida. Solicitá asistencia");
        await Assertions.Expect(pagina.Locator("#simulacion-persona")).ToHaveValueAsync("");

        await pagina.Locator("#simulacion-persona").SelectOptionAsync(new SelectOptionValue { Label = "Persona sintética 01" });

        await Assertions.Expect(pagina.GetByRole(AriaRole.Heading, new() { Name = "Elegí tu servicio de hoy" })).ToBeVisibleAsync();
        await Assertions.Expect(pagina.Locator("#saludo")).ToContainTextAsync("Persona sintética 01");
        var texto = await pagina.Locator("body").InnerTextAsync();
        Assert.DoesNotContain("0001", texto, StringComparison.Ordinal);
        _escenario.Registrar("elegida la persona de 0001 en el combo: selección con su nombre y sin su código en la página");
    }
}
