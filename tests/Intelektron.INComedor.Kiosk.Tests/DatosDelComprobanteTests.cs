using System.Text.Json;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// «Datos del comprobante» en la pestaña Impresora (comprobante-local de enriquecer-comprobante, D7): el número y el
/// nombre del puesto y la numeración, guardados en <c>puesto.json</c>, registrados como TICKET_DATA y vigentes desde la
/// próxima confirmación. bUnit sobre el puesto de prueba con la base de la demostración.
/// </summary>
public class DatosDelComprobanteTests
{
    [Fact(DisplayName = "comprobante-local · Guardar los datos del comprobante: el puesto 3 «Comedor Norte» con correlativo desde 1000 se guarda, se avisa, queda como TICKET_DATA y la próxima confirmación sale con el 1000")]
    public async Task Guardar_los_datos()
    {
        using var puesto = Preparar();
        var impresora = new ImpresoraDePrueba();
        var pantalla = puesto.Dibujar(impresora);
        await AbrirImpresora(pantalla);

        await Completar(pantalla, "3", "Comedor Norte", correlativo: true, "1000");
        await pantalla.Find("#guardar-datos-comprobante").ClickAsync(new());

        Assert.Equal(MensajesAtencion.DatosPuestoGuardados, pantalla.Find("#aviso-guardado").TextContent.Trim());
        Assert.Equal(new DatosPuesto(3, "Comedor Norte", true, 1000), new AlmacenDatosPuesto(puesto.Carpeta).Leer());
        var contexto = ContextoDe(puesto.General().Single(l => Tipo(l) == "CONFIGURATION_CHANGED" && ContextoDe(l).GetProperty("Setting").GetString() == "TICKET_DATA"));
        Assert.Equal("APPLIED", contexto.GetProperty("Result").GetString());
        Assert.Equal(JsonValueKind.Null, contexto.GetProperty("Before").GetProperty("PostNumber").ValueKind);
        Assert.Equal(3, contexto.GetProperty("After").GetProperty("PostNumber").GetInt32());
        Assert.Equal("CORRELATIVE", contexto.GetProperty("After").GetProperty("Numbering").GetString());
        Assert.Equal(1000, contexto.GetProperty("After").GetProperty("From").GetInt64());

        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => Assert.NotEmpty(impresora.Recibidos));
        Assert.Equal(1000, JsonDocument.Parse(impresora.Recibidos[^1].DatosImpresion).RootElement.GetProperty("TicketNumber").GetInt64());
    }

    [Fact(DisplayName = "comprobante-local · Datos fuera de forma: el puesto 0, un nombre con un salto de línea y un desde de 0 se señalan, no se guardan y quedan como TICKET_DATA rechazado")]
    public async Task Datos_fuera_de_forma()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirImpresora(pantalla);

        await Completar(pantalla, "0", "Comedor\nNorte", correlativo: true, "0");
        await pantalla.Find("#guardar-datos-comprobante").ClickAsync(new());

        Assert.Equal("true", pantalla.Find("#numero-puesto").GetAttribute("aria-invalid"));
        Assert.Equal("true", pantalla.Find("#nombre-puesto").GetAttribute("aria-invalid"));
        Assert.Equal("true", pantalla.Find("#desde-correlativo").GetAttribute("aria-invalid"));
        Assert.Equal(MensajesAtencion.PanelInvalido, pantalla.Find("#aviso-datos-comprobante").TextContent.Trim());
        Assert.False(File.Exists(Path.Combine(puesto.Carpeta, "puesto.json")));
        var contexto = ContextoDe(puesto.General().Single(l => Tipo(l) == "CONFIGURATION_CHANGED" && ContextoDe(l).GetProperty("Setting").GetString() == "TICKET_DATA"));
        Assert.Equal("REJECTED", contexto.GetProperty("Result").GetString());
        Assert.Equal(["Numero", "Nombre", "Desde"], contexto.GetProperty("Fields").EnumerateArray().Select(c => c.GetString()));
    }

    [Fact(DisplayName = "comprobante-local · Guardar los datos del comprobante: con el código de un operador, la sección se ve en Impresora como el resto de la pestaña")]
    public async Task Con_un_operador()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();

        await EntradaConfiguracion.Entrar(pantalla, "9001");
        await pantalla.Find("#pestana-impresora").ClickAsync(new());

        Assert.Single(pantalla.FindAll("#guardar-datos-comprobante"));
        Assert.Single(pantalla.FindAll("#guardar-textos"));
    }

    [Fact(DisplayName = "comprobante-local · La numeración del comprobante: con correlativo, «Reimprimir» en Consumos pide el motivo con el número de la fila, el 1000, y no con los ocho caracteres")]
    public async Task El_pedido_del_motivo_dice_el_correlativo()
    {
        using var puesto = Preparar();
        new AlmacenDatosPuesto(puesto.Carpeta).Guardar(new DatosPuesto(null, string.Empty, true, 1000));
        var pantalla = puesto.Dibujar();
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        await pantalla.Find("#listo").ClickAsync(new());
        await EntradaConfiguracion.Entrar(pantalla);

        Assert.Equal("1000", pantalla.Find("#seccion-consumos tr.consumo td.numero").TextContent.Trim());
        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());

        Assert.Contains("N.º 1000 ·", pantalla.Find("#seccion-consumos").TextContent);
    }

    private static PuestoDePrueba Preparar()
    {
        var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        puesto.Contexto.Services.AddSingleton(new AlmacenDatosPuesto(puesto.Carpeta));
        return puesto;
    }

    private static async Task AbrirImpresora(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-impresora").ClickAsync(new());
    }

    private static async Task Completar(IRenderedComponent<PantallaAtencion> pantalla, string numero, string nombre, bool correlativo, string desde)
    {
        await pantalla.Find("#numero-puesto").InputAsync(new ChangeEventArgs { Value = numero });
        await pantalla.Find("#nombre-puesto").InputAsync(new ChangeEventArgs { Value = nombre });
        await pantalla.Find(correlativo ? "#numeracion-correlativo" : "#numeracion-codigo").ClickAsync(new());
        await pantalla.Find("#desde-correlativo").InputAsync(new ChangeEventArgs { Value = desde });
    }
}
