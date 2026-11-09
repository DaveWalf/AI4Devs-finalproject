using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El engranaje en el pie (D1 y D2 de mover-engranaje-al-pie), con bUnit sobre un <see cref="ServicioAtencion"/> real y
/// una base temporal: en reposo, la marca Intelektron y después el engranaje, al final del pie; en la selección, el lugar
/// reservado del engranaje y ningún botón. El tamaño y la posición se miden en la ventana real, en el E2E.
/// </summary>
public class PieEngranajeTests : IDisposable
{
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
    }

    [Fact(DisplayName = "presentacion-puesto · El engranaje en el borde inferior derecho: en reposo, dentro del pie, la marca Intelektron va antes del engranaje y son lo último del pie")]
    public void En_reposo_la_marca_y_despues_el_engranaje_al_final_del_pie()
    {
        var pantalla = Dibujar();

        var pie = pantalla.Find("footer");
        var marca = pie.QuerySelector("img.marca-intelektron")!;
        var engranaje = Assert.Single(pie.QuerySelectorAll("button.engranaje"));

        Assert.Same(engranaje, marca.NextElementSibling);
        Assert.Null(engranaje.NextElementSibling);
        Assert.True(ReferenceEquals(marca.ParentElement, pie) || pie.LastElementChild == marca.ParentElement,
            "La marca y el engranaje deben ser lo último del pie, solos o dentro de un grupo que cierra el pie.");
    }

    [Fact(DisplayName = "presentacion-puesto · El pie no cambia de alto al salir de reposo: en la selección el pie tiene el lugar reservado del engranaje y ningún botón")]
    public async Task En_la_seleccion_el_lugar_reservado_y_ningun_boton()
    {
        var pantalla = Dibujar();
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());

        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        var pie = pantalla.Find("footer");
        Assert.NotNull(pie.QuerySelector("span.lugar-engranaje"));
        Assert.Empty(pie.QuerySelectorAll("button"));
    }

    private IRenderedComponent<PantallaAtencion> Dibujar()
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba());
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        return _contexto.Render<PantallaAtencion>();
    }
}
