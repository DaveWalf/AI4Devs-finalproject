using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El orden de las pestañas y «Cerrar» (configuracion-puesto de ordenar-configuracion, D1 y D2): Fin llega a General, la
/// última, e Inicio vuelve a Consumos; el pie dice «Cerrar» con la conducta de «Cancelar». El orden y la pestaña inicial
/// los afirma PestanasConfiguracionTests.
/// bUnit sobre un <see cref="ServicioAtencion"/> real.
/// </summary>
public class OrdenConfiguracionTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "incomedor-tests", "orden-" + Guid.NewGuid().ToString("N"));
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public OrdenConfiguracionTests()
    {
        Directory.CreateDirectory(_carpeta);
    }

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, recursive: true);
        }
    }

    [Fact(DisplayName = "configuracion-puesto · Las pestañas con el teclado: desde Consumos, flecha derecha elige Personas, Fin elige General e Inicio vuelve a Consumos")]
    public async Task Las_pestanas_con_el_teclado_desde_consumos()
    {
        var pantalla = Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#pestana-consumos").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("true", pantalla.Find("#pestana-personas").GetAttribute("aria-selected"));

        await pantalla.Find("#pestana-personas").KeyDownAsync(new KeyboardEventArgs { Key = "End" });
        Assert.Equal("true", pantalla.Find("#pestana-general").GetAttribute("aria-selected"));

        await pantalla.Find("#pestana-general").KeyDownAsync(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal("true", pantalla.Find("#pestana-consumos").GetAttribute("aria-selected"));
        Assert.Equal(("0", "-1"), (pantalla.Find("#pestana-consumos").GetAttribute("tabindex"), pantalla.Find("#pestana-general").GetAttribute("tabindex")));
    }

    [Fact(DisplayName = "configuracion-puesto · Cancelar descarta lo no guardado y conserva lo guardado: el pie dice «Cerrar», ningún botón suyo dice «Cancelar», y «Cerrar» descarta la opacidad no guardada")]
    public async Task El_pie_dice_cerrar_y_descarta_lo_no_guardado()
    {
        var pantalla = Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-identidad").ClickAsync(new());
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "88" });

        var pie = pantalla.Find(".pie-dialogo");
        Assert.Equal(["Cerrar"], pie.QuerySelectorAll("button").Select(b => b.TextContent.Trim()));
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("[role=\"dialog\"]"));
        Assert.Contains("--opacidad-superficie: 0.76", pantalla.Find(".puesto").GetAttribute("style") ?? "");
        Assert.False(File.Exists(Path.Combine(_carpeta, "estilo-visual.json")));
    }

    private IRenderedComponent<PantallaAtencion> Dibujar()
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: true);
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        _contexto.Services.AddSingleton(new AlmacenEstiloVisual(_carpeta));
        return _contexto.Render<PantallaAtencion>();
    }
}
