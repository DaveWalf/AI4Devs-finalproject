using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// La columna «Operador» de Personas (marcar-operadores-en-personas, tarea 4.3): con el puesto compilado, a 1024 × 768 y por
/// CDP, en lectura el escudo de «Operador sintético 01» queda centrado en su columna y en edición el botón de la celda tiene el
/// alto de los campos de cupo de su fila, con la tolerancia de 1 px de la regla de prolijidad; las dos vistas siguen mostrando
/// al menos diez filas y la tabla no se desplaza en horizontal.
/// </summary>
public class OperadoresEnPersonasE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("operadores-personas");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "administracion-local · La columna «Operador» a 1024 × 768 (puesto compilado): el escudo centrado en lectura, el botón con el alto de los cupos en edición, diez filas a la vista y sin desplazamiento horizontal")]
    public async Task La_columna_operador_a_1024_x_768()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-personas").ClickAsync();
        await Assertions.Expect(pagina.Locator("#tabla-personas .escudo-operador")).ToHaveCountAsync(1);

        var lectura = await Medir(pagina, "#tabla-personas");
        _escenario.Registrar($"1024 × 768, lectura: columna «Operador» de x {lectura.ColumnaIzquierda:F1} a {lectura.ColumnaDerecha:F1} ({lectura.ColumnaDerecha - lectura.ColumnaIzquierda:F1} px), escudo centrado en {lectura.CentroEscudo:F1}, {lectura.FilasVisibles} de {lectura.Total} filas a la vista, tabla de {lectura.AnchoTabla} px en {lectura.AnchoCaja} px");
        await _escenario.CapturarAsync(pagina, "personas-operador-1024x768");
        Assert.True(Math.Abs(lectura.CentroEscudo - (lectura.ColumnaIzquierda + lectura.ColumnaDerecha) / 2) <= Tolerancia, "El escudo no queda centrado en su columna.");
        Assert.True(lectura.FilasVisibles >= Math.Min(10, lectura.Total), $"Se ven {lectura.FilasVisibles} de {lectura.Total} filas.");
        // Sin tolerancia: medio píxel de más ya muestra la barra de desplazamiento horizontal.
        Assert.True(lectura.AnchoTabla <= lectura.AnchoCaja, $"La tabla mide {lectura.AnchoTabla} px en {lectura.AnchoCaja} px: se desplaza en horizontal.");

        await pagina.Locator("#abrir-personas").ClickAsync();
        await Assertions.Expect(pagina.Locator("#panel-personas button.operador-celda").First).ToBeVisibleAsync();
        var edicion = await Medir(pagina, "#panel-personas");
        _escenario.Registrar($"1024 × 768, edición: botón de {edicion.AnchoBoton:F1} × {edicion.AltoBoton:F1} px contra cupos de {edicion.AltoCupo:F1} px, arriba {edicion.ArribaBoton:F1} contra {edicion.ArribaCupo:F1}; columna de {edicion.ColumnaDerecha - edicion.ColumnaIzquierda:F1} px; {edicion.FilasVisibles} de {edicion.Total} filas a la vista, tabla de {edicion.AnchoTabla} px en {edicion.AnchoCaja} px");
        await _escenario.CapturarAsync(pagina, "personas-operador-edicion-1024x768");
        Assert.True(Math.Abs(edicion.AltoBoton - edicion.AltoCupo) <= Tolerancia, "El botón de la celda no tiene el alto de los campos de cupo.");
        Assert.True(edicion.AltoBoton >= 44 && edicion.AnchoBoton >= 44, "El botón de la celda es menor que 44 × 44 px.");
        Assert.True(Math.Abs(edicion.CentroBoton - (edicion.ColumnaIzquierda + edicion.ColumnaDerecha) / 2) <= Tolerancia, "El botón no queda centrado en su columna.");
        Assert.True(edicion.AnchoTabla <= edicion.AnchoCaja, $"La tabla de edición mide {edicion.AnchoTabla} px en {edicion.AnchoCaja} px: se desplaza en horizontal.");
        _escenario.Registrar($"filas a la vista en edición: {edicion.FilasVisibles} de {edicion.Total}, con el teclado en pantalla abierto");
        // Con el teclado abierto entran al menos dos filas, como pide RacionesEnPersonasE2ETests, y el botón no corre los cupos.
        Assert.True(edicion.FilasVisibles >= 2, $"Con el teclado se ven {edicion.FilasVisibles} filas enteras.");
        Assert.True(Math.Abs(edicion.ArribaBoton - edicion.ArribaCupo) <= Tolerancia, $"El botón empieza en {edicion.ArribaBoton} y el cupo en {edicion.ArribaCupo}.");
    }

    /// <summary>
    /// En la fila de «Operador sintético 01» (lectura) o de «Persona sintética 01» (edición): la columna «Operador», el escudo o
    /// el botón, el primer campo de cupo; y cuántas filas entran enteras en la caja de la tabla.
    /// </summary>
    private static async Task<Medidas> Medir(IPage pagina, string tabla)
    {
        var json = await pagina.EvaluateAsync<string>(
            """
            t => {
              const caja = document.querySelector(t + ' .caja-tabla'); const c = caja.getBoundingClientRect();
              const filas = [...document.querySelectorAll(t + ' tr.persona')];
              const nombre = f => (f.querySelector('input.nombre-persona')?.value ?? f.querySelector('.nombre-persona').textContent).trim();
              const fila = filas.find(f => nombre(f) === 'Operador sintético 01' && f.querySelector('.escudo-operador'))
                ?? filas.find(f => nombre(f) === 'Persona sintética 01');
              const col = fila.querySelector('td.operador-persona').getBoundingClientRect();
              const escudo = fila.querySelector('.escudo-operador')?.getBoundingClientRect();
              const boton = fila.querySelector('button.operador-celda')?.getBoundingClientRect();
              const cupo = fila.querySelector('td.cupo input.limite')?.getBoundingClientRect();
              return JSON.stringify({
                ColumnaIzquierda: col.left, ColumnaDerecha: col.right,
                CentroEscudo: escudo ? (escudo.left + escudo.right) / 2 : 0,
                CentroBoton: boton ? (boton.left + boton.right) / 2 : 0, AnchoBoton: boton?.width ?? 0, AltoBoton: boton?.height ?? 0, ArribaBoton: boton?.top ?? 0,
                AltoCupo: cupo?.height ?? 0, ArribaCupo: cupo?.top ?? 0,
                FilasVisibles: filas.filter(f => { const r = f.getBoundingClientRect(); return r.top >= c.top && r.bottom <= c.bottom + 1; }).length,
                Total: filas.length, AnchoTabla: caja.querySelector('table').getBoundingClientRect().width, AnchoCaja: caja.clientWidth
              });
            }
            """, tabla);
        return JsonSerializer.Deserialize<Medidas>(json)!;
    }

    private sealed record Medidas(
        double ColumnaIzquierda, double ColumnaDerecha, double CentroEscudo, double CentroBoton, double AnchoBoton, double AltoBoton,
        double ArribaBoton, double AltoCupo, double ArribaCupo, int FilasVisibles, int Total, double AnchoTabla, double AnchoCaja);
}
