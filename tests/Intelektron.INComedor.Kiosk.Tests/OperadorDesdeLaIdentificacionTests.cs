using System.Text.Json;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Un operador que se identifica en reposo (operador-desde-la-identificacion): quien tiene OPERATOR vigente ve «Configuración»
/// junto a «Cancelar» en la selección o junto a «Listo» sin opciones, por el código o por el combo; tocarlo vuelve a evaluar el
/// permiso, termina la atención sin consumo y abre Configuración como el engranaje con su código, con OPERATOR_IDENTIFICATION en
/// la general. Para el operador con comidas, la prueba le da OPERATOR a «Persona sintética 01» en la versión activa.
/// </summary>
public class OperadorDesdeLaIdentificacionTests
{
    private static readonly Guid PersonaDe0001 = Guid.Parse("00000000-0000-4000-8000-000000000027");
    private static readonly Guid PersonaDe9001 = Guid.Parse("00000000-0000-4000-8000-000000000037");

    [Fact(DisplayName = "atencion-puesto · Un operador con comidas: con OPERATOR y cupo de ALMUERZO, la selección muestra sus opciones, «Cancelar» y «Configuración», y al confirmar una opción el consumo se registra como el de cualquier comensal")]
    public async Task Un_operador_con_comidas()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        DarOperador(puesto, PersonaDe0001);

        await Identificar(pantalla, "0001");

        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        Assert.Equal("Cancelar", pantalla.Find("#cancelar").TextContent.Trim());
        Assert.Equal("Configuración", pantalla.Find("#configuracion-operador").TextContent.Trim());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        Assert.Equal(1L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorConsumos"));
    }

    [Fact(DisplayName = "atencion-puesto · Un operador sin comidas: 9001 ve «No tenés opciones disponibles ahora.» con «Listo» y «Configuración», y tocarlo abre Consumos, Personas e Impresora, con Consumos elegida y sin General")]
    public async Task Un_operador_sin_comidas()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        await Identificar(pantalla, "9001");

        Assert.Equal(MensajesAtencion.SinOpciones, pantalla.Find("#mensaje").TextContent.Trim());
        Assert.Equal("Listo", pantalla.Find("#listo").TextContent.Trim());
        await pantalla.Find("#configuracion-operador").ClickAsync(new());

        Assert.Equal(["pestana-consumos", "pestana-personas", "pestana-impresora"], pantalla.FindAll("[role=\"tab\"]").Select(t => t.Id).ToArray());
        Assert.Equal("true", pantalla.Find("#pestana-consumos").GetAttribute("aria-selected"));
        Assert.Empty(pantalla.FindAll("#pestana-general"));
    }

    [Fact(DisplayName = "atencion-puesto · Desde el combo de la simulación: elegir a la persona de 9001 y tocar «Configuración» abre Consumos, Personas e Impresora, sin pedir ninguna clave ni código")]
    public async Task Desde_el_combo_de_la_simulacion()
    {
        using var puesto = new PuestoDePrueba();
        new AlmacenSimulacion(puesto.Carpeta).Guardar(true);
        puesto.Contexto.Services.AddSingleton(new AlmacenSimulacion(puesto.Carpeta));
        var pantalla = puesto.Dibujar();

        await pantalla.Find("#simulacion-persona").ClickAsync(new());
        await pantalla.FindAll("#simulacion-persona-lista [role=\"option\"]").Single(e => e.TextContent.Trim() == "Operador sintético 01").ClickAsync(new());
        await pantalla.Find("#configuracion-operador").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        Assert.Equal(["pestana-consumos", "pestana-personas", "pestana-impresora"], pantalla.FindAll("[role=\"tab\"]").Select(t => t.Id).ToArray());
    }

    [Theory(DisplayName = "atencion-puesto · Una persona sin permiso de operador, con el permiso vencido o sólo administrador no ve «Configuración»")]
    [InlineData("0001")]
    [InlineData("9004")]
    [InlineData("9002")]
    public async Task Sin_permiso_no_hay_configuracion(string codigo)
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();

        await Identificar(pantalla, codigo);

        Assert.True(pantalla.FindAll(".opcion").Count > 0 || pantalla.FindAll("#listo").Count > 0, "La identificación no llegó a la selección ni al aviso.");
        Assert.Empty(pantalla.FindAll("#configuracion-operador"));
    }

    [Fact(DisplayName = "atencion-puesto · El permiso cae antes de tocar «Configuración»: Configuración sigue cerrada, la pantalla avisa que el permiso está vencido y la general tiene la denegación con PERMISSION_EXPIRED")]
    public async Task El_permiso_cae_antes_de_tocar()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        DarOperador(puesto, PersonaDe0001);
        await Identificar(pantalla, "0001");
        Ejecutar(puesto, "UPDATE tbComedorPermisosOperadores SET compopFechaVigenciaHastaUtc = $ahora WHERE comperCodigo = $per",
            ("$ahora", "2026-11-06T15:00:00.000Z"), ("$per", PersonaDe0001.ToString("D")));

        await pantalla.Find("#configuracion-operador").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("[role=\"tab\"]"));
        Assert.Equal(MensajesAtencion.DeMotivo("PERMISSION_EXPIRED"), pantalla.Find("#aviso-operador").TextContent.Trim());
        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        var denegada = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_ACCESS_DENIED"));
        Assert.Equal("PERMISSION_EXPIRED", denegada.GetProperty("ReasonCode").GetString());
        Assert.Equal("OPERATOR_IDENTIFICATION", denegada.GetProperty("Access").GetString());
        Assert.Equal(PersonaDe0001, denegada.GetProperty("ActorPersonId").GetGuid());
        var asistencia = ContextoDe(Assert.Single(Interacciones(puesto), l => Tipo(l) == "ASSISTANCE_DENIED"));
        Assert.Equal("IDENTIFICATION", asistencia.GetProperty("Source").GetString());
    }

    [Fact(DisplayName = "atencion-puesto · Después de una denegación: «Configuración» deja de ofrecerse en esa atención, y la persona siguiente no ve el aviso")]
    public async Task Despues_de_una_denegacion()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        DarOperador(puesto, PersonaDe0001);
        await Identificar(pantalla, "0001");
        Ejecutar(puesto, "UPDATE tbComedorPermisosOperadores SET compopFechaVigenciaHastaUtc = $ahora WHERE comperCodigo = $per",
            ("$ahora", "2026-11-06T15:00:00.000Z"), ("$per", PersonaDe0001.ToString("D")));
        await pantalla.Find("#configuracion-operador").ClickAsync(new());
        Assert.NotNull(pantalla.Find("#aviso-operador"));

        Assert.Empty(pantalla.FindAll("#configuracion-operador"));
        await pantalla.Find("#cancelar").ClickAsync(new());
        await Identificar(pantalla, "0001");

        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        Assert.Empty(pantalla.FindAll("#aviso-operador"));
    }

    [Fact(DisplayName = "atencion-puesto · Configuración no deja consumo: un operador con comidas toca «Configuración» y la cierra, sin intención ni consumo nuevos, con su sesión de consumo vencida y el puesto en reposo, sin su nombre")]
    public async Task Configuracion_no_deja_consumo()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        DarOperador(puesto, PersonaDe0001);
        await Identificar(pantalla, "0001");

        await pantalla.Find("#configuracion-operador").ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll("[role=\"tab\"]"));
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        Assert.Equal(0L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorIntencionesConsumo"));
        Assert.Equal(0L, Escalar(puesto, "SELECT COUNT(*) FROM tbComedorConsumos"));
        Assert.Equal(0L, Escalar(puesto,
            "SELECT COUNT(*) FROM tbComedorSesionesIdentidad WHERE comsidProposito = 'CONSUMPTION' AND comsidFechaVencimientoUtc > '2026-11-06T15:00:00.000Z'"));
        Assert.NotEmpty(pantalla.FindAll("#codigo"));
        Assert.DoesNotContain("Persona sintética 01", pantalla.Markup);
    }

    [Fact(DisplayName = "acceso-puesto · Entrada de un operador desde la identificación: 9001, «Ingresar» y «Configuración» dejan CONFIGURATION_OPENED con OPERATOR_IDENTIFICATION y su persona como actor, y ningún registro de las dos bitácoras contiene 9001")]
    public async Task La_general_distingue_la_entrada()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await Identificar(pantalla, "9001");

        await pantalla.Find("#configuracion-operador").ClickAsync(new());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        var general = puesto.General();
        var abierta = ContextoDe(Assert.Single(general, l => Tipo(l) == "CONFIGURATION_OPENED"));
        Assert.Equal("OPERATOR_IDENTIFICATION", abierta.GetProperty("Access").GetString());
        Assert.Equal(PersonaDe9001, abierta.GetProperty("ActorPersonId").GetGuid());
        var concedida = ContextoDe(Assert.Single(Interacciones(puesto), l => Tipo(l) == "ASSISTANCE_GRANTED"));
        Assert.Equal("IDENTIFICATION", concedida.GetProperty("Source").GetString());
        var carpeta = Directory.GetDirectories(Path.Combine(puesto.Carpeta, "bitacoras")).Single();
        foreach (var archivo in Directory.GetFiles(carpeta, "*.log"))
        {
            Assert.DoesNotContain("9001", File.ReadAllText(archivo));
        }
    }

    /// <summary>Las líneas de la bitácora de interacciones de noviembre de 2026, después de la pasada del escritor de General().</summary>
    private static List<JsonElement> Interacciones(PuestoDePrueba puesto)
    {
        puesto.General();
        var carpeta = Directory.GetDirectories(Path.Combine(puesto.Carpeta, "bitacoras")).Single();
        var ruta = Directory.GetFiles(carpeta, "INComedor-interacciones-*.log").Single();
        return File.ReadAllText(ruta).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();
    }

    private static async Task Identificar(IRenderedComponent<PantallaAtencion> pantalla, string codigo)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = codigo });
        await pantalla.Find("#ingresar").ClickAsync(new());
    }

    /// <summary>Le da OPERATOR vigente y sin fin a una persona, en la versión activa del puesto.</summary>
    private static void DarOperador(PuestoDePrueba puesto, Guid persona) =>
        Ejecutar(puesto,
            """
            INSERT INTO tbComedorPermisosOperadores
            VALUES ((SELECT comvcCodigoActiva FROM tbComedorConfiguracionPuestos), $pop, $per, 'OPERATOR', NULL, NULL, NULL)
            """,
            ("$pop", Guid.NewGuid().ToString("D")), ("$per", persona.ToString("D")));

    private static void Ejecutar(PuestoDePrueba puesto, string sql, params (string Nombre, object Valor)[] parametros)
    {
        using var conexion = new SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor);
        }

        Assert.Equal(1, comando.ExecuteNonQuery());
    }

    private static long Escalar(PuestoDePrueba puesto, string sql)
    {
        using var conexion = new SqliteConnection($"Data Source={puesto.RutaBase};Mode=ReadOnly;Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(comando.ExecuteScalar());
    }
}
