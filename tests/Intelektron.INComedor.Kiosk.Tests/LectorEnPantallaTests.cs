using System.Text;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Intelektron.INComedor.Kiosk.Lector;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La lectura en la atención (lector-identificacion de elegir-medio-de-identificacion, D5 y D6): el simulador interno en
/// reposo sólo en mantenimiento, el puerto con el puerto simulado como doble, el rechazo como el del código tipeado y los
/// descartes en una atención o con un diálogo abierto. bUnit sobre el puesto de prueba con la base de la demostración.
/// </summary>
public class LectorEnPantallaTests
{
    [Fact(DisplayName = "lector-identificacion · Pasar un código por el lector simulado: en mantenimiento, 1 con el relleno 4 identifica a la persona de 0001 y ofrece sus opciones")]
    public async Task Pasar_un_codigo_por_el_lector_simulado()
    {
        using var puesto = Preparar(ConfiguracionLector.PorDefecto with { Medio = MedioIdentificacion.Simulador, Relleno = 4 });
        var pantalla = puesto.Dibujar();

        await pantalla.Find("#lector-simulado").InputAsync(new ChangeEventArgs { Value = "1" });
        await pantalla.Find("#pasar-por-lector").ClickAsync(new());

        pantalla.WaitForAssertion(() => Assert.NotEmpty(pantalla.FindAll(".opcion")));
        Assert.Contains("Persona sintética 01", pantalla.Markup, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "lector-identificacion · El simulador interno: en operativo la pantalla de reposo no muestra el lector simulado")]
    public void En_operativo_no_hay_lector_simulado()
    {
        using var puesto = Preparar(ConfiguracionLector.PorDefecto with { Medio = MedioIdentificacion.Simulador }, ModoPuesto.Operativo);

        var pantalla = puesto.Dibujar();

        Assert.Empty(pantalla.FindAll("#lector-simulado"));
        Assert.Empty(pantalla.FindAll("#pasar-por-lector"));
    }

    [Fact(DisplayName = "lector-identificacion · La tarjeta de 0001: por el puerto serie con el terminador CR y el relleno 4, 1 y CR identifican a la persona de 0001")]
    public void La_tarjeta_de_0001_por_el_puerto()
    {
        var puerto = new PuertoSimulado();
        using var puesto = Preparar(Serie(TerminadorLector.Cr, 4), puerto: puerto);
        var pantalla = puesto.Dibujar();

        puerto.Escribir(Bytes("1\r"));

        pantalla.WaitForAssertion(() => Assert.NotEmpty(pantalla.FindAll(".opcion")));
    }

    [Fact(DisplayName = "lector-identificacion · Una credencial vencida por el lector: 0002 dice «Esta credencial está vencida», como con el código tipeado")]
    public void Una_credencial_vencida_por_el_lector()
    {
        var puerto = new PuertoSimulado();
        using var puesto = Preparar(Serie(TerminadorLector.CrLf, 0), puerto: puerto);
        var pantalla = puesto.Dibujar();

        puerto.Escribir(Bytes("0002\r\n"));

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.DeMotivo("CREDENTIAL_EXPIRED"), pantalla.Find("#aviso").TextContent.Trim()));
    }

    [Fact(DisplayName = "lector-identificacion · Una lectura durante la selección: otro código no cambia la selección, nadie más queda identificado y la general registra NOT_IDLE")]
    public async Task Una_lectura_durante_la_seleccion()
    {
        var puerto = new PuertoSimulado();
        using var puesto = Preparar(Serie(TerminadorLector.CrLf, 0), puerto: puerto);
        var pantalla = puesto.Dibujar();
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        var antes = pantalla.FindAll(".opcion").Count;

        puerto.Escribir(Bytes("0002\r\n"));

        pantalla.WaitForAssertion(() => Assert.Contains(puesto.General(), l => Tipo(l) == "READER_READING_DISCARDED" && ContextoDe(l).GetProperty("Reason").GetString() == "NOT_IDLE"));
        Assert.Equal(antes, pantalla.FindAll(".opcion").Count);
        Assert.Empty(pantalla.FindAll("#aviso"));
        Assert.All(puesto.General().Where(l => Tipo(l)!.StartsWith("READER_", StringComparison.Ordinal)),
            l => Assert.DoesNotContain("0002", ContextoDe(l).GetRawText(), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "lector-identificacion · Fuera de la identificación la lectura se descarta: con Configuración abierta, una lectura no identifica a nadie y la general registra DIALOG_OPEN")]
    public async Task Con_configuracion_abierta()
    {
        var puerto = new PuertoSimulado();
        using var puesto = Preparar(Serie(TerminadorLector.CrLf, 0), puerto: puerto);
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);

        puerto.Escribir(Bytes("0001\r\n"));

        pantalla.WaitForAssertion(() => Assert.Contains(puesto.General(), l => Tipo(l) == "READER_READING_DISCARDED" && ContextoDe(l).GetProperty("Reason").GetString() == "DIALOG_OPEN"));
        Assert.Empty(pantalla.FindAll(".opcion"));
    }

    private static ConfiguracionLector Serie(TerminadorLector terminador, int relleno) =>
        ConfiguracionLector.PorDefecto with { Medio = MedioIdentificacion.PuertoSerie, Puerto = "COM3", Terminador = terminador, Relleno = relleno };

    private static PuestoDePrueba Preparar(ConfiguracionLector configuracion, ModoPuesto? modo = null, PuertoSimulado? puerto = null)
    {
        var puesto = new PuestoDePrueba(modo);
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        new AlmacenLector(puesto.Carpeta).Guardar(configuracion);
        puesto.Contexto.Services.AddSingleton(new AlmacenLector(puesto.Carpeta));
        if (puerto is not null)
        {
            puesto.Contexto.Services.AddSingleton<IPuertoSerie>(puerto);
        }

        return puesto;
    }

    private static byte[] Bytes(string texto) => Encoding.ASCII.GetBytes(texto);
}
