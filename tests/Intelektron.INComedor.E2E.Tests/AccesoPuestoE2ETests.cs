using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// La ventana y el cierre según el modo con el puesto compilado (acceso-puesto y presentacion-puesto de
/// configurar-puesto-por-pestanas, D7): pantalla completa sin bordes en los dos modos, Escape que cierra en orden en
/// mantenimiento, y en operativo un pedido de cierre de la ventana que se convierte en la clave del día. Lo que bUnit no ve:
/// el estilo de la ventana de Windows, su rectángulo, su ícono y el cierre del proceso.
/// </summary>
public class AccesoPuestoE2ETests : IAsyncLifetime
{
    private const long WsCaption = 0x00C00000;
    private const long WsThickFrame = 0x00040000;
    private const int EstiloDeVentana = -16;
    private const uint WmGetIcon = 0x007F;
    private static readonly IntPtr IconoChico = 0;
    private static readonly IntPtr IconoGrande = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("acceso");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "acceso-puesto · En mantenimiento: la ventana maximizada a pantalla completa, sin barra de título ni bordes y del tamaño del monitor, y Escape la cierra en orden con KIOSK_STOPPED")]
    public async Task En_mantenimiento_pantalla_completa_y_escape_cierra()
    {
        using var playwright = await Playwright.CreateAsync();
        var (puesto, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        ComprobarPantallaCompleta(puesto.MainWindowHandle, "mantenimiento");

        await pagina.Locator(".puesto").FocusAsync();
        await pagina.Keyboard.PressAsync("Escape");
        await EsperarCierre(puesto, TimeSpan.FromSeconds(30));

        var general = General();
        Assert.Contains(general, l => Tipo(l) == "KIOSK_CLOSE_REQUESTED");
        Assert.Equal("KIOSK_STOPPED", Tipo(general[^1]));
    }

    [Fact(DisplayName = "acceso-puesto · En operativo: la misma pantalla completa; el pedido de cierre de la ventana no la cierra en 5 s y muestra la clave, y con 4711 se cierra en orden")]
    public async Task En_operativo_el_cierre_pide_la_clave()
    {
        Directory.CreateDirectory(_escenario.Carpeta);
        File.WriteAllText(Path.Combine(_escenario.Carpeta, "modo-puesto.json"), "{\"modo\":\"OPERATIVO\"}");
        using var playwright = await Playwright.CreateAsync();
        var (puesto, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        ComprobarPantallaCompleta(puesto.MainWindowHandle, "operativo");

        Assert.True(puesto.CloseMainWindow(), "La ventana del puesto no recibió el pedido de cierre.");
        Assert.False(puesto.WaitForExit(5000), "El puesto se cerró en operativo sin la clave del día.");
        await Assertions.Expect(pagina.Locator("#dialogo-clave")).ToBeVisibleAsync();
        _escenario.Registrar("pedido de cierre en operativo: sigue abierto a los 5 s, con la clave a la vista");

        await pagina.Locator("#clave-del-dia").FillAsync("4711");
        await pagina.Locator("#aceptar-clave").ClickAsync();
        await EsperarCierre(puesto, TimeSpan.FromSeconds(30));

        var general = General();
        Assert.Contains(general, l => Tipo(l) == "DAILY_KEY_ACCEPTED");
        Assert.Equal("KIOSK_STOPPED", Tipo(general[^1]));
        Assert.All(general, l => Assert.DoesNotContain("4711", System.Text.RegularExpressions.Regex.Replace(l.GetRawText(),
            "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", "")));
    }

    [Fact(DisplayName = "presentacion-puesto · El monitor guardado no está: con un pantalla.json de un monitor inexistente, el puesto abre a pantalla completa en el principal y la pestaña Pantalla lo avisa")]
    public async Task El_monitor_guardado_no_esta()
    {
        Directory.CreateDirectory(_escenario.Carpeta);
        File.WriteAllText(Path.Combine(_escenario.Carpeta, "pantalla.json"),
            "{\"id\":\"MONITOR-QUE-NO-EXISTE\",\"dispositivo\":\"DISPLAY99\"}");
        using var playwright = await Playwright.CreateAsync();
        var (puesto, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        ComprobarPantallaCompleta(puesto.MainWindowHandle, "mantenimiento, con el monitor guardado ausente");
        var monitor = new InformacionMonitor { Tamano = Marshal.SizeOf<InformacionMonitor>() };
        Assert.True(GetMonitorInfo(MonitorFromWindow(puesto.MainWindowHandle, 2), ref monitor));
        Assert.True((monitor.Marcas & 1) != 0, "El puesto no abrió en el monitor principal.");

        await pagina.Locator(".engranaje").ClickAsync();
        await pagina.Locator("#pestana-pantalla").ClickAsync();
        await Assertions.Expect(pagina.Locator("#aviso-monitor")).ToHaveTextAsync("El monitor guardado no está conectado: el puesto abrió en el principal");
    }

    [Fact(DisplayName = "presentacion-puesto · La ventana con su ícono: la ventana del puesto compilado tiene un ícono propio, grande y chico (WM_GETICON), y el ejecutable lleva al menos un ícono (ExtractIconEx)")]
    public async Task La_ventana_con_su_icono()
    {
        using var playwright = await Playwright.CreateAsync();
        var (puesto, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        var grande = SendMessage(puesto.MainWindowHandle, WmGetIcon, IconoGrande, IntPtr.Zero);
        var chico = SendMessage(puesto.MainWindowHandle, WmGetIcon, IconoChico, IntPtr.Zero);
        var ejecutable = puesto.MainModule!.FileName;
        var iconos = ExtractIconEx(ejecutable, -1, null, null, 0);
        _escenario.Registrar($"ícono: WM_GETICON grande 0x{grande:X}, chico 0x{chico:X}; {Path.GetFileName(ejecutable)} con {iconos} íconos");

        Assert.NotEqual(IntPtr.Zero, grande);
        Assert.NotEqual(IntPtr.Zero, chico);
        Assert.True(iconos >= 1, $"{Path.GetFileName(ejecutable)} no lleva ningún ícono.");
    }

    /// <summary>Maximizada para Windows, sin barra de título ni marco, y con el rectángulo del monitor entero (D7).</summary>
    private void ComprobarPantallaCompleta(IntPtr ventana, string modo)
    {
        var estilo = GetWindowLongPtr(ventana, EstiloDeVentana).ToInt64();
        Assert.True(GetWindowRect(ventana, out var rectangulo), "No se pudo leer el rectángulo de la ventana.");
        var monitor = new InformacionMonitor { Tamano = Marshal.SizeOf<InformacionMonitor>() };
        Assert.True(GetMonitorInfo(MonitorFromWindow(ventana, 2), ref monitor), "No se pudo leer el monitor de la ventana.");
        _escenario.Registrar($"ventana en {modo}: estilo 0x{estilo:X8}, zoom {IsZoomed(ventana)}, rectángulo {rectangulo}, monitor {monitor.Monitor}");

        Assert.True(IsZoomed(ventana), "La ventana del puesto no está maximizada.");
        Assert.Equal(0, estilo & WsCaption);
        Assert.Equal(0, estilo & WsThickFrame);
        Assert.Equal(monitor.Monitor, rectangulo);
    }

    private async Task EsperarCierre(System.Diagnostics.Process puesto, TimeSpan plazo)
    {
        using var cancelacion = new CancellationTokenSource(plazo);
        await puesto.WaitForExitAsync(cancelacion.Token);
        _escenario.Registrar($"puesto cerrado en orden, código de salida {puesto.ExitCode}");
    }

    private List<JsonElement> General()
    {
        var carpeta = Assert.Single(Directory.GetDirectories(Path.Combine(_escenario.Carpeta, "bitacoras")));
        var ruta = Assert.Single(Directory.GetFiles(carpeta, "INComedor-general-*.log"));
        return File.ReadAllText(ruta).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();
    }

    private static string? Tipo(JsonElement linea) => linea.GetProperty("EventType").GetString();

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Rectangulo(int Izquierda, int Arriba, int Derecha, int Abajo);

    [StructLayout(LayoutKind.Sequential)]
    private struct InformacionMonitor
    {
        public int Tamano;
        public Rectangulo Monitor;
        public Rectangulo Trabajo;
        public uint Marcas;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr ventana);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr ventana, int indice);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr ventana, out Rectangulo rectangulo);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr ventana, uint marcas);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr ventana, uint mensaje, IntPtr parametro, IntPtr dato);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW")]
    private static extern uint ExtractIconEx(string archivo, int indice, IntPtr[]? grandes, IntPtr[]? chicos, uint cantidad);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref InformacionMonitor informacion);
}
