using AngleSharp.Dom;
using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Los operadores en Personas (administracion-local de marcar-operadores-en-personas, D3): la columna «Operador», después de
/// «Estado», con un escudo y el texto accesible «Operador» en quien tiene OPERATOR vigente, ordenable; en edición, una celda
/// que se marca o se desmarca, deshabilitada en la propia fila de un operador y en una persona de baja. Sobre la base de la
/// demostración: 9001 vigente, 9002 vencido, 9003 revocado y 9004 sólo administrador.
/// </summary>
public class OperadoresEnPersonasPantallaTests
{
    private static readonly string[] Codigos = ["0001", "0002", "0003", "0004", "9001", "9002", "9003", "9004"];

    [Fact(DisplayName = "administracion-local · Ver quién es operador: sólo la fila de «Operador sintético 01» tiene el escudo, con el texto accesible «Operador», y ninguna fila muestra un código")]
    public async Task Ver_quien_es_operador()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirPersonas(puesto);

        Assert.Equal(["Nombre", "Estado", "Operador"], pantalla.FindAll("#tabla-personas thead th").Take(3).Select(th => th.TextContent.Trim()));
        var conEscudo = pantalla.FindAll("tr.persona").Where(f => f.QuerySelector("td.operador-persona [role=\"img\"]") is not null).ToList();
        Assert.Equal("Operador sintético 01", Nombre(Assert.Single(conEscudo)));
        Assert.Equal("Operador", conEscudo[0].QuerySelector("td.operador-persona [role=\"img\"]")!.GetAttribute("aria-label"));
        var tabla = pantalla.Find("#tabla-personas");
        Assert.All(Codigos, c => Assert.DoesNotContain(c, tabla.TextContent, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "administracion-local · Marcar a una persona como operador: con la clave del día, marcar «Operador» en «Persona sintética 01» y guardar le da el permiso, y en reposo, con 0001, el engranaje abre Configuración como operador")]
    public async Task Marcar_a_una_persona()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirPersonas(puesto);
        await pantalla.Find("#abrir-personas").ClickAsync(new());

        var celda = Fila(pantalla, "Persona sintética 01").QuerySelector("button.operador-celda")!;
        Assert.Equal("false", celda.GetAttribute("aria-pressed"));
        await celda.ClickAsync(new());
        Assert.Equal("true", Fila(pantalla, "Persona sintética 01").QuerySelector("button.operador-celda")!.GetAttribute("aria-pressed"));
        await pantalla.Find("#guardar-panel").ClickAsync(new());
        Assert.NotNull(Fila(pantalla, "Persona sintética 01").QuerySelector("td.operador-persona [role=\"img\"]"));
        var cambio = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"
            && ContextoDe(l).GetProperty("Setting").GetString() == "PERSON"));
        Assert.False(cambio.GetProperty("Before").GetProperty("Operator").GetBoolean());
        Assert.True(cambio.GetProperty("After").GetProperty("Operator").GetBoolean());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        await EntradaConfiguracion.Entrar(pantalla, "0001");

        Assert.Equal(["pestana-consumos", "pestana-personas", "pestana-impresora"], pantalla.FindAll("[role=\"tab\"]").Select(t => t.Id).ToArray());
    }

    [Fact(DisplayName = "administracion-local · Desmarcar a un operador: con la clave del día, desmarcar a «Operador sintético 01» y guardar revoca su permiso, y el engranaje con 9001 dice que el permiso está revocado")]
    public async Task Desmarcar_a_un_operador()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirPersonas(puesto);
        await pantalla.Find("#abrir-personas").ClickAsync(new());

        await Fila(pantalla, "Operador sintético 01").QuerySelector("button.operador-celda")!.ClickAsync(new());
        await pantalla.Find("#guardar-panel").ClickAsync(new());
        Assert.Null(Fila(pantalla, "Operador sintético 01").QuerySelector("td.operador-persona [role=\"img\"]"));
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        await EntradaConfiguracion.Entrar(pantalla, "9001");

        Assert.Empty(pantalla.FindAll("[role=\"tab\"]"));
        Assert.Contains(MensajesAtencion.DeMotivo("PERMISSION_REVOKED"), pantalla.Find("#dialogo-clave").TextContent, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "administracion-local · Un operador marca a otro, no a sí mismo: con 9001, su propia celda está deshabilitada con «No podés cambiar tu propio permiso.» y marcar a «Persona sintética 01» se guarda con él como actor")]
    public async Task Un_operador_marca_a_otro()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla, "9001");
        await pantalla.Find("#pestana-personas").ClickAsync(new());
        await pantalla.Find("#abrir-personas").ClickAsync(new());

        var propia = Fila(pantalla, "Operador sintético 01").QuerySelector("button.operador-celda")!;
        Assert.True(propia.HasAttribute("disabled"));
        Assert.Equal(MensajesAtencion.PermisoPropio, propia.GetAttribute("title"));
        await Fila(pantalla, "Persona sintética 01").QuerySelector("button.operador-celda")!.ClickAsync(new());
        await pantalla.Find("#guardar-panel").ClickAsync(new());

        var cambio = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"
            && ContextoDe(l).GetProperty("Setting").GetString() == "PERSON"));
        Assert.Equal("APPLIED", cambio.GetProperty("Result").GetString());
        Assert.Equal(Guid.Parse("00000000-0000-4000-8000-000000000037"), cambio.GetProperty("ActorPersonId").GetGuid());
    }

    [Fact(DisplayName = "administracion-local · Una persona de baja: en edición, la celda «Operador» de una persona dada de baja no se puede marcar")]
    public async Task Una_persona_de_baja()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirPersonas(puesto);
        await pantalla.Find("#abrir-personas").ClickAsync(new());
        await Fila(pantalla, "Persona sintética 03").QuerySelector("button.quitar-persona")!.ClickAsync(new());
        Assert.True(Fila(pantalla, "Persona sintética 03").QuerySelector("button.operador-celda")!.HasAttribute("disabled"));
        await pantalla.Find("#guardar-panel").ClickAsync(new());

        await pantalla.Find("#abrir-personas").ClickAsync(new());

        Assert.True(Fila(pantalla, "Persona sintética 03").QuerySelector("button.operador-celda")!.HasAttribute("disabled"));
    }

    [Fact(DisplayName = "administracion-local · Ordenar por «Operador»: tocar su encabezado lo marca ascendente con los operadores primero, y otro toque, descendente")]
    public async Task Ordenar_por_operador()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirPersonas(puesto);

        await pantalla.Find("#tabla-personas th[data-columna=\"Operador\"] button.ordenar").ClickAsync(new());

        Assert.Equal("ascending", pantalla.Find("#tabla-personas th[data-columna=\"Operador\"]").GetAttribute("aria-sort"));
        Assert.Equal("Operador sintético 01", Nombre(pantalla.FindAll("tr.persona")[0]));
        await pantalla.Find("#tabla-personas th[data-columna=\"Operador\"] button.ordenar").ClickAsync(new());
        Assert.Equal("descending", pantalla.Find("#tabla-personas th[data-columna=\"Operador\"]").GetAttribute("aria-sort"));
        Assert.Equal("Operador sintético 01", Nombre(pantalla.FindAll("tr.persona")[^1]));
    }

    [Fact(DisplayName = "administracion-local · Dar y quitar por planilla en pantalla: el resumen dice «1 permiso de operador dado y 1 quitado.» y PEOPLE_IMPORTED tiene OperatorGranted 1 y OperatorRevoked 1")]
    public async Task Dar_y_quitar_por_planilla()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        var carpeta = Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "planillas")).FullName;
        var nombre = EscritorPlanilla.EscribirLibro(carpeta, "operadores",
            [new HojaPlanilla("Personas", ["Nombre", "Código", "Operador"],
                [new object[] { "Persona sintética 01", "0001", "Sí" }, new object[] { "Operador sintético 01", "9001", "No" }], [1])]);
        puesto.Contexto.Services.AddSingleton<IElectorArchivo>(new ElectorArchivoFijo(Path.Combine(carpeta, nombre)));
        var pantalla = await AbrirPersonas(puesto);

        await pantalla.Find("#importar-personas").ClickAsync(new());

        Assert.EndsWith(" 1 permiso de operador dado y 1 quitado.", pantalla.Find("#importacion-totales").TextContent.Trim(), StringComparison.Ordinal);
        await pantalla.FindAll("#resumen-importacion .acciones button").Single(b => b.TextContent.Trim() == "Modificar y agregar").ClickAsync(new());
        var importacion = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "PEOPLE_IMPORTED"));
        Assert.Equal((1, 1), (importacion.GetProperty("OperatorGranted").GetInt32(), importacion.GetProperty("OperatorRevoked").GetInt32()));
    }

    [Fact(DisplayName = "administracion-local · Un operador que se quita a sí mismo por planilla: con 9001, su fila en «No» se señala con «no podés cambiar tu propio permiso de operador.» y no deja aplicar")]
    public async Task Un_operador_no_se_quita_por_planilla_en_pantalla()
    {
        using var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        var carpeta = Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "planillas")).FullName;
        var nombre = EscritorPlanilla.EscribirLibro(carpeta, "propio",
            [new HojaPlanilla("Personas", ["Nombre", "Código", "Operador"], [new object[] { "Operador sintético 01", "9001", "No" }], [1])]);
        puesto.Contexto.Services.AddSingleton<IElectorArchivo>(new ElectorArchivoFijo(Path.Combine(carpeta, nombre)));
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla, "9001");
        await pantalla.Find("#pestana-personas").ClickAsync(new());

        await pantalla.Find("#importar-personas").ClickAsync(new());

        Assert.Contains("Fila 2: no podés cambiar tu propio permiso de operador.", pantalla.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(pantalla.FindAll("button"), b => b.TextContent.Trim() == "Modificar y agregar");
    }

    [Fact(DisplayName = "administracion-local · Una persona de baja: marcar «Operador» y después «Quitar» le devuelve el permiso que tenía, y el guardado da de baja sin rechazo")]
    public async Task Quitar_devuelve_el_permiso()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = await AbrirPersonas(puesto);
        await pantalla.Find("#abrir-personas").ClickAsync(new());

        await Fila(pantalla, "Persona sintética 03").QuerySelector("button.operador-celda")!.ClickAsync(new());
        await Fila(pantalla, "Persona sintética 03").QuerySelector("button.quitar-persona")!.ClickAsync(new());

        Assert.Equal("false", Fila(pantalla, "Persona sintética 03").QuerySelector("button.operador-celda")!.GetAttribute("aria-pressed"));
        await pantalla.Find("#guardar-panel").ClickAsync(new());
        Assert.Equal("De baja", Fila(pantalla, "Persona sintética 03").QuerySelector(".estado-persona")!.TextContent.Trim());
    }

    private static async Task<IRenderedComponent<PantallaAtencion>> AbrirPersonas(PuestoDePrueba puesto)
    {
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-personas").ClickAsync(new());
        return pantalla;
    }

    private static IElement Fila(IRenderedComponent<PantallaAtencion> pantalla, string nombre) =>
        pantalla.FindAll("tr.persona").Single(f => Nombre(f) == nombre);

    private static string Nombre(IElement fila) =>
        fila.QuerySelector("input.nombre-persona")?.GetAttribute("value") ?? fila.QuerySelector(".nombre-persona")!.TextContent.Trim();

    private sealed class ElectorArchivoFijo(string ruta) : IElectorArchivo
    {
        public string? Elegir(string? carpeta) => ruta;
    }
}
