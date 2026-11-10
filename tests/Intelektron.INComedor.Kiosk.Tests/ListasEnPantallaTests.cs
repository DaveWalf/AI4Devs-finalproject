using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Las listas propias en sus pantallas (presentacion-puesto de ordenar-configuracion, D3): el combo de la simulación y los
/// filtros de Consumos son listas dibujadas en la página, ninguna pantalla tiene un select nativo, y elegir en la lista del
/// combo identifica como antes. bUnit sobre el puesto de prueba.
/// </summary>
public class ListasEnPantallaTests
{
    [Fact(DisplayName = "presentacion-puesto · Ningún select nativo: la pantalla de identificación con la simulación y la pestaña Consumos no tienen select, y sus listas son botones que abren un listbox")]
    public async Task Ningun_select_nativo()
    {
        using var puesto = Puesto();
        var pantalla = puesto.Dibujar();

        Assert.Empty(pantalla.FindAll("main select"));
        Assert.Equal("listbox", pantalla.Find("button#simulacion-persona").GetAttribute("aria-haspopup"));

        await EntradaConfiguracion.Entrar(pantalla);

        var consumos = pantalla.Find("#seccion-consumos");
        Assert.Empty(consumos.QuerySelectorAll("select"));
        Assert.Equal(
            ["listbox", "listbox", "listbox"],
            new[] { "#consumos-persona", "#consumos-servicio", "#consumos-opcion" }.Select(id => consumos.QuerySelector("button" + id)?.GetAttribute("aria-haspopup")));
    }

    [Fact(DisplayName = "presentacion-puesto · Elegir en la lista identifica a la persona: tocar el combo y la fila de «Persona sintética 01» cierra la lista y muestra su nombre y el catálogo, sin «Ingresar»")]
    public async Task Elegir_en_la_lista_identifica_a_la_persona()
    {
        using var puesto = Puesto();
        var pantalla = puesto.Dibujar();

        await pantalla.Find("#simulacion-persona").ClickAsync(new());
        var fila = pantalla.FindAll("#simulacion-persona-lista [role=\"option\"]").Single(f => f.TextContent.Trim() == "Persona sintética 01");
        await fila.ClickAsync(new());

        Assert.Empty(pantalla.FindAll("[role=\"listbox\"]"));
        Assert.Contains("Persona sintética 01", pantalla.Find("#saludo").TextContent);
        Assert.NotEmpty(pantalla.FindAll(".opcion"));
    }

    [Fact(DisplayName = "presentacion-puesto · Escape o un toque afuera cierran sin elegir: Escape en la lista del combo la cierra, sigue en reposo, sin identificación nueva y sin cerrar el puesto")]
    public async Task Escape_en_la_lista_del_combo_no_identifica_ni_cierra()
    {
        using var puesto = Puesto();
        var pantalla = puesto.Dibujar();

        await pantalla.Find("#simulacion-persona").ClickAsync(new());
        await pantalla.Find("#simulacion-persona-lista").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(pantalla.FindAll("[role=\"listbox\"]"));
        Assert.NotEmpty(pantalla.FindAll("#codigo"));
        Assert.Empty(pantalla.FindAll("#saludo"));
        Assert.Empty(pantalla.FindAll("[role=\"dialog\"]"));
        Assert.Equal(0, puesto.Cierres);
    }

    [Fact(DisplayName = "presentacion-puesto · Escape o un toque afuera cierran sin elegir: Escape en la lista del filtro Persona de Consumos la cierra y Configuración sigue abierta")]
    public async Task Escape_en_un_filtro_no_cierra_configuracion()
    {
        using var puesto = Puesto();
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#consumos-persona").ClickAsync(new());
        await pantalla.Find("#consumos-persona-lista").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(pantalla.FindAll("[role=\"listbox\"]"));
        Assert.NotEmpty(pantalla.FindAll("[role=\"dialog\"]"));
        Assert.Equal("Todas", pantalla.Find("#consumos-persona").TextContent.Trim());
    }

    private static PuestoDePrueba Puesto()
    {
        var puesto = new PuestoDePrueba();
        new AlmacenSimulacion(puesto.Carpeta).Guardar(true);
        puesto.Contexto.Services.AddSingleton(new AlmacenSimulacion(puesto.Carpeta));
        return puesto;
    }
}
