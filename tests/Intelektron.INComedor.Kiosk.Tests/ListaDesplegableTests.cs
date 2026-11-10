using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components.Web;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La lista desplegable propia (presentacion-puesto de ordenar-configuracion, D3): un botón que abre, dentro de la página,
/// una lista con una fila por entrada; elegir cierra y aplica, Escape o perder el foco cierran sin elegir, y el teclado la
/// recorre. bUnit sobre el componente solo.
/// </summary>
public class ListaDesplegableTests : IDisposable
{
    private static readonly EntradaLista[] Entradas = [new("0", "Persona sintética 01"), new("1", "Persona sintética 02"), new("2", "Persona sintética 03")];

    private readonly BunitContext _contexto = new();
    private readonly List<string> _elegidos = [];

    public void Dispose() => _contexto.Dispose();

    [Fact(DisplayName = "presentacion-puesto · Listas desplegables dentro de la ventana: cerrada es un botón con el texto vacío, sin lista")]
    public void Cerrada_es_un_boton_sin_lista()
    {
        var lista = Dibujar();

        var boton = lista.Find("button#prueba");
        Assert.Equal("Todas", boton.TextContent.Trim());
        Assert.Equal(("listbox", "false"), (boton.GetAttribute("aria-haspopup"), boton.GetAttribute("aria-expanded")));
        Assert.Empty(lista.FindAll("[role=\"listbox\"]"));
        Assert.Empty(lista.FindAll("select"));
    }

    [Fact(DisplayName = "presentacion-puesto · La lista se abre en el monitor del puesto: tocar el botón la abre en la página, con una fila por entrada y la elegida marcada")]
    public async Task Tocar_abre_la_lista_en_la_pagina()
    {
        var lista = Dibujar(valor: "1");

        await lista.Find("#prueba").ClickAsync(new());

        Assert.Equal("true", lista.Find("#prueba").GetAttribute("aria-expanded"));
        var filas = lista.FindAll("[role=\"listbox\"] [role=\"option\"]");
        Assert.Equal(["Todas", "Persona sintética 01", "Persona sintética 02", "Persona sintética 03"], filas.Select(f => f.TextContent.Trim()));
        Assert.Equal(["false", "false", "true", "false"], filas.Select(f => f.GetAttribute("aria-selected")));
    }

    [Fact(DisplayName = "presentacion-puesto · Elegir en la lista: tocar una fila cierra la lista y aplica su valor")]
    public async Task Elegir_cierra_y_aplica()
    {
        var lista = Dibujar();
        await lista.Find("#prueba").ClickAsync(new());

        await lista.FindAll("[role=\"option\"]")[2].ClickAsync(new());

        Assert.Equal(["1"], _elegidos);
        Assert.Empty(lista.FindAll("[role=\"listbox\"]"));
        Assert.Equal("false", lista.Find("#prueba").GetAttribute("aria-expanded"));
    }

    [Theory(DisplayName = "presentacion-puesto · Escape o un toque afuera cierran sin elegir")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Escape_o_perder_el_foco_cierran_sin_elegir(bool conEscape)
    {
        var lista = Dibujar();
        await lista.Find("#prueba").ClickAsync(new());

        if (conEscape)
        {
            await lista.Find("[role=\"listbox\"]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        }
        else
        {
            await lista.Find("[role=\"listbox\"]").FocusOutAsync(new FocusEventArgs());
        }

        Assert.Empty(lista.FindAll("[role=\"listbox\"]"));
        Assert.Empty(_elegidos);
    }

    [Fact(DisplayName = "presentacion-puesto · Con el teclado: Enter abre, dos flechas abajo y Enter eligen la segunda persona")]
    public async Task Con_el_teclado()
    {
        var lista = Dibujar();

        await lista.Find("#prueba").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        var listbox = lista.Find("[role=\"listbox\"]");
        Assert.Equal("prueba-opcion-0", listbox.GetAttribute("aria-activedescendant"));
        await listbox.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await lista.Find("[role=\"listbox\"]").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("prueba-opcion-2", lista.Find("[role=\"listbox\"]").GetAttribute("aria-activedescendant"));
        await lista.Find("[role=\"listbox\"]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["1"], _elegidos);
        Assert.Empty(lista.FindAll("[role=\"listbox\"]"));
    }

    [Fact(DisplayName = "presentacion-puesto · Con el teclado: Fin va a la última fila e Inicio a la primera, y la flecha arriba no pasa de la primera")]
    public async Task Inicio_y_fin()
    {
        var lista = Dibujar();
        await lista.Find("#prueba").ClickAsync(new());

        await lista.Find("[role=\"listbox\"]").KeyDownAsync(new KeyboardEventArgs { Key = "End" });
        Assert.Equal("prueba-opcion-3", lista.Find("[role=\"listbox\"]").GetAttribute("aria-activedescendant"));
        await lista.Find("[role=\"listbox\"]").KeyDownAsync(new KeyboardEventArgs { Key = "Home" });
        await lista.Find("[role=\"listbox\"]").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
        Assert.Equal("prueba-opcion-0", lista.Find("[role=\"listbox\"]").GetAttribute("aria-activedescendant"));
    }

    [Fact(DisplayName = "presentacion-puesto · Listas desplegables: sin la fila vacía, la lista tiene sólo las entradas y el botón muestra la elegida")]
    public async Task Sin_fila_vacia()
    {
        var lista = Dibujar(valor: "2", opcionVacia: false, textoVacio: "Elegí a una persona…");

        Assert.Equal("Persona sintética 03", lista.Find("#prueba").TextContent.Trim());
        await lista.Find("#prueba").ClickAsync(new());

        Assert.Equal(3, lista.FindAll("[role=\"option\"]").Count);
    }

    private IRenderedComponent<ListaDesplegable> Dibujar(string valor = "", bool opcionVacia = true, string textoVacio = "Todas") =>
        _contexto.Render<ListaDesplegable>(p => p
            .Add(x => x.Id, "prueba")
            .Add(x => x.Entradas, Entradas)
            .Add(x => x.Valor, valor)
            .Add(x => x.TextoVacio, textoVacio)
            .Add(x => x.OpcionVacia, opcionVacia)
            .Add(x => x.ValorChanged, (string v) => _elegidos.Add(v)));
}
