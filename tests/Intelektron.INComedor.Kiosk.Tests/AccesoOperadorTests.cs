using System.Text.RegularExpressions;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La entrada a Configuración con la clave o un operador (entrar-a-configuracion-con-teclado, D1 y D4 a D7): la clave en los
/// dos modos con su aviso de clave sin validar, y el código de un operador con OPERATOR vigente, que abre sólo Consumos,
/// Personas e Impresora, queda como actor de lo que hace y nunca vale como clave del día. Sobre el puesto de bUnit y la
/// semilla de la atención (9001 operador vigente, 9002 vencido, 9004 sin OPERATOR, 0001 persona).
/// </summary>
public class AccesoOperadorTests
{
    private static readonly Guid PersonaDe9001 = Guid.Parse("00000000-0000-4000-8000-000000000037");

    private static readonly Regex Uuid = new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

    [Fact(DisplayName = "acceso-puesto · Mantenimiento también pide la clave (el diálogo): en mantenimiento el engranaje muestra la clave del día y Configuración sigue cerrada hasta aceptarla")]
    public async Task Mantenimiento_tambien_pide_la_clave()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        await pantalla.Find(".engranaje").ClickAsync(new());

        Assert.NotEmpty(pantalla.FindAll("#dialogo-clave"));
        Assert.Empty(pantalla.FindAll("[role=\"tablist\"]"));
        await EntradaConfiguracion.Tipear(pantalla, "4711");
        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        Assert.Contains("Modo: mantenimiento", pantalla.Find("#seccion-general").TextContent);
    }

    [Theory(DisplayName = "acceso-puesto · Cualquier número en desarrollo (con el aviso): en mantenimiento y en operativo, 4, 7, 1, 1 y «ENTER» abren Configuración con el aviso de clave sin validar, y el diálogo mostraba el aviso de desarrollo")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cualquier_numero_con_el_aviso(bool operativo)
    {
        using var puesto = new PuestoDePrueba(operativo ? ModoPuesto.Operativo : null);
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        Assert.Equal(MensajesAtencion.ClaveDeDesarrollo, pantalla.Find("#aviso-desarrollo").TextContent.Trim());

        await EntradaConfiguracion.Tipear(pantalla, "4711");

        Assert.NotEmpty(pantalla.FindAll("[role=\"tablist\"]"));
        var aviso = pantalla.Find("#aviso-clave-sin-validar");
        Assert.Equal(MensajesAtencion.ClaveSinValidar, aviso.TextContent.Trim());
        Assert.Equal("status", aviso.GetAttribute("role"));
        Assert.Equal(9, pantalla.FindAll("[role=\"tab\"]").Count);
    }

    [Fact(DisplayName = "acceso-puesto · Entrada con la clave del día (mantenimiento): la general tiene DAILY_KEY_ACCEPTED para abrir Configuración y CONFIGURATION_OPENED con DAILY_KEY, los dos con el actor nulo y NO_AUTHENTICATION")]
    public async Task Entrada_con_la_clave_del_dia_en_mantenimiento()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        await EntradaConfiguracion.Entrar(pantalla);

        var general = puesto.General();
        var clave = ContextoDe(Assert.Single(general, l => Tipo(l) == "DAILY_KEY_ACCEPTED"));
        Assert.Equal("OPEN_CONFIGURATION", clave.GetProperty("Action").GetString());
        Assert.True(SinActor(clave));
        var abierta = ContextoDe(Assert.Single(general, l => Tipo(l) == "CONFIGURATION_OPENED"));
        Assert.Equal("DAILY_KEY", abierta.GetProperty("Access").GetString());
        Assert.True(SinActor(abierta));
    }

    [Fact(DisplayName = "acceso-puesto · Entrada de un operador: con 9001, la general tiene CONFIGURATION_OPENED con OPERATOR y su persona como actor, y ningún registro de las dos bitácoras contiene 9001")]
    public async Task Entrada_de_un_operador()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        await EntradaConfiguracion.Entrar(pantalla, "9001");

        var general = puesto.General();
        var abierta = ContextoDe(Assert.Single(general, l => Tipo(l) == "CONFIGURATION_OPENED"));
        Assert.Equal("OPERATOR", abierta.GetProperty("Access").GetString());
        Assert.Equal(PersonaDe9001, abierta.GetProperty("ActorPersonId").GetGuid());
        Assert.False(abierta.TryGetProperty("ActorReason", out _));
        Assert.DoesNotContain(general, l => Tipo(l) == "DAILY_KEY_ACCEPTED");
        Assert.All(ArchivosDeBitacoras(puesto), texto => Assert.DoesNotContain("9001", Uuid.Replace(texto, "")));
    }

    [Theory(DisplayName = "acceso-puesto · Un operador entra con su código: en mantenimiento y en operativo, 9001 abre Configuración sólo con Consumos, Personas e Impresora, en Consumos, sin General, sin «Cerrar el puesto» y sin el aviso de clave sin validar")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Un_operador_entra_con_su_codigo(bool operativo)
    {
        using var puesto = new PuestoDePrueba(operativo ? ModoPuesto.Operativo : null);
        var pantalla = puesto.Dibujar();

        await EntradaConfiguracion.Entrar(pantalla, "9001");

        Assert.Equal(["pestana-consumos", "pestana-personas", "pestana-impresora"], pantalla.FindAll("[role=\"tab\"]").Select(t => t.Id));
        Assert.Equal("true", pantalla.Find("#pestana-consumos").GetAttribute("aria-selected"));
        Assert.Empty(pantalla.FindAll("#seccion-general"));
        Assert.Empty(pantalla.FindAll("#cerrar-puesto"));
        Assert.Empty(pantalla.FindAll("#pasar-operativo"));
        Assert.Empty(pantalla.FindAll("#pasar-mantenimiento"));
        Assert.Empty(pantalla.FindAll("#aviso-clave-sin-validar"));
        Assert.Equal("Operador: Operador sintético 01", pantalla.Find("#operador-configuracion").TextContent.Trim());
    }

    [Fact(DisplayName = "acceso-puesto · Un permiso de operador vencido: 9002 deja el diálogo con «Tu permiso de operador está vencido», Configuración cerrada y 9002 sin aceptarse como clave del día")]
    public async Task Un_permiso_de_operador_vencido()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        await EntradaConfiguracion.Entrar(pantalla, "9002");

        Assert.Equal("Tu permiso de operador está vencido.", pantalla.Find("#acceso-rechazado").TextContent.Trim());
        Assert.NotEmpty(pantalla.FindAll("#dialogo-clave"));
        Assert.Empty(pantalla.FindAll("[role=\"tablist\"]"));
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "DAILY_KEY_ACCEPTED");
    }

    [Fact(DisplayName = "acceso-puesto · El código de una persona sin OPERATOR: 9004 y, después, 0001 dejan el diálogo con el motivo del rechazo y Configuración cerrada")]
    public async Task El_codigo_de_una_persona_sin_operator()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        await EntradaConfiguracion.Entrar(pantalla, "9004");
        Assert.Equal(MensajesAtencion.DeMotivo("PERMISSION_MISSING"), pantalla.Find("#acceso-rechazado").TextContent.Trim());
        Assert.Empty(pantalla.FindAll("[role=\"tablist\"]"));

        await EntradaConfiguracion.Tipear(pantalla, "0001");
        Assert.Equal(MensajesAtencion.DeMotivo("PERMISSION_MISSING"), pantalla.Find("#acceso-rechazado").TextContent.Trim());
        Assert.NotEmpty(pantalla.FindAll("#dialogo-clave"));
        Assert.Empty(pantalla.FindAll("[role=\"tablist\"]"));
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "DAILY_KEY_ACCEPTED");
    }

    [Fact(DisplayName = "acceso-puesto · Un operador no cierra el puesto: en operativo, Escape y 9001 dejan el diálogo con «El código de un operador no cierra el puesto», el puesto abierto, ningún KIOSK_CLOSE_REQUESTED y la clave rechazada por PERSON_CODE, sin el código")]
    public async Task Un_operador_no_cierra_el_puesto()
    {
        using var puesto = new PuestoDePrueba(ModoPuesto.Operativo);
        var pantalla = puesto.Dibujar();

        await pantalla.Find(".puesto").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        await EntradaConfiguracion.Tipear(pantalla, "9001");

        Assert.Equal(MensajesAtencion.CodigoNoEsClave, pantalla.Find("#acceso-rechazado").TextContent.Trim());
        Assert.Equal(0, puesto.Cierres);
        var general = puesto.General();
        Assert.DoesNotContain(general, l => Tipo(l) == "KIOSK_CLOSE_REQUESTED");
        var rechazo = ContextoDe(Assert.Single(general, l => Tipo(l) == "DAILY_KEY_REJECTED"));
        Assert.Equal(("CLOSE_KIOSK", "PERSON_CODE"), (rechazo.GetProperty("Action").GetString(), rechazo.GetProperty("Cause").GetString()));
        Assert.DoesNotContain("9001", Uuid.Replace(rechazo.GetRawText(), ""));
    }

    [Fact(DisplayName = "acceso-puesto · Lo guardado por un operador: con 9001, agregar «Persona sintética 05» en Personas y guardar deja el CONFIGURATION_CHANGED de las personas con su persona como actor, sin ningún código")]
    public async Task Lo_guardado_por_un_operador()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla, "9001");

        await pantalla.Find("#abrir-personas").ClickAsync(new());
        await pantalla.Find("#agregar-persona").ClickAsync(new());
        await pantalla.FindAll("tr.persona").Last().QuerySelector("input.nombre-persona")!.InputAsync(new ChangeEventArgs { Value = "Persona sintética 05" });
        await pantalla.FindAll("tr.persona").Last().QuerySelector("input.codigo-persona")!.InputAsync(new ChangeEventArgs { Value = "0105" });
        await pantalla.Find("#guardar-panel").ClickAsync(new());

        var cambio = Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED" && ContextoDe(l).GetProperty("Setting").GetString() == "PERSON");
        var contexto = ContextoDe(cambio);
        Assert.Equal(PersonaDe9001, contexto.GetProperty("ActorPersonId").GetGuid());
        Assert.False(contexto.TryGetProperty("ActorReason", out _));
        var texto = Uuid.Replace(cambio.GetRawText(), "");
        Assert.DoesNotContain("0105", texto);
        Assert.DoesNotContain("9001", texto);
    }

    [Fact(DisplayName = "configuracion-puesto · Las tres pestañas de un operador: con el foco en Consumos, dos flechas a la derecha e Inicio eligen Personas, Impresora y otra vez Consumos, y ninguna otra pestaña existe")]
    public async Task Las_tres_pestanas_de_un_operador()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla, "9001");

        await pantalla.Find("#pestana-consumos").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("true", pantalla.Find("#pestana-personas").GetAttribute("aria-selected"));
        await pantalla.Find("#pestana-personas").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("true", pantalla.Find("#pestana-impresora").GetAttribute("aria-selected"));
        await pantalla.Find("#pestana-impresora").KeyDownAsync(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal("true", pantalla.Find("#pestana-consumos").GetAttribute("aria-selected"));

        Assert.Equal(3, pantalla.FindAll("[role=\"tab\"]").Count);
        Assert.All(new[] { "general", "opciones", "comprobantes", "identidad", "pantalla", "red" },
            c => Assert.Empty(pantalla.FindAll($"#pestana-{c}, #seccion-{c}")));
    }

    [Fact(DisplayName = "consulta-consumos · Un operador ve los consumos: con un consumo de hoy, 9001 abre en Consumos con la tabla, y la general tiene CONSUMPTIONS_QUERIED con OPERATOR y su persona como actor, sin nombres ni códigos")]
    public async Task Un_operador_ve_los_consumos()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla);

        await EntradaConfiguracion.Entrar(pantalla, "9001");

        Assert.Single(pantalla.FindAll("#seccion-consumos tr.consumo"));
        var consulta = Assert.Single(puesto.General(), l => Tipo(l) == "CONSUMPTIONS_QUERIED");
        var contexto = ContextoDe(consulta);
        Assert.Equal("OPERATOR", contexto.GetProperty("Access").GetString());
        Assert.Equal(PersonaDe9001, contexto.GetProperty("ActorPersonId").GetGuid());
        var texto = Uuid.Replace(consulta.GetRawText(), "");
        Assert.DoesNotContain("Persona sintética", texto);
        Assert.DoesNotContain("Operador sintético", texto);
        Assert.DoesNotContain("0001", texto);
        Assert.DoesNotContain("9001", texto);
    }

    /// <summary>Una atención completa de 0001 con su primer almuerzo, para que haya un consumo de hoy.</summary>
    private static async Task Consumir(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion").First(o => o.TextContent.Contains("Almuerzo", StringComparison.Ordinal)).ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        await pantalla.Find("#listo").ClickAsync(new());
    }

    /// <summary>El texto de los archivos de las dos bitácoras, después de una pasada del escritor.</summary>
    private static List<string> ArchivosDeBitacoras(PuestoDePrueba puesto)
    {
        puesto.Servicio.Bitacoras!.Entregar();
        return Directory.EnumerateFiles(Path.Combine(puesto.Carpeta, "bitacoras"), "*.log", SearchOption.AllDirectories)
            .Select(File.ReadAllText).ToList();
    }
}
