using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// «Imprimir una prueba» en la pestaña Impresora (comprobante-local de configurar-puesto-por-pestanas, D13): va directo a la
/// impresora con la leyenda PRUEBA, sin persona ni importe, no toca la base, no se confunde con el comprobante de una
/// atención y queda en la general con su resultado.
/// </summary>
public class PruebaImpresionTests
{
    private static readonly string[] TablasDeHechos =
        ["tbComedorIntencionesConsumo", "tbComedorConsumos", "tbComedorComprobantes", "tbComedorTrabajosImpresion", "tbComedorEventosSalida"];

    [Fact(DisplayName = "comprobante-local · Prueba con la impresora disponible: la impresora recibe PRUEBA, la pestaña dice que salió y la general tiene PRINTER_TESTED con el actor nulo")]
    public async Task Prueba_con_la_impresora_disponible()
    {
        using var puesto = new PuestoDePrueba();
        var impresora = new ImpresoraDePrueba();
        var pantalla = puesto.Dibujar(impresora);

        await ImprimirPrueba(pantalla);

        var recibido = Assert.Single(impresora.Recibidos);
        Assert.Equal("PRUEBA", recibido.Leyenda);
        Assert.DoesNotContain("Persona", recibido.DatosImpresion);
        Assert.Equal(MensajesAtencion.PruebaSalio, pantalla.Find("#resultado-prueba").TextContent.Trim());
        var prueba = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "PRINTER_TESTED"));
        Assert.Equal("PRINTED", prueba.GetProperty("Result").GetString());
        Assert.Equal("DAILY_KEY", prueba.GetProperty("Access").GetString());
        Assert.True(SinActor(prueba));
    }

    [Fact(DisplayName = "comprobante-local · Prueba con la impresora disponible: con el puesto guardado, la prueba lleva su línea y una frase, sin comensal ni número")]
    public async Task La_prueba_lleva_el_puesto_y_una_frase()
    {
        using var puesto = new PuestoDePrueba();
        new AlmacenDatosPuesto(puesto.Carpeta).Guardar(new DatosPuesto(3, "Comedor Norte", true, 1000));
        puesto.Contexto.Services.AddSingleton(new AlmacenDatosPuesto(puesto.Carpeta));
        var papel = Path.Combine(puesto.Carpeta, "papel");
        var pantalla = puesto.Dibujar(new ImpresoraSimulada(papel));

        await ImprimirPrueba(pantalla);

        var lineas = File.ReadAllLines(Assert.Single(Directory.GetFiles(papel)));
        Assert.Contains("Puesto 3 · Comedor Norte", lineas);
        Assert.Contains("PRUEBA", lineas);
        Assert.Contains(lineas, FrasesComprobante.Todas.Contains);
        Assert.DoesNotContain(lineas, l => l.StartsWith("Comensal:", StringComparison.Ordinal) || l.StartsWith("Número:", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "comprobante-local · Prueba sin papel: la pestaña dice que la impresora no tiene papel, y PRINTER_TESTED queda fallida con PAPER_OUT")]
    public async Task Prueba_sin_papel()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar(new ImpresoraDePrueba { Modo = ModoImpresoraDePrueba.SinPapel });

        await ImprimirPrueba(pantalla);

        Assert.Equal(MensajesAtencion.PruebaSinPapel, pantalla.Find("#resultado-prueba").TextContent.Trim());
        var prueba = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "PRINTER_TESTED"));
        Assert.Equal(("FAILED", "PAPER_OUT"), (prueba.GetProperty("Result").GetString(), prueba.GetProperty("ErrorCode").GetString()));
    }

    [Fact(DisplayName = "comprobante-local · La prueba no toca la base: intenciones, consumos, comprobantes, trabajos y eventos de salida no tienen ninguna fila nueva")]
    public async Task La_prueba_no_toca_la_base()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        await pantalla.Find("#listo").ClickAsync(new());
        var antes = Conteos(puesto);
        Assert.All(antes, c => Assert.True(c > 0));

        await ImprimirPrueba(pantalla);

        Assert.Equal(antes, Conteos(puesto));
    }

    [Fact(DisplayName = "comprobante-local · La prueba no es el comprobante de la próxima atención: con «Sin papel» después, la pantalla dice que el comprobante queda pendiente y no muestra la prueba")]
    public async Task La_prueba_no_es_el_comprobante_de_la_proxima_atencion()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar(new ImpresoraSimulada(Path.Combine(puesto.Carpeta, "comprobantes")));
        await ImprimirPrueba(pantalla);
        Assert.Equal(MensajesAtencion.PruebaSalio, pantalla.Find("#resultado-prueba").TextContent.Trim());
        await pantalla.Find("#impresora-sinpapel").ClickAsync(new());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.ComprobantePendiente, pantalla.Find("#mensaje").TextContent.Trim()));
        Assert.Empty(pantalla.FindAll(".comprobante"));
        Assert.DoesNotContain("PRUEBA", pantalla.Find("main").TextContent);
    }

    private static async Task ImprimirPrueba(IRenderedComponent<PantallaAtencion> pantalla)
    {
        if (pantalla.FindAll("[role=\"tablist\"]").Count == 0)
        {
            await EntradaConfiguracion.Entrar(pantalla);
        }

        await pantalla.Find("#pestana-impresora").ClickAsync(new());
        await pantalla.Find("#imprimir-prueba").ClickAsync(new());
    }

    private static List<long> Conteos(PuestoDePrueba puesto)
    {
        using var conexion = new SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        return TablasDeHechos.Select(tabla =>
        {
            using var comando = conexion.CreateCommand();
            comando.CommandText = $"SELECT COUNT(*) FROM {tabla}";
            return Convert.ToInt64(comando.ExecuteScalar());
        }).ToList();
    }
}
