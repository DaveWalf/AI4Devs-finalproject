using System.Security.Cryptography;
using System.Text;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components.Forms;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Los logos desde la pestaña Identidad visual (presentacion-puesto y configuracion-puesto de configurar-puesto-por-pestanas,
/// D14): <c>InputFile</c> con las reglas de <see cref="LogosLocales"/>, la vista previa como img sobre una cuadrícula, el
/// guardado en marcas\ de la carpeta de datos y la bitácora con la huella, nunca el contenido.
/// </summary>
public class LogosDesdeConfiguracionTests
{
    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact(DisplayName = "presentacion-puesto · Elegir y guardar el logo de la empresa: un PNG de 40 KB se ve sobre la cuadrícula, se guarda y queda en la cabecera y en un puesto nuevo")]
    public async Task Elegir_y_guardar_el_logo_de_la_empresa()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirIdentidad(puesto);
        var png = Png(40 * 1024);
        var uri = "data:image/png;base64," + Convert.ToBase64String(png);

        Archivo(pantalla, LogoPuesto.Empresa).UploadFiles(InputFileContent.CreateFromBinary(png, "logo-planta.png"));

        Assert.Equal(uri, pantalla.Find(".cuadricula-logo #vista-logo-empresa").GetAttribute("src"));
        Assert.Equal(LogosLocales.EmpresaDeEjemplo, pantalla.Find("img.logo-empresa").GetAttribute("src"));

        await pantalla.Find("#guardar-logos").ClickAsync(new());

        Assert.Equal(uri, pantalla.Find("img.logo-empresa").GetAttribute("src"));
        Assert.Equal(new Logos(uri, LogosLocales.ConcesionariaDeEjemplo), new LogosLocales(puesto.Carpeta).Leer());
        Assert.Equal(MensajesAtencion.LogosGuardados, pantalla.Find("#aviso-guardado").TextContent.Trim());
    }

    [Fact(DisplayName = "presentacion-puesto · Archivo de más de 2 MB: un WebP de 2 MB más un byte como logo de la concesionaria dice que pasa de 2 MB y el logo vigente no cambia")]
    public async Task Archivo_de_mas_de_2_MB()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirIdentidad(puesto);
        var webp = new byte[LogosLocales.TamanoMaximo + 1];
        "RIFF"u8.CopyTo(webp);
        "WEBP"u8.CopyTo(webp.AsSpan(8));

        Archivo(pantalla, LogoPuesto.Concesionaria).UploadFiles(InputFileContent.CreateFromBinary(webp, "grande.webp"));

        Assert.Equal(MensajesAtencion.LogoPasaDe2MB, pantalla.Find("#aviso-logo-concesionaria").TextContent.Trim());
        Assert.Equal(LogosLocales.ConcesionariaDeEjemplo, pantalla.Find("#vista-logo-concesionaria").GetAttribute("src"));
        Assert.Equal(LogosLocales.ConcesionariaDeEjemplo, pantalla.Find("img.logo-concesionaria").GetAttribute("src"));
        Assert.Empty(pantalla.FindAll("#guardar-logos"));
        Assert.False(Directory.Exists(Path.Combine(puesto.Carpeta, "marcas")));
    }

    [Fact(DisplayName = "presentacion-puesto · Contenido que no corresponde a la extensión: un .png de texto dice que no es una imagen válida y no ofrece guardarlo")]
    public async Task Contenido_que_no_corresponde()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirIdentidad(puesto);

        Archivo(pantalla, LogoPuesto.Empresa).UploadFiles(InputFileContent.CreateFromText("esto no es una imagen", "logo.png"));

        Assert.Equal(MensajesAtencion.LogoInvalido, pantalla.Find("#aviso-logo-empresa").TextContent.Trim());
        Assert.Equal(LogosLocales.EmpresaDeEjemplo, pantalla.Find("#vista-logo-empresa").GetAttribute("src"));
        Assert.Empty(pantalla.FindAll("#guardar-logos"));
    }

    [Fact(DisplayName = "presentacion-puesto · Un SVG con un script en la vista previa: se ve como el contenido de un img y la página no contiene el elemento script")]
    public async Task Un_svg_con_script()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirIdentidad(puesto);
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><script>alert('logo')</script><rect width=\"10\" height=\"10\"/></svg>";

        Archivo(pantalla, LogoPuesto.Empresa).UploadFiles(InputFileContent.CreateFromText(svg, "logo.svg"));

        var vista = pantalla.Find("#vista-logo-empresa");
        Assert.Equal("IMG", vista.TagName);
        Assert.Equal("data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg)), vista.GetAttribute("src"));
        Assert.Empty(pantalla.FindAll("script"));
        Assert.DoesNotContain("alert(", pantalla.Markup, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "presentacion-puesto · Volver al de ejemplo: con un logo de la empresa guardado, «Volver al de ejemplo» y guardar deja el de ejemplo en la cabecera y borra el archivo")]
    public async Task Volver_al_de_ejemplo()
    {
        using var puesto = new PuestoDePrueba();
        var marcas = Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "marcas")).FullName;
        File.WriteAllBytes(Path.Combine(marcas, "empresa.png"), Png(2048));
        var pantalla = await AbrirIdentidad(puesto);
        Assert.StartsWith("data:image/png;base64,", pantalla.Find("img.logo-empresa").GetAttribute("src"));

        await pantalla.Find("#ejemplo-logo-empresa").ClickAsync(new());
        Assert.Equal(LogosLocales.EmpresaDeEjemplo, pantalla.Find("#vista-logo-empresa").GetAttribute("src"));
        await pantalla.Find("#guardar-logos").ClickAsync(new());

        Assert.Equal(LogosLocales.EmpresaDeEjemplo, pantalla.Find("img.logo-empresa").GetAttribute("src"));
        Assert.Empty(Directory.GetFiles(marcas, "empresa.*"));
        var cambio = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"));
        Assert.True(cambio.GetProperty("After").GetProperty("Example").GetBoolean());
        Assert.Equal("PNG", cambio.GetProperty("Before").GetProperty("Format").GetString());
    }

    [Fact(DisplayName = "configuracion-puesto · Un logo guardado: la general tiene CONFIGURATION_CHANGED con COMPANY_LOGO, su SHA-256, 40 KB y PNG, el actor nulo y MAINTENANCE_MODE, sin el contenido del archivo")]
    public async Task Un_logo_guardado_en_la_bitacora()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirIdentidad(puesto);
        var png = Png(40 * 1024);
        Archivo(pantalla, LogoPuesto.Empresa).UploadFiles(InputFileContent.CreateFromBinary(png, "logo.png"));

        await pantalla.Find("#guardar-logos").ClickAsync(new());

        var linea = Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED");
        var cambio = ContextoDe(linea);
        Assert.Equal(("COMPANY_LOGO", "APPLIED", "MAINTENANCE_MODE"),
            (cambio.GetProperty("Setting").GetString(), cambio.GetProperty("Result").GetString(), cambio.GetProperty("Access").GetString()));
        var despues = cambio.GetProperty("After");
        Assert.Equal(Convert.ToHexString(SHA256.HashData(png)), despues.GetProperty("Sha256").GetString());
        Assert.Equal((40 * 1024, "PNG"), (despues.GetProperty("Size").GetInt32(), despues.GetProperty("Format").GetString()));
        Assert.True(cambio.GetProperty("Before").GetProperty("Example").GetBoolean());
        Assert.True(SinActor(cambio));
        Assert.DoesNotContain(Convert.ToBase64String(png)[..40], linea.GetRawText(), StringComparison.Ordinal);
    }

    private static async Task<IRenderedComponent<PantallaAtencion>> AbrirIdentidad(PuestoDePrueba puesto)
    {
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#pestana-identidad").ClickAsync(new());
        return pantalla;
    }

    /// <summary>El InputFile de cada logo, en el orden de la pestaña: la empresa y la concesionaria.</summary>
    private static IRenderedComponent<InputFile> Archivo(IRenderedComponent<PantallaAtencion> pantalla, LogoPuesto logo) =>
        pantalla.FindComponents<InputFile>()[(int)logo];

    private static byte[] Png(int tamano)
    {
        var png = new byte[tamano];
        FirmaPng.CopyTo(png, 0);
        return png;
    }
}
