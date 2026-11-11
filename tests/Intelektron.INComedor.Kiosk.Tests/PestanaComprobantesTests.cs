using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La asistencia para reimprimir, antes en la pestaña Comprobantes y desde buscar-y-exportar-consumos en las filas de
/// Consumos (comprobante-local, D8 de configurar-puesto-por-pestanas y D4 y D5 de buscar-y-exportar-consumos): reposo sin
/// «Asistencia», la sesión del operador que vive mientras Configuración esté abierta y vence a los 30 s sin un toque, y el
/// aviso del motivo cuando el permiso cae. Las reglas de RB26 son las de Asistencia, que no cambia.
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

    [Fact(DisplayName = "comprobante-local · Operador con permiso vigente: al reimprimir en Consumos, 9001 abre la sesión y la pestaña muestra su nombre, el motivo de la reimpresión y «Salir»")]
    public async Task Operador_con_permiso_vigente()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        await EntrarAComprobantes(pantalla, "9001");

        var pestana = pantalla.Find("#seccion-consumos");
        Assert.Contains(Operador, pestana.TextContent);
        Assert.Equal(4, pestana.QuerySelectorAll(".motivo").Length);
        Assert.NotNull(pestana.QuerySelector("#salir"));
        Assert.Empty(pantalla.FindAll("#codigo-operador"));
    }

    [Fact(DisplayName = "comprobante-local · Cerrar Configuración termina la sesión: deja de servir, la general tiene PRIVILEGED_SESSION_CLOSED con CONFIGURATION_CLOSED y al volver a abrir la pestaña pide un código")]
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
        Assert.NotNull(pantalla.Find("#codigo-operador"));
        Assert.DoesNotContain(Operador, pantalla.Find("#seccion-consumos").TextContent);
    }

    [Fact(DisplayName = "comprobante-local · Treinta segundos sin tocar la pestaña: la sesión termina y la pestaña vuelve a pedir un código, sin el nombre del operador")]
    public async Task Treinta_segundos_sin_tocar_la_pestana()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await EntrarAComprobantes(pantalla, "9001");

        puesto.Reloj.Avanzar(TimeSpan.FromSeconds(30));

        pantalla.WaitForAssertion(() => Assert.DoesNotContain(Operador, pantalla.Find("#seccion-consumos").TextContent), TimeSpan.FromSeconds(5));
        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());
        Assert.NotNull(pantalla.Find("#codigo-operador"));
        Assert.Equal(0L, SesionesAbiertas(puesto));
        Assert.NotEmpty(pantalla.FindAll("[role=\"tablist\"]"));
    }

    [Fact(DisplayName = "comprobante-local · La sesión del operador vence en el segundo 30 sin un toque, no en el 29, y un toque reinicia el plazo")]
    public void La_sesion_vence_a_los_30_segundos()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Dibujar();
        var sesion = new SesionOperador(puesto.Servicio.Asistencia, puesto.Reloj);
        sesion.Entrar("9001");
        Assert.True(sesion.Abierta);

        puesto.Reloj.Avanzar(TimeSpan.FromSeconds(29));
        Assert.False(sesion.RevisarInactividad());
        sesion.Tocar();
        puesto.Reloj.Avanzar(TimeSpan.FromSeconds(29));
        Assert.False(sesion.RevisarInactividad());
        Assert.True(sesion.Abierta);

        puesto.Reloj.Avanzar(TimeSpan.FromSeconds(1));
        Assert.True(sesion.RevisarInactividad());
        Assert.False(sesion.Abierta);
        Assert.Null(sesion.NombreVisible);
    }

    [Fact(DisplayName = "comprobante-local · El permiso vence durante la sesión: reimprimir cierra la sesión y la pestaña muestra el motivo PERMISSION_EXPIRED y vuelve a pedir un código")]
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

        await pantalla.Find(".reimprimir").ClickAsync(new());
        await pantalla.FindAll(".motivo")[0].ClickAsync(new());

        Assert.Equal(MensajesAtencion.DeMotivo("PERMISSION_EXPIRED"), pantalla.Find("#aviso-comprobantes").TextContent.Trim());
        Assert.NotNull(pantalla.Find("#codigo-operador"));
        Assert.DoesNotContain(Operador, pantalla.Find("#seccion-consumos").TextContent);
    }

    /// <summary>
    /// La asistencia desde la fila de Consumos (buscar-y-exportar-consumos): si no hay ningún consumo, hace uno de 0001; entra
    /// a Configuración, toca «Reimprimir» en la primera fila y tipea el código. Antes era la pestaña Comprobantes.
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

            await EntradaConfiguracion.Entrar(pantalla);
        }

        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());
        await pantalla.Find("#codigo-operador").InputAsync(new ChangeEventArgs { Value = codigo });
        await pantalla.Find("#entrar-operador").ClickAsync(new());
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
