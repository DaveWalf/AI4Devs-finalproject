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

    [Fact(DisplayName = "presentacion-puesto · Con el teclado (puesto compilado): con el foco en el combo, Enter abre la lista y la deja abierta, cuatro flechas abajo y Enter eligen a «Persona sintética 01» y abren su selección")]
    public async Task El_combo_con_el_teclado()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var combo = pagina.Locator("#simulacion-persona");
        await Assertions.Expect(combo).ToBeVisibleAsync();

        await combo.FocusAsync();
        await pagina.Keyboard.PressAsync("Enter");
        await Assertions.Expect(pagina.Locator("#simulacion-persona-lista")).ToBeVisibleAsync();
        await Assertions.Expect(combo).ToHaveAttributeAsync("aria-expanded", "true");
        for (var i = 0; i < 4; i++)
        {
            await pagina.Keyboard.PressAsync("ArrowDown");
        }

        await Assertions.Expect(pagina.Locator("#simulacion-persona-lista")).ToHaveAttributeAsync("aria-activedescendant", "simulacion-persona-opcion-4");
        await pagina.Keyboard.PressAsync("Enter");

        await Assertions.Expect(pagina.Locator("#saludo")).ToContainTextAsync("Persona sintética 01");
        _escenario.Registrar("con el teclado: Enter abrió la lista, cuatro flechas y Enter eligieron a «Persona sintética 01»");
    }

    [Fact(DisplayName = "presentacion-puesto · La lista se abre en el monitor del puesto, en Consumos (puesto compilado): a 1024 × 768 los filtros y «Consultar» miden lo mismo, y la lista del filtro Persona queda entera dentro del panel de la pestaña, debajo del filtro, con filas de 56 px")]
    public async Task Los_filtros_de_consumos_y_su_lista()
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
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await Assertions.Expect(pagina.Locator("#consumos-persona")).ToBeVisibleAsync();

        var filtros = await MedirFiltros(pagina);
        _escenario.Registrar($"1024 × 768, los filtros de Consumos: {string.Join("; ", filtros.Select(f => f.ToString()))}");
        await _escenario.CapturarAsync(pagina, "filtros-consumos-1024x768");
        var alto = filtros[0].Alto;
        Assert.All(filtros, f => Assert.True(Math.Abs(f.Alto - alto) <= Tolerancia && f.Alto >= 56, $"{f.Id}: {f.Alto} px de alto, contra {alto} px"));
        foreach (var renglon in filtros.GroupBy(f => Math.Round(f.Arriba)))
        {
            Assert.All(renglon, f => Assert.True(Math.Abs(f.Abajo - renglon.First().Abajo) <= Tolerancia, $"{f.Id} termina en {f.Abajo} px en su renglón"));
        }

        await pagina.Locator("#consumos-persona").ClickAsync();
        await Assertions.Expect(pagina.Locator("#consumos-persona-lista")).ToBeVisibleAsync();
        var json = await pagina.EvaluateAsync<string>(
            """
            () => {
              const r = s => document.querySelector(s).getBoundingClientRect();
              const l = r('#consumos-persona-lista'), b = r('#consumos-persona'), p = r('#seccion-consumos'), d = r('[role="dialog"]');
              const c = document.querySelector('#consumos-persona').parentElement.getBoundingClientRect();
              const filas = [...document.querySelectorAll('#consumos-persona-lista [role="option"]')].map(f => f.getBoundingClientRect().height);
              return JSON.stringify({ Arriba: l.top, Abajo: l.bottom, Boton: b.bottom, Panel: p.bottom, Dialogo: d.bottom, Filas: filas,
                IzquierdaLista: l.left, DerechaLista: l.right, IzquierdaBoton: b.left, DerechaBoton: b.right, AnchoContenedor: c.width });
            }
            """);
        var lista = JsonSerializer.Deserialize<ListaEnPanel>(json)!;
        _escenario.Registrar($"la lista del filtro Persona: de {lista.Arriba:F1} a {lista.Abajo:F1} px, debajo del filtro que termina en {lista.Boton:F1}; el panel termina en {lista.Panel:F1} y el diálogo en {lista.Dialogo:F1}; filas de {string.Join(", ", lista.Filas.Select(f => f.ToString("F1")))} px");
        await _escenario.CapturarAsync(pagina, "lista-filtro-persona-1024x768");

        Assert.True(lista.Arriba >= lista.Boton - Tolerancia, $"La lista empieza en {lista.Arriba} px y el filtro termina en {lista.Boton} px");
        Assert.True(lista.Abajo <= Math.Min(lista.Panel, lista.Dialogo) + Tolerancia, $"La lista termina en {lista.Abajo} px y el panel en {lista.Panel} px");
        Assert.All(lista.Filas, f => Assert.True(f >= 56, $"Una fila mide {f} px"));
        Assert.True(Math.Abs(lista.IzquierdaLista - lista.IzquierdaBoton) <= Tolerancia && Math.Abs(lista.DerechaLista - lista.DerechaBoton) <= Tolerancia,
            $"La lista va de {lista.IzquierdaLista} a {lista.DerechaLista} px y el filtro de {lista.IzquierdaBoton} a {lista.DerechaBoton} px (contenedor de {lista.AnchoContenedor} px)");
    }

    /// <summary>Las cajas de los filtros de Consumos y de «Consultar».</summary>
    private static async Task<Filtro[]> MedirFiltros(IPage pagina)
    {
        var json = await pagina.EvaluateAsync<string>(
            """
            () => JSON.stringify(['#consumos-desde', '#consumos-hasta', '#consumos-persona', '#consumos-servicio', '#consumos-opcion', '#consultar-consumos'].map(s => {
              const r = document.querySelector(s).getBoundingClientRect();
              return { Id: s, Arriba: r.top, Abajo: r.bottom, Izquierda: r.left, Derecha: r.right, Alto: r.height };
            }))
            """);
        return JsonSerializer.Deserialize<Filtro[]>(json)!;
    }

    private sealed record Filtro(string Id, double Arriba, double Abajo, double Izquierda, double Derecha, double Alto)
    {
        public override string ToString() => $"{Id} de {Izquierda:F1} a {Derecha:F1} px y de {Arriba:F1} a {Abajo:F1} px";
    }

    private sealed record ListaEnPanel(double Arriba, double Abajo, double Boton, double Panel, double Dialogo, double[] Filas,
        double IzquierdaLista, double DerechaLista, double IzquierdaBoton, double DerechaBoton, double AnchoContenedor);

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
