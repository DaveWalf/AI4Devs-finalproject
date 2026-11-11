using AngleSharp.Dom;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La tarjeta de una opción con su presentación en la atención (atencion-puesto de personalizar-opciones, D4): el fondo y la
/// transparencia guardados, y el precio oculto en la tarjeta, en el cartel y en la reconfirmación, con el comprobante que
/// conserva el importe. bUnit sobre el puesto de prueba con la base de la demostración; 0001 tiene ALMUERZO y REFRIGERIO.
/// </summary>
public class AtencionPersonalizadaTests
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

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

    [Fact(DisplayName = "atencion-puesto · El precio oculto en la reconfirmación: si el precio de una opción con el precio oculto cambia antes de confirmar, la reconfirmación muestra su nombre sin el importe")]
    public async Task El_precio_oculto_en_la_reconfirmacion()
    {
        using var puesto = Preparar(out var almacen);
        almacen.Guardar(SemillaAtencion.PrimeraOpcionId, PresentacionOpcion.PorDefecto with { PrecioVisible = false }, null);
        var pantalla = puesto.Dibujar();
        await Identificar(pantalla);
        var nombre = puesto.Servicio.Administracion!.Leer().Opciones.Single(o => o.Id == SemillaAtencion.PrimeraOpcionId).Nombre;
        await Tarjeta(pantalla, nombre).ClickAsync(new());

        using (var conexion = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False"))
        {
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "UPDATE tbComedorOpcionesMenu SET commenVersionMenu = commenVersionMenu + 1, commenImporteEmpleadoUnidadesMenores = commenImporteEmpleadoUnidadesMenores + 30000 WHERE commenCodigo = $men";
            comando.Parameters.AddWithValue("$men", SemillaAtencion.PrimeraOpcionId.ToString("D"));
            Assert.True(comando.ExecuteNonQuery() >= 1);
        }

        await pantalla.Find("#confirmar").ClickAsync(new());

        Assert.Contains(nombre, pantalla.Find("#vigente").TextContent, StringComparison.Ordinal);
        Assert.Empty(pantalla.FindAll("#cargo-vigente"));
        Assert.DoesNotContain("ARS", pantalla.Find("#vigente").TextContent, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "atencion-puesto · Una imagen que ya no está: si el archivo de la imagen se borró, la tarjeta se ve sin fondo y la atención sigue")]
    public async Task Una_imagen_que_ya_no_esta()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        new AlmacenPresentacionOpciones(puesto.Carpeta)
            .Guardar(SemillaAtencion.PrimeraOpcionId, PresentacionOpcion.PorDefecto, new LogoElegido(".png", "image/png", "PNG", Png));
        foreach (var archivo in Directory.GetFiles(Path.Combine(puesto.Carpeta, "opciones")))
        {
            File.Delete(archivo);
        }

        puesto.Contexto.Services.AddSingleton(new AlmacenPresentacionOpciones(puesto.Carpeta));
        var pantalla = puesto.Dibujar();
        await Identificar(pantalla);

        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        Assert.All(pantalla.FindAll(".opcion"), o => Assert.DoesNotContain("con-imagen", o.ClassList));
    }

    [Fact(DisplayName = "administracion-local · Un archivo de presentación roto: con un JSON ilegible, la atención ofrece las opciones de siempre, sin personalizar")]
    public async Task Un_archivo_roto_no_frena_la_atencion()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        File.WriteAllText(Path.Combine(puesto.Carpeta, "presentacion-opciones.json"), "{ no es json");
        puesto.Contexto.Services.AddSingleton(new AlmacenPresentacionOpciones(puesto.Carpeta));
        var pantalla = puesto.Dibujar();

        await Identificar(pantalla);

        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        Assert.All(pantalla.FindAll(".opcion"), o => Assert.DoesNotContain("personalizada", o.ClassList));
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
