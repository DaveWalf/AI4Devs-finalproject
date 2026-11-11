using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// La planilla de ejemplo y la importación de Personas con el puesto compilado (importar-personas-desde-excel, D6, D7 y D9):
/// por CDP y a 1024 × 768, con la tolerancia de 1 px de la regla de prolijidad. La carpeta de exportación se deja elegida en
/// <c>exportacion.json</c>, como en Consumos; el diálogo de archivos de Windows, que CDP no maneja, se contesta con mensajes de
/// ventana: la ruta en el cuadro del nombre y «Abrir».
/// </summary>
public class ImportarPersonasE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    /// <summary>El título del diálogo de <c>ElectorArchivoWindows</c>.</summary>
    private const string TituloDialogo = "Planilla de Personas para importar";

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("importar-personas");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "administracion-local · Importar Personas desde Excel (puesto compilado): a 1024 × 768, los cuatro botones del pie miden lo mismo con la letra y el alto de «Cerrar»; la planilla de ejemplo, cambiada para 0001 y vuelta a importar, muestra el resumen con sus tres botones parejos y «Modificar y agregar» deja la versión 2")]
    public async Task Planilla_de_ejemplo_importada_con_un_cambio()
    {
        var destino = Directory.CreateDirectory(Path.Combine(_escenario.Carpeta, "exportaciones")).FullName;
        File.WriteAllText(Path.Combine(_escenario.Carpeta, "exportacion.json"), JsonSerializer.Serialize(new { carpeta = destino }));
        using var playwright = await Playwright.CreateAsync();
        var (puesto, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-personas").ClickAsync();
        await Assertions.Expect(pagina.Locator("#importar-personas")).ToBeVisibleAsync();

        var pie = await MedirAsync(pagina, ["#exportar-personas-csv", "#exportar-personas-xlsx", "#importar-personas", "#planilla-personas", "#cancelar-configuracion"]);
        _escenario.Registrar("1024 × 768, el pie de Personas: " + string.Join("; ", pie.Cajas.Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1} px desde {c.Arriba:F1}, derecha {c.Derecha:F1}, letra {c.Letra}"))
            + $"; el diálogo termina a la derecha en {pie.DialogoDerecha:F1} px");
        await _escenario.CapturarAsync(pagina, "personas-pie-importar-1024x768");
        var cerrar = pie.Cajas[^1];
        Assert.All(pie.Cajas, c => Assert.True(Math.Abs(c.Alto - cerrar.Alto) <= Tolerancia && Math.Abs(c.Arriba - cerrar.Arriba) <= Tolerancia && c.Letra == cerrar.Letra,
            $"{c.Id}: {c.Alto} px desde {c.Arriba}, letra {c.Letra}, contra «Cerrar»"));
        Assert.All(pie.Cajas[..4], c => Assert.True(Math.Abs(c.Ancho - pie.Cajas[0].Ancho) <= Tolerancia, $"{c.Id} mide {c.Ancho} px y el primero {pie.Cajas[0].Ancho}"));
        Assert.All(pie.Cajas, c => Assert.True(c.Derecha <= pie.DialogoDerecha + Tolerancia, $"{c.Id} termina en {c.Derecha} px, fuera del diálogo"));

        await pagina.Locator("#planilla-personas").ClickAsync();
        await Assertions.Expect(pagina.Locator("#mensaje-exportacion-personas")).ToContainTextAsync("Planilla de ejemplo guardada como");
        var planilla = Assert.Single(Directory.GetFiles(destino, "INComedor-planilla-personas-*.xlsx"));
        _escenario.Registrar($"planilla de ejemplo: {Path.GetFileName(planilla)}");

        // El cambio: la primera fila de ejemplo pasa a ser 0001, con ALMUERZO 1 y REFRIGERIO 1 (tenía 2).
        Cambiar(planilla, ("Persona de ejemplo 1<", "Persona sintética 01<"), (">1001<", ">0001<"));

        var toque = pagina.Locator("#importar-personas").ClickAsync();
        await ElegirArchivoAsync(puesto, planilla);
        await toque;
        var resumen = pagina.Locator("#resumen-importacion");
        await Assertions.Expect(resumen).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(resumen).ToContainTextAsync("1 persona nueva, 1 modificada y 0 sin cambios.");
        var cartel = await MedirAsync(pagina, ["#importar-modificar", "#importar-reemplazar", "#importar-cancelar"]);
        var abajo = await pagina.EvaluateAsync<double>("() => document.querySelector('#resumen-importacion').getBoundingClientRect().bottom");
        _escenario.Registrar("1024 × 768, el resumen de la importación: " + string.Join("; ", cartel.Cajas.Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1} px desde {c.Arriba:F1}, letra {c.Letra}"))
            + $"; el cartel termina en {abajo:F1} px");
        await _escenario.CapturarAsync(pagina, "personas-resumen-importacion-1024x768");
        Assert.All(cartel.Cajas, c => Assert.True(Math.Abs(c.Alto - cartel.Cajas[0].Alto) <= Tolerancia && Math.Abs(c.Arriba - cartel.Cajas[0].Arriba) <= Tolerancia
            && c.Letra == cartel.Cajas[0].Letra && c.Alto >= 56 - Tolerancia, $"{c.Id}: {c.Alto} px desde {c.Arriba}, letra {c.Letra}"));
        Assert.True(abajo <= 768, $"El cartel termina en {abajo} px, fuera de la ventana");

        await pagina.Locator("#importar-modificar").ClickAsync();

        await Assertions.Expect(resumen).ToHaveCountAsync(0);
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToContainTextAsync("versión 2");
        await Assertions.Expect(pagina.Locator("#tabla-personas tr.persona", new() { HasText = "Persona sintética 01" })
            .Locator("td.cupo[data-cupo=\"REFRIGERIO\"]")).ToHaveTextAsync("0/1");
        await Assertions.Expect(pagina.Locator("#tabla-personas tr.persona", new() { HasText = "Persona de ejemplo 2" })
            .Locator("td.cupo[data-cupo=\"ALMUERZO\"]")).ToHaveTextAsync("0/2");
        await _escenario.CapturarAsync(pagina, "personas-importadas-1024x768");
    }

    private static async Task<Medidas> MedirAsync(IPage pagina, string[] ids) =>
        JsonSerializer.Deserialize<Medidas>(await pagina.EvaluateAsync<string>(
            """
            ids => JSON.stringify({
              Cajas: ids.map(i => {
                const e = document.querySelector(i), b = e.getBoundingClientRect();
                return { Id: i, Arriba: b.top, Alto: b.height, Ancho: b.width, Derecha: b.right, Letra: getComputedStyle(e).fontSize };
              }),
              DialogoDerecha: document.querySelector('.dialogo[role="dialog"]').getBoundingClientRect().right
            })
            """, ids))!;

    /// <summary>Reemplaza textos en la hoja de un libro del puesto, que guarda las cadenas en línea.</summary>
    private static void Cambiar(string libro, params (string Antes, string Despues)[] cambios)
    {
        using var zip = ZipFile.Open(libro, ZipArchiveMode.Update);
        var entrada = zip.GetEntry("xl/worksheets/sheet1.xml")!;
        string xml;
        using (var lector = new StreamReader(entrada.Open(), Encoding.UTF8))
        {
            xml = lector.ReadToEnd();
        }

        foreach (var (antes, despues) in cambios)
        {
            Assert.Contains(antes, xml, StringComparison.Ordinal);
            xml = xml.Replace(antes, despues, StringComparison.Ordinal);
        }

        entrada.Delete();
        using var escritor = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open(), new UTF8Encoding(false));
        escritor.Write(xml);
    }

    /// <summary>
    /// Contesta el diálogo de archivos del puesto: lo busca entre las ventanas del proceso por su título, escribe la ruta en
    /// el cuadro del nombre (el control 1148 de todo diálogo común de archivos) y le manda IDOK, que es «Abrir».
    /// </summary>
    private async Task ElegirArchivoAsync(Process puesto, string ruta)
    {
        var dialogo = IntPtr.Zero;
        var limite = Stopwatch.StartNew();
        while (dialogo == IntPtr.Zero && limite.Elapsed < TimeSpan.FromSeconds(15))
        {
            dialogo = BuscarDialogo(puesto.Id);
            if (dialogo == IntPtr.Zero)
            {
                await Task.Delay(200);
            }
        }

        Assert.True(dialogo != IntPtr.Zero, $"No apareció «{TituloDialogo}» en {limite.Elapsed.TotalSeconds:F1} s");

        // El diálogo arma sus controles después de aparecer y puede vaciar el nombre: se escribe la ruta hasta leerla de vuelta
        // (con WM_GETTEXT, que cruza procesos; GetWindowText no lee el texto de un Edit ajeno).
        var intentos = 0;
        var escrita = false;
        while (!escrita && limite.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(300);
            intentos++;
            var nombre = IsWindowVisible(dialogo)
                ? Descendientes(dialogo).FirstOrDefault(v => Clase(v) == "Edit" && (GetDlgCtrlID(v) == 1148 || GetDlgCtrlID(GetParent(GetParent(v))) == 1148))
                : IntPtr.Zero;
            if (nombre != IntPtr.Zero)
            {
                SendMessage(nombre, WmSetText, IntPtr.Zero, ruta);
                var leido = new StringBuilder(1024);
                SendMessage(nombre, WmGetText, (IntPtr)leido.Capacity, leido);
                escrita = leido.ToString() == ruta;
            }
        }

        Assert.True(escrita, $"La ruta no quedó en el cuadro del nombre en {intentos} intentos");
        _escenario.Registrar($"diálogo de archivos en {limite.Elapsed.TotalSeconds:F1} s, la ruta escrita y leída en {intentos} intentos; se manda «Abrir»");
        PostMessage(dialogo, WmCommand, (IntPtr)IdOk, GetDlgItem(dialogo, IdOk));
        if (!await CerradoAsync(dialogo))
        {
            _escenario.Registrar("el diálogo siguió abierto con IDOK: se toca «Abrir» con BM_CLICK");
            PostMessage(GetDlgItem(dialogo, IdOk), BmClick, IntPtr.Zero, IntPtr.Zero);
            Assert.True(await CerradoAsync(dialogo), "El diálogo de archivos no se cerró con «Abrir»");
        }
    }

    private static async Task<bool> CerradoAsync(IntPtr dialogo)
    {
        for (var i = 0; i < 20 && IsWindow(dialogo); i++)
        {
            await Task.Delay(150);
        }

        return !IsWindow(dialogo);
    }

    private static IntPtr BuscarDialogo(int proceso)
    {
        var encontrado = IntPtr.Zero;
        EnumWindows((ventana, _) =>
        {
            GetWindowThreadProcessId(ventana, out var id);
            if (id == proceso && Clase(ventana) == "#32770" && Titulo(ventana) == TituloDialogo)
            {
                encontrado = ventana;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return encontrado;
    }

    private static List<IntPtr> Descendientes(IntPtr ventana)
    {
        var lista = new List<IntPtr>();
        EnumChildWindows(ventana, (hija, _) =>
        {
            lista.Add(hija);
            return true;
        }, IntPtr.Zero);
        return lista;
    }

    private static string Clase(IntPtr ventana)
    {
        var texto = new StringBuilder(256);
        GetClassName(ventana, texto, texto.Capacity);
        return texto.ToString();
    }

    private static string Titulo(IntPtr ventana)
    {
        var texto = new StringBuilder(256);
        GetWindowText(ventana, texto, texto.Capacity);
        return texto.ToString();
    }

    private const uint WmSetText = 0x000C;
    private const uint WmGetText = 0x000D;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr ventana);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr ventana, uint mensaje, IntPtr parametro, StringBuilder texto);
    private const uint WmCommand = 0x0111;
    private const uint BmClick = 0x00F5;
    private const int IdOk = 1;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr ventana);

    private delegate bool EnumeracionVentanas(IntPtr ventana, IntPtr dato);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumeracionVentanas llamada, IntPtr dato);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(IntPtr ventana, EnumeracionVentanas llamada, IntPtr dato);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr ventana, out int proceso);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
    private static extern int GetClassName(IntPtr ventana, StringBuilder clase, int largo);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    private static extern int GetWindowText(IntPtr ventana, StringBuilder texto, int largo);

    [DllImport("user32.dll")]
    private static extern int GetDlgCtrlID(IntPtr ventana);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDlgItem(IntPtr dialogo, int control);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr ventana);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr ventana, uint mensaje, IntPtr parametro, string texto);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr ventana, uint mensaje, IntPtr parametro, IntPtr dato);

    private sealed record Caja(string Id, double Arriba, double Alto, double Ancho, double Derecha, string Letra);

    private sealed record Medidas(Caja[] Cajas, double DialogoDerecha);
}
