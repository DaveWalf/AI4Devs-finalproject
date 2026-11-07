using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// El encabezado y el pie del comprobante (D13 de configurar-puesto-por-pestanas): son de la impresora, no del comprobante
/// guardado, así que se aplican desde el próximo que sale, también a una copia. Sin textos guardados, el encabezado de
/// siempre y ningún pie.
/// </summary>
public sealed record TextosComprobante(string Encabezado, string Pie)
{
    public const string EncabezadoPorDefecto = "INComedor · comprobante";
    public const int LargoEncabezado = 40;
    public const int LargoPie = 60;

    public static readonly TextosComprobante PorDefecto = new(EncabezadoPorDefecto, string.Empty);

    /// <summary>Los campos que no tienen forma: el encabezado de 1 a 40 caracteres y el pie de 0 a 60, sin caracteres de control.</summary>
    public static IReadOnlyList<string> Validar(string? encabezado, string? pie)
    {
        var campos = new List<string>();
        if (string.IsNullOrWhiteSpace(encabezado) || encabezado.Length > LargoEncabezado || ConControl(encabezado))
        {
            campos.Add("Encabezado");
        }

        if (pie is null || pie.Length > LargoPie || ConControl(pie))
        {
            campos.Add("Pie");
        }

        return campos;
    }

    private static bool ConControl(string texto) =>
        texto.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator);
}

/// <summary>El resultado de guardar los textos: los campos rechazados o el aviso de que no se pudo escribir.</summary>
public sealed record ResultadoTextos(bool Guardado, IReadOnlyList<string> Campos, string? Aviso);

/// <summary>
/// Los textos en <c>impresora.json</c> de la carpeta de datos, validados al leer y al guardar, con la escritura atómica del
/// estilo (D13). Un archivo ilegible o fuera de forma da los textos por defecto.
/// </summary>
public sealed class AlmacenImpresora
{
    private const string Nombre = "impresora.json";

    private readonly string _carpeta;

    public AlmacenImpresora(string carpetaDatos)
    {
        _carpeta = carpetaDatos;
    }

    private string Ruta => Path.Combine(_carpeta, Nombre);

    public TextosComprobante Leer()
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(Ruta));
            var raiz = documento.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object
                && raiz.TryGetProperty("encabezado", out var encabezado) && encabezado.ValueKind == JsonValueKind.String
                && raiz.TryGetProperty("pie", out var pie) && pie.ValueKind == JsonValueKind.String
                && TextosComprobante.Validar(encabezado.GetString(), pie.GetString()).Count == 0)
            {
                return new TextosComprobante(encabezado.GetString()!, pie.GetString()!);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Sin archivo, o ilegible: los textos por defecto.
        }

        return TextosComprobante.PorDefecto;
    }

    public void Guardar(TextosComprobante textos)
    {
        if (TextosComprobante.Validar(textos.Encabezado, textos.Pie).Count > 0)
        {
            throw new ArgumentException("Los textos del comprobante no tienen forma.", nameof(textos));
        }

        Directory.CreateDirectory(_carpeta);
        var temporal = Ruta + ".tmp";
        try
        {
            File.WriteAllText(temporal, JsonSerializer.Serialize(new { encabezado = textos.Encabezado, pie = textos.Pie }));
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
