using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Exportar Opciones, cambiar el archivo e importarlo reemplazando el catálogo, con el puesto compilado
/// (importar-opciones-desde-excel, D7): por CDP y a 1024 × 768, con la tolerancia de 1 px de la regla de prolijidad. La
/// carpeta de exportación se deja elegida en <c>exportacion.json</c>, y el diálogo de archivos se contesta con
/// <see cref="DialogoDeArchivosE2E"/>.
/// </summary>
public class ImportarOpcionesE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private const string TituloDialogo = "Planilla de Opciones para importar";

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("importar-opciones");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "administracion-local · Importar Opciones desde Excel (puesto compilado): a 1024 × 768, los tres botones del pie de Opciones miden lo mismo con la letra y el alto de «Cerrar»; el catálogo exportado, con un nombre cambiado y vuelto a importar, muestra el resumen con sus dos botones parejos y «Reemplazar» deja la versión 2")]
    public async Task Exportar_cambiar_e_importar()
    {
        var destino = Directory.CreateDirectory(Path.Combine(_escenario.Carpeta, "exportaciones")).FullName;
        File.WriteAllText(Path.Combine(_escenario.Carpeta, "exportacion.json"), JsonSerializer.Serialize(new { carpeta = destino }));
        using var playwright = await Playwright.CreateAsync();
        var (puesto, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-opciones").ClickAsync();
        await Assertions.Expect(pagina.Locator("#importar-opciones")).ToBeVisibleAsync();

        var pie = await MedirAsync(pagina, ["#exportar-opciones", "#importar-opciones", "#planilla-opciones", "#cancelar-configuracion"]);
        _escenario.Registrar("1024 × 768, el pie de Opciones: " + string.Join("; ", pie.Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1} px desde {c.Arriba:F1}, letra {c.Letra}")));
        await _escenario.CapturarAsync(pagina, "opciones-pie-importar-1024x768");
        var cerrar = pie[^1];
        Assert.All(pie, c => Assert.True(Math.Abs(c.Alto - cerrar.Alto) <= Tolerancia && Math.Abs(c.Arriba - cerrar.Arriba) <= Tolerancia && c.Letra == cerrar.Letra,
            $"{c.Id}: {c.Alto} px desde {c.Arriba}, letra {c.Letra}, contra «Cerrar»"));
        Assert.All(pie[..3], c => Assert.True(Math.Abs(c.Ancho - pie[0].Ancho) <= Tolerancia, $"{c.Id} mide {c.Ancho} px y el primero {pie[0].Ancho}"));

        await pagina.Locator("#exportar-opciones").ClickAsync();
        await Assertions.Expect(pagina.Locator("#mensaje-archivos-opciones")).ToContainTextAsync("Exportado");
        var exportado = Assert.Single(Directory.GetFiles(destino, "INComedor-opciones-*.xlsx"));
        _escenario.Registrar($"catálogo exportado: {Path.GetFileName(exportado)}");

        // El cambio: «Almuerzo común» pasa a «Almuerzo Común», que es la misma opción con otro nombre escrito.
        Cambiar(exportado, (">Almuerzo común<", ">Almuerzo Común<"));

        var toque = pagina.Locator("#importar-opciones").ClickAsync();
        await DialogoDeArchivosE2E.ElegirAsync(puesto, TituloDialogo, exportado, _escenario.Registrar);
        await toque;
        var resumen = pagina.Locator("#resumen-importacion-opciones");
        await Assertions.Expect(resumen).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(resumen).ToContainTextAsync("0 opciones nuevas, 1 modificada");
        var cartel = await MedirAsync(pagina, ["#importar-opciones-reemplazar", "#importar-opciones-cancelar"]);
        var abajo = await pagina.EvaluateAsync<double>("() => document.querySelector('#resumen-importacion-opciones').getBoundingClientRect().bottom");
        _escenario.Registrar("1024 × 768, el resumen de Opciones: " + string.Join("; ", cartel.Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1} px desde {c.Arriba:F1}, letra {c.Letra}"))
            + $"; el cartel termina en {abajo:F1} px");
        await _escenario.CapturarAsync(pagina, "opciones-resumen-importacion-1024x768");
        Assert.All(cartel, c => Assert.True(Math.Abs(c.Alto - cartel[0].Alto) <= Tolerancia && Math.Abs(c.Arriba - cartel[0].Arriba) <= Tolerancia
            && c.Letra == cartel[0].Letra && c.Alto >= 56 - Tolerancia, $"{c.Id}: {c.Alto} px desde {c.Arriba}, letra {c.Letra}"));
        Assert.True(abajo <= 768, $"El cartel termina en {abajo} px, fuera de la ventana");

        await pagina.Locator("#importar-opciones-reemplazar").ClickAsync();

        await Assertions.Expect(resumen).ToHaveCountAsync(0);
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToContainTextAsync("versión 2");
        await Assertions.Expect(pagina.Locator("#catalogo-opciones .nombre-opcion", new() { HasTextString = "Almuerzo Común" })).ToHaveCountAsync(1);
        await _escenario.CapturarAsync(pagina, "opciones-importadas-1024x768");
    }

    private static async Task<Caja[]> MedirAsync(IPage pagina, string[] ids) =>
        JsonSerializer.Deserialize<Caja[]>(await pagina.EvaluateAsync<string>(
            """
            ids => JSON.stringify(ids.map(i => {
              const e = document.querySelector(i), b = e.getBoundingClientRect();
              return { Id: i, Arriba: b.top, Alto: b.height, Ancho: b.width, Letra: getComputedStyle(e).fontSize };
            }))
            """, ids))!;

    /// <summary>Reemplaza textos en la hoja de un libro del puesto, que guarda las cadenas en línea.</summary>
    private static void Cambiar(string libro, params (string Antes, string Despues)[] cambios)
    {
        using var zip = ZipFile.Open(libro, ZipArchiveMode.Update);
        var entrada = zip.GetEntry("xl/worksheets/sheet1.xml")!;
        string xml;
        using (var lector = new StreamReader(entrada.Open(), Encoding.UTF8))
        {
            xml = lector.ReadToEnd();
        }

        foreach (var (antes, despues) in cambios)
        {
            Assert.Contains(antes, xml, StringComparison.Ordinal);
            xml = xml.Replace(antes, despues, StringComparison.Ordinal);
        }

        entrada.Delete();
        using var escritor = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open(), new UTF8Encoding(false));
        escritor.Write(xml);
    }

    private sealed record Caja(string Id, double Arriba, double Alto, double Ancho, string Letra);
}
