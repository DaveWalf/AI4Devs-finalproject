using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La asistencia para reimprimir, antes en la pestaña Comprobantes y desde buscar-y-exportar-consumos en las filas de
/// Consumos (comprobante-local, D8 de configurar-puesto-por-pestanas, D4 y D5 de buscar-y-exportar-consumos y D5 y D6 de
/// reimprimir-sin-codigo): reposo sin «Asistencia», la sesión del operador que entra por el engranaje y vive mientras
/// Configuración esté abierta, y el aviso del motivo cuando el permiso cae, sin pedir un código. Las reglas de RB26 son las
/// de Asistencia, que no cambia.
/// </summary>
public class PestanaComprobantesTests
{
    private const string Operador = "Operador sintético 01";

    [Fact(DisplayName = "comprobante-local · Reposo sin «Asistencia»: el campo del código e «Ingresar», y ningún botón «Asistencia»")]
    public void Reposo_sin_asistencia()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        Assert.NotNull(pantalla.Find("#codigo"));
        Assert.NotNull(pantalla.Find("#ingresar"));
        Assert.Empty(pantalla.FindAll("#asistencia"));
        Assert.DoesNotContain(pantalla.FindAll("main button"), b => b.TextContent.Contains("Asistencia", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "comprobante-local · Cerrar Configuración termina la sesión: la de 9001, que entró por el engranaje, deja de servir, la general tiene PRIVILEGED_SESSION_CLOSED con CONFIGURATION_CLOSED y, al volver con la clave del día, la pestaña no tiene su nombre ni pide un código")]
    public async Task Cerrar_configuracion_termina_la_sesion()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await EntrarAComprobantes(pantalla, "9001");

        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        Assert.Equal(0L, SesionesAbiertas(puesto));
        var cierre = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "PRIVILEGED_SESSION_CLOSED"));
        Assert.Equal("CONFIGURATION_CLOSED", cierre.GetProperty("Cause").GetString());
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());
        Assert.Empty(pantalla.FindAll("#codigo-operador"));
        Assert.DoesNotContain(Operador, pantalla.Find("#seccion-consumos").TextContent);
    }

    [Fact(DisplayName = "comprobante-local · El permiso vence durante la sesión: reimprimir se deniega sin copia, la pestaña muestra el motivo PERMISSION_EXPIRED sin pedir un código, y la acción siguiente también se deniega")]
    public async Task El_permiso_vence_durante_la_sesion()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        await pantalla.Find("#listo").ClickAsync(new());
        Ejecutar(puesto, "UPDATE tbComedorPermisosOperadores SET compopFechaVigenciaHastaUtc = '2026-11-06T15:00:20.000Z' WHERE compopCodigo = '00000000-0000-4000-8000-000000000045'");
        await EntrarAComprobantes(pantalla, "9001");
        puesto.Reloj.Avanzar(TimeSpan.FromSeconds(20));

        await pantalla.FindAll(".motivo")[0].ClickAsync(new());

        Assert.Equal(MensajesAtencion.DeMotivo("PERMISSION_EXPIRED"), pantalla.Find("#aviso-comprobantes").TextContent.Trim());
        Assert.Empty(pantalla.FindAll("#codigo-operador"));
        Assert.Equal(0L, Copias(puesto));
        await pantalla.Find(".reimprimir").ClickAsync(new());
        await pantalla.FindAll(".motivo")[0].ClickAsync(new());
        Assert.Equal(MensajesAtencion.DeMotivo("PERMISSION_EXPIRED"), pantalla.Find("#aviso-comprobantes").TextContent.Trim());
        Assert.Equal(0L, Copias(puesto));
        Assert.Equal(2L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = 'PRIVILEGED_ACTION_DENIED'"));
    }

    /// <summary>
    /// La asistencia desde la fila de Consumos: si no hay ningún consumo, hace uno de 0001; entra a Configuración por el
    /// engranaje con el código del operador y toca «Reimprimir» en la primera fila, que pide el motivo sin otro código
    /// (reimprimir-sin-codigo). Antes era la pestaña Comprobantes, y después el código se tipeaba en Consumos.
    /// </summary>
    private static async Task EntrarAComprobantes(IRenderedComponent<PantallaAtencion> pantalla, string codigo)
    {
        if (pantalla.FindAll("[role=\"tablist\"]").Count == 0)
        {
            if (pantalla.FindAll("#listo").Count == 0 && pantalla.FindAll("#codigo").Count > 0)
            {
                await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
                await pantalla.Find("#ingresar").ClickAsync(new());
                await pantalla.FindAll(".opcion")[0].ClickAsync(new());
                await pantalla.Find("#confirmar").ClickAsync(new());
                pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
                await pantalla.Find("#listo").ClickAsync(new());
            }

            await EntradaConfiguracion.Entrar(pantalla, codigo);
        }

        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());
    }

    private static long Copias(PuestoDePrueba puesto) =>
        Escalar(puesto, "SELECT COUNT(*) FROM tbComedorTrabajosImpresion WHERE comimpNumeroCopia > 0");

    private static long Escalar(PuestoDePrueba puesto, string sql)
    {
        using var conexion = new SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private static long SesionesAbiertas(PuestoDePrueba puesto)
    {
        using var conexion = new SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM tbComedorSesionesIdentidad WHERE comsidProposito = 'PRIVILEGED_ACTION' AND comsidFechaVencimientoUtc > $ahora";
        comando.Parameters.AddWithValue("$ahora", puesto.Reloj.GetUtcNow().UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private static void Ejecutar(PuestoDePrueba puesto, string sql)
    {
        using var conexion = new SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        Assert.Equal(1, comando.ExecuteNonQuery());
    }
}
