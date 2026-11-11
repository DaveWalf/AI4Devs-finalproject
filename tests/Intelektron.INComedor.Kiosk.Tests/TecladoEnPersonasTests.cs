using AngleSharp.Dom;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El teclado en pantalla de Personas (administracion-local de editar-raciones-en-personas, D4): escribe en el campo
/// elegido, en el límite y el código sólo dígitos, y se oculta o se muestra hasta cerrar Configuración. bUnit sobre el
/// puesto de prueba, con la base de la demostración.
/// </summary>
public class TecladoEnPersonasTests
{
    private const string Persona = "Persona sintética 01";

    [Fact(DisplayName = "administracion-local · Escribir un nombre con el teclado: con el nombre de 0001 elegido, «Mayús» y las teclas de «ANA» lo dejan terminado en «ANA»")]
    public async Task Escribir_un_nombre_con_el_teclado()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Editar(pantalla);

        await Fila(pantalla).QuerySelector("input.nombre-persona")!.FocusAsync(new FocusEventArgs());
        foreach (var tecla in new[] { "MAYUS", "A", "N", "A" })
        {
            await Tecla(pantalla, tecla).ClickAsync(new MouseEventArgs());
        }

        Assert.Equal(Persona + "ANA", Fila(pantalla).QuerySelector("input.nombre-persona")!.GetAttribute("value"));
        Assert.Contains("elegido", Fila(pantalla).QuerySelector("input.nombre-persona")!.ClassList);
    }

    [Fact(DisplayName = "administracion-local · Una letra en el límite: con REFRIGERIO de 0001 elegido, borrar, «A» y «1» lo dejan en «1»")]
    public async Task Una_letra_en_el_limite()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-personas").ClickAsync(new MouseEventArgs());
        await Fila(pantalla).QuerySelector("td.cupo[data-cupo=\"REFRIGERIO\"] .cupo-tocable")!.ClickAsync(new MouseEventArgs());

        foreach (var tecla in new[] { "BORRAR", "A", "1" })
        {
            await Tecla(pantalla, tecla).ClickAsync(new MouseEventArgs());
        }

        Assert.Equal("1", Fila(pantalla).QuerySelector("td.cupo[data-cupo=\"REFRIGERIO\"] input.limite")!.GetAttribute("value"));
    }

    [Fact(DisplayName = "administracion-local · Ocultar el teclado: «Ocultar teclado» lo esconde, sigue oculto al cancelar, ir a Consumos y volver a editar, y «Mostrar teclado» lo trae")]
    public async Task Ocultar_el_teclado()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Editar(pantalla);
        Assert.NotNull(pantalla.Find(".teclado-en-pantalla"));

        await pantalla.Find("#ocultar-teclado").ClickAsync(new MouseEventArgs());
        Assert.Empty(pantalla.FindAll(".teclado-en-pantalla"));
        await pantalla.Find("#cancelar-panel").ClickAsync(new MouseEventArgs());
        await pantalla.Find("#pestana-consumos").ClickAsync(new MouseEventArgs());
        await pantalla.Find("#pestana-personas").ClickAsync(new MouseEventArgs());
        await pantalla.Find("#abrir-personas").ClickAsync(new MouseEventArgs());
        Assert.Empty(pantalla.FindAll(".teclado-en-pantalla"));
        await pantalla.Find("#mostrar-teclado").ClickAsync(new MouseEventArgs());

        Assert.NotNull(pantalla.Find(".teclado-en-pantalla"));
    }

    [Fact(DisplayName = "administracion-local · Ocultar el teclado: cerrar Configuración y volver a abrirla lo muestra otra vez")]
    public async Task Reabrir_configuracion_muestra_el_teclado()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Editar(pantalla);
        await pantalla.Find("#ocultar-teclado").ClickAsync(new MouseEventArgs());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new MouseEventArgs());

        await Editar(pantalla);

        Assert.NotNull(pantalla.Find(".teclado-en-pantalla"));
    }

    [Fact(DisplayName = "administracion-local · Un teclado en pantalla para editar Personas: el código de una persona nueva se escribe con el teclado sin aparecer en la página, y se guarda")]
    public async Task El_codigo_con_el_teclado()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.SetupVoid("incomedor.ponerValor", _ => true).SetVoidResult();
        var pantalla = puesto.Dibujar();
        await Editar(pantalla);
        await pantalla.Find("#agregar-persona").ClickAsync(new MouseEventArgs());
        var nueva = pantalla.FindAll("tr.persona").Last();
        await nueva.QuerySelector("input.nombre-persona")!.InputAsync(new ChangeEventArgs { Value = "Persona sintética 05" });

        await pantalla.FindAll("tr.persona").Last().QuerySelector("input.codigo-persona")!.FocusAsync(new FocusEventArgs());
        foreach (var tecla in new[] { "0", "1", "0", "5" })
        {
            await Tecla(pantalla, tecla).ClickAsync(new MouseEventArgs());
        }

        var llamadas = puesto.Contexto.JSInterop.Invocations["incomedor.ponerValor"];
        Assert.Equal(4, llamadas.Count);
        Assert.Equal("0105", llamadas[^1].Arguments[1]);
        Assert.DoesNotContain("0105", pantalla.Find("#panel-personas").OuterHtml);
        await pantalla.Find("#guardar-panel").ClickAsync(new MouseEventArgs());
        Assert.Contains("versión 2", pantalla.Markup);
    }

    [Fact(DisplayName = "administracion-local · Una letra en el límite: el límite no pasa de dos dígitos con el teclado")]
    public async Task El_limite_no_pasa_de_dos_digitos()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-personas").ClickAsync(new MouseEventArgs());
        await Fila(pantalla).QuerySelector("td.cupo[data-cupo=\"REFRIGERIO\"] .cupo-tocable")!.ClickAsync(new MouseEventArgs());

        foreach (var tecla in new[] { "BORRAR", "1", "2", "3" })
        {
            await Tecla(pantalla, tecla).ClickAsync(new MouseEventArgs());
        }

        Assert.Equal("12", Fila(pantalla).QuerySelector("td.cupo[data-cupo=\"REFRIGERIO\"] input.limite")!.GetAttribute("value"));
    }

    [Fact(DisplayName = "administracion-local · Escribir un nombre con el teclado: descartar una fila nueva anterior no cambia el campo elegido de la siguiente")]
    public async Task Descartar_no_cambia_el_campo_elegido()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Editar(pantalla);
        await pantalla.Find("#agregar-persona").ClickAsync(new MouseEventArgs());
        await pantalla.Find("#agregar-persona").ClickAsync(new MouseEventArgs());
        var nuevas = pantalla.FindAll("tr.persona").Where(f => f.QuerySelector("input.codigo-persona") is not null).ToList();
        await nuevas[1].QuerySelector("input.nombre-persona")!.FocusAsync(new FocusEventArgs());

        var primera = pantalla.FindAll("tr.persona").First(f => f.QuerySelector("input.codigo-persona") is not null);
        await primera.QuerySelector("button.quitar-persona")!.ClickAsync(new MouseEventArgs());
        await Tecla(pantalla, "X").ClickAsync(new MouseEventArgs());

        var queda = Assert.Single(pantalla.FindAll("tr.persona").Where(f => f.QuerySelector("input.codigo-persona") is not null));
        Assert.Equal("x", queda.QuerySelector("input.nombre-persona")!.GetAttribute("value"));
    }

    [Fact(DisplayName = "administracion-local · Un teclado en pantalla para editar Personas: no está en la tabla de lectura, sólo al editar")]
    public async Task Solo_al_editar()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-personas").ClickAsync(new MouseEventArgs());

        Assert.Empty(pantalla.FindAll(".teclado-en-pantalla"));
    }

    private static async Task Editar(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-personas").ClickAsync(new MouseEventArgs());
        await pantalla.Find("#abrir-personas").ClickAsync(new MouseEventArgs());
    }

    private static IElement Fila(IRenderedComponent<PantallaAtencion> pantalla) =>
        pantalla.FindAll("tr.persona").Single(f =>
            (f.QuerySelector("input.nombre-persona")?.GetAttribute("value") ?? f.QuerySelector(".nombre-persona")!.TextContent.Trim()).StartsWith(Persona, StringComparison.Ordinal));

    private static IElement Tecla(IRenderedComponent<PantallaAtencion> pantalla, string tecla) =>
        pantalla.Find($".teclado-en-pantalla .tecla[data-tecla=\"{tecla}\"]");
}
