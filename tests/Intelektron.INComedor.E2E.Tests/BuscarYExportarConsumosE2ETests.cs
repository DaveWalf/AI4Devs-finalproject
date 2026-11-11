using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Consumos con la búsqueda, el número, «Ver» y la exportación, con el puesto compilado (buscar-y-exportar-consumos): por CDP y
/// a 1024 × 768, lo que bUnit no mide, con la tolerancia de 1 px de la regla de prolijidad (D8). La carpeta de exportación se
/// deja elegida en <c>exportacion.json</c> antes de exportar, porque el diálogo de carpetas de Windows no se maneja por CDP.
/// </summary>
public class BuscarYExportarConsumosE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("buscar-exportar");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "consulta-consumos · Buscar, ver y exportar a 1024 × 768 (puesto compilado): las 24 horas con fecha y el número, la búsqueda por número, «Ver» sin operador, los filtros, las acciones y la exportación del mismo alto, la tabla sin desplazamiento horizontal, y un CSV y un Excel en la carpeta elegida")]
    public async Task Buscar_ver_y_exportar_a_1024_x_768()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await FijarArea(pagina, 1024, 768);
        await pagina.Locator("#codigo").FillAsync("0001");
        await pagina.Locator("#ingresar").ClickAsync();
        await pagina.Locator(".opcion").First.ClickAsync();
        await pagina.Locator("#confirmar").ClickAsync();
        await pagina.Locator("#listo").ClickAsync();
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        var destino = Directory.CreateDirectory(Path.Combine(_escenario.Carpeta, "exportaciones")).FullName;
        File.WriteAllText(Path.Combine(_escenario.Carpeta, "exportacion.json"), JsonSerializer.Serialize(new { carpeta = destino }));

        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        var filas = pagina.Locator("#seccion-consumos tr.consumo");
        await Assertions.Expect(filas).ToHaveCountAsync(1);
        var numero = (await filas.Locator("td.numero").InnerTextAsync()).Trim();
        var hora = (await filas.Locator("td.hora").InnerTextAsync()).Trim();
        _escenario.Registrar($"la fila de las últimas 24 horas: hora «{hora}», N.º «{numero}»");
        Assert.Matches("^[0-9a-f]{8}$", numero);
        Assert.Matches(@"^\d{2}/\d{2} \d{2}:\d{2}$", hora);

        await pagina.Locator("#consumos-texto").FillAsync("nadie se llama así");
        await pagina.Locator("#consultar-consumos").ClickAsync();
        await Assertions.Expect(filas).ToHaveCountAsync(0);
        await Assertions.Expect(pagina.Locator("#sin-consumos")).ToBeVisibleAsync();
        await pagina.Locator("#consumos-texto").FillAsync(numero.ToUpperInvariant());
        await pagina.Locator("#consultar-consumos").ClickAsync();
        await Assertions.Expect(filas).ToHaveCountAsync(1);

        var medidas = await Medir(pagina);
        _escenario.Registrar($"1024 × 768, filtros: {string.Join("; ", medidas.Filtros.Select(c => c.ToString()))}");
        _escenario.Registrar($"1024 × 768, acciones de la fila: {string.Join("; ", medidas.Acciones.Select(c => c.ToString()))}");
        _escenario.Registrar($"1024 × 768, exportar: {string.Join("; ", medidas.Exportar.Select(c => c.ToString()))}");
        _escenario.Registrar($"1024 × 768, tabla de {medidas.AnchoTabla} px en una caja de {medidas.AnchoCaja} px; el diálogo termina en {medidas.DialogoAbajo} px de {medidas.AltoVentana}");
        await _escenario.CapturarAsync(pagina, "consumos-busqueda-1024x768");

        var alto = medidas.Filtros[0].Alto;
        Assert.All(medidas.Filtros, c => Assert.True(Math.Abs(c.Alto - alto) <= Tolerancia && c.Alto >= 56, $"{c.Id}: {c.Alto} px de alto, contra {alto} px"));
        Assert.All(medidas.Filtros, c => Assert.Equal("16px", c.Letra));
        foreach (var renglon in medidas.Filtros.GroupBy(c => Math.Round(c.Arriba)))
        {
            Assert.All(renglon, c => Assert.True(Math.Abs(c.Abajo - renglon.First().Abajo) <= Tolerancia, $"{c.Id} termina en {c.Abajo} px en su renglón"));
            Assert.True(renglon.Count() > 1, $"{renglon.First().Id} queda solo en su renglón de filtros");
        }

        Assert.Equal(2, medidas.Acciones.Length);
        Assert.All(medidas.Acciones, c => Assert.True(Math.Abs(c.Alto - medidas.Acciones[0].Alto) <= Tolerancia && c.Alto >= 44 && c.Letra == medidas.Acciones[0].Letra,
            $"{c.Id}: {c.Alto} px y letra {c.Letra}, contra {medidas.Acciones[0].Alto} px y {medidas.Acciones[0].Letra}"));
        var consultar = medidas.Filtros.Single(c => c.Id == "#consultar-consumos");
        Assert.All(medidas.Exportar, c => Assert.True(Math.Abs(c.Alto - consultar.Alto) <= Tolerancia && c.Letra == consultar.Letra,
            $"{c.Id}: {c.Alto} px y letra {c.Letra}, contra «Consultar» de {consultar.Alto} px y {consultar.Letra}"));
        Assert.All(medidas.Exportar, c => Assert.True(Math.Abs(c.Arriba - medidas.Exportar[0].Arriba) <= Tolerancia, $"{c.Id} empieza en {c.Arriba} px"));
        Assert.All(medidas.Exportar, c => Assert.True(Math.Abs(c.Derecha - c.Izquierda - (medidas.Exportar[0].Derecha - medidas.Exportar[0].Izquierda)) <= Tolerancia,
            $"{c.Id} mide {c.Derecha - c.Izquierda:F1} px de ancho, contra {medidas.Exportar[0].Derecha - medidas.Exportar[0].Izquierda:F1}"));
        Assert.All(medidas.Exportar, c => Assert.True(c.Abajo <= Math.Min(medidas.DialogoAbajo, medidas.AltoVentana) + Tolerancia, $"{c.Id} termina en {c.Abajo} px"));
        Assert.True(medidas.AnchoTabla <= medidas.AnchoCaja + Tolerancia, $"La tabla mide {medidas.AnchoTabla} px en una caja de {medidas.AnchoCaja} px: se desplaza en horizontal.");

        await filas.Locator(".ver").ClickAsync();
        var papel = pagina.Locator("#comprobante-en-pantalla");
        await Assertions.Expect(papel).ToContainTextAsync("COPIA EN PANTALLA");
        await Assertions.Expect(papel).ToContainTextAsync(numero);
        await Assertions.Expect(pagina.Locator("#codigo-operador")).ToHaveCountAsync(0);
        await Assertions.Expect(pagina.Locator("#cerrar-comprobante")).ToBeInViewportAsync(new() { Ratio = 1 });
        await _escenario.CapturarAsync(pagina, "consumos-ver-1024x768");
        await pagina.Locator("#cerrar-comprobante").ClickAsync();
        await Assertions.Expect(papel).ToHaveCountAsync(0);

        await pagina.Locator("#exportar-csv").ClickAsync();
        await Assertions.Expect(pagina.Locator("#mensaje-exportacion")).ToContainTextAsync(".csv");
        await pagina.Locator("#exportar-xlsx").ClickAsync();
        await Assertions.Expect(pagina.Locator("#mensaje-exportacion")).ToContainTextAsync(".xlsx");
        var archivos = Directory.GetFiles(destino).Select(Path.GetFileName).Order().ToList();
        _escenario.Registrar($"exportados en la carpeta elegida: {string.Join(", ", archivos)}");
        await _escenario.CapturarAsync(pagina, "consumos-exportado-1024x768");
        Assert.Single(archivos, a => a!.EndsWith(".csv", StringComparison.Ordinal));
        Assert.Single(archivos, a => a!.EndsWith(".xlsx", StringComparison.Ordinal));
        Assert.Equal(2, File.ReadAllLines(Path.Combine(destino, archivos.Single(a => a!.EndsWith(".csv", StringComparison.Ordinal))!)).Length);
    }

    /// <summary>Las cajas de los filtros, de las acciones de la fila y de la exportación, y el ancho de la tabla en su caja.</summary>
    private static async Task<Medidas> Medir(IPage pagina)
    {
        var json = await pagina.EvaluateAsync<string>(
            """
            () => {
              const caja = e => { const b = e.getBoundingClientRect(); return { Id: e.id ? '#' + e.id : '.' + e.className, Arriba: b.top, Abajo: b.bottom,
                Izquierda: b.left, Derecha: b.right, Alto: b.height, Letra: getComputedStyle(e).fontSize }; };
              const ids = s => s.map(i => caja(document.querySelector(i)));
              const tabla = document.querySelector('#seccion-consumos .caja-tabla');
              return JSON.stringify({
                Filtros: ids(['#consumos-desde', '#consumos-hasta', '#ultimas-24h', '#consumos-texto', '#consumos-codigo', '#consumos-persona',
                  '#consumos-servicio', '#consumos-opcion', '#consultar-consumos']),
                Acciones: [...document.querySelectorAll('#seccion-consumos tr.consumo .acciones-consumo button')].map(caja),
                Exportar: ids(['#exportar-csv', '#exportar-xlsx', '#cambiar-carpeta']),
                AnchoTabla: tabla.scrollWidth, AnchoCaja: tabla.clientWidth,
                DialogoAbajo: document.querySelector('[role="dialog"]').getBoundingClientRect().bottom, AltoVentana: window.innerHeight
              });
            }
            """);
        return JsonSerializer.Deserialize<Medidas>(json)!;
    }

    private static async Task FijarArea(IPage pagina, int ancho, int alto)
    {
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = ancho, ["height"] = alto, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
    }

    private sealed record Caja(string Id, double Arriba, double Abajo, double Izquierda, double Derecha, double Alto, string Letra)
    {
        public override string ToString() => $"{Id} de {Izquierda:F1} a {Derecha:F1} px y de {Arriba:F1} a {Abajo:F1} px, letra {Letra}";
    }

    private sealed record Medidas(Caja[] Filtros, Caja[] Acciones, Caja[] Exportar, double AnchoTabla, double AnchoCaja, double DialogoAbajo, double AltoVentana);
}
