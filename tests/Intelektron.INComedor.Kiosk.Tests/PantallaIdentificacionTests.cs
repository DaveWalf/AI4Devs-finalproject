using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La pantalla de identificación que invita a comer (presentacion-puesto de la ampliación del 10/11 de
/// simular-identificacion-por-lista, D12 y D13): el título, el combo arriba del campo, y el campo e «Ingresar» juntos en su
/// fila, con y sin simulación. Las medidas en píxeles las toma la E2E en el WebView2; acá se mira el orden del DOM.
/// </summary>
public class PantallaIdentificacionTests
{
    [Fact(DisplayName = "presentacion-puesto · El título invita a disfrutar la comida: en reposo el título es «¡Hola! Identificate y disfrutá tu comida» y la pantalla no contiene «Identificate para comenzar»")]
    public void El_titulo_invita_a_disfrutar_la_comida()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        Assert.Equal("¡Hola! Identificate y disfrutá tu comida", pantalla.Find(".atencion h2").TextContent.Trim());
        Assert.DoesNotContain("Identificate para comenzar", pantalla.Find("main").TextContent);
    }

    [Fact(DisplayName = "presentacion-puesto · El combo arriba y «Ingresar» a la derecha del campo (DOM): con la simulación activa, el combo va antes del campo, y el campo e «Ingresar» van juntos en .ingreso")]
    public void Con_simulacion_el_combo_arriba_y_la_fila_del_codigo()
    {
        using var puesto = Puesto(simulacion: true);
        var pantalla = puesto.Dibujar();

        var orden = pantalla.FindAll(".atencion #simulacion-persona, .atencion #codigo, .atencion #ingresar").Select(e => e.Id);
        Assert.Equal(["simulacion-persona", "codigo", "ingresar"], orden);
        AssertFilaDelCodigo(pantalla);
    }

    [Fact(DisplayName = "presentacion-puesto · Sin simulación, el campo e «Ingresar» siguen en una fila (DOM): con la simulación apagada no hay combo, y el campo e «Ingresar» van juntos en .ingreso")]
    public void Sin_simulacion_la_fila_del_codigo()
    {
        using var puesto = Puesto(simulacion: false);
        var pantalla = puesto.Dibujar();

        Assert.Empty(pantalla.FindAll("#simulacion-persona"));
        AssertFilaDelCodigo(pantalla);
    }

    /// <summary>El campo y después «Ingresar», hijos de la misma fila .ingreso, y ningún div.acciones de reposo.</summary>
    private static void AssertFilaDelCodigo(IRenderedComponent<PantallaAtencion> pantalla)
    {
        var fila = pantalla.Find(".atencion .ingreso");
        Assert.Equal(["codigo", "ingresar"], fila.Children.Select(e => e.Id));
        Assert.Empty(pantalla.FindAll(".atencion .acciones"));
    }

    /// <summary>El puesto de bUnit con el almacén de la simulación en su carpeta y el ajuste ya guardado.</summary>
    private static PuestoDePrueba Puesto(bool simulacion)
    {
        var puesto = new PuestoDePrueba();
        new AlmacenSimulacion(puesto.Carpeta).Guardar(simulacion);
        puesto.Contexto.Services.AddSingleton(new AlmacenSimulacion(puesto.Carpeta));
        return puesto;
    }
}
