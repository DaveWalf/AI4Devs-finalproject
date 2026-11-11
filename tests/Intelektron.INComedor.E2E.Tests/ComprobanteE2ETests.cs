using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Los datos del comprobante y el comprobante enriquecido, con el puesto compilado (enriquecer-comprobante, D7 y D8): por
/// CDP y a 1024 × 768, con la tolerancia de 1 px de la regla de prolijidad. Guarda el puesto 3 «Comedor Norte» con el
/// correlativo desde 1000, confirma un consumo con 0001 y lee el comprobante en pantalla y en el archivo de la impresora.
/// </summary>
public class ComprobanteE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("comprobante");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "comprobante-local · Datos del comprobante (puesto compilado): a 1024 × 768, los campos y el «Guardar» de la sección miden como los de los textos; con el puesto 3 y el correlativo desde 1000, el comprobante en pantalla dice el puesto, el 1000, el comensal y la frase del archivo, y entra en la ventana")]
    public async Task Datos_y_comprobante()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-impresora").ClickAsync();
        await pagina.Locator("#guardar-datos-comprobante").ScrollIntoViewIfNeededAsync();

        var campos = await MedirAsync(pagina, ["#encabezado-comprobante", "#numero-puesto", "#nombre-puesto", "#numeracion-codigo", "#numeracion-correlativo", "#desde-correlativo"]);
        var guardar = await MedirAsync(pagina, ["#guardar-textos", "#guardar-datos-comprobante"]);
        _escenario.Registrar("1024 × 768, Datos del comprobante: " + string.Join("; ", campos.Concat(guardar).Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1} px desde x {c.Izquierda:F1}, letra {c.Letra}")));
        await _escenario.CapturarAsync(pagina, "impresora-datos-comprobante-1024x768");
        var encabezado = campos[0];
        Assert.All(campos, c => Assert.True(Math.Abs(c.Alto - encabezado.Alto) <= Tolerancia && c.Letra == encabezado.Letra,
            $"{c.Id}: {c.Alto} px, letra {c.Letra}, contra el encabezado"));
        Assert.All(campos[1..3], c => Assert.True(Math.Abs(c.Izquierda - encabezado.Izquierda) <= Tolerancia && Math.Abs(c.Ancho - encabezado.Ancho) <= Tolerancia,
            $"{c.Id}: desde x {c.Izquierda} con {c.Ancho} px, contra el encabezado desde x {encabezado.Izquierda} con {encabezado.Ancho} px"));
        var desde = campos[^1];
        Assert.True(Math.Abs(desde.Izquierda + desde.Ancho - (encabezado.Izquierda + encabezado.Ancho)) <= Tolerancia,
            $"el «desde» termina en x {desde.Izquierda + desde.Ancho} y el encabezado en x {encabezado.Izquierda + encabezado.Ancho}");
        Assert.True(Math.Abs(guardar[0].Alto - guardar[1].Alto) <= Tolerancia && Math.Abs(guardar[0].Ancho - guardar[1].Ancho) <= Tolerancia && guardar[0].Letra == guardar[1].Letra,
            $"los dos «Guardar»: {guardar[0].Ancho} × {guardar[0].Alto} y {guardar[1].Ancho} × {guardar[1].Alto}");

        await pagina.Locator("#numero-puesto").FillAsync("3");
        await pagina.Locator("#nombre-puesto").FillAsync("Comedor Norte");
        await pagina.Locator("#numeracion-correlativo").ClickAsync();
        await pagina.Locator("#desde-correlativo").FillAsync("1000");
        await pagina.Locator("#guardar-datos-comprobante").ClickAsync();
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToContainTextAsync("Se guardaron los datos del comprobante");
        await pagina.Locator("#cancelar-configuracion").ClickAsync();
        // Con el diálogo abierto la atención es inerte: el código se tipea recién cuando el diálogo se fue.
        await Assertions.Expect(pagina.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);

        await pagina.Locator("#codigo").FillAsync("0001");
        await pagina.Locator("#ingresar").ClickAsync();
        await pagina.Locator(".opcion").First.ClickAsync();
        await pagina.Locator("#confirmar").ClickAsync();
        var comprobante = pagina.Locator(".comprobante");
        await Assertions.Expect(comprobante).ToBeVisibleAsync(new() { Timeout = 15000 });
        // La animación de salida del comprobante dura 1,2 s: se mide y se captura con el comprobante quieto.
        await Task.Delay(1500, TestContext.Current.CancellationToken);
        var texto = await comprobante.InnerTextAsync();
        var archivo = Assert.Single(Directory.GetFiles(Path.Combine(_escenario.Carpeta, "comprobantes")));
        var lineas = File.ReadAllLines(archivo);
        var abajo = await pagina.EvaluateAsync<double>("() => document.querySelector('.comprobante').getBoundingClientRect().bottom");
        _escenario.Registrar($"comprobante en pantalla, hasta {abajo:F1} px: {texto.Replace('\n', '|')}");
        _escenario.Registrar("archivo de la impresora: " + string.Join("|", lineas));
        await _escenario.CapturarAsync(pagina, "comprobante-enriquecido-1024x768");
        Assert.Contains("Puesto 3 · Comedor Norte", texto);
        Assert.Contains("N.º 1000", texto);
        Assert.Contains("Persona sintética 01", texto);
        Assert.Contains(lineas[^1], texto);
        Assert.Contains("Número: 1000", lineas);
        Assert.Contains("Comensal: Persona sintética 01", lineas);
        Assert.DoesNotContain(lineas, l => l.Contains("0001", StringComparison.Ordinal) && !l.StartsWith('{') && !l.StartsWith("Comprobante:", StringComparison.Ordinal));
        Assert.True(abajo <= 768, $"El comprobante termina en {abajo} px, fuera de la ventana");
    }

    private static async Task<Caja[]> MedirAsync(IPage pagina, string[] ids) =>
        JsonSerializer.Deserialize<Caja[]>(await pagina.EvaluateAsync<string>(
            """
            ids => JSON.stringify(ids.map(i => {
              const e = document.querySelector(i), b = e.getBoundingClientRect();
              return { Id: i, Izquierda: b.left, Alto: b.height, Ancho: b.width, Letra: getComputedStyle(e).fontSize };
            }))
            """, ids))!;

    private sealed record Caja(string Id, double Izquierda, double Alto, double Ancho, string Letra);
}
