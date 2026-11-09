using System.Text;
using System.Text.Json;
using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Las bitácoras del puesto de punta a punta dentro del Kiosk (escribir-bitacoras-externas, D8 y D9): la máquina de
/// atención, la pantalla con bUnit y la pasada del escritor sobre una carpeta de datos temporal propia por caso, con la
/// base adentro. Se lee sólo el archivo, nunca la base, para comprobar que alcanza para reconstruir.
/// </summary>
public class BitacorasDelPuestoTests : IDisposable
{
    private static readonly Guid AlmuerzoComun = SemillaAtencion.PrimeraOpcionId;

    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "incomedor-tests", "bitacoras-puesto-" + Guid.NewGuid().ToString("N"));
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-11-06T15:00:00Z");
    private ServicioAtencion? _servicio;

    public BitacorasDelPuestoTests()
    {
        Directory.CreateDirectory(_carpeta);
    }

    private string RutaBase => Path.Combine(_carpeta, "incomedor.db");

    public void Dispose()
    {
        _contexto.Dispose();
        _servicio?.Dispose();
        foreach (var archivo in Directory.EnumerateFiles(_carpeta, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(archivo, FileAttributes.Normal);
        }

        Directory.Delete(_carpeta, recursive: true);
    }

    [Fact(DisplayName = "bitacoras-externas · Atención autorizada: el archivo de interacciones dice, en orden y con la misma sesión, SESSION_OPENED, OPTIONS_OFFERED, OPTION_SELECTED con la etiqueta y el importe, CONFIRMATION_REQUESTED, CONSUMPTION_AUTHORIZED y SESSION_CLOSED con el mensaje de éxito")]
    public async Task Atencion_autorizada()
    {
        var maquina = NuevaMaquina();
        Assert.Equal(ResultadoToque.Aceptado, maquina.Identificar("0001"));
        maquina.Elegir(AlmuerzoComun);
        await maquina.ConfirmarAsync();
        Assert.True(maquina.ConsumoRegistrado);
        maquina.Listo();

        var lineas = DeLaSesion(Entregar(DestinoBitacora.Interacciones));

        Assert.Equal(["SESSION_OPENED", "OPTIONS_OFFERED", "OPTION_SELECTED", "CONFIRMATION_REQUESTED", "CONSUMPTION_AUTHORIZED", "SESSION_CLOSED"],
            lineas.Select(Tipo));
        var elegida = lineas[2].GetProperty("Context");
        Assert.Equal(AlmuerzoComun, elegida.GetProperty("MenuItemId").GetGuid());
        Assert.Equal("Almuerzo común", elegida.GetProperty("MenuLabel").GetString());
        Assert.Equal("ARS 1.500,00", elegida.GetProperty("DisplayedCharge").GetString());
        Assert.Equal(maquina.IntentId, lineas[3].GetProperty("Context").GetProperty("IntentId").GetGuid());
        Assert.Equal(maquina.IntentId, lineas[4].GetProperty("Context").GetProperty("IntentId").GetGuid());
        var cierre = lineas[5].GetProperty("Context");
        Assert.Equal(nameof(MensajesAtencion.Exito), cierre.GetProperty("MessageCode").GetString());
        Assert.Equal(lineas[4].GetProperty("Context").GetProperty("DecisionId").GetGuid(), cierre.GetProperty("DecisionId").GetGuid());
    }

    [Fact(DisplayName = "bitacoras-externas · Atención cancelada: termina en SESSION_CANCELLED con la opción que tenía elegida, sin CONFIRMATION_REQUESTED ni consumo")]
    public void Atencion_cancelada()
    {
        var maquina = NuevaMaquina();
        maquina.Identificar("0001");
        maquina.Elegir(AlmuerzoComun);

        maquina.Cancelar();

        var lineas = DeLaSesion(Entregar(DestinoBitacora.Interacciones));
        Assert.Equal(["SESSION_OPENED", "OPTIONS_OFFERED", "OPTION_SELECTED", "SESSION_CANCELLED"], lineas.Select(Tipo));
        Assert.Equal(AlmuerzoComun, lineas[^1].GetProperty("Context").GetProperty("MenuItemId").GetGuid());
        Assert.DoesNotContain(Entregar(DestinoBitacora.Interacciones), l => Tipo(l) is "CONFIRMATION_REQUESTED" or "CONSUMPTION_AUTHORIZED");
    }

    [Fact(DisplayName = "bitacoras-externas · Inactividad: una persona que se identifica y no toca nada más termina en SESSION_TIMED_OUT a los 30 s, sin consumo")]
    public void Inactividad()
    {
        var maquina = NuevaMaquina();
        maquina.Identificar("0001");

        _reloj.Avanzar(MaquinaAtencion.PlazoInactividad);
        Assert.True(maquina.RevisarInactividad());

        var lineas = DeLaSesion(Entregar(DestinoBitacora.Interacciones));
        Assert.Equal(["SESSION_OPENED", "OPTIONS_OFFERED", "SESSION_TIMED_OUT"], lineas.Select(Tipo));
        Assert.Equal("SELECTION", lineas[^1].GetProperty("Context").GetProperty("FromState").GetString());
        Assert.DoesNotContain(Entregar(DestinoBitacora.Interacciones), l => Tipo(l) == "CONSUMPTION_AUTHORIZED");
    }

    [Fact(DisplayName = "bitacoras-externas · Cambiar de página: con quince opciones ofrecidas, pasar a la página siguiente y volver no agrega ninguna línea")]
    public void Cambiar_de_pagina_no_agrega_lineas()
    {
        var maquina = NuevaMaquina();
        maquina.Identificar("0001");
        Assert.Equal(2, maquina.TotalPaginas);
        var antes = Entregar(DestinoBitacora.Interacciones).Count;

        maquina.Siguiente();
        maquina.Anterior();

        Assert.Equal(antes, Entregar(DestinoBitacora.Interacciones).Count);
    }

    [Fact(DisplayName = "bitacoras-externas · Guardar el estilo: la general tiene CONFIGURATION_CHANGED con el estilo, antes 76 y 4, después 81 y 4, el resultado y el actor nulo con NO_AUTHENTICATION")]
    public async Task Guardar_el_estilo()
    {
        var pantalla = Dibujar(new ImpresoraDePrueba(), _carpeta);
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "81" });

        await pantalla.Find("#guardar-configuracion").ClickAsync(new());

        var general = Entregar(DestinoBitacora.General);
        Assert.Contains(general, l => Tipo(l) == "CONFIGURATION_OPENED" && SinActor(l.GetProperty("Context")));
        var cambio = Assert.Single(general, l => Tipo(l) == "CONFIGURATION_CHANGED").GetProperty("Context");
        Assert.Equal("VISUAL_STYLE", cambio.GetProperty("Setting").GetString());
        Assert.Equal((76, 4), Estilo(cambio.GetProperty("Before")));
        Assert.Equal((81, 4), Estilo(cambio.GetProperty("After")));
        Assert.Equal("APPLIED", cambio.GetProperty("Result").GetString());
        Assert.True(SinActor(cambio));
    }

    [Fact(DisplayName = "bitacoras-externas · Guardar sin poder escribir: la general tiene CONFIGURATION_CHANGED con el resultado fallido y el valor que quedó vigente")]
    public async Task Guardar_sin_poder_escribir()
    {
        var datos = Path.Combine(_carpeta, "datos-estilo");
        File.WriteAllText(datos, "no es una carpeta");
        var pantalla = Dibujar(new ImpresoraDePrueba(), datos);
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "81" });

        await pantalla.Find("#guardar-configuracion").ClickAsync(new());

        var cambio = Assert.Single(Entregar(DestinoBitacora.General), l => Tipo(l) == "CONFIGURATION_CHANGED").GetProperty("Context");
        Assert.Equal("FAILED", cambio.GetProperty("Result").GetString());
        Assert.Equal((81, 4), Estilo(cambio.GetProperty("After")));
        Assert.Equal((76, 4), Estilo(cambio.GetProperty("Effective")));
        Assert.True(SinActor(cambio));
    }

    [Fact(DisplayName = "bitacoras-externas · El modo de la impresora: elegir «Sin papel» deja CONFIGURATION_CHANGED con el modo anterior y el nuevo, y «Cancelar» deja CONFIGURATION_CANCELLED, los dos sin actor")]
    public async Task Modo_de_la_impresora()
    {
        var pantalla = Dibujar(new ImpresoraSimulada(Path.Combine(_carpeta, "comprobantes")), _carpeta);
        await pantalla.Find(".engranaje").ClickAsync(new());

        await pantalla.Find("#impresora-sinpapel").ClickAsync(new());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        var general = Entregar(DestinoBitacora.General);
        var cambio = Assert.Single(general, l => Tipo(l) == "CONFIGURATION_CHANGED").GetProperty("Context");
        Assert.Equal("PRINTER_SIMULATION_MODE", cambio.GetProperty("Setting").GetString());
        Assert.Equal("AVAILABLE", cambio.GetProperty("Before").GetString());
        Assert.Equal("PAPER_OUT", cambio.GetProperty("After").GetString());
        Assert.Equal("APPLIED", cambio.GetProperty("Result").GetString());
        Assert.True(SinActor(cambio));
        Assert.True(SinActor(Assert.Single(general, l => Tipo(l) == "CONFIGURATION_CANCELLED").GetProperty("Context")));
    }

    [Fact(DisplayName = "bitacoras-externas · Arranque y cierre: con las bitácoras automáticas de la ventana, la general tiene KIOSK_STARTED y KIOSK_STOPPED con la versión de la aplicación, del runtime, del sistema operativo, del esquema y de la configuración, sin pedir ninguna pasada")]
    public void Arranque_y_cierre()
    {
        NuevaMaquina(automaticas: true);

        _servicio!.Dispose();

        var general = LeerArchivo(DestinoBitacora.General);
        foreach (var tipo in new[] { "KIOSK_STARTED", "KIOSK_STOPPED" })
        {
            var contexto = Assert.Single(general, l => Tipo(l) == tipo).GetProperty("Context");
            Assert.False(string.IsNullOrEmpty(contexto.GetProperty("AppVersion").GetString()));
            Assert.Equal(Environment.Version.ToString(), contexto.GetProperty("RuntimeVersion").GetString());
            Assert.False(string.IsNullOrEmpty(contexto.GetProperty("OsDescription").GetString()));
            Assert.Equal(4, contexto.GetProperty("SchemaVersion").GetInt32());
            Assert.Equal(JsonValueKind.Number, contexto.GetProperty("ConfigurationVersion").ValueKind);
        }

        Assert.Equal("KIOSK_STOPPED", Tipo(general[^1]));
    }

    [Fact(DisplayName = "bitacoras-externas · Versión desconocida: si la versión de la aplicación no se puede leer, KIOSK_STARTED la deja nula y las demás con su valor")]
    public void Version_desconocida()
    {
        _servicio = new ServicioAtencion(RutaBase, _reloj, new ImpresoraDePrueba())
        {
            LeerVersionAplicacion = () => throw new InvalidOperationException("Sin versión"),
        };
        new MaquinaAtencion(_servicio).Iniciar();

        var contexto = Assert.Single(Entregar(DestinoBitacora.General), l => Tipo(l) == "KIOSK_STARTED").GetProperty("Context");

        Assert.Equal(JsonValueKind.Null, contexto.GetProperty("AppVersion").ValueKind);
        Assert.Equal(Environment.Version.ToString(), contexto.GetProperty("RuntimeVersion").GetString());
        Assert.Equal(4, contexto.GetProperty("SchemaVersion").GetInt32());
    }

    [Fact(DisplayName = "bitacoras-externas · Configuración al día: con las dos bitácoras sin pendientes, dice que las dos están al día")]
    public async Task Configuracion_al_dia()
    {
        var pantalla = Dibujar(new ImpresoraDePrueba(), _carpeta);
        Entregar(DestinoBitacora.General);

        await pantalla.Find(".engranaje").ClickAsync(new());

        Assert.Equal("Bitácora general: al día", pantalla.Find("#bitacora-general").TextContent.Trim());
        Assert.Equal("Bitácora de interacciones: al día", pantalla.Find("#bitacora-interacciones").TextContent.Trim());
    }

    [Fact(DisplayName = "bitacoras-externas · Configuración degradada: con la general sin permiso de escritura desde las 12:01 y cuatro pendientes, dice que está degradada con 4 pendientes desde las 12:01 y que la de interacciones está al día")]
    public async Task Configuracion_degradada()
    {
        var pantalla = Dibujar(new ImpresoraDePrueba(), _carpeta);
        Entregar(DestinoBitacora.General);
        var general = Directory.GetFiles(CarpetaPuesto(), "INComedor-general-*.log").Single();
        File.SetAttributes(general, FileAttributes.ReadOnly);
        _reloj.Avanzar(TimeSpan.FromMinutes(1));
        var registro = new RegistroDelPuesto(_servicio!.Base, _reloj, "Pruebas");
        for (var i = 0; i < 3; i++)
        {
            registro.Registrar("TIPO_DE_PRUEBA_GENERAL", new { Prueba = i });
        }

        _servicio.Bitacoras!.Entregar();

        await pantalla.Find(".engranaje").ClickAsync(new());

        var texto = pantalla.Find("#bitacora-general").TextContent.Trim();
        Assert.Equal("Bitácora general: degradada, 4 pendientes desde las 12:01", texto);
        Assert.Equal("Bitácora de interacciones: al día", pantalla.Find("#bitacora-interacciones").TextContent.Trim());
        Assert.DoesNotContain("Persona", pantalla.Find("#estado-bitacoras").TextContent);
    }

    private MaquinaAtencion NuevaMaquina(bool automaticas = false)
    {
        _servicio = new ServicioAtencion(RutaBase, _reloj, new ImpresoraDePrueba()) { BitacorasAutomaticas = automaticas };
        var maquina = new MaquinaAtencion(_servicio);
        maquina.Iniciar();
        Assert.Equal(EstadoAtencion.Reposo, maquina.Estado);
        return maquina;
    }

    private IRenderedComponent<PantallaAtencion> Dibujar(IImpresoraComprobantes impresora, string carpetaEstilo)
    {
        _servicio = new ServicioAtencion(RutaBase, _reloj, impresora);
        _contexto.Services.AddSingleton(_servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(_servicio));
        _contexto.Services.AddSingleton(new AlmacenEstiloVisual(carpetaEstilo));
        return _contexto.Render<PantallaAtencion>();
    }

    /// <summary>Corre una pasada del escritor y lee sólo el archivo del destino.</summary>
    private List<JsonElement> Entregar(DestinoBitacora destino)
    {
        _servicio!.Bitacoras!.Entregar();
        return LeerArchivo(destino);
    }

    private string CarpetaPuesto() => Directory.GetDirectories(Path.Combine(_carpeta, "bitacoras")).Single();

    private List<JsonElement> LeerArchivo(DestinoBitacora destino)
    {
        var ruta = Path.Combine(CarpetaPuesto(), EscritorBitacoras.NombreArchivo(destino, "202611"));
        if (!File.Exists(ruta))
        {
            return [];
        }

        using var archivo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var lector = new StreamReader(archivo, Encoding.UTF8);
        return lector.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();
    }

    /// <summary>Las líneas de la sesión que abrió la última SESSION_OPENED, en el orden del archivo.</summary>
    private static List<JsonElement> DeLaSesion(List<JsonElement> lineas)
    {
        var sesion = lineas.Last(l => Tipo(l) == "SESSION_OPENED").GetProperty("Context").GetProperty("IdentitySessionId").GetGuid();
        return lineas.Where(l => l.GetProperty("Context").TryGetProperty("IdentitySessionId", out var s)
                                 && s.ValueKind == JsonValueKind.String && s.GetGuid() == sesion).ToList();
    }

    private static string? Tipo(JsonElement linea) => linea.GetProperty("EventType").GetString();

    private static (int, int) Estilo(JsonElement estilo) => (estilo.GetProperty("Opacity").GetInt32(), estilo.GetProperty("Blur").GetInt32());

    private static bool SinActor(JsonElement contexto) =>
        contexto.GetProperty("ActorPersonId").ValueKind == JsonValueKind.Null && contexto.GetProperty("ActorReason").GetString() == "NO_AUTHENTICATION";
}
