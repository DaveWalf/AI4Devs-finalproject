using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La versión del programa en el pie (D1 a D3 de mover-version-al-pie): sale de la cabecera y va al centro del pie, con
/// el texto de hoy, sin la palabra «versión», en reposo y durante una atención, sin ser un control. El Kiosk de las
/// pruebas se compila sin tag: dice «desarrollo».
/// </summary>
public class VersionEnElPieTests
{
    [Fact(DisplayName = "presentacion-puesto · Un build de desarrollo: en reposo, la cabecera no tiene la versión y el pie tiene una sola, «desarrollo · » y el commit, sin «versión» y sin ser un control")]
    public void La_version_en_el_pie_en_reposo()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        Assert.Null(pantalla.Find("header.cabecera").QuerySelector("#version-programa"));
        var pie = pantalla.Find("footer.pie");
        var versiones = pie.QuerySelectorAll("#version-programa");
        var version = Assert.Single(versiones);
        var texto = version.TextContent.Trim();
        Assert.Equal(VersionPrograma.DelPuesto().Cabecera, texto);
        Assert.StartsWith("desarrollo · ", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("versión", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Null(version.GetAttribute("tabindex"));
        Assert.NotEqual("BUTTON", version.TagName);
        Assert.Null(version.Closest("button"));
    }

    [Fact(DisplayName = "presentacion-puesto · Un paquete publicado: con una persona en la selección, la versión sigue en el pie y no aparece en la cabecera")]
    public async Task La_version_en_el_pie_durante_una_atencion()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());

        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        Assert.Equal(VersionPrograma.DelPuesto().Cabecera, pantalla.Find("footer.pie #version-programa").TextContent.Trim());
        Assert.Null(pantalla.Find("header.cabecera").QuerySelector("#version-programa"));
    }
}
