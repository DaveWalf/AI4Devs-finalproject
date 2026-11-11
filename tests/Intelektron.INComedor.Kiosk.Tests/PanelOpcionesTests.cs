using AngleSharp.Dom;
using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// «Opciones del comedor» en Configuración (administracion-local de administrar-personas-y-opciones): bUnit sobre un
/// <see cref="ServicioAtencion"/> real con la base de la demostración y el catálogo de nueve en una carpeta temporal.
/// </summary>
public class PanelOpcionesTests : IDisposable
{
    private const string Opcion = "Almuerzo opcional";

    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
    }

    [Fact(DisplayName = "administracion-local · Las opciones con todos sus parámetros: nombre, servicio, importe y cargas de cada una de las nueve")]
    public async Task Las_opciones_con_sus_parametros()
    {
        var pantalla = await AbrirOpciones();

        Assert.Equal(9, pantalla.FindAll("article.opcion-admin").Count);
        var opcion = OpcionAdmin(pantalla, Opcion);
        Assert.Equal("1800,00", opcion.QuerySelector("input.importe-opcion")!.GetAttribute("value"));
        Assert.Equal("ALMUERZO", opcion.QuerySelector("select.servicio-opcion option[selected]")!.TextContent.Trim());
        var carga = Assert.Single(opcion.QuerySelectorAll(".carga"));
        Assert.Equal("ALMUERZO", carga.QuerySelector("select.categoria-carga option[selected]")!.TextContent.Trim());
        Assert.Equal("1", carga.QuerySelector("input.unidades-carga")!.GetAttribute("value"));
        Assert.Equal("Todo el día", opcion.QuerySelector(".sin-franjas")!.TextContent.Trim());
    }

    [Fact(DisplayName = "administracion-local · Cargar la franja del almuerzo desde el panel: al volver a abrirlo, la opción figura con «11:00 a 14:00» y conserva su versión de menú")]
    public async Task La_franja_vista_como_11_a_14()
    {
        var pantalla = await AbrirOpciones();
        await OpcionAdmin(pantalla, Opcion).QuerySelector("button.agregar-franja")!.ClickAsync(new());
        await OpcionAdmin(pantalla, Opcion).QuerySelector("input.inicio-franja")!.ChangeAsync(new ChangeEventArgs { Value = "11:00" });
        await OpcionAdmin(pantalla, Opcion).QuerySelector("input.fin-franja")!.ChangeAsync(new ChangeEventArgs { Value = "14:00" });

        await pantalla.Find("#guardar-panel").ClickAsync(new());
        Assert.Empty(pantalla.FindAll("#panel-opciones"));
        await pantalla.Find("#abrir-opciones").ClickAsync(new());

        var franja = Assert.Single(OpcionAdmin(pantalla, Opcion).QuerySelectorAll(".franja-horaria"));
        Assert.Equal("11:00 a 14:00", franja.QuerySelector(".franja-texto")!.TextContent.Trim());
        Assert.Equal(2L, Escalar("SELECT COUNT(*) FROM tbComedorVersionesConfiguracion"));
        Assert.Equal(1L, Escalar("SELECT commenVersionMenu FROM tbComedorOpcionesMenu WHERE comvcCodigo = (SELECT comvcCodigoActiva FROM tbComedorConfiguracionPuestos) AND commenNombre = 'Almuerzo opcional'"));
    }

    [Fact(DisplayName = "administracion-local · El campo de hora del WebView2 entrega «11:00:00»: el panel lo lleva a 11:00 y la franja se guarda y se ve como «11:00 a 14:00»")]
    public async Task La_hora_con_segundos_se_lleva_a_horas_y_minutos()
    {
        var pantalla = await AbrirOpciones();
        await OpcionAdmin(pantalla, Opcion).QuerySelector("button.agregar-franja")!.ClickAsync(new());
        await OpcionAdmin(pantalla, Opcion).QuerySelector("input.inicio-franja")!.ChangeAsync(new ChangeEventArgs { Value = "11:00:00" });
        await OpcionAdmin(pantalla, Opcion).QuerySelector("input.fin-franja")!.ChangeAsync(new ChangeEventArgs { Value = "14:00:00" });

        await pantalla.Find("#guardar-panel").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#panel-opciones"));
        await pantalla.Find("#abrir-opciones").ClickAsync(new());
        Assert.Equal("11:00 a 14:00", OpcionAdmin(pantalla, Opcion).QuerySelector(".franja-texto")!.TextContent.Trim());
    }

    [Theory(DisplayName = "administracion-local · Cancelar o Escape con un importe cambiado: el panel se cierra, la versión activa es la misma y el catálogo ofrecido no cambió")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancelar_o_escape_no_crean_version(bool conEscape)
    {
        var pantalla = await AbrirOpciones();
        var catalogo = new ConsultaCatalogo(BaseComedor.Abrir(_archivo.Ruta)).Leer();
        await OpcionAdmin(pantalla, Opcion).QuerySelector("input.importe-opcion")!.ChangeAsync(new ChangeEventArgs { Value = "2500,00" });

        if (conEscape)
        {
            await pantalla.Find("[role=\"dialog\"]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        }
        else
        {
            await pantalla.Find("#cancelar-panel").ClickAsync(new());
        }

        Assert.Empty(pantalla.FindAll("#panel-opciones"));
        Assert.Equal(1L, Escalar("SELECT COUNT(*) FROM tbComedorVersionesConfiguracion"));
        Assert.Equal(catalogo, new ConsultaCatalogo(BaseComedor.Abrir(_archivo.Ruta)).Leer());
    }

    [Fact(DisplayName = "administracion-local · Una franja de 12:00 a 12:00 queda señalada con el aviso y sin versión nueva")]
    public async Task Franja_vacia_senalada()
    {
        var pantalla = await AbrirOpciones();
        await OpcionAdmin(pantalla, Opcion).QuerySelector("button.agregar-franja")!.ClickAsync(new());
        await OpcionAdmin(pantalla, Opcion).QuerySelector("input.inicio-franja")!.ChangeAsync(new ChangeEventArgs { Value = "12:00" });
        await OpcionAdmin(pantalla, Opcion).QuerySelector("input.fin-franja")!.ChangeAsync(new ChangeEventArgs { Value = "12:00" });

        await pantalla.Find("#guardar-panel").ClickAsync(new());

        Assert.Equal("true", OpcionAdmin(pantalla, Opcion).QuerySelector(".franja-horaria")!.GetAttribute("aria-invalid"));
        Assert.Equal("Revisá los campos marcados.", pantalla.Find("#aviso-panel").TextContent.Trim());
        Assert.Equal(1L, Escalar("SELECT COUNT(*) FROM tbComedorVersionesConfiguracion"));
    }

    [Fact(DisplayName = "administracion-local · Versión activa de INWeb: las opciones se ven sin «Guardar» ni controles de edición y con «Estos datos los administra INWeb»")]
    public async Task Version_activa_de_INWeb()
    {
        var pantalla = Dibujar();
        Ejecutar("INSERT INTO tbComedorOrigenesVersiones (comvcCodigo, comvoOrigen) SELECT comvcCodigoActiva, 'INWEB' FROM tbComedorConfiguracionPuestos");
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#abrir-opciones").ClickAsync(new());

        var panel = pantalla.Find("#panel-opciones");
        Assert.Equal("Estos datos los administra INWeb.", panel.QuerySelector("#aviso-inweb")!.TextContent.Trim());
        Assert.Contains(Opcion, panel.TextContent);
        Assert.Empty(panel.QuerySelectorAll("#guardar-panel"));
        Assert.Empty(panel.QuerySelectorAll("input, select, textarea"));
    }

    [Fact(DisplayName = "administracion-local · Con INWeb, el estilo visual sigue editable: el diálogo conserva sus rangos y su «Guardar»")]
    public async Task Con_INWeb_el_estilo_sigue_editable()
    {
        var pantalla = Dibujar();
        Ejecutar("INSERT INTO tbComedorOrigenesVersiones (comvcCodigo, comvoOrigen) SELECT comvcCodigoActiva, 'INWEB' FROM tbComedorConfiguracionPuestos");

        await EntradaConfiguracion.Entrar(pantalla);

        Assert.NotNull(pantalla.Find("#opacidad"));
        Assert.NotNull(pantalla.Find("#guardar-configuracion"));
    }

    private async Task<IRenderedComponent<PantallaAtencion>> AbrirOpciones()
    {
        var pantalla = Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#abrir-opciones").ClickAsync(new());
        return pantalla;
    }

    private static IElement OpcionAdmin(IRenderedComponent<PantallaAtencion> pantalla, string nombre) =>
        pantalla.FindAll("article.opcion-admin").Single(a => a.QuerySelector("input.nombre-opcion")?.GetAttribute("value") == nombre
            || a.QuerySelector(".nombre-opcion")?.TextContent.Trim() == nombre);

    private IRenderedComponent<PantallaAtencion> Dibujar()
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: true);
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        return _contexto.Render<PantallaAtencion>();
    }

    private void Ejecutar(string sql)
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        comando.ExecuteNonQuery();
    }

    private long Escalar(string sql)
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private SqliteConnection Abrir()
    {
        var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
        conexion.Open();
        return conexion;
    }
}
