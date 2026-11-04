using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// La opacidad de las superficies, en por ciento, y el desenfoque del fondo, en px (D5 de presentar-atencion-tactil). Es
/// presentación local del puesto, no configuración que llegue por snapshot.
/// </summary>
public sealed record EstiloVisual(int Opacidad, int Desenfoque)
{
    public const int OpacidadMinima = 72;
    public const int OpacidadMaxima = 88;
    public const int DesenfoqueMinimo = 0;
    public const int DesenfoqueMaximo = 12;

    public static readonly EstiloVisual PorDefecto = new(76, 4);

    public bool EnRango =>
        Opacidad is >= OpacidadMinima and <= OpacidadMaxima && Desenfoque is >= DesenfoqueMinimo and <= DesenfoqueMaximo;

    /// <summary>Las dos variables CSS que usa puesto.css, con punto decimal sea cual sea la cultura.</summary>
    public string VariablesCss =>
        string.Create(CultureInfo.InvariantCulture, $"--opacidad-superficie: {Opacidad / 100m:0.00}; --desenfoque-fondo: {Desenfoque}px");
}

/// <summary>
/// Lee y escribe estilo-visual.json en la carpeta de datos (D5). Escribe a un archivo temporal y lo mueve encima del
/// anterior, para no dejar uno a medias. Si el archivo falta, no se puede leer o algún valor está fuera de rango,
/// devuelve 76 y 4: un archivo roto no se acota al extremo más cercano ni impide atender (OWASP A05).
/// </summary>
public sealed class AlmacenEstiloVisual
{
    private const string Nombre = "estilo-visual.json";

    private readonly string _carpeta;

    public AlmacenEstiloVisual(string carpetaDatos)
    {
        _carpeta = carpetaDatos;
    }

    private string Ruta => Path.Combine(_carpeta, Nombre);

    public EstiloVisual Leer()
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(Ruta));
            var raiz = documento.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object
                && raiz.TryGetProperty("opacidad", out var opacidad) && opacidad.ValueKind == JsonValueKind.Number && opacidad.TryGetInt32(out var o)
                && raiz.TryGetProperty("desenfoque", out var desenfoque) && desenfoque.ValueKind == JsonValueKind.Number && desenfoque.TryGetInt32(out var d)
                && new EstiloVisual(o, d) is { EnRango: true } estilo)
            {
                return estilo;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Sin archivo, o ilegible: los valores por defecto.
        }

        return EstiloVisual.PorDefecto;
    }

    public void Guardar(EstiloVisual estilo)
    {
        if (!estilo.EnRango)
        {
            throw new ArgumentOutOfRangeException(nameof(estilo), estilo, "El estilo visual está fuera de rango.");
        }

        Directory.CreateDirectory(_carpeta);
        var temporal = Ruta + ".tmp";
        File.WriteAllText(temporal, JsonSerializer.Serialize(new { opacidad = estilo.Opacidad, desenfoque = estilo.Desenfoque }));
        File.Move(temporal, Ruta, overwrite: true);
    }
}
