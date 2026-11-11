using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Personas con las raciones en x/n, el toque en un cupo y el teclado en pantalla, con el puesto compilado
/// (editar-raciones-en-personas): por CDP y a 1024 × 768, con la tolerancia de 1 px de la regla de prolijidad (D6).
/// </summary>
public class RacionesEnPersonasE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("raciones-personas");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "administracion-local · Tocar un cupo lo edita, con el teclado en pantalla (puesto compilado): a 1024 × 768, «0/2» en REFRIGERIO de 0001, el toque abre la edición con ese límite elegido, las teclas miden 56 px y lo mismo por fila dentro del diálogo, y borrar y «1» guardan «0/1»")]
    public async Task Tocar_un_cupo_y_escribir_con_el_teclado()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-personas").ClickAsync();
        var fila = pagina.Locator("#tabla-personas tr.persona", new() { HasText = "Persona sintética 01" });
        var refrigerio = fila.Locator("td.cupo[data-cupo=\"REFRIGERIO\"]");
        await Assertions.Expect(refrigerio).ToHaveTextAsync("0/2");
        await Assertions.Expect(fila.Locator("td.cupo[data-cupo=\"ALMUERZO\"]")).ToHaveTextAsync("0/1");
        await _escenario.CapturarAsync(pagina, "personas-x-de-n-1024x768");
        var pie = JsonSerializer.Deserialize<Caja[]>(await pagina.EvaluateAsync<string>(
            """
            () => JSON.stringify(['#exportar-personas-csv', '#exportar-personas-xlsx', '#cancelar-configuracion'].map(i => {
              const e = document.querySelector(i), b = e.getBoundingClientRect();
              return { Id: i, Arriba: b.top, Alto: b.height, Ancho: b.width, Letra: getComputedStyle(e).fontSize };
            }))
            """))!;
        _escenario.Registrar("1024 × 768, el pie de Personas: " + string.Join("; ", pie.Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1} px desde {c.Arriba:F1}, letra {c.Letra}")));
        Assert.All(pie, c => Assert.True(Math.Abs(c.Alto - pie[^1].Alto) <= Tolerancia && Math.Abs(c.Arriba - pie[^1].Arriba) <= Tolerancia && c.Letra == pie[^1].Letra,
            $"{c.Id}: {c.Alto} px desde {c.Arriba}, letra {c.Letra}, contra «Cerrar»"));
        Assert.True(Math.Abs(pie[0].Ancho - pie[1].Ancho) <= Tolerancia, $"Los botones de exportar miden {pie[0].Ancho} y {pie[1].Ancho} px");

        await refrigerio.Locator(".cupo-tocable").ClickAsync();
        await Assertions.Expect(pagina.Locator("#panel-personas")).ToBeVisibleAsync();
        await Assertions.Expect(pagina.Locator("input.limite.elegido")).ToHaveCountAsync(1);
        await Assertions.Expect(pagina.Locator("input.limite.elegido")).ToBeFocusedAsync();

        var json = await pagina.EvaluateAsync<string>(
            """
            () => {
              const filas = [...document.querySelectorAll('.teclado-en-pantalla .fila-teclas')].map(f =>
                [...f.querySelectorAll('.tecla')].map(t => { const b = t.getBoundingClientRect(); return { Ancho: b.width, Alto: b.height, Abajo: b.bottom, Derecha: b.right }; }));
              const dialogo = document.querySelector('[role="dialog"]').getBoundingClientRect();
              const guardar = document.querySelector('#guardar-panel').getBoundingClientRect();
              const elegido = document.querySelector('input.limite.elegido').getBoundingClientRect();
              const encabezado = document.querySelector('#panel-personas thead th').getBoundingClientRect();
              const izquierdas = [...document.querySelectorAll('#panel-personas td.cupo[data-cupo="REFRIGERIO"] input.limite')].map(i => i.getBoundingClientRect().left);
              const caja = document.querySelector('#panel-personas .caja-tabla').getBoundingClientRect();
              const filasVisibles = [...document.querySelectorAll('#panel-personas tbody tr.persona')].map(f => f.getBoundingClientRect())
                .filter(f => f.top >= encabezado.bottom - 1 && f.bottom <= caja.bottom + 1).length;
              return JSON.stringify({ Filas: filas, DialogoAbajo: dialogo.bottom, DialogoDerecha: dialogo.right, GuardarAbajo: guardar.bottom,
                AltoVentana: window.innerHeight, AnchoVentana: window.innerWidth, ElegidoArriba: elegido.top, ElegidoAbajo: elegido.bottom,
                EncabezadoAbajo: encabezado.bottom, CajaAbajo: caja.bottom, FilasVisibles: filasVisibles, Izquierdas: izquierdas });
            }
            """);
        var medida = JsonSerializer.Deserialize<Medida>(json)!;
        _escenario.Registrar("1024 × 768, el teclado: " + string.Join(" | ", medida.Filas.Select(f =>
            $"{f.Length} teclas de {f[0].Ancho:F1} × {f[0].Alto:F1} px, de {f.Min(t => t.Ancho):F1} a {f.Max(t => t.Ancho):F1} de ancho")));
        _escenario.Registrar($"1024 × 768, el diálogo termina en {medida.DialogoAbajo} px y «Guardar» en {medida.GuardarAbajo} px, de {medida.AltoVentana}");
        _escenario.Registrar($"1024 × 768, el límite elegido de {medida.ElegidoArriba:F1} a {medida.ElegidoAbajo:F1} px, debajo del encabezado que termina en {medida.EncabezadoAbajo:F1} y dentro de la tabla que termina en {medida.CajaAbajo:F1}; {medida.FilasVisibles} filas enteras a la vista");
        await _escenario.CapturarAsync(pagina, "personas-teclado-1024x768");

        Assert.Equal(4, medida.Filas.Length);
        Assert.True(medida.ElegidoArriba >= medida.EncabezadoAbajo - Tolerancia && medida.ElegidoAbajo <= medida.CajaAbajo + Tolerancia,
            $"El límite elegido va de {medida.ElegidoArriba} a {medida.ElegidoAbajo} px, con el encabezado hasta {medida.EncabezadoAbajo} y la tabla hasta {medida.CajaAbajo}");
        Assert.True(medida.FilasVisibles >= 2, $"Con el teclado se ven {medida.FilasVisibles} filas enteras");
        _escenario.Registrar($"1024 × 768, los límites de REFRIGERIO empiezan de {medida.Izquierdas.Min():F1} a {medida.Izquierdas.Max():F1} px, en {medida.Izquierdas.Length} filas");
        Assert.True(medida.Izquierdas.Max() - medida.Izquierdas.Min() <= Tolerancia, $"Los límites de una columna empiezan de {medida.Izquierdas.Min()} a {medida.Izquierdas.Max()} px");
        Assert.All(medida.Filas, f => Assert.All(f, t => Assert.True(t.Alto >= 56 - Tolerancia, $"Una tecla mide {t.Alto} px de alto")));
        Assert.All(medida.Filas, f => Assert.True(f.Max(t => t.Ancho) - f.Min(t => t.Ancho) <= Tolerancia, $"Una fila tiene teclas de {f.Min(t => t.Ancho)} a {f.Max(t => t.Ancho)} px"));
        Assert.All(medida.Filas.SelectMany(f => f), t => Assert.True(t.Abajo <= medida.DialogoAbajo + Tolerancia && t.Derecha <= medida.DialogoDerecha + Tolerancia,
            $"Una tecla sale del diálogo: abajo {t.Abajo}, derecha {t.Derecha}"));
        Assert.True(medida.GuardarAbajo <= medida.AltoVentana, $"«Guardar» termina en {medida.GuardarAbajo} px, fuera de la ventana");

        await pagina.Locator(".teclado-en-pantalla .tecla[data-tecla=\"BORRAR\"]").ClickAsync();
        await pagina.Locator(".teclado-en-pantalla .tecla[data-tecla=\"1\"]").ClickAsync();
        await Assertions.Expect(pagina.Locator("input.limite.elegido")).ToHaveValueAsync("1");
        await pagina.Locator("#guardar-panel").ClickAsync();

        await Assertions.Expect(pagina.Locator("#tabla-personas tr.persona", new() { HasText = "Persona sintética 01" })
            .Locator("td.cupo[data-cupo=\"REFRIGERIO\"]")).ToHaveTextAsync("0/1");
    }

    private sealed record Tecla(double Ancho, double Alto, double Abajo, double Derecha);

    private sealed record Caja(string Id, double Arriba, double Alto, double Ancho, string Letra);

    private sealed record Medida(Tecla[][] Filas, double DialogoAbajo, double DialogoDerecha, double GuardarAbajo, double AltoVentana, double AnchoVentana,
        double ElegidoArriba, double ElegidoAbajo, double EncabezadoAbajo, double CajaAbajo, int FilasVisibles, double[] Izquierdas);
}
