using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Un número de una planilla: en el CSV, con coma decimal y sus decimales; en el libro, como número.</summary>
public readonly record struct NumeroPlanilla(decimal Valor, int Decimales);

/// <summary>Una hoja de un libro: su nombre, su encabezado, sus filas y las columnas (desde 0) con formato de texto.</summary>
public sealed record HojaPlanilla(string Nombre, IReadOnlyList<string> Encabezado, IEnumerable<IReadOnlyList<object>> Filas, IReadOnlyCollection<int>? ColumnasTexto = null);

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
        string carpeta, string raiz, FormatoExportacion formato, string hoja, IReadOnlyList<string> encabezado, IEnumerable<IReadOnlyList<object>> filas) =>
        formato == FormatoExportacion.Csv
            ? EscribirArchivo(carpeta, raiz, ".csv", flujo => EscribirCsv(flujo, encabezado, filas))
            : EscribirArchivo(carpeta, raiz, ".xlsx", flujo => EscribirXlsx(flujo, [new HojaPlanilla(hoja, encabezado, filas)]));

    /// <summary>
    /// Un libro de varias hojas, siempre <c>.xlsx</c>, con columnas de texto donde se pidan: Excel les deja ese formato a las
    /// celdas que se escriban después, así que un código no pierde los ceros del principio (D6 de importar-personas-desde-excel).
    /// </summary>
    public static string EscribirLibro(string carpeta, string raiz, IReadOnlyList<HojaPlanilla> hojas) =>
        EscribirArchivo(carpeta, raiz, ".xlsx", flujo => EscribirXlsx(flujo, hojas));

    private static string EscribirArchivo(string carpeta, string raiz, string extension, Action<Stream> escribir)
    {
        var temporal = Path.Combine(carpeta, $"{raiz}-{Guid.NewGuid():N}.tmp");
        var renombrado = false;
        try
        {
            using (var flujo = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                escribir(flujo);
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

    /// <summary>
    /// Una línea CSV como las de la exportación, sin el salto: cada celda entre comillas, separadas por punto y coma, los
    /// números en es-AR y el apóstrofo delante de lo que una planilla tomaría como fórmula (D1 de exportar-consumos-automaticamente).
    /// </summary>
    public static string LineaCsv(IEnumerable<object> celdas) =>
        string.Join(';', celdas.Select(celda => Comillas(celda switch
        {
            NumeroPlanilla numero => numero.Valor.ToString("F" + numero.Decimales.ToString(CultureInfo.InvariantCulture), EnEspanol),
            _ => SinFormula((string)celda),
        })));

    private static void EscribirCsv(Stream flujo, IReadOnlyList<string> encabezado, IEnumerable<IReadOnlyList<object>> filas)
    {
        using var escritor = new StreamWriter(flujo, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        escritor.NewLine = "\r\n";
        // El encabezado también: en Personas lleva los nombres de los servicios y las categorías, que llegan de la configuración.
        escritor.WriteLine(LineaCsv(encabezado));
        foreach (var fila in filas)
        {
            escritor.WriteLine(LineaCsv(fila));
        }
    }

    /// <summary>Un texto que una planilla tomaría como fórmula lleva un apóstrofo delante (OWASP, inyección de fórmulas).</summary>
    private static string SinFormula(string texto) =>
        texto.Length > 0 && texto[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + texto : texto;

    private static string Comillas(string texto) => "\"" + texto.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static void EscribirXlsx(Stream flujo, IReadOnlyList<HojaPlanilla> hojas)
    {
        var conTexto = hojas.Any(h => h.ColumnasTexto is { Count: > 0 });
        using var libro = new ZipArchive(flujo, ZipArchiveMode.Create);
        var hojasTipos = string.Concat(hojas.Select((_, i) =>
            $"""<Override PartName="/xl/worksheets/sheet{i + 1}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>"""));
        var estilosTipo = conTexto ? """<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""" : string.Empty;
        Parte(libro, "[Content_Types].xml",
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>{hojasTipos}{estilosTipo}</Types>
            """);
        Parte(libro, "_rels/.rels",
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="{Relaciones}"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
            """);
        var hojasLibro = string.Concat(hojas.Select((h, i) => $"""<sheet name="{SecurityElementEscape(h.Nombre)}" sheetId="{i + 1}" r:id="rId{i + 1}"/>"""));
        Parte(libro, "xl/workbook.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="{Principal}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>{hojasLibro}</sheets></workbook>
            """);
        var relacionesHojas = string.Concat(hojas.Select((_, i) =>
            $"""<Relationship Id="rId{i + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i + 1}.xml"/>"""));
        var relacionEstilos = conTexto
            ? $"""<Relationship Id="rId{hojas.Count + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>"""
            : string.Empty;
        Parte(libro, "xl/_rels/workbook.xml.rels",
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="{Relaciones}">{relacionesHojas}{relacionEstilos}</Relationships>
            """);
        if (conTexto)
        {
            // El estilo 1 es el formato de texto («@», numFmtId 49); el 0, el de siempre.
            Parte(libro, "xl/styles.xml",
                $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="{Principal}"><fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="2"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="49" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>
                """);
        }

        for (var i = 0; i < hojas.Count; i++)
        {
            EscribirHoja(libro, $"xl/worksheets/sheet{i + 1}.xml", hojas[i]);
        }
    }

    private static void EscribirHoja(ZipArchive libro, string ruta, HojaPlanilla hoja)
    {
        var texto = hoja.ColumnasTexto ?? [];
        using var parte = libro.CreateEntry(ruta, CompressionLevel.Optimal).Open();
        using var xml = XmlWriter.Create(parte, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        xml.WriteStartDocument(standalone: true);
        xml.WriteStartElement("worksheet", Principal);
        if (texto.Count > 0)
        {
            // Las columnas con el ancho de su encabezado y, las de texto, con su formato para lo que se escriba después.
            xml.WriteStartElement("cols", Principal);
            for (var c = 0; c < hoja.Encabezado.Count; c++)
            {
                xml.WriteStartElement("col", Principal);
                var numero = (c + 1).ToString(CultureInfo.InvariantCulture);
                xml.WriteAttributeString("min", numero);
                xml.WriteAttributeString("max", numero);
                xml.WriteAttributeString("width", Math.Max(14, hoja.Encabezado[c].Length + 4).ToString(CultureInfo.InvariantCulture));
                xml.WriteAttributeString("customWidth", "1");
                if (texto.Contains(c))
                {
                    xml.WriteAttributeString("style", "1");
                }

                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        }

        xml.WriteStartElement("sheetData", Principal);
        var numeroFila = 1;
        EscribirFila(xml, numeroFila, hoja.Encabezado.Cast<object>().ToList(), texto);
        foreach (var fila in hoja.Filas)
        {
            EscribirFila(xml, ++numeroFila, fila, texto);
        }

        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndDocument();
    }

    private static string SecurityElementEscape(string texto) => System.Security.SecurityElement.Escape(texto) ?? string.Empty;

    private static void EscribirFila(XmlWriter xml, int numero, IReadOnlyList<object> celdas, IReadOnlyCollection<int> texto)
    {
        var fila = numero.ToString(CultureInfo.InvariantCulture);
        xml.WriteStartElement("row", Principal);
        xml.WriteAttributeString("r", fila);
        for (var i = 0; i < celdas.Count; i++)
        {
            xml.WriteStartElement("c", Principal);
            xml.WriteAttributeString("r", Columna(i) + fila);
            if (texto.Contains(i))
            {
                xml.WriteAttributeString("s", "1");
            }

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
