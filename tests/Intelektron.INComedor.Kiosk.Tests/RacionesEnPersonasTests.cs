using AngleSharp.Dom;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Las raciones en x/n, tocar un cupo para editarlo y exportar Personas (administracion-local de
/// editar-raciones-en-personas, D1, D2 y D5). bUnit sobre el puesto de prueba, con la base de la demostración: 0001 tiene
/// ALMUERZO 1 y REFRIGERIO 2.
/// </summary>
public class RacionesEnPersonasTests
{
    private const string Persona = "Persona sintética 01";

    [Fact(DisplayName = "administracion-local · La persona del código 0001: sin consumos en el día, su fila dice «0/1» en ALMUERZO y «0/2» en REFRIGERIO")]
    public async Task La_fila_de_0001_en_x_de_n()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        await AbrirPersonas(pantalla);

        var fila = Fila(pantalla, Persona);
        Assert.Equal("0/1", Celda(fila, "ALMUERZO"));
        Assert.Equal("0/2", Celda(fila, "REFRIGERIO"));
    }

    [Fact(DisplayName = "administracion-local · Lo usado hoy en cada cupo: después de un almuerzo de 0001, su fila dice «1/1» en ALMUERZO")]
    public async Task Lo_usado_hoy_en_cada_cupo()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla);

        await AbrirPersonas(pantalla);

        Assert.Equal("1/1", Celda(Fila(pantalla, Persona), "ALMUERZO"));
    }

    [Fact(DisplayName = "administracion-local · Tocar un cupo lo edita: tocar la celda REFRIGERIO de 0001 abre la edición con ese límite elegido, y guardarlo crea la versión 2")]
    public async Task Tocar_un_cupo_lo_edita()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);

        await Fila(pantalla, Persona).QuerySelector("td.cupo[data-cupo=\"REFRIGERIO\"] .cupo-tocable")!.ClickAsync(new());

        Assert.NotNull(pantalla.Find("#panel-personas"));
        var elegido = Assert.Single(pantalla.FindAll("input.limite.elegido"));
        Assert.Equal("REFRIGERIO", elegido.Closest("td.cupo")!.GetAttribute("data-cupo"));
        Assert.Equal(Persona, elegido.Closest("tr.persona")!.QuerySelector("input.nombre-persona")!.GetAttribute("value"));
        await elegido.InputAsync(new ChangeEventArgs { Value = "1" });
        await pantalla.Find("#guardar-panel").ClickAsync(new());
        Assert.Contains("versión 2", pantalla.Markup);
        Assert.Equal("0/1", Celda(Fila(pantalla, Persona), "REFRIGERIO"));
    }

    [Fact(DisplayName = "administracion-local · Exportar Personas a CSV: con la carpeta elegida, «Exportar CSV» deja un .csv con una fila por persona y la general tiene PEOPLE_EXPORTED sin nombres")]
    public async Task Exportar_personas_a_csv()
    {
        using var puesto = new PuestoDePrueba();
        var destino = Preparar(puesto);
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);
        var personas = pantalla.FindAll("#tabla-personas tr.persona").Count;

        await pantalla.Find("#exportar-personas-csv").ClickAsync(new());

        var archivo = Assert.Single(Directory.GetFiles(destino));
        Assert.EndsWith(".csv", archivo, StringComparison.Ordinal);
        Assert.Equal(personas + 1, File.ReadAllLines(archivo).Length);
        Assert.Equal(MensajesAtencion.ExportacionPersonasHecha(Path.GetFileName(archivo), personas, destino),
            pantalla.Find("#mensaje-exportacion-personas").TextContent.Trim());
        var linea = puesto.General().Single(l => Tipo(l) == "PEOPLE_EXPORTED");
        Assert.Equal("CSV", ContextoDe(linea).GetProperty("Format").GetString());
        Assert.Equal(personas, ContextoDe(linea).GetProperty("Rows").GetInt32());
        Assert.DoesNotContain("Persona sintética", linea.GetRawText(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "administracion-local · Exportar Personas a Excel: «Exportar Excel» deja un .xlsx en la carpeta elegida")]
    public async Task Exportar_personas_a_excel()
    {
        using var puesto = new PuestoDePrueba();
        var destino = Preparar(puesto);
        var pantalla = puesto.Dibujar();
        await AbrirPersonas(pantalla);

        await pantalla.Find("#exportar-personas-xlsx").ClickAsync(new());

        Assert.EndsWith(".xlsx", Assert.Single(Directory.GetFiles(destino)), StringComparison.Ordinal);
        Assert.Equal("XLSX", ContextoDe(puesto.General().Single(l => Tipo(l) == "PEOPLE_EXPORTED")).GetProperty("Format").GetString());
    }

    private static async Task AbrirPersonas(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-personas").ClickAsync(new());
    }

    private static IElement Fila(IRenderedComponent<PantallaAtencion> pantalla, string nombre) =>
        pantalla.FindAll("tr.persona").Single(f =>
            (f.QuerySelector("input.nombre-persona")?.GetAttribute("value") ?? f.QuerySelector(".nombre-persona")!.TextContent.Trim()) == nombre);

    private static string Celda(IElement fila, string cupo) => fila.QuerySelector($"td.cupo[data-cupo=\"{cupo}\"]")!.TextContent.Trim();

    /// <summary>Un almuerzo de 0001.</summary>
    private static async Task Consumir(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion").First(o => o.TextContent.Contains("Almuerzo", StringComparison.Ordinal)).ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        await pantalla.Find("#listo").ClickAsync(new());
    }

    /// <summary>La carpeta de exportación ya elegida y un elector que no se usa, registrados antes de dibujar.</summary>
    private static string Preparar(PuestoDePrueba puesto)
    {
        var destino = Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "exportaciones")).FullName;
        var almacen = new AlmacenExportacion(puesto.Carpeta);
        almacen.Guardar(destino);
        puesto.Contexto.Services.AddSingleton(almacen);
        puesto.Contexto.Services.AddSingleton<IElectorCarpeta>(new ElectorQueNoSeUsa());
        return destino;
    }

    private sealed class ElectorQueNoSeUsa : IElectorCarpeta
    {
        public string? Elegir(string? actual) => throw new InvalidOperationException("La carpeta ya estaba elegida.");
    }
}
