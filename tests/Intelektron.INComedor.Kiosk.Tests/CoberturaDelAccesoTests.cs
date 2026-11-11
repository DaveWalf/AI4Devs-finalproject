using System.Text.RegularExpressions;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Lo que la verificación de entrar-a-configuracion-con-teclado (10/11/2026) dejó sin prueba (cubrir-pruebas-de-las-verificaciones,
/// grupo 3): el código de una persona tipeado como la clave de «Pasar a operativo», y las guardas de D6 sobre el modo y la
/// simulación con el acceso de un operador. Sus botones no existen en el DOM, así que los eventos llegan por los callbacks
/// del panel, como en ComboSimulacionTests: «por si un evento llegara igual».
/// </summary>
public class CoberturaDelAccesoTests
{
    private static readonly Regex Uuid = new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

    [Fact(DisplayName = "acceso-puesto · El código de una persona no es la clave para pasar a operativo: 0001 deja el diálogo con el rechazo de un código (hoy «El código de un operador no cierra el puesto.»), el modo en mantenimiento y DAILY_KEY_REJECTED con ACTIVATE_OPERATIVE y PERSON_CODE, sin 0001")]
    public async Task Un_codigo_no_pasa_a_operativo()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#pasar-operativo").ClickAsync(new());
        await EntradaConfiguracion.Tipear(pantalla, "0001");

        Assert.Equal(MensajesAtencion.CodigoNoEsClave, pantalla.Find("#acceso-rechazado").TextContent.Trim());
        Assert.Contains("Modo: mantenimiento", pantalla.Find("#seccion-general").TextContent, StringComparison.Ordinal);
        Assert.Equal(ModoPuesto.Mantenimiento, new AlmacenModoPuesto(puesto.Carpeta).Leer());
        var general = puesto.General();
        var rechazo = ContextoDe(Assert.Single(general, l => Tipo(l) == "DAILY_KEY_REJECTED"));
        Assert.Equal(("ACTIVATE_OPERATIVE", "PERSON_CODE"), (rechazo.GetProperty("Action").GetString(), rechazo.GetProperty("Cause").GetString()));
        Assert.DoesNotContain(general, l => Tipo(l) == "CONFIGURATION_CHANGED" && ContextoDe(l).GetProperty("Setting").GetString() == "KIOSK_MODE");
        Assert.All(general, l => Assert.DoesNotContain("0001", Uuid.Replace(l.GetRawText(), ""), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "acceso-puesto · Las guardas de D6 con el acceso de un operador: pasar a operativo, pasar a mantenimiento y cambiar la simulación por los callbacks del panel no piden la clave, no cambian el modo ni la simulación y no dejan CONFIGURATION_CHANGED")]
    public async Task Las_guardas_del_operador()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla, "9001");
        Assert.Empty(pantalla.FindAll("#pasar-operativo"));
        var panel = pantalla.FindComponent<PanelConfiguracion>();

        await pantalla.InvokeAsync(() => panel.Instance.PasarAOperativo.InvokeAsync());
        await pantalla.InvokeAsync(() => panel.Instance.PasarAMantenimiento.InvokeAsync());
        await pantalla.InvokeAsync(() => panel.Instance.CambiarSimulacion.InvokeAsync(false));

        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        Assert.Equal(ModoPuesto.Mantenimiento, new AlmacenModoPuesto(puesto.Carpeta).Leer());
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"
            && ContextoDe(l).GetProperty("Setting").GetString() is "KIOSK_MODE" or "SIMULATION_MODE");
    }
}
