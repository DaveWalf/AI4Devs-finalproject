using AngleSharp.Dom;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// «Personas habilitadas» en Configuración (administracion-local de administrar-personas-y-opciones): bUnit sobre un
/// <see cref="ServicioAtencion"/> real con la base de la demostración y el catálogo de nueve en una carpeta temporal.
/// </summary>
public class PanelPersonasTests : IDisposable
{
    private static readonly string[] CodigosSinteticos = ["0001", "0002", "0003", "0004", "9001", "9002", "9003", "9004"];

    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
    }

    [Fact(DisplayName = "administracion-local · La persona del código 0001: «Persona sintética 01» activa, con ALMUERZO límite 1, REFRIGERIO límite 2 y las nueve opciones habilitadas")]
    public async Task La_persona_del_codigo_0001()
    {
        var pantalla = await AbrirPersonas();

        var persona = Persona(pantalla, "Persona sintética 01");

        Assert.Equal("Activa", persona.QuerySelector(".estado-persona")!.TextContent.Trim());
        var cupos = persona.QuerySelectorAll(".cupo")
            .Select(c => (c.QuerySelector(".nombre-cupo")!.TextContent.Trim(), c.QuerySelector("input.limite")!.GetAttribute("value")))
            .ToList();
        Assert.Equal([("ALMUERZO", "1"), ("REFRIGERIO", "2")], cupos);
        Assert.Equal("9", persona.QuerySelector(".habilitadas")!.GetAttribute("data-cantidad"));
    }

    [Fact(DisplayName = "administracion-local · Ningún código en el panel: con la edición de cada persona abierta, ni el texto ni los valores de los campos contienen los códigos de 0001 a 0004 ni de 9001 a 9004")]
    public async Task Ningun_codigo_en_el_panel()
    {
        var pantalla = await AbrirPersonas();

        var panel = pantalla.Find("#panel-personas");
        Assert.NotEmpty(panel.QuerySelectorAll("input.nombre-persona"));
        var valores = panel.QuerySelectorAll("input, select, textarea, option").Select(c => c.GetAttribute("value") ?? "").ToList();
        foreach (var codigo in CodigosSinteticos)
        {
            Assert.DoesNotContain(codigo, panel.TextContent);
            Assert.DoesNotContain(valores, v => v.Contains(codigo, StringComparison.Ordinal));
        }

        Assert.All(panel.QuerySelectorAll("*").SelectMany(e => e.Attributes), a => Assert.DoesNotContain(CodigosSinteticos, c => a.Value.Contains(c, StringComparison.Ordinal)));
    }

    [Fact(DisplayName = "administracion-local · Persona sin cupos: «Persona sintética 02», activa y sin reglas, figura con «Sin cupos» y ninguna opción habilitada")]
    public async Task Persona_sin_cupos()
    {
        var pantalla = await AbrirPersonas();

        var persona = Persona(pantalla, "Persona sintética 02");

        Assert.Equal("Sin cupos", persona.QuerySelector(".sin-cupos")!.TextContent.Trim());
        Assert.Empty(persona.QuerySelectorAll(".cupo"));
        Assert.Equal("0", persona.QuerySelector(".habilitadas")!.GetAttribute("data-cantidad"));
    }

    [Fact(DisplayName = "administracion-local · Versión activa de INWeb: el panel lista las personas sin «Guardar» ni controles de edición y dice «Estos datos los administra INWeb»")]
    public async Task Version_activa_de_INWeb()
    {
        var pantalla = Dibujar();
        MarcarINWeb();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#abrir-personas").ClickAsync(new());

        var panel = pantalla.Find("#panel-personas");
        Assert.Equal("Estos datos los administra INWeb", panel.QuerySelector("#aviso-inweb")!.TextContent.Trim());
        Assert.Contains("Persona sintética 01", panel.TextContent);
        Assert.Empty(panel.QuerySelectorAll("#guardar-panel"));
        Assert.Empty(panel.QuerySelectorAll("input, select, textarea"));
        Assert.Equal(["cancelar-panel"], panel.QuerySelectorAll("button").Select(b => b.Id));
    }

    [Theory(DisplayName = "administracion-local · Cancelar o Escape con un cupo cambiado: el panel se cierra y no hay versión nueva")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancelar_o_escape_no_crean_version(bool conEscape)
    {
        var pantalla = await AbrirPersonas();
        await Persona(pantalla, "Persona sintética 01").QuerySelector("input.limite")!.InputAsync(new ChangeEventArgs { Value = "3" });

        if (conEscape)
        {
            await pantalla.Find("[role=\"dialog\"]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        }
        else
        {
            await pantalla.Find("#cancelar-panel").ClickAsync(new());
        }

        Assert.Empty(pantalla.FindAll("#panel-personas"));
        Assert.Equal(1L, Versiones());
    }

    [Fact(DisplayName = "administracion-local · Un guardado inválido: una persona nueva sin nombre queda con su campo señalado, el aviso, y sin versión 2")]
    public async Task Campo_invalido_senalado()
    {
        var pantalla = await AbrirPersonas();
        await pantalla.Find("#agregar-persona").ClickAsync(new());
        var nueva = pantalla.FindAll("article.persona").Last();
        await nueva.QuerySelector("input.codigo-persona")!.InputAsync(new ChangeEventArgs { Value = "0105" });

        await pantalla.Find("#guardar-panel").ClickAsync(new());

        var nombre = pantalla.FindAll("article.persona").Last().QuerySelector("input.nombre-persona")!;
        Assert.Equal("true", nombre.GetAttribute("aria-invalid"));
        Assert.Equal("Revisá los campos marcados", pantalla.Find("#aviso-panel").TextContent.Trim());
        Assert.Equal(1L, Versiones());
    }

    [Fact(DisplayName = "administracion-local · El campo de código es de sólo dígitos, enmascarado y sin autocompletado, y no vuelve a mostrar lo tipeado")]
    public async Task Campo_de_codigo_enmascarado()
    {
        var pantalla = await AbrirPersonas();
        await pantalla.Find("#agregar-persona").ClickAsync(new());
        var campo = pantalla.FindAll("article.persona").Last().QuerySelector("input.codigo-persona")!;

        await campo.InputAsync(new ChangeEventArgs { Value = "0105" });

        campo = pantalla.FindAll("article.persona").Last().QuerySelector("input.codigo-persona")!;
        Assert.Equal(("password", "numeric", "off", "[0-9]*"), (campo.GetAttribute("type"), campo.GetAttribute("inputmode"), campo.GetAttribute("autocomplete"), campo.GetAttribute("pattern")));
        Assert.DoesNotContain("0105", pantalla.Find("#panel-personas").OuterHtml);
    }

    [Fact(DisplayName = "administracion-local · Agregar «Persona sintética 05» con 0105 y ALMUERZO límite 1 desde el panel: se guarda la versión 2 y, en reposo, 0105 abre su sesión")]
    public async Task Agregar_una_persona_desde_el_panel()
    {
        var pantalla = await AbrirPersonas();
        await pantalla.Find("#agregar-persona").ClickAsync(new());
        var nueva = pantalla.FindAll("article.persona").Last();
        await nueva.QuerySelector("input.nombre-persona")!.InputAsync(new ChangeEventArgs { Value = "Persona sintética 05" });
        await pantalla.FindAll("article.persona").Last().QuerySelector("input.codigo-persona")!.InputAsync(new ChangeEventArgs { Value = "0105" });
        var selector = pantalla.FindAll("article.persona").Last().QuerySelector("select.nuevo-cupo")!;
        var almuerzo = selector.QuerySelectorAll("option").Single(o => o.TextContent.Trim() == "ALMUERZO").GetAttribute("value");
        await selector.ChangeAsync(new ChangeEventArgs { Value = almuerzo });
        await pantalla.FindAll("article.persona").Last().QuerySelector("button.agregar-cupo")!.ClickAsync(new());

        await pantalla.Find("#guardar-panel").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#panel-personas"));
        Assert.Equal(2L, Versiones());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0105" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        Assert.Contains("Persona sintética 05", pantalla.Find("#saludo").TextContent);
        Assert.All(pantalla.FindAll(".opcion .nombre"), n => Assert.Contains("Almuerzo", n.TextContent));
    }

    private async Task<IRenderedComponent<PantallaAtencion>> AbrirPersonas()
    {
        var pantalla = Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#abrir-personas").ClickAsync(new());
        return pantalla;
    }

    private static IElement Persona(IRenderedComponent<PantallaAtencion> pantalla, string nombre) =>
        pantalla.FindAll("article.persona").Single(a => a.QuerySelector("input.nombre-persona")?.GetAttribute("value") == nombre
            || a.QuerySelector(".nombre-persona")?.TextContent.Trim() == nombre);

    private IRenderedComponent<PantallaAtencion> Dibujar()
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: true);
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        return _contexto.Render<PantallaAtencion>();
    }

    private void MarcarINWeb()
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "INSERT INTO tbComedorOrigenesVersiones (comvcCodigo, comvoOrigen) SELECT comvcCodigoActiva, 'INWEB' FROM tbComedorConfiguracionPuestos";
        comando.ExecuteNonQuery();
    }

    private long Versiones()
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM tbComedorVersionesConfiguracion";
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private SqliteConnection Abrir()
    {
        var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
        conexion.Open();
        return conexion;
    }
}
