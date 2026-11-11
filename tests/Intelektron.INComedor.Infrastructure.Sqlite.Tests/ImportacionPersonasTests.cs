using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Revisar y aplicar la importación de Personas, y la planilla de ejemplo (administracion-local de
/// importar-personas-desde-excel, D2 a D6). Sobre una base SQLite real temporal con el fixture sintético: «Persona
/// habilitada» tiene el código 000123 y sus cupos; «Persona sin derecho» está activa sin código; «Persona inactiva», de baja.
/// </summary>
public class ImportacionPersonasTests : IDisposable
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById(FixtureSintetico.ZonaHoraria);

    public ImportacionPersonasTests()
    {
        Directory.CreateDirectory(Destino);
        Base = BaseComedor.Abrir(Path.Combine(Carpeta, "incomedor.db"));
        Fixture = FixtureSintetico.Cargar(Base);
    }

    private string Carpeta { get; } = Path.Combine(Path.GetTempPath(), "incomedor-tests", "importar-" + Guid.NewGuid().ToString("N"));

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

    [Fact(DisplayName = "administracion-local · Modificar y agregar: el resumen cuenta una modificada por su código y una agregada; al aplicar, la versión 2 las tiene y la persona que no está sigue activa")]
    public void Modificar_y_agregar()
    {
        var revision = Revisar(["Habilitada nueva", FixtureSintetico.CodigoSintetico, "2"], ["Persona nueva", "0777", "1"]);

        Assert.True(revision.Valida, string.Join("; ", revision.Errores));
        Assert.Equal(1, revision.Agregadas);
        Assert.Equal(1, revision.Modificadas);
        Assert.Contains(revision.DeBajaAlReemplazar, p => p.PersonaId == Fixture.SinDerecho.Id);
        var resultado = Importacion.Aplicar(revision, ModoImportacion.ModificarYAgregar, null);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Guardado.Tipo);
        Assert.Equal(2, resultado.Guardado.VersionActiva);
        var vista = Admin.Leer();
        var habilitada = Assert.Single(vista.Personas, p => p.Id == Fixture.Habilitada.Id);
        Assert.Equal("Habilitada nueva", habilitada.Nombre);
        Assert.Equal(2, habilitada.Cupos.Single(c => c.ServicioId == Par.Servicio && c.CategoriaId == Par.Categoria).Limite);
        Assert.Single(vista.Personas, p => p.Nombre == "Persona nueva" && p.Activa);
        Assert.True(vista.Personas.Single(p => p.Id == Fixture.SinDerecho.Id).Activa);
        Assert.All(resultado.Personas, p => Assert.NotNull(p.PersonaId));
        using var conexion = Base.AbrirConexion();
        Assert.Equal(2L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = 'CONFIGURATION_CHANGED'"));
    }

    [Fact(DisplayName = "administracion-local · Reemplazar conserva a los operadores: da de baja a la persona activa que no está, salvo si tiene un permiso de operador")]
    public void Reemplazar_conserva_a_los_operadores()
    {
        var revision = Revisar(["Persona habilitada", FixtureSintetico.CodigoSintetico, "1"]);
        Importacion.Aplicar(revision, ModoImportacion.Reemplazar, null);
        Assert.False(Admin.Leer().Personas.Single(p => p.Id == Fixture.SinDerecho.Id).Activa);

        using var otra = new ImportacionPersonasTests();
        otra.DarPermiso(otra.Fixture.SinDerecho.Id);
        var conservada = otra.Revisar(["Persona habilitada", FixtureSintetico.CodigoSintetico, "1"]);
        Assert.DoesNotContain(conservada.DeBajaAlReemplazar, p => p.PersonaId == otra.Fixture.SinDerecho.Id);
        otra.Importacion.Aplicar(conservada, ModoImportacion.Reemplazar, null);
        Assert.True(otra.Admin.Leer().Personas.Single(p => p.Id == otra.Fixture.SinDerecho.Id).Activa);
    }

    [Fact(DisplayName = "administracion-local · Los consumos de hoy siguen contando: importar no toca lo usado hoy, que la vista sigue mostrando")]
    public void Los_consumos_de_hoy_siguen_contando()
    {
        using (var conexion = Base.AbrirConexion())
        {
            Sql.Ejecutar(conexion,
                "INSERT INTO tbComedorUsosCupo (comperCodigo, comacCodigo, comcatCodigo, comucFechaNegocio, comucUsado, comucVersion) VALUES ($per, $amb, $cat, '2026-11-06', 1, 1)",
                ("$per", Fixture.Habilitada.Id), ("$amb", Fixture.Ambito), ("$cat", Par.Categoria));
        }

        Importacion.Aplicar(Revisar(["Persona habilitada", FixtureSintetico.CodigoSintetico, "1"]), ModoImportacion.ModificarYAgregar, null);

        var cupo = Admin.Leer().Personas.Single(p => p.Id == Fixture.Habilitada.Id).Cupos.Single(c => c.ServicioId == Par.Servicio && c.CategoriaId == Par.Categoria);
        Assert.Equal(1, cupo.UsadoHoy);
        Assert.Equal(1, cupo.Limite);
    }

    [Fact(DisplayName = "administracion-local · Una fila con error no aplica nada: sin código, repetido, dado de baja, nombre vacío y límite fuera de rango, cada uno con su fila y su motivo")]
    public void Una_fila_con_error_no_aplica_nada()
    {
        RevocarCodigo("0999");

        var revision = Revisar(
            ["Sin código", "", "1"], ["Repetida A", "0888", "1"], ["Repetida B", "0888", "1"], ["De baja", "0999", "1"], ["", "0444", "1"], ["Límite alto", "0555", "100"]);

        Assert.False(revision.Valida);
        Assert.Contains(new ErrorImportacion(2, "Código", CodigosAdministracion.Requerido), revision.Errores);
        Assert.Contains(new ErrorImportacion(3, "Código", CodigosAdministracion.Repetido), revision.Errores);
        Assert.Contains(new ErrorImportacion(4, "Código", CodigosAdministracion.Repetido), revision.Errores);
        Assert.Contains(new ErrorImportacion(5, "Código", ImportacionPersonas.CodigoDadoDeBaja), revision.Errores);
        Assert.Contains(revision.Errores, e => e.Fila == 6 && e.Campo == "Nombre");
        Assert.Contains(revision.Errores, e => e.Fila == 7 && e.Motivo == CodigosAdministracion.FueraDeRango);
        Assert.Throws<InvalidOperationException>(() => Importacion.Aplicar(revision, ModoImportacion.ModificarYAgregar, null));
        Assert.Equal(1, Admin.Leer().VersionConfiguracion);
    }

    [Fact(DisplayName = "administracion-local · Un archivo que no es una planilla: sin la columna «Código», con una columna desconocida o dañado, el resumen dice qué falta o qué sobra")]
    public void Un_archivo_que_no_es_una_planilla()
    {
        var sinCodigo = Revisar(["Nombre"], [["Ana"]]);
        Assert.Contains(new ErrorImportacion(1, "Código", CodigosAdministracion.Requerido), sinCodigo.Errores);

        var desconocida = Revisar(["Nombre", "Código", "Postre: límite"], [["Ana", "0777", "1"]]);
        Assert.Contains(new ErrorImportacion(1, "Postre: límite", ImportacionPersonas.ColumnaDesconocida), desconocida.Errores);

        var vacia = Revisar(["Nombre", "Código"], Array.Empty<string[]>());
        Assert.Equal([new ErrorImportacion(0, "Personas", CodigosAdministracion.Requerido)], vacia.Errores);

        using var danado = new MemoryStream(Encoding.UTF8.GetBytes("no es un libro"));
        Assert.Contains(new ErrorImportacion(0, "Archivo", PlanillaInvalidaException.NoEsUnLibro), Importacion.Revisar(danado).Errores);
    }

    [Fact(DisplayName = "administracion-local · Descargar la planilla de ejemplo: un libro con «Personas» e «Instrucciones», el encabezado de cada cupo, el código como texto y ningún código del puesto")]
    public void La_planilla_de_ejemplo()
    {
        var nombre = Importacion.EscribirPlanillaDeEjemplo(Destino, Zona);

        Assert.Equal("INComedor-planilla-personas-20261106-120000.xlsx", nombre);
        var ruta = Path.Combine(Destino, nombre);
        using (var libro = ZipFile.OpenRead(ruta))
        {
            var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            using var flujo = libro.GetEntry("xl/workbook.xml")!.Open();
            Assert.Equal(["Personas", "Instrucciones"], XDocument.Load(flujo).Descendants(ns + "sheet").Select(s => (string)s.Attribute("name")!));
        }

        var filas = Leer(ruta);
        var vista = Admin.Leer();
        Assert.Equal(new[] { "Nombre", "Código" }.Concat(vista.ParesDeCupo().Select(p => $"{vista.NombrePar(p)}: límite")), filas[0]);
        Assert.True(filas.Count >= 3);
        Assert.DoesNotContain(filas.Skip(1), f => f[1] == FixtureSintetico.CodigoSintetico);

        using var planilla = File.OpenRead(ruta);
        var vuelta = Importacion.Revisar(planilla);
        Assert.True(vuelta.Valida, string.Join("; ", vuelta.Errores));
        Assert.Equal(2, vuelta.Agregadas);
    }

    private (Guid Servicio, Guid Categoria) Par => Admin.Leer().ParesDeCupo()[0];

    /// <summary>Un archivo con el encabezado de Nombre, Código y el primer cupo, y las filas dadas.</summary>
    private RevisionImportacion Revisar(params string[][] filas)
    {
        var vista = Admin.Leer();
        return Revisar(["Nombre", "Código", $"{vista.NombrePar(vista.ParesDeCupo()[0])}: límite"], filas);
    }

    private RevisionImportacion Revisar(string[] encabezado, string[][] filas)
    {
        var nombre = EscritorPlanilla.EscribirLibro(Destino, "importar-" + Guid.NewGuid().ToString("N"),
            [new HojaPlanilla("Personas", encabezado, filas.Select(f => (IReadOnlyList<object>)f.Cast<object>().ToList()), [1])]);
        using var flujo = File.OpenRead(Path.Combine(Destino, nombre));
        return Importacion.Revisar(flujo);
    }

    private static IReadOnlyList<IReadOnlyList<string>> Leer(string ruta)
    {
        using var flujo = File.OpenRead(ruta);
        return LectorPlanilla.Leer(flujo);
    }

    private void DarPermiso(Guid persona)
    {
        using var conexion = Base.AbrirConexion();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorPermisosOperadores (comvcCodigo, compopCodigo, comperCodigo, compopRol) VALUES ($vc, $pop, $per, 'OPERATOR')",
            ("$vc", Fixture.SnapshotId), ("$pop", Guid.NewGuid()), ("$per", persona));
    }

    /// <summary>Una credencial revocada de la persona inactiva: un código que existe y no se puede reasignar ni reactivar.</summary>
    private void RevocarCodigo(string codigo)
    {
        using var conexion = Base.AbrirConexion();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, NULL, '2026-11-01T00:00:00.000Z')",
            ("$vc", Fixture.SnapshotId), ("$cre", Guid.NewGuid()), ("$per", Fixture.Inactiva.Id), ("$valor", codigo));
    }
}
