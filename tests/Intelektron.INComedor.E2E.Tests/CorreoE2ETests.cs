using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Los avisos por correo, con el puesto compilado (avisar-por-correo, D3 y D7): por CDP y a 1024 × 768, con la tolerancia
/// de 1 px de la regla de prolijidad. Las diez pestañas en una fila; la pestaña Mails medida contra Impresora; con el
/// servidor simulado caído, «Enviar una prueba» queda en la cola, y al levantarlo sale con la siguiente como dos `.eml`.
/// </summary>
public partial class CorreoE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("correo");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "avisos-correo · El servidor de correo cae y vuelve (puesto compilado): a 1024 × 768 las diez pestañas entran en una fila y la pestaña Mails tiene sus campos en la columna de Impresora y sus botones parejos; con el servidor caído la prueba queda en la cola y, levantado, salen las dos")]
    public async Task Enviar_una_prueba_con_el_servidor_caido_y_levantado()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await Assertions.Expect(pagina.Locator("[role=\"tab\"]")).ToHaveCountAsync(10);

        var pestanas = await MedirAsync(pagina, "[role=\"tab\"]");
        _escenario.Registrar("1024 × 768, las pestañas: " + string.Join("; ", pestanas.Select(c => $"{c.Id} desde x {c.Izquierda:F1} con {c.Ancho:F1} px")));
        Assert.All(pestanas, c => Assert.True(Math.Abs(c.Arriba - pestanas[0].Arriba) <= Tolerancia && c.Alto >= 56 - Tolerancia, $"{c.Id} desde {c.Arriba} con {c.Alto} px"));
        Assert.True(pestanas[^1].Izquierda + pestanas[^1].Ancho <= 1024, $"La última pestaña termina en {pestanas[^1].Izquierda + pestanas[^1].Ancho} px");
        Assert.Equal("pestana-mails", pestanas[5].Id);

        await pagina.Locator("#pestana-impresora").ClickAsync();
        // Medir recién con Impresora a la vista: en un equipo lento, la sección oculta mide 0.
        await Assertions.Expect(pagina.Locator("#numero-puesto")).ToBeVisibleAsync();
        var impresora = (await MedirAsync(pagina, "#numero-puesto"))[0];
        await pagina.Locator("#pestana-mails").ClickAsync();
        await Assertions.Expect(pagina.Locator("#cliente-correo")).ToBeVisibleAsync();

        await pagina.Locator("#correos-activos").ClickAsync();
        await pagina.Locator("#cliente-correo").FillAsync("Cliente Demo");
        await pagina.Locator("#remitente-correo").FillAsync("puesto@ejemplo.com");
        await pagina.Locator("#servidor-correo").FillAsync("smtp.ejemplo.local");
        await pagina.Locator("#operador-correo-1").FillAsync("operador1@ejemplo.com");
        await pagina.Locator("#administrador-correo").FillAsync("admin@ejemplo.com");
        await pagina.Locator("#hora-senal").FillAsync("23:59");
        var campos = await MedirAsync(pagina, "#cliente-correo, #remitente-correo, #servidor-correo, #puerto-correo, [id^=\"operador-correo-\"], #administrador-correo, #hora-senal");
        var botones = await MedirAsync(pagina, "#correos-activos, #correos-apagados, #cifrado-starttls, #cifrado-tls, #factor-aplicacion, #factor-oauth, #servidor-correo-disponible, #servidor-correo-caido, #enviar-prueba-correo, #guardar-correo, #cancelar-configuracion");
        _escenario.Registrar("1024 × 768, Mails: " + string.Join("; ", campos.Concat(botones).Append(impresora)
            .Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1} px desde x {c.Izquierda:F1}, letra {c.Letra}")));
        await pagina.Locator("#titulo-correo").ScrollIntoViewIfNeededAsync();
        await _escenario.CapturarAsync(pagina, "correo-pestana-1024x768");
        Assert.Equal(11, campos.Length);
        Assert.All(campos, c => Assert.True(Math.Abs(c.Izquierda - impresora.Izquierda) <= Tolerancia && Math.Abs(c.Ancho - impresora.Ancho) <= Tolerancia
            && Math.Abs(c.Alto - impresora.Alto) <= Tolerancia && c.Letra == impresora.Letra, $"{c.Id}: desde x {c.Izquierda} con {c.Ancho} × {c.Alto}, letra {c.Letra}"));
        var cerrar = botones[^1];
        Assert.All(botones, c => Assert.True(Math.Abs(c.Alto - cerrar.Alto) <= Tolerancia && c.Letra == cerrar.Letra, $"{c.Id}: {c.Alto} px, letra {c.Letra}"));

        await pagina.Locator("#guardar-correo").ClickAsync();
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToContainTextAsync("Se guardaron los correos");
        await pagina.Locator("#servidor-correo-caido").ClickAsync();
        await pagina.Locator("#enviar-prueba-correo").ClickAsync();
        await Assertions.Expect(pagina.Locator("#mensaje-correo")).ToHaveTextAsync("La prueba quedó en la cola: el servidor no responde y se reintenta cada minuto.");
        await Assertions.Expect(pagina.Locator("#estado-correo")).ToHaveTextAsync("Cola: 1 pendiente");
        await pagina.Locator("#mensaje-correo").ScrollIntoViewIfNeededAsync();
        await _escenario.CapturarAsync(pagina, "correo-en-cola-1024x768");
        Assert.Empty(Pruebas());

        await pagina.Locator("#servidor-correo-disponible").ClickAsync();
        await pagina.Locator("#enviar-prueba-correo").ClickAsync();
        await Assertions.Expect(pagina.Locator("#mensaje-correo")).ToHaveTextAsync("Se envió la prueba a 2 casillas.");
        await Assertions.Expect(pagina.Locator("#estado-correo")).ToHaveTextAsync("Cola: sin pendientes");
        await _escenario.CapturarAsync(pagina, "correo-enviado-1024x768");
        var pruebas = Pruebas();
        _escenario.Registrar("correos de prueba: " + string.Join(" | ", pruebas));
        Assert.Equal(2, pruebas.Count);
        Assert.All(pruebas, p => Assert.Equal("Cliente Demo · Puesto · Prueba", p));
    }

    /// <summary>Los asuntos de los `.eml` de prueba de `correo-simulado\`, decodificados.</summary>
    private List<string> Pruebas()
    {
        var carpeta = Path.Combine(_escenario.Carpeta, "correo-simulado");
        return !Directory.Exists(carpeta)
            ? []
            : Directory.GetFiles(carpeta, "*.eml")
                .Select(a => Encoding.UTF8.GetString(Convert.FromBase64String(Asunto().Match(File.ReadAllText(a, Encoding.UTF8)).Groups[1].Value)))
                .Where(a => a.EndsWith("· Prueba", StringComparison.Ordinal))
                .ToList();
    }

    [GeneratedRegex(@"^Subject: =\?utf-8\?B\?([A-Za-z0-9+/=]+)\?=\r$", RegexOptions.Multiline)]
    private static partial Regex Asunto();

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
