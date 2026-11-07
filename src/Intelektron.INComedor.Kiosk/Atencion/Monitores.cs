using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>Un rectángulo de la pantalla en píxeles físicos.</summary>
public sealed record RectanguloPantalla(int Izquierda, int Arriba, int Derecha, int Abajo)
{
    public int Ancho => Derecha - Izquierda;

    public int Alto => Abajo - Arriba;
}

/// <summary>
/// Un monitor conectado (D10 de configurar-puesto-por-pestanas): su identificador de interfaz, que sobrevive a un reinicio y a
/// un cambio de orden; su nombre de dispositivo (\\.\DISPLAYn), que no; su área, si es el principal, su área de trabajo y el
/// nombre que da Windows.
/// </summary>
public sealed record MonitorDelPuesto(string Id, string Dispositivo, RectanguloPantalla Area, bool Principal, RectanguloPantalla? Trabajo = null, string? Nombre = null);

/// <summary>Lo guardado en pantalla.json: el identificador y el nombre de dispositivo del monitor del puesto.</summary>
public sealed record PantallaGuardada(string Id, string Dispositivo);

/// <summary>Lo que se leyó de pantalla.json: nada (sin archivo), el monitor guardado, o que no se pudo leer.</summary>
public sealed record LecturaPantalla(PantallaGuardada? Guardada, bool Ilegible);

/// <summary>El monitor donde abre el puesto y, si no es el guardado, por qué.</summary>
public sealed record EleccionMonitor(MonitorDelPuesto Monitor, string? Aviso);

/// <summary>
/// Los monitores del equipo con tres llamadas de user32 (D10): EnumDisplayMonitors, GetMonitorInfo y EnumDisplayDevices con el
/// nombre de interfaz del monitor. La elección es una función pura sobre esa lista y lo guardado.
/// </summary>
public static class Monitores
{
    private const uint MonitorPrincipal = 1;
    private const uint NombreDeInterfaz = 1;

    private delegate bool AlEnumerar(IntPtr monitor, IntPtr contexto, ref Rect rectangulo, IntPtr dato);

    public static IReadOnlyList<MonitorDelPuesto> Leer()
    {
        var monitores = new List<MonitorDelPuesto>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr _, ref Rect _, IntPtr _) =>
        {
            var informacion = new InformacionMonitor { Tamano = Marshal.SizeOf<InformacionMonitor>() };
            if (GetMonitorInfo(monitor, ref informacion))
            {
                var dispositivo = new Dispositivo { Tamano = Marshal.SizeOf<Dispositivo>() };
                var tieneDispositivo = EnumDisplayDevices(informacion.Nombre, 0, ref dispositivo, NombreDeInterfaz);
                var id = tieneDispositivo && dispositivo.Id.Length > 0 ? dispositivo.Id : informacion.Nombre;
                monitores.Add(new MonitorDelPuesto(id, informacion.Nombre, informacion.Monitor.Rectangulo(),
                    (informacion.Marcas & MonitorPrincipal) != 0, informacion.Trabajo.Rectangulo(),
                    tieneDispositivo && dispositivo.Descripcion.Length > 0 ? dispositivo.Descripcion : informacion.Nombre));
            }

            return true;
        }, IntPtr.Zero);
        return monitores;
    }

    /// <summary>
    /// El guardado si está conectado; si no está, o lo guardado no se pudo leer, el principal con el aviso; sin nada guardado,
    /// el principal sin aviso.
    /// </summary>
    public static EleccionMonitor ElegirMonitor(IReadOnlyList<MonitorDelPuesto> monitores, LecturaPantalla lectura)
    {
        var principal = monitores.FirstOrDefault(m => m.Principal) ?? monitores[0];
        if (lectura.Ilegible)
        {
            return new EleccionMonitor(principal, MensajesAtencion.MonitorIlegible);
        }

        if (lectura.Guardada is not { } guardada)
        {
            return new EleccionMonitor(principal, null);
        }

        return monitores.FirstOrDefault(m => m.Id == guardada.Id) is { } hallado
            ? new EleccionMonitor(hallado, null)
            : new EleccionMonitor(principal, MensajesAtencion.MonitorAusente);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Izquierda;
        public int Arriba;
        public int Derecha;
        public int Abajo;

        public readonly RectanguloPantalla Rectangulo() => new(Izquierda, Arriba, Derecha, Abajo);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InformacionMonitor
    {
        public int Tamano;
        public Rect Monitor;
        public Rect Trabajo;
        public uint Marcas;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Nombre;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Dispositivo
    {
        public int Tamano;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string NombreDispositivo;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Descripcion;

        public uint Estado;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Id;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Clave;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr contexto, IntPtr recorte, AlEnumerar alEnumerar, IntPtr dato);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref InformacionMonitor informacion);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplayDevicesW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string? dispositivo, uint indice, ref Dispositivo datos, uint marcas);
}

/// <summary>El monitor del puesto en pantalla.json de la carpeta de datos, con la escritura atómica del estilo (D10).</summary>
public sealed class AlmacenPantalla
{
    private const string Nombre = "pantalla.json";

    private readonly string _carpeta;

    public AlmacenPantalla(string carpetaDatos)
    {
        _carpeta = carpetaDatos;
    }

    private string Ruta => Path.Combine(_carpeta, Nombre);

    public LecturaPantalla Leer()
    {
        if (!File.Exists(Ruta))
        {
            return new LecturaPantalla(null, false);
        }

        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(Ruta));
            var raiz = documento.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object
                && raiz.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && id.GetString() is { Length: > 0 and <= 512 } valorId
                && raiz.TryGetProperty("dispositivo", out var dispositivo) && dispositivo.ValueKind == JsonValueKind.String
                && dispositivo.GetString() is { Length: <= 64 } valorDispositivo)
            {
                return new LecturaPantalla(new PantallaGuardada(valorId, valorDispositivo), false);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Ilegible: el principal, con el aviso.
        }

        return new LecturaPantalla(null, true);
    }

    public void Guardar(MonitorDelPuesto monitor)
    {
        Directory.CreateDirectory(_carpeta);
        var temporal = Ruta + ".tmp";
        try
        {
            File.WriteAllText(temporal, JsonSerializer.Serialize(new
            {
                id = monitor.Id,
                dispositivo = monitor.Dispositivo,
                area = new { izquierda = monitor.Area.Izquierda, arriba = monitor.Area.Arriba, derecha = monitor.Area.Derecha, abajo = monitor.Area.Abajo },
            }));
            File.Move(temporal, Ruta, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporal);
            }
            catch (Exception borrado) when (borrado is IOException or UnauthorizedAccessException)
            {
                // Sin escritura tampoco se puede borrar: queda un temporal que Leer nunca mira.
            }

            throw;
        }
    }
}

/// <summary>
/// El monitor entre la pantalla y la ventana (D10): la ventana dice si el guardado no estaba al abrir, y la pantalla le pide
/// pasar a otro monitor al guardar. En bUnit no hay ventana: la prueba sólo anota los pedidos.
/// </summary>
public sealed class PantallaDelPuesto
{
    private readonly Action<MonitorDelPuesto> _mover;

    public PantallaDelPuesto(Action<MonitorDelPuesto> mover)
    {
        _mover = mover;
    }

    /// <summary>Por qué el puesto no abrió en el monitor guardado; null si abrió en él o no había nada guardado.</summary>
    public string? Aviso { get; set; }

    public void Mover(MonitorDelPuesto monitor) => _mover(monitor);
}
