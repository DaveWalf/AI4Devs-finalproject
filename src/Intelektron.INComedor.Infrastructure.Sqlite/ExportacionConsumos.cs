using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Intelektron.INComedor.Domain;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Los formatos de la exportación de consumos (D6 de buscar-y-exportar-consumos).</summary>
public enum FormatoExportacion
{
    Csv,
    Xlsx,
}

/// <summary>
/// El archivo escrito, sólo su nombre y nunca la ruta, las filas que tiene y si la consulta tenía más que el tope; o el
/// motivo por el que el filtro se rechazó sin escribir nada.
/// </summary>
public sealed record ResultadoExportacion(string? Archivo, int Filas, bool HayMas, string? Rechazo);

/// <summary>
/// La exportación de la consulta de consumos (consulta-consumos de buscar-y-exportar-consumos, D6): lee los consumos del
/// filtro aplicado, con su orden y hasta 10 000, y los escribe con las columnas de la tabla, sin ningún código, a un
/// temporal en la carpeta elegida que después renombra, así que un error no deja un archivo a medias ni pisa uno que ya
/// estaba. CSV en UTF-8 con BOM, separado por punto y coma y con comillas, como lo abre Excel en español; una celda de texto
/// que empieza con <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tabulador o retorno lleva un apóstrofo delante (OWASP, inyección
/// de fórmulas). Excel, un libro mínimo escrito con <see cref="ZipArchive"/>: una hoja con cadenas en línea, que nunca son
/// fórmulas, y el cargo como número. La carpeta tiene que existir: no se crea.
/// </summary>
public sealed class ExportacionConsumos
{
    public const int Tope = 10_000;

    private static readonly CultureInfo EnEspanol = CultureInfo.GetCultureInfo("es-AR");

    private static readonly string[] Encabezado = ["Fecha y hora", "N.º", "Persona", "Opción", "Servicio", "Cargo", "Moneda", "Estado"];

    private readonly ConsultaConsumos _consulta;

    public ExportacionConsumos(ConsultaConsumos consulta)
    {
        _consulta = consulta;
    }

    /// <summary>
    /// Escribe el archivo y devuelve su nombre. Sin poder escribir en <paramref name="carpeta"/>, lanza la excepción de E/S
    /// después de borrar el temporal; lo que ya estaba en la carpeta no se toca.
    /// </summary>
    public ResultadoExportacion Exportar(FiltroConsumos filtro, FormatoExportacion formato, string carpeta, TimeZoneInfo zona)
    {
        var resultado = _consulta.Leer(filtro, Tope);
        if (resultado.Rechazo is { } rechazo)
        {
            return new ResultadoExportacion(null, 0, false, rechazo);
        }

        var ahora = TimeZoneInfo.ConvertTime(_consulta.Ahora(), zona);
        var nombre = "INComedor-consumos-" + ahora.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            + (formato == FormatoExportacion.Csv ? ".csv" : ".xlsx");
        var destino = Path.Combine(carpeta, nombre);
        var temporal = destino + ".tmp";
        try
        {
            using (var flujo = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var filas = resultado.Filas.Select(f => Fila(f, zona));
                if (formato == FormatoExportacion.Csv)
                {
                    EscribirCsv(flujo, filas);
                }
                else
                {
                    EscribirXlsx(flujo, filas);
                }
            }

            File.Move(temporal, destino, overwrite: false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            BorrarTemporal(temporal);
            throw;
        }

        return new ResultadoExportacion(nombre, resultado.Filas.Count, resultado.HayMas, null);
    }

    /// <summary>El estado del comprobante como lo dice la tabla de Consumos.</summary>
    public static string TextoEstado(EstadoTrabajoImpresion? estado) => estado switch
    {
        EstadoTrabajoImpresion.Submitted => "Enviado",
        EstadoTrabajoImpresion.Failed => "Falló",
        EstadoTrabajoImpresion.Unknown => "Incierto",
        null => "—",
        _ => "En cola",
    };

    /// <summary>Una fila exportada: los textos de la tabla y el cargo como número, en las unidades de su moneda.</summary>
    private sealed record FilaExportada(string FechaHora, string Numero, string Persona, string Opcion, string Servicio, decimal Cargo, int Escala,
        string Moneda, string Estado);

    private static FilaExportada Fila(ConsumoConsultado consumo, TimeZoneInfo zona)
    {
        var local = TimeZoneInfo.ConvertTime(consumo.ConfirmadoUtc, zona);
        var divisor = 1m;
        for (var i = 0; i < consumo.Escala; i++)
        {
            divisor *= 10;
        }

        return new FilaExportada(local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), consumo.Numero ?? string.Empty, consumo.Persona,
            consumo.Opcion, consumo.Servicio, consumo.CargoUnidadesMenores / divisor, consumo.Escala, consumo.Moneda,
            TextoEstado(consumo.EstadoComprobante));
    }

    private static void EscribirCsv(Stream flujo, IEnumerable<FilaExportada> filas)
    {
        using var escritor = new StreamWriter(flujo, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        escritor.NewLine = "\r\n";
        escritor.WriteLine(string.Join(';', Encabezado.Select(Comillas)));
        foreach (var fila in filas)
        {
            string[] celdas =
            [
                fila.FechaHora, SinFormula(fila.Numero), SinFormula(fila.Persona), SinFormula(fila.Opcion), SinFormula(fila.Servicio),
                fila.Cargo.ToString("F" + fila.Escala.ToString(CultureInfo.InvariantCulture), EnEspanol), SinFormula(fila.Moneda), fila.Estado,
            ];
            escritor.WriteLine(string.Join(';', celdas.Select(Comillas)));
        }
    }

    /// <summary>Un texto que una planilla tomaría como fórmula lleva un apóstrofo delante (OWASP, inyección de fórmulas).</summary>
    private static string SinFormula(string texto) =>
        texto.Length > 0 && texto[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + texto : texto;

    private static string Comillas(string texto) => "\"" + texto.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private const string Principal = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Relaciones = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static void EscribirXlsx(Stream flujo, IEnumerable<FilaExportada> filas)
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
            <workbook xmlns="{Principal}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Consumos" sheetId="1" r:id="rId1"/></sheets></workbook>
            """);
        Parte(libro, "xl/_rels/workbook.xml.rels",
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="{Relaciones}"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>
            """);

        using var hoja = libro.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal).Open();
        using var xml = XmlWriter.Create(hoja, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        xml.WriteStartDocument(standalone: true);
        xml.WriteStartElement("worksheet", Principal);
        xml.WriteStartElement("sheetData", Principal);
        var numero = 1;
        EscribirFila(xml, numero, Encabezado.Select(t => (object)t).ToArray());
        foreach (var fila in filas)
        {
            EscribirFila(xml, ++numero, [fila.FechaHora, fila.Numero, fila.Persona, fila.Opcion, fila.Servicio, fila.Cargo, fila.Moneda, fila.Estado]);
        }

        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndDocument();
    }

    private static void EscribirFila(XmlWriter xml, int numero, object[] celdas)
    {
        var fila = numero.ToString(CultureInfo.InvariantCulture);
        xml.WriteStartElement("row", Principal);
        xml.WriteAttributeString("r", fila);
        for (var i = 0; i < celdas.Length; i++)
        {
            xml.WriteStartElement("c", Principal);
            xml.WriteAttributeString("r", (char)('A' + i) + fila);
            if (celdas[i] is decimal valor)
            {
                xml.WriteElementString("v", Principal, valor.ToString(CultureInfo.InvariantCulture));
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

    /// <summary>Sin los caracteres de control que XML no admite, que de otro modo cortarían la escritura del libro.</summary>
    private static string SoloCaracteresXml(string texto) =>
        texto.All(XmlConvert.IsXmlChar) ? texto : string.Concat(texto.Where(XmlConvert.IsXmlChar));

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
