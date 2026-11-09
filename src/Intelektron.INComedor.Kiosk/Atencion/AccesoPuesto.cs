using System.IO;
using System.Text.Json;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Los dos modos del puesto (D5 de configurar-puesto-por-pestanas). Sólo deciden la entrada a Configuración y el cierre:
/// la atención es la misma en los dos, y la máquina no los conoce.
/// </summary>
public enum ModoPuesto
{
    Mantenimiento,
    Operativo,
}

/// <summary>
/// El modo en <c>modo-puesto.json</c> de la carpeta de datos, con la escritura atómica del estilo (D5). Sin archivo, ilegible
/// o con otro valor, mantenimiento: una carpeta nueva nunca queda trabada detrás de una clave.
/// </summary>
public sealed class AlmacenModoPuesto
{
    private const string Nombre = "modo-puesto.json";
    private const string Operativo = "OPERATIVO";
    private const string Mantenimiento = "MANTENIMIENTO";

    private readonly string _carpeta;

    public AlmacenModoPuesto(string carpetaDatos)
    {
        _carpeta = carpetaDatos;
    }

    private string Ruta => Path.Combine(_carpeta, Nombre);

    public ModoPuesto Leer()
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(Ruta));
            var raiz = documento.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("modo", out var modo)
                && modo.ValueKind == JsonValueKind.String && modo.GetString() == Operativo)
            {
                return ModoPuesto.Operativo;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Sin archivo, o ilegible: mantenimiento.
        }

        return ModoPuesto.Mantenimiento;
    }

    public void Guardar(ModoPuesto modo)
    {
        Directory.CreateDirectory(_carpeta);
        var temporal = Ruta + ".tmp";
        try
        {
            File.WriteAllText(temporal, JsonSerializer.Serialize(new { modo = modo == ModoPuesto.Operativo ? Operativo : Mantenimiento }));
            File.Move(temporal, Ruta, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // El anterior queda intacto; quien llama decide cómo avisar.
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
/// Decide si una clave del día vale para una fecha (D6). El validador real, con la tecnología de INWeb, entra por acá fuera
/// de este corte, sin copiar su biblioteca a un repo público.
/// </summary>
public interface IValidadorClaveDelDia
{
    bool Validar(string clave, DateOnly fecha);
}

/// <summary>La clave de desarrollo: cualquier número de 1 a 12 dígitos ASCII; el diálogo lo avisa en pantalla (D6).</summary>
public sealed class ClaveDelDiaDeDesarrollo : IValidadorClaveDelDia
{
    public const int LargoMaximo = 12;

    public bool Validar(string clave, DateOnly fecha) =>
        clave is { Length: >= 1 and <= LargoMaximo } && clave.All(char.IsAsciiDigit);
}

/// <summary>
/// El cierre del puesto entre la ventana y la pantalla (D7). La pantalla dice si el modo pide la clave y, con la clave
/// aceptada o en mantenimiento, cierra; la ventana frena un pedido de cierre sin autorizar en operativo y avisa para que la
/// pantalla pida la clave. El fin de la sesión de Windows no pasa por acá.
/// </summary>
public sealed class CierrePuesto
{
    private readonly Action _cerrarVentana;

    public CierrePuesto(Action cerrarVentana)
    {
        _cerrarVentana = cerrarVentana;
    }

    /// <summary>La avisa la ventana cuando le piden cerrar en operativo sin la clave.</summary>
    public event Action? ClavePedida;

    /// <summary>Lo pone la pantalla según el modo: en operativo, un pedido de cierre sin la clave se frena.</summary>
    public bool RequiereClave { get; set; }

    public bool Autorizado { get; private set; }

    public bool DebeFrenar => RequiereClave && !Autorizado;

    public void PedirClave() => ClavePedida?.Invoke();

    /// <summary>El cierre ordenado, autorizado por la clave o por el modo de mantenimiento.</summary>
    public void Cerrar()
    {
        Autorizado = true;
        _cerrarVentana();
    }
}
