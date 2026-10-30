using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Intelektron.INComedor.Contracts;

// JSON canónico RFC 8785 para la forma que usa el contrato: enteros dentro de ±(2^53 − 1), sin
// decimales ni exponente (D4). El receptor hashea con esta forma el lote y cada evento; el puesto envía
// el texto canónico mismo como cuerpo, así el hash que guarda es el de los bytes que envió.
public static class JsonCanonico
{
    private const long MayorEnteroExacto = 9007199254740991;

    private static readonly JsonDocumentOptions Opciones = new() { AllowDuplicateProperties = false };

    /// <summary>Lee un texto JSON UTF-8 y devuelve su forma canónica en UTF-8.</summary>
    /// <exception cref="JsonCanonicoException">No es JSON, repite una propiedad o trae un número que el contrato no usa.</exception>
    public static byte[] Canonicalizar(ReadOnlySpan<byte> json)
    {
        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(json.ToArray(), Opciones);
        }
        catch (JsonException ex)
        {
            throw new JsonCanonicoException("El texto no es JSON válido o repite una propiedad.", ex);
        }

        using (documento)
        {
            return Canonicalizar(documento.RootElement);
        }
    }

    /// <summary>Forma canónica de un elemento ya leído, por ejemplo un evento dentro del lote.</summary>
    /// <exception cref="JsonCanonicoException">Repite una propiedad o trae un número que el contrato no usa.</exception>
    public static byte[] Canonicalizar(JsonElement elemento)
    {
        var salida = new ArrayBufferWriter<byte>();
        Escribir(elemento, salida);
        return salida.WrittenSpan.ToArray();
    }

    private static void Escribir(JsonElement elemento, ArrayBufferWriter<byte> salida)
    {
        switch (elemento.ValueKind)
        {
            case JsonValueKind.Object:
                var propiedades = elemento.EnumerateObject().ToList();
                propiedades.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                Ascii(salida, "{");
                for (var i = 0; i < propiedades.Count; i++)
                {
                    if (i > 0)
                    {
                        if (propiedades[i].Name == propiedades[i - 1].Name)
                        {
                            throw new JsonCanonicoException("El objeto repite una propiedad.");
                        }

                        Ascii(salida, ",");
                    }

                    EscribirCadena(propiedades[i].Name, salida);
                    Ascii(salida, ":");
                    Escribir(propiedades[i].Value, salida);
                }

                Ascii(salida, "}");
                break;

            case JsonValueKind.Array:
                Ascii(salida, "[");
                var primero = true;
                foreach (var item in elemento.EnumerateArray())
                {
                    if (!primero)
                    {
                        Ascii(salida, ",");
                    }

                    Escribir(item, salida);
                    primero = false;
                }

                Ascii(salida, "]");
                break;

            case JsonValueKind.String:
                string? valor;
                try
                {
                    valor = elemento.GetString();
                }
                catch (InvalidOperationException ex)
                {
                    throw new JsonCanonicoException("La cadena no es UTF-16 válido.", ex);
                }

                EscribirCadena(valor!, salida);
                break;

            case JsonValueKind.Number:
                var crudo = elemento.GetRawText();
                if (crudo.AsSpan().IndexOfAny('.', 'e', 'E') >= 0
                    || !elemento.TryGetInt64(out var entero)
                    || entero is > MayorEnteroExacto or < -MayorEnteroExacto)
                {
                    throw new JsonCanonicoException("El contrato sólo usa enteros dentro de ±(2^53 − 1), sin decimales ni exponente.");
                }

                Ascii(salida, entero.ToString(CultureInfo.InvariantCulture));
                break;

            case JsonValueKind.True:
                Ascii(salida, "true");
                break;

            case JsonValueKind.False:
                Ascii(salida, "false");
                break;

            case JsonValueKind.Null:
                Ascii(salida, "null");
                break;

            default:
                throw new JsonCanonicoException("Elemento JSON indefinido.");
        }
    }

    // Escape mínimo de RFC 8785 §3.2.2.2: comilla, barra invertida y los controles; los cinco con
    // escape corto lo usan, el resto de U+0000 a U+001F va en hexadecimal minúscula, y todo lo demás
    // queda literal en UTF-8. Una sustituta sin pareja no es I-JSON.
    private static void EscribirCadena(string valor, ArrayBufferWriter<byte> salida)
    {
        var texto = new StringBuilder(valor.Length + 2);
        texto.Append('"');
        for (var i = 0; i < valor.Length; i++)
        {
            var c = valor[i];
            switch (c)
            {
                case '"': texto.Append("\\\""); break;
                case '\\': texto.Append("\\\\"); break;
                case '\b': texto.Append("\\b"); break;
                case '\t': texto.Append("\\t"); break;
                case '\n': texto.Append("\\n"); break;
                case '\f': texto.Append("\\f"); break;
                case '\r': texto.Append("\\r"); break;
                default:
                    if (c < ' ')
                    {
                        texto.Append('\\').Append('u').Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else if (char.IsHighSurrogate(c) && i + 1 < valor.Length && char.IsLowSurrogate(valor[i + 1]))
                    {
                        texto.Append(c).Append(valor[i + 1]);
                        i++;
                    }
                    else if (char.IsSurrogate(c))
                    {
                        throw new JsonCanonicoException("La cadena tiene una sustituta UTF-16 sin pareja.");
                    }
                    else
                    {
                        texto.Append(c);
                    }

                    break;
            }
        }

        texto.Append('"');
        salida.Write(Encoding.UTF8.GetBytes(texto.ToString()));
    }

    private static void Ascii(ArrayBufferWriter<byte> salida, string texto) =>
        salida.Write(Encoding.ASCII.GetBytes(texto));
}

/// <summary>Un texto que no tiene forma canónica en el contrato: no es JSON, repite una propiedad o trae un número fuera de lo que el contrato usa.</summary>
public sealed class JsonCanonicoException : Exception
{
    public JsonCanonicoException(string mensaje) : base(mensaje)
    {
    }

    public JsonCanonicoException(string mensaje, Exception interna) : base(mensaje, interna)
    {
    }
}
