using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// El combo alineado y su lista propia con el puesto compilado (presentacion-puesto de ordenar-configuracion, D3 y D4, y la
/// regla de prolijidad visual): por CDP, a 1024 × 768, el combo tiene los bordes de la fila del código, su letra y su
/// relleno, y su lista abierta es parte de la página, entera dentro de la ventana y debajo del combo, con filas de 56 px.
/// Lo que bUnit no ve: las cajas dibujadas y que la lista no sea una ventana emergente del WebView2.
/// </summary>
public class ListaPropiaE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("lista-propia");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "presentacion-puesto · El combo alineado con la fila del código, y La lista se abre en el monitor del puesto (puesto compilado): a 1024 × 768 el combo va de borde a borde con el campo e «Ingresar», con su letra y su relleno, y su lista abierta queda entera en la ventana, debajo, con filas de 56 px")]
    public async Task Combo_alineado_y_lista_en_la_ventana()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await Assertions.Expect(pagina.Locator("#simulacion-persona")).ToBeVisibleAsync();
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024,
            ["height"] = 768,
            ["deviceScaleFactor"] = 1,
            ["mobile"] = false,
        });

        var medidas = await Medir(pagina);
        _escenario.Registrar($"1024 × 768, la fila del código: {medidas}");
        await _escenario.CapturarAsync(pagina, "combo-alineado-1024x768");

        Assert.True(Math.Abs(medidas.Combo.Izquierda - medidas.Campo.Izquierda) <= Tolerancia, $"Borde izquierdo: combo {medidas.Combo.Izquierda} px, campo {medidas.Campo.Izquierda} px");
        Assert.True(Math.Abs(medidas.Combo.Derecha - medidas.Ingresar.Derecha) <= Tolerancia, $"Borde derecho: combo {medidas.Combo.Derecha} px, «Ingresar» {medidas.Ingresar.Derecha} px");
        Assert.Equal(medidas.Campo.Letra, medidas.Combo.Letra);
        Assert.Equal(medidas.Campo.Relleno, medidas.Combo.Relleno);
        Assert.True(Math.Abs(medidas.Combo.Alto - medidas.Campo.Alto) <= Tolerancia, $"Alto: combo {medidas.Combo.Alto} px, campo {medidas.Campo.Alto} px");

        await pagina.Locator("#simulacion-persona").ClickAsync();
        await Assertions.Expect(pagina.Locator("#simulacion-persona-lista")).ToBeVisibleAsync();
        var lista = await MedirLista(pagina);
        _escenario.Registrar($"la lista abierta: {lista}");
        await _escenario.CapturarAsync(pagina, "lista-abierta-1024x768");

        Assert.Equal(0, await pagina.Locator("select").CountAsync());
        Assert.True(lista.Caja.Arriba >= medidas.Combo.Abajo - Tolerancia, $"La lista empieza en {lista.Caja.Arriba} px y el combo termina en {medidas.Combo.Abajo} px");
        Assert.True(lista.Caja.Abajo <= 768 && lista.Caja.Izquierda >= 0 && lista.Caja.Derecha <= 1024, $"La lista no entra en la ventana: {lista.Caja}");
        Assert.True(lista.Filas.Length >= 8, $"La lista tiene {lista.Filas.Length} filas");
        Assert.All(lista.Filas, alto => Assert.True(alto >= 56, $"Una fila mide {alto} px de alto"));
    }

    /// <summary>Las cajas, la letra y el relleno izquierdo del combo, del campo del código y de «Ingresar».</summary>
    private static async Task<Medidas> Medir(IPage pagina)
    {
        var json = await pagina.EvaluateAsync<string>(
            """
            () => {
              const caja = s => {
                const e = document.querySelector(s), r = e.getBoundingClientRect(), c = getComputedStyle(e);
                return { Arriba: r.top, Abajo: r.bottom, Izquierda: r.left, Derecha: r.right, Alto: r.height, Letra: c.fontSize, Relleno: c.paddingLeft };
              };
              return JSON.stringify({ Combo: caja('#simulacion-persona'), Campo: caja('#codigo'), Ingresar: caja('#ingresar') });
            }
            """);
        return JsonSerializer.Deserialize<Medidas>(json)!;
    }

    /// <summary>La caja de la lista abierta y el alto de cada fila.</summary>
    private static async Task<Lista> MedirLista(IPage pagina)
    {
        var json = await pagina.EvaluateAsync<string>(
            """
            () => {
              const l = document.querySelector('#simulacion-persona-lista'), r = l.getBoundingClientRect();
              return JSON.stringify({
                Caja: { Arriba: r.top, Abajo: r.bottom, Izquierda: r.left, Derecha: r.right, Alto: r.height, Letra: '', Relleno: '' },
                Filas: [...l.querySelectorAll('[role="option"]')].map(f => f.getBoundingClientRect().height),
              });
            }
            """);
        return JsonSerializer.Deserialize<Lista>(json)!;
    }

    private sealed record Caja(double Arriba, double Abajo, double Izquierda, double Derecha, double Alto, string Letra, string Relleno)
    {
        public override string ToString() => $"de {Izquierda:F1} a {Derecha:F1} px y de {Arriba:F1} a {Abajo:F1} px, {Alto:F1} px de alto, letra {Letra}, relleno {Relleno}";
    }

    private sealed record Medidas(Caja Combo, Caja Campo, Caja Ingresar)
    {
        public override string ToString() => $"combo {Combo}; campo {Campo}; «Ingresar» {Ingresar}";
    }

    private sealed record Lista(Caja Caja, double[] Filas)
    {
        public override string ToString() => $"{Caja}; {Filas.Length} filas de {string.Join(", ", Filas.Select(f => f.ToString("F1")))} px";
    }
}
