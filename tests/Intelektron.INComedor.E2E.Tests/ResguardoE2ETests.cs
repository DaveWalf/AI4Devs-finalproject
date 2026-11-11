using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Guardar el resguardo, cambiar el estilo y recuperarlo con el diálogo de archivos, con el puesto compilado
/// (resguardar-configuracion, D4 y D6): por CDP y a 1024 × 768, con la tolerancia de 1 px de la regla de prolijidad. La
/// carpeta de exportación y un estilo propio se dejan escritos antes de abrir el puesto, y el diálogo de archivos se contesta
/// con <see cref="DialogoDeArchivosE2E"/>.
/// </summary>
public class ResguardoE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private const string TituloDialogo = "Resguardo de la configuración para recuperar";

    /// <summary>El estilo del puesto al guardar el resguardo, distinto del de fábrica, y el que se elige después.</summary>
    private const string EstiloResguardado = "--opacidad-superficie: 0.84; --desenfoque-fondo: 10px";

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("resguardo");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "configuracion-puesto · Guardar y recuperar el resguardo (puesto compilado): a 1024 × 768, los dos botones de General miden lo mismo con la letra y el alto de «Cerrar»; guardado el resguardo y cambiado el estilo, recuperarlo con el diálogo de archivos muestra el resumen con sus dos botones parejos y deja la versión 2 con el estilo de antes")]
    public async Task Guardar_cambiar_y_recuperar()
    {
        var destino = Directory.CreateDirectory(Path.Combine(_escenario.Carpeta, "exportaciones")).FullName;
        File.WriteAllText(Path.Combine(_escenario.Carpeta, "exportacion.json"), JsonSerializer.Serialize(new { carpeta = destino }));
        File.WriteAllText(Path.Combine(_escenario.Carpeta, "estilo-visual.json"), JsonSerializer.Serialize(new { opacidad = 84, desenfoque = 10 }));
        using var playwright = await Playwright.CreateAsync();
        var (puesto, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await Assertions.Expect(pagina.Locator(".puesto")).ToHaveAttributeAsync("style", EstiloResguardado);
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-general").ClickAsync();
        await Assertions.Expect(pagina.Locator("#recuperar-resguardo")).ToBeVisibleAsync();

        var general = await MedirAsync(pagina, ["#guardar-resguardo", "#recuperar-resguardo", "#cancelar-configuracion"]);
        _escenario.Registrar("1024 × 768, el resguardo en General: " + string.Join("; ", general.Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1} px desde {c.Arriba:F1}, letra {c.Letra}")));
        await _escenario.CapturarAsync(pagina, "general-resguardo-1024x768");
        var cerrar = general[^1];
        Assert.All(general, c => Assert.True(Math.Abs(c.Alto - cerrar.Alto) <= Tolerancia && c.Letra == cerrar.Letra,
            $"{c.Id}: {c.Alto} px, letra {c.Letra}, contra «Cerrar»"));
        Assert.True(Math.Abs(general[0].Ancho - general[1].Ancho) <= Tolerancia && Math.Abs(general[0].Arriba - general[1].Arriba) <= Tolerancia,
            $"«Guardar resguardo» mide {general[0].Ancho} px desde {general[0].Arriba} y «Recuperar resguardo» {general[1].Ancho} px desde {general[1].Arriba}");
        Assert.True(general[1].Arriba + general[1].Alto <= 768, "«Recuperar resguardo» queda fuera de la ventana");

        await pagina.Locator("#guardar-resguardo").ClickAsync();
        await Assertions.Expect(pagina.Locator("#mensaje-resguardo")).ToContainTextAsync("Guardado");
        var resguardo = Assert.Single(Directory.GetFiles(destino, "INComedor-resguardo-*.zip"));
        _escenario.Registrar($"resguardo guardado: {Path.GetFileName(resguardo)}, {new FileInfo(resguardo).Length} bytes");

        // El cambio: la opacidad y el desenfoque al mínimo, guardados desde Identidad visual.
        await pagina.Locator("#pestana-identidad").ClickAsync();
        await pagina.Locator("#opacidad").FillAsync("72");
        await pagina.Locator("#desenfoque").FillAsync("0");
        await pagina.Locator("#guardar-configuracion").ClickAsync();
        await Assertions.Expect(pagina.Locator(".puesto")).ToHaveAttributeAsync("style", "--opacidad-superficie: 0.72; --desenfoque-fondo: 0px");

        await pagina.Locator("#pestana-general").ClickAsync();
        var toque = pagina.Locator("#recuperar-resguardo").ClickAsync();
        await DialogoDeArchivosE2E.ElegirAsync(puesto, TituloDialogo, resguardo, _escenario.Registrar);
        await toque;
        var resumen = pagina.Locator("#resumen-resguardo");
        await Assertions.Expect(resumen).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(pagina.Locator("#resguardo-origen")).ToContainTextAsync("versión 1");
        await Assertions.Expect(pagina.Locator("#resguardo-ajustes")).ToContainTextAsync("el estilo visual");
        var cartel = await MedirAsync(pagina, ["#resguardo-recuperar", "#resguardo-cancelar"]);
        var abajo = await pagina.EvaluateAsync<double>("() => document.querySelector('#resumen-resguardo').getBoundingClientRect().bottom");
        _escenario.Registrar("1024 × 768, el resumen del resguardo: " + string.Join("; ", cartel.Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1} px desde {c.Arriba:F1}, letra {c.Letra}"))
            + $"; el cartel termina en {abajo:F1} px");
        await _escenario.CapturarAsync(pagina, "general-resumen-resguardo-1024x768");
        Assert.All(cartel, c => Assert.True(Math.Abs(c.Alto - cartel[0].Alto) <= Tolerancia && Math.Abs(c.Arriba - cartel[0].Arriba) <= Tolerancia
            && c.Letra == cartel[0].Letra && c.Alto >= 56 - Tolerancia, $"{c.Id}: {c.Alto} px desde {c.Arriba}, letra {c.Letra}"));
        Assert.True(abajo <= 768, $"El cartel termina en {abajo} px, fuera de la ventana");
        Assert.Equal("resguardo-cancelar", await pagina.EvaluateAsync<string>("() => document.activeElement?.id ?? ''"));

        await pagina.Locator("#resguardo-recuperar").ClickAsync();

        // Recuperar escribe una versión y los ajustes, y la pantalla los relee: 15 s, como «Reemplazar» en un runner lento.
        var despues = new LocatorAssertionsToHaveCountOptions { Timeout = 15000 };
        await Assertions.Expect(resumen).ToHaveCountAsync(0, despues);
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToContainTextAsync("versión 2", new() { Timeout = 15000 });
        await Assertions.Expect(pagina.Locator(".puesto")).ToHaveAttributeAsync("style", EstiloResguardado, new() { Timeout = 15000 });
        await _escenario.CapturarAsync(pagina, "general-resguardo-recuperado-1024x768");
    }

    private static async Task<Caja[]> MedirAsync(IPage pagina, string[] ids) =>
        JsonSerializer.Deserialize<Caja[]>(await pagina.EvaluateAsync<string>(
            """
            ids => JSON.stringify(ids.map(i => {
              const e = document.querySelector(i), b = e.getBoundingClientRect();
              return { Id: i, Arriba: b.top, Alto: b.height, Ancho: b.width, Letra: getComputedStyle(e).fontSize };
            }))
            """, ids))!;

    private sealed record Caja(string Id, double Arriba, double Alto, double Ancho, string Letra);
}
