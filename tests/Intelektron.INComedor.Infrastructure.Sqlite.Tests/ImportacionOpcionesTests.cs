using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Exportar Opciones, la planilla base e importarlas reemplazando el catálogo (administracion-local de
/// importar-opciones-desde-excel, D1 a D5). Sobre una base SQLite real temporal con el fixture sintético: todas las opciones
/// cuestan 1.500,00 con una carga, y el catálogo tiene las categorías ALMUERZO y REFRIGERIO.
/// </summary>
public class ImportacionOpcionesTests : IDisposable
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById(FixtureSintetico.ZonaHoraria);

    public ImportacionOpcionesTests()
    {
        Directory.CreateDirectory(Destino);
        Base = BaseComedor.Abrir(Path.Combine(Carpeta, "incomedor.db"));
        Fixture = FixtureSintetico.Cargar(Base);
    }

    private string Carpeta { get; } = Path.Combine(Path.GetTempPath(), "incomedor-tests", "opciones-" + Guid.NewGuid().ToString("N"));

    private string Destino => Path.Combine(Carpeta, "archivos");

    private BaseComedor Base { get; }

    private FixtureSintetico Fixture { get; }

    private AdministracionLocal Admin => new(Base, new RelojFijo("2026-11-06T15:00:00Z"));

    private ImportacionOpciones Importacion => new(Admin);

    private static string[] Encabezado => ["Nombre", "Servicio", "Importe", "Franjas", "ALMUERZO: unidades", "REFRIGERIO: unidades"];

    public void Dispose()
    {
        if (Directory.Exists(Carpeta))
        {
            Directory.Delete(Carpeta, recursive: true);
        }
    }

    [Fact(DisplayName = "administracion-local · Exportar el catálogo: un .xlsx con el encabezado de D1 y una fila por opción, que vuelto a importar queda sin cambios y no crea versión")]
    public void Exportar_y_volver_a_importar()
    {
        var resultado = Importacion.Exportar(Destino, Zona);

        Assert.Equal("INComedor-opciones-20261106-120000.xlsx", resultado.Archivo);
        var vista = Admin.Leer();
        Assert.Equal(vista.Opciones.Count, resultado.Filas);
        var filas = Leer(Path.Combine(Destino, resultado.Archivo));
        Assert.Equal(Encabezado, filas[0]);
        var combo = filas.Single(f => f.Count > 0 && f[0] == "Almuerzo con refrigerio");
        Assert.Equal(["Almuerzo con refrigerio", "ALMUERZO", "1500", "", "1", "1"], combo);

        using var flujo = File.OpenRead(Path.Combine(Destino, resultado.Archivo));
        var revision = Importacion.Revisar(flujo);
        Assert.True(revision.Valida, string.Join("; ", revision.Errores));
        Assert.Equal(vista.Opciones.Count, revision.SinCambios);
        Assert.Empty(revision.Ediciones);
        Assert.Equal(1, Importacion.Aplicar(revision, null).Guardado.VersionActiva);
    }

    [Fact(DisplayName = "administracion-local · Descargar la planilla base: las hojas «Opciones» e «Instrucciones», el encabezado y dos filas de ejemplo que se importan como agregadas")]
    public void La_planilla_base()
    {
        var nombre = Importacion.EscribirPlanillaBase(Destino, Zona);

        Assert.Equal("INComedor-planilla-opciones-20261106-120000.xlsx", nombre);
        var ruta = Path.Combine(Destino, nombre);
        using (var libro = ZipFile.OpenRead(ruta))
        {
            var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            using var flujo = libro.GetEntry("xl/workbook.xml")!.Open();
            Assert.Equal(["Opciones", "Instrucciones"], XDocument.Load(flujo).Descendants(ns + "sheet").Select(s => (string)s.Attribute("name")!));
        }

        var filas = Leer(ruta);
        Assert.Equal(Encabezado, filas[0]);
        Assert.Equal(3, filas.Count);
        using var planilla = File.OpenRead(ruta);
        var revision = Importacion.Revisar(planilla);
        Assert.True(revision.Valida, string.Join("; ", revision.Errores));
        Assert.Equal(2, revision.Agregadas);
        Assert.Equal(Admin.Leer().Opciones.Count, revision.Quitadas);
    }

    [Fact(DisplayName = "administracion-local · Reemplazar el catálogo: una opción con otro importe, una nueva y una que falta dejan la versión 2 con una modificada, una agregada y una quitada, en una sola versión")]
    public void Reemplazar_el_catalogo()
    {
        var vista = Admin.Leer();
        var filas = vista.Opciones.Select(Fila).ToList();
        var quitada = vista.Opciones[^1];
        filas.RemoveAt(filas.Count - 1);
        var modificada = vista.Opciones[0];
        filas[0] = [modificada.Nombre, Servicio(modificada), "1.800,00", "11:00 a 14:00", .. filas[0].Skip(4)];
        filas.Add(["Fruta de estación", "REFRIGERIO", new NumeroPlanilla(400, 2), "", "", new NumeroPlanilla(1, 0)]);

        var revision = Revisar(filas);

        Assert.True(revision.Valida, string.Join("; ", revision.Errores));
        Assert.Equal((1, 1, 1), (revision.Agregadas, revision.Modificadas, revision.Quitadas));
        var resultado = Importacion.Aplicar(revision, null);
        Assert.Equal(TipoGuardado.Aplicado, resultado.Guardado.Tipo);
        Assert.Equal(2, resultado.Guardado.VersionActiva);
        var despues = Admin.Leer();
        var cambiada = despues.Opciones.Single(o => o.Id == modificada.Id);
        Assert.Equal(180000, cambiada.ImporteUnidadesMenores);
        Assert.Equal([new TimeOnly(11, 0)], cambiada.Franjas.Select(f => f.Inicio));
        Assert.Equal(40000, despues.Opciones.Single(o => o.Nombre == "Fruta de estación").ImporteUnidadesMenores);
        Assert.DoesNotContain(despues.Opciones, o => o.Id == quitada.Id);
        Assert.All(resultado.Opciones, o => Assert.NotNull(o.OpcionId));
    }

    [Fact(DisplayName = "administracion-local · Una fila de Opciones con error: sin nombre, repetida, servicio desconocido, sin cargas, unidades fuera de rango, franja mal escrita e importe que no es un número, cada una con su campo y su motivo, y no se aplica nada")]
    public void Una_fila_con_error()
    {
        var revision = Revisar(
        [
            ["", "ALMUERZO", "1500", "", "1", ""],
            ["Repetida", "ALMUERZO", "1500", "", "1", ""],
            ["repetida ", "ALMUERZO", "1500", "", "1", ""],
            ["Servicio raro", "POSTRE", "1500", "", "1", ""],
            ["Sin cargas", "ALMUERZO", "1500", "", "", "0"],
            ["Muchas unidades", "ALMUERZO", "1500", "", "10", ""],
            ["Franja mala", "ALMUERZO", "1500", "11 a 14", "1", ""],
            ["Importe raro", "ALMUERZO", "uno", "", "1", ""],
        ]);

        Assert.False(revision.Valida);
        Assert.Contains(new ErrorImportacion(2, "Nombre", CodigosAdministracion.Requerido), revision.Errores);
        Assert.Contains(new ErrorImportacion(3, "Nombre", CodigosAdministracion.Repetido), revision.Errores);
        Assert.Contains(new ErrorImportacion(4, "Nombre", CodigosAdministracion.Repetido), revision.Errores);
        Assert.Contains(new ErrorImportacion(5, "Servicio", CodigosAdministracion.Desconocido), revision.Errores);
        Assert.Contains(new ErrorImportacion(6, "Cargas", CodigosAdministracion.Requerido), revision.Errores);
        Assert.Contains(new ErrorImportacion(7, "ALMUERZO: unidades", CodigosAdministracion.FueraDeRango), revision.Errores);
        Assert.Contains(new ErrorImportacion(8, "Franjas", CodigosAdministracion.HoraInvalida), revision.Errores);
        Assert.Contains(new ErrorImportacion(9, "Importe", CodigosAdministracion.NoSonDigitos), revision.Errores);
        Assert.Throws<InvalidOperationException>(() => Importacion.Aplicar(revision, null));
        Assert.Equal(1, Admin.Leer().VersionConfiguracion);
    }

    [Fact(DisplayName = "administracion-local · Un archivo de Opciones vacío o que no es una planilla: sin «Importe», con una columna que sobra, sin ninguna opción o dañado, el resumen dice qué falta o qué está mal")]
    public void Un_archivo_vacio_o_que_no_es_una_planilla()
    {
        var sinImporte = Revisar(["Nombre", "Servicio", "Franjas", "ALMUERZO: unidades"], [["Fruta", "ALMUERZO", "", "1"]]);
        Assert.Contains(new ErrorImportacion(1, "Importe", CodigosAdministracion.Requerido), sinImporte.Errores);

        var sobra = Revisar([.. Encabezado, "Postre: unidades"], [["Fruta", "ALMUERZO", "1500", "", "1", "", "1"]]);
        Assert.Contains(new ErrorImportacion(1, "G", ImportacionPersonas.ColumnaDesconocida), sobra.Errores);

        var vacia = Revisar(Encabezado, Array.Empty<object[]>());
        Assert.Equal([new ErrorImportacion(0, "Opciones", CodigosAdministracion.Requerido)], vacia.Errores);

        using var danado = new MemoryStream(Encoding.UTF8.GetBytes("no es un libro"));
        Assert.Contains(new ErrorImportacion(0, ImportacionPersonas.CampoArchivo, PlanillaInvalidaException.NoEsUnLibro), Importacion.Revisar(danado).Errores);
    }

    [Fact(DisplayName = "administracion-local · Importar Opciones desde Excel: el importe se lee como número de Excel o como texto con coma decimal, y una franja vacía es todo el día")]
    public void El_importe_como_numero_o_como_texto()
    {
        var revision = Revisar(
        [
            ["Texto", "ALMUERZO", "1.800,50", "", "1", ""],
            ["Número", "ALMUERZO", new NumeroPlanilla(1800.5m, 2), "08:00 a 10:00; 12:00 a 14:00", "1", ""],
        ]);

        Assert.True(revision.Valida, string.Join("; ", revision.Errores));
        Importacion.Aplicar(revision, null);
        var vista = Admin.Leer();
        Assert.Equal(180050, vista.Opciones.Single(o => o.Nombre == "Texto").ImporteUnidadesMenores);
        Assert.Equal(180050, vista.Opciones.Single(o => o.Nombre == "Número").ImporteUnidadesMenores);
        Assert.Empty(vista.Opciones.Single(o => o.Nombre == "Texto").Franjas);
        Assert.Equal(2, vista.Opciones.Single(o => o.Nombre == "Número").Franjas.Count);
    }

    private object[] Fila(OpcionAdministrada opcion)
    {
        var vista = Admin.Leer();
        return
        [
            opcion.Nombre, Servicio(opcion), new NumeroPlanilla(opcion.ImporteUnidadesMenores / 100m, 2), "",
            .. vista.Categorias.Select(c => opcion.Cargas.FirstOrDefault(x => x.CategoriaId == c.Id) is { } carga ? (object)new NumeroPlanilla(carga.Unidades, 0) : string.Empty),
        ];
    }

    private static string Servicio(OpcionAdministrada opcion) => opcion.Servicio;

    private RevisionOpciones Revisar(IEnumerable<object[]> filas) => Revisar(Encabezado, filas);

    private RevisionOpciones Revisar(string[] encabezado, IEnumerable<object[]> filas)
    {
        var nombre = EscritorPlanilla.EscribirLibro(Destino, "opciones-" + Guid.NewGuid().ToString("N"),
            [new HojaPlanilla("Opciones", encabezado, filas.Select(f => (IReadOnlyList<object>)f))]);
        using var flujo = File.OpenRead(Path.Combine(Destino, nombre));
        return Importacion.Revisar(flujo);
    }

    private static IReadOnlyList<IReadOnlyList<string>> Leer(string ruta)
    {
        using var flujo = File.OpenRead(ruta);
        return LectorPlanilla.Leer(flujo);
    }
}
