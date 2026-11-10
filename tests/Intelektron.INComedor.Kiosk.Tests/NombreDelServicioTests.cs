using Bunit;
using Microsoft.AspNetCore.Components;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El nombre del servicio en la cabecera (presentacion-puesto de ordenar-configuracion, D7): «INComedor · tu pausa del día»,
/// pedido de David del 10/11, en reposo y durante una atención. bUnit sobre el puesto de prueba.
/// </summary>
public class NombreDelServicioTests
{
    private const string Nombre = "INComedor · tu pausa del día";

    [Fact(DisplayName = "presentacion-puesto · El nombre del servicio en la cabecera: dice «INComedor · tu pausa del día» en reposo y con una persona en la selección")]
    public async Task El_nombre_del_servicio_en_la_cabecera()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        Assert.Equal(Nombre, pantalla.Find(".cabecera .servicio").TextContent.Trim());

        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());

        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        Assert.Equal(Nombre, pantalla.Find(".cabecera .servicio").TextContent.Trim());
    }
}
