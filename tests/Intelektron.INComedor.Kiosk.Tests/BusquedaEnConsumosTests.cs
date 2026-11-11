using System.Text.RegularExpressions;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La pestaña Consumos con las últimas 24 horas, la búsqueda por texto y por código, y el número de comprobante
/// (consulta-consumos de buscar-y-exportar-consumos, D1 a D3). bUnit sobre el puesto de prueba, con el reloj fijo.
/// </summary>
public class BusquedaEnConsumosTests
{
    [Fact(DisplayName = "consulta-consumos · Las últimas 24 horas por defecto: la tabla abre con el consumo recién hecho, la hora con su fecha, la columna N.º con el número de comprobante y «Últimas 24 h»")]
    public async Task Abre_con_las_ultimas_24_horas_y_el_numero()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);

        await EntradaConfiguracion.Entrar(pantalla);

        var fila = Assert.Single(pantalla.FindAll("#seccion-consumos tr.consumo"));
        Assert.Contains("N.º", pantalla.Find("#seccion-consumos thead").TextContent);
        Assert.Matches(new Regex(@"^\d{2}/\d{2} \d{2}:\d{2}$"), fila.QuerySelector("td.hora")!.TextContent.Trim());
        Assert.Matches(new Regex("^[0-9a-f]{8}$"), fila.QuerySelector("td.numero")!.TextContent.Trim());
        Assert.NotNull(pantalla.Find("#ultimas-24h"));
    }

    [Fact(DisplayName = "consulta-consumos · Buscar por texto: «REFRÍGERIO», con mayúsculas y un acento de más, deja sólo el refrigerio")]
    public async Task Buscar_por_texto()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);
        await Consumir(pantalla, "0001", 0, refrigerio: true);
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#consumos-texto").InputAsync(new ChangeEventArgs { Value = "REFRÍGERIO" });
        await pantalla.Find("#consultar-consumos").ClickAsync(new());

        var fila = Assert.Single(pantalla.FindAll("#seccion-consumos tr.consumo"));
        Assert.Contains("REFRIGERIO", fila.TextContent);
    }

    [Fact(DisplayName = "consulta-consumos · El código de una persona: con 0001 filtra por su persona, el campo queda vacío, la pestaña no lo contiene y la bitácora sólo dice que se usó")]
    public async Task El_codigo_no_queda_a_la_vista()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#consumos-codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#consultar-consumos").ClickAsync(new());

        Assert.Single(pantalla.FindAll("#seccion-consumos tr.consumo"));
        Assert.Equal("", pantalla.Find("#consumos-codigo").GetAttribute("value") ?? "");
        Assert.DoesNotContain("0001", pantalla.Find("#seccion-consumos").OuterHtml);
        var linea = puesto.General().Last(l => Tipo(l) == "CONSUMPTIONS_QUERIED");
        var contexto = ContextoDe(linea);
        Assert.True(contexto.GetProperty("PersonCode").GetBoolean());
        Assert.False(contexto.GetProperty("TextSearched").GetBoolean());
        Assert.DoesNotContain("\"0001\"", linea.GetRawText());
    }

    [Fact(DisplayName = "consulta-consumos · Un código que no es de nadie: avisa sin consultar, la tabla no cambia y ni la pestaña ni la bitácora contienen el código")]
    public async Task Un_codigo_que_no_es_de_nadie()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);
        await EntradaConfiguracion.Entrar(pantalla);
        var consultas = puesto.General().Count(l => Tipo(l) == "CONSUMPTIONS_QUERIED");

        await pantalla.Find("#consumos-codigo").InputAsync(new ChangeEventArgs { Value = "7777" });
        await pantalla.Find("#consultar-consumos").ClickAsync(new());

        Assert.Equal(MensajesAtencion.CodigoDeNadie, pantalla.Find("#aviso-consumos").TextContent.Trim());
        Assert.Single(pantalla.FindAll("#seccion-consumos tr.consumo"));
        Assert.DoesNotContain("7777", pantalla.Find("#seccion-consumos").OuterHtml);
        Assert.Equal(consultas, puesto.General().Count(l => Tipo(l) == "CONSUMPTIONS_QUERIED"));
        Assert.DoesNotContain(puesto.General(), l => l.GetRawText().Contains("\"7777\"", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "consulta-consumos · Volver a las últimas 24 horas: después de una semana sin consumos, «Últimas 24 h» vuelve a mostrar el de recién")]
    public async Task Volver_a_las_ultimas_24_horas()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#consumos-desde").ChangeAsync(new ChangeEventArgs { Value = "2026-10-01" });
        await pantalla.Find("#consumos-hasta").ChangeAsync(new ChangeEventArgs { Value = "2026-10-07" });
        await pantalla.Find("#consultar-consumos").ClickAsync(new());
        Assert.Empty(pantalla.FindAll("#seccion-consumos tr.consumo"));

        await pantalla.Find("#ultimas-24h").ClickAsync(new());

        Assert.Single(pantalla.FindAll("#seccion-consumos tr.consumo"));
    }

    [Fact(DisplayName = "consulta-consumos · El texto y el código en la bitácora: con el texto «almuerzo», CONSUMPTIONS_QUERIED dice que se buscó un texto y no lo contiene")]
    public async Task El_texto_en_la_bitacora()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#consumos-texto").InputAsync(new ChangeEventArgs { Value = "almuerzo" });
        await pantalla.Find("#consultar-consumos").ClickAsync(new());

        var linea = puesto.General().Last(l => Tipo(l) == "CONSUMPTIONS_QUERIED");
        Assert.True(ContextoDe(linea).GetProperty("TextSearched").GetBoolean());
        Assert.DoesNotContain("almuerzo", linea.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Una atención completa en reposo: el código, la opción por su posición entre las de su servicio y «Confirmar».</summary>
    [Fact(DisplayName = "consulta-consumos · El filtro por código sigue a la vista: después de consultar con un código, la pestaña lo dice sin mostrarlo, también al ordenar, y una consulta sin código lo quita")]
    public async Task El_filtro_por_codigo_sigue_a_la_vista()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);
        await EntradaConfiguracion.Entrar(pantalla);
        Assert.Empty(pantalla.FindAll("#filtro-codigo"));

        await pantalla.Find("#consumos-codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#consultar-consumos").ClickAsync(new());
        Assert.Equal(MensajesAtencion.FiltradoPorCodigo, pantalla.Find("#filtro-codigo").TextContent.Trim());
        await pantalla.Find("th[data-columna=\"Persona\"] .ordenar").ClickAsync(new());
        Assert.Equal(MensajesAtencion.FiltradoPorCodigo, pantalla.Find("#filtro-codigo").TextContent.Trim());
        Assert.DoesNotContain("0001", pantalla.Find("#seccion-consumos").OuterHtml);
        await pantalla.Find("#consultar-consumos").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#filtro-codigo"));
    }

    private static async Task Consumir(IRenderedComponent<PantallaAtencion> pantalla, string codigo, int posicion, bool refrigerio = false)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = codigo });
        await pantalla.Find("#ingresar").ClickAsync(new());
        var opciones = pantalla.FindAll(".opcion").Where(o => refrigerio != o.TextContent.Contains("Almuerzo", StringComparison.Ordinal)).ToList();
        await opciones[posicion].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        await pantalla.Find("#listo").ClickAsync(new());
    }
}
