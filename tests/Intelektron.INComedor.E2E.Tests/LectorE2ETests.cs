using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// El medio de identificación, con el puesto compilado (elegir-medio-de-identificacion, D6 y D8): por CDP y a 1024 × 768,
/// con la tolerancia de 1 px de la regla de prolijidad. Las once pestañas en una fila; la pestaña Lector medida contra
/// Impresora; con el simulador interno y el relleno 4, «1» por el lector simulado identifica a la persona de 0001.
/// </summary>
public class LectorE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("lector");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "lector-identificacion · Pasar un código por el lector simulado (puesto compilado): a 1024 × 768 las once pestañas entran en una fila, la pestaña Lector tiene sus campos en la columna de Impresora y, con el simulador interno y el relleno 4, «1» identifica a la persona de 0001")]
    public async Task Pasar_un_codigo_por_el_lector_simulado()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await Assertions.Expect(pagina.Locator("[role=\"tab\"]")).ToHaveCountAsync(11);

        var pestanas = await MedirAsync(pagina, "[role=\"tab\"]");
        var fila = (await MedirAsync(pagina, "[role=\"tablist\"]"))[0];
        _escenario.Registrar("1024 × 768, las pestañas: " + string.Join("; ", pestanas.Select(c => $"{c.Id} desde x {c.Izquierda:F1} con {c.Ancho:F1} px"))
            + $"; la fila de x {fila.Izquierda:F1} a {fila.Izquierda + fila.Ancho:F1}");
        Assert.All(pestanas, c => Assert.True(Math.Abs(c.Arriba - pestanas[0].Arriba) <= Tolerancia && c.Alto >= 56 - Tolerancia, $"{c.Id} desde {c.Arriba} con {c.Alto} px"));
        Assert.True(pestanas[^1].Izquierda + pestanas[^1].Ancho <= fila.Izquierda + fila.Ancho + Tolerancia,
            $"La última pestaña termina en {pestanas[^1].Izquierda + pestanas[^1].Ancho} px y la fila en {fila.Izquierda + fila.Ancho}");
        Assert.Equal("pestana-lector", pestanas[8].Id);

        await pagina.Locator("#pestana-impresora").ClickAsync();
        // Medir recién con Impresora a la vista: en un equipo lento, la sección oculta mide 0.
        await Assertions.Expect(pagina.Locator("#numero-puesto")).ToBeVisibleAsync();
        var impresora = (await MedirAsync(pagina, "#numero-puesto"))[0];
        await pagina.Locator("#pestana-lector").ClickAsync();
        await Assertions.Expect(pagina.Locator("#velocidad-lector")).ToBeVisibleAsync();

        await pagina.Locator("#medio-simulador").ClickAsync();
        await pagina.Locator("#relleno-lector").FillAsync("4");
        var campos = await MedirAsync(pagina, "#velocidad-lector, #relleno-lector");
        var puerto = await MedirAsync(pagina, "#puerto-lector, #actualizar-puertos");
        var botones = await MedirAsync(pagina, "#medio-teclado, #medio-simulador, #medio-puerto, #bits-7, #paridad-ninguna, #parada-1, #terminador-crlf, #guardar-lector, #cancelar-configuracion");
        _escenario.Registrar("1024 × 768, Lector: " + string.Join("; ", campos.Concat(puerto).Concat(botones).Append(impresora)
            .Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1} px desde x {c.Izquierda:F1}, letra {c.Letra}")));
        await pagina.Locator("#titulo-lector").ScrollIntoViewIfNeededAsync();
        await _escenario.CapturarAsync(pagina, "lector-pestana-1024x768");
        Assert.All(campos, c => Assert.True(Math.Abs(c.Izquierda - impresora.Izquierda) <= Tolerancia && Math.Abs(c.Ancho - impresora.Ancho) <= Tolerancia
            && Math.Abs(c.Alto - impresora.Alto) <= Tolerancia && c.Letra == impresora.Letra, $"{c.Id}: desde x {c.Izquierda} con {c.Ancho} × {c.Alto}, letra {c.Letra}"));
        Assert.True(Math.Abs(puerto[0].Izquierda - impresora.Izquierda) <= Tolerancia
            && Math.Abs(puerto[1].Izquierda + puerto[1].Ancho - (impresora.Izquierda + impresora.Ancho)) <= Tolerancia,
            $"El puerto desde x {puerto[0].Izquierda} y «Actualizar» hasta x {puerto[1].Izquierda + puerto[1].Ancho}");
        var cerrar = botones[^1];
        Assert.All(botones, c => Assert.True(Math.Abs(c.Alto - cerrar.Alto) <= Tolerancia && c.Letra == cerrar.Letra, $"{c.Id}: {c.Alto} px, letra {c.Letra}"));
        Assert.All(botones.Where(c => c.Id is "medio-teclado" or "bits-7" or "paridad-ninguna" or "parada-1" or "terminador-crlf"),
            c => Assert.True(Math.Abs(c.Izquierda - impresora.Izquierda) <= Tolerancia, $"{c.Id} desde x {c.Izquierda}"));

        await pagina.Locator("#guardar-lector").ClickAsync();
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToContainTextAsync("Se guardó el medio de identificación");
        await Assertions.Expect(pagina.Locator("#estado-lector")).ToHaveTextAsync("Lector: escuchando el simulador interno");
        await pagina.Locator("#cancelar-configuracion").ClickAsync();
        // Con el diálogo abierto la atención es inerte: la lectura se pasa recién cuando el diálogo se fue.
        await Assertions.Expect(pagina.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);

        var reposo = await MedirAsync(pagina, "#codigo, #ingresar, #lector-simulado, #pasar-por-lector");
        _escenario.Registrar("1024 × 768, reposo: " + string.Join("; ", reposo.Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1} px desde x {c.Izquierda:F1}")));
        await _escenario.CapturarAsync(pagina, "lector-simulado-reposo-1024x768");
        // Los dos campos y los dos botones, iguales: la fila del lector no es asimétrica con la del código.
        Assert.True(Math.Abs(reposo[2].Izquierda - reposo[0].Izquierda) <= Tolerancia && Math.Abs(reposo[2].Ancho - reposo[0].Ancho) <= Tolerancia
            && Math.Abs(reposo[3].Izquierda - reposo[1].Izquierda) <= Tolerancia && Math.Abs(reposo[3].Ancho - reposo[1].Ancho) <= Tolerancia
            && Math.Abs(reposo[2].Alto - reposo[0].Alto) <= Tolerancia,
            $"El lector simulado de x {reposo[2].Izquierda} a {reposo[3].Izquierda + reposo[3].Ancho}; el código de x {reposo[0].Izquierda} a {reposo[1].Izquierda + reposo[1].Ancho}");

        await pagina.Locator("#lector-simulado").FillAsync("1");
        await pagina.Locator("#pasar-por-lector").ClickAsync();
        await Assertions.Expect(pagina.Locator(".opcion").First).ToBeVisibleAsync();
        await _escenario.CapturarAsync(pagina, "lector-simulado-identifica-1024x768");
        await Assertions.Expect(pagina.Locator("body")).ToContainTextAsync("Persona sintética 01");
    }

    private static async Task<Caja[]> MedirAsync(IPage pagina, string selector) =>
        JsonSerializer.Deserialize<Caja[]>(await pagina.EvaluateAsync<string>(
            """
            s => JSON.stringify([...document.querySelectorAll(s)].map(e => {
              const b = e.getBoundingClientRect();
              return { Id: e.id, Arriba: b.top, Izquierda: b.left, Ancho: b.width, Alto: b.height, Letra: getComputedStyle(e).fontSize };
            }))
            """, selector))!;

    private sealed record Caja(string Id, double Arriba, double Izquierda, double Ancho, double Alto, string Letra);
}
