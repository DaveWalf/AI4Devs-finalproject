using System.Text;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Los logos locales de la empresa y la concesionaria (D4 de presentar-atencion-tactil): archivos en marcas\ de una
/// carpeta temporal, validados por tamaño y contenido, y entregados como URI data: para un img.
/// </summary>
public class LogosLocalesTests : IDisposable
{
    private const int DosMegas = 2 * 1024 * 1024;
    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "incomedor-tests", "logos-" + Guid.NewGuid().ToString("N"));
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, recursive: true);
        }
    }

    [Fact(DisplayName = "presentacion-puesto · Logo local de la empresa: un PNG válido de 40 KB se entrega como data:image/png, y la concesionaria queda la de ejemplo")]
    public void Png_valido_de_la_empresa()
    {
        var png = Png(40 * 1024);
        Escribir("empresa.png", png);

        var logos = new LogosLocales(_carpeta).Leer();

        Assert.Equal("data:image/png;base64," + Convert.ToBase64String(png), logos.Empresa);
        Assert.Equal(LogosLocales.ConcesionariaDeEjemplo, logos.Concesionaria);
    }

    [Fact(DisplayName = "presentacion-puesto · WebP y SVG válidos se entregan como data: de su tipo")]
    public void Webp_y_svg_validos()
    {
        Escribir("empresa.webp", Webp(1024));
        Escribir("concesionaria.svg", Encoding.UTF8.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="10" height="10"/></svg>"""));

        var logos = new LogosLocales(_carpeta).Leer();

        Assert.StartsWith("data:image/webp;base64,", logos.Empresa);
        Assert.StartsWith("data:image/svg+xml;base64,", logos.Concesionaria);
    }

    [Fact(DisplayName = "presentacion-puesto · Exactamente 2 MB se acepta; 2 MB más un byte, no: queda el de ejemplo")]
    public void Tope_de_dos_megas()
    {
        Escribir("empresa.png", Png(DosMegas));
        Escribir("concesionaria.webp", Webp(DosMegas + 1));

        var logos = new LogosLocales(_carpeta).Leer();

        Assert.StartsWith("data:image/png;base64,", logos.Empresa);
        Assert.Equal(LogosLocales.ConcesionariaDeEjemplo, logos.Concesionaria);
    }

    [Fact(DisplayName = "presentacion-puesto · Contenido que no corresponde a la extensión: un .png con texto se rechaza")]
    public void Png_con_texto_se_rechaza()
    {
        Escribir("empresa.png", Encoding.UTF8.GetBytes("esto no es una imagen"));

        Assert.Equal(LogosLocales.EmpresaDeEjemplo, new LogosLocales(_carpeta).Leer().Empresa);
    }

    [Fact(DisplayName = "OWASP A05 · Un SVG con DTD se rechaza")]
    public void Svg_con_dtd_se_rechaza()
    {
        Escribir("empresa.svg", Encoding.UTF8.GetBytes(
            """<?xml version="1.0"?><!DOCTYPE svg [<!ENTITY x "y">]><svg xmlns="http://www.w3.org/2000/svg">&x;</svg>"""));

        Assert.Equal(LogosLocales.EmpresaDeEjemplo, new LogosLocales(_carpeta).Leer().Empresa);
    }

    [Fact(DisplayName = "OWASP A05 · Un SVG cuyo elemento raíz no es svg, o mal formado, se rechaza")]
    public void Svg_que_no_es_svg_se_rechaza()
    {
        Escribir("empresa.svg", Encoding.UTF8.GetBytes("<html><body/></html>"));
        Escribir("concesionaria.svg", Encoding.UTF8.GetBytes("<svg><g></svg>"));

        var logos = new LogosLocales(_carpeta).Leer();

        Assert.Equal(LogosLocales.EmpresaDeEjemplo, logos.Empresa);
        Assert.Equal(LogosLocales.ConcesionariaDeEjemplo, logos.Concesionaria);
    }

    [Fact(DisplayName = "OWASP A03 · Un SVG con un script se muestra como imagen: la pantalla lo pone en un img y su marcado no contiene <script")]
    public void Svg_con_script_se_muestra_como_imagen()
    {
        Escribir("empresa.svg", Encoding.UTF8.GetBytes(
            """<svg xmlns="http://www.w3.org/2000/svg"><script>alert('x')</script><rect width="5" height="5"/></svg>"""));

        var pantalla = Dibujar();

        var logo = pantalla.Find("header img.logo-empresa");
        Assert.StartsWith("data:image/svg+xml;base64,", logo.GetAttribute("src"));
        Assert.DoesNotContain("<script", pantalla.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(pantalla.FindAll("svg"));
    }

    [Fact(DisplayName = "presentacion-puesto · Con empresa.png y empresa.svg gana el .png")]
    public void Gana_el_png()
    {
        Escribir("empresa.png", Png(100));
        Escribir("empresa.svg", Encoding.UTF8.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg"/>"""));

        Assert.StartsWith("data:image/png;base64,", new LogosLocales(_carpeta).Leer().Empresa);
    }

    [Fact(DisplayName = "presentacion-puesto · Sin carpeta marcas, los logos de ejemplo; la pantalla los muestra y atiende")]
    public void Sin_carpeta_los_de_ejemplo()
    {
        var logos = new LogosLocales(_carpeta).Leer();

        Assert.Equal("img/empresa-ejemplo.svg", logos.Empresa);
        Assert.Equal("img/concesionaria-ejemplo.svg", logos.Concesionaria);
        var pantalla = Dibujar();
        Assert.Equal("img/empresa-ejemplo.svg", pantalla.Find("header img.logo-empresa").GetAttribute("src"));
        Assert.NotNull(pantalla.Find("#codigo"));
    }

    private IRenderedComponent<PantallaAtencion> Dibujar()
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, new RelojManual("2026-10-30T15:00:00Z"), new ImpresoraDePrueba());
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        _contexto.Services.AddSingleton(new LogosLocales(_carpeta));
        return _contexto.Render<PantallaAtencion>();
    }

    private void Escribir(string nombre, byte[] contenido)
    {
        var marcas = Path.Combine(_carpeta, "marcas");
        Directory.CreateDirectory(marcas);
        File.WriteAllBytes(Path.Combine(marcas, nombre), contenido);
    }

    private static byte[] Png(int largo)
    {
        var bytes = new byte[largo];
        FirmaPng.CopyTo(bytes, 0);
        return bytes;
    }

    private static byte[] Webp(int largo)
    {
        var bytes = new byte[largo];
        "RIFF"u8.CopyTo(bytes);
        "WEBP"u8.CopyTo(bytes.AsSpan(8));
        return bytes;
    }
}
