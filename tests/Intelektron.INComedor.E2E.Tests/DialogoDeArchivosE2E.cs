using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Contesta el diálogo de archivos de Windows del puesto, que CDP no maneja (D6 de importar-opciones-desde-excel): lo busca
/// entre las ventanas del proceso por su título, escribe la ruta en el cuadro del nombre (el control 1148 de todo diálogo
/// común de archivos) hasta leerla de vuelta con WM_GETTEXT, porque el diálogo vacía las primeras escrituras mientras se arma,
/// y le manda IDOK, que es «Abrir»; si sigue abierto, toca «Abrir» con BM_CLICK.
/// </summary>
internal static class DialogoDeArchivosE2E
{
    private const uint WmSetText = 0x000C;
    private const uint WmGetText = 0x000D;
    private const uint WmCommand = 0x0111;
    private const uint BmClick = 0x00F5;
    private const int IdOk = 1;

    private delegate bool EnumeracionVentanas(IntPtr ventana, IntPtr dato);

    public static async Task ElegirAsync(Process puesto, string titulo, string ruta, Action<string> registrar)
    {
        var dialogo = IntPtr.Zero;
        var limite = Stopwatch.StartNew();
        while (dialogo == IntPtr.Zero && limite.Elapsed < TimeSpan.FromSeconds(15))
        {
            dialogo = BuscarDialogo(puesto.Id, titulo);
            if (dialogo == IntPtr.Zero)
            {
                await Task.Delay(200);
            }
        }

        Assert.True(dialogo != IntPtr.Zero, $"No apareció «{titulo}» en {limite.Elapsed.TotalSeconds:F1} s");

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
        registrar($"diálogo «{titulo}» en {limite.Elapsed.TotalSeconds:F1} s, la ruta escrita y leída en {intentos} intentos; se manda «Abrir»");
        PostMessage(dialogo, WmCommand, (IntPtr)IdOk, GetDlgItem(dialogo, IdOk));
        if (!await CerradoAsync(dialogo))
        {
            registrar("el diálogo siguió abierto con IDOK: se toca «Abrir» con BM_CLICK");
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

    private static IntPtr BuscarDialogo(int proceso, string titulo)
    {
        var encontrado = IntPtr.Zero;
        EnumWindows((ventana, _) =>
        {
            GetWindowThreadProcessId(ventana, out var id);
            if (id == proceso && Clase(ventana) == "#32770" && Titulo(ventana) == titulo)
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr ventana);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr ventana);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr ventana, uint mensaje, IntPtr parametro, string texto);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr ventana, uint mensaje, IntPtr parametro, StringBuilder texto);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr ventana, uint mensaje, IntPtr parametro, IntPtr dato);
}
