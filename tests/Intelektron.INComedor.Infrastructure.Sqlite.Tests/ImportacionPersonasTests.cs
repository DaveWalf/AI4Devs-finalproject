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

        // Una columna que sobra se nombra por su letra: su título lo escribe cualquiera y puede ser un código.
        var desconocida = Revisar(["Nombre", "Código", "Postre: límite", "0001"], [["Ana", "0777", "1", ""]]);
        Assert.Contains(new ErrorImportacion(1, "C", ImportacionPersonas.ColumnaDesconocida), desconocida.Errores);
        Assert.Contains(new ErrorImportacion(1, "D", ImportacionPersonas.ColumnaDesconocida), desconocida.Errores);
        Assert.DoesNotContain(desconocida.Errores, e => e.Campo.Contains("0001", StringComparison.Ordinal));

        var repetida = Revisar(["Nombre", "Código", "Nombre"], [["Ana", "0777", "Ana"]]);
        Assert.Contains(new ErrorImportacion(1, "C", CodigosAdministracion.Repetido), repetida.Errores);

        var sinTitulo = Revisar(["Nombre", "Código"], [["Ana", "0777", "algo"]]);
        Assert.Contains(new ErrorImportacion(1, "C", ImportacionPersonas.ColumnaDesconocida), sinTitulo.Errores);

        var vacia = Revisar(["Nombre", "Código"], Array.Empty<string[]>());
        Assert.Equal([new ErrorImportacion(0, "Personas", CodigosAdministracion.Requerido)], vacia.Errores);

        using var danado = new MemoryStream(Encoding.UTF8.GetBytes("no es un libro"));
        Assert.Contains(new ErrorImportacion(0, "Archivo", PlanillaInvalidaException.NoEsUnLibro), Importacion.Revisar(danado).Errores);
    }

    [Fact(DisplayName = "administracion-local · Descargar la planilla de ejemplo: un libro con «Personas» e «Instrucciones», el encabezado de cada cupo, el código como texto y ningún código del puesto")]
    public void La_planilla_de_ejemplo()
    {
        // 1001 ya se usó en el puesto, aunque revocado: la planilla no lo ofrece.
        RevocarCodigo("1001");

        var nombre = Importacion.EscribirPlanillaDeEjemplo(Destino, Zona);

        Assert.Equal("INComedor-planilla-personas-20261106-120000.xlsx", nombre);
        var ruta = Path.Combine(Destino, nombre);
        using (var libro = ZipFile.OpenRead(ruta))
        {
            var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            XDocument Xml(string parte)
            {
                using var flujo = libro.GetEntry(parte)!.Open();
                return XDocument.Load(flujo);
            }

            Assert.Equal(["Personas", "Instrucciones"], Xml("xl/workbook.xml").Descendants(ns + "sheet").Select(s => (string)s.Attribute("name")!));
            var estilos = Xml("xl/styles.xml").Descendants(ns + "cellXfs").Elements(ns + "xf").ToList();
            var hoja = Xml("xl/worksheets/sheet1.xml");
            string Formato(string? estilo) => (string)estilos[int.Parse(estilo ?? "0", System.Globalization.CultureInfo.InvariantCulture)].Attribute("numFmtId")!;
            Assert.Equal("49", Formato((string?)hoja.Descendants(ns + "col").Single(c => (string?)c.Attribute("min") == "2").Attribute("style")));
            Assert.All(new[] { "B2", "B3" }, r => Assert.Equal("49", Formato((string?)hoja.Descendants(ns + "c").Single(c => (string?)c.Attribute("r") == r).Attribute("s"))));
        }

        var filas = Leer(ruta);
        var vista = Admin.Leer();
        Assert.Equal(new[] { "Nombre", "Código", "Operador" }.Concat(vista.ParesDeCupo().Select(p => $"{vista.NombrePar(p)}: límite")), filas[0]);
        Assert.Equal(["1002", "1003"], filas.Skip(1).Select(f => f[1]));

        using var planilla = File.OpenRead(ruta);
        var vuelta = Importacion.Revisar(planilla);
        Assert.True(vuelta.Valida, string.Join("; ", vuelta.Errores));
        Assert.Equal(2, vuelta.Agregadas);
        Assert.Equal(2, vuelta.FilasDeEjemplo);
    }

    [Fact(DisplayName = "administracion-local · Importar Personas desde Excel: el encabezado se reconoce sin distinguir mayúsculas, espacios de más ni tildes")]
    public void El_encabezado_sin_mayusculas_espacios_ni_tildes()
    {
        var vista = Admin.Leer();
        var cupo = $"  {vista.NombrePar(vista.ParesDeCupo()[0]).ToLowerInvariant()}:   LIMITE ";

        var revision = Revisar(["  NOMBRE ", "codigo", cupo], [["Persona nueva", "0777", "1"]]);

        Assert.True(revision.Valida, string.Join("; ", revision.Errores));
        Assert.Equal(1, revision.Agregadas);
    }

    [Fact(DisplayName = "administracion-local · Importar Personas desde Excel: una celda vacía quita ese cupo, una columna que falta deja los demás como estaban y una fila igual queda sin cambios, sin crear versión")]
    public void Celda_vacia_columna_ausente_y_fila_sin_cambios()
    {
        var antes = Admin.Leer().Personas.Single(p => p.Id == Fixture.Habilitada.Id);
        Assert.Contains(antes.Cupos, c => c.ServicioId == Par.Servicio && c.CategoriaId == Par.Categoria);
        var limite = antes.Cupos.Single(c => c.ServicioId == Par.Servicio && c.CategoriaId == Par.Categoria).Limite.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var igual = Revisar([antes.Nombre, FixtureSintetico.CodigoSintetico, limite]);
        Assert.Equal(1, igual.SinCambios);
        Assert.Empty(igual.Ediciones);
        var sinCambios = Importacion.Aplicar(igual, ModoImportacion.ModificarYAgregar, null);
        Assert.Equal(TipoGuardado.Aplicado, sinCambios.Guardado.Tipo);
        Assert.Equal(1, sinCambios.Guardado.VersionActiva);
        Assert.Equal(1, Admin.Leer().VersionConfiguracion);

        Importacion.Aplicar(Revisar([antes.Nombre, FixtureSintetico.CodigoSintetico, ""]), ModoImportacion.ModificarYAgregar, null);

        var despues = Admin.Leer().Personas.Single(p => p.Id == Fixture.Habilitada.Id);
        Assert.DoesNotContain(despues.Cupos, c => c.ServicioId == Par.Servicio && c.CategoriaId == Par.Categoria);
        Assert.Equal(
            antes.Cupos.Where(c => c.ServicioId != Par.Servicio || c.CategoriaId != Par.Categoria).Select(c => (c.ServicioId, c.CategoriaId, c.Limite)),
            despues.Cupos.Select(c => (c.ServicioId, c.CategoriaId, c.Limite)));
    }

    [Fact(DisplayName = "administracion-local · Quién es quién: una credencial vencida de una persona activa la reconoce sin tocar sus fechas; una revocada es un error, y dos códigos de la misma persona también")]
    public void Credenciales_de_una_persona_activa()
    {
        AgregarCredencial(Fixture.SinDerecho.Id, "0555", hasta: "2026-10-15T03:00:00.000Z");
        AgregarCredencial(Fixture.SinDerecho.Id, "0666", revocada: "2026-10-15T03:00:00.000Z");
        AgregarCredencial(Fixture.Habilitada.Id, "0124");

        var vencida = Revisar(["Sin derecho corregida", "0555", "1"]);
        Assert.True(vencida.Valida, string.Join("; ", vencida.Errores));
        Assert.Equal(Fixture.SinDerecho.Id, Assert.Single(vencida.Personas).PersonaId);
        Importacion.Aplicar(vencida, ModoImportacion.ModificarYAgregar, null);
        Assert.Equal("Sin derecho corregida", Admin.Leer().Personas.Single(p => p.Id == Fixture.SinDerecho.Id).Nombre);
        using (var conexion = Base.AbrirConexion())
        {
            Assert.Equal("2026-10-15T03:00:00.000Z", Sql.Escalar<string>(conexion,
                "SELECT c.comcreFechaVigenciaHastaUtc FROM tbComedorCredencialesPersonas c JOIN tbComedorConfiguracionPuestos p ON p.comvcCodigoActiva = c.comvcCodigo WHERE c.comcreValorNormalizado = '0555'"));
        }

        var revocada = Revisar(["Sin derecho", "0666", "1"]);
        Assert.Contains(new ErrorImportacion(2, "Código", ImportacionPersonas.CodigoDadoDeBaja), revocada.Errores);

        var dos = Revisar(["Persona habilitada", FixtureSintetico.CodigoSintetico, "1"], ["Persona habilitada", "0124", "1"]);
        Assert.Contains(new ErrorImportacion(3, "Código", ImportacionPersonas.MismaPersona), dos.Errores);
    }

    [Fact(DisplayName = "administracion-local · Reemplazar conserva a los operadores: un permiso vencido sin revocar conserva a la persona; uno revocado, no")]
    public void Reemplazar_con_permisos_vencidos_y_revocados()
    {
        var vencido = Fixture.SinDerecho.Id;
        DarPermiso(vencido, hasta: "2026-10-15T03:00:00.000Z");
        var revocado = AgregarPersona("Operador revocado");
        DarPermiso(revocado, revocado: "2026-10-15T03:00:00.000Z");

        var revision = Revisar(["Persona habilitada", FixtureSintetico.CodigoSintetico, "1"]);

        Assert.DoesNotContain(revision.DeBajaAlReemplazar, p => p.PersonaId == vencido);
        Assert.Contains(revision.DeBajaAlReemplazar, p => p.PersonaId == revocado);
    }

    [Fact(DisplayName = "administracion-local · Quién es quién: el resumen cuenta los códigos guardados como número y las filas de la planilla de ejemplo; uno de más de 15 dígitos guardado como número es un error")]
    public void Codigos_como_numero_y_filas_de_ejemplo()
    {
        var vista = Admin.Leer();
        string[] encabezado = ["Nombre", "Código", $"{vista.NombrePar(vista.ParesDeCupo()[0])}: límite"];

        var numeros = Revisar(encabezado, [["Persona de ejemplo 1", new NumeroPlanilla(105, 0), "1"], ["Persona nueva", "0106", "1"]]);
        Assert.True(numeros.Valida, string.Join("; ", numeros.Errores));
        Assert.Equal(1, numeros.CodigosComoNumero);
        Assert.Equal(1, numeros.FilasDeEjemplo);

        var largo = Revisar(encabezado, [["Persona nueva", new NumeroPlanilla(1234567890123456, 0), "1"]]);
        Assert.Contains(new ErrorImportacion(2, "Código", ImportacionPersonas.CodigoComoNumero), largo.Errores);
    }

    private (Guid Servicio, Guid Categoria) Par => Admin.Leer().ParesDeCupo()[0];

    /// <summary>Un archivo con el encabezado de Nombre, Código y el primer cupo, y las filas dadas.</summary>
    private RevisionImportacion Revisar(params string[][] filas)
    {
        var vista = Admin.Leer();
        return Revisar(["Nombre", "Código", $"{vista.NombrePar(vista.ParesDeCupo()[0])}: límite"], filas);
    }

    private RevisionImportacion Revisar(string[] encabezado, string[][] filas) =>
        Revisar(encabezado, filas.Select(f => f.Cast<object>().ToArray()).ToArray());

    /// <summary>Un archivo con celdas de texto o números (<see cref="NumeroPlanilla"/>), como las guarda Excel.</summary>
    private RevisionImportacion Revisar(string[] encabezado, object[][] filas)
    {
        var nombre = EscritorPlanilla.EscribirLibro(Destino, "importar-" + Guid.NewGuid().ToString("N"),
            [new HojaPlanilla("Personas", encabezado, filas.Select(f => (IReadOnlyList<object>)f), [1])]);
        using var flujo = File.OpenRead(Path.Combine(Destino, nombre));
        return Importacion.Revisar(flujo);
    }

    private void AgregarCredencial(Guid persona, string codigo, string? hasta = null, string? revocada = null)
    {
        using var conexion = Base.AbrirConexion();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, $hasta, $revocada)",
            ("$vc", Fixture.SnapshotId), ("$cre", Guid.NewGuid()), ("$per", persona), ("$valor", codigo), ("$hasta", hasta), ("$revocada", revocada));
    }

    private Guid AgregarPersona(string nombre)
    {
        var id = Guid.NewGuid();
        using var conexion = Base.AbrirConexion();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorPersonas (comvcCodigo, comperCodigo, comperNombreVisible, comperActivo, comperVersionMaterialIdentidad) VALUES ($vc, $per, $nombre, 1, 1)",
            ("$vc", Fixture.SnapshotId), ("$per", id), ("$nombre", nombre));
        return id;
    }

    private static IReadOnlyList<IReadOnlyList<string>> Leer(string ruta)
    {
        using var flujo = File.OpenRead(ruta);
        return LectorPlanilla.Leer(flujo);
    }

    private void DarPermiso(Guid persona, string? hasta = null, string? revocado = null)
    {
        using var conexion = Base.AbrirConexion();
        Sql.Ejecutar(conexion,
            """
            INSERT INTO tbComedorPermisosOperadores (comvcCodigo, compopCodigo, comperCodigo, compopRol, compopFechaVigenciaHastaUtc, compopFechaRevocacionUtc)
            VALUES ($vc, $pop, $per, 'OPERATOR', $hasta, $revocado)
            """,
            ("$vc", Fixture.SnapshotId), ("$pop", Guid.NewGuid()), ("$per", persona), ("$hasta", hasta), ("$revocado", revocado));
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
