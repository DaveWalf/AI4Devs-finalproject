using System.Security.Cryptography;
using AngleSharp.Dom;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El catálogo a la vista en la pestaña Opciones y la personalización de cada opción (administracion-local de
/// personalizar-opciones, D1, D2 y D5). bUnit sobre el puesto de prueba con la base de la demostración, que tiene quince
/// opciones; «Almuerzo opcional» cuesta 1.800,00.
/// </summary>
public class OpcionesEnElLugarTests
{
    private const string Opcion = "Almuerzo opcional";

    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact(DisplayName = "administracion-local · El catálogo a la vista: la pestaña Opciones muestra todas las opciones del catálogo activo con su servicio, su importe y sus cargas, sin abrir la edición")]
    public async Task El_catalogo_a_la_vista()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();

        await AbrirOpciones(pantalla);

        Assert.Equal(puesto.Servicio.Administracion!.Leer().Opciones.Count, pantalla.FindAll("#catalogo-opciones article.opcion-catalogo").Count);
        Assert.Empty(pantalla.FindAll("#panel-opciones"));
        var tarjeta = Tarjeta(pantalla, Opcion);
        Assert.Contains("ALMUERZO", tarjeta.QuerySelector(".servicio-opcion")!.TextContent, StringComparison.Ordinal);
        Assert.Contains("1.800,00", tarjeta.QuerySelector(".importe-opcion")!.TextContent, StringComparison.Ordinal);
        Assert.NotEmpty(tarjeta.QuerySelectorAll(".carga"));
        Assert.Equal(Opcion, tarjeta.QuerySelector(".muestra-opcion .nombre")!.TextContent.Trim());
    }

    [Fact(DisplayName = "administracion-local · Editar desde una opción: «Editar» en «Almuerzo opcional» abre la edición en el lugar del catálogo y enfoca su nombre")]
    public async Task Editar_desde_una_opcion()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirOpciones(pantalla);
        var indice = Tarjeta(pantalla, Opcion).GetAttribute("data-indice");

        await Tarjeta(pantalla, Opcion).QuerySelector("button.editar-opcion")!.ClickAsync(new());

        Assert.NotNull(pantalla.Find("#panel-opciones"));
        Assert.Empty(pantalla.FindAll("#catalogo-opciones"));
        pantalla.WaitForAssertion(() => Assert.Contains(puesto.Contexto.JSInterop.Invocations,
            i => i.Identifier == "incomedor.enfocar" && Equals(i.Arguments[0], $"article.opcion-admin[data-indice=\"{indice}\"] input.nombre-opcion")));
    }

    [Fact(DisplayName = "administracion-local · Un fondo de color con transparencia: el tercer color y 25 % se guardan sin crear versión, la muestra los usa y la general tiene OPTION_STYLE_CHANGED")]
    public async Task Un_fondo_de_color_con_transparencia()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirOpciones(pantalla);
        await Tarjeta(pantalla, Opcion).QuerySelector("button.personalizar-opcion")!.ClickAsync(new());

        await pantalla.FindAll("#personalizar-opcion button.color-opcion")[2].ClickAsync(new());
        await pantalla.FindAll("#personalizar-opcion button.transparencia-opcion").Single(b => b.TextContent.Trim() == "25 %").ClickAsync(new());
        await pantalla.Find("#guardar-presentacion").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#personalizar-opcion"));
        Assert.Equal(MensajesAtencion.PresentacionGuardada(Opcion), pantalla.Find("#aviso-guardado").TextContent.Trim());
        Assert.Contains(PresentacionOpcion.Paleta[2], Tarjeta(pantalla, Opcion).QuerySelector(".muestra-opcion")!.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal(1, puesto.Servicio.Administracion!.Leer().VersionConfiguracion);
        var contexto = ContextoDe(puesto.General().Single(l => Tipo(l) == "OPTION_STYLE_CHANGED"));
        Assert.Equal(PresentacionOpcion.Paleta[2], contexto.GetProperty("Color").GetString());
        Assert.Equal(25, contexto.GetProperty("Transparency").GetInt32());
        Assert.True(contexto.GetProperty("PriceVisible").GetBoolean());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, contexto.GetProperty("ImageSha256").ValueKind);
        Assert.Equal(PresentacionOpcion.Paleta[2], new AlmacenPresentacionOpciones(puesto.Carpeta).De(IdDe(puesto, Opcion)).Color);
    }

    [Fact(DisplayName = "administracion-local · Una imagen de fondo: un PNG válido se ve en la muestra y la general registra su huella, no su contenido; uno inválido se rechaza con el aviso de los logos")]
    public async Task Una_imagen_de_fondo()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirOpciones(pantalla);
        await Tarjeta(pantalla, Opcion).QuerySelector("button.personalizar-opcion")!.ClickAsync(new());

        ArchivoDeImagen(pantalla).UploadFiles(InputFileContent.CreateFromText("esto no es una imagen", "fondo.png"));
        Assert.Equal(MensajesAtencion.LogoInvalido, pantalla.Find("#aviso-imagen-opcion").TextContent.Trim());

        ArchivoDeImagen(pantalla).UploadFiles(InputFileContent.CreateFromBinary(Png, "fondo.png"));
        Assert.Empty(pantalla.FindAll("#aviso-imagen-opcion"));
        Assert.Contains("data:image/png;base64,", pantalla.Find("#personalizar-opcion .muestra-opcion").GetAttribute("style"), StringComparison.Ordinal);
        await pantalla.Find("#guardar-presentacion").ClickAsync(new());

        var linea = puesto.General().Single(l => Tipo(l) == "OPTION_STYLE_CHANGED");
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Png)), ContextoDe(linea).GetProperty("ImageSha256").GetString());
        Assert.DoesNotContain(Convert.ToBase64String(Png), linea.GetRawText(), StringComparison.Ordinal);
        Assert.Contains("data:image/png;base64,", Tarjeta(pantalla, Opcion).QuerySelector(".muestra-opcion")!.GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "administracion-local · Ocultar el precio: con «Mostrar el precio» apagado, la muestra de «Almuerzo opcional» no tiene importe y la general registra PriceVisible en falso")]
    public async Task Ocultar_el_precio()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirOpciones(pantalla);
        Assert.NotNull(Tarjeta(pantalla, Opcion).QuerySelector(".muestra-opcion .cargo"));
        await Tarjeta(pantalla, Opcion).QuerySelector("button.personalizar-opcion")!.ClickAsync(new());

        await pantalla.Find("#precio-visible").ClickAsync(new());
        Assert.Equal("false", pantalla.Find("#precio-visible").GetAttribute("aria-pressed"));
        await pantalla.Find("#guardar-presentacion").ClickAsync(new());

        Assert.Null(Tarjeta(pantalla, Opcion).QuerySelector(".muestra-opcion .cargo"));
        Assert.False(ContextoDe(puesto.General().Single(l => Tipo(l) == "OPTION_STYLE_CHANGED")).GetProperty("PriceVisible").GetBoolean());
    }

    [Fact(DisplayName = "administracion-local · Cancelar la personalización: con un color elegido, «Cancelar» deja la muestra como estaba y no escribe nada")]
    public async Task Cancelar_la_personalizacion()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirOpciones(pantalla);
        var antes = Tarjeta(pantalla, Opcion).QuerySelector(".muestra-opcion")!.GetAttribute("style");
        await Tarjeta(pantalla, Opcion).QuerySelector("button.personalizar-opcion")!.ClickAsync(new());

        await pantalla.FindAll("#personalizar-opcion button.color-opcion")[0].ClickAsync(new());
        await pantalla.Find("#cancelar-presentacion").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#personalizar-opcion"));
        Assert.Equal(antes, Tarjeta(pantalla, Opcion).QuerySelector(".muestra-opcion")!.GetAttribute("style"));
        Assert.False(File.Exists(Path.Combine(puesto.Carpeta, "presentacion-opciones.json")));
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "OPTION_STYLE_CHANGED");
    }

    private static PuestoDePrueba Preparar()
    {
        var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        puesto.Contexto.Services.AddSingleton(new AlmacenPresentacionOpciones(puesto.Carpeta));
        return puesto;
    }

    private static async Task AbrirOpciones(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-opciones").ClickAsync(new());
    }

    private static IElement Tarjeta(IRenderedComponent<PantallaAtencion> pantalla, string nombre) =>
        pantalla.FindAll("#catalogo-opciones article.opcion-catalogo").Single(a => a.QuerySelector(".nombre-opcion")!.TextContent.Trim() == nombre);

    private static IRenderedComponent<InputFile> ArchivoDeImagen(IRenderedComponent<PantallaAtencion> pantalla) =>
        pantalla.FindComponents<InputFile>().Single(c => c.Find("input").Id == "archivo-imagen-opcion");

    private static Guid IdDe(PuestoDePrueba puesto, string nombre) =>
        puesto.Servicio.Administracion!.Leer().Opciones.Single(o => o.Nombre == nombre).Id;
}
