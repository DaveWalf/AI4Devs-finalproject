using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La cabecera y el pie del puesto (D3 y D5 de presentar-atencion-tactil), con bUnit sobre un <see cref="ServicioAtencion"/>
/// real y una base temporal: un solo control en reposo, ninguno durante la atención, los logos de ejemplo y la marca.
/// </summary>
public class CabeceraTests : IDisposable
{
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
    }

    [Fact(DisplayName = "presentacion-puesto · Un solo control en la cabecera: en reposo hay un botón, con el nombre «Configuración»")]
    public void En_reposo_un_solo_boton_configuracion()
    {
        var pantalla = Dibujar();

        var botones = pantalla.Find("header").QuerySelectorAll("button");

        var boton = Assert.Single(botones);
        Assert.Equal("Configuración", boton.GetAttribute("aria-label"));
        Assert.Equal("button", boton.GetAttribute("type"));
        Assert.Contains("engranaje", boton.ClassName);
    }

    [Fact(DisplayName = "presentacion-puesto · Sin engranaje durante una atención: con una persona en la selección no hay ningún botón «Configuración»")]
    public async Task Sin_engranaje_durante_la_seleccion()
    {
        var pantalla = Dibujar();
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());

        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        Assert.Empty(pantalla.Find("header").QuerySelectorAll("button"));
        Assert.Empty(pantalla.FindAll("[aria-label=\"Configuración\"]"));
    }

    [Fact(DisplayName = "presentacion-puesto · Sin logos locales, la cabecera muestra los de ejemplo con su texto alternativo y el nombre del servicio")]
    public void Logos_de_ejemplo_con_texto_alternativo()
    {
        var pantalla = Dibujar();

        var cabecera = pantalla.Find("header");
        var empresa = cabecera.QuerySelector("img.logo-empresa")!;
        var concesionaria = cabecera.QuerySelector("img.logo-concesionaria")!;
        Assert.Equal("Logo de la empresa", empresa.GetAttribute("alt"));
        Assert.Equal("img/empresa-ejemplo.svg", empresa.GetAttribute("src"));
        Assert.Equal("Logo de la concesionaria", concesionaria.GetAttribute("alt"));
        Assert.Equal("img/concesionaria-ejemplo.svg", concesionaria.GetAttribute("src"));
        Assert.Contains("Comedor", cabecera.QuerySelector(".servicio")!.TextContent);
    }

    [Fact(DisplayName = "presentacion-puesto · El pie lleva la marca Intelektron y la franja de sincronización con su rol y su nombre")]
    public void Pie_con_la_marca_y_la_sincronizacion()
    {
        var pantalla = Dibujar();

        var pie = pantalla.Find("footer");
        var marca = pie.QuerySelector("img.marca-intelektron")!;
        Assert.Equal("Intelektron", marca.GetAttribute("alt"));
        Assert.Equal("img/intelektron.svg", marca.GetAttribute("src"));
        var franja = pie.QuerySelector("[role=\"status\"]")!;
        Assert.Equal("Sincronización con el receptor", franja.GetAttribute("aria-label"));
    }

    private IRenderedComponent<PantallaAtencion> Dibujar()
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba());
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        return _contexto.Render<PantallaAtencion>();
    }
}
