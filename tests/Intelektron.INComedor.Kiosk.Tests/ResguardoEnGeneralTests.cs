using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// «Guardar resguardo» y «Recuperar resguardo» al final de General, el resumen con «Recuperar» y «Cancelar» y los tres
/// registros de la general (configuracion-puesto de resguardar-configuracion, D4 y D5). bUnit sobre el puesto de prueba con
/// la base de la demostración; el resguardo que se recupera sale de otro puesto con la misma semilla.
/// </summary>
public class ResguardoEnGeneralTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    private static readonly string[] Eventos = ["CONFIGURATION_BACKUP_CREATED", "CONFIGURATION_RESTORED", "CONFIGURATION_RESTORE_REJECTED"];

    [Fact(DisplayName = "configuracion-puesto · Guardar el resguardo: «Guardar resguardo» deja el .zip en la carpeta de la exportación, General lo dice y la general tiene CONFIGURATION_BACKUP_CREATED con su huella, sin códigos ni nombres")]
    public async Task Guardar_el_resguardo()
    {
        using var puesto = Preparar();
        var destino = PrepararCarpeta(puesto);
        var pantalla = puesto.Dibujar();
        await AbrirGeneral(pantalla);

        await pantalla.Find("#guardar-resguardo").ClickAsync(new());

        var archivo = Assert.Single(Directory.GetFiles(destino, "INComedor-resguardo-*.zip"));
        Assert.Equal(MensajesAtencion.ResguardoGuardado(Path.GetFileName(archivo), destino), pantalla.Find("#mensaje-resguardo").TextContent.Trim());
        var contexto = ContextoDe(puesto.General().Single(l => Tipo(l) == "CONFIGURATION_BACKUP_CREATED"));
        Assert.Equal(Path.GetFileName(archivo), contexto.GetProperty("File").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivo))), contexto.GetProperty("FileSha256").GetString());
        Assert.Equal(1, contexto.GetProperty("Version").GetInt32());
        Assert.True(SinActor(contexto));
        SinCodigosNiNombres(puesto);
    }

    [Fact(DisplayName = "configuracion-puesto · Sin carpeta para el resguardo: cancelar la elección de la carpeta avisa en General y no escribe nada")]
    public async Task Sin_carpeta()
    {
        using var puesto = Preparar();
        puesto.Contexto.Services.AddSingleton(new AlmacenExportacion(puesto.Carpeta));
        puesto.Contexto.Services.AddSingleton<IElectorCarpeta>(new ElectorCarpetaQueCancela());
        var pantalla = puesto.Dibujar();
        await AbrirGeneral(pantalla);

        await pantalla.Find("#guardar-resguardo").ClickAsync(new());

        Assert.Equal(MensajesAtencion.SinCarpetaResguardo, pantalla.Find("#mensaje-resguardo").TextContent.Trim());
        Assert.Empty(Directory.GetFiles(puesto.Carpeta, "*.zip", SearchOption.AllDirectories));
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "CONFIGURATION_BACKUP_CREATED");
    }

    [Fact(DisplayName = "configuracion-puesto · Recuperar en otro puesto: el resumen dice versión, personas, opciones, permisos y ajustes; «Recuperar» deja la versión nueva, la pantalla con el estilo y el logo del resguardo y CONFIGURATION_RESTORED sin códigos ni nombres")]
    public async Task Recuperar_en_otro_puesto()
    {
        using var puesto = Preparar();
        var elector = Elector(puesto);
        var pantalla = puesto.Dibujar();
        var zip = elector.Ruta = ResguardoDeOtroPuesto(puesto);
        var revisado = Revisar(puesto, zip);
        await AbrirGeneral(pantalla);

        await pantalla.Find("#recuperar-resguardo").ClickAsync(new());

        Assert.Equal(MensajesAtencion.ResumenResguardo(Path.GetFileName(zip), revisado, Zona), pantalla.Find("#resguardo-origen").TextContent.Trim());
        // Los conteos, contra la base y la semilla: las personas activas, las opciones y un solo permiso de operador vigente (9001).
        var vista = puesto.Servicio.Administracion!.Leer();
        Assert.Contains($"{vista.Personas.Count(p => p.Activa)} personas activas, {vista.Opciones.Count} opciones y 1 permiso de operador vigente.",
            pantalla.Find("#resguardo-origen").TextContent, StringComparison.Ordinal);
        Assert.Equal(MensajesAtencion.AjustesDelResguardo(revisado), pantalla.Find("#resguardo-ajustes").TextContent.Trim());
        Assert.Contains("principal", pantalla.Find("#resguardo-cancelar").ClassList);

        await pantalla.Find("#resguardo-recuperar").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#resumen-resguardo"));
        Assert.Equal(MensajesAtencion.ResguardoRecuperado(2), pantalla.Find("#aviso-guardado").TextContent.Trim());
        Assert.Equal(2, puesto.Servicio.Administracion!.Leer().VersionConfiguracion);
        Assert.Equal(new EstiloVisual(80, 6).VariablesCss, pantalla.Find(".puesto").GetAttribute("style"));
        Assert.Equal(new LogoElegido(".png", "image/png", "PNG", Png).UriData, pantalla.Find(".logo-empresa").GetAttribute("src"));
        var contexto = ContextoDe(puesto.General().Single(l => Tipo(l) == "CONFIGURATION_RESTORED"));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip))), contexto.GetProperty("FileSha256").GetString());
        Assert.Equal(1, contexto.GetProperty("SourceVersion").GetInt32());
        Assert.Equal(2, contexto.GetProperty("Version").GetInt32());
        SinCodigosNiNombres(puesto);
    }

    [Fact(DisplayName = "configuracion-puesto · Cancelar la recuperación: «Cancelar» cierra el resumen, la versión activa y el estilo no cambian y la general no tiene CONFIGURATION_RESTORED")]
    public async Task Cancelar_la_recuperacion()
    {
        using var puesto = Preparar();
        var elector = Elector(puesto);
        var pantalla = puesto.Dibujar();
        elector.Ruta = ResguardoDeOtroPuesto(puesto);
        await AbrirGeneral(pantalla);
        await pantalla.Find("#recuperar-resguardo").ClickAsync(new());

        await pantalla.Find("#resguardo-cancelar").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#resumen-resguardo"));
        Assert.Equal(1, puesto.Servicio.Administracion!.Leer().VersionConfiguracion);
        Assert.Equal(EstiloVisual.PorDefecto.VariablesCss, pantalla.Find(".puesto").GetAttribute("style"));
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "CONFIGURATION_RESTORED");
    }

    [Fact(DisplayName = "configuracion-puesto · Un resguardo alterado o de otro esquema: el resumen dice el motivo con «Cerrar», la versión no cambia y la general tiene CONFIGURATION_RESTORE_REJECTED con el motivo")]
    public async Task Un_resguardo_rechazado()
    {
        using var puesto = Preparar();
        var elector = Elector(puesto);
        var pantalla = puesto.Dibujar();
        var archivo = elector.Ruta = Path.Combine(puesto.Carpeta, "no-es-un-resguardo.zip");
        File.WriteAllText(archivo, "no soy un zip");
        await AbrirGeneral(pantalla);

        await pantalla.Find("#recuperar-resguardo").ClickAsync(new());

        Assert.Equal(MensajesAtencion.ResguardoRechazado(Path.GetFileName(archivo), MotivosResguardo.NoEsUnZip), pantalla.Find("#resguardo-rechazado").TextContent.Trim());
        Assert.Empty(pantalla.FindAll("#resguardo-recuperar"));
        await pantalla.Find("#resguardo-cerrar").ClickAsync(new());
        Assert.Empty(pantalla.FindAll("#resumen-resguardo"));
        Assert.Equal(1, puesto.Servicio.Administracion!.Leer().VersionConfiguracion);
        var contexto = ContextoDe(puesto.General().Single(l => Tipo(l) == "CONFIGURATION_RESTORE_REJECTED"));
        Assert.Equal(MotivosResguardo.NoEsUnZip, contexto.GetProperty("ReasonCode").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivo))), contexto.GetProperty("FileSha256").GetString());
    }

    [Fact(DisplayName = "configuracion-puesto · Recuperar un resguardo: con INWeb como dueño de los datos, General ofrece guardar el resguardo pero no recuperarlo")]
    public async Task Con_inweb_no_se_recupera()
    {
        using var puesto = Preparar();
        PrepararCarpeta(puesto);
        Elector(puesto);
        var pantalla = puesto.Dibujar();
        using (var conexion = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False"))
        {
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "INSERT INTO tbComedorOrigenesVersiones (comvcCodigo, comvoOrigen) SELECT comvcCodigoActiva, 'INWEB' FROM tbComedorConfiguracionPuestos";
            comando.ExecuteNonQuery();
        }

        await AbrirGeneral(pantalla);

        Assert.Single(pantalla.FindAll("#guardar-resguardo"));
        Assert.Empty(pantalla.FindAll("#recuperar-resguardo"));
    }

    [Fact(DisplayName = "configuracion-puesto · Recuperar un resguardo: con la edición de Personas abierta, General no ofrece recuperar, como no se ofrece importar, para no mezclar lo editado con lo recuperado")]
    public async Task Con_una_edicion_abierta()
    {
        using var puesto = Preparar();
        PrepararCarpeta(puesto);
        Elector(puesto);
        var pantalla = puesto.Dibujar();
        await AbrirGeneral(pantalla);
        Assert.Single(pantalla.FindAll("#recuperar-resguardo"));

        await pantalla.Find("#abrir-personas").ClickAsync(new());
        await pantalla.Find("#pestana-general").ClickAsync(new());

        Assert.Single(pantalla.FindAll("#guardar-resguardo"));
        Assert.Empty(pantalla.FindAll("#recuperar-resguardo"));
    }

    [Fact(DisplayName = "configuracion-puesto · Un resguardo alterado o de otro esquema: una parte cambiada y otro esquema se rechazan con su motivo en el resumen y en CONFIGURATION_RESTORE_REJECTED, sin cambiar la versión")]
    public async Task Alterado_y_de_otro_esquema()
    {
        using var puesto = Preparar();
        var elector = Elector(puesto);
        var pantalla = puesto.Dibujar();
        var valido = ResguardoDeOtroPuesto(puesto);
        var casos = new (string Archivo, string Motivo)[]
        {
            (ResguardosDePrueba.Rehacer(valido, Path.Combine(puesto.Carpeta, "parte-cambiada.zip"),
                p => p["impresora.json"] = """{"encabezado":"Otro","pie":""}"""u8.ToArray(), recalcular: false), MotivosResguardo.HuellaDistinta),
            (ResguardosDePrueba.Rehacer(valido, Path.Combine(puesto.Carpeta, "otro-esquema.zip"),
                _ => { }, m => m["esquema"] = AdministracionLocal.VersionEsquema + 1), MotivosResguardo.OtroEsquema),
        };
        await AbrirGeneral(pantalla);

        foreach (var (archivo, motivo) in casos)
        {
            elector.Ruta = archivo;
            await pantalla.Find("#recuperar-resguardo").ClickAsync(new());
            Assert.Equal(MensajesAtencion.ResguardoRechazado(Path.GetFileName(archivo), motivo), pantalla.Find("#resguardo-rechazado").TextContent.Trim());
            await pantalla.Find("#resguardo-cerrar").ClickAsync(new());
        }

        Assert.Equal(1, puesto.Servicio.Administracion!.Leer().VersionConfiguracion);
        var rechazos = puesto.General().Where(l => Tipo(l) == "CONFIGURATION_RESTORE_REJECTED").Select(l => ContextoDe(l).GetProperty("ReasonCode").GetString());
        Assert.Equal(casos.Select(c => c.Motivo), rechazos);
    }

    private static PuestoDePrueba Preparar()
    {
        var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        puesto.Contexto.Services.AddSingleton(new AlmacenPresentacionOpciones(puesto.Carpeta));
        return puesto;
    }

    private static async Task AbrirGeneral(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-general").ClickAsync(new());
    }

    /// <summary>El elector de archivos, registrado antes de dibujar; la ruta se le da después.</summary>
    private static ElectorArchivoFijo Elector(PuestoDePrueba puesto)
    {
        var elector = new ElectorArchivoFijo();
        puesto.Contexto.Services.AddSingleton<IElectorArchivo>(elector);
        return elector;
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

    /// <summary>
    /// Un resguardo de otro puesto con la misma semilla, guardado con un estilo, unos textos y el logo de la empresa propios;
    /// ese puesto se cierra antes de volver.
    /// </summary>
    private static string ResguardoDeOtroPuesto(PuestoDePrueba puesto)
    {
        var carpeta = Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "otro-puesto")).FullName;
        using var servicio = new ServicioAtencion(Path.Combine(carpeta, "incomedor.db"), new RelojManual("2026-11-06T15:00:00Z"), new ImpresoraDePrueba());
        Assert.Equal(Demostracion.EstadoPreparacion.Lista, servicio.Preparar());
        var estilo = new AlmacenEstiloVisual(carpeta);
        var impresora = new AlmacenImpresora(carpeta);
        var logos = new LogosLocales(carpeta);
        estilo.Guardar(new EstiloVisual(80, 6));
        impresora.Guardar(new TextosComprobante("Comedor de prueba", "Buen provecho."));
        logos.Guardar(LogoPuesto.Empresa, LogosLocales.Validar("empresa.png", Png.Length, Png).Logo!);
        var resguardo = new ResguardoPuesto(servicio.Administracion!, estilo, impresora, logos, new AlmacenPresentacionOpciones(carpeta));
        return resguardo.Guardar(Path.Combine(puesto.Carpeta, "resguardos"), TimeZoneInfo.Utc).Ruta;
    }

    /// <summary>Lo que el resumen tiene que decir, revisado con los almacenes del puesto, sin escribir nada.</summary>
    private static ResguardoRevisado Revisar(PuestoDePrueba puesto, string zip) =>
        new ResguardoPuesto(puesto.Servicio.Administracion!, new AlmacenEstiloVisual(puesto.Carpeta), new AlmacenImpresora(puesto.Carpeta),
            new LogosLocales(puesto.Carpeta), new AlmacenPresentacionOpciones(puesto.Carpeta)).Revisar(zip).Resguardo!;

    /// <summary>Ningún registro del resguardo lleva un código de identificación ni un nombre de persona (D5).</summary>
    private static void SinCodigosNiNombres(PuestoDePrueba puesto)
    {
        var configuracion = JsonNode.Parse(puesto.Servicio.Administracion!.LeerConfiguracion().Json)!;
        var codigos = configuracion["tbComedorCredencialesPersonas"]!.AsArray().Select(c => (string)c!["comcreValorNormalizado"]!).ToList();
        var nombres = configuracion["tbComedorPersonas"]!.AsArray().Select(p => (string)p!["comperNombreVisible"]!).ToList();
        Assert.NotEmpty(codigos);
        foreach (var linea in puesto.General().Where(l => Eventos.Contains(Tipo(l))))
        {
            var texto = JsonSerializer.Serialize(ContextoDe(linea));
            Assert.DoesNotContain(codigos, c => texto.Contains('"' + c + '"', StringComparison.Ordinal));
            Assert.DoesNotContain(nombres, n => texto.Contains(n, StringComparison.Ordinal));
        }
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
