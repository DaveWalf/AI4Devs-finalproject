using System.IO;
using System.Text.Json;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// La carpeta de exportación de Consumos en <c>exportacion.json</c> de la carpeta de datos, con la escritura atómica de los
/// demás almacenes (D6 de buscar-y-exportar-consumos): la primera exportación la pide y las siguientes la usan sin preguntar.
/// Sin archivo, ilegible o sin una ruta absoluta, ninguna.
/// </summary>
public sealed class AlmacenExportacion
{
    private const string Nombre = "exportacion.json";

    private readonly string _carpeta;

    public AlmacenExportacion(string carpetaDatos)
    {
        _carpeta = carpetaDatos;
    }

    private string Ruta => Path.Combine(_carpeta, Nombre);

    public string? Leer()
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(Ruta));
            var raiz = documento.RootElement;
            return raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("carpeta", out var valor)
                && valor.ValueKind == JsonValueKind.String && valor.GetString() is { Length: > 0 } carpeta && Path.IsPathFullyQualified(carpeta)
                ? carpeta
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Sin archivo, o ilegible: ninguna carpeta, y la próxima exportación la pide.
            return null;
        }
    }

    public void Guardar(string carpeta)
    {
        Directory.CreateDirectory(_carpeta);
        var temporal = Ruta + ".tmp";
        try
        {
            File.WriteAllText(temporal, JsonSerializer.Serialize(new { carpeta }));
            File.Move(temporal, Ruta, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // La anterior queda intacta; quien llama decide cómo avisar.
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
