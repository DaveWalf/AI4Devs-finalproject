using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El modo simulación sólo en mantenimiento y su registro en la general (acceso-puesto de simular-identificacion-por-lista,
/// D4 a D7): apagado por omisión, se enciende en General, se conserva, pasar a operativo lo apaga y un puesto operativo nunca
/// lo tiene activo. Sobre el puesto de bUnit de configurar-puesto-por-pestanas, con el almacén de la simulación en su carpeta.
/// </summary>
public class ModoSimulacionTests
{
    [Fact(DisplayName = "acceso-puesto · Una carpeta nueva arranca sin simulación: General dice «Simulación: apagada» y la pantalla de identificación no tiene combo")]
    public async Task Una_carpeta_nueva_arranca_sin_simulacion()
    {
        using var puesto = Puesto();
        var pantalla = puesto.Dibujar();

        Assert.Empty(pantalla.FindAll("#simulacion-persona"));
        await pantalla.Find(".engranaje").ClickAsync(new());

        Assert.Equal("Simulación: apagada", pantalla.Find("#modo-simulacion").TextContent.Trim());
        Assert.NotEmpty(pantalla.FindAll("#encender-simulacion"));
        Assert.False(File.Exists(Path.Combine(puesto.Carpeta, "simulacion.json")));
    }

    [Fact(DisplayName = "acceso-puesto · Encenderla se conserva al volver a abrir: «Encender la simulación» la guarda, y el puesto que arranca con esa carpeta la tiene encendida y muestra el combo")]
    public async Task Encenderla_se_conserva_al_volver_a_abrir()
    {
        using var primero = Puesto();
        var pantalla = primero.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());

        await pantalla.Find("#encender-simulacion").ClickAsync(new());

        Assert.Equal("Simulación: encendida", pantalla.Find("#modo-simulacion").TextContent.Trim());
        Assert.NotEmpty(pantalla.FindAll("#apagar-simulacion"));

        // El puesto se cierra y se vuelve a abrir: otro puesto arranca con el ajuste que dejó el primero.
        using var segundo = new PuestoDePrueba();
        File.Copy(Path.Combine(primero.Carpeta, "simulacion.json"), Path.Combine(segundo.Carpeta, "simulacion.json"));
        segundo.Contexto.Services.AddSingleton(new AlmacenSimulacion(segundo.Carpeta));
        var reabierta = segundo.Dibujar();

        Assert.NotEmpty(reabierta.FindAll("#simulacion-persona"));
        await reabierta.Find(".engranaje").ClickAsync(new());
        Assert.Equal("Simulación: encendida", reabierta.Find("#modo-simulacion").TextContent.Trim());
    }

    [Fact(DisplayName = "acceso-puesto · Pasar a operativo la apaga: en operativo no hay combo ni simulación en General, y de vuelta en mantenimiento dice «Simulación: apagada»")]
    public async Task Pasar_a_operativo_la_apaga()
    {
        using var puesto = Puesto(simulacion: true);
        var pantalla = puesto.Dibujar();
        Assert.NotEmpty(pantalla.FindAll("#simulacion-persona"));
        await pantalla.Find(".engranaje").ClickAsync(new());

        await pantalla.Find("#pasar-operativo").ClickAsync(new());
        await Aceptar(pantalla, "4711");

        Assert.Contains("Modo: operativo", pantalla.Find("#seccion-general").TextContent);
        Assert.Empty(pantalla.FindAll("#modo-simulacion"));
        Assert.Empty(pantalla.FindAll("#encender-simulacion"));
        Assert.Empty(pantalla.FindAll("#apagar-simulacion"));
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        Assert.Empty(pantalla.FindAll("#simulacion-persona"));
        Assert.False(new AlmacenSimulacion(puesto.Carpeta).Leer());

        await pantalla.Find(".engranaje").ClickAsync(new());
        await Aceptar(pantalla, "4711");
        await pantalla.Find("#pasar-mantenimiento").ClickAsync(new());

        Assert.Equal("Simulación: apagada", pantalla.Find("#modo-simulacion").TextContent.Trim());
    }

    [Fact(DisplayName = "acceso-puesto · Operativo con la simulación guardada encendida: arranca sin combo y el ajuste queda guardado apagado, con su CONFIGURATION_CHANGED")]
    public void Operativo_con_la_simulacion_guardada_encendida()
    {
        using var puesto = Puesto(ModoPuesto.Operativo, simulacion: true);
        var pantalla = puesto.Dibujar();

        Assert.Empty(pantalla.FindAll("#simulacion-persona"));
        Assert.NotEmpty(pantalla.FindAll("#codigo"));
        Assert.False(new AlmacenSimulacion(puesto.Carpeta).Leer());
        var cambio = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"));
        Assert.Equal("SIMULATION_MODE", cambio.GetProperty("Setting").GetString());
        Assert.Equal(("ON", "OFF", "APPLIED", "OFF"),
            (cambio.GetProperty("Before").GetString(), cambio.GetProperty("After").GetString(), cambio.GetProperty("Result").GetString(), cambio.GetProperty("Effective").GetString()));
        Assert.Equal("STARTUP", cambio.GetProperty("Access").GetString());
        Assert.True(SinActor(cambio));
    }

    [Fact(DisplayName = "acceso-puesto · Un ajuste de simulación ilegible: con texto que no es un valor conocido, arranca en mantenimiento con la simulación apagada y atiende normalmente")]
    public async Task Un_ajuste_ilegible()
    {
        using var puesto = Puesto();
        File.WriteAllText(Path.Combine(puesto.Carpeta, "simulacion.json"), "{\"simulacion\":\"PRENDIDA\"}");
        var pantalla = puesto.Dibujar();

        Assert.Empty(pantalla.FindAll("#simulacion-persona"));
        await pantalla.Find(".engranaje").ClickAsync(new());
        Assert.Equal("Simulación: apagada", pantalla.Find("#modo-simulacion").TextContent.Trim());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        Assert.Contains("Persona sintética 01", pantalla.Find("#saludo").TextContent);
    }

    [Fact(DisplayName = "acceso-puesto · Encender la simulación: la general tiene CONFIGURATION_CHANGED con SIMULATION_MODE, de OFF a ON, APPLIED, MAINTENANCE_MODE, el actor nulo y NO_AUTHENTICATION")]
    public async Task Encender_la_simulacion_queda_en_la_general()
    {
        using var puesto = Puesto();
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());

        await pantalla.Find("#encender-simulacion").ClickAsync(new());

        var cambio = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"));
        Assert.Equal("SIMULATION_MODE", cambio.GetProperty("Setting").GetString());
        Assert.Equal(("OFF", "ON", "APPLIED", "ON"),
            (cambio.GetProperty("Before").GetString(), cambio.GetProperty("After").GetString(), cambio.GetProperty("Result").GetString(), cambio.GetProperty("Effective").GetString()));
        Assert.Equal("MAINTENANCE_MODE", cambio.GetProperty("Access").GetString());
        Assert.True(SinActor(cambio));
    }

    [Fact(DisplayName = "acceso-puesto · Apagada al pasar a operativo: la general tiene el CONFIGURATION_CHANGED de KIOSK_MODE y después el de SIMULATION_MODE, de ON a OFF, los dos con DAILY_KEY")]
    public async Task Apagada_al_pasar_a_operativo()
    {
        using var puesto = Puesto(simulacion: true);
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());

        await pantalla.Find("#pasar-operativo").ClickAsync(new());
        await Aceptar(pantalla, "4711");

        var cambios = puesto.General().Where(l => Tipo(l) == "CONFIGURATION_CHANGED").Select(ContextoDe).ToList();
        Assert.Equal(["KIOSK_MODE", "SIMULATION_MODE"], cambios.Select(c => c.GetProperty("Setting").GetString()));
        Assert.All(cambios, c => Assert.Equal("DAILY_KEY", c.GetProperty("Access").GetString()));
        var simulacion = cambios[1];
        Assert.Equal(("ON", "OFF", "APPLIED"),
            (simulacion.GetProperty("Before").GetString(), simulacion.GetProperty("After").GetString(), simulacion.GetProperty("Result").GetString()));
        Assert.True(SinActor(simulacion));
    }

    [Fact(DisplayName = "acceso-puesto · La simulación no se puede guardar: General avisa, sigue apagada, no hay combo y la general tiene SIMULATION_MODE con FAILED")]
    public async Task La_simulacion_no_se_puede_guardar()
    {
        using var puesto = Puesto(simulacion: false);
        File.SetAttributes(Path.Combine(puesto.Carpeta, "simulacion.json"), FileAttributes.ReadOnly);
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());

        await pantalla.Find("#encender-simulacion").ClickAsync(new());

        Assert.Equal(MensajesAtencion.SimulacionNoGuardada, pantalla.Find("#aviso-simulacion").TextContent.Trim());
        Assert.Equal("Simulación: apagada", pantalla.Find("#modo-simulacion").TextContent.Trim());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        Assert.Empty(pantalla.FindAll("#simulacion-persona"));
        var cambio = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"));
        Assert.Equal("SIMULATION_MODE", cambio.GetProperty("Setting").GetString());
        Assert.Equal(("OFF", "ON", "FAILED", "OFF"),
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
