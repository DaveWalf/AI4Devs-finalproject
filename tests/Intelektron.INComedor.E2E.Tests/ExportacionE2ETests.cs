using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// La exportación automática de consumos, con el puesto compilado (exportar-consumos-automaticamente, D6 y D7): por CDP y a
/// 1024 × 768, con la tolerancia de 1 px de la regla de prolijidad. Las nueve pestañas en una fila; la pestaña
/// Exportación medida; con el SFTP simulado caído, «Exportar ahora» llega a la carpeta y no al SFTP, y al levantarlo los dos
/// quedan al día con el mismo archivo.
/// </summary>
public class ExportacionE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("exportacion");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "exportacion-consumos · El servidor cae y vuelve (puesto compilado): a 1024 × 768 las nueve pestañas entran en una fila y la pestaña Exportación tiene sus campos y botones parejos, en la columna de los datos de Impresora y con «Elegir» al borde de los campos; con el SFTP simulado caído la carpeta recibe el consumo y el SFTP no, y al levantarlo los dos quedan al día con el mismo archivo")]
    public async Task Exportar_con_el_sftp_caido_y_levantado()
    {
        var destino = Directory.CreateDirectory(Path.Combine(_escenario.Carpeta, "exportaciones-automaticas")).FullName;
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await Assertions.Expect(pagina.Locator("[role=\"tab\"]")).ToHaveCountAsync(9);

        var pestanas = JsonSerializer.Deserialize<Caja[]>(await pagina.EvaluateAsync<string>(
            """
            () => JSON.stringify([...document.querySelectorAll('[role="tab"]')].map(e => {
              const b = e.getBoundingClientRect();
              return { Id: e.id, Arriba: b.top, Izquierda: b.left, Ancho: b.width, Alto: b.height, Letra: getComputedStyle(e).fontSize };
            }))
            """))!;
        _escenario.Registrar("1024 × 768, las pestañas: " + string.Join("; ", pestanas.Select(c => $"{c.Id} desde x {c.Izquierda:F1} con {c.Ancho:F1} px")));
        Assert.Equal(9, pestanas.Length);
        Assert.All(pestanas, c => Assert.True(Math.Abs(c.Arriba - pestanas[0].Arriba) <= Tolerancia && c.Alto >= 56 - Tolerancia, $"{c.Id} desde {c.Arriba} con {c.Alto} px"));
        Assert.True(pestanas[^1].Izquierda + pestanas[^1].Ancho <= 1024, $"La última pestaña termina en {pestanas[^1].Izquierda + pestanas[^1].Ancho} px");
        Assert.Equal("pestana-exportacion", pestanas[^2].Id);

        await pagina.Locator("#pestana-impresora").ClickAsync();
        // Medir recién con Impresora a la vista: en un equipo lento, la sección oculta mide 0.
        await Assertions.Expect(pagina.Locator("#numero-puesto")).ToBeVisibleAsync();
        var impresora = (await MedirAsync(pagina, ["#numero-puesto"]))[0];
        await pagina.Locator("#pestana-exportacion").ClickAsync();
        await Assertions.Expect(pagina.Locator("#minutos-exportacion")).ToBeVisibleAsync();
        await pagina.Locator("#exportacion-activa").ClickAsync();
        await pagina.Locator("#carpeta-exportacion").FillAsync(destino);
        await pagina.Locator("#sftp-activo").ClickAsync();
        await pagina.Locator("#servidor-sftp").FillAsync("sftp.ejemplo.local");
        await pagina.Locator("#usuario-sftp").FillAsync("comedor");
        await pagina.Locator("#carpeta-remota-sftp").FillAsync("entrada");
        var campos = await MedirAsync(pagina, ["#minutos-exportacion", "#servidor-sftp", "#puerto-sftp", "#usuario-sftp", "#carpeta-remota-sftp"]);
        var botones = await MedirAsync(pagina, ["#exportacion-activa", "#exportacion-apagada", "#archivo-que-crece", "#archivo-por-dia", "#sftp-activo", "#sftp-apagado",
            "#sftp-disponible", "#sftp-caido", "#exportar-ahora", "#guardar-exportacion", "#cancelar-configuracion"]);
        var carpeta = await MedirAsync(pagina, ["#carpeta-exportacion", "#elegir-carpeta-exportacion"]);
        _escenario.Registrar("1024 × 768, Exportación: " + string.Join("; ", campos.Concat(carpeta).Concat(botones).Append(impresora)
            .Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1} px desde x {c.Izquierda:F1}, letra {c.Letra}")));
        await pagina.Locator("#titulo-exportacion").ScrollIntoViewIfNeededAsync();
        await _escenario.CapturarAsync(pagina, "exportacion-pestana-1024x768");
        Assert.True(Math.Abs(campos[0].Izquierda - impresora.Izquierda) <= Tolerancia, $"Los campos desde x {campos[0].Izquierda}, los de Impresora desde x {impresora.Izquierda}");
        var (enCarpeta, elegir) = (carpeta[0], carpeta[1]);
        Assert.True(Math.Abs(enCarpeta.Izquierda - campos[0].Izquierda) <= Tolerancia
            && Math.Abs(elegir.Izquierda + elegir.Ancho - (campos[0].Izquierda + campos[0].Ancho)) <= Tolerancia
            && Math.Abs(elegir.Arriba - enCarpeta.Arriba) <= Tolerancia && Math.Abs(elegir.Alto - enCarpeta.Alto) <= Tolerancia,
            $"La carpeta desde x {enCarpeta.Izquierda} y «Elegir» hasta x {elegir.Izquierda + elegir.Ancho}; los campos de {campos[0].Izquierda} a {campos[0].Izquierda + campos[0].Ancho}");
        Assert.All(campos, c => Assert.True(Math.Abs(c.Izquierda - campos[0].Izquierda) <= Tolerancia && Math.Abs(c.Ancho - campos[0].Ancho) <= Tolerancia
            && Math.Abs(c.Alto - campos[0].Alto) <= Tolerancia && c.Letra == campos[0].Letra, $"{c.Id}: desde x {c.Izquierda} con {c.Ancho} × {c.Alto}, letra {c.Letra}"));
        var cerrar = botones[^1];
        Assert.All(botones, c => Assert.True(Math.Abs(c.Alto - cerrar.Alto) <= Tolerancia && c.Letra == cerrar.Letra, $"{c.Id}: {c.Alto} px, letra {c.Letra}"));

        await pagina.Locator("#guardar-exportacion").ClickAsync();
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToContainTextAsync("Se guardó la exportación automática");
        await pagina.Locator("#sftp-caido").ClickAsync();
        await pagina.Locator("#cancelar-configuracion").ClickAsync();
        // Con el diálogo abierto la atención es inerte: el código se tipea recién cuando el diálogo se fue.
        await Assertions.Expect(pagina.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);

        await pagina.Locator("#codigo").FillAsync("0001");
        await pagina.Locator("#ingresar").ClickAsync();
        await pagina.Locator(".opcion").First.ClickAsync();
        await pagina.Locator("#confirmar").ClickAsync();
        await pagina.Locator("#listo").ClickAsync(new() { Timeout = 15000 });

        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-exportacion").ClickAsync();
        await pagina.Locator("#exportar-ahora").ClickAsync();
        await Assertions.Expect(pagina.Locator("#mensaje-exportacion-automatica")).ToContainTextAsync("No se pudo exportar al SFTP simulado");
        await Assertions.Expect(pagina.Locator("#estado-exportacion-sftp")).ToContainTextAsync("falló");
        await pagina.Locator("#mensaje-exportacion-automatica").ScrollIntoViewIfNeededAsync();
        await _escenario.CapturarAsync(pagina, "exportacion-sftp-caido-1024x768");
        var remoto = Path.Combine(_escenario.Carpeta, "sftp-simulado", "sftp.ejemplo.local", "entrada", "INComedor-consumos.csv");
        Assert.True(File.Exists(Path.Combine(destino, "INComedor-consumos.csv")));
        Assert.False(File.Exists(remoto));

        await pagina.Locator("#sftp-disponible").ClickAsync();
        await pagina.Locator("#exportar-ahora").ClickAsync();
        await Assertions.Expect(pagina.Locator("#mensaje-exportacion-automatica")).ToHaveTextAsync("Se exportó 1 consumo.");
        await Assertions.Expect(pagina.Locator("#estado-exportacion-sftp")).ToContainTextAsync("último envío");
        await _escenario.CapturarAsync(pagina, "exportacion-al-dia-1024x768");
        var lineas = File.ReadAllLines(Path.Combine(destino, "INComedor-consumos.csv"));
        _escenario.Registrar("archivo exportado: " + string.Join("|", lineas));
        Assert.Equal(lineas, File.ReadAllLines(remoto));
        Assert.Equal(2, lineas.Length);
        Assert.DoesNotContain(lineas, l => l.Contains("\"0001\"", StringComparison.Ordinal));
    }

    private static async Task<Caja[]> MedirAsync(IPage pagina, string[] ids) =>
        JsonSerializer.Deserialize<Caja[]>(await pagina.EvaluateAsync<string>(
            """
            ids => JSON.stringify(ids.map(i => {
              const e = document.querySelector(i), b = e.getBoundingClientRect();
              return { Id: i, Arriba: b.top, Izquierda: b.left, Ancho: b.width, Alto: b.height, Letra: getComputedStyle(e).fontSize };
            }))
            """, ids))!;

    private sealed record Caja(string Id, double Arriba, double Izquierda, double Ancho, double Alto, string Letra);
}
