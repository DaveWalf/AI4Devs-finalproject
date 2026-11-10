using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El modo simulación sólo en mantenimiento y su registro en la general (acceso-puesto de simular-identificacion-por-lista,
/// D4 a D7 y D12): encendido por omisión, se apaga en General, se conserva, en operativo nunca está activo y pasar o arrancar
/// en operativo no cambia lo guardado. Sobre el puesto de bUnit de configurar-puesto-por-pestanas, con el almacén de la
/// simulación en su carpeta.
/// </summary>
public class ModoSimulacionTests
{
    [Fact(DisplayName = "acceso-puesto · Una carpeta nueva arranca con la simulación: General dice «Simulación: encendida» y la pantalla de identificación tiene el combo")]
    public async Task Una_carpeta_nueva_arranca_con_la_simulacion()
    {
        using var puesto = Puesto();
        var pantalla = puesto.Dibujar();

        Assert.NotEmpty(pantalla.FindAll("#simulacion-persona"));
        await EntradaConfiguracion.Entrar(pantalla);

        Assert.Equal("Simulación: encendida", pantalla.Find("#modo-simulacion").TextContent.Trim());
        Assert.NotEmpty(pantalla.FindAll("#apagar-simulacion"));
        Assert.False(File.Exists(Path.Combine(puesto.Carpeta, "simulacion.json")));
    }

    [Fact(DisplayName = "acceso-puesto · Apagarla se conserva al volver a abrir: «Apagar la simulación» la guarda, y el puesto que arranca con esa carpeta la tiene apagada y sin combo")]
    public async Task Apagarla_se_conserva_al_volver_a_abrir()
    {
        using var primero = Puesto();
        var pantalla = primero.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#apagar-simulacion").ClickAsync(new());

        Assert.Equal("Simulación: apagada", pantalla.Find("#modo-simulacion").TextContent.Trim());
        Assert.NotEmpty(pantalla.FindAll("#encender-simulacion"));

        // El puesto se cierra y se vuelve a abrir: otro puesto arranca con el ajuste que dejó el primero.
        using var segundo = new PuestoDePrueba();
        File.Copy(Path.Combine(primero.Carpeta, "simulacion.json"), Path.Combine(segundo.Carpeta, "simulacion.json"));
        segundo.Contexto.Services.AddSingleton(new AlmacenSimulacion(segundo.Carpeta));
        var reabierta = segundo.Dibujar();

        Assert.Empty(reabierta.FindAll("#simulacion-persona"));
        await EntradaConfiguracion.Entrar(reabierta);
        Assert.Equal("Simulación: apagada", reabierta.Find("#modo-simulacion").TextContent.Trim());
    }

    [Fact(DisplayName = "acceso-puesto · En operativo nunca se ve: en operativo no hay combo ni simulación en General, lo guardado sigue encendido, y de vuelta en mantenimiento dice «Simulación: encendida» y el combo vuelve")]
    public async Task En_operativo_nunca_se_ve()
    {
        using var puesto = Puesto(simulacion: true);
        var pantalla = puesto.Dibujar();
        Assert.NotEmpty(pantalla.FindAll("#simulacion-persona"));
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#pasar-operativo").ClickAsync(new());
        await Aceptar(pantalla, "4711");

        Assert.Contains("Modo: operativo", pantalla.Find("#seccion-general").TextContent);
        Assert.Empty(pantalla.FindAll("#modo-simulacion"));
        Assert.Empty(pantalla.FindAll("#encender-simulacion"));
        Assert.Empty(pantalla.FindAll("#apagar-simulacion"));
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        Assert.Empty(pantalla.FindAll("#simulacion-persona"));
        Assert.True(new AlmacenSimulacion(puesto.Carpeta).Leer());

        await pantalla.Find(".engranaje").ClickAsync(new());
        await Aceptar(pantalla, "4711");
        await pantalla.Find("#pasar-mantenimiento").ClickAsync(new());

        Assert.Equal("Simulación: encendida", pantalla.Find("#modo-simulacion").TextContent.Trim());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll("#simulacion-persona"));
    }

    [Fact(DisplayName = "acceso-puesto · Operativo con la simulación guardada encendida: arranca sin combo, el ajuste sigue guardado encendido y no hay ningún CONFIGURATION_CHANGED de la simulación")]
    public void Operativo_con_la_simulacion_guardada_encendida()
    {
        using var puesto = Puesto(ModoPuesto.Operativo, simulacion: true);
        var pantalla = puesto.Dibujar();

        Assert.Empty(pantalla.FindAll("#simulacion-persona"));
        Assert.NotEmpty(pantalla.FindAll("#codigo"));
        Assert.True(new AlmacenSimulacion(puesto.Carpeta).Leer());
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"
            && ContextoDe(l).GetProperty("Setting").GetString() == "SIMULATION_MODE");
    }

    [Fact(DisplayName = "acceso-puesto · Un ajuste de simulación ilegible: con texto que no es un valor conocido, arranca en mantenimiento con la simulación encendida y atiende normalmente")]
    public async Task Un_ajuste_ilegible()
    {
        using var puesto = Puesto();
        File.WriteAllText(Path.Combine(puesto.Carpeta, "simulacion.json"), "{\"simulacion\":\"PRENDIDA\"}");
        var pantalla = puesto.Dibujar();

        Assert.NotEmpty(pantalla.FindAll("#simulacion-persona"));
        await EntradaConfiguracion.Entrar(pantalla);
        Assert.Equal("Simulación: encendida", pantalla.Find("#modo-simulacion").TextContent.Trim());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        Assert.Contains("Persona sintética 01", pantalla.Find("#saludo").TextContent);
    }

    [Fact(DisplayName = "acceso-puesto · Apagar la simulación: la general tiene CONFIGURATION_CHANGED con SIMULATION_MODE, de ON a OFF, APPLIED, DAILY_KEY, el actor nulo y NO_AUTHENTICATION")]
    public async Task Apagar_la_simulacion_queda_en_la_general()
    {
        using var puesto = Puesto();
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#apagar-simulacion").ClickAsync(new());

        var cambio = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"));
        Assert.Equal("SIMULATION_MODE", cambio.GetProperty("Setting").GetString());
        Assert.Equal(("ON", "OFF", "APPLIED", "OFF"),
            (cambio.GetProperty("Before").GetString(), cambio.GetProperty("After").GetString(), cambio.GetProperty("Result").GetString(), cambio.GetProperty("Effective").GetString()));
        Assert.Equal("DAILY_KEY", cambio.GetProperty("Access").GetString());
        Assert.True(SinActor(cambio));
    }

    [Fact(DisplayName = "acceso-puesto · Pasar a operativo no la registra: la general tiene sólo el CONFIGURATION_CHANGED de KIOSK_MODE, con DAILY_KEY, y ninguno de SIMULATION_MODE")]
    public async Task Pasar_a_operativo_no_la_registra()
    {
        using var puesto = Puesto(simulacion: true);
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#pasar-operativo").ClickAsync(new());
        await Aceptar(pantalla, "4711");

        var cambios = puesto.General().Where(l => Tipo(l) == "CONFIGURATION_CHANGED").Select(ContextoDe).ToList();
        Assert.Equal(["KIOSK_MODE"], cambios.Select(c => c.GetProperty("Setting").GetString()));
        Assert.Equal("DAILY_KEY", cambios[0].GetProperty("Access").GetString());
    }

    [Fact(DisplayName = "acceso-puesto · La simulación no se puede guardar: al apagarla, General avisa, sigue encendida, el combo se conserva y la general tiene SIMULATION_MODE con FAILED")]
    public async Task La_simulacion_no_se_puede_guardar()
    {
        using var puesto = Puesto(simulacion: true);
        File.SetAttributes(Path.Combine(puesto.Carpeta, "simulacion.json"), FileAttributes.ReadOnly);
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#apagar-simulacion").ClickAsync(new());

        Assert.Equal(MensajesAtencion.SimulacionNoGuardada, pantalla.Find("#aviso-simulacion").TextContent.Trim());
        Assert.Equal("Simulación: encendida", pantalla.Find("#modo-simulacion").TextContent.Trim());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll("#simulacion-persona"));
        var cambio = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"));
        Assert.Equal("SIMULATION_MODE", cambio.GetProperty("Setting").GetString());
        Assert.Equal(("ON", "OFF", "FAILED", "ON"),
            (cambio.GetProperty("Before").GetString(), cambio.GetProperty("After").GetString(), cambio.GetProperty("Result").GetString(), cambio.GetProperty("Effective").GetString()));
    }

    /// <summary>El puesto de bUnit con el almacén de la simulación en su carpeta y, si se pide, el ajuste ya guardado.</summary>
    private static PuestoDePrueba Puesto(ModoPuesto? modo = null, bool? simulacion = null)
    {
        var puesto = new PuestoDePrueba(modo);
        if (simulacion is { } guardada)
        {
            new AlmacenSimulacion(puesto.Carpeta).Guardar(guardada);
        }

        puesto.Contexto.Services.AddSingleton(new AlmacenSimulacion(puesto.Carpeta));
        return puesto;
    }

    private static async Task Aceptar(IRenderedComponent<PantallaAtencion> pantalla, string clave)
    {
        await pantalla.Find("#clave-del-dia").InputAsync(new ChangeEventArgs { Value = clave });
        await pantalla.Find("#aceptar-clave").ClickAsync(new());
    }
}
