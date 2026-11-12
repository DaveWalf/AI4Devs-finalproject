using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La versión del programa en pantalla (D3 y D4 de mostrar-version-del-programa, y D1 a D3 de mover-version-al-pie): en el
/// centro del pie, en reposo y durante una atención, sin ser un control; y al final de General, «Versión del programa» con la versión, el commit, la
/// fecha de compilación y .NET, sin rutas ni datos del equipo. El Kiosk de las pruebas se compila sin tag: dice «desarrollo».
/// </summary>
public class VersionEnPantallaTests
{
    [Fact(DisplayName = "presentacion-puesto · Un build de desarrollo: en reposo, el pie dice «desarrollo · » y el commit, y la cabecera, con «INComedor · tu pausa del día», no tiene la versión y sigue sin botones")]
    public void La_version_en_reposo()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        var cabecera = pantalla.Find("header.cabecera");
        Assert.Equal("INComedor · tu pausa del día", cabecera.QuerySelector(".servicio")!.TextContent.Trim());
        Assert.Null(cabecera.QuerySelector("#version-programa"));
        var version = pantalla.Find("footer.pie #version-programa");
        Assert.Equal(VersionPrograma.DelPuesto().Cabecera, version.TextContent.Trim());
        Assert.StartsWith("desarrollo · ", version.TextContent.Trim(), StringComparison.Ordinal);
        Assert.Empty(cabecera.QuerySelectorAll("button"));
        Assert.Null(version.GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "presentacion-puesto · Un paquete publicado: con una persona en la selección, el pie sigue mostrando la versión")]
    public async Task La_version_durante_una_atencion()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());

        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        Assert.Equal(VersionPrograma.DelPuesto().Cabecera, pantalla.Find("footer.pie #version-programa").TextContent.Trim());
    }

    [Fact(DisplayName = "configuracion-puesto · La versión de un build de desarrollo: la última sección de General es «Versión del programa», con «desarrollo», el commit, la fecha de compilación y .NET")]
    public async Task La_version_en_general()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-general").ClickAsync(new());

        var ultima = pantalla.Find("#seccion-general").Children.Last();
        Assert.Equal("version-programa-general", ultima.Id);
        Assert.Equal("Versión del programa", ultima.QuerySelector("h3")!.TextContent.Trim());
        var esperada = VersionPrograma.DelPuesto();
        var datos = ultima.QuerySelectorAll("dt").Zip(ultima.QuerySelectorAll("dd"), (t, d) => (t.TextContent.Trim(), d.TextContent.Trim())).ToList();
        Assert.Equal(
            [("Versión", "desarrollo"), ("Commit", esperada.Commit),
             ("Compilado", esperada.CompiladoEn(TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"))), (".NET", esperada.Net)],
            datos);
    }

    [Fact(DisplayName = "configuracion-puesto · Sin datos del equipo: la sección «Versión del programa» no contiene la carpeta de datos, el nombre del equipo ni el del usuario")]
    public async Task Sin_datos_del_equipo()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-general").ClickAsync(new());

        var seccion = pantalla.Find("#version-programa-general").OuterHtml;
        Assert.DoesNotContain(puesto.Carpeta, seccion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, seccion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.UserName, seccion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":\\", seccion, StringComparison.Ordinal);
    }
}
