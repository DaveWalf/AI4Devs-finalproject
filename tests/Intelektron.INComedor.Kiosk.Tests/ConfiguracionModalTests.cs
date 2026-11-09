using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El diálogo de Configuración atrapa el foco (sugerencia 6 de la verificación del 03/11): mientras está abierto, el resto
/// de la página es inerte, así que con Tab no se llega a «Ingresar». Y si igual se sale de reposo, Configuración se cancela
/// como con «Cancelar»: la vista previa sin guardar no queda aplicada ni el panel reaparece al volver a reposo (D5).
/// </summary>
public class ConfiguracionModalTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "incomedor-tests", "configuracion-modal-" + Guid.NewGuid().ToString("N"));
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public ConfiguracionModalTests()
    {
        Directory.CreateDirectory(_carpeta);
    }

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
        Directory.Delete(_carpeta, recursive: true);
    }

    [Fact(DisplayName = "presentacion-puesto · Con Configuración abierta, la cabecera, la atención y el pie son inertes; al cerrarla dejan de serlo")]
    public async Task El_resto_de_la_pagina_es_inerte_con_el_dialogo_abierto()
    {
        var pantalla = Dibujar();
        Assert.All(new[] { ".cabecera", ".atencion", ".pie" }, selector => Assert.False(pantalla.Find(selector).HasAttribute("inert")));

        await pantalla.Find(".engranaje").ClickAsync(new());

        Assert.All(new[] { ".cabecera", ".atencion", ".pie" }, selector => Assert.True(pantalla.Find(selector).HasAttribute("inert"), selector));
        Assert.Null(pantalla.Find("[role=\"dialog\"]").Closest("[inert]"));

        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        Assert.All(new[] { ".cabecera", ".atencion", ".pie" }, selector => Assert.False(pantalla.Find(selector).HasAttribute("inert"), selector));
    }

    [Fact(DisplayName = "presentacion-puesto · Si la atención arranca con la vista previa sin guardar, Configuración se cancela y no reaparece al volver a reposo")]
    public async Task Salir_de_reposo_cancela_la_configuracion()
    {
        var pantalla = Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "88" });
        await pantalla.Find("#desenfoque").InputAsync(new ChangeEventArgs { Value = "12" });

        // bUnit no respeta inert: así se reproduce llegar a «Ingresar» detrás del diálogo.
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());

        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        Assert.Contains("--opacidad-superficie: 0.76", Estilo(pantalla));
        Assert.Contains("--desenfoque-fondo: 4px", Estilo(pantalla));

        await pantalla.Find("#cancelar").ClickAsync(new());

        Assert.NotEmpty(pantalla.FindAll("#ingresar"));
        Assert.Empty(pantalla.FindAll("[role=\"dialog\"]"));
        Assert.Contains("--opacidad-superficie: 0.76", Estilo(pantalla));
        Assert.False(File.Exists(Path.Combine(_carpeta, "estilo-visual.json")));
    }

    private IRenderedComponent<PantallaAtencion> Dibujar()
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba());
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        _contexto.Services.AddSingleton(new AlmacenEstiloVisual(_carpeta));
        return _contexto.Render<PantallaAtencion>();
    }

    private static string Estilo(IRenderedComponent<PantallaAtencion> pantalla) => pantalla.Find(".puesto").GetAttribute("style") ?? "";
}
