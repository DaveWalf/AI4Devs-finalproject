using System.Text;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// La columna «Operador» de los Excel de Personas (D4 de marcar-operadores-en-personas): al final de la exportación, con «Sí» o
/// «No»; después de «Código» en la planilla de ejemplo, con la segunda fila en «Sí» y su oración en «Instrucciones»; y en la
/// importación, donde «Sí» da, «No» quita, vacía o ausente no cambia, otro valor es un error de su fila sin lo escrito, y un
/// operador no cambia el suyo. Sobre el fixture sintético: «Persona habilitada» tiene el código 000123; la prueba suma un
/// operador vigente con el código 9001.
/// </summary>
public class OperadorEnLosExcelTests : IDisposable
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById(FixtureSintetico.ZonaHoraria);

    private readonly Guid _operador;

    public OperadorEnLosExcelTests()
    {
        Directory.CreateDirectory(Destino);
        Base = BaseComedor.Abrir(Path.Combine(Carpeta, "incomedor.db"));
        Fixture = FixtureSintetico.Cargar(Base);
        _operador = Guid.NewGuid();
        using var conexion = Base.AbrirConexion();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Operador sintético 01', 1, 1)",
            ("$vc", Fixture.SnapshotId), ("$per", _operador));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', '9001', NULL, NULL, NULL)",
            ("$vc", Fixture.SnapshotId), ("$cre", Guid.NewGuid()), ("$per", _operador));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPermisosOperadores VALUES ($vc, $pop, $per, 'OPERATOR', NULL, NULL, NULL)",
            ("$vc", Fixture.SnapshotId), ("$pop", Guid.NewGuid()), ("$per", _operador));
    }

    private string Carpeta { get; } = Path.Combine(Path.GetTempPath(), "incomedor-tests", "operador-excel-" + Guid.NewGuid().ToString("N"));

    private string Destino => Path.Combine(Carpeta, "archivos");

    private BaseComedor Base { get; }

    private FixtureSintetico Fixture { get; }

    private RelojFijo Reloj { get; } = new("2026-11-06T15:00:00Z");

    private AdministracionLocal Admin => new(Base, Reloj);

    private ImportacionPersonas Importacion => new(Admin);

    public void Dispose()
    {
        if (Directory.Exists(Carpeta))
        {
            Directory.Delete(Carpeta, recursive: true);
        }
    }

    [Theory(DisplayName = "administracion-local · Exportar Personas: «Operador» va al final, con «Sí» para el operador vigente y «No» para los demás, en CSV y en Excel")]
    [InlineData(FormatoExportacion.Csv)]
    [InlineData(FormatoExportacion.Xlsx)]
    public void La_exportacion_lleva_operador_al_final(FormatoExportacion formato)
    {
        var resultado = new ExportacionPersonas(Admin, Reloj).Exportar(formato, Destino, Zona);

        var filas = formato == FormatoExportacion.Csv
            ? File.ReadAllText(Path.Combine(Destino, resultado.Archivo), Encoding.UTF8).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
                .Select(l => (IReadOnlyList<string>)l.Split(';').Select(c => c.Trim('"')).ToList()).ToList()
            : Leer(Path.Combine(Destino, resultado.Archivo)).ToList();
        Assert.Equal("Operador", filas[0][^1]);
        Assert.Equal("Sí", filas.Single(f => f[0] == "Operador sintético 01")[^1]);
        Assert.Equal("No", filas.Single(f => f[0] == "Persona habilitada")[^1]);
        Assert.All(filas.Skip(1), f => Assert.Contains(f[^1], new[] { "Sí", "No" }));
    }

    [Fact(DisplayName = "administracion-local · La planilla de ejemplo lleva «Operador» después de «Código», «No» en la primera fila y «Sí» en la segunda, y la oración de «Instrucciones»")]
    public void La_planilla_de_ejemplo_lleva_operador()
    {
        var nombre = Importacion.EscribirPlanillaDeEjemplo(Destino, Zona);

        var ruta = Path.Combine(Destino, nombre);
        var filas = Leer(ruta);
        Assert.Equal(["Nombre", "Código", "Operador"], filas[0].Take(3));
        Assert.Equal(["No", "Sí"], filas.Skip(1).Select(f => f[2]));
        using var libro = System.IO.Compression.ZipFile.OpenRead(ruta);
        using var hoja = libro.GetEntry("xl/worksheets/sheet2.xml")!.Open();
        var instrucciones = System.Xml.Linq.XDocument.Load(hoja).Descendants()
            .Where(e => e.Name.LocalName == "t").Select(e => e.Value).ToList();
        Assert.Contains(instrucciones, t => t.Contains("«Operador»", StringComparison.Ordinal) && t.Contains("«Sí»", StringComparison.Ordinal)
            && t.Contains("«No»", StringComparison.Ordinal) && t.Contains("vacía", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "administracion-local · Dar y quitar por planilla: «Sí» a 000123 y «No» a 9001 dan y quitan el permiso en la versión nueva, y la revisión y el resultado cuentan 1 dado y 1 quitado")]
    public void Dar_y_quitar_por_planilla()
    {
        var revision = Revisar(["Persona habilitada", FixtureSintetico.CodigoSintetico, "Sí"], ["Operador sintético 01", "9001", "No"]);

        Assert.True(revision.Valida, string.Join("; ", revision.Errores));
        Assert.Equal((1, 1), (revision.PermisosDados, revision.PermisosQuitados));
        Assert.Equal(2, revision.Modificadas);
        var resultado = Importacion.Aplicar(revision, ModoImportacion.ModificarYAgregar, null);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Guardado.Tipo);
        Assert.Equal((1, 1), (resultado.PermisosDados, resultado.PermisosQuitados));
        var vista = Admin.Leer();
        Assert.True(vista.Personas.Single(p => p.Id == Fixture.Habilitada.Id).Operador);
        Assert.False(vista.Personas.Single(p => p.Id == _operador).Operador);
    }

    [Theory(DisplayName = "administracion-local · «Si» sin tilde y en minúsculas también da el permiso")]
    [InlineData("si")]
    [InlineData("SÍ")]
    public void Si_sin_distinguir_mayusculas_ni_tilde(string valor)
    {
        var revision = Revisar(["Persona habilitada", FixtureSintetico.CodigoSintetico, valor]);

        Assert.True(revision.Valida, string.Join("; ", revision.Errores));
        Assert.Equal(1, revision.PermisosDados);
    }

    [Fact(DisplayName = "administracion-local · Una celda «Operador» vacía no cambia el permiso: 9001 sigue operador y su fila queda sin cambios")]
    public void Una_celda_vacia_no_cambia()
    {
        var revision = Revisar(["Operador sintético 01", "9001", ""]);

        Assert.True(revision.Valida, string.Join("; ", revision.Errores));
        Assert.Equal((0, 0), (revision.PermisosDados, revision.PermisosQuitados));
        Assert.Equal(1, revision.SinCambios);
    }

    [Fact(DisplayName = "administracion-local · Una planilla de antes, sin la columna «Operador»: 9001 sigue con su permiso y la columna faltante no es un error")]
    public void Una_planilla_de_antes()
    {
        var vista = Admin.Leer();
        var revision = Revisar(["Nombre", "Código", $"{vista.NombrePar(vista.ParesDeCupo()[0])}: límite"], [["Operador sintético 01", "9001", "1"]]);

        Assert.True(revision.Valida, string.Join("; ", revision.Errores));
        Importacion.Aplicar(revision, ModoImportacion.ModificarYAgregar, null);
        Assert.True(Admin.Leer().Personas.Single(p => p.Id == _operador).Operador);
    }

    [Fact(DisplayName = "administracion-local · Un valor que no se entiende: «Tal vez» y la fórmula =1+1 son errores INVALID_VALUE de su fila, sin lo escrito, y no se aplica nada")]
    public void Un_valor_que_no_se_entiende()
    {
        var revision = Revisar(["Persona habilitada", FixtureSintetico.CodigoSintetico, "Tal vez"], ["Operador sintético 01", "9001", "=1+1"]);

        Assert.False(revision.Valida);
        Assert.Contains(new ErrorImportacion(2, "Operador", "INVALID_VALUE"), revision.Errores);
        Assert.Contains(new ErrorImportacion(3, "Operador", "INVALID_VALUE"), revision.Errores);
        Assert.DoesNotContain(revision.Errores, e => e.ToString().Contains("Tal vez", StringComparison.Ordinal) || e.ToString().Contains("=1+1", StringComparison.Ordinal));
        Assert.Empty(revision.Ediciones);
    }

    [Fact(DisplayName = "administracion-local · Un operador que se quita a sí mismo por planilla: con 9001 como actor, su fila en «No» es un error OWN_PERMISSION y no se aplica nada")]
    public void Un_operador_no_se_quita_por_planilla()
    {
        var revision = Revisar(_operador, ["Operador sintético 01", "9001", "No"]);

        Assert.False(revision.Valida);
        Assert.Contains(new ErrorImportacion(2, "Operador", "OWN_PERMISSION"), revision.Errores);
    }

    private RevisionImportacion Revisar(params string[][] filas) => Revisar(null, filas);

    /// <summary>Un archivo con Nombre, Código y Operador, y las filas dadas, revisado con el actor dado.</summary>
    private RevisionImportacion Revisar(Guid? actor, params string[][] filas) => Revisar(["Nombre", "Código", "Operador"], filas, actor);

    private RevisionImportacion Revisar(string[] encabezado, string[][] filas, Guid? actor = null)
    {
        var nombre = EscritorPlanilla.EscribirLibro(Destino, "importar-" + Guid.NewGuid().ToString("N"),
            [new HojaPlanilla("Personas", encabezado, filas.Select(f => (IReadOnlyList<object>)f.Cast<object>().ToArray()), [1])]);
        using var flujo = File.OpenRead(Path.Combine(Destino, nombre));
        return Importacion.Revisar(flujo, actor);
    }

    private static IReadOnlyList<IReadOnlyList<string>> Leer(string ruta)
    {
        using var flujo = File.OpenRead(ruta);
        return LectorPlanilla.Leer(flujo);
    }
}
