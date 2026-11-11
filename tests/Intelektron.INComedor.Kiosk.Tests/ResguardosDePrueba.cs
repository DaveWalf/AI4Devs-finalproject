using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Resguardos alterados para las pruebas de resguardar-configuracion: otro .zip con las partes de uno válido cambiadas, y
/// la lectura de una parte.
/// </summary>
internal static class ResguardosDePrueba
{
    public static byte[] Leer(ZipArchive zip, string nombre)
    {
        using var flujo = zip.GetEntry(nombre)!.Open();
        using var memoria = new MemoryStream();
        flujo.CopyTo(memoria);
        return memoria.ToArray();
    }

    /// <summary>
    /// Escribe en <paramref name="ruta"/> otro .zip con las partes del válido cambiadas; con <paramref name="recalcular"/>, el
    /// manifiesto se rehace con sus tamaños y huellas, como lo haría quien altera el archivo a sabiendas.
    /// </summary>
    public static string Rehacer(
        string valido, string ruta, Action<Dictionary<string, byte[]>> cambiar, Action<JsonObject>? manifiesto = null, bool recalcular = true)
    {
        var partes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using (var zip = ZipFile.OpenRead(valido))
        {
            foreach (var entrada in zip.Entries)
            {
                partes[entrada.FullName] = Leer(zip, entrada.FullName);
            }
        }

        var original = JsonNode.Parse(partes["manifiesto.json"])!.AsObject();
        partes.Remove("manifiesto.json");
        cambiar(partes);
        if (recalcular)
        {
            original["partes"] = new JsonArray(partes.Select(p => (JsonNode)new JsonObject
            {
                ["nombre"] = p.Key,
                ["tamano"] = p.Value.LongLength,
                ["sha256"] = Convert.ToHexString(SHA256.HashData(p.Value)),
            }).ToArray());
        }

        manifiesto?.Invoke(original);
        using (var zip = ZipFile.Open(ruta, ZipArchiveMode.Create))
        {
            foreach (var (parte, contenido) in partes.Prepend(new("manifiesto.json", Encoding.UTF8.GetBytes(original.ToJsonString()))))
            {
                using var flujo = zip.CreateEntry(parte, CompressionLevel.Optimal).Open();
                flujo.Write(contenido);
            }
        }

        return ruta;
    }
}
