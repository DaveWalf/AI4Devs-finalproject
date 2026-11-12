using System.Net;
using System.Runtime.InteropServices;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>Un socket TCP en escucha: la dirección local, el puerto y el proceso dueño.</summary>
public sealed record Escucha(IPAddress Direccion, int Puerto, int Pid);

/// <summary>
/// Qué procesos cuelgan de un PID y qué puertos TCP escuchan (D5): CreateToolhelp32Snapshot para el árbol y
/// GetExtendedTcpTable para las escuchas de IPv4 e IPv6. Las dos son de sólo lectura y no suman dependencias.
/// </summary>
public static class ProcesosYPuertos
{
    private const uint InstantaneaDeProcesos = 0x2;
    private const int FamiliaIPv4 = 2;
    private const int FamiliaIPv6 = 23;
    private const int TablaDeEscuchasConPid = 3;
    private const uint BufferInsuficiente = 122;

    /// <summary>El PID dado y todos sus descendientes vivos, con el nombre de su ejecutable.</summary>
    public static IReadOnlyDictionary<int, string> Arbol(int raiz)
    {
        var procesos = Instantanea();
        var arbol = new Dictionary<int, string>();
        if (procesos.FirstOrDefault(p => p.Pid == raiz) is { } propio)
        {
            arbol[raiz] = propio.Nombre;
        }

        var pendientes = new Queue<int>([raiz]);
        while (pendientes.TryDequeue(out var padre))
        {
            foreach (var hijo in procesos.Where(p => p.Padre == padre && p.Pid != padre && !arbol.ContainsKey(p.Pid)))
            {
                arbol[hijo.Pid] = hijo.Nombre;
                pendientes.Enqueue(hijo.Pid);
            }
        }

        return arbol;
    }

    /// <summary>Si el PID sigue vivo con el mismo ejecutable.</summary>
    public static bool Vivo(int pid, string nombre) =>
        Instantanea().Any(p => p.Pid == pid && string.Equals(p.Nombre, nombre, StringComparison.OrdinalIgnoreCase));

    /// <summary>Todos los sockets TCP en escucha del equipo, de IPv4 y de IPv6.</summary>
    public static IReadOnlyList<Escucha> EnEscucha() => [.. Escuchas(FamiliaIPv4), .. Escuchas(FamiliaIPv6)];

    private static List<(int Pid, int Padre, string Nombre)> Instantanea()
    {
        var instantanea = CreateToolhelp32Snapshot(InstantaneaDeProcesos, 0);
        if (instantanea == new IntPtr(-1))
        {
            throw new InvalidOperationException($"CreateToolhelp32Snapshot falló con {Marshal.GetLastPInvokeError()}.");
        }

        try
        {
            var procesos = new List<(int, int, string)>();
            var entrada = new EntradaProceso { Tamano = (uint)Marshal.SizeOf<EntradaProceso>() };
            for (var hay = Process32First(instantanea, ref entrada); hay; hay = Process32Next(instantanea, ref entrada))
            {
                procesos.Add(((int)entrada.Pid, (int)entrada.PidPadre, entrada.Ejecutable));
            }

            return procesos;
        }
        finally
        {
            CloseHandle(instantanea);
        }
    }

    private static List<Escucha> Escuchas(int familia)
    {
        var tamano = 0;
        var buffer = IntPtr.Zero;
        try
        {
            uint resultado;
            while ((resultado = GetExtendedTcpTable(buffer, ref tamano, false, familia, TablaDeEscuchasConPid, 0)) == BufferInsuficiente)
            {
                Marshal.FreeHGlobal(buffer);
                buffer = Marshal.AllocHGlobal(tamano);
            }

            if (resultado != 0)
            {
                throw new InvalidOperationException($"GetExtendedTcpTable falló con {resultado}.");
            }

            // MIB_TCPTABLE_OWNER_PID y MIB_TCP6TABLE_OWNER_PID: la cantidad y después las filas, de 24 y 56 bytes. La
            // dirección y el puerto vienen en el orden de la red; el PID, en el del equipo.
            var filas = Marshal.ReadInt32(buffer);
            var (largo, desplazamientoDireccion, bytesDireccion, desplazamientoPuerto, desplazamientoPid) =
                familia == FamiliaIPv4 ? (24, 4, 4, 8, 20) : (56, 0, 16, 20, 52);
            var escuchas = new List<Escucha>(filas);
            for (var i = 0; i < filas; i++)
            {
                var fila = buffer + 4 + (i * largo);
                var direccion = new byte[bytesDireccion];
                Marshal.Copy(fila + desplazamientoDireccion, direccion, 0, bytesDireccion);
                var puerto = (Marshal.ReadByte(fila + desplazamientoPuerto) << 8) | Marshal.ReadByte(fila + desplazamientoPuerto + 1);
                escuchas.Add(new Escucha(new IPAddress(direccion), puerto, Marshal.ReadInt32(fila + desplazamientoPid)));
            }

            return escuchas;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct EntradaProceso
    {
        public uint Tamano;
        public uint Uso;
        public uint Pid;
        public IntPtr Monticulo;
        public uint Modulo;
        public uint Hilos;
        public uint PidPadre;
        public int Prioridad;
        public uint Banderas;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string Ejecutable;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint banderas, uint pid);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32First(IntPtr instantanea, ref EntradaProceso entrada);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32Next(IntPtr instantanea, ref EntradaProceso entrada);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedTcpTable(IntPtr tabla, ref int tamano, [MarshalAs(UnmanagedType.Bool)] bool ordenar, int familia, int clase, uint reservado);
}
