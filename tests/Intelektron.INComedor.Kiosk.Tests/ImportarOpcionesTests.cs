using System.Security.Cryptography;
using System.Text.Json;
using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Exportar Opciones, la planilla base e importarlas reemplazando el catálogo, en la pantalla (administracion-local de
/// importar-opciones-desde-excel, D4 a D6). bUnit sobre el puesto de prueba con la base de la demostración.
/// </summary>
public class ImportarOpcionesTests
{
    [Fact(DisplayName = "administracion-local · Exportar el catálogo: «Exportar Excel» en el pie de Opciones deja el .xlsx con una fila por opción y la general tiene MENU_OPTIONS_EXPORTED")]
    public async Task Exportar_el_catalogo()
    {
        using var puesto = Preparar();
        var destino = PrepararCarpeta(puesto);
        var pantalla = puesto.Dibujar();
        await AbrirOpciones(pantalla);
        var opciones = puesto.Servicio.Administracion!.Leer().Opciones.Count;

        await pantalla.Find("#exportar-opciones").ClickAsync(new());

        var archivo = Assert.Single(Directory.GetFiles(destino, "INComedor-opciones-*.xlsx"));
        Assert.Equal(MensajesAtencion.ExportacionOpcionesHecha(Path.GetFileName(archivo), opciones, destino), pantalla.Find("#mensaje-archivos-opciones").TextContent.Trim());
        var contexto = ContextoDe(puesto.General().Single(l => Tipo(l) == "MENU_OPTIONS_EXPORTED"));
        Assert.Equal(opciones, contexto.GetProperty("Rows").GetInt32());
        Assert.Equal(Path.GetFileName(archivo), contexto.GetProperty("File").GetString());
    }

    [Fact(DisplayName = "administracion-local · Descargar la planilla base: «Planilla base» deja el .xlsx en la carpeta y el pie lo dice; sin carpeta, cancelar la elección avisa y no escribe nada")]
    public async Task La_planilla_base_y_sin_carpeta()
    {
        using (var puesto = Preparar())
        {
            var destino = PrepararCarpeta(puesto);
            var pantalla = puesto.Dibujar();
            await AbrirOpciones(pantalla);

            await pantalla.Find("#planilla-opciones").ClickAsync(new());

            var archivo = Assert.Single(Directory.GetFiles(destino, "INComedor-planilla-opciones-*.xlsx"));
            Assert.Equal(MensajesAtencion.PlanillaBaseHecha(Path.GetFileName(archivo), destino), pantalla.Find("#mensaje-archivos-opciones").TextContent.Trim());
        }

        using (var puesto = Preparar())
        {
            puesto.Contexto.Services.AddSingleton(new AlmacenExportacion(puesto.Carpeta));
            puesto.Contexto.Services.AddSingleton<IElectorCarpeta>(new ElectorCarpetaQueCancela());
            var pantalla = puesto.Dibujar();
            await AbrirOpciones(pantalla);

            await pantalla.Find("#exportar-opciones").ClickAsync(new());

            Assert.Equal(MensajesAtencion.SinCarpeta, pantalla.Find("#mensaje-archivos-opciones").TextContent.Trim());
            Assert.Empty(Directory.GetFiles(puesto.Carpeta, "*.xlsx", SearchOption.AllDirectories));
        }
    }

    [Fact(DisplayName = "administracion-local · Reemplazar el catálogo: el resumen cuenta una modificada, una agregada y una quitada; «Reemplazar» deja la versión siguiente, el catálogo a la vista leído de nuevo y MENU_OPTIONS_IMPORTED opción por opción")]
    public async Task Reemplazar_el_catalogo()
    {
        using var puesto = Preparar();
        var elector = Elector(puesto);
        var pantalla = puesto.Dibujar();
        var vista = puesto.Servicio.Administracion!.Leer();
        var filas = vista.Opciones.Select(o => Fila(vista, o)).ToList();
        var quitada = vista.Opciones[^1];
        filas.RemoveAt(filas.Count - 1);
        filas[0][2] = "2.000,00";
        filas.Add(["Fruta de estación nueva", "REFRIGERIO", new NumeroPlanilla(400, 2), "", .. vista.Categorias.Select(c => c.Nombre == "REFRIGERIO" ? (object)new NumeroPlanilla(1, 0) : string.Empty)]);
        var ruta = elector.Ruta = Planilla(puesto, vista, filas);
        await AbrirOpciones(pantalla);

        await pantalla.Find("#importar-opciones").ClickAsync(new());

        var resumen = pantalla.Find("#resumen-importacion-opciones");
        Assert.Contains(MensajesAtencion.ResumenImportacionOpciones(Path.GetFileName(ruta), 1, 1, vista.Opciones.Count - 2, 1), resumen.TextContent, StringComparison.Ordinal);
        Assert.Equal(["Reemplazar", "Cancelar"], resumen.QuerySelectorAll(".acciones button").Select(b => b.TextContent.Trim()));
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "MENU_OPTIONS_IMPORTED");

        await pantalla.Find("#importar-opciones-reemplazar").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#resumen-importacion-opciones"));
        Assert.Equal(MensajesAtencion.ImportacionOpcionesAplicada(vista.VersionConfiguracion + 1, 1, 1, 1), pantalla.Find("#aviso-guardado").TextContent.Trim());
        Assert.Contains(pantalla.FindAll("#catalogo-opciones .nombre-opcion"), n => n.TextContent.Trim() == "Fruta de estación nueva");
        Assert.DoesNotContain(pantalla.FindAll("#catalogo-opciones .nombre-opcion"), n => n.TextContent.Trim() == quitada.Nombre);
        var linea = puesto.General().Single(l => Tipo(l) == "MENU_OPTIONS_IMPORTED");
        var contexto = ContextoDe(linea);
        Assert.Equal("REPLACE", contexto.GetProperty("Mode").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ruta))), contexto.GetProperty("FileSha256").GetString());
        Assert.Equal((1, 1, 1), (contexto.GetProperty("Added").GetInt32(), contexto.GetProperty("Updated").GetInt32(), contexto.GetProperty("Removed").GetInt32()));
        Assert.Equal(vista.Opciones.Count + 1, contexto.GetProperty("Options").GetArrayLength());
        Assert.All(contexto.GetProperty("Options").EnumerateArray(), o => Assert.NotEqual(Guid.Empty, o.GetProperty("OptionId").GetGuid()));
        Assert.Equal(3, puesto.General().Count(l => Tipo(l) == "CONFIGURATION_CHANGED" && ContextoDe(l).GetProperty("Setting").GetString() == "MENU_OPTION"));
    }

    [Fact(DisplayName = "administracion-local · Cancelar el resumen de Opciones: la versión activa no cambia y la general no tiene MENU_OPTIONS_IMPORTED")]
    public async Task Cancelar_el_resumen()
    {
        using var puesto = Preparar();
        var elector = Elector(puesto);
        var pantalla = puesto.Dibujar();
        var vista = puesto.Servicio.Administracion!.Leer();
        elector.Ruta = Planilla(puesto, vista, [["Una sola opción", "ALMUERZO", "1.500,00", "", .. vista.Categorias.Select((_, i) => i == 0 ? (object)"1" : string.Empty)]]);
        await AbrirOpciones(pantalla);
        await pantalla.Find("#importar-opciones").ClickAsync(new());

        await pantalla.Find("#importar-opciones-cancelar").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#resumen-importacion-opciones"));
        Assert.Equal(vista.VersionConfiguracion, puesto.Servicio.Administracion.Leer().VersionConfiguracion);
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) is "MENU_OPTIONS_IMPORTED" or "MENU_OPTIONS_IMPORT_REJECTED");
    }

    [Fact(DisplayName = "administracion-local · Una importación de Opciones rechazada: las filas con error se listan con su motivo, no se puede aplicar y la general tiene MENU_OPTIONS_IMPORT_REJECTED")]
    public async Task Una_importacion_rechazada()
    {
        using var puesto = Preparar();
        var elector = Elector(puesto);
        var pantalla = puesto.Dibujar();
        var vista = puesto.Servicio.Administracion!.Leer();
        elector.Ruta = Planilla(puesto, vista,
        [
            ["", "ALMUERZO", "1.500,00", "", .. vista.Categorias.Select((_, i) => i == 0 ? (object)"1" : string.Empty)],
            ["Con franja mala", "ALMUERZO", "1.500,00", "11 a 14", .. vista.Categorias.Select((_, i) => i == 0 ? (object)"1" : string.Empty)],
        ]);
        await AbrirOpciones(pantalla);

        await pantalla.Find("#importar-opciones").ClickAsync(new());

        Assert.Equal(
            [
                MensajesAtencion.ErrorDeImportacion(new ErrorImportacion(2, "Nombre", CodigosAdministracion.Requerido)),
                MensajesAtencion.ErrorDeImportacion(new ErrorImportacion(3, "Franjas", CodigosAdministracion.HoraInvalida)),
            ],
            pantalla.FindAll("#errores-importacion-opciones li").Select(l => l.TextContent.Trim()));
        Assert.Empty(pantalla.FindAll("#importar-opciones-reemplazar"));
        var errores = ContextoDe(puesto.General().Single(l => Tipo(l) == "MENU_OPTIONS_IMPORT_REJECTED")).GetProperty("Errors");
        Assert.Equal(1, errores.GetProperty(CodigosAdministracion.Requerido).GetInt32());
        Assert.Equal(1, errores.GetProperty(CodigosAdministracion.HoraInvalida).GetInt32());

        await pantalla.Find("#importar-opciones-cerrar").ClickAsync(new());
        Assert.Empty(pantalla.FindAll("#resumen-importacion-opciones"));
    }

    [Fact(DisplayName = "administracion-local · Importar Opciones desde Excel: con INWeb como dueño de los datos, Opciones no ofrece «Importar Excel»")]
    public async Task Con_inweb_no_se_importa()
    {
        using var puesto = Preparar();
        PrepararCarpeta(puesto);
        var pantalla = puesto.Dibujar();
        using (var conexion = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False"))
        {
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "INSERT INTO tbComedorOrigenesVersiones (comvcCodigo, comvoOrigen) SELECT comvcCodigoActiva, 'INWEB' FROM tbComedorConfiguracionPuestos";
            comando.ExecuteNonQuery();
        }

        await AbrirOpciones(pantalla);

        Assert.NotNull(pantalla.Find("#exportar-opciones"));
        Assert.Empty(pantalla.FindAll("#importar-opciones"));
    }

    private static PuestoDePrueba Preparar()
    {
        var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        return puesto;
    }

    private static async Task AbrirOpciones(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-opciones").ClickAsync(new());
    }

    private static object[] Fila(VistaAdministracion vista, OpcionAdministrada opcion) =>
    [
        opcion.Nombre, opcion.Servicio, new NumeroPlanilla(opcion.ImporteUnidadesMenores / 100m, 2),
        string.Join("; ", opcion.Franjas.Select(f => $"{f.Inicio:HH\\:mm} a {f.Fin:HH\\:mm}")),
        .. vista.Categorias.Select(c => opcion.Cargas.FirstOrDefault(x => x.CategoriaId == c.Id) is { } carga ? (object)new NumeroPlanilla(carga.Unidades, 0) : string.Empty),
    ];

    /// <summary>El elector de archivos, registrado antes de dibujar; la ruta se le da después, cuando ya se leyó el catálogo.</summary>
    private static ElectorArchivoFijo Elector(PuestoDePrueba puesto)
    {
        var elector = new ElectorArchivoFijo();
        puesto.Contexto.Services.AddSingleton<IElectorArchivo>(elector);
        return elector;
    }

    /// <summary>Una planilla con el encabezado del catálogo y las filas dadas.</summary>
    private static string Planilla(PuestoDePrueba puesto, VistaAdministracion vista, IEnumerable<object[]> filas)
    {
        var carpeta = Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "planillas")).FullName;
        string[] encabezado = ["Nombre", "Servicio", "Importe", "Franjas", .. vista.Categorias.Select(c => c.Nombre + ": unidades")];
        var nombre = EscritorPlanilla.EscribirLibro(carpeta, "opciones-para-importar",
            [new HojaPlanilla("Opciones", encabezado, filas.Select(f => (IReadOnlyList<object>)f))]);
        return Path.Combine(carpeta, nombre);
    }

    private static string PrepararCarpeta(PuestoDePrueba puesto)
    {
        var destino = Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "exportaciones")).FullName;
        var almacen = new AlmacenExportacion(puesto.Carpeta);
        almacen.Guardar(destino);
        puesto.Contexto.Services.AddSingleton(almacen);
        puesto.Contexto.Services.AddSingleton<IElectorCarpeta>(new ElectorCarpetaQueNoSeUsa());
        return destino;
    }

    private sealed class ElectorArchivoFijo : IElectorArchivo
    {
        public string? Ruta { get; set; }

        public string? Elegir(string? carpeta) => Ruta;
    }

    private sealed class ElectorCarpetaQueCancela : IElectorCarpeta
    {
        public string? Elegir(string? actual) => null;
    }

    private sealed class ElectorCarpetaQueNoSeUsa : IElectorCarpeta
    {
        public string? Elegir(string? actual) => throw new InvalidOperationException("La carpeta ya estaba elegida.");
    }
}
