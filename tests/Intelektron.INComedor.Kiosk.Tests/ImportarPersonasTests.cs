using AngleSharp.Dom;
using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La planilla de ejemplo e importar Personas desde Excel en la pantalla (administracion-local de
/// importar-personas-desde-excel, D4, D6, D7 y D8). bUnit sobre el puesto de prueba con la base de la demostración: 0001 es
/// «Persona sintética 01» con ALMUERZO 1 y REFRIGERIO 2; 9001 es un operador y 9004 el administrador; 0105 no existe.
/// </summary>
public class ImportarPersonasTests
{
    private static readonly string[] Encabezado = ["Nombre", "Código", "ALMUERZO: límite", "REFRIGERIO: límite"];

    [Fact(DisplayName = "administracion-local · Descargar la planilla de ejemplo: con la carpeta elegida, «Planilla de ejemplo» deja el .xlsx y el pie lo dice")]
    public async Task Descargar_la_planilla_de_ejemplo()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        var destino = PrepararCarpeta(puesto);
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);

        await pantalla.Find("#planilla-personas").ClickAsync(new());

        var archivo = Assert.Single(Directory.GetFiles(destino));
        Assert.StartsWith("INComedor-planilla-personas-", Path.GetFileName(archivo), StringComparison.Ordinal);
        Assert.EndsWith(".xlsx", archivo, StringComparison.Ordinal);
        Assert.Equal(MensajesAtencion.PlanillaDeEjemploHecha(Path.GetFileName(archivo), destino), pantalla.Find("#mensaje-exportacion-personas").TextContent.Trim());
    }

    [Fact(DisplayName = "administracion-local · Sin carpeta para la planilla: cancelar la elección avisa en el pie y no escribe nada")]
    public async Task Sin_carpeta_para_la_planilla()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        puesto.Contexto.Services.AddSingleton(new AlmacenExportacion(puesto.Carpeta));
        puesto.Contexto.Services.AddSingleton<IElectorCarpeta>(new ElectorCarpetaQueCancela());
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);

        await pantalla.Find("#planilla-personas").ClickAsync(new());

        Assert.Equal(MensajesAtencion.SinCarpeta, pantalla.Find("#mensaje-exportacion-personas").TextContent.Trim());
        Assert.Empty(Directory.GetFiles(puesto.Carpeta, "*.xlsx", SearchOption.AllDirectories));
    }

    [Fact(DisplayName = "administracion-local · Importar Personas desde Excel: «Importar Excel» muestra el resumen con los dos modos y «Cancelar», sin aplicar nada")]
    public async Task Importar_muestra_el_resumen()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        var ruta = Planilla(puesto, ["Persona sintética 01", "0001", "1", "1"], ["Persona sintética 05", "0105", "1", ""]);
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);

        await pantalla.Find("#importar-personas").ClickAsync(new());

        var resumen = pantalla.Find("#resumen-importacion");
        Assert.Contains(MensajesAtencion.ResumenImportacion(Path.GetFileName(ruta), 1, 1, 0), resumen.TextContent, StringComparison.Ordinal);
        Assert.Contains(MensajesAtencion.ConsumosDeHoyAlImportar, resumen.TextContent, StringComparison.Ordinal);
        Assert.Equal(["Modificar y agregar", "Reemplazar", "Cancelar"], resumen.QuerySelectorAll(".acciones button").Select(b => b.TextContent.Trim()));
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) is "PEOPLE_IMPORTED" or "CONFIGURATION_CHANGED");
    }

    [Fact(DisplayName = "administracion-local · Modificar y agregar: la versión 2 tiene a 0001 con REFRIGERIO 1 y a «Persona sintética 05», y la general, PEOPLE_IMPORTED MERGE persona por persona sin nombres ni códigos")]
    public async Task Modificar_y_agregar()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        var ruta = Planilla(puesto, ["Persona sintética 01", "0001", "1", "1"], ["Persona sintética 05", "0105", "1", ""]);
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);
        await pantalla.Find("#importar-personas").ClickAsync(new());

        await pantalla.Find("#importar-modificar").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#resumen-importacion"));
        Assert.Equal(MensajesAtencion.ImportacionAplicada(2, 1, 1, 0), pantalla.Find("#aviso-guardado").TextContent.Trim());
        Assert.Equal("0/1", Celda(Fila(pantalla, "Persona sintética 01"), "REFRIGERIO"));
        Assert.Equal("0/1", Celda(Fila(pantalla, "Persona sintética 05"), "ALMUERZO"));
        Assert.Equal("Activa", Fila(pantalla, "Persona sintética 02").QuerySelector("td.estado-persona")!.TextContent.Trim());

        var linea = puesto.General().Single(l => Tipo(l) == "PEOPLE_IMPORTED");
        var contexto = ContextoDe(linea);
        Assert.Equal("MERGE", contexto.GetProperty("Mode").GetString());
        // La huella del archivo y no su nombre, que lo escribe cualquiera y puede tener un código (D8).
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(ruta))), contexto.GetProperty("FileSha256").GetString());
        Assert.False(contexto.TryGetProperty("File", out _));
        Assert.Equal(1, contexto.GetProperty("Added").GetInt32());
        Assert.Equal(1, contexto.GetProperty("Updated").GetInt32());
        Assert.Equal(0, contexto.GetProperty("Deactivated").GetInt32());
        Assert.Equal(2, contexto.GetProperty("Version").GetInt32());
        var personas = contexto.GetProperty("People").EnumerateArray().ToList();
        Assert.Equal(["ADDED", "UPDATED"], personas.Select(p => p.GetProperty("Result").GetString()).Order(StringComparer.Ordinal));
        Assert.All(personas, p => Assert.NotEqual(Guid.Empty, p.GetProperty("PersonId").GetGuid()));
        Assert.All(new[] { "Persona sintética", "\"0001\"", "\"0105\"" }, t => Assert.DoesNotContain(t, linea.GetRawText(), StringComparison.Ordinal));
        Assert.Equal(2, puesto.General().Count(l => Tipo(l) == "CONFIGURATION_CHANGED" && ContextoDe(l).GetProperty("Setting").GetString() == "PERSON"));
    }

    [Fact(DisplayName = "administracion-local · Reemplazar conserva a los operadores: con un archivo sólo con 0001, quedan de baja las cuatro personas sin permiso y el operador de permiso revocado (9003); siguen activos 9001, 9002 (permiso vencido) y el administrador")]
    public async Task Reemplazar_conserva_a_los_operadores()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        Planilla(puesto, ["Persona sintética 01", "0001", "1", "2"]);
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);
        await pantalla.Find("#importar-personas").ClickAsync(new());

        await pantalla.Find("#importar-reemplazar").ClickAsync(new());

        string[] deBaja = ["Persona de demostración", "Persona sintética 02", "Persona sintética 03", "Persona sintética 04", "Operador sintético 03"];
        string[] activas = ["Persona sintética 01", "Operador sintético 01", "Operador sintético 02", "Administrador sintético 04"];
        Assert.All(deBaja, n => Assert.Equal("De baja", Fila(pantalla, n).QuerySelector("td.estado-persona")!.TextContent.Trim()));
        Assert.All(activas, n => Assert.Equal("Activa", Fila(pantalla, n).QuerySelector("td.estado-persona")!.TextContent.Trim()));
        var contexto = ContextoDe(puesto.General().Single(l => Tipo(l) == "PEOPLE_IMPORTED"));
        Assert.Equal("REPLACE", contexto.GetProperty("Mode").GetString());
        Assert.Equal(deBaja.Length, contexto.GetProperty("Deactivated").GetInt32());
        Assert.Equal(contexto.GetProperty("Deactivated").GetInt32(),
            contexto.GetProperty("People").EnumerateArray().Count(p => p.GetProperty("Result").GetString() == "DEACTIVATED"));
    }

    [Fact(DisplayName = "administracion-local · Cancelar el resumen: la versión activa no cambia y la general no tiene PEOPLE_IMPORTED")]
    public async Task Cancelar_el_resumen()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        Planilla(puesto, ["Persona sintética 05", "0105", "1", ""]);
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);
        await pantalla.Find("#importar-personas").ClickAsync(new());

        await pantalla.Find("#importar-cancelar").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#resumen-importacion"));
        Assert.DoesNotContain(pantalla.FindAll("tr.persona"), f => f.TextContent.Contains("Persona sintética 05", StringComparison.Ordinal));
        Assert.Equal(1, puesto.Servicio.Administracion!.Leer().VersionConfiguracion);
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) is "PEOPLE_IMPORTED" or "PEOPLE_IMPORT_REJECTED");
    }

    [Fact(DisplayName = "administracion-local · Una importación rechazada: una fila sin código y otra de un código revocado se listan con su motivo, no se puede aplicar y la general tiene PEOPLE_IMPORT_REJECTED")]
    public async Task Una_importacion_rechazada()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        Planilla(puesto, ["Persona sintética 05", "", "1", ""], ["Persona sintética 03", "0003", "1", ""]);
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);

        await pantalla.Find("#importar-personas").ClickAsync(new());

        var resumen = pantalla.Find("#resumen-importacion");
        Assert.Contains(MensajesAtencion.ImportacionConErrores(2), resumen.TextContent, StringComparison.Ordinal);
        Assert.Equal(
            [
                MensajesAtencion.ErrorDeImportacion(new ErrorImportacion(2, "Código", CodigosAdministracion.Requerido)),
                MensajesAtencion.ErrorDeImportacion(new ErrorImportacion(3, "Código", ImportacionPersonas.CodigoDadoDeBaja)),
            ],
            resumen.QuerySelectorAll("#errores-importacion li").Select(l => l.TextContent.Trim()));
        Assert.Empty(pantalla.FindAll("#importar-modificar"));
        Assert.Empty(pantalla.FindAll("#importar-reemplazar"));
        var linea = puesto.General().Single(l => Tipo(l) == "PEOPLE_IMPORT_REJECTED");
        Assert.Equal(64, ContextoDe(linea).GetProperty("FileSha256").GetString()!.Length);
        var errores = ContextoDe(linea).GetProperty("Errors");
        Assert.Equal(1, errores.GetProperty(CodigosAdministracion.Requerido).GetInt32());
        Assert.Equal(1, errores.GetProperty(ImportacionPersonas.CodigoDadoDeBaja).GetInt32());
        Assert.All(new[] { "Persona sintética", "\"0003\"" }, t => Assert.DoesNotContain(t, linea.GetRawText(), StringComparison.Ordinal));
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "PEOPLE_IMPORTED");

        await pantalla.Find("#importar-cerrar").ClickAsync(new());
        Assert.Empty(pantalla.FindAll("#resumen-importacion"));
    }

    [Fact(DisplayName = "administracion-local · Un archivo que no es una planilla: un .xlsx dañado se lista como un error del archivo y no se aplica nada")]
    public async Task Un_archivo_danado()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        var ruta = Path.Combine(Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "planillas")).FullName, "danada.xlsx");
        File.WriteAllText(ruta, "no es un libro");
        puesto.Contexto.Services.AddSingleton<IElectorArchivo>(new ElectorArchivoFijo(ruta));
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);

        await pantalla.Find("#importar-personas").ClickAsync(new());

        Assert.Equal([MensajesAtencion.ErrorDeImportacion(new ErrorImportacion(0, ImportacionPersonas.CampoArchivo, PlanillaInvalidaException.NoEsUnLibro))],
            pantalla.FindAll("#errores-importacion li").Select(l => l.TextContent.Trim()));
        Assert.Equal(1, puesto.Servicio.Administracion!.Leer().VersionConfiguracion);
    }

    [Fact(DisplayName = "administracion-local · Importar Personas desde Excel: cancelar la elección del archivo no muestra el resumen ni registra nada")]
    public async Task Cancelar_la_eleccion_del_archivo()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        puesto.Contexto.Services.AddSingleton<IElectorArchivo>(new ElectorArchivoFijo(null));
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);

        await pantalla.Find("#importar-personas").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#resumen-importacion"));
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) is "PEOPLE_IMPORTED" or "PEOPLE_IMPORT_REJECTED");
    }

    [Fact(DisplayName = "administracion-local · Los consumos de hoy siguen contando: con un almuerzo de 0001 hoy, importar ALMUERZO 1 deja su fila en «1/1»")]
    public async Task Los_consumos_de_hoy_siguen_contando()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        Planilla(puesto, ["Persona sintética 01", "0001", "1", "1"]);
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla);
        await AbrirPersonas(pantalla);
        await pantalla.Find("#importar-personas").ClickAsync(new());

        await pantalla.Find("#importar-modificar").ClickAsync(new());

        Assert.Equal("1/1", Celda(Fila(pantalla, "Persona sintética 01"), "ALMUERZO"));
        Assert.Equal("0/1", Celda(Fila(pantalla, "Persona sintética 01"), "REFRIGERIO"));

        // Y su oferta de hoy ya no tiene almuerzos: el límite nuevo cuenta lo consumido (RB03).
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        Assert.DoesNotContain(pantalla.FindAll(".opcion"), o => o.TextContent.Contains("Almuerzo", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "administracion-local · Importar Personas desde Excel: Escape con el resumen abierto cierra Configuración, como siempre, y no aplica nada")]
    public async Task Escape_con_el_resumen_abierto()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        Planilla(puesto, ["Persona sintética 05", "0105", "1", ""]);
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);
        await pantalla.Find("#importar-personas").ClickAsync(new());

        await pantalla.Find("#importar-modificar").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(pantalla.FindAll("[role=\"dialog\"]"));
        Assert.Equal(1, puesto.Servicio.Administracion!.Leer().VersionConfiguracion);
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "PEOPLE_IMPORTED");
    }

    [Fact(DisplayName = "administracion-local · Quién es quién: una credencial vencida (0002) o que todavía no rige (0004) de una persona activa la reconoce y la corrige, sin tocar las fechas de la credencial")]
    public async Task Credenciales_vencidas_o_futuras()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        Planilla(puesto, ["Persona sintética 02 corregida", "0002", "1", ""], ["Persona sintética 04 corregida", "0004", "1", ""]);
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);
        await pantalla.Find("#importar-personas").ClickAsync(new());
        Assert.Contains(MensajesAtencion.ResumenImportacion("personas-para-importar.xlsx", 0, 2, 0), pantalla.Find("#resumen-importacion").TextContent, StringComparison.Ordinal);

        await pantalla.Find("#importar-modificar").ClickAsync(new());

        Assert.Equal("0/1", Celda(Fila(pantalla, "Persona sintética 02 corregida"), "ALMUERZO"));
        Assert.Equal("0/1", Celda(Fila(pantalla, "Persona sintética 04 corregida"), "ALMUERZO"));
        using var conexion = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            SELECT c.comcreValorNormalizado || ' ' || IFNULL(c.comcreFechaVigenciaDesdeUtc, '-') || ' ' || IFNULL(c.comcreFechaVigenciaHastaUtc, '-')
            FROM tbComedorCredencialesPersonas c JOIN tbComedorConfiguracionPuestos p ON p.comvcCodigoActiva = c.comvcCodigo
            WHERE c.comcreValorNormalizado IN ('0002', '0004') ORDER BY 1
            """;
        using var lector = comando.ExecuteReader();
        var fechas = new List<string>();
        while (lector.Read())
        {
            fechas.Add(lector.GetString(0));
        }

        Assert.Equal(["0002 - 2026-10-15T03:00:00.000Z", "0004 2030-01-01T03:00:00.000Z -"], fechas);
    }

    [Fact(DisplayName = "administracion-local · Importar Personas desde Excel: con el código de un operador (9001), PEOPLE_IMPORTED y el CONFIGURATION_CHANGED de cada persona lo tienen como actor")]
    public async Task El_operador_es_el_actor()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        Planilla(puesto, ["Persona sintética 05", "0105", "1", ""]);
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla, "9001");
        await pantalla.Find("#pestana-personas").ClickAsync(new());
        await pantalla.Find("#importar-personas").ClickAsync(new());

        await pantalla.Find("#importar-modificar").ClickAsync(new());

        using var conexion = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT comperCodigo FROM tbComedorPersonas WHERE comperNombreVisible = 'Operador sintético 01' LIMIT 1";
        var operador = Guid.Parse((string)comando.ExecuteScalar()!);
        Assert.Equal(operador, ContextoDe(puesto.General().Single(l => Tipo(l) == "PEOPLE_IMPORTED")).GetProperty("ActorPersonId").GetGuid());
        var cambio = ContextoDe(puesto.General().Single(l => Tipo(l) == "CONFIGURATION_CHANGED" && ContextoDe(l).GetProperty("Setting").GetString() == "PERSON"));
        Assert.Equal(operador, cambio.GetProperty("ActorPersonId").GetGuid());
    }

    [Fact(DisplayName = "administracion-local · Importar Personas desde Excel: el resumen avisa que quedan filas de la planilla de ejemplo y códigos guardados como número")]
    public async Task El_resumen_avisa_filas_de_ejemplo_y_codigos_como_numero()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        PlanillaConCeldas(puesto, ["Persona de ejemplo 1", new NumeroPlanilla(105, 0), "1", ""]);
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);

        await pantalla.Find("#importar-personas").ClickAsync(new());

        Assert.Equal(MensajesAtencion.ImportacionConFilasDeEjemplo(1), pantalla.Find("#importacion-ejemplo").TextContent.Trim());
        Assert.Equal(MensajesAtencion.ImportacionCodigosComoNumero(1), pantalla.Find("#importacion-numeros").TextContent.Trim());
    }

    private static async Task AbrirPersonas(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-personas").ClickAsync(new());
    }

    private static IElement Fila(IRenderedComponent<PantallaAtencion> pantalla, string nombre) =>
        pantalla.FindAll("tr.persona").Single(f => f.QuerySelector(".nombre-persona")!.TextContent.Trim() == nombre);

    private static string Celda(IElement fila, string cupo) => fila.QuerySelector($"td.cupo[data-cupo=\"{cupo}\"]")!.TextContent.Trim();

    /// <summary>Un almuerzo de 0001.</summary>
    private static async Task Consumir(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion").First(o => o.TextContent.Contains("Almuerzo", StringComparison.Ordinal)).ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        await pantalla.Find("#listo").ClickAsync(new());
    }

    /// <summary>Una planilla con el encabezado de la demostración y las filas dadas, y el elector que la devuelve, antes de dibujar.</summary>
    private static string Planilla(PuestoDePrueba puesto, params string[][] filas) =>
        PlanillaConCeldas(puesto, filas.Select(f => f.Cast<object>().ToArray()).ToArray());

    /// <summary>Como <see cref="Planilla"/>, con celdas de texto o números (<see cref="NumeroPlanilla"/>), como las guarda Excel.</summary>
    private static string PlanillaConCeldas(PuestoDePrueba puesto, params object[][] filas)
    {
        var carpeta = Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "planillas")).FullName;
        var nombre = EscritorPlanilla.EscribirLibro(carpeta, "personas-para-importar",
            [new HojaPlanilla("Personas", Encabezado, filas.Select(f => (IReadOnlyList<object>)f), [1])]);
        var ruta = Path.Combine(carpeta, nombre);
        puesto.Contexto.Services.AddSingleton<IElectorArchivo>(new ElectorArchivoFijo(ruta));
        return ruta;
    }

    /// <summary>La carpeta de exportación ya elegida y un elector de carpetas que no se usa.</summary>
    private static string PrepararCarpeta(PuestoDePrueba puesto)
    {
        var destino = Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "exportaciones")).FullName;
        var almacen = new AlmacenExportacion(puesto.Carpeta);
        almacen.Guardar(destino);
        puesto.Contexto.Services.AddSingleton(almacen);
        puesto.Contexto.Services.AddSingleton<IElectorCarpeta>(new ElectorCarpetaQueNoSeUsa());
        return destino;
    }

    private sealed class ElectorArchivoFijo(string? ruta) : IElectorArchivo
    {
        public string? Elegir(string? carpeta) => ruta;
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
