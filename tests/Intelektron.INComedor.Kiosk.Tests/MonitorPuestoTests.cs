using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El monitor del puesto (presentacion-puesto de configurar-puesto-por-pestanas, D10): la elección es una función pura sobre
/// la lista de monitores y lo guardado, que se prueba con datos; la pestaña lista los monitores reales del equipo, sin
/// dobles, guarda en pantalla.json y pide mover la ventana por un mediador que en bUnit sólo cuenta.
/// </summary>
public class MonitorPuestoTests
{
    private static readonly MonitorDelPuesto Principal = new(@"\\?\DISPLAY#AAA0001#principal", @"\\.\DISPLAY1", new RectanguloPantalla(0, 0, 1920, 1080), true);
    private static readonly MonitorDelPuesto Segundo = new(@"\\?\DISPLAY#BBB0002#segundo", @"\\.\DISPLAY2", new RectanguloPantalla(1920, 0, 3200, 1024), false);

    [Fact(DisplayName = "presentacion-puesto · Volver a abrir en el monitor guardado: con dos monitores y el segundo guardado, la elección es el segundo, sin aviso")]
    public void El_guardado_presente()
    {
        var eleccion = Monitores.ElegirMonitor([Principal, Segundo], new LecturaPantalla(new PantallaGuardada(Segundo.Id, Segundo.Dispositivo), false));

        Assert.Equal(Segundo, eleccion.Monitor);
        Assert.Null(eleccion.Aviso);
    }

    [Fact(DisplayName = "presentacion-puesto · El monitor guardado no está: la elección es el principal y avisa que el guardado no está conectado")]
    public void El_guardado_ausente()
    {
        var eleccion = Monitores.ElegirMonitor([Principal], new LecturaPantalla(new PantallaGuardada(Segundo.Id, Segundo.Dispositivo), false));

        Assert.Equal(Principal, eleccion.Monitor);
        Assert.Equal(MensajesAtencion.MonitorAusente, eleccion.Aviso);
    }

    [Fact(DisplayName = "presentacion-puesto · Un solo monitor: sin nada guardado, la elección es ese monitor, el principal, sin aviso")]
    public void Un_solo_monitor()
    {
        var eleccion = Monitores.ElegirMonitor([Principal], new LecturaPantalla(null, false));

        Assert.Equal(Principal, eleccion.Monitor);
        Assert.Null(eleccion.Aviso);
    }

    [Fact(DisplayName = "presentacion-puesto · Lo guardado ilegible: la elección es el principal y avisa")]
    public void Lo_guardado_ilegible()
    {
        var eleccion = Monitores.ElegirMonitor([Segundo, Principal], new LecturaPantalla(null, true));

        Assert.Equal(Principal, eleccion.Monitor);
        Assert.Equal(MensajesAtencion.MonitorIlegible, eleccion.Aviso);
    }

    [Fact(DisplayName = "presentacion-puesto · pantalla.json: lo guardado se vuelve a leer, un archivo ilegible se marca y sin archivo no hay nada guardado")]
    public void El_almacen_de_la_pantalla()
    {
        using var puesto = new PuestoDePrueba();
        var almacen = new AlmacenPantalla(puesto.Carpeta);
        Assert.Equal(new LecturaPantalla(null, false), almacen.Leer());

        almacen.Guardar(Segundo);
        Assert.Equal(new LecturaPantalla(new PantallaGuardada(Segundo.Id, Segundo.Dispositivo), false), new AlmacenPantalla(puesto.Carpeta).Leer());

        File.WriteAllText(Path.Combine(puesto.Carpeta, "pantalla.json"), "{no es json");
        Assert.Equal(new LecturaPantalla(null, true), new AlmacenPantalla(puesto.Carpeta).Leer());
    }

    [Fact(DisplayName = "presentacion-puesto · La pestaña Pantalla lista los monitores reales del equipo con su resolución y el principal, guarda el elegido, mueve la ventana y deja CONFIGURATION_CHANGED con DISPLAY_MONITOR y MAINTENANCE_MODE")]
    public async Task La_pestana_pantalla()
    {
        using var puesto = new PuestoDePrueba();
        var reales = Monitores.Leer();
        Assert.NotEmpty(reales);
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#pestana-pantalla").ClickAsync(new());

        var items = pantalla.FindAll("#seccion-pantalla .monitor");
        Assert.Equal(reales.Count, items.Count);
        var principal = reales.Single(m => m.Principal);
        var textoPrincipal = items[reales.ToList().IndexOf(principal)].TextContent;
        Assert.Contains($"{principal.Area.Ancho} × {principal.Area.Alto}", textoPrincipal);
        Assert.Contains("Principal", textoPrincipal);

        await pantalla.Find("#guardar-monitor").ClickAsync(new());

        Assert.Equal(principal.Id, new AlmacenPantalla(puesto.Carpeta).Leer().Guardada!.Id);
        Assert.Equal([principal], puesto.Movimientos);
        Assert.Equal(MensajesAtencion.MonitorGuardado, pantalla.Find("#aviso-guardado").TextContent.Trim());
        var cambio = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"));
        Assert.Equal(("DISPLAY_MONITOR", "APPLIED", "MAINTENANCE_MODE"), (cambio.GetProperty("Setting").GetString(), cambio.GetProperty("Result").GetString(), cambio.GetProperty("Access").GetString()));
        Assert.True(SinActor(cambio));
    }

    [Fact(DisplayName = "configuracion-puesto · El monitor guardado: con otro monitor guardado antes, elegir el principal real y guardarlo deja CONFIGURATION_CHANGED con el monitor de antes y el de después, APPLIED y MAINTENANCE_MODE")]
    public async Task El_monitor_guardado_en_la_bitacora()
    {
        using var puesto = new PuestoDePrueba();
        new AlmacenPantalla(puesto.Carpeta).Guardar(Segundo);
        var principal = Monitores.Leer().Single(m => m.Principal);
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#pestana-pantalla").ClickAsync(new());

        await pantalla.Find("#guardar-monitor").ClickAsync(new());

        var cambio = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"));
        Assert.Equal("DISPLAY_MONITOR", cambio.GetProperty("Setting").GetString());
        Assert.Equal(Segundo.Dispositivo, cambio.GetProperty("Before").GetProperty("Device").GetString());
        Assert.Equal(principal.Dispositivo, cambio.GetProperty("After").GetProperty("Device").GetString());
        Assert.Equal(("APPLIED", "MAINTENANCE_MODE"), (cambio.GetProperty("Result").GetString(), cambio.GetProperty("Access").GetString()));
        Assert.True(SinActor(cambio));
    }

    [Fact(DisplayName = "presentacion-puesto · Con el monitor guardado ausente al abrir, la pestaña Pantalla dice que no está conectado")]
    public async Task La_pestana_avisa_el_monitor_ausente()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Pantalla.Aviso = MensajesAtencion.MonitorAusente;
        var pantalla = puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());

        await pantalla.Find("#pestana-pantalla").ClickAsync(new());

        Assert.Equal(MensajesAtencion.MonitorAusente, pantalla.Find("#aviso-monitor").TextContent.Trim());
    }
}
