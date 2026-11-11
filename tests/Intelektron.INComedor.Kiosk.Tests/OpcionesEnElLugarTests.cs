using System.Security.Cryptography;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
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

    [Fact(DisplayName = "administracion-local · Editar desde una opción: «Editar» en «Almuerzo opcional» abre la edición en el lugar del catálogo, enfoca su nombre y «Guardar» crea una sola versión")]
    public async Task Editar_desde_una_opcion()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirOpciones(pantalla);
        var indice = Tarjeta(pantalla, Opcion).GetAttribute("data-indice");
        var antes = puesto.Servicio.Administracion!.Leer().VersionConfiguracion;

        await Tarjeta(pantalla, Opcion).QuerySelector("button.editar-opcion")!.ClickAsync(new());

        Assert.NotNull(pantalla.Find("#panel-opciones"));
        Assert.Empty(pantalla.FindAll("#catalogo-opciones"));
        pantalla.WaitForAssertion(() => Assert.Contains(puesto.Contexto.JSInterop.Invocations,
            i => i.Identifier == "incomedor.enfocar" && Equals(i.Arguments[0], $"article.opcion-admin[data-indice=\"{indice}\"] input.nombre-opcion")));
        await pantalla.Find($"article.opcion-admin[data-indice=\"{indice}\"] input.importe-opcion").ChangeAsync(new ChangeEventArgs { Value = "1900,00" });
        await pantalla.Find("#guardar-panel").ClickAsync(new());
        Assert.Equal(antes + 1, puesto.Servicio.Administracion.Leer().VersionConfiguracion);
        Assert.Contains("1.900,00", Tarjeta(pantalla, Opcion).QuerySelector(".importe-opcion")!.TextContent, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "administracion-local · Un fondo de color con transparencia: el tercer color y 25 % se guardan sin crear versión, la general tiene CONFIGURATION_CHANGED de OPTION_STYLE con el antes y el después, y al reabrir Configuración la muestra los conserva")]
    public async Task Un_fondo_de_color_con_transparencia()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirOpciones(pantalla);
        var version = puesto.Servicio.Administracion!.Leer().VersionConfiguracion;
        await Tarjeta(pantalla, Opcion).QuerySelector("button.personalizar-opcion")!.ClickAsync(new());

        await pantalla.FindAll("#personalizar-opcion button.color-opcion")[2].ClickAsync(new());
        await pantalla.FindAll("#personalizar-opcion button.transparencia-opcion").Single(b => b.TextContent.Trim() == "25 %").ClickAsync(new());
        await pantalla.Find("#guardar-presentacion").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#personalizar-opcion"));
        Assert.Equal(MensajesAtencion.PresentacionGuardada(Opcion), pantalla.Find("#aviso-guardado").TextContent.Trim());
        Assert.Equal(version, puesto.Servicio.Administracion.Leer().VersionConfiguracion);
        var contexto = Registro(puesto);
        Assert.Equal(IdDe(puesto, Opcion), contexto.GetProperty("OptionId").GetGuid());
        Assert.Equal("APPLIED", contexto.GetProperty("Result").GetString());
        Assert.Equal(JsonValueKind.Null, contexto.GetProperty("Before").GetProperty("Color").ValueKind);
        Assert.Equal(PresentacionOpcion.Paleta[2], contexto.GetProperty("After").GetProperty("Color").GetString());
        Assert.Equal(25, contexto.GetProperty("After").GetProperty("Transparency").GetInt32());
        Assert.True(contexto.GetProperty("After").GetProperty("PriceVisible").GetBoolean());
        Assert.Equal(JsonValueKind.Null, contexto.GetProperty("After").GetProperty("ImageSha256").ValueKind);

        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        await AbrirOpciones(pantalla);
        Assert.Contains(PresentacionOpcion.Paleta[2], Tarjeta(pantalla, Opcion).QuerySelector(".muestra-opcion")!.GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "administracion-local · Una imagen de fondo: un archivo inválido no escribe nada; un PNG válido se ve en la muestra y la general registra su huella, no su contenido")]
    public async Task Una_imagen_de_fondo()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirOpciones(pantalla);
        await Tarjeta(pantalla, Opcion).QuerySelector("button.personalizar-opcion")!.ClickAsync(new());

        ArchivoDeImagen(pantalla).UploadFiles(InputFileContent.CreateFromText("esto no es una imagen", "fondo.png"));
        Assert.Equal(MensajesAtencion.LogoInvalido, pantalla.Find("#aviso-imagen-opcion").TextContent.Trim());
        Assert.False(Directory.Exists(Path.Combine(puesto.Carpeta, "opciones")));
        Assert.DoesNotContain(puesto.General(), EsDeOpcion);

        ArchivoDeImagen(pantalla).UploadFiles(InputFileContent.CreateFromBinary(Png, "fondo.png"));
        Assert.Empty(pantalla.FindAll("#aviso-imagen-opcion"));
        Assert.Contains("data:image/png;base64,", pantalla.Find("#personalizar-opcion .muestra-opcion").GetAttribute("style"), StringComparison.Ordinal);
        await pantalla.Find("#guardar-presentacion").ClickAsync(new());

        var linea = puesto.General().Single(EsDeOpcion);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Png)), ContextoDe(linea).GetProperty("After").GetProperty("ImageSha256").GetString());
        Assert.DoesNotContain(Convert.ToBase64String(Png), linea.GetRawText(), StringComparison.Ordinal);
        Assert.Contains("data:image/png;base64,", Tarjeta(pantalla, Opcion).QuerySelector(".muestra-opcion")!.GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "administracion-local · Una imagen de fondo: un archivo inválido elegido después de uno válido descarta también el válido, y elegir un color quita la imagen y una imagen quita el color")]
    public async Task La_imagen_y_el_color_se_reemplazan()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirOpciones(pantalla);
        await Tarjeta(pantalla, Opcion).QuerySelector("button.personalizar-opcion")!.ClickAsync(new());
        string? Muestra() => pantalla.Find("#personalizar-opcion .muestra-opcion").GetAttribute("style");

        ArchivoDeImagen(pantalla).UploadFiles(InputFileContent.CreateFromBinary(Png, "fondo.png"));
        ArchivoDeImagen(pantalla).UploadFiles(InputFileContent.CreateFromText("esto no es una imagen", "otra.png"));
        Assert.DoesNotContain("data:image", Muestra() ?? string.Empty, StringComparison.Ordinal);

        ArchivoDeImagen(pantalla).UploadFiles(InputFileContent.CreateFromBinary(Png, "fondo.png"));
        await pantalla.FindAll("#personalizar-opcion button.color-opcion")[1].ClickAsync(new());
        Assert.DoesNotContain("data:image", Muestra()!, StringComparison.Ordinal);
        Assert.Contains(PresentacionOpcion.Paleta[1], Muestra()!, StringComparison.Ordinal);

        ArchivoDeImagen(pantalla).UploadFiles(InputFileContent.CreateFromBinary(Png, "fondo.png"));
        Assert.Contains("data:image", Muestra()!, StringComparison.Ordinal);
        Assert.DoesNotContain(PresentacionOpcion.Paleta[1], Muestra()!, StringComparison.Ordinal);
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
        Assert.False(Registro(puesto).GetProperty("After").GetProperty("PriceVisible").GetBoolean());
    }

    [Fact(DisplayName = "administracion-local · Guardar sin poder escribir: el cartel sigue abierto con el aviso, nada cambia y la general tiene CONFIGURATION_CHANGED de OPTION_STYLE con el resultado fallido")]
    public async Task Guardar_sin_poder_escribir()
    {
        using var puesto = Preparar();
        // Una carpeta con el nombre del temporal: escribirlo falla como en una carpeta sin escritura.
        Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "presentacion-opciones.json.tmp"));
        var pantalla = puesto.Dibujar();
        await AbrirOpciones(pantalla);
        await Tarjeta(pantalla, Opcion).QuerySelector("button.personalizar-opcion")!.ClickAsync(new());

        await pantalla.FindAll("#personalizar-opcion button.color-opcion")[0].ClickAsync(new());
        await pantalla.Find("#guardar-presentacion").ClickAsync(new());

        Assert.NotNull(pantalla.Find("#personalizar-opcion"));
        Assert.Equal(MensajesAtencion.PresentacionNoGuardada, pantalla.Find("#aviso-presentacion").TextContent.Trim());
        Assert.False(File.Exists(Path.Combine(puesto.Carpeta, "presentacion-opciones.json")));
        var contexto = Registro(puesto);
        Assert.Equal("FAILED", contexto.GetProperty("Result").GetString());
        Assert.Equal(PresentacionOpcion.Paleta[0], contexto.GetProperty("After").GetProperty("Color").GetString());
        Assert.Equal(JsonValueKind.Null, contexto.GetProperty("Effective").GetProperty("Color").ValueKind);
    }

    [Fact(DisplayName = "administracion-local · Cancelar la personalización: con un color elegido, «Cancelar» o Escape dejan la muestra como estaba y no escriben nada")]
    public async Task Cancelar_o_escape()
    {
        foreach (var conEscape in new[] { false, true })
        {
            using var puesto = Preparar();
            var pantalla = puesto.Dibujar();
            await AbrirOpciones(pantalla);
            var antes = Tarjeta(pantalla, Opcion).QuerySelector(".muestra-opcion")!.GetAttribute("style");
            await Tarjeta(pantalla, Opcion).QuerySelector("button.personalizar-opcion")!.ClickAsync(new());

            await pantalla.FindAll("#personalizar-opcion button.color-opcion")[0].ClickAsync(new());
            if (conEscape)
            {
                await pantalla.Find("#personalizar-opcion button.color-opcion").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
                Assert.Empty(pantalla.FindAll("[role=\"dialog\"]"));
                await AbrirOpciones(pantalla);
            }
            else
            {
                await pantalla.Find("#cancelar-presentacion").ClickAsync(new());
            }

            Assert.Empty(pantalla.FindAll("#personalizar-opcion"));
            Assert.Equal(antes, Tarjeta(pantalla, Opcion).QuerySelector(".muestra-opcion")!.GetAttribute("style"));
            Assert.False(File.Exists(Path.Combine(puesto.Carpeta, "presentacion-opciones.json")));
            Assert.DoesNotContain(puesto.General(), EsDeOpcion);
        }
    }

    [Fact(DisplayName = "administracion-local · Personalizar una opción: con INWeb como dueño de los datos, el catálogo dice «Ver» y la presentación se guarda igual")]
    public async Task Con_inweb_como_dueno()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        using (var conexion = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False"))
        {
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "INSERT INTO tbComedorOrigenesVersiones (comvcCodigo, comvoOrigen) SELECT comvcCodigoActiva, 'INWEB' FROM tbComedorConfiguracionPuestos";
            comando.ExecuteNonQuery();
        }

        await AbrirOpciones(pantalla);
        Assert.Equal("Ver", Tarjeta(pantalla, Opcion).QuerySelector("button.editar-opcion")!.TextContent.Trim());
        await Tarjeta(pantalla, Opcion).QuerySelector("button.personalizar-opcion")!.ClickAsync(new());
        await pantalla.FindAll("#personalizar-opcion button.color-opcion")[4].ClickAsync(new());
        await pantalla.Find("#guardar-presentacion").ClickAsync(new());

        Assert.Equal("APPLIED", Registro(puesto).GetProperty("Result").GetString());
        Assert.Equal(PresentacionOpcion.Paleta[4], new AlmacenPresentacionOpciones(puesto.Carpeta).De(IdDe(puesto, Opcion)).Color);
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

    private static bool EsDeOpcion(JsonElement linea) =>
        Tipo(linea) == "CONFIGURATION_CHANGED" && ContextoDe(linea).TryGetProperty("Setting", out var ajuste) && ajuste.GetString() == "OPTION_STYLE";

    private static JsonElement Registro(PuestoDePrueba puesto) => ContextoDe(puesto.General().Single(EsDeOpcion));

    private static IElement Tarjeta(IRenderedComponent<PantallaAtencion> pantalla, string nombre) =>
        pantalla.FindAll("#catalogo-opciones article.opcion-catalogo").Single(a => a.QuerySelector(".nombre-opcion")!.TextContent.Trim() == nombre);

    private static IRenderedComponent<InputFile> ArchivoDeImagen(IRenderedComponent<PantallaAtencion> pantalla) =>
        pantalla.FindComponents<InputFile>().Single(c => c.Find("input").Id == "archivo-imagen-opcion");

    private static Guid IdDe(PuestoDePrueba puesto, string nombre) =>
        puesto.Servicio.Administracion!.Leer().Opciones.Single(o => o.Nombre == nombre).Id;
}
