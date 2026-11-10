using System.IO;
using System.Text.Json;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// El modo simulación en <c>simulacion.json</c> de la carpeta de datos, con la escritura atómica del modo del puesto (D4 de
/// simular-identificacion-por-lista). Sólo "APAGADA" la apaga: sin archivo, ilegible o con otro valor, encendido, su valor
/// por omisión desde la ampliación del 10/11 (D12). Lo guardado no basta para mostrar la lista: la pantalla la activa sólo
/// en mantenimiento (D5).
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
            return !(raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("simulacion", out var valor)
                && valor.ValueKind == JsonValueKind.String && valor.GetString() == Apagada);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Sin archivo, o ilegible: encendida, el valor por omisión.
            return true;
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
