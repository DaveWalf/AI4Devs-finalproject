using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La distribución por cantidad en la pantalla (D1, D2 y D9 de presentar-atencion-tactil), con bUnit sobre un
/// <see cref="ServicioAtencion"/> real, la semilla de nueve y una base temporal. Cada toque se espera con su versión
/// asíncrona, como en <see cref="PantallaAtencionTests"/>.
/// </summary>
public class PantallaDistribucionTests : IDisposable
{
    private static readonly string[] OrdenNueve =
    [
        "Almuerzo con gaseosa", "Almuerzo de demostración", "Almuerzo opcional", "Almuerzo opcional con gaseosa",
        "Gaseosa solamente", "Merienda", "Refrigerio", "Refrigerio opcional", "Yogurt",
    ];

    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
    }

    [Theory(DisplayName = "atencion-puesto · Ocho en una columna, nueve y diez en dos, y sin «Anterior» ni «Siguiente» hasta diez")]
    [InlineData(8, "1")]
    [InlineData(9, "2")]
    [InlineData(10, "2")]
    public async Task Columnas_segun_la_cantidad(int cantidad, string columnas)
    {
        var pantalla = Dibujar(cantidad);
        await Ingresar(pantalla);

        Assert.Equal(cantidad, pantalla.FindAll(".opcion").Count);
        Assert.Equal(columnas, pantalla.Find(".opciones").GetAttribute("data-columnas"));
        Assert.Empty(pantalla.FindAll("#anterior"));
        Assert.Empty(pantalla.FindAll("#siguiente"));
    }

    [Fact(DisplayName = "atencion-puesto · Nueve opciones juntas: dos columnas de cinco filas, la primera con las cinco primeras del catálogo, sin relleno")]
    public async Task Nueve_con_las_cinco_primeras_en_la_primera_columna()
    {
        var pantalla = Dibujar(9);
        await Ingresar(pantalla);

        var lista = pantalla.Find(".opciones");
        Assert.Equal("5", lista.GetAttribute("data-filas"));
        var nombres = pantalla.FindAll(".opcion .nombre").Select(n => n.TextContent.Trim()).ToList();
        Assert.Equal(OrdenNueve, nombres);
        Assert.Equal(9, lista.Children.Length);
    }

    [Fact(DisplayName = "presentacion-puesto · La opción tocada dice «Elegida», y al tocar otra la marca se mueve sin escribir ninguna intención")]
    public async Task La_marca_elegida_se_mueve()
    {
        var pantalla = Dibujar(9);
        await Ingresar(pantalla);

        await Opcion(pantalla, "Almuerzo opcional").ClickAsync(new());

        Assert.Equal("true", Opcion(pantalla, "Almuerzo opcional").GetAttribute("aria-pressed"));
        Assert.Contains("Elegida", Opcion(pantalla, "Almuerzo opcional").TextContent);
        Assert.Single(pantalla.FindAll(".opcion"), o => o.TextContent.Contains("Elegida"));

        await Opcion(pantalla, "Merienda").ClickAsync(new());

        var elegidas = pantalla.FindAll(".opcion").Where(o => o.TextContent.Contains("Elegida")).ToList();
        Assert.Single(elegidas);
        Assert.Contains("Merienda", elegidas[0].TextContent);
        Assert.Equal("false", Opcion(pantalla, "Almuerzo opcional").GetAttribute("aria-pressed"));
        Assert.Equal(0L, Intenciones());
    }

    [Fact(DisplayName = "atencion-puesto · Opción sin cargo: ni la opción ni la elección muestran importe, y «Confirmar» queda habilitado")]
    public async Task Opcion_sin_cargo_no_muestra_importe()
    {
        var pantalla = Dibujar(9);
        await Ingresar(pantalla);

        Assert.Empty(Opcion(pantalla, "Merienda").QuerySelectorAll(".cargo"));
        Assert.DoesNotContain("ARS", Opcion(pantalla, "Merienda").TextContent);
        Assert.Contains("ARS 1.800,00", Opcion(pantalla, "Almuerzo opcional").TextContent);

        await Opcion(pantalla, "Merienda").ClickAsync(new());

        Assert.Contains("Merienda", pantalla.Find("#eleccion").TextContent);
        Assert.DoesNotContain("ARS", pantalla.Find("#eleccion").TextContent);
        Assert.DoesNotContain("Cargo", pantalla.Find("#eleccion").TextContent);
        Assert.False(pantalla.Find("#confirmar").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "TK02.1 · Con nueve opciones, cada opción, «Cancelar» y «Confirmar» son botones sin tabindex negativo")]
    public async Task Con_nueve_los_controles_son_botones()
    {
        var pantalla = Dibujar(9);
        await Ingresar(pantalla);
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());

        var controles = pantalla.FindAll(".opcion").Concat(new[] { "#cancelar", "#confirmar" }.Select(pantalla.Find)).ToList();

        Assert.Equal(9 + 2, controles.Count);
        Assert.All(controles, c =>
        {
            Assert.Equal("BUTTON", c.TagName);
            Assert.Equal("button", c.GetAttribute("type"));
            Assert.NotEqual("-1", c.GetAttribute("tabindex"));
        });
    }

    private IRenderedComponent<PantallaAtencion> Dibujar(int cantidad)
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: true);
        Assert.Equal(Demostracion.EstadoPreparacion.Lista, servicio.Preparar());
        if (cantidad == 8)
        {
            CatalogoDePrueba.Quitar(_archivo.Ruta, CatalogoDePrueba.Yogurt);
        }
        else if (cantidad == 10)
        {
            CatalogoDePrueba.Agregar(_archivo.Ruta);
        }

        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        return _contexto.Render<PantallaAtencion>();
    }

    private static AngleSharp.Dom.IElement Opcion(IRenderedComponent<PantallaAtencion> pantalla, string nombre) =>
        pantalla.FindAll(".opcion").Single(o => o.QuerySelector(".nombre")!.TextContent.Trim() == nombre);

    private static async Task Ingresar(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll(".opcion"));
    }

    private long Intenciones()
    {
        using var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM tbComedorIntencionesConsumo";
        return Convert.ToInt64(comando.ExecuteScalar());
    }
}
