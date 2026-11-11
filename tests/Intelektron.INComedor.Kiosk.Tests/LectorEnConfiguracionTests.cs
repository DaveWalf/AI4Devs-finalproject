using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Intelektron.INComedor.Kiosk.Lector;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La pestaña Lector (lector-identificacion y configuracion-puesto de elegir-medio-de-identificacion, D8): después de Red,
/// guarda y valida el medio de identificación, registra READER, lista los puertos del equipo, muestra el estado y la
/// última lectura sin el código, y con un operador no existe. bUnit sobre el puesto de prueba.
/// </summary>
public class LectorEnConfiguracionTests
{
    [Fact(DisplayName = "lector-identificacion · Elegir el puerto serie: COM3 a 9600 8N1 con el terminador CR y el relleno 4 se guarda, se avisa y la general tiene CONFIGURATION_CHANGED de READER")]
    public async Task Elegir_el_puerto_serie()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirLector(pantalla);

        await pantalla.Find("#medio-puerto").ClickAsync(new());
        await Escribir(pantalla, "#puerto-lector", "COM3");
        await Escribir(pantalla, "#velocidad-lector", "9600");
        await pantalla.Find("#bits-8").ClickAsync(new());
        await pantalla.Find("#paridad-ninguna").ClickAsync(new());
        await pantalla.Find("#parada-1").ClickAsync(new());
        await pantalla.Find("#terminador-cr").ClickAsync(new());
        await Escribir(pantalla, "#relleno-lector", "4");
        await pantalla.Find("#guardar-lector").ClickAsync(new());

        Assert.Equal(MensajesAtencion.LectorGuardado, pantalla.Find("#aviso-guardado").TextContent.Trim());
        Assert.Equal(ConfiguracionLector.PorDefecto with
        {
            Medio = MedioIdentificacion.PuertoSerie, Puerto = "COM3", Terminador = TerminadorLector.Cr, Relleno = 4,
        }, new AlmacenLector(puesto.Carpeta).Leer());
        var contexto = ContextoDe(puesto.General().Single(l => Tipo(l) == "CONFIGURATION_CHANGED" && ContextoDe(l).GetProperty("Setting").GetString() == "READER"));
        Assert.Equal("APPLIED", contexto.GetProperty("Result").GetString());
        Assert.Equal("KEYBOARD", contexto.GetProperty("Before").GetProperty("Medium").GetString());
        Assert.Equal("SERIAL_PORT", contexto.GetProperty("After").GetProperty("Medium").GetString());
    }

    [Fact(DisplayName = "lector-identificacion · Un puerto fuera de forma: con el puerto serie, sin puerto y con el relleno 25, los dos campos se señalan y la configuración no cambia")]
    public async Task Un_puerto_fuera_de_forma()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirLector(pantalla);

        await pantalla.Find("#medio-puerto").ClickAsync(new());
        await Escribir(pantalla, "#relleno-lector", "25");
        await pantalla.Find("#guardar-lector").ClickAsync(new());

        Assert.Equal("true", pantalla.Find("#puerto-lector").GetAttribute("aria-invalid"));
        Assert.Equal("true", pantalla.Find("#relleno-lector").GetAttribute("aria-invalid"));
        Assert.Null(pantalla.Find("#velocidad-lector").GetAttribute("aria-invalid"));
        Assert.Equal(MensajesAtencion.PanelInvalido, pantalla.Find("#aviso-lector").TextContent.Trim());
        Assert.Equal(ConfiguracionLector.PorDefecto, new AlmacenLector(puesto.Carpeta).Leer());
    }

    [Fact(DisplayName = "lector-identificacion · Pasar un código por el lector simulado: la pestaña dice que escucha el simulador y que la última lectura identificó, sin el código")]
    public async Task El_estado_y_la_ultima_lectura()
    {
        using var puesto = Preparar(ConfiguracionLector.PorDefecto with { Medio = MedioIdentificacion.Simulador });
        var pantalla = puesto.Dibujar();
        await pantalla.Find("#lector-simulado").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#pasar-por-lector").ClickAsync(new());
        pantalla.WaitForAssertion(() => Assert.NotEmpty(pantalla.FindAll(".opcion")));
        await pantalla.Find("#cancelar").ClickAsync(new());

        await AbrirLector(pantalla);

        Assert.Equal(MensajesAtencion.EstadoLector(EstadoLector.Escuchando, ConfiguracionLector.PorDefecto with { Medio = MedioIdentificacion.Simulador }),
            pantalla.Find("#estado-lector").TextContent.Trim());
        var ultima = pantalla.Find("#ultima-lectura").TextContent;
        Assert.Contains("identificó", ultima, StringComparison.Ordinal);
        Assert.DoesNotContain("0001", ultima, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "lector-identificacion · El medio de identificación: «Actualizar» lista los puertos que el equipo tiene")]
    public async Task Actualizar_los_puertos()
    {
        var puerto = new PuertoSimulado();
        puerto.Puertos.Add("COM7");
        using var puesto = Preparar(puerto: puerto);
        var pantalla = puesto.Dibujar();
        await AbrirLector(pantalla);

        await pantalla.Find("#actualizar-puertos").ClickAsync(new());

        Assert.Equal(MensajesAtencion.PuertosDelEquipo(["COM3", "COM7"]), pantalla.Find("#puertos-lector").TextContent.Trim());
    }

    [Fact(DisplayName = "lector-identificacion · El medio de identificación: con el código de un operador no existe la pestaña Lector")]
    public async Task Con_un_operador()
    {
        using var conClave = Preparar();
        var conLaClave = conClave.Dibujar();
        await EntradaConfiguracion.Entrar(conLaClave);
        Assert.Single(conLaClave.FindAll("#pestana-lector"));
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();

        await EntradaConfiguracion.Entrar(pantalla, "9001");

        Assert.Empty(pantalla.FindAll("#pestana-lector"));
        Assert.Empty(pantalla.FindAll("#seccion-lector"));
    }

    private static PuestoDePrueba Preparar(ConfiguracionLector? configuracion = null, PuertoSimulado? puerto = null)
    {
        var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        if (configuracion is not null)
        {
            new AlmacenLector(puesto.Carpeta).Guardar(configuracion);
        }

        puesto.Contexto.Services.AddSingleton(new AlmacenLector(puesto.Carpeta));
        if (puerto is not null)
        {
            puesto.Contexto.Services.AddSingleton<IPuertoSerie>(puerto);
        }

        return puesto;
    }

    private static async Task AbrirLector(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-lector").ClickAsync(new());
    }

    private static Task Escribir(IRenderedComponent<PantallaAtencion> pantalla, string selector, string valor) =>
        pantalla.Find(selector).InputAsync(new ChangeEventArgs { Value = valor });
}
