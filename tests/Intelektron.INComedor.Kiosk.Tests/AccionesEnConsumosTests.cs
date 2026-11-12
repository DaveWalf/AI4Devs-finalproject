using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Ver, Reimprimir y Recuperar en cada fila de Consumos, sin la pestaña Comprobantes (comprobante-local de
/// buscar-y-exportar-consumos, D4 y D7, y de reimprimir-sin-codigo, D1 y D5): «Ver» sin operador y sin imprimir; reimprimir
/// y recuperar con el acceso con que se entró a Configuración, sin pedir otro código. bUnit sobre el puesto de prueba.
/// </summary>
public class AccionesEnConsumosTests
{
    private const string Operador = "Operador sintético 01";

    [Fact(DisplayName = "comprobante-local · Ver un comprobante: «Ver» lo muestra con la marca COPIA EN PANTALLA, su número, el comensal y la opción, sin el código y sin pedir un operador, y al cerrarlo Consumos sigue igual")]
    public async Task Ver_un_comprobante()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);

        var fila = pantalla.Find("#seccion-consumos tr.consumo");
        var numero = fila.QuerySelector("td.numero")!.TextContent.Trim();
        var opcion = fila.QuerySelectorAll("td")[3].TextContent.Trim();
        var cargo = fila.QuerySelector("td.cargo")!.TextContent.Trim();
        await pantalla.Find("#seccion-consumos tr.consumo .ver").ClickAsync(new());

        var comprobante = pantalla.Find("#comprobante-en-pantalla");
        Assert.Contains("COPIA EN PANTALLA", comprobante.TextContent);
        Assert.Contains(numero, comprobante.TextContent);
        Assert.Contains(opcion, comprobante.TextContent);
        Assert.Contains(cargo, comprobante.TextContent);
        Assert.Contains("06/11/2026", comprobante.TextContent);
        Assert.Contains("Persona sintética", comprobante.TextContent);
        Assert.DoesNotContain("0001", comprobante.TextContent);
        Assert.Empty(pantalla.FindAll("#codigo-operador"));
        await pantalla.Find("#cerrar-comprobante").ClickAsync(new());
        Assert.Empty(pantalla.FindAll("#comprobante-en-pantalla"));
        Assert.Single(pantalla.FindAll("#seccion-consumos tr.consumo"));
    }

    [Fact(DisplayName = "comprobante-local · Ver no imprime ni crea copia: verlo dos veces deja los mismos trabajos de impresión, sin copias")]
    public async Task Ver_no_imprime_ni_crea_copia()
    {
        using var puesto = new PuestoDePrueba();
        var impresora = new ImpresoraDePrueba();
        var pantalla = puesto.Dibujar(impresora);
        await Consumir(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);
        var trabajos = Escalar(puesto, "SELECT COUNT(*) FROM tbComedorTrabajosImpresion");
        var recibidos = impresora.Recibidos.Count;

        for (var i = 0; i < 2; i++)
        {
            await pantalla.Find("#seccion-consumos tr.consumo .ver").ClickAsync(new());
            await pantalla.Find("#cerrar-comprobante").ClickAsync(new());
        }

        Assert.Equal(trabajos, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorTrabajosImpresion"));
        Assert.Equal(0L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorTrabajosImpresion WHERE comimpNumeroCopia > 0"));
        Assert.Equal(recibidos, impresora.Recibidos.Count);
    }

    [Fact(DisplayName = "comprobante-local · Con la clave del día no se pide un código: «Reimprimir» pide el motivo sin campo de código ni operador, y la copia 1 sale")]
    public async Task Con_la_clave_no_se_pide_un_codigo()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#codigo-operador"));
        Assert.DoesNotContain(Operador, pantalla.Find("#seccion-consumos").TextContent);
        await pantalla.FindAll("#seccion-consumos .motivo")[0].ClickAsync(new());
        Assert.Equal(1L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorTrabajosImpresion WHERE comimpNumeroCopia = 1"));
        Assert.Contains("Copia 1", pantalla.Find("#mensaje-asistencia").TextContent);
    }

    [Fact(DisplayName = "comprobante-local · Un operador que entró con su código: con 9001, «Reimprimir» pide el motivo sin otro código, y la reimpresión queda en la bitácora con su persona")]
    public async Task Un_operador_que_entro_con_su_codigo()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla);
        await EntradaConfiguracion.Entrar(pantalla, "9001");

        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#codigo-operador"));
        await pantalla.FindAll("#seccion-consumos .motivo")[0].ClickAsync(new());
        Assert.Equal(1L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorTrabajosImpresion WHERE comimpNumeroCopia = 1"));
        var reimpresion = System.Text.Json.JsonDocument.Parse(Texto(puesto, "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = 'TICKET_REPRINTED'")).RootElement;
        var operador = Guid.Parse(Texto(puesto, "SELECT comperCodigo FROM tbComedorPersonas WHERE comperNombreVisible = 'Operador sintético 01' LIMIT 1"));
        Assert.Equal(operador, reimpresion.GetProperty("PersonId").GetGuid());
    }

    [Fact(DisplayName = "comprobante-local · Trabajo fallido por papel: «Recuperar» aparece sólo con el original en FAILED, y lo envía otra vez sin copia nueva")]
    public async Task Recuperar_solo_con_el_original_fallido()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla);
        await EntradaConfiguracion.Entrar(pantalla, "9001");
        Assert.Empty(pantalla.FindAll("#seccion-consumos tr.consumo .recuperar"));

        Ejecutar(puesto, "UPDATE tbComedorTrabajosImpresion SET comimpEstado = 'FAILED' WHERE comimpNumeroCopia = 0");
        await pantalla.Find("#consultar-consumos").ClickAsync(new());
        await pantalla.Find("#seccion-consumos tr.consumo .recuperar").ClickAsync(new());

        Assert.Equal(0L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorTrabajosImpresion WHERE comimpNumeroCopia > 0"));
        Assert.Equal(1L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = 'TICKET_RECOVERED'"));
    }

    [Fact(DisplayName = "configuracion-puesto · Abre en Consumos con todas las pestañas en orden: once pestañas, sin Comprobantes")]
    public async Task Sin_la_pestana_comprobantes()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        await EntradaConfiguracion.Entrar(pantalla);

        Assert.Equal(11, pantalla.FindAll("[role=\"tablist\"] [role=\"tab\"]").Count);
        Assert.Empty(pantalla.FindAll("#pestana-comprobantes"));
        Assert.Empty(pantalla.FindAll("#seccion-comprobantes"));
    }

    [Fact(DisplayName = "comprobante-local · Recuperar con la clave del día: «Recuperar» saca el original otra vez sin pedir un código, sin copia nueva")]
    public async Task Recuperar_con_la_clave_del_dia()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);
        Ejecutar(puesto, "UPDATE tbComedorTrabajosImpresion SET comimpEstado = 'FAILED' WHERE comimpNumeroCopia = 0");
        await pantalla.Find("#consultar-consumos").ClickAsync(new());

        await pantalla.Find("#seccion-consumos tr.consumo .recuperar").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#codigo-operador"));
        Assert.Equal(1L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = 'TICKET_RECOVERED'"));
        Assert.Equal(0L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorTrabajosImpresion WHERE comimpNumeroCopia > 0"));
    }

    [Fact(DisplayName = "comprobante-local · Cerrar Configuración termina la sesión: 9001 reimprime desde Consumos, sin «Salir» en la pestaña, y cerrar Configuración la cierra una vez con CONFIGURATION_CLOSED")]
    public async Task Cerrar_configuracion_termina_la_sesion()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla);
        await EntradaConfiguracion.Entrar(pantalla, "9001");
        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());
        await pantalla.FindAll("#seccion-consumos .motivo")[0].ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#salir"));
        Assert.Equal(0L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = 'PRIVILEGED_SESSION_CLOSED'"));
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        Assert.Equal(1L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = 'PRIVILEGED_SESSION_CLOSED'"));
        Assert.Equal("CONFIGURATION_CLOSED", Texto(puesto,
            "SELECT json_extract(combitContexto, '$.Cause') FROM tbComedorBitacora WHERE combitTipoEvento = 'PRIVILEGED_SESSION_CLOSED'"));
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

    private static long Escalar(PuestoDePrueba puesto, string sql)
    {
        using var conexion = new SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private static string Texto(PuestoDePrueba puesto, string sql)
    {
        using var conexion = new SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return (string)comando.ExecuteScalar()!;
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
