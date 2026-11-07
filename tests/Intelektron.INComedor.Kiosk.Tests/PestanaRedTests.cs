using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Intelektron.INComedor.Kiosk.Red;
using Microsoft.AspNetCore.Components;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La pestaña Red (conectividad-puesto de configurar-puesto-por-pestanas, D11) con los adaptadores reales del equipo, sin
/// dobles: el puesto de bUnit no registra <see cref="EjecutorNetsh"/>, así que confirmar nunca corre <c>netsh</c> y la red
/// del equipo no cambia. La vista sin adaptadores se prueba con una lista vacía.
/// </summary>
public class PestanaRedTests
{
    private static readonly string[] Campos = ["#ipv4-direccion", "#ipv4-mascara", "#ipv4-puerta", "#ipv4-dns1", "#ipv4-dns2"];

    [Fact(DisplayName = "conectividad-puesto · Los datos de red del equipo: el nombre del equipo y cada adaptador activo real con su IPv4, su máscara, su puerta, sus DNS y si usa DHCP, elegidos de una lista y sin campo de texto para el adaptador")]
    public async Task Los_adaptadores_reales()
    {
        var datos = DatosDeRed.Leer();
        Assert.True(datos.Adaptadores.Count > 0, "El equipo de la prueba no tiene ningún adaptador de red activo.");
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirRed(puesto);

        Assert.Contains(datos.NombreEquipo, pantalla.Find("#nombre-equipo").TextContent);
        var items = pantalla.FindAll("#seccion-red .adaptador");
        Assert.Equal(datos.Adaptadores.Count, items.Count);
        foreach (var (adaptador, item) in datos.Adaptadores.Zip(items))
        {
            var texto = item.TextContent;
            Assert.Contains(adaptador.Nombre, texto);
            Assert.Contains(adaptador.Dhcp ? "DHCP" : "IPv4 fija", texto);
            Assert.All(adaptador.Direcciones, d => Assert.Contains($"{d.Direccion} / {d.Mascara}", texto));
            Assert.All(adaptador.Puertas.Concat(adaptador.Dns), d => Assert.Contains(d, texto));
            Assert.Equal("radio", item.QuerySelector("input")!.GetAttribute("type"));
        }

        Assert.All(pantalla.FindAll("#seccion-red input:not([type=radio])"), e => Assert.Contains("#" + e.Id, Campos));
    }

    [Fact(DisplayName = "conectividad-puesto · Sin adaptadores activos: la vista de una lista vacía dice «Sin conexión de red» y no ofrece editar")]
    public void Sin_adaptadores()
    {
        using var contexto = new BunitContext();

        var pestana = contexto.Render<PestanaRed>(p => p.Add(x => x.Datos, new DatosRed("PUESTO-01", [])));

        Assert.Equal(MensajesAtencion.SinRed, pestana.Find("#sin-red").TextContent.Trim());
        Assert.Contains("PUESTO-01", pestana.Find("#nombre-equipo").TextContent);
        Assert.Empty(pestana.FindAll("#aplicar-red"));
    }

    [Fact(DisplayName = "conectividad-puesto · Un valor que no es una IPv4: «Aplicar» con 192.168.10.300, después 1, después «192.168.10.30 & del» y después la máscara 255.0.255.0 señala cada vez el campo y no pide confirmación")]
    public async Task Un_valor_invalido()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirRed(puesto);
        await CargarFija(pantalla);

        foreach (var (campo, valor) in new[]
                 {
                     ("#ipv4-direccion", "192.168.10.300"), ("#ipv4-direccion", "1"), ("#ipv4-direccion", "192.168.10.30 & del"),
                     ("#ipv4-mascara", "255.0.255.0"),
                 })
        {
            await CargarFija(pantalla);
            await pantalla.Find(campo).InputAsync(new ChangeEventArgs { Value = valor });
            await pantalla.Find("#aplicar-red").ClickAsync(new());

            Assert.Equal("true", pantalla.Find(campo).GetAttribute("aria-invalid"));
            Assert.All(Campos.Where(c => c != campo), c => Assert.Null(pantalla.Find(c).GetAttribute("aria-invalid")));
            Assert.Equal(MensajesAtencion.RedRevisar, pantalla.Find("#aviso-red").TextContent.Trim());
            Assert.Empty(pantalla.FindAll("#confirmacion-red"));
        }

        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED");
    }

    [Fact(DisplayName = "conectividad-puesto · IPv4 fija con confirmación: con valores válidos «Aplicar» pide confirmar y avisa que puede cortar la conexión; «Cancelar» no ejecuta ni registra nada y los valores siguen en la pestaña")]
    public async Task Confirmar_y_cancelar()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirRed(puesto);
        await CargarFija(pantalla);

        await pantalla.Find("#aplicar-red").ClickAsync(new());

        Assert.Contains(MensajesAtencion.RedAviso, pantalla.Find("#confirmacion-red").TextContent);
        Assert.All(Campos, c => Assert.Null(pantalla.Find(c).GetAttribute("aria-invalid")));

        await pantalla.Find("#cancelar-red").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#confirmacion-red"));
        Assert.Empty(pantalla.FindAll("#aviso-red"));
        Assert.Equal(("192.168.10.30", "255.255.255.0", "192.168.10.1", "192.168.10.2"),
            (Valor(pantalla, "#ipv4-direccion"), Valor(pantalla, "#ipv4-mascara"), Valor(pantalla, "#ipv4-puerta"), Valor(pantalla, "#ipv4-dns1")));
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED");
    }

    [Fact(DisplayName = "conectividad-puesto · Sin EjecutorNetsh, como en las pruebas: confirmar dice que la edición de red no está disponible, no corre nada y no registra nada")]
    public async Task Sin_ejecutor()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirRed(puesto);
        await CargarFija(pantalla);
        await pantalla.Find("#aplicar-red").ClickAsync(new());

        await pantalla.Find("#confirmar-red").ClickAsync(new());

        Assert.Equal(MensajesAtencion.RedNoDisponible, pantalla.Find("#aviso-red").TextContent.Trim());
        Assert.Empty(pantalla.FindAll("#confirmacion-red"));
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED");
    }

    internal static async Task<IRenderedComponent<PantallaAtencion>> AbrirRed(PuestoDePrueba puesto)
    {
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#pestana-red").ClickAsync(new());
        return pantalla;
    }

    /// <summary>El primer adaptador de la lista, IPv4 fija y los valores del escenario «IPv4 fija con confirmación».</summary>
    private static async Task CargarFija(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await pantalla.FindAll("#seccion-red .adaptador input")[0].ChangeAsync(new ChangeEventArgs { Value = "on" });
        await pantalla.Find("#red-fija").ChangeAsync(new ChangeEventArgs { Value = "on" });
        foreach (var (campo, valor) in Campos.Zip(new[] { "192.168.10.30", "255.255.255.0", "192.168.10.1", "192.168.10.2", "" }))
        {
            await pantalla.Find(campo).InputAsync(new ChangeEventArgs { Value = valor });
        }
    }

    private static string? Valor(IRenderedComponent<PantallaAtencion> pantalla, string campo) => pantalla.Find(campo).GetAttribute("value");
}
