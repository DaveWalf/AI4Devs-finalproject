using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// La exportación de Personas a CSV y a Excel (administracion-local de editar-raciones-en-personas, D5): la persona, el
/// estado, lo usado hoy y el límite de cada cupo y las opciones habilitadas, sin ningún código. Sobre una base SQLite real
/// temporal con el fixture sintético; el reloj está fijo en 2026-11-06T15:00:00Z, las 12:00 del puesto.
/// </summary>
public class ExportacionPersonasTests : IDisposable
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById(FixtureSintetico.ZonaHoraria);

    public ExportacionPersonasTests()
    {
        Directory.CreateDirectory(Destino);
        Base = BaseComedor.Abrir(Path.Combine(Carpeta, "incomedor.db"));
        Fixture = FixtureSintetico.Cargar(Base);
    }

    private string Carpeta { get; } = Path.Combine(Path.GetTempPath(), "incomedor-tests", "exportar-personas-" + Guid.NewGuid().ToString("N"));

    private string Destino => Path.Combine(Carpeta, "exportaciones");

    private BaseComedor Base { get; }

    private FixtureSintetico Fixture { get; }

    private RelojFijo Reloj { get; } = new("2026-11-06T15:00:00Z");

    private AdministracionLocal Admin => new(Base, Reloj);

    public void Dispose()
    {
        if (Directory.Exists(Carpeta))
        {
            Directory.Delete(Carpeta, recursive: true);
        }
    }

    [Fact(DisplayName = "administracion-local · Exportar Personas a CSV: un encabezado con la persona, el estado, lo usado hoy y el límite de cada cupo y las opciones, una fila por persona y ningún código")]
    public void Exportar_personas_a_csv()
    {
        var vista = Admin.Leer();
        Usar(Fixture.CategoriaAlmuerzo, 1);

        var resultado = new ExportacionPersonas(Admin, Reloj).Exportar(FormatoExportacion.Csv, Destino, Zona);

        Assert.Equal("INComedor-personas-20261106-120000.csv", resultado.Archivo);
        Assert.Equal(vista.Personas.Count, resultado.Filas);
        var lineas = File.ReadAllText(Path.Combine(Destino, resultado.Archivo), Encoding.UTF8).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(vista.Personas.Count + 1, lineas.Length);
        var par = vista.ParesDeCupo()[0];
        Assert.StartsWith("\"Persona\";\"Estado\";", lineas[0], StringComparison.Ordinal);
        Assert.Contains($"\"{vista.NombrePar(par)}: usado hoy\";\"{vista.NombrePar(par)}: límite\"", lineas[0], StringComparison.Ordinal);
        Assert.EndsWith("\"Opciones habilitadas\";\"Operador\"", lineas[0], StringComparison.Ordinal);
        var habilitada = Assert.Single(lineas, l => l.StartsWith("\"Persona habilitada\";", StringComparison.Ordinal));
        Assert.StartsWith("\"Persona habilitada\";\"Activa\";", habilitada, StringComparison.Ordinal);
        Assert.Contains(lineas, l => l.StartsWith("\"Persona inactiva\";\"De baja\";", StringComparison.Ordinal));
        Assert.DoesNotContain(FixtureSintetico.CodigoSintetico, string.Join('\n', lineas), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "administracion-local · Exportar Personas a Excel: un libro con una hoja, y lo usado hoy y el límite como números")]
    public void Exportar_personas_a_excel()
    {
        Usar(Fixture.CategoriaAlmuerzo, 1);

        var resultado = new ExportacionPersonas(Admin, Reloj).Exportar(FormatoExportacion.Xlsx, Destino, Zona);

        Assert.EndsWith(".xlsx", resultado.Archivo, StringComparison.Ordinal);
        using var libro = ZipFile.OpenRead(Path.Combine(Destino, resultado.Archivo));
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        XDocument Leer(string parte)
        {
            using var flujo = libro.GetEntry(parte)!.Open();
            return XDocument.Load(flujo);
        }

        Assert.Single(Leer("xl/workbook.xml").Descendants(ns + "sheet"));
        var filas = Leer("xl/worksheets/sheet1.xml").Descendants(ns + "row").ToList();
        var habilitada = filas.Single(f => f.Elements(ns + "c").First().Value == "Persona habilitada");
        var usado = habilitada.Elements(ns + "c").ElementAt(2);
        Assert.Null(usado.Attribute("t"));
        Assert.Equal("1", usado.Element(ns + "v")!.Value);
        var limite = habilitada.Elements(ns + "c").ElementAt(3);
        Assert.Null(limite.Attribute("t"));
        Assert.True(int.Parse(limite.Element(ns + "v")!.Value, System.Globalization.CultureInfo.InvariantCulture) >= 1);
    }

    [Fact(DisplayName = "administracion-local · Exportar Personas a CSV: un nombre de servicio que empieza como una fórmula lleva el apóstrofo también en el encabezado")]
    public void Un_encabezado_que_empieza_como_formula()
    {
        using (var conexion = Base.AbrirConexion())
        {
            Sql.Ejecutar(conexion, "UPDATE tbComedorTiposServicio SET comsrvNombre = '=SUMA(1)' WHERE comsrvCodigo = $srv", ("$srv", Fixture.ServicioAlmuerzo));
        }

        var resultado = new ExportacionPersonas(Admin, Reloj).Exportar(FormatoExportacion.Csv, Destino, Zona);

        var encabezado = File.ReadAllLines(Path.Combine(Destino, resultado.Archivo), Encoding.UTF8)[0];
        Assert.Contains("\"'=SUMA(1)", encabezado, StringComparison.Ordinal);
        Assert.DoesNotContain(";\"=SUMA(1)", encabezado, StringComparison.Ordinal);
    }

    /// <summary>Lo usado hoy por la persona habilitada en una categoría, por SQL, como lo deja una confirmación.</summary>
    private void Usar(Guid categoria, int usado)
    {
        using var conexion = Base.AbrirConexion();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorUsosCupo (comperCodigo, comacCodigo, comcatCodigo, comucFechaNegocio, comucUsado, comucVersion) VALUES ($per, $amb, $cat, '2026-11-06', $usado, 1)",
            ("$per", Fixture.Habilitada.Id), ("$amb", Fixture.Ambito), ("$cat", categoria), ("$usado", usado));
    }
}
