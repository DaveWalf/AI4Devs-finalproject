using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>
/// Hash del contenido de una intención (D8): SHA-256 de un JSON con las propiedades en orden ordinal y sin
/// espacios. Para esta forma restringida (ASCII, sin decimales ni anidamiento) coincide con RFC 8785.
/// </summary>
internal static class HashIntencion
{
    public const string Esquema = "ConsumptionIntent/1";

    public static byte[] Calcular(SolicitudConfirmacion solicitud)
    {
        using var flujo = new MemoryStream();
        using (var escritor = new Utf8JsonWriter(flujo))
        {
            escritor.WriteStartObject();
            escritor.WriteString("IdentitySessionId", solicitud.IdentitySessionId.ToString("D"));
            escritor.WriteString("MenuItemId", solicitud.MenuItemId.ToString("D"));
            escritor.WriteNumber("MenuVersion", solicitud.MenuVersion);
            escritor.WriteString("PersonId", solicitud.PersonId.ToString("D"));
            escritor.WriteNumber("Quantity", solicitud.Quantity);
            escritor.WriteEndObject();
        }

        return SHA256.HashData(flujo.ToArray());
    }

    public static byte[] DeTexto(string contenido) => SHA256.HashData(Encoding.UTF8.GetBytes(contenido));
}
