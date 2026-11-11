using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Leer y escribir planillas para importar Personas (administracion-local de importar-personas-desde-excel, D1 y D6): el
/// lector entiende lo que escribe el puesto y lo que guarda Excel (cadenas compartidas, celdas que faltan), y rechaza lo que
/// no es un libro o es demasiado grande; el escritor arma un libro de varias hojas con una columna de texto.
/// </summary>
public class PlanillasTests : IDisposable
{
    private const string Principal = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private readonly string _carpeta = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "incomedor-tests", "planillas-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_carpeta, recursive: true);

    [Fact(DisplayName = "administracion-local · Importar Personas desde Excel: el lector lee un libro del puesto, con texto y números, fila por fila")]
    public void Lee_un_libro_del_puesto()
    {
        var nombre = EscritorPlanilla.Escribir(_carpeta, "libro", FormatoExportacion.Xlsx, "Personas", ["Nombre", "Código", "ALMUERZO: límite"],
            [["Ana", "0105", new NumeroPlanilla(1, 0)], ["Bruno", "0106", new NumeroPlanilla(2, 0)]]);

        var filas = Leer(Path.Combine(_carpeta, nombre));

        Assert.Equal(["Nombre", "Código", "ALMUERZO: límite"], filas[0]);
        Assert.Equal(["Ana", "0105", "1"], filas[1]);
        Assert.Equal(["Bruno", "0106", "2"], filas[2]);
    }

    [Fact(DisplayName = "administracion-local · Importar Personas desde Excel: el lector entiende las cadenas compartidas de Excel y ubica cada celda por su referencia aunque falte una")]
    public void Lee_cadenas_compartidas_y_celdas_que_faltan()
    {
        var ruta = Libro(
            """<sheetData><row r="1"><c r="A1" t="s"><v>0</v></c><c r="C1" t="s"><v>1</v></c></row><row r="3"><c r="B3"><v>7.0</v></c></row></sheetData>""",
            """<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><si><t>Nombre</t></si><si><r><t>Có</t></r><r><t>digo</t></r></si></sst>""");

        var filas = Leer(ruta);

        Assert.Equal(["Nombre", "", "Código"], filas[0]);
        Assert.Equal(["", "7"], filas[2]);
    }

    [Fact(DisplayName = "administracion-local · Un archivo que no es una planilla: un archivo que no es un zip se rechaza como NOT_A_WORKBOOK")]
    public void Un_archivo_que_no_es_un_zip()
    {
        var ruta = Path.Combine(_carpeta, "texto.xlsx");
        File.WriteAllText(ruta, "hola");

        Assert.Equal(PlanillaInvalidaException.NoEsUnLibro, Assert.Throws<PlanillaInvalidaException>(() => Leer(ruta)).Motivo);
    }

    [Fact(DisplayName = "administracion-local · Un archivo que no es una planilla: un libro sin hojas se rechaza como NO_SHEETS")]
    public void Un_libro_sin_hojas()
    {
        var ruta = Path.Combine(_carpeta, "vacio.xlsx");
        using (var libro = ZipFile.Open(ruta, ZipArchiveMode.Create))
        {
            Parte(libro, "xl/workbook.xml", $"""<workbook xmlns="{Principal}"><sheets/></workbook>""");
        }

        Assert.Equal(PlanillaInvalidaException.SinHojas, Assert.Throws<PlanillaInvalidaException>(() => Leer(ruta)).Motivo);
    }

    [Fact(DisplayName = "administracion-local · Un archivo que no es una planilla: una hoja de más de 20 MB descomprimida se rechaza como TOO_LARGE, sin leerla")]
    public void Una_hoja_demasiado_grande()
    {
        var ruta = Libro("<sheetData>" + new string(' ', 21 * 1024 * 1024) + "</sheetData>");

        Assert.Equal(PlanillaInvalidaException.Demasiado, Assert.Throws<PlanillaInvalidaException>(() => Leer(ruta)).Motivo);
    }

    [Fact(DisplayName = "administracion-local · La planilla de ejemplo de Personas: el escritor arma un libro de dos hojas con la columna del código como texto, y el lector lee la primera")]
    public void Un_libro_de_dos_hojas_con_una_columna_de_texto()
    {
        var nombre = EscritorPlanilla.EscribirLibro(_carpeta, "planilla",
        [
            new HojaPlanilla("Personas", ["Nombre", "Código"], [["Persona de ejemplo 1", "1001"]], [1]),
            new HojaPlanilla("Instrucciones", ["Cómo completarla"], [["Una persona por fila."]]),
        ]);

        Assert.EndsWith(".xlsx", nombre, StringComparison.Ordinal);
        using (var libro = ZipFile.OpenRead(Path.Combine(_carpeta, nombre)))
        {
            var ns = XNamespace.Get(Principal);
            Assert.Equal(["Personas", "Instrucciones"], Xml(libro, "xl/workbook.xml").Descendants(ns + "sheet").Select(s => (string)s.Attribute("name")!));
            var texto = Xml(libro, "xl/styles.xml").Descendants(ns + "cellXfs").Elements(ns + "xf").ToList();
            var codigo = Xml(libro, "xl/worksheets/sheet1.xml").Descendants(ns + "c").Single(c => (string?)c.Attribute("r") == "B2");
            Assert.Equal("49", (string?)texto[int.Parse((string)codigo.Attribute("s")!)].Attribute("numFmtId"));
            Assert.NotNull(Xml(libro, "xl/worksheets/sheet1.xml").Descendants(ns + "col").SingleOrDefault(c => (string?)c.Attribute("min") == "2"));
        }

        Assert.Equal(["Persona de ejemplo 1", "1001"], Leer(Path.Combine(_carpeta, nombre))[1]);
    }

    private static IReadOnlyList<IReadOnlyList<string>> Leer(string ruta)
    {
        using var flujo = File.OpenRead(ruta);
        return LectorPlanilla.Leer(flujo);
    }

    /// <summary>Un libro mínimo armado a mano, como lo guardaría otro programa: una hoja y, si se pasan, cadenas compartidas.</summary>
    private string Libro(string datosHoja, string? cadenas = null)
    {
        var ruta = Path.Combine(_carpeta, Guid.NewGuid().ToString("N") + ".xlsx");
        using var libro = ZipFile.Open(ruta, ZipArchiveMode.Create);
        Parte(libro, "xl/workbook.xml",
            $"""<workbook xmlns="{Principal}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Hoja1" sheetId="1" r:id="rId5"/></sheets></workbook>""");
        Parte(libro, "xl/_rels/workbook.xml.rels",
            """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId5" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="/xl/worksheets/hoja.xml"/></Relationships>""");
        Parte(libro, "xl/worksheets/hoja.xml", $"""<worksheet xmlns="{Principal}">{datosHoja}</worksheet>""");
        if (cadenas is not null)
        {
            Parte(libro, "xl/sharedStrings.xml", cadenas);
        }

        return ruta;
    }

    private static void Parte(ZipArchive libro, string nombre, string contenido)
    {
        using var escritor = new StreamWriter(libro.CreateEntry(nombre).Open(), new UTF8Encoding(false));
        escritor.Write(contenido);
    }

    private static XDocument Xml(ZipArchive libro, string parte)
    {
        using var flujo = libro.GetEntry(parte)!.Open();
        return XDocument.Load(flujo);
    }
}
