using System.IO;
using System.Text.Json;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// El modo simulación en <c>simulacion.json</c> de la carpeta de datos, con la escritura atómica del modo del puesto (D4 de
/// simular-identificacion-por-lista). Sin archivo, ilegible o con otro valor, apagado: una carpeta nueva nunca muestra la
/// lista de personas. Lo guardado no basta para mostrarla: la pantalla la activa sólo en mantenimiento (D5).
/// </summary>
public sealed class AlmacenSimulacion
{
    private const string Nombre = "simulacion.json";
    private const string Encendida = "ENCENDIDA";
    private const string Apagada = "APAGADA";

    private readonly string _carpeta;

    public AlmacenSimulacion(string carpetaDatos)
    {
        _carpeta = carpetaDatos;
    }

    private string Ruta => Path.Combine(_carpeta, Nombre);

    public bool Leer()
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(Ruta));
            var raiz = documento.RootElement;
            return raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("simulacion", out var valor)
                && valor.ValueKind == JsonValueKind.String && valor.GetString() == Encendida;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Sin archivo, o ilegible: apagada.
            return false;
        }
    }

    public void Guardar(bool encendida)
    {
        Directory.CreateDirectory(_carpeta);
        var temporal = Ruta + ".tmp";
        try
        {
            File.WriteAllText(temporal, JsonSerializer.Serialize(new { simulacion = encendida ? Encendida : Apagada }));
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
