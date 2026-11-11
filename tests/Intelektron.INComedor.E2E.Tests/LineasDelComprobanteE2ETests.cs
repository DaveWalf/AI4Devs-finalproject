using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Las líneas del comprobante en la pestaña Impresora (elegir-lineas-del-comprobante, tarea 3.3): con el puesto compilado, a
/// 1024 × 768 y por CDP, los rótulos de las tres líneas caen en la columna de «Encabezado» y «Pie», los tres pares empiezan en
/// la misma vertical, y los seis botones tienen el ancho, el alto y la letra de los pares de Mails, con la tolerancia de 1 px
/// de la regla de prolijidad.
/// </summary>
public class LineasDelComprobanteE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private static readonly string[] Botones =
        ["#comensal-imprimir", "#comensal-no-imprimir", "#aviso-reimpresion-imprimir", "#aviso-reimpresion-no-imprimir", "#operador-imprimir", "#operador-no-imprimir"];

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("lineas-comprobante");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "comprobante-local · Las líneas del comprobante a 1024 × 768 (puesto compilado): los rótulos en la columna de «Encabezado», los tres pares en la misma vertical y los seis botones del ancho, el alto y la letra de los pares de Mails")]
    public async Task Las_lineas_a_1024_x_768()
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

        await pagina.Locator("#pestana-mails").ClickAsync();
        var mails = await Medir(pagina, "#correos-activos", "#correos-apagados");
        _escenario.Registrar($"1024 × 768, los pares de Mails: {string.Join("; ", mails.Select(c => c.ToString()))}");

        await pagina.Locator("#pestana-impresora").ClickAsync();
        await pagina.Locator("#operador-no-imprimir").ScrollIntoViewIfNeededAsync();
        var rotulos = await Medir(pagina, "label[for=\"encabezado-comprobante\"]", "label[for=\"pie-comprobante\"]", "#rotulo-linea-comensal", "#rotulo-linea-aviso", "#rotulo-linea-operador");
        var botones = await Medir(pagina, Botones);
        var campo = (await Medir(pagina, "#pie-comprobante"))[0];
        _escenario.Registrar($"1024 × 768, los rótulos: {string.Join("; ", rotulos.Select(c => c.ToString()))}");
        _escenario.Registrar($"1024 × 768, los botones de las líneas: {string.Join("; ", botones.Select(c => c.ToString()))}; el campo del pie empieza en {campo.Izquierda:F1}");
        await _escenario.CapturarAsync(pagina, "impresora-lineas-1024x768");

        Assert.All(rotulos, r => Assert.True(Math.Abs(r.Izquierda - rotulos[0].Izquierda) <= Tolerancia, $"{r.Id} empieza en {r.Izquierda} px, contra {rotulos[0].Izquierda} px."));
        Assert.All(rotulos.Skip(2), r => Assert.Equal(rotulos[0].Letra, r.Letra));
        var primeros = botones.Where((_, i) => i % 2 == 0).ToList();
        Assert.All(primeros, b => Assert.True(Math.Abs(b.Izquierda - campo.Izquierda) <= Tolerancia, $"{b.Id} empieza en {b.Izquierda} px, y el campo del pie en {campo.Izquierda} px."));
        Assert.All(botones, b => Assert.True(Math.Abs(b.Alto - mails[0].Alto) <= Tolerancia && b.Letra == mails[0].Letra,
            $"{b.Id}: alto {b.Alto} px y letra {b.Letra}, contra {mails[0].Alto} px y {mails[0].Letra} en Mails."));
        // Los de Mails no comparten ancho entre sí (lo da su texto, con la negrita del presionado): los seis de las líneas sí, y
        // ninguno es más angosto que ellos.
        Assert.All(botones, b => Assert.True(Math.Abs(b.Ancho - botones[0].Ancho) <= Tolerancia && b.Ancho >= mails.Max(m => m.Ancho) - Tolerancia,
            $"{b.Id}: ancho {b.Ancho} px, contra {botones[0].Ancho} px del primero y {mails.Max(m => m.Ancho)} px del más ancho de Mails."));
        for (var i = 0; i < botones.Length; i += 2)
        {
            Assert.True(Math.Abs(botones[i].Arriba - botones[i + 1].Arriba) <= Tolerancia, $"{botones[i].Id} y {botones[i + 1].Id} no comparten el borde de arriba.");
        }

        Assert.All(botones, b => Assert.True(b.Abajo <= 768, $"{b.Id} termina en {b.Abajo} px, fuera de la ventana."));
        // El borde según el estado: el presionado lleva el del acento, como en Mails.
        Assert.All(botones, b => Assert.Equal(mails.First(m => m.Presionado == b.Presionado).Borde, b.Borde));

        // Con «No imprimir» presionado en las tres, en negrita y con el borde de 3 px, los anchos no cambian.
        foreach (var no in Botones.Where((_, i) => i % 2 == 1))
        {
            await pagina.Locator(no).ClickAsync();
        }

        var presionados = await Medir(pagina, Botones);
        _escenario.Registrar($"1024 × 768, con «No imprimir» presionado: {string.Join("; ", presionados.Select(c => c.ToString()))}");
        await _escenario.CapturarAsync(pagina, "impresora-lineas-no-imprimir-1024x768");
        Assert.All(presionados, b => Assert.True(Math.Abs(b.Ancho - botones[0].Ancho) <= Tolerancia, $"{b.Id}: ancho {b.Ancho} px, contra {botones[0].Ancho} px."));
    }

    private static async Task<Caja[]> Medir(IPage pagina, params string[] selectores)
    {
        var json = await pagina.EvaluateAsync<string>(
            """
            s => JSON.stringify(s.map(x => { const e = document.querySelector(x); const b = e.getBoundingClientRect();
              const c = getComputedStyle(e);
              return { Id: x, Izquierda: b.left, Arriba: b.top, Abajo: b.bottom, Ancho: b.width, Alto: b.height, Letra: c.fontSize, Borde: c.borderTopColor + ' ' + c.borderRadius,
                Presionado: e.getAttribute('aria-pressed') === 'true' }; }))
            """, selectores);
        return JsonSerializer.Deserialize<Caja[]>(json)!;
    }

    private sealed record Caja(string Id, double Izquierda, double Arriba, double Abajo, double Ancho, double Alto, string Letra, string Borde, bool Presionado)
    {
        public override string ToString() => $"{Id} en x {Izquierda:F1}, arriba {Arriba:F1}, {Ancho:F1} × {Alto:F1} px, letra {Letra}, borde {Borde}";
    }
}
