using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// «Configuración» de un operador identificado en reposo (operador-desde-la-identificacion, tarea 3.3): con el puesto
/// compilado, a 1024 × 768 y por CDP, en la selección de un operador con comidas «Configuración» y «Cancelar» comparten el
/// borde de arriba, el alto, la letra y el relleno, y en la pantalla sin opciones de 9001 «Configuración» y «Listo» comparten
/// el alto, el ancho mínimo y la letra, con la tolerancia de 1 px de la regla de prolijidad. Para el operador con comidas, la
/// prueba le da OPERATOR a «Persona sintética 01» por SQL en la versión activa.
/// </summary>
public class OperadorDesdeLaIdentificacionE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("operador-identificacion");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "atencion-puesto · «Configuración» desde la identificación a 1024 × 768 (puesto compilado): en la selección junto a «Cancelar» y sin opciones junto a «Listo», del mismo alto y letra, y abre Configuración con Consumos, Personas e Impresora")]
    public async Task Configuracion_desde_la_identificacion_a_1024_x_768()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        DarOperadorA0001();

        await pagina.Locator("#codigo").FillAsync("0001");
        await pagina.Locator("#ingresar").ClickAsync();
        await Assertions.Expect(pagina.Locator("#configuracion-operador")).ToBeVisibleAsync();
        var seleccion = await Medir(pagina, "#configuracion-operador", "#cancelar");
        _escenario.Registrar($"1024 × 768, la selección de un operador con comidas: {string.Join("; ", seleccion.Select(c => c.ToString()))}");
        await _escenario.CapturarAsync(pagina, "operador-seleccion-1024x768");
        Assert.True(seleccion[0].Derecha <= seleccion[1].Izquierda, "«Configuración» no queda a la izquierda de «Cancelar».");
        Assert.True(Math.Abs(seleccion[0].Arriba - seleccion[1].Arriba) <= Tolerancia, "No comparten el borde de arriba.");
        Assert.True(Math.Abs(seleccion[0].Alto - seleccion[1].Alto) <= Tolerancia, "No comparten el alto.");
        Assert.Equal(seleccion[1].Letra, seleccion[0].Letra);
        Assert.Equal(seleccion[1].Relleno, seleccion[0].Relleno);
        Assert.Equal(seleccion[1].Clase, seleccion[0].Clase);
        await pagina.Locator("#cancelar").ClickAsync();
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        await pagina.Locator("#codigo").FillAsync("9001");
        await pagina.Locator("#ingresar").ClickAsync();
        await Assertions.Expect(pagina.Locator("#listo")).ToBeVisibleAsync();
        var sinOpciones = await Medir(pagina, "#configuracion-operador", "#listo");
        _escenario.Registrar($"1024 × 768, la pantalla sin opciones de 9001: {string.Join("; ", sinOpciones.Select(c => c.ToString()))}");
        await _escenario.CapturarAsync(pagina, "operador-sin-opciones-1024x768");
        Assert.True(sinOpciones[0].Derecha <= sinOpciones[1].Izquierda, "«Configuración» no queda a la izquierda de «Listo».");
        Assert.True(Math.Abs(sinOpciones[0].Alto - sinOpciones[1].Alto) <= Tolerancia, "No comparten el alto.");
        Assert.True(Math.Abs(sinOpciones[0].Arriba - sinOpciones[1].Arriba) <= Tolerancia, "No comparten el borde de arriba.");
        Assert.Equal(sinOpciones[1].AnchoMinimo, sinOpciones[0].AnchoMinimo);
        Assert.Equal(sinOpciones[1].Letra, sinOpciones[0].Letra);

        await pagina.Locator("#configuracion-operador").ClickAsync();
        await Assertions.Expect(pagina.Locator("[role=\"tab\"]")).ToHaveCountAsync(3);
        var pestanas = await pagina.Locator("[role=\"tab\"]").EvaluateAllAsync<string[]>("ts => ts.map(t => t.id)");
        _escenario.Registrar($"pestañas del operador entrado desde la identificación: {string.Join(", ", pestanas)}");
        Assert.Equal(["pestana-consumos", "pestana-personas", "pestana-impresora"], pestanas);
    }

    /// <summary>OPERATOR vigente y sin fin para «Persona sintética 01» en la versión activa, con el puesto abierto.</summary>
    private void DarOperadorA0001()
    {
        using var conexion = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_escenario.Carpeta, "incomedor.db"),
            Pooling = false,
        }.ConnectionString);
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            INSERT INTO tbComedorPermisosOperadores
            VALUES ((SELECT comvcCodigoActiva FROM tbComedorConfiguracionPuestos), $pop, '00000000-0000-4000-8000-000000000027', 'OPERATOR', NULL, NULL, NULL)
            """;
        comando.Parameters.AddWithValue("$pop", Guid.NewGuid().ToString("D"));
        Assert.Equal(1, comando.ExecuteNonQuery());
    }

    private static async Task<Caja[]> Medir(IPage pagina, params string[] selectores)
    {
        var json = await pagina.EvaluateAsync<string>(
            """
            s => JSON.stringify(s.map(x => { const e = document.querySelector(x); const b = e.getBoundingClientRect(); const c = getComputedStyle(e);
              return { Id: x, Izquierda: b.left, Derecha: b.right, Arriba: b.top, Alto: b.height, Letra: c.fontSize, Relleno: c.paddingLeft + ' ' + c.paddingRight,
                AnchoMinimo: c.minWidth, Clase: e.className }; }))
            """, selectores);
        return JsonSerializer.Deserialize<Caja[]>(json)!;
    }

    private sealed record Caja(string Id, double Izquierda, double Derecha, double Arriba, double Alto, string Letra, string Relleno, string AnchoMinimo, string Clase)
    {
        public override string ToString() =>
            $"{Id} de x {Izquierda:F1} a {Derecha:F1}, arriba {Arriba:F1}, alto {Alto:F1} px, letra {Letra}, relleno {Relleno}, ancho mínimo {AnchoMinimo}, clase «{Clase}»";
    }
}
