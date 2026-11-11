using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// La administración local con el puesto compilado (administrar-personas-y-opciones): por CDP, Configuración agrega una
/// persona con su código y su cupo, esa persona se identifica y ve sólo lo de ALMUERZO, y una opción recibe su franja. Al
/// cerrar en orden, la general tiene el CONFIGURATION_CHANGED de la persona y ninguna bitácora tiene el código. Usa los
/// ayudantes del E2E sin cambiarlos; las capturas 13 y 14 salen a 1024 × 768 en JPEG, en la carpeta de resultados.
/// </summary>
public class AdministracionE2ETests : IAsyncLifetime
{
    private const string Codigo = "0105";
    private const string Nombre = "Persona sintética 05";

    /// <summary>Las de REFRIGERIO del catálogo de quince: ninguna se le puede ofrecer a quien sólo tiene ALMUERZO.</summary>
    private static readonly string[] Refrigerios =
        ["Café con leche", "Té con galletas", "Yogur con cereales", "Fruta de estación", "Sándwich de queso", "Barra de cereal"];

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("administracion");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "administracion-local · De punta a punta: agregar «Persona sintética 05» con 0105 y ALMUERZO límite 1 desde Configuración, identificarse con 0105 y ver sólo ALMUERZO; la general tiene su CONFIGURATION_CHANGED y ninguna bitácora tiene 0105")]
    public async Task Agregar_una_persona_y_atenderla()
    {
        using var playwright = await Playwright.CreateAsync();
        var (puesto, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        // Personas habilitadas: la persona nueva con su código y ALMUERZO límite 1.
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-personas").ClickAsync();
        await pagina.Locator("#abrir-personas").ClickAsync();
        await pagina.Locator("#agregar-persona").ClickAsync();
        // Blazor dibuja la fila nueva después del clic: «Last» antes de que exista toma a la última persona guardada, que
        // recibía el nombre (en la CI, 3 de 4 corridas). Se escribe cuando ya hay 10 filas, las 9 guardadas y la nueva.
        await Assertions.Expect(pagina.Locator("tr.persona")).ToHaveCountAsync(10);
        var nueva = pagina.Locator("tr.persona").Last;
        await nueva.Locator("input.nombre-persona").FillAsync(Nombre);
        await nueva.Locator("input.codigo-persona").FillAsync(Codigo);
        await nueva.Locator("td.cupo[data-cupo=\"ALMUERZO\"] input.limite").FillAsync("1");
        await pagina.Locator("#guardar-panel").ClickAsync();
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToHaveTextAsync("Se guardó la versión 2 de la configuración.");

        await pagina.Locator("#abrir-personas").ClickAsync();
        // La pestaña ya muestra las 10 personas en la tabla de consulta, sin campos: se espera a las 10 filas del panel, con
        // su nombre editable, porque la búsqueda de abajo mira una sola vez.
        await Assertions.Expect(pagina.Locator("tr.persona input.nombre-persona")).ToHaveCountAsync(10);
        // La captura muestra una persona con cupos: la lista se desplaza hasta «Persona sintética 01». Blazor pone el valor del
        // campo como propiedad, no como atributo: se busca por la propiedad.
        Assert.True(await pagina.EvaluateAsync<bool>(
            """
            () => {
                const fila = [...document.querySelectorAll('tr.persona')]
                    .find(a => a.querySelector('input.nombre-persona')?.value === 'Persona sintética 01');
                fila?.scrollIntoView({ block: 'start' });
                return fila !== undefined;
            }
            """));
        await Capturar(pagina, "13-personas-habilitadas");
        await pagina.Locator("#cancelar-panel").ClickAsync();
        await pagina.Locator("#cancelar-configuracion").ClickAsync();
        // Con el diálogo abierto la atención es inerte: el código se tipea recién cuando el diálogo se fue.
        await Assertions.Expect(pagina.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);

        // La primera atención de la persona nueva: sólo opciones de ALMUERZO.
        await pagina.Locator("#codigo").FillAsync(Codigo);
        await pagina.Locator("#ingresar").ClickAsync();
        await Assertions.Expect(pagina.Locator("#saludo")).ToContainTextAsync(Nombre);
        var ofrecidas = await pagina.Locator(".opcion .nombre").AllTextContentsAsync();
        _escenario.Registrar($"ofrecidas a {Nombre}: {string.Join(" · ", ofrecidas)}");
        Assert.Equal(9, ofrecidas.Count);
        Assert.DoesNotContain(ofrecidas, o => Refrigerios.Contains(o.Trim()));
        await pagina.Locator("#cancelar").ClickAsync();
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        // Opciones del comedor: «Almuerzo común» con la franja de 11:00 a 14:00, para la captura 14.
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-opciones").ClickAsync();
        await pagina.Locator("#abrir-opciones").ClickAsync();
        var almuerzo = pagina.Locator("article.opcion-admin").First;
        await Assertions.Expect(almuerzo.Locator("input.nombre-opcion")).ToHaveValueAsync("Almuerzo común");
        await almuerzo.Locator("button.agregar-franja").ClickAsync();
        await almuerzo.Locator("input.inicio-franja").FillAsync("11:00");
        await almuerzo.Locator("input.fin-franja").FillAsync("14:00");
        await pagina.Locator("#guardar-panel").ClickAsync();
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToHaveTextAsync("Se guardó la versión 3 de la configuración.");
        await pagina.Locator("#abrir-opciones").ClickAsync();
        await Assertions.Expect(pagina.Locator("article.opcion-admin").First.Locator(".franja-texto")).ToHaveTextAsync("11:00 a 14:00");
        await Capturar(pagina, "14-opciones-con-franja");
        await pagina.Locator("#cancelar-panel").ClickAsync();
        await pagina.Locator("#cancelar-configuracion").ClickAsync();

        // El cierre ordenado: el puesto registra KIOSK_STOPPED y hace la última pasada de las bitácoras.
        Assert.True(puesto.CloseMainWindow(), "La ventana del puesto no recibió el cierre.");
        using (var plazo = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
        {
            await puesto.WaitForExitAsync(plazo.Token);
        }

        _escenario.Registrar($"puesto cerrado en orden, código de salida {puesto.ExitCode}");
        var carpeta = Assert.Single(Directory.GetDirectories(Path.Combine(_escenario.Carpeta, "bitacoras")));
        var general = Lineas(Assert.Single(Directory.GetFiles(carpeta, "INComedor-general-*.log")));
        var interacciones = Lineas(Assert.Single(Directory.GetFiles(carpeta, "INComedor-interacciones-*.log")));
        _escenario.Registrar($"bitácoras: {general.Count} líneas de la general y {interacciones.Count} de interacciones");

        var cambio = Assert.Single(general, l => Tipo(l) == "CONFIGURATION_CHANGED"
            && l.GetProperty("Context").GetProperty("Setting").GetString() == "PERSON").GetProperty("Context");
        Assert.Equal(Nombre, cambio.GetProperty("After").GetProperty("Name").GetString());
        Assert.Equal("APPLIED", cambio.GetProperty("Result").GetString());
        Assert.True(cambio.GetProperty("CredentialChanged").GetBoolean());
        Assert.All(general.Concat(interacciones),
            l => Assert.All(Textos(l).Where(t => !Guid.TryParse(t, out _)), t => Assert.DoesNotContain(Codigo, t)));
    }

    /// <summary>JPEG a la escala CSS, como las capturas de docs/capturas, en la carpeta de resultados de la prueba.</summary>
    private async Task Capturar(IPage pagina, string nombre)
    {
        var ruta = Path.Combine(_escenario.Resultados, $"{nombre}.jpg");
        await pagina.ScreenshotAsync(new() { Path = ruta, Type = ScreenshotType.Jpeg, Quality = 85, Scale = ScreenshotScale.Css });
        _escenario.Registrar($"captura {ruta}");
    }

    private static List<JsonElement> Lineas(string ruta) =>
        File.ReadAllText(ruta).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();

    private static string? Tipo(JsonElement linea) => linea.GetProperty("EventType").GetString();

    /// <summary>Los valores de texto de una línea: el código se busca en ellos, fuera de los UUID sintéticos, que pueden contener esos dígitos.</summary>
    private static IEnumerable<string> Textos(JsonElement elemento) => elemento.ValueKind switch
    {
        JsonValueKind.Object => elemento.EnumerateObject().SelectMany(p => Textos(p.Value)),
        JsonValueKind.Array => elemento.EnumerateArray().SelectMany(Textos),
        JsonValueKind.String => [elemento.GetString()!],
        _ => [],
    };
}
