using System.Text.Json;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La entrada a Configuración y el cierre según el modo (acceso-puesto de configurar-puesto-por-pestanas, D5 a D7 y D17):
/// mantenimiento entra y cierra directo, operativo pide la clave del día, la atención no cambia, y cada acceso queda en la
/// general con el actor nulo, NO_AUTHENTICATION y cómo se entró. El cierre de la ventana lo cuenta un CierrePuesto.
/// </summary>
public class AccesoPuestoTests
{
    [Fact(DisplayName = "acceso-puesto · Mantenimiento entra directo: el engranaje abre Configuración sin clave y CONFIGURATION_OPENED lleva MAINTENANCE_MODE")]
    public async Task Mantenimiento_entra_directo()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        await pantalla.Find(".engranaje").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        Assert.Contains("Modo: mantenimiento", pantalla.Find("#seccion-general").TextContent);
        var abierta = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_OPENED"));
        Assert.Equal("MAINTENANCE_MODE", abierta.GetProperty("Access").GetString());
        Assert.True(SinActor(abierta));
    }

    [Fact(DisplayName = "acceso-puesto · Operativo pide la clave: el engranaje muestra la clave y Configuración abre recién al aceptarla, con DAILY_KEY_ACCEPTED para abrirla y CONFIGURATION_OPENED con DAILY_KEY")]
    public async Task Operativo_pide_la_clave()
    {
        using var puesto = new PuestoDePrueba(ModoPuesto.Operativo);
        var pantalla = puesto.Dibujar();

        await pantalla.Find(".engranaje").ClickAsync(new());

        Assert.NotEmpty(pantalla.FindAll("#dialogo-clave"));
        Assert.Empty(pantalla.FindAll("[role=\"tablist\"]"));
        await Aceptar(pantalla, "4711");

        Assert.Contains("Modo: operativo", pantalla.Find("#seccion-general").TextContent);
        var general = puesto.General();
        var clave = ContextoDe(Assert.Single(general, l => Tipo(l) == "DAILY_KEY_ACCEPTED"));
        Assert.Equal("OPEN_CONFIGURATION", clave.GetProperty("Action").GetString());
        Assert.True(SinActor(clave));
        var abierta = ContextoDe(Assert.Single(general, l => Tipo(l) == "CONFIGURATION_OPENED"));
        Assert.Equal("DAILY_KEY", abierta.GetProperty("Access").GetString());
        Assert.True(SinActor(abierta));
    }

    [Fact(DisplayName = "acceso-puesto · Pasar a operativo pide la clave: cancelada sigue en mantenimiento; aceptada dice «Modo: operativo», lo guarda y deja CONFIGURATION_CHANGED con KIOSK_MODE y DAILY_KEY")]
    public async Task Pasar_a_operativo_pide_la_clave()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());

        await pantalla.Find("#pasar-operativo").ClickAsync(new());
        await pantalla.Find("#cancelar-clave").ClickAsync(new());

        Assert.Contains("Modo: mantenimiento", pantalla.Find("#seccion-general").TextContent);
        Assert.Equal(ModoPuesto.Mantenimiento, new AlmacenModoPuesto(puesto.Carpeta).Leer());

        await pantalla.Find("#pasar-operativo").ClickAsync(new());
        await Aceptar(pantalla, "4711");

        Assert.Contains("Modo: operativo", pantalla.Find("#seccion-general").TextContent);
        Assert.Equal(ModoPuesto.Operativo, new AlmacenModoPuesto(puesto.Carpeta).Leer());
        Assert.NotEmpty(pantalla.FindAll("[role=\"tablist\"]"));
        var cambio = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"));
        Assert.Equal("KIOSK_MODE", cambio.GetProperty("Setting").GetString());
        Assert.Equal(("MAINTENANCE", "OPERATIVE", "APPLIED"), (cambio.GetProperty("Before").GetString(), cambio.GetProperty("After").GetString(), cambio.GetProperty("Result").GetString()));
        Assert.Equal("DAILY_KEY", cambio.GetProperty("Access").GetString());
        Assert.True(SinActor(cambio));
    }

    [Fact(DisplayName = "acceso-puesto · Volver a mantenimiento: dentro de Configuración, a la que se entró con la clave, «Pasar a mantenimiento» no la pide otra vez y el próximo arranque es en mantenimiento")]
    public async Task Volver_a_mantenimiento()
    {
        using var puesto = new PuestoDePrueba(ModoPuesto.Operativo);
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await Aceptar(pantalla, "4711");

        await pantalla.Find("#pasar-mantenimiento").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        Assert.Contains("Modo: mantenimiento", pantalla.Find("#seccion-general").TextContent);
        Assert.Equal(ModoPuesto.Mantenimiento, new AlmacenModoPuesto(puesto.Carpeta).Leer());
    }

    [Fact(DisplayName = "acceso-puesto · En operativo la atención no pide clave: 0001 se identifica, elige y confirma, y el consumo queda registrado")]
    public async Task En_operativo_la_atencion_no_pide_clave()
    {
        using var puesto = new PuestoDePrueba(ModoPuesto.Operativo);
        var pantalla = puesto.Dibujar();

        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());

        pantalla.WaitForAssertion(() => Assert.NotEmpty(pantalla.FindAll("#listo")));
        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        Assert.Single(puesto.General().Concat(Interacciones(puesto)), l => Tipo(l) == "CONSUMPTION_AUTHORIZED");
    }

    [Fact(DisplayName = "acceso-puesto · Escape en mantenimiento: en reposo y sin diálogos, el puesto se cierra en orden, con KIOSK_CLOSE_REQUESTED y MAINTENANCE_MODE")]
    public async Task Escape_en_mantenimiento()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        await pantalla.Find(".puesto").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(1, puesto.Cierres);
        var pedido = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "KIOSK_CLOSE_REQUESTED"));
        Assert.Equal(("ESCAPE", "MAINTENANCE_MODE"), (pedido.GetProperty("Source").GetString(), pedido.GetProperty("Access").GetString()));
        Assert.True(SinActor(pedido));
    }

    [Fact(DisplayName = "acceso-puesto · Escape en operativo: muestra la clave y el puesto se cierra en orden recién al aceptarla")]
    public async Task Escape_en_operativo()
    {
        using var puesto = new PuestoDePrueba(ModoPuesto.Operativo);
        var pantalla = puesto.Dibujar();

        await pantalla.Find(".puesto").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.NotEmpty(pantalla.FindAll("#dialogo-clave"));
        Assert.Equal(0, puesto.Cierres);
        await Aceptar(pantalla, "4711");
        Assert.Equal(1, puesto.Cierres);
        var clave = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "DAILY_KEY_ACCEPTED"));
        Assert.Equal("CLOSE_KIOSK", clave.GetProperty("Action").GetString());
    }

    [Fact(DisplayName = "acceso-puesto · Clave rechazada: 12a4 para cerrar el puesto deja DAILY_KEY_REJECTED con CLOSE_KIOSK e INVALID_FORMAT, sin 12a4, y el puesto sigue abierto")]
    public async Task Clave_rechazada_para_cerrar()
    {
        using var puesto = new PuestoDePrueba(ModoPuesto.Operativo);
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".puesto").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        await Aceptar(pantalla, "12a4");

        Assert.Equal(0, puesto.Cierres);
        var rechazo = Assert.Single(puesto.General(), l => Tipo(l) == "DAILY_KEY_REJECTED");
        var contexto = ContextoDe(rechazo);
        Assert.Equal(("CLOSE_KIOSK", "INVALID_FORMAT"), (contexto.GetProperty("Action").GetString(), contexto.GetProperty("Cause").GetString()));
        Assert.True(SinActor(contexto));
        Assert.DoesNotContain("12a4", rechazo.GetRawText());
    }

    [Fact(DisplayName = "acceso-puesto · Cancelar la clave al cerrar: el diálogo se cierra, el puesto sigue en reposo y no se pidió ningún cierre")]
    public async Task Cancelar_la_clave_al_cerrar()
    {
        using var puesto = new PuestoDePrueba(ModoPuesto.Operativo);
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".puesto").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        await pantalla.Find("#dialogo-clave").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        Assert.Equal(0, puesto.Cierres);
        Assert.NotEmpty(pantalla.FindAll("#ingresar"));
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "KIOSK_CLOSE_REQUESTED");
    }

    [Fact(DisplayName = "acceso-puesto · Escape durante una atención: el puesto sigue abierto con la misma persona y la misma elección")]
    public async Task Escape_durante_una_atencion()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        var elegida = pantalla.Find(".opcion.elegida .nombre").TextContent;

        await pantalla.Find(".puesto").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(0, puesto.Cierres);
        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        Assert.Contains("Persona sintética 01", pantalla.Find("#saludo").TextContent);
        Assert.Equal(elegida, pantalla.Find(".opcion.elegida .nombre").TextContent);
    }

    [Theory(DisplayName = "acceso-puesto · «Cerrar el puesto» en General: en mantenimiento cierra directo; en operativo, después de la clave")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cerrar_el_puesto_en_general(bool operativo)
    {
        using var puesto = new PuestoDePrueba(operativo ? ModoPuesto.Operativo : null);
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        if (operativo)
        {
            await Aceptar(pantalla, "4711");
        }

        await pantalla.Find("#cerrar-puesto").ClickAsync(new());

        if (operativo)
        {
            Assert.Equal(0, puesto.Cierres);
            await Aceptar(pantalla, "4711");
        }

        Assert.Equal(1, puesto.Cierres);
        var pedido = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "KIOSK_CLOSE_REQUESTED"));
        Assert.Equal("CLOSE_BUTTON", pedido.GetProperty("Source").GetString());
    }

    [Fact(DisplayName = "acceso-puesto · Otro pedido de cierre en operativo: el pedido de la ventana muestra la clave sin cerrar")]
    public async Task Otro_pedido_de_cierre_en_operativo()
    {
        using var puesto = new PuestoDePrueba(ModoPuesto.Operativo);
        var pantalla = puesto.Dibujar();
        Assert.True(puesto.Cierre.RequiereClave);
        Assert.True(puesto.Cierre.DebeFrenar);

        await pantalla.InvokeAsync(puesto.Cierre.PedirClave);

        Assert.NotEmpty(pantalla.FindAll("#dialogo-clave"));
        Assert.Equal(0, puesto.Cierres);
        await Aceptar(pantalla, "4711");
        Assert.Equal(1, puesto.Cierres);
        Assert.False(puesto.Cierre.DebeFrenar);
    }

    private static async Task Aceptar(IRenderedComponent<PantallaAtencion> pantalla, string clave)
    {
        await pantalla.Find("#clave-del-dia").InputAsync(new ChangeEventArgs { Value = clave });
        await pantalla.Find("#aceptar-clave").ClickAsync(new());
    }

    private static List<JsonElement> Interacciones(PuestoDePrueba puesto)
    {
        var carpeta = Directory.GetDirectories(Path.Combine(puesto.Carpeta, "bitacoras")).Single();
        var ruta = Path.Combine(carpeta, Infrastructure.Sqlite.EscritorBitacoras.NombreArchivo(Infrastructure.Sqlite.DestinoBitacora.Interacciones, "202611"));
        return File.Exists(ruta)
            ? File.ReadAllText(ruta).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList()
            : [];
    }
}
