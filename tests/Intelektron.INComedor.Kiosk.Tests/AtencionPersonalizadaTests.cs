using AngleSharp.Dom;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La tarjeta de una opción con su presentación en la atención (atencion-puesto de personalizar-opciones, D4): el fondo y la
/// transparencia guardados, y el precio oculto en la tarjeta y en el cartel, con el comprobante que conserva el importe.
/// bUnit sobre el puesto de prueba con la base de la demostración; 0001 tiene ALMUERZO y REFRIGERIO.
/// </summary>
public class AtencionPersonalizadaTests
{
    [Fact(DisplayName = "atencion-puesto · Una opción personalizada: la primera opción de 0001, con el segundo color y 25 %, se dibuja con ese fondo y esa opacidad; las demás, como siempre")]
    public async Task Una_opcion_personalizada()
    {
        using var puesto = Preparar(out var almacen);
        var pantalla = puesto.Dibujar();
        await Identificar(pantalla);
        var nombre = pantalla.FindAll(".opcion .nombre")[0].TextContent.Trim();
        almacen.Guardar(IdDe(puesto, nombre), new PresentacionOpcion(PresentacionOpcion.Paleta[1], null, 25, true), null);

        await pantalla.Find("#cancelar").ClickAsync(new());
        await Identificar(pantalla);

        var tarjeta = Tarjeta(pantalla, nombre);
        Assert.Contains("personalizada", tarjeta.ClassList);
        Assert.Contains($"--fondo-opcion: {PresentacionOpcion.Paleta[1]}", tarjeta.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("--opacidad-fondo: 0.75", tarjeta.GetAttribute("style"), StringComparison.Ordinal);
        Assert.All(pantalla.FindAll(".opcion").Where(o => o != tarjeta), o => Assert.DoesNotContain("personalizada", o.ClassList));
    }

    [Fact(DisplayName = "atencion-puesto · El precio oculto: una opción con importe y el precio oculto no lo muestra en su tarjeta ni en el cartel, y el comprobante sí")]
    public async Task El_precio_oculto()
    {
        using var puesto = Preparar(out var almacen);
        var impresora = new ImpresoraDePrueba();
        var pantalla = puesto.Dibujar(impresora);
        await Identificar(pantalla);
        var conCargo = pantalla.FindAll(".opcion").First(o => o.QuerySelector(".cargo") is not null);
        var nombre = conCargo.QuerySelector(".nombre")!.TextContent.Trim();
        var cargo = conCargo.QuerySelector(".cargo")!.TextContent.Trim();
        almacen.Guardar(IdDe(puesto, nombre), PresentacionOpcion.PorDefecto with { PrecioVisible = false }, null);
        await pantalla.Find("#cancelar").ClickAsync(new());
        await Identificar(pantalla);

        Assert.Null(Tarjeta(pantalla, nombre).QuerySelector(".cargo"));
        await Tarjeta(pantalla, nombre).ClickAsync(new());
        Assert.Empty(pantalla.FindAll("#cartel-eleccion .cargo-eleccion"));
        Assert.DoesNotContain(cargo, pantalla.Find("#cartel-eleccion").TextContent, StringComparison.Ordinal);

        await pantalla.Find("#confirmar").ClickAsync(new());
        // El comprobante conserva su importe: el registro no depende de la presentación.
        var importe = puesto.Servicio.Administracion!.Leer().Opciones.Single(o => o.Nombre == nombre).ImporteUnidadesMenores;
        Assert.True(importe > 0);
        pantalla.WaitForAssertion(() =>
        {
            using var datos = System.Text.Json.JsonDocument.Parse(Assert.Single(impresora.Recibidos).DatosImpresion);
            Assert.Equal(importe, datos.RootElement.GetProperty("EmployeeChargeMinor").GetInt64());
        });
    }

    private static PuestoDePrueba Preparar(out AlmacenPresentacionOpciones almacen)
    {
        var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        almacen = new AlmacenPresentacionOpciones(puesto.Carpeta);
        puesto.Contexto.Services.AddSingleton(almacen);
        return puesto;
    }

    private static async Task Identificar(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
    }

    private static IElement Tarjeta(IRenderedComponent<PantallaAtencion> pantalla, string nombre) =>
        pantalla.FindAll(".opcion").Single(o => o.QuerySelector(".nombre")!.TextContent.Trim() == nombre);

    private static Guid IdDe(PuestoDePrueba puesto, string nombre) =>
        puesto.Servicio.Administracion!.Leer().Opciones.Single(o => o.Nombre == nombre).Id;
}
