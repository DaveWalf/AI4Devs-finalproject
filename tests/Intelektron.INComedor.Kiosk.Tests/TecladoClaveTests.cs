using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El teclado del diálogo de la clave del día (acceso-puesto de entrar-a-configuracion-con-teclado, D2 y D3): la disposición
/// del teclado numérico extendido con «ENTER», «Borrar» y «Limpiar», el campo enmascarado que no abre el teclado táctil del
/// sistema, el teclado físico, y el tope de 12 dígitos. En operativo, que ya pide la clave al tocar el engranaje.
/// </summary>
public class TecladoClaveTests : IDisposable
{
    private readonly PuestoDePrueba _puesto = new(ModoPuesto.Operativo);

    public void Dispose() => _puesto.Dispose();

    [Fact(DisplayName = "acceso-puesto · El teclado en pantalla: los diez dígitos en el orden del teclado numérico extendido, «Limpiar» y «ENTER» altos, el 0 ancho y «Borrar» a su lado; 4 y 7 dejan dos puntos, «Borrar» quita uno y «Limpiar» todos")]
    public async Task El_teclado_en_pantalla()
    {
        var pantalla = await AbrirDialogo();

        var teclas = pantalla.FindAll(".teclado-numerico button").Select(t => t.Id).ToList();
        Assert.Equal(["tecla-7", "tecla-8", "tecla-9", "tecla-limpiar", "tecla-4", "tecla-5", "tecla-6", "tecla-1", "tecla-2", "tecla-3",
            "aceptar-clave", "tecla-0", "tecla-borrar"], teclas);
        Assert.All(pantalla.FindAll(".teclado-numerico button"), t => Assert.Equal("button", t.GetAttribute("type")));
        Assert.Equal("ENTER", pantalla.Find("#aceptar-clave").TextContent.Trim());
        Assert.Equal("Limpiar", pantalla.Find("#tecla-limpiar").TextContent.Trim());
        Assert.Equal("Borrar", pantalla.Find("#tecla-borrar").GetAttribute("aria-label"));
        Assert.Contains("tecla-alta", pantalla.Find("#tecla-limpiar").ClassList);
        Assert.Contains("tecla-alta", pantalla.Find("#aceptar-clave").ClassList);
        Assert.Contains("tecla-ancha", pantalla.Find("#tecla-0").ClassList);
        Assert.Null(pantalla.Find("#cancelar-clave").Closest(".teclado-numerico"));

        await pantalla.Find("#tecla-4").ClickAsync(new());
        await pantalla.Find("#tecla-7").ClickAsync(new());

        var campo = pantalla.Find("#clave-del-dia");
        Assert.Equal("password", campo.GetAttribute("type"));
        Assert.Equal(2, (campo.GetAttribute("value") ?? "").Length);

        await pantalla.Find("#tecla-borrar").ClickAsync(new());
        Assert.Equal(1, (pantalla.Find("#clave-del-dia").GetAttribute("value") ?? "").Length);

        await pantalla.Find("#tecla-7").ClickAsync(new());
        await pantalla.Find("#tecla-limpiar").ClickAsync(new());
        Assert.Equal("", pantalla.Find("#clave-del-dia").GetAttribute("value") ?? "");
    }

    [Fact(DisplayName = "acceso-puesto · El teclado en pantalla abre Configuración: 4, 7, 1, 1 y «ENTER» aceptan la clave, y el campo se vacía")]
    public async Task El_teclado_en_pantalla_abre_configuracion()
    {
        var pantalla = await AbrirDialogo();

        foreach (var digito in "4711")
        {
            await pantalla.Find($"#tecla-{digito}").ClickAsync(new());
        }

        await pantalla.Find("#aceptar-clave").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        Assert.NotEmpty(pantalla.FindAll("[role=\"tablist\"]"));
    }

    [Fact(DisplayName = "acceso-puesto · El teclado físico también tipea: 4711 tipeado y Enter en el campo abren Configuración, y el campo pide no abrir el teclado táctil del sistema")]
    public async Task El_teclado_fisico_tambien_tipea()
    {
        var pantalla = await AbrirDialogo();
        var campo = pantalla.Find("#clave-del-dia");
        Assert.Equal("none", campo.GetAttribute("inputmode"));

        await campo.InputAsync(new ChangeEventArgs { Value = "4711" });
        await pantalla.Find("#clave-del-dia").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        Assert.NotEmpty(pantalla.FindAll("[role=\"tablist\"]"));
    }

    [Fact(DisplayName = "acceso-puesto · Clave vacía o con letras (con «ENTER»): «ENTER» con el campo vacío y, después, 12a4 tipeado y «ENTER», las dos veces dicen que la clave no es válida y Configuración sigue cerrada")]
    public async Task Clave_vacia_o_con_letras_con_enter()
    {
        var pantalla = await AbrirDialogo();
        Assert.Equal("ENTER", pantalla.Find("#aceptar-clave").TextContent.Trim());

        await pantalla.Find("#aceptar-clave").ClickAsync(new());
        Assert.Equal(MensajesAtencion.ClaveInvalida, pantalla.Find("#clave-invalida").TextContent.Trim());
        Assert.Empty(pantalla.FindAll("[role=\"tablist\"]"));

        await pantalla.Find("#clave-del-dia").InputAsync(new ChangeEventArgs { Value = "12a4" });
        await pantalla.Find("#aceptar-clave").ClickAsync(new());

        Assert.Equal(MensajesAtencion.ClaveInvalida, pantalla.Find("#clave-invalida").TextContent.Trim());
        Assert.Equal("", pantalla.Find("#clave-del-dia").GetAttribute("value") ?? "");
        Assert.NotEmpty(pantalla.FindAll("#dialogo-clave"));
        Assert.Empty(pantalla.FindAll("[role=\"tablist\"]"));
    }

    [Fact(DisplayName = "acceso-puesto · Clave del día de desarrollo (borde de los 12 dígitos): el decimotercer dígito del teclado en pantalla no se agrega")]
    public async Task El_borde_de_los_12_digitos()
    {
        var pantalla = await AbrirDialogo();

        for (var i = 0; i < 13; i++)
        {
            await pantalla.Find("#tecla-1").ClickAsync(new());
        }

        Assert.Equal(12, (pantalla.Find("#clave-del-dia").GetAttribute("value") ?? "").Length);
        Assert.Equal(ClaveDelDiaDeDesarrollo.LargoMaximo.ToString(), pantalla.Find("#clave-del-dia").GetAttribute("maxlength"));
    }

    private async Task<IRenderedComponent<PantallaAtencion>> AbrirDialogo()
    {
        var pantalla = _puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll("#dialogo-clave"));
        return pantalla;
    }
}
