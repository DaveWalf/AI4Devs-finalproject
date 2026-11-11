using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Operar sólo con la pantalla, con el puesto compilado (operar-solo-con-la-pantalla, D1 a D5): por CDP y a 1024 × 768,
/// con toques reales de la pantalla táctil (Input.dispatchTouchEvent), no clics de mouse. El código de reposo y una casilla
/// de Mails se escriben sólo con el teclado en pantalla; «Ocultar teclado» dura lo que la sesión de Configuración.
/// </summary>
public class OperacionTactilE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("tactil");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "operacion-tactil · Una casilla sólo con la pantalla (puesto compilado): a 1024 × 768 y con toques, el código 0001 y la casilla p@e.com se escriben con el teclado en pantalla, que deja el campo a la vista, y «Ocultar teclado» dura lo que la sesión de Configuración")]
    public async Task Operar_solo_con_la_pantalla()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await cdp.SendAsync("Emulation.setTouchEmulationEnabled", new Dictionary<string, object> { ["enabled"] = true, ["maxTouchPoints"] = 1 });

        // El código de reposo, sólo con toques.
        await TocarAsync(cdp, pagina, "#codigo");
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToBeVisibleAsync();
        foreach (var tecla in new[] { "0", "0", "0", "1" })
        {
            await TocarAsync(cdp, pagina, $".teclado-general [data-tecla=\"{tecla}\"]");
        }

        await Assertions.Expect(pagina.Locator("#codigo")).ToHaveValueAsync("0001");
        var teclado = (await MedirAsync(pagina, ".teclado-general"))[0];
        var teclas = await MedirAsync(pagina, ".teclado-general .tecla");
        var codigo = (await MedirAsync(pagina, "#codigo"))[0];
        _escenario.Registrar($"1024 × 768, reposo con teclado: el teclado de y {teclado.Arriba:F1} a {teclado.Arriba + teclado.Alto:F1}, {teclas.Length} teclas; el código de y {codigo.Arriba:F1} a {codigo.Arriba + codigo.Alto:F1}");
        await _escenario.CapturarAsync(pagina, "tactil-reposo-1024x768");
        Assert.True(teclado.Arriba + teclado.Alto <= 768 + Tolerancia, $"El teclado termina en {teclado.Arriba + teclado.Alto}");
        Assert.True(codigo.Arriba + codigo.Alto <= teclado.Arriba, $"El código termina en {codigo.Arriba + codigo.Alto} y el teclado empieza en {teclado.Arriba}");
        Assert.All(teclas, t => Assert.True(t.Alto >= 56 - Tolerancia, $"Una tecla de {t.Alto} px"));
        Assert.Equal(0, await pagina.Locator(".teclado-general [data-tecla=\"OCULTAR\"]").CountAsync());

        await TocarAsync(cdp, pagina, "#ingresar");
        await Assertions.Expect(pagina.Locator(".opcion").First).ToBeVisibleAsync();
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToHaveCountAsync(0);
        await pagina.Locator("#cancelar").ClickAsync();

        // Una casilla de Mails, con Configuración acortada arriba del teclado.
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-mails").ClickAsync();
        await Assertions.Expect(pagina.Locator("#remitente-correo")).ToBeVisibleAsync();

        // Con el mouse no aparece: quien tiene un mouse tiene un teclado físico, que escribe como siempre.
        await pagina.Locator("#remitente-correo").ClickAsync();
        await pagina.Keyboard.TypeAsync("f");
        await pagina.WaitForTimeoutAsync(300);
        Assert.Equal(0, await pagina.Locator(".teclado-general").CountAsync());
        await Assertions.Expect(pagina.Locator("#remitente-correo")).ToHaveValueAsync("f");
        await pagina.Locator("#remitente-correo").FillAsync(string.Empty);
        await pagina.Locator("#titulo-correo").ClickAsync();

        await TocarAsync(cdp, pagina, "#remitente-correo");
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToBeVisibleAsync();
        foreach (var tecla in new[] { "P", "@", "E", ".", "C", "O", "M" })
        {
            await TocarAsync(cdp, pagina, $".teclado-general [data-tecla=\"{tecla}\"]");
        }

        await Assertions.Expect(pagina.Locator("#remitente-correo")).ToHaveValueAsync("p@e.com");
        await Assertions.Expect(pagina.Locator("#remitente-correo")).ToBeFocusedAsync();

        // Un toque en el teclado fuera de las teclas (en su margen, lejos de la Q, porque Chromium lleva a la tecla un toque
        // que cae a pocos píxeles de ella) no saca el foco, no cierra el teclado ni escribe.
        var q = (await MedirAsync(pagina, ".teclado-general [data-tecla=\"Q\"]"))[0];
        var general = (await MedirAsync(pagina, ".teclado-general"))[0];
        await TocarEnAsync(cdp, pagina, general.Izquierda + 4, q.Arriba + q.Alto / 2);
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToBeVisibleAsync();
        await Assertions.Expect(pagina.Locator("#remitente-correo")).ToBeFocusedAsync();
        await Assertions.Expect(pagina.Locator("#remitente-correo")).ToHaveValueAsync("p@e.com");
        _escenario.Registrar($"toque en el margen del teclado: x {general.Izquierda + 4:F1}, la Q desde x {q.Izquierda:F1}");

        // Las teclas, con la letra y el borde de los botones del puesto, parejas por fila.
        var estilo = JsonSerializer.Deserialize<Dictionary<string, string>>(await pagina.EvaluateAsync<string>(
            """
            () => {
              const t = getComputedStyle(document.querySelector('.teclado-general .tecla'));
              const c = getComputedStyle(document.querySelector('#cancelar-configuracion'));
              return JSON.stringify({ TeclaFamilia: t.fontFamily, CerrarFamilia: c.fontFamily, TeclaRadio: t.borderTopLeftRadius, CerrarRadio: c.borderTopLeftRadius });
            }
            """))!;
        _escenario.Registrar("estilo de las teclas: " + string.Join("; ", estilo.Select(e => $"{e.Key} {e.Value}")));
        Assert.Equal(estilo["CerrarFamilia"], estilo["TeclaFamilia"]);
        Assert.Equal(estilo["CerrarRadio"], estilo["TeclaRadio"]);
        var filas = JsonSerializer.Deserialize<double[][]>(await pagina.EvaluateAsync<string>(
            """
            () => JSON.stringify([...document.querySelectorAll('.teclado-general .fila-teclas')].map(f => {
              const ts = [...f.querySelectorAll('.tecla')].map(t => t.getBoundingClientRect());
              return [ts[0].left, ts[ts.length - 1].right];
            }))
            """))!;
        _escenario.Registrar("filas del teclado: " + string.Join("; ", filas.Select(f => $"de x {f[0]:F1} a {f[1]:F1}")));
        Assert.All(filas, f => Assert.True(Math.Abs(f[0] - filas[0][0]) <= Tolerancia && Math.Abs(f[1] - filas[0][1]) <= Tolerancia, $"Una fila de x {f[0]} a {f[1]}"));
        var dialogo = (await MedirAsync(pagina, "[role=\"dialog\"]"))[0];
        var remitente = (await MedirAsync(pagina, "#remitente-correo"))[0];
        teclado = (await MedirAsync(pagina, ".teclado-general"))[0];
        _escenario.Registrar($"1024 × 768, Mails con teclado: el diálogo de y {dialogo.Arriba:F1} a {dialogo.Arriba + dialogo.Alto:F1}; el remitente de y {remitente.Arriba:F1} a {remitente.Arriba + remitente.Alto:F1}; el teclado desde y {teclado.Arriba:F1}");
        await _escenario.CapturarAsync(pagina, "tactil-mails-1024x768");
        Assert.True(dialogo.Arriba + dialogo.Alto <= teclado.Arriba + Tolerancia, $"El diálogo termina en {dialogo.Arriba + dialogo.Alto} y el teclado empieza en {teclado.Arriba}");
        Assert.True(remitente.Arriba >= dialogo.Arriba && remitente.Arriba + remitente.Alto <= teclado.Arriba, $"El remitente de y {remitente.Arriba} a {remitente.Arriba + remitente.Alto}");

        // Un campo del fondo de la pestaña también queda arriba del teclado.
        await TocarAsync(cdp, pagina, "#hora-senal");
        await pagina.WaitForTimeoutAsync(600);
        var hora = (await MedirAsync(pagina, "#hora-senal"))[0];
        teclado = (await MedirAsync(pagina, ".teclado-general"))[0];
        _escenario.Registrar($"la hora de la señal de y {hora.Arriba:F1} a {hora.Arriba + hora.Alto:F1}, el teclado desde y {teclado.Arriba:F1}");
        Assert.True(hora.Arriba >= dialogo.Arriba && hora.Arriba + hora.Alto <= teclado.Arriba, $"La hora de y {hora.Arriba} a {hora.Arriba + hora.Alto}");

        // «Ocultar teclado» dura lo que la sesión.
        await TocarAsync(cdp, pagina, ".teclado-general [data-tecla=\"OCULTAR\"]");
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToHaveCountAsync(0);
        await TocarAsync(cdp, pagina, "#servidor-correo");
        await pagina.WaitForTimeoutAsync(300);
        Assert.Equal(0, await pagina.Locator(".teclado-general").CountAsync());
        await pagina.Locator("#cancelar-configuracion").ClickAsync();
        await Assertions.Expect(pagina.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);

        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-mails").ClickAsync();
        await Assertions.Expect(pagina.Locator("#servidor-correo")).ToBeVisibleAsync();
        await TocarAsync(cdp, pagina, "#servidor-correo");
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToBeVisibleAsync();
    }

    [Fact(DisplayName = "operacion-tactil · Lo escrito con las teclas se guarda (puesto compilado): el nombre de una opción y los datos del puesto escritos con las teclas se guardan y se releen; las unidades sólo reciben dígitos; la clave del día y Personas conservan su teclado")]
    public async Task Un_campo_de_opciones_se_guarda()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await cdp.SendAsync("Emulation.setTouchEmulationEnabled", new Dictionary<string, object> { ["enabled"] = true, ["maxTouchPoints"] = 1 });

        // La clave del día tiene su teclado propio: el general no aparece.
        await TocarAsync(cdp, pagina, ".engranaje");
        await Assertions.Expect(pagina.Locator("#dialogo-clave")).ToBeVisibleAsync();
        await TocarAsync(cdp, pagina, "#dialogo-clave input");
        await pagina.WaitForTimeoutAsync(300);
        Assert.Equal(0, await pagina.Locator(".teclado-general").CountAsync());
        await pagina.Keyboard.PressAsync("Escape");
        await Assertions.Expect(pagina.Locator("#dialogo-clave")).ToHaveCountAsync(0);

        // Personas, con su teclado de las raciones: el general no aparece en el nombre.
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-personas").ClickAsync();
        await pagina.Locator("#abrir-personas").ClickAsync();
        await Assertions.Expect(pagina.Locator("tr.persona input.nombre-persona")).ToHaveCountAsync(9);
        await TocarAsync(cdp, pagina, "tr.persona input.nombre-persona");
        await pagina.WaitForTimeoutAsync(300);
        Assert.Equal(0, await pagina.Locator(".teclado-general").CountAsync());
        await pagina.Locator("#cancelar-panel").ClickAsync();

        await pagina.Locator("#pestana-opciones").ClickAsync();
        await pagina.Locator("#abrir-opciones").ClickAsync();

        // Las unidades de la carga de «Almuerzo común» son un número: una letra o un punto no lo vacían, un dígito se suma y
        // «⌫» lo vuelve a sacar.
        var unidades = pagina.Locator("article.opcion-admin input.unidades-carga").First;
        await Assertions.Expect(unidades).ToBeVisibleAsync();
        var antes = await unidades.InputValueAsync();
        await TocarAsync(cdp, pagina, "article.opcion-admin input.unidades-carga");
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToBeVisibleAsync();
        await TocarAsync(cdp, pagina, ".teclado-general [data-tecla=\"A\"]");
        await TocarAsync(cdp, pagina, ".teclado-general [data-tecla=\".\"]");
        await Assertions.Expect(unidades).ToHaveValueAsync(antes);
        await TocarAsync(cdp, pagina, ".teclado-general [data-tecla=\"2\"]");
        await Assertions.Expect(unidades).ToHaveValueAsync(antes + "2");
        await TocarAsync(cdp, pagina, ".teclado-general [data-tecla=\"BORRAR\"]");
        await Assertions.Expect(unidades).ToHaveValueAsync(antes);
        _escenario.Registrar($"unidades: «{antes}», con A y . «{antes}», con 2 «{antes}2», con ⌫ «{antes}»");

        var nombre = pagina.Locator("article.opcion-admin input.nombre-opcion").First;
        await Assertions.Expect(nombre).ToHaveValueAsync("Almuerzo común");
        await TocarAsync(cdp, pagina, "article.opcion-admin input.nombre-opcion");
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToBeVisibleAsync();
        await pagina.EvaluateAsync("() => { const c = document.activeElement; c.setSelectionRange(c.value.length, c.value.length); }");
        await TocarAsync(cdp, pagina, ".teclado-general [data-tecla=\"2\"]");
        await Assertions.Expect(nombre).ToHaveValueAsync("Almuerzo común2");

        await TocarAsync(cdp, pagina, "#guardar-panel");
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToContainTextAsync("Se guardó la versión");
        await pagina.Locator("#abrir-opciones").ClickAsync();
        await Assertions.Expect(pagina.Locator("article.opcion-admin input.nombre-opcion").First).ToHaveValueAsync("Almuerzo común2");
        await _escenario.CapturarAsync(pagina, "tactil-opciones-guardada-1024x768");
        await pagina.Locator("#cancelar-panel").ClickAsync();

        // Los datos del puesto: el número respeta su largo de 3, y lo guardado se relee al volver a entrar.
        await pagina.Locator("#pestana-impresora").ClickAsync();
        await TocarAsync(cdp, pagina, "#numero-puesto");
        await Assertions.Expect(pagina.Locator(".teclado-general")).ToBeVisibleAsync();
        await pagina.EvaluateAsync("() => document.activeElement.select()");
        foreach (var tecla in new[] { "1", "2", "3", "4" })
        {
            await TocarAsync(cdp, pagina, $".teclado-general [data-tecla=\"{tecla}\"]");
        }

        await Assertions.Expect(pagina.Locator("#numero-puesto")).ToHaveValueAsync("123");
        await TocarAsync(cdp, pagina, "#nombre-puesto");
        await pagina.EvaluateAsync("() => document.activeElement.select()");
        foreach (var tecla in new[] { "N", "O", "R", "T", "E" })
        {
            await TocarAsync(cdp, pagina, $".teclado-general [data-tecla=\"{tecla}\"]");
        }

        await TocarAsync(cdp, pagina, "#guardar-datos-comprobante");
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToContainTextAsync("Se guardaron los datos del comprobante");
        await pagina.Locator("#cancelar-configuracion").ClickAsync();
        await Assertions.Expect(pagina.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-impresora").ClickAsync();
        await Assertions.Expect(pagina.Locator("#numero-puesto")).ToHaveValueAsync("123");
        await Assertions.Expect(pagina.Locator("#nombre-puesto")).ToHaveValueAsync("norte");
    }

    /// <summary>Un toque de la pantalla táctil en el centro del elemento, como un dedo: no un clic de mouse.</summary>
    private static async Task TocarAsync(ICDPSession cdp, IPage pagina, string selector)
    {
        await pagina.Locator(selector).First.ScrollIntoViewIfNeededAsync();
        var caja = (await MedirAsync(pagina, selector))[0];
        var punto = new Dictionary<string, object> { ["x"] = caja.Izquierda + caja.Ancho / 2, ["y"] = caja.Arriba + caja.Alto / 2 };
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = "touchStart", ["touchPoints"] = new[] { punto } });
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = "touchEnd", ["touchPoints"] = Array.Empty<object>() });
        await pagina.WaitForTimeoutAsync(60);
    }

    /// <summary>Un toque en un punto de la pantalla.</summary>
    private static async Task TocarEnAsync(ICDPSession cdp, IPage pagina, double x, double y)
    {
        var punto = new Dictionary<string, object> { ["x"] = x, ["y"] = y };
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = "touchStart", ["touchPoints"] = new[] { punto } });
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object> { ["type"] = "touchEnd", ["touchPoints"] = Array.Empty<object>() });
        await pagina.WaitForTimeoutAsync(60);
    }

    private static async Task<Caja[]> MedirAsync(IPage pagina, string selector) =>
        JsonSerializer.Deserialize<Caja[]>(await pagina.EvaluateAsync<string>(
            """
            s => JSON.stringify([...document.querySelectorAll(s)].map(e => {
              const b = e.getBoundingClientRect();
              return { Id: e.id, Arriba: b.top, Izquierda: b.left, Ancho: b.width, Alto: b.height };
            }))
            """, selector))!;

    private sealed record Caja(string Id, double Arriba, double Izquierda, double Ancho, double Alto);
}
