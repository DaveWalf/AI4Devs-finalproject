using System.Text.RegularExpressions;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Lo que la verificación de entrar-a-configuracion-con-teclado (10/11/2026) dejó sin prueba (cubrir-pruebas-de-las-verificaciones,
/// grupo 3): el código de una persona tipeado como la clave de «Pasar a operativo». Las guardas de D6 sobre el modo y la
/// simulación no se alcanzan desde la pantalla (con el acceso de un operador sus botones no existen) y no se prueban acá.
/// </summary>
public class CoberturaDelAccesoTests
{
    private static readonly Regex Uuid = new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

    [Fact(DisplayName = "acceso-puesto · El código de una persona no es la clave para pasar a operativo: 0001 deja el diálogo con «el código no es la clave», el modo en mantenimiento y DAILY_KEY_REJECTED con ACTIVATE_OPERATIVE y PERSON_CODE, sin 0001")]
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
}
