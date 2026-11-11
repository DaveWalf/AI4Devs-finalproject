using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Los datos del comprobante que son del equipo (D3 de enriquecer-comprobante): el número y el nombre del puesto, si se
/// guardaron, y la numeración, el código de siempre o un correlativo desde un número.
/// </summary>
public sealed record DatosPuesto(int? Numero, string Nombre, bool Correlativo, long Desde)
{
    public const int NumeroMaximo = 999;
    public const int LargoNombre = 30;
    public const long DesdeMaximo = 999_999_999;

    public static readonly DatosPuesto PorDefecto = new(null, string.Empty, false, 1);

    /// <summary>«Puesto 3 · Comedor Norte», lo que haya de los dos, o null sin ninguno.</summary>
    public string? Linea
    {
        get
        {
            if (Numero is not { } numero)
            {
                return Nombre.Length > 0 ? Nombre : null;
            }

            var puesto = "Puesto " + numero.ToString(CultureInfo.InvariantCulture);
            return Nombre.Length > 0 ? puesto + " · " + Nombre : puesto;
        }
    }

    /// <summary>
    /// Los campos que no tienen forma: Numero (1 a 999, o ninguno), Nombre (hasta 30 caracteres, sin caracteres de control,
    /// o ninguno) y Desde (1 a 999.999.999).
    /// </summary>
    public static IReadOnlyList<string> Validar(int? numero, string? nombre, long desde)
    {
        var campos = new List<string>();
        if (numero is < 1 or > NumeroMaximo)
        {
            campos.Add("Numero");
        }

        if (nombre is null || nombre.Length > LargoNombre
            || nombre.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator))
        {
            campos.Add("Nombre");
        }

        if (desde is < 1 or > DesdeMaximo)
        {
            campos.Add("Desde");
        }

        return campos;
    }
}

/// <summary>
/// <c>puesto.json</c> de la carpeta de datos, validado al leer y al guardar, con escritura atómica; un archivo ilegible o
/// fuera de forma da los valores por defecto (D3). No entra en el resguardo: es del equipo.
/// </summary>
public sealed class AlmacenDatosPuesto
{
    private const string Nombre = "puesto.json";

    private readonly string _carpeta;
    private DatosPuesto? _ultimo;

    public AlmacenDatosPuesto(string carpetaDatos)
    {
        _carpeta = carpetaDatos;
    }

    private string Ruta => Path.Combine(_carpeta, Nombre);

    /// <summary>
    /// Lo guardado; sin archivo, o con uno fuera de forma, los valores por defecto. Si el archivo existe pero no se puede
    /// leer en ese instante (un antivirus que lo tiene tomado), lo último que se leyó o se guardó: un comprobante no sale
    /// con el código de siempre en medio de la serie correlativa.
    /// </summary>
    public DatosPuesto Leer()
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(Ruta));
            var raiz = documento.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object
                && raiz.TryGetProperty("numero", out var numero) && (numero.ValueKind == JsonValueKind.Null || numero.ValueKind == JsonValueKind.Number && numero.TryGetInt32(out _))
                && raiz.TryGetProperty("nombre", out var nombre) && nombre.ValueKind == JsonValueKind.String
                && raiz.TryGetProperty("correlativo", out var correlativo) && correlativo.ValueKind is JsonValueKind.True or JsonValueKind.False
                && raiz.TryGetProperty("desde", out var desde) && desde.ValueKind == JsonValueKind.Number && desde.TryGetInt64(out var inicio))
            {
                int? puesto = numero.ValueKind == JsonValueKind.Null ? null : numero.GetInt32();
                if (DatosPuesto.Validar(puesto, nombre.GetString(), inicio).Count == 0)
                {
                    return _ultimo = new DatosPuesto(puesto, nombre.GetString()!, correlativo.GetBoolean(), inicio);
                }
            }
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or JsonException)
        {
            // Sin archivo, o ilegible: los valores por defecto.
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Existe pero no se puede leer ahora: lo último que se leyó o se guardó.
            return _ultimo ?? DatosPuesto.PorDefecto;
        }

        return DatosPuesto.PorDefecto;
    }

    public void Guardar(DatosPuesto datos)
    {
        if (DatosPuesto.Validar(datos.Numero, datos.Nombre, datos.Desde).Count > 0)
        {
            throw new ArgumentException("Los datos del puesto no tienen forma.", nameof(datos));
        }

        Directory.CreateDirectory(_carpeta);
        var temporal = Ruta + ".tmp";
        try
        {
            File.WriteAllText(temporal, JsonSerializer.Serialize(new
            {
                numero = datos.Numero, nombre = datos.Nombre, correlativo = datos.Correlativo, desde = datos.Desde,
            }));
            File.Move(temporal, Ruta, overwrite: true);
            _ultimo = datos;
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
