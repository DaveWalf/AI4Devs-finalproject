using System.Diagnostics;
using Microsoft.Playwright;

namespace Spike.E2E.Cdp;

/// <summary>
/// Spike del 29/10/2026: ¿alcanza Playwright, por CDP, al WebView2 del Kiosk real? Lanza el Kiosk con
/// WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port, se conecta con ConnectOverCDPAsync, toca
/// «Confirmar» dos veces y saca una captura. El proceso se cierra por su PID, nunca por un filtro de nombre.
/// </summary>
public sealed class DemostracionPorCdpTests
{
    [Fact(DisplayName = "Spike CDP · primer toque AUTHORIZED, segundo LOCAL_LIMIT_REACHED, sobre el Kiosk real")]
    public async Task RecorreLaPantallaDeDemostracion()
    {
        var exe = Requerida("SPIKE_KIOSK_EXE");
        var puerto = int.Parse(Environment.GetEnvironmentVariable("SPIKE_CDP_PORT") ?? "9333");
        var salida = Requerida("SPIKE_SALIDA");
        Directory.CreateDirectory(salida);

        var inicio = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
        // La variable documentada de WebView2 sirve en una sesión sin elevar; elevada (como esta y como los runners
        // Windows de GitHub) se ignora, y el puerto entra por la costura del Kiosk.
        inicio.Environment["WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS"] = $"--remote-debugging-port={puerto}";
        inicio.Environment["INCOMEDOR_CDP_PORT"] = puerto.ToString();
        using var kiosk = Process.Start(inicio) ?? throw new InvalidOperationException("No arrancó el Kiosk.");
        File.WriteAllText(Path.Combine(salida, "kiosk.pid"), kiosk.Id.ToString());
        var reloj = Stopwatch.StartNew();

        try
        {
            using var playwright = await Playwright.CreateAsync();
            var navegador = await Conectar(playwright, puerto);
            Registrar(salida, $"conectado por CDP a los {reloj.ElapsedMilliseconds} ms · version {navegador.Version}");

            var pagina = await PaginaConConfirmar(navegador);
            Registrar(salida, $"pagina {pagina.Url} a los {reloj.ElapsedMilliseconds} ms");

            await pagina.Locator("#confirmar").ClickAsync();
            await Assertions.Expect(pagina.Locator("#decision")).ToHaveTextAsync("AUTHORIZED");
            Registrar(salida, $"primer toque: {await pagina.Locator("#decision").InnerTextAsync()}");

            await pagina.Locator("#confirmar").ClickAsync();
            await Assertions.Expect(pagina.Locator("#motivo")).ToHaveTextAsync("LOCAL_LIMIT_REACHED");
            Registrar(salida, $"segundo toque: {await pagina.Locator("#decision").InnerTextAsync()} · {await pagina.Locator("#motivo").InnerTextAsync()}");

            await pagina.ScreenshotAsync(new() { Path = Path.Combine(salida, "captura-cdp.png") });
            Registrar(salida, $"captura a los {reloj.ElapsedMilliseconds} ms");
        }
        finally
        {
            kiosk.Kill(entireProcessTree: true);
            kiosk.WaitForExit(10_000);
            Registrar(salida, $"Kiosk PID {kiosk.Id} cerrado: {kiosk.HasExited}");
        }
    }

    private static async Task<IBrowser> Conectar(IPlaywright playwright, int puerto)
    {
        Exception? ultimo = null;
        for (var intento = 0; intento < 60; intento++)
        {
            try
            {
                return await playwright.Chromium.ConnectOverCDPAsync($"http://127.0.0.1:{puerto}");
            }
            catch (PlaywrightException e)
            {
                ultimo = e;
                await Task.Delay(500);
            }
        }

        throw new InvalidOperationException($"CDP no respondió en el puerto {puerto} en 30 s.", ultimo);
    }

    private static async Task<IPage> PaginaConConfirmar(IBrowser navegador)
    {
        for (var intento = 0; intento < 60; intento++)
        {
            foreach (var pagina in navegador.Contexts.SelectMany(c => c.Pages))
            {
                if (await pagina.Locator("#confirmar").CountAsync() == 1)
                {
                    return pagina;
                }
            }

            await Task.Delay(500);
        }

        throw new InvalidOperationException("Ninguna página del WebView2 mostró #confirmar en 30 s.");
    }

    private static string Requerida(string variable) =>
        Environment.GetEnvironmentVariable(variable) ?? throw new InvalidOperationException($"Falta {variable}.");

    private static void Registrar(string salida, string linea) =>
        File.AppendAllText(Path.Combine(salida, "registro.txt"), $"{DateTimeOffset.Now:HH:mm:ss.fff} {linea}{Environment.NewLine}");
}
