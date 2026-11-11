using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Un número de una planilla: en el CSV, con coma decimal y sus decimales; en el libro, como número.</summary>
public readonly record struct NumeroPlanilla(decimal Valor, int Decimales);

/// <summary>
/// La escritura de una planilla de la pantalla, compartida por las exportaciones de Consumos y de Personas (D6 de
/// buscar-y-exportar-consumos y D5 de editar-raciones-en-personas). Escribe a un temporal con un GUID en la carpeta elegida
/// y lo renombra al primer nombre libre del segundo (el de siempre y de <c>-2</c> a <c>-9</c>), así que un error no deja un
/// archivo a medias ni pisa uno que ya estaba. CSV en UTF-8 con BOM, separado por punto y coma y con comillas, como lo abre
/// Excel en español; una celda de texto que empieza con <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tabulador o retorno lleva
/// un apóstrofo delante (OWASP, inyección de fórmulas). Excel, un libro mínimo con <see cref="ZipArchive"/>: una hoja con
/// cadenas en línea, que nunca son fórmulas, y los números como números. La carpeta tiene que existir: no se crea.
/// </summary>
public static class EscritorPlanilla
{
    /// <summary>Cuántos nombres prueba en el mismo segundo: el de siempre y de <c>-2</c> a <c>-9</c>.</summary>
    public const int NombresPorSegundo = 9;

    private static readonly CultureInfo EnEspanol = CultureInfo.GetCultureInfo("es-AR");

    private const string Principal = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Relaciones = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>
    /// Escribe la planilla y devuelve el nombre del archivo, sin la ruta. Las celdas son <see cref="string"/> o
    /// <see cref="NumeroPlanilla"/>. Sin poder escribir, o con los nueve nombres de ese segundo ya tomados, lanza la excepción
    /// de E/S después de borrar el temporal.
    /// </summary>
    public static string Escribir(
        string carpeta, string raiz, FormatoExportacion formato, string hoja, IReadOnlyList<string> encabezado, IEnumerable<IReadOnlyList<object>> filas)
    {
        var extension = formato == FormatoExportacion.Csv ? ".csv" : ".xlsx";
        var temporal = Path.Combine(carpeta, $"{raiz}-{Guid.NewGuid():N}.tmp");
        var renombrado = false;
        try
        {
            using (var flujo = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (formato == FormatoExportacion.Csv)
                {
                    EscribirCsv(flujo, encabezado, filas);
                }
                else
                {
                    EscribirXlsx(flujo, hoja, encabezado, filas);
                }
            }

            var nombre = Renombrar(temporal, carpeta, raiz, extension);
            renombrado = true;
            return nombre;
        }
        finally
        {
            // Cualquier error, no sólo los de E/S, deja la carpeta como estaba: el temporal no queda.
            if (!renombrado)
            {
                BorrarTemporal(temporal);
            }
        }
    }

    /// <summary>
    /// El primer nombre libre del segundo, sin pisar ninguno: el renombrado sin sobrescribir falla si otro lo tomó entre la
    /// mirada y el movimiento, y entonces prueba el siguiente.
    /// </summary>
    private static string Renombrar(string temporal, string carpeta, string raiz, string extension)
    {
        for (var i = 1; i <= NombresPorSegundo; i++)
        {
            var nombre = i == 1 ? raiz + extension : $"{raiz}-{i}{extension}";
            var destino = Path.Combine(carpeta, nombre);
            if (File.Exists(destino) || Directory.Exists(destino))
            {
                continue;
            }

            try
            {
                File.Move(temporal, destino, overwrite: false);
                return nombre;
            }
            catch (IOException) when (File.Exists(destino))
            {
                // Otro lo tomó recién: el siguiente.
            }
        }

        throw new IOException($"Ya hay {NombresPorSegundo} exportaciones de ese segundo en la carpeta.");
    }

    private static void EscribirCsv(Stream flujo, IReadOnlyList<string> encabezado, IEnumerable<IReadOnlyList<object>> filas)
    {
        using var escritor = new StreamWriter(flujo, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        escritor.NewLine = "\r\n";
        // El encabezado también: en Personas lleva los nombres de los servicios y las categorías, que llegan de la configuración.
        escritor.WriteLine(string.Join(';', encabezado.Select(t => Comillas(SinFormula(t)))));
        foreach (var fila in filas)
        {
            escritor.WriteLine(string.Join(';', fila.Select(celda => Comillas(celda switch
            {
                NumeroPlanilla numero => numero.Valor.ToString("F" + numero.Decimales.ToString(CultureInfo.InvariantCulture), EnEspanol),
                _ => SinFormula((string)celda),
            }))));
        }
    }

    /// <summary>Un texto que una planilla tomaría como fórmula lleva un apóstrofo delante (OWASP, inyección de fórmulas).</summary>
    private static string SinFormula(string texto) =>
        texto.Length > 0 && texto[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + texto : texto;

    private static string Comillas(string texto) => "\"" + texto.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static void EscribirXlsx(Stream flujo, string hoja, IReadOnlyList<string> encabezado, IEnumerable<IReadOnlyList<object>> filas)
    {
        using var libro = new ZipArchive(flujo, ZipArchiveMode.Create);
        Parte(libro, "[Content_Types].xml",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
            """);
        Parte(libro, "_rels/.rels",
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="{Relaciones}"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
            """);
        Parte(libro, "xl/workbook.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="{Principal}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{SecurityElementEscape(hoja)}" sheetId="1" r:id="rId1"/></sheets></workbook>
            """);
        Parte(libro, "xl/_rels/workbook.xml.rels",
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="{Relaciones}"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>
            """);

        using var parte = libro.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal).Open();
        using var xml = XmlWriter.Create(parte, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        xml.WriteStartDocument(standalone: true);
        xml.WriteStartElement("worksheet", Principal);
        xml.WriteStartElement("sheetData", Principal);
        var numero = 1;
        EscribirFila(xml, numero, encabezado.Cast<object>().ToList());
        foreach (var fila in filas)
        {
            EscribirFila(xml, ++numero, fila);
        }

        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndDocument();
    }

    private static string SecurityElementEscape(string texto) => System.Security.SecurityElement.Escape(texto) ?? string.Empty;

    private static void EscribirFila(XmlWriter xml, int numero, IReadOnlyList<object> celdas)
    {
        var fila = numero.ToString(CultureInfo.InvariantCulture);
        xml.WriteStartElement("row", Principal);
        xml.WriteAttributeString("r", fila);
        for (var i = 0; i < celdas.Count; i++)
        {
            xml.WriteStartElement("c", Principal);
            xml.WriteAttributeString("r", Columna(i) + fila);
            if (celdas[i] is NumeroPlanilla valor)
            {
                xml.WriteElementString("v", Principal, valor.Valor.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                xml.WriteAttributeString("t", "inlineStr");
                xml.WriteStartElement("is", Principal);
                xml.WriteStartElement("t", Principal);
                xml.WriteAttributeString("xml", "space", null, "preserve");
                xml.WriteString(SoloCaracteresXml((string)celdas[i]));
                xml.WriteEndElement();
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        }

        xml.WriteEndElement();
    }

    /// <summary>La letra de la columna: A a Z y después AA, AB…, porque Personas puede tener más de 26 columnas.</summary>
    private static string Columna(int indice)
    {
        var letras = string.Empty;
        for (var i = indice + 1; i > 0; i = (i - 1) / 26)
        {
            letras = (char)('A' + ((i - 1) % 26)) + letras;
        }

        return letras;
    }

    private static string SoloCaracteresXml(string texto)
    {
        // Por Rune y no por char: un carácter fuera del plano básico (un emoji) son dos char sustitutos, que XmlConvert
        // rechaza de a uno aunque juntos sean válidos.
        var limpio = new StringBuilder(texto.Length);
        foreach (var caracter in texto.EnumerateRunes())
        {
            var valor = caracter.Value;
            if (valor is 0x9 or 0xA or 0xD || valor is >= 0x20 and <= 0xD7FF || valor is >= 0xE000 and <= 0xFFFD || valor >= 0x10000)
            {
                limpio.Append(caracter.ToString());
            }
        }

        return limpio.ToString();
    }

    private static void Parte(ZipArchive libro, string nombre, string contenido)
    {
        using var escritor = new StreamWriter(libro.CreateEntry(nombre, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        escritor.Write(contenido);
    }

    private static void BorrarTemporal(string temporal)
    {
        try
        {
            File.Delete(temporal);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Sin escritura tampoco se puede borrar: el temporal termina en .tmp y nadie lo toma por una exportación.
        }
    }
}
