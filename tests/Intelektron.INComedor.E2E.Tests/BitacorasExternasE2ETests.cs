using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Las bitácoras mensuales con el puesto compilado (escribir-bitacoras-externas): una atención autorizada por CDP, el cierre
/// ordenado de la ventana y la lectura de los dos archivos de su carpeta. Usa los ayudantes del E2E sin cambiarlos.
/// </summary>
public class BitacorasExternasE2ETests : IAsyncLifetime
{
    private const string Codigo = "0001";
    private const string Exito = "Consumo registrado. Retirá tu comprobante.";

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("bitacoras-externas");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "bitacoras-externas · De punta a punta: una atención autorizada y el cierre ordenado del puesto dejan en interacciones de SESSION_OPENED a SESSION_CLOSED con la misma sesión, en la general KIOSK_STARTED y KIOSK_STOPPED, y en ninguno el código")]
    public async Task Atencion_y_cierre_quedan_en_las_bitacoras()
    {
        using var playwright = await Playwright.CreateAsync();
        var (puesto, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        await pagina.Locator("#codigo").FillAsync(Codigo);
        await pagina.Locator("#ingresar").ClickAsync();
        var opcion = pagina.Locator(".opcion", new() { HasText = "Almuerzo común" });
        await opcion.ClickAsync();
        await Assertions.Expect(opcion).ToHaveAttributeAsync("aria-pressed", "true");
        await pagina.Locator("#confirmar").ClickAsync();
        await Assertions.Expect(pagina.Locator("#mensaje")).ToHaveTextAsync(Exito);
        await pagina.Locator("#listo").ClickAsync();
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        await _escenario.CapturarAsync(pagina, "01-atencion-terminada");

        // El cierre ordenado: la ventana se cierra como con la X, y el puesto registra KIOSK_STOPPED y hace la última pasada.
        Assert.True(puesto.CloseMainWindow(), "La ventana del puesto no recibió el cierre.");
        using (var plazo = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
        {
            await puesto.WaitForExitAsync(plazo.Token);
        }

        _escenario.Registrar($"puesto cerrado en orden, código de salida {puesto.ExitCode}");

        var carpeta = Assert.Single(Directory.GetDirectories(Path.Combine(_escenario.Carpeta, "bitacoras")));
        var interacciones = Lineas(Assert.Single(Directory.GetFiles(carpeta, "INComedor-interacciones-*.log")));
        var general = Lineas(Assert.Single(Directory.GetFiles(carpeta, "INComedor-general-*.log")));
        _escenario.Registrar($"bitácoras: {interacciones.Count} líneas de interacciones y {general.Count} de la general en {carpeta}");

        var sesion = Assert.Single(interacciones, l => Tipo(l) == "SESSION_OPENED").GetProperty("Context").GetProperty("IdentitySessionId").GetGuid();
        var deLaSesion = interacciones
            .Where(l => l.GetProperty("Context").TryGetProperty("IdentitySessionId", out var s) && s.ValueKind == JsonValueKind.String && s.GetGuid() == sesion)
            .Select(Tipo)
            .ToList();
        Assert.Equal(["SESSION_OPENED", "OPTIONS_OFFERED", "OPTION_SELECTED", "CONFIRMATION_REQUESTED", "CONSUMPTION_AUTHORIZED", "SESSION_CLOSED"], deLaSesion);
        Assert.Equal("KIOSK_STARTED", Tipo(general.First(l => Tipo(l)!.StartsWith("KIOSK_", StringComparison.Ordinal))));
        Assert.Equal("KIOSK_STOPPED", Tipo(general[^1]));
        Assert.All(interacciones.Concat(general), l => Assert.DoesNotContain(Codigo, Textos(l)));
    }

    private static List<JsonElement> Lineas(string ruta) =>
        File.ReadAllText(ruta).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

    private static string? Tipo(JsonElement linea) => linea.GetProperty("EventType").GetString();

    /// <summary>
    /// Todos los valores de texto de una línea. El código se busca como valor y no como subcadena del archivo: «0001» aparece
    /// dentro de los identificadores sintéticos de la semilla.
    /// </summary>
    private static IEnumerable<string> Textos(JsonElement elemento) => elemento.ValueKind switch
    {
        JsonValueKind.Object => elemento.EnumerateObject().SelectMany(p => Textos(p.Value)),
        JsonValueKind.Array => elemento.EnumerateArray().SelectMany(Textos),
        JsonValueKind.String => [elemento.GetString()!],
        _ => [],
    };
}
