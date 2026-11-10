using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// El modo simulación con el puesto compilado (simular-identificacion-por-lista): por CDP, una carpeta nueva arranca con el
/// combo en la pantalla de identificación (D12), arriba del campo del código y con «Ingresar» a la derecha del campo, medidos
/// en un área de 1024 × 768; una credencial vencida deja el combo sin persona elegida en el DOM real del WebView2, y elegir a
/// la persona de 0001 abre su selección sin que el texto de la página tenga su código. Lo que bUnit no ve: el evento change
/// del select nativo, la vuelta del combo a la opción vacía y las cajas dibujadas.
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

    [Fact(DisplayName = "atencion-puesto · Elegir a una persona abre la selección (puesto compilado), y presentacion-puesto · El combo arriba y «Ingresar» a la derecha del campo: con la simulación de una carpeta nueva, el combo entero sobre el campo e «Ingresar» a su derecha en 1024 × 768, los tres de 56 px; elegir a la persona de 0001 llega a «Elegí tu servicio de hoy» con su nombre, sin 0001 en el texto de la página")]
    public async Task Elegir_en_el_combo_abre_la_seleccion()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var combo = pagina.Locator("#simulacion-persona");
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        await Assertions.Expect(combo).ToBeVisibleAsync();
        _escenario.Registrar("carpeta nueva: el combo está en la pantalla de identificación sin tocar General");

        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024,
            ["height"] = 768,
            ["deviceScaleFactor"] = 1,
            ["mobile"] = false,
        });
        var fila = await MedirFila(pagina);
        _escenario.Registrar($"1024 × 768, reposo con simulación: {fila}");
        await _escenario.CapturarAsync(pagina, "reposo-simulacion-1024x768");

        Assert.True(fila.Combo.Abajo <= fila.Campo.Arriba, $"El combo termina en {fila.Combo.Abajo} px y el campo empieza en {fila.Campo.Arriba} px");
        Assert.True(fila.Ingresar.Izquierda >= fila.Campo.Derecha, $"«Ingresar» empieza en {fila.Ingresar.Izquierda} px y el campo termina en {fila.Campo.Derecha} px");
        Assert.True(Math.Abs(fila.Ingresar.Arriba - fila.Campo.Arriba) < 1, $"«Ingresar» empieza arriba en {fila.Ingresar.Arriba} px y el campo en {fila.Campo.Arriba} px");
        Assert.All([("combo", fila.Combo), ("campo", fila.Campo), ("Ingresar", fila.Ingresar)],
            c => Assert.True(c.Item2.Alto >= 56, $"{c.Item1}: {c.Item2.Alto} px de alto"));

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

    /// <summary>Las cajas del combo, del campo del código y de «Ingresar», respecto de la ventana.</summary>
    private static async Task<Fila> MedirFila(IPage pagina)
    {
        var json = await pagina.EvaluateAsync<string>(
            """
            () => {
              const caja = s => { const r = document.querySelector(s).getBoundingClientRect(); return { Arriba: r.top, Abajo: r.bottom, Izquierda: r.left, Derecha: r.right, Alto: r.height }; };
              return JSON.stringify({ Combo: caja('#simulacion-persona'), Campo: caja('#codigo'), Ingresar: caja('#ingresar') });
            }
            """);
        return JsonSerializer.Deserialize<Fila>(json)!;
    }

    private sealed record Caja(double Arriba, double Abajo, double Izquierda, double Derecha, double Alto)
    {
        public override string ToString() => $"de {Izquierda:F1} a {Derecha:F1} px y de {Arriba:F1} a {Abajo:F1} px, {Alto:F1} px de alto";
    }

    private sealed record Fila(Caja Combo, Caja Campo, Caja Ingresar)
    {
        public override string ToString() => $"combo {Combo}; campo {Campo}; «Ingresar» {Ingresar}";
    }
}
