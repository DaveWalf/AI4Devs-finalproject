using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Una planilla que no se puede leer, con su motivo: NOT_A_WORKBOOK, NO_SHEETS, TOO_LARGE o TOO_MANY_ROWS.</summary>
public sealed class PlanillaInvalidaException : Exception
{
    public const string NoEsUnLibro = "NOT_A_WORKBOOK";
    public const string SinHojas = "NO_SHEETS";
    public const string Demasiado = "TOO_LARGE";
    public const string DemasiadasFilas = "TOO_MANY_ROWS";

    public PlanillaInvalidaException(string motivo)
        : base(motivo)
    {
        Motivo = motivo;
    }

    public string Motivo { get; }
}

/// <summary>
/// Lee la primera hoja de un <c>.xlsx</c> como filas de texto (D1 de importar-personas-desde-excel): las cadenas
/// compartidas (como guarda Excel), en línea (como escribe el puesto), el texto de una fórmula y los números en cultura
/// invariante, sin «.0» si son enteros. Cada celda va a su columna por su referencia («C4») aunque falten otras, y cada fila
/// a su lugar por su número. Sin DTD ni resolución externa (OWASP, XXE); rechaza un archivo de más de 5 MB, una parte de más
/// de 20 MB descomprimida (una bomba de zip) y más de 2 000 filas además del encabezado.
/// </summary>
public static class LectorPlanilla
{
    public const long TamanoMaximo = 5L * 1024 * 1024;
    public const long ParteMaxima = 20L * 1024 * 1024;
    public const int FilasMaximas = 2000;

    private static readonly XNamespace Principal = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relacion = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Paquete = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static IReadOnlyList<IReadOnlyList<string>> Leer(Stream flujo)
    {
        if (flujo.CanSeek && flujo.Length > TamanoMaximo)
        {
            throw new PlanillaInvalidaException(PlanillaInvalidaException.Demasiado);
        }

        ZipArchive libro;
        try
        {
            libro = new ZipArchive(flujo, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            throw new PlanillaInvalidaException(PlanillaInvalidaException.NoEsUnLibro);
        }

        using (libro)
        {
            if (libro.Entries.Any(e => e.Length > ParteMaxima))
            {
                throw new PlanillaInvalidaException(PlanillaInvalidaException.Demasiado);
            }

            var cuaderno = Parte(libro, "xl/workbook.xml") ?? throw new PlanillaInvalidaException(PlanillaInvalidaException.NoEsUnLibro);
            var primera = cuaderno.Descendants(Principal + "sheet").FirstOrDefault()
                ?? throw new PlanillaInvalidaException(PlanillaInvalidaException.SinHojas);
            var id = (string?)primera.Attribute(Relacion + "id");
            var destino = Parte(libro, "xl/_rels/workbook.xml.rels")?.Descendants(Paquete + "Relationship")
                .FirstOrDefault(r => (string?)r.Attribute("Id") == id)?.Attribute("Target")?.Value ?? "worksheets/sheet1.xml";
            var ruta = destino.StartsWith('/') ? destino.TrimStart('/') : "xl/" + destino;
            var hoja = Parte(libro, ruta) ?? throw new PlanillaInvalidaException(PlanillaInvalidaException.SinHojas);
            var cadenas = Parte(libro, "xl/sharedStrings.xml")?.Root?.Elements(Principal + "si")
                .Select(si => string.Concat(si.Descendants(Principal + "t").Select(t => t.Value))).ToList() ?? [];

            var filas = new List<IReadOnlyList<string>>();
            foreach (var fila in hoja.Descendants(Principal + "row"))
            {
                var numero = int.TryParse((string?)fila.Attribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var r) ? r : filas.Count + 1;
                if (numero > FilasMaximas + 1)
                {
                    throw new PlanillaInvalidaException(PlanillaInvalidaException.DemasiadasFilas);
                }

                while (filas.Count < numero - 1)
                {
                    filas.Add([]);
                }

                var celdas = new List<string>();
                foreach (var celda in fila.Elements(Principal + "c"))
                {
                    var columna = Columna((string?)celda.Attribute("r")) ?? celdas.Count;
                    while (celdas.Count <= columna)
                    {
                        celdas.Add(string.Empty);
                    }

                    celdas[columna] = Valor(celda, cadenas);
                }

                filas.Add(celdas);
            }

            return filas;
        }
    }

    private static string Valor(XElement celda, List<string> cadenas)
    {
        var valor = celda.Element(Principal + "v")?.Value ?? string.Empty;
        switch ((string?)celda.Attribute("t"))
        {
            case "s":
                return int.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i < cadenas.Count ? cadenas[i] : string.Empty;
            case "inlineStr":
                return string.Concat(celda.Descendants(Principal + "t").Select(t => t.Value));
            case "str" or "e" or "b":
                return valor;
            default:
                return decimal.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var numero)
                    ? numero == decimal.Truncate(numero) ? decimal.Truncate(numero).ToString("0", CultureInfo.InvariantCulture) : numero.ToString(CultureInfo.InvariantCulture)
                    : valor;
        }
    }

    /// <summary>La columna desde 0 de una referencia («C4» es 2), o null sin referencia.</summary>
    private static int? Columna(string? referencia)
    {
        if (string.IsNullOrEmpty(referencia) || !char.IsAsciiLetter(referencia[0]))
        {
            return null;
        }

        var columna = 0;
        foreach (var letra in referencia.TakeWhile(char.IsAsciiLetter))
        {
            columna = (columna * 26) + (char.ToUpperInvariant(letra) - 'A' + 1);
        }

        return columna - 1;
    }

    private static XDocument? Parte(ZipArchive libro, string nombre)
    {
        var entrada = libro.GetEntry(nombre);
        if (entrada is null)
        {
            return null;
        }

        try
        {
            using var flujo = entrada.Open();
            using var lector = XmlReader.Create(flujo, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(lector);
        }
        catch (Exception e) when (e is XmlException or InvalidDataException)
        {
            throw new PlanillaInvalidaException(PlanillaInvalidaException.NoEsUnLibro);
        }
    }
}
