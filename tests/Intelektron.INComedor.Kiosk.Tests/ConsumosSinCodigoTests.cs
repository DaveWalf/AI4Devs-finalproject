using System.Text.Json;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Consumos sin pedido de código (comprobante-local de reimprimir-sin-codigo, D1, D2, D3, D5 y D7): con la clave del día,
/// «Reimprimir» y «Recuperar» no piden un código, la copia sale marcada SIN VALIDEZ y la pestaña lo avisa antes del motivo.
/// bUnit sobre el puesto de prueba, con la impresora simulada donde el papel importa.
/// </summary>
public class ConsumosSinCodigoTests
{
    private const string Marca = "SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD";
    private const string AvisoSinValidez = "La copia va a salir marcada SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD porque se entró con la clave del día.";

    [Fact(DisplayName = "comprobante-local · Con la clave del día no se pide un código: «Reimprimir» pide el motivo, sin pedido ni campo para tipear un código")]
    public async Task Con_la_clave_no_se_pide_un_codigo()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());

        Assert.Equal(4, pantalla.FindAll("#seccion-consumos .motivo").Count);
        Assert.Empty(pantalla.FindAll("#codigo-operador"));
        Assert.Empty(pantalla.FindAll("#pedido-operador"));
        Assert.Empty(pantalla.FindAll("#seccion-consumos input[aria-label=\"Código del operador\"]"));
    }

    [Fact(DisplayName = "comprobante-local · Reimpresión con la clave del día: la copia 1 sale con REIMPRESIÓN y la marca SIN VALIDEZ, sin «Habilitó:», y TICKET_REPRINTED lleva el actor nulo con DAILY_KEY")]
    public async Task Reimpresion_con_la_clave_del_dia()
    {
        using var puesto = new PuestoDePrueba();
        var comprobantes = Path.Combine(puesto.Carpeta, "comprobantes");
        var pantalla = puesto.Dibujar(new ImpresoraSimulada(comprobantes));
        await Consumir(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);
        var ticket = Texto(puesto, "SELECT comcmpCodigo FROM tbComedorComprobantes");

        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());
        await pantalla.FindAll("#seccion-consumos .motivo")[1].ClickAsync(new());

        var lineas = File.ReadAllLines(Path.Combine(comprobantes, $"{ticket}-1.txt"));
        Assert.Contains("REIMPRESIÓN · copia 1", lineas);
        Assert.Contains(Marca, lineas);
        Assert.DoesNotContain(lineas, l => l.StartsWith("Habilitó", StringComparison.Ordinal));
        Assert.Contains("Copia 1", pantalla.Find("#mensaje-asistencia").TextContent);
        var reimpresion = Registro(puesto, "TICKET_REPRINTED");
        Assert.Equal("PAPER_ILLEGIBLE", reimpresion.GetProperty("Reason").GetString());
        Assert.Equal(JsonValueKind.Null, reimpresion.GetProperty("ActorPersonId").ValueKind);
        Assert.Equal("NO_AUTHENTICATION", reimpresion.GetProperty("ActorReason").GetString());
        Assert.Equal("DAILY_KEY", reimpresion.GetProperty("Access").GetString());
        Assert.False(reimpresion.TryGetProperty("PersonId", out _));
    }

    [Fact(DisplayName = "comprobante-local · El aviso de una copia sin validez: con la clave del día, junto a los motivos la pestaña avisa la marca con el mensaje entero, y la impresora todavía no recibió nada")]
    public async Task El_aviso_de_una_copia_sin_validez()
    {
        using var puesto = new PuestoDePrueba();
        var impresora = new ImpresoraDePrueba();
        var pantalla = puesto.Dibujar(impresora);
        await Consumir(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);
        var recibidos = impresora.Recibidos.Count;

        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());

        var aviso = pantalla.Find("#seccion-consumos .pedido-reimpresion #aviso-sin-validez");
        Assert.Equal(AvisoSinValidez, aviso.TextContent.Trim());
        Assert.Contains("aviso", aviso.ClassList);
        Assert.Equal(recibidos, impresora.Recibidos.Count);
    }

    [Fact(DisplayName = "comprobante-local · Recuperar con la clave del día: sin pedir un código, el mismo original sale otra vez sin leyenda, sin «Habilitó:» ni la marca, y TICKET_RECOVERED lleva DAILY_KEY")]
    public async Task Recuperar_con_la_clave_del_dia()
    {
        using var puesto = new PuestoDePrueba();
        var comprobantes = Path.Combine(puesto.Carpeta, "comprobantes");
        var pantalla = puesto.Dibujar(new ImpresoraSimulada(comprobantes));
        await Consumir(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);
        var ticket = Texto(puesto, "SELECT comcmpCodigo FROM tbComedorComprobantes");
        var original = Path.Combine(comprobantes, $"{ticket}-0.txt");
        File.Delete(original);
        Ejecutar(puesto, "UPDATE tbComedorTrabajosImpresion SET comimpEstado = 'FAILED' WHERE comimpNumeroCopia = 0");
        await pantalla.Find("#consultar-consumos").ClickAsync(new());

        await pantalla.Find("#seccion-consumos tr.consumo .recuperar").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#codigo-operador"));
        var lineas = File.ReadAllLines(original);
        Assert.Contains("ORIGINAL", lineas);
        Assert.DoesNotContain(lineas, l => l.Contains("SIN VALIDEZ", StringComparison.Ordinal) || l.StartsWith("Habilitó", StringComparison.Ordinal));
        Assert.Equal(0L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorTrabajosImpresion WHERE comimpNumeroCopia > 0"));
        Assert.Equal("SUBMITTED", Texto(puesto, "SELECT comimpEstado FROM tbComedorTrabajosImpresion WHERE comimpNumeroCopia = 0"));
        var recuperacion = Registro(puesto, "TICKET_RECOVERED");
        Assert.Equal(JsonValueKind.Null, recuperacion.GetProperty("ActorPersonId").ValueKind);
        Assert.Equal("DAILY_KEY", recuperacion.GetProperty("Access").GetString());
    }

    private static async Task Consumir(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        await pantalla.Find("#listo").ClickAsync(new());
    }

    private static JsonElement Registro(PuestoDePrueba puesto, string tipo) =>
        JsonDocument.Parse(Texto(puesto, $"SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = '{tipo}'")).RootElement.Clone();

    private static long Escalar(PuestoDePrueba puesto, string sql) => Convert.ToInt64(Leer(puesto, sql));

    private static string Texto(PuestoDePrueba puesto, string sql) => (string)Leer(puesto, sql)!;

    private static object? Leer(PuestoDePrueba puesto, string sql)
    {
        using var conexion = new SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return comando.ExecuteScalar();
    }

    private static void Ejecutar(PuestoDePrueba puesto, string sql)
    {
        using var conexion = new SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        comando.ExecuteNonQuery();
    }
}
