using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La pestaña Consumos (consulta-consumos de configurar-puesto-por-pestanas, D9 y la decisión de David del 06/11): la ve
/// cualquiera que entre a Configuración, sin el código de un operador; Comprobantes sigue pidiéndolo; cada consulta queda en
/// la general con el actor nulo, NO_AUTHENTICATION y cómo se entró, sin nombres ni códigos.
/// </summary>
public class PestanaConsumosTests
{
    private static readonly string[] Codigos = ["0001", "0002", "0003", "0004", "9001", "9002", "9003", "9004"];

    [Fact(DisplayName = "consulta-consumos · En mantenimiento, sin ningún código: la pestaña Consumos muestra los consumos de hoy de 0001")]
    public async Task En_mantenimiento_sin_ningun_codigo()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);
        await Consumir(pantalla, "0001", 0, refrigerio: true);

        await AbrirConsumos(pantalla);

        Assert.Empty(pantalla.FindAll("#seccion-consumos input[type=\"password\"]"));
        var filas = pantalla.FindAll("#seccion-consumos tr.consumo");
        Assert.Equal(2, filas.Count);
        Assert.All(filas, f => Assert.Contains("Persona sintética 01", f.TextContent));
        Assert.Equal("descending", pantalla.Find("#seccion-consumos th[data-columna=\"Hora\"]").GetAttribute("aria-sort"));
    }

    [Fact(DisplayName = "consulta-consumos · En operativo, con la clave del día: después de entrar con 4711, la pestaña muestra los consumos de hoy sin pedir otro código")]
    public async Task En_operativo_con_la_clave_del_dia()
    {
        using var puesto = new PuestoDePrueba(ModoPuesto.Operativo);
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#clave-del-dia").InputAsync(new ChangeEventArgs { Value = "4711" });
        await pantalla.Find("#aceptar-clave").ClickAsync(new());

        await pantalla.Find("#pestana-consumos").ClickAsync(new());

        Assert.Single(pantalla.FindAll("#seccion-consumos tr.consumo"));
        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        var consulta = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONSUMPTIONS_QUERIED"));
        Assert.Equal("DAILY_KEY", consulta.GetProperty("Access").GetString());
    }

    [Fact(DisplayName = "consulta-consumos · Comprobantes sigue pidiendo un operador: con la tabla de Consumos a la vista, pasar a Comprobantes pide el código y no muestra comprobantes")]
    public async Task Comprobantes_sigue_pidiendo_un_operador()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);
        await AbrirConsumos(pantalla);
        Assert.NotEmpty(pantalla.FindAll("#seccion-consumos tr.consumo"));

        await pantalla.Find("#pestana-comprobantes").ClickAsync(new());

        Assert.NotNull(pantalla.Find("#codigo-operador"));
        Assert.Empty(pantalla.FindAll("#seccion-comprobantes .reimprimir"));
    }

    [Fact(DisplayName = "consulta-consumos · La consulta en la bitácora: filtrar por REFRIGERIO con dos refrigerios deja CONSUMPTIONS_QUERIED con el actor nulo, NO_AUTHENTICATION, DAILY_KEY, el servicio filtrado y 2 filas, sin nombres ni códigos")]
    public async Task La_consulta_en_la_bitacora()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);
        await Consumir(pantalla, "0001", 0, refrigerio: true);
        await Consumir(pantalla, "0001", 0, refrigerio: true);
        await AbrirConsumos(pantalla);
        var servicio = pantalla.FindAll("#consumos-servicio option").Single(o => o.TextContent.Trim() == "REFRIGERIO").GetAttribute("value");

        await pantalla.Find("#consumos-servicio").ChangeAsync(new ChangeEventArgs { Value = servicio });
        await pantalla.Find("#consultar-consumos").ClickAsync(new());

        Assert.Equal(2, pantalla.FindAll("#seccion-consumos tr.consumo").Count);
        var linea = Assert.Single(puesto.General(), l => Tipo(l) == "CONSUMPTIONS_QUERIED" && ContextoDe(l).GetProperty("ServiceId").ValueKind != System.Text.Json.JsonValueKind.Null);
        var contexto = ContextoDe(linea);
        Assert.True(SinActor(contexto));
        Assert.Equal("DAILY_KEY", contexto.GetProperty("Access").GetString());
        Assert.Equal(2, contexto.GetProperty("Rows").GetInt32());
        var crudo = linea.GetRawText();
        Assert.DoesNotContain("Persona sintética", crudo);
        Assert.DoesNotContain("REFRIGERIO", crudo);
    }

    [Fact(DisplayName = "consulta-consumos · Ningún código en la tabla: ni el texto ni los atributos de la pestaña Consumos contienen los códigos sintéticos")]
    public async Task Ningun_codigo_en_la_tabla()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);
        await Consumir(pantalla, "0001", 0, refrigerio: true);

        await AbrirConsumos(pantalla);

        var pestana = pantalla.Find("#seccion-consumos");
        Assert.NotEmpty(pestana.QuerySelectorAll("tr.consumo"));
        foreach (var codigo in Codigos)
        {
            Assert.DoesNotContain(codigo, pestana.TextContent);
        }

        Assert.All(pestana.QuerySelectorAll("*").SelectMany(e => e.Attributes), a => Assert.DoesNotContain(Codigos, c => a.Value.Contains(c, StringComparison.Ordinal)));
    }

    [Fact(DisplayName = "consulta-consumos · Ordenar por persona: tocar su encabezado lo marca ascendente con aria-sort, y otro toque, descendente")]
    public async Task Ordenar_por_persona()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);
        await AbrirConsumos(pantalla);

        await pantalla.Find("#seccion-consumos th[data-columna=\"Persona\"] button.ordenar").ClickAsync(new());
        Assert.Equal("ascending", pantalla.Find("#seccion-consumos th[data-columna=\"Persona\"]").GetAttribute("aria-sort"));
        Assert.Equal("none", pantalla.Find("#seccion-consumos th[data-columna=\"Hora\"]").GetAttribute("aria-sort"));
        await pantalla.Find("#seccion-consumos th[data-columna=\"Persona\"] button.ordenar").ClickAsync(new());

        Assert.Equal("descending", pantalla.Find("#seccion-consumos th[data-columna=\"Persona\"]").GetAttribute("aria-sort"));
    }

    [Fact(DisplayName = "consulta-consumos · Un rango invertido: la pestaña dice que el inicio no puede ser posterior al fin y no cambia la tabla")]
    public async Task Un_rango_invertido()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla, "0001", 0);
        await AbrirConsumos(pantalla);

        await pantalla.Find("#consumos-desde").ChangeAsync(new ChangeEventArgs { Value = "2026-11-06" });
        await pantalla.Find("#consumos-hasta").ChangeAsync(new ChangeEventArgs { Value = "2026-11-01" });
        await pantalla.Find("#consultar-consumos").ClickAsync(new());

        Assert.Equal(MensajesAtencion.RangoInvertido, pantalla.Find("#aviso-consumos").TextContent.Trim());
        Assert.Single(pantalla.FindAll("#seccion-consumos tr.consumo"));
    }

    private static async Task AbrirConsumos(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-consumos").ClickAsync(new());
    }

    /// <summary>Una atención completa en reposo: el código, la opción por su posición entre las de su servicio y «Listo».</summary>
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
