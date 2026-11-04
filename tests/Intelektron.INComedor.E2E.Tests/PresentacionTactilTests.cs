using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// La presentación táctil en la ventana real (D8 y D9 de presentar-atencion-tactil): el puesto compilado con
/// INCOMEDOR_CATALOGO=nueve, manejado por CDP. Las áreas de 1024 × 768 y de 600 de ancho se fijan con
/// Emulation.setDeviceMetricsOverride y el movimiento reducido con Emulation.setEmulatedMedia; las cajas se miden en la
/// página. El puesto se lanza acá, con la carpeta y los puertos del escenario, porque el escenario no pasa variables del
/// catálogo; se cierra por su PID con su árbol.
/// </summary>
public class PresentacionTactilTests : IAsyncLifetime
{
    private const string Opcion = "Almuerzo con gaseosa";

    private EscenarioE2E _escenario = null!;
    private Process? _puesto;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar(TestContext.Current.Test!.TestCase.TestMethod!.MethodName);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_puesto is not null)
        {
            await _escenario.DetenerAsync(_puesto);
        }

        await _escenario.DisposeAsync();
    }

    [Fact(DisplayName = "presentacion-puesto · Abre maximizada: la ventana principal del puesto está maximizada según IsZoomed")]
    public async Task La_ventana_abre_maximizada()
    {
        using var playwright = await Playwright.CreateAsync();
        await AbrirAsync(playwright);

        _puesto!.Refresh();
        Assert.NotEqual(IntPtr.Zero, _puesto.MainWindowHandle);
        Assert.True(IsZoomed(_puesto.MainWindowHandle), "La ventana del puesto no está maximizada.");
    }

    [Fact(DisplayName = "presentacion-puesto · Nueve opciones y las acciones entran en 1024 × 768 con 56 px de alto; en 600 de ancho, una columna con la misma letra")]
    public async Task Nueve_opciones_en_1024_por_768_y_una_columna_en_600()
    {
        using var playwright = await Playwright.CreateAsync();
        var pagina = await AbrirAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);

        await Area(cdp, 1024, 768);
        await Ingresar(pagina);
        await pagina.Locator(".opcion", new() { HasText = Opcion }).ClickAsync();
        await _escenario.CapturarAsync(pagina, "seleccion-1024x768");
        var ancha = await Medir(pagina);
        _escenario.Registrar($"1024 × 768: {ancha}");

        Assert.Equal(9, ancha.Opciones.Length);
        Assert.Equal(2, ancha.Opciones.Select(o => o.X).Distinct().Count());
        Assert.All(ancha.Opciones.Concat([ancha.Cancelar, ancha.Confirmar]), c =>
        {
            Assert.True(c.Alto >= 56, $"{c.Texto}: {c.Alto} px de alto");
            Assert.True(c.Abajo <= 768, $"{c.Texto}: termina en {c.Abajo} px");
        });
        Assert.True(ancha.AltoDocumento <= 768, $"La página mide {ancha.AltoDocumento} px de alto");
        Assert.True(ancha.AlfaTextoOpcion == 1, $"El texto de las opciones es {ancha.ColorTextoOpcion}, con alfa {ancha.AlfaTextoOpcion}");

        await Area(cdp, 600, 768);
        await _escenario.CapturarAsync(pagina, "seleccion-600");
        var angosta = await Medir(pagina);
        _escenario.Registrar($"600: {angosta}");

        Assert.Equal(9, angosta.Opciones.Select(o => o.Texto).Distinct().Count());
        Assert.Single(angosta.Opciones.Select(o => o.X).Distinct());
        Assert.Equal(ancha.LetraOpcion, angosta.LetraOpcion);
        Assert.All(angosta.Opciones, o => Assert.True(o.Alto >= 56, $"{o.Texto}: {o.Alto} px de alto"));
        Assert.True(angosta.AltoDocumento > 768, $"La página mide {angosta.AltoDocumento} px de alto: no se desplaza en vertical");
    }

    [Fact(DisplayName = "comprobante-local · Movimiento reducido: el comprobante en pantalla aparece completo, sin animación")]
    public async Task Con_movimiento_reducido_el_comprobante_no_se_anima()
    {
        using var playwright = await Playwright.CreateAsync();
        var pagina = await AbrirAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setEmulatedMedia", new Dictionary<string, object>
        {
            ["features"] = new[] { new Dictionary<string, string> { ["name"] = "prefers-reduced-motion", ["value"] = "reduce" } },
        });
        Assert.True(await pagina.EvaluateAsync<bool>("matchMedia('(prefers-reduced-motion: reduce)').matches"));

        await Ingresar(pagina);
        await pagina.Locator(".opcion", new() { HasText = Opcion }).ClickAsync();
        await pagina.Locator("#confirmar").ClickAsync();
        var comprobante = pagina.Locator(".comprobante");
        await Assertions.Expect(comprobante).ToBeVisibleAsync();
        await _escenario.CapturarAsync(pagina, "comprobante-sin-movimiento");

        var animaciones = await comprobante.EvaluateAsync<int>("e => e.getAnimations({ subtree: true }).length");
        var nombre = await comprobante.EvaluateAsync<string>("e => getComputedStyle(e).animationName");
        _escenario.Registrar($"comprobante con movimiento reducido: {animaciones} animaciones, animation-name {nombre}");
        Assert.Equal(0, animaciones);
        Assert.Equal("none", nombre);
        await Assertions.Expect(comprobante).ToContainTextAsync(Opcion);
    }

    /// <summary>El puesto con la carpeta y el CDP del escenario y el catálogo de nueve, y su página de reposo.</summary>
    private async Task<IPage> AbrirAsync(IPlaywright playwright)
    {
        var exe = EscenarioE2E.ExeDelPaquete ?? ExeDelKiosk();
        var inicio = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
        foreach (var heredada in inicio.Environment.Keys
                     .Where(v => v.StartsWith("INCOMEDOR_", StringComparison.OrdinalIgnoreCase) || v.StartsWith("WEBVIEW2_", StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            inicio.Environment.Remove(heredada);
        }

        inicio.Environment["INCOMEDOR_DEMO_DIR"] = _escenario.Carpeta;
        inicio.Environment["INCOMEDOR_RECEPTOR_URL"] = _escenario.DireccionReceptor.ToString();
        inicio.Environment["INCOMEDOR_CDP_PORT"] = _escenario.PuertoCdp.ToString();
        inicio.Environment["INCOMEDOR_CATALOGO"] = "nueve";
        _puesto = Process.Start(inicio) ?? throw new InvalidOperationException("No arrancó el puesto.");
        _escenario.Registrar($"puesto PID {_puesto.Id} · {exe} · INCOMEDOR_CATALOGO=nueve");
        var pagina = await _escenario.ConectarAsync(playwright);

        // La ventana tarda un momento en tener su identificador después de que la página responde.
        var reloj = Stopwatch.StartNew();
        while (_puesto.MainWindowHandle == IntPtr.Zero && reloj.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(250);
            _puesto.Refresh();
        }

        return pagina;
    }

    private static Task Area(ICDPSession cdp, int ancho, int alto) =>
        cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = ancho,
            ["height"] = alto,
            ["deviceScaleFactor"] = 1,
            ["mobile"] = false,
        });

    private static async Task Ingresar(IPage pagina)
    {
        await pagina.Locator("#codigo").FillAsync("0001");
        await pagina.Locator("#ingresar").ClickAsync();
        await Assertions.Expect(pagina.Locator(".opcion")).ToHaveCountAsync(9);
    }

    /// <summary>Las cajas de las opciones y de las dos acciones, el alto del documento, y la letra y el color calculado (con su alfa) del nombre de una opción.</summary>
    private static async Task<Medida> Medir(IPage pagina)
    {
        var json = await pagina.EvaluateAsync<string>(
            """
            () => {
              const nombre = document.querySelector('.opcion .nombre');
              const color = getComputedStyle(nombre).color;
              const canales = color.match(/[\d.]+/g) ?? [];
              const caja = e => { const r = e.getBoundingClientRect(); return { Texto: e.innerText.split('\n')[0], X: Math.round(r.left + scrollX), Alto: r.height, Abajo: r.bottom + scrollY }; };
              return JSON.stringify({
                Opciones: [...document.querySelectorAll('.opcion')].map(caja),
                Cancelar: caja(document.querySelector('#cancelar')),
                Confirmar: caja(document.querySelector('#confirmar')),
                AltoDocumento: document.documentElement.scrollHeight,
                LetraOpcion: getComputedStyle(nombre).fontSize,
                ColorTextoOpcion: color,
                AlfaTextoOpcion: canales.length > 3 ? Number(canales[3]) : 1,
              });
            }
            """);
        return JsonSerializer.Deserialize<Medida>(json)!;
    }

    private static string ExeDelKiosk()
    {
        var configuracion = typeof(PresentacionTactilTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "Configuracion").Value!;
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "INComedor.sln")))
        {
            carpeta = carpeta.Parent;
        }

        var exe = Path.Combine(carpeta!.FullName, "src", "Intelektron.INComedor.Kiosk", "bin", configuracion, "net11.0-windows10.0.17763.0", "Intelektron.INComedor.Kiosk.exe");
        return File.Exists(exe) ? exe : throw new FileNotFoundException($"No está compilado {exe}.", exe);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr ventana);

    private sealed record Caja(string Texto, double X, double Alto, double Abajo);

    private sealed record Medida(Caja[] Opciones, Caja Cancelar, Caja Confirmar, double AltoDocumento, string LetraOpcion, string ColorTextoOpcion, double AlfaTextoOpcion)
    {
        public override string ToString() =>
            $"{Opciones.Length} opciones en {Opciones.Select(o => o.X).Distinct().Count()} columnas, alto mínimo {Opciones.Concat([Cancelar, Confirmar]).Min(c => c.Alto):F0} px, " +
            $"última caja en {Opciones.Concat([Cancelar, Confirmar]).Max(c => c.Abajo):F0} px, documento {AltoDocumento:F0} px, letra {LetraOpcion}, texto {ColorTextoOpcion}";
    }
}
