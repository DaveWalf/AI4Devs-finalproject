using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Configuración en este corte (D5 de presentar-atencion-tactil): el engranaje abre un diálogo con «Estilo visual» y, con
/// la impresora simulada, sus modos. bUnit sobre un <see cref="ServicioAtencion"/> real, una base y una carpeta
/// temporales.
/// </summary>
public class PanelConfiguracionTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "incomedor-tests", "configuracion-" + Guid.NewGuid().ToString("N"));
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public PanelConfiguracionTests()
    {
        Directory.CreateDirectory(_carpeta);
    }

    private string ArchivoEstilo => Path.Combine(_carpeta, "estilo-visual.json");

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, recursive: true);
        }
    }

    [Fact(DisplayName = "presentacion-puesto · El engranaje abre un diálogo modal con «Estilo visual»: opacidad de 72 a 88 y desenfoque de 0 a 12, con su valor visible")]
    public async Task El_engranaje_abre_el_dialogo()
    {
        var pantalla = Dibujar(new ImpresoraDePrueba());
        Assert.Empty(pantalla.FindAll("[role=\"dialog\"]"));

        await pantalla.Find(".engranaje").ClickAsync(new());

        var dialogo = pantalla.Find("[role=\"dialog\"]");
        Assert.Equal("true", dialogo.GetAttribute("aria-modal"));
        Assert.Contains("Estilo visual", dialogo.TextContent);
        var opacidad = pantalla.Find("#opacidad");
        Assert.Equal(("range", "72", "88", "1", "76"), (opacidad.GetAttribute("type"), opacidad.GetAttribute("min"), opacidad.GetAttribute("max"), opacidad.GetAttribute("step"), opacidad.GetAttribute("value")));
        var desenfoque = pantalla.Find("#desenfoque");
        Assert.Equal(("range", "0", "12", "1", "4"), (desenfoque.GetAttribute("type"), desenfoque.GetAttribute("min"), desenfoque.GetAttribute("max"), desenfoque.GetAttribute("step"), desenfoque.GetAttribute("value")));
        Assert.NotNull(pantalla.Find("label[for=\"opacidad\"]"));
        Assert.NotNull(pantalla.Find("label[for=\"desenfoque\"]"));
        Assert.Contains("76 %", pantalla.Find("#valor-opacidad").TextContent);
        Assert.Contains("4 px", pantalla.Find("#valor-desenfoque").TextContent);
    }

    [Fact(DisplayName = "presentacion-puesto · Mover un rango cambia la variable CSS al instante, y el valor visible con él")]
    public async Task Mover_un_rango_cambia_la_variable()
    {
        var pantalla = Dibujar(new ImpresoraDePrueba());
        Assert.Contains("--opacidad-superficie: 0.76", Estilo(pantalla));
        Assert.Contains("--desenfoque-fondo: 4px", Estilo(pantalla));
        await pantalla.Find(".engranaje").ClickAsync(new());

        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "84" });
        await pantalla.Find("#desenfoque").InputAsync(new ChangeEventArgs { Value = "8" });

        Assert.Contains("--opacidad-superficie: 0.84", Estilo(pantalla));
        Assert.Contains("--desenfoque-fondo: 8px", Estilo(pantalla));
        Assert.Contains("84 %", pantalla.Find("#valor-opacidad").TextContent);
        Assert.False(File.Exists(ArchivoEstilo));
    }

    [Fact(DisplayName = "presentacion-puesto · Guardar y volver a abrir: «Guardar» escribe 84 % y 8 px, y un puesto nuevo los usa")]
    public async Task Guardar_y_volver_a_abrir()
    {
        var pantalla = Dibujar(new ImpresoraDePrueba());
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "84" });
        await pantalla.Find("#desenfoque").InputAsync(new ChangeEventArgs { Value = "8" });

        await pantalla.Find("#guardar-configuracion").ClickAsync(new());

        Assert.Equal(MensajesAtencion.EstiloGuardado, pantalla.Find("[role=\"dialog\"] #aviso-guardado").TextContent.Trim());
        Assert.Equal(new EstiloVisual(84, 8), new AlmacenEstiloVisual(_carpeta).Leer());
        using var otro = new BunitContext();
        var reabierta = Dibujar(new ImpresoraDePrueba(), otro);
        Assert.Contains("--opacidad-superficie: 0.84", Estilo(reabierta));
        Assert.Contains("--desenfoque-fondo: 8px", Estilo(reabierta));
    }

    [Theory(DisplayName = "presentacion-puesto · Cancelar descarta la vista previa: con «Cancelar» o Escape vuelven los valores guardados y el archivo no cambia")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancelar_o_escape_restauran(bool conEscape)
    {
        new AlmacenEstiloVisual(_carpeta).Guardar(new EstiloVisual(76, 4));
        var antes = File.ReadAllText(ArchivoEstilo);
        var pantalla = Dibujar(new ImpresoraDePrueba());
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "88" });
        await pantalla.Find("#desenfoque").InputAsync(new ChangeEventArgs { Value = "12" });

        if (conEscape)
        {
            await pantalla.Find("[role=\"dialog\"]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        }
        else
        {
            await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        }

        Assert.Empty(pantalla.FindAll("[role=\"dialog\"]"));
        Assert.Contains("--opacidad-superficie: 0.76", Estilo(pantalla));
        Assert.Contains("--desenfoque-fondo: 4px", Estilo(pantalla));
        Assert.Equal(antes, File.ReadAllText(ArchivoEstilo));
    }

    [Fact(DisplayName = "comprobante-local · Con otra impresora no hay sección «Impresora simulada»")]
    public async Task Con_otra_impresora_no_hay_seccion()
    {
        var pantalla = Dibujar(new ImpresoraDePrueba());

        await pantalla.Find(".engranaje").ClickAsync(new());

        Assert.DoesNotContain("Impresora simulada", pantalla.Find("[role=\"dialog\"]").TextContent);
        Assert.Empty(pantalla.FindAll("#impresora-sinpapel"));
    }

    [Fact(DisplayName = "comprobante-local · La atención no muestra los modos; con la impresora simulada, Configuración los ofrece con sus id")]
    public async Task Los_modos_estan_en_configuracion()
    {
        var pantalla = Dibujar(Simulada());
        Assert.Empty(pantalla.FindAll("#impresora-disponible"));
        Assert.Empty(pantalla.FindAll("#impresora-sinpapel"));
        Assert.Empty(pantalla.FindAll("#impresora-incierta"));

        await pantalla.Find(".engranaje").ClickAsync(new());

        var dialogo = pantalla.Find("[role=\"dialog\"]");
        Assert.Contains("Impresora simulada", dialogo.TextContent);
        Assert.Equal(3, dialogo.QuerySelectorAll("#impresora-disponible, #impresora-sinpapel, #impresora-incierta").Length);
    }

    [Fact(DisplayName = "comprobante-local · Elegir «Sin papel» desde Configuración: la confirmación siguiente registra el consumo y deja el comprobante pendiente")]
    public async Task Sin_papel_desde_configuracion()
    {
        var pantalla = Dibujar(Simulada());
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#impresora-sinpapel").ClickAsync(new());
        await pantalla.Find("#guardar-configuracion").ClickAsync(new());

        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.ComprobantePendiente, pantalla.Find("#mensaje").TextContent.Trim()));
    }

    private ImpresoraSimulada Simulada() => new(Path.Combine(_carpeta, "comprobantes"));

    private IRenderedComponent<PantallaAtencion> Dibujar(IImpresoraComprobantes impresora, BunitContext? contexto = null)
    {
        contexto ??= _contexto;
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, impresora);
        contexto.Services.AddSingleton(servicio);
        contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        contexto.Services.AddSingleton(new AlmacenEstiloVisual(_carpeta));
        return contexto.Render<PantallaAtencion>();
    }

    private static string Estilo(IRenderedComponent<PantallaAtencion> pantalla) => pantalla.Find(".puesto").GetAttribute("style") ?? "";
}
