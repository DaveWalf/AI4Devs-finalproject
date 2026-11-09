using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Configuración por pestañas (configuracion-puesto de configurar-puesto-por-pestanas): las nueve pestañas con el patrón de
/// WAI-ARIA, todas montadas y la elegida visible (D1, D2), lo editado que se conserva hasta cerrar y cada «Guardar» que
/// guarda lo suyo y deja Configuración abierta (D3). bUnit sobre un <see cref="ServicioAtencion"/> real.
/// </summary>
public class PestanasConfiguracionTests : IDisposable
{
    private static readonly string[] Pestanas =
        ["General", "Personas", "Opciones", "Consumos", "Comprobantes", "Identidad visual", "Impresora", "Pantalla", "Red"];

    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "incomedor-tests", "pestanas-" + Guid.NewGuid().ToString("N"));
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");
    private ServicioAtencion _servicio = null!;

    public PestanasConfiguracionTests()
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

    [Fact(DisplayName = "configuracion-puesto · Abre en General con las nueve pestañas, en orden, y General muestra el modo y el estado de las bitácoras")]
    public async Task Abre_en_general_con_las_nueve_pestanas()
    {
        var pantalla = Dibujar();

        await pantalla.Find(".engranaje").ClickAsync(new());

        var lista = pantalla.Find("[role=\"tablist\"]");
        var pestanas = lista.QuerySelectorAll("[role=\"tab\"]");
        Assert.Equal(Pestanas, pestanas.Select(p => p.TextContent.Trim()));
        Assert.Equal(["true", "false", "false", "false", "false", "false", "false", "false", "false"], pestanas.Select(p => p.GetAttribute("aria-selected")));
        var general = pantalla.Find("#seccion-general");
        Assert.Equal("tabpanel", general.GetAttribute("role"));
        Assert.False(general.HasAttribute("hidden"));
        Assert.Contains("Modo: mantenimiento", general.TextContent);
        Assert.NotNull(general.QuerySelector("#estado-bitacoras"));
        Assert.Equal(8, pantalla.FindAll("[role=\"tabpanel\"][hidden]").Count);
    }

    [Fact(DisplayName = "configuracion-puesto · Elegir otra pestaña: tocar «Impresora» deja visible sólo su contenido y la marca como elegida")]
    public async Task Elegir_otra_pestana()
    {
        var pantalla = Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());

        await pantalla.Find("#pestana-impresora").ClickAsync(new());

        Assert.Equal("true", pantalla.Find("#pestana-impresora").GetAttribute("aria-selected"));
        Assert.Equal("false", pantalla.Find("#pestana-general").GetAttribute("aria-selected"));
        var visibles = pantalla.FindAll("[role=\"tabpanel\"]").Where(p => !p.HasAttribute("hidden")).Select(p => p.Id).ToList();
        Assert.Equal(["seccion-impresora"], visibles);
    }

    [Fact(DisplayName = "configuracion-puesto · Las pestañas con el teclado: flecha derecha elige Personas e Inicio vuelve a General, con la elegida en el orden de tabulación")]
    public async Task Las_pestanas_con_el_teclado()
    {
        var pantalla = Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());

        await pantalla.Find("#pestana-general").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("true", pantalla.Find("#pestana-personas").GetAttribute("aria-selected"));
        Assert.Equal(("0", "-1"), (pantalla.Find("#pestana-personas").GetAttribute("tabindex"), pantalla.Find("#pestana-general").GetAttribute("tabindex")));

        await pantalla.Find("#pestana-personas").KeyDownAsync(new KeyboardEventArgs { Key = "Home" });

        Assert.Equal("true", pantalla.Find("#pestana-general").GetAttribute("aria-selected"));
        Assert.Equal("-1", pantalla.Find("#pestana-personas").GetAttribute("tabindex"));
        Assert.NotEmpty(pantalla.FindAll("[role=\"dialog\"]"));
    }

    [Fact(DisplayName = "configuracion-puesto · Pasar de pestaña no pierde lo editado: la opacidad en 84 % sin guardar sigue al ir a Impresora y volver, y la vista previa también")]
    public async Task Pasar_de_pestana_no_pierde_lo_editado()
    {
        var pantalla = Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#pestana-identidad").ClickAsync(new());
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "84" });

        await pantalla.Find("#pestana-impresora").ClickAsync(new());
        await pantalla.Find("#pestana-identidad").ClickAsync(new());

        Assert.Equal("84", pantalla.Find("#opacidad").GetAttribute("value"));
        Assert.Contains("--opacidad-superficie: 0.84", Estilo(pantalla));
        Assert.False(File.Exists(ArchivoEstilo));
    }

    [Theory(DisplayName = "configuracion-puesto · Cancelar o Escape descartan lo no guardado y conservan lo guardado: la opacidad vuelve y la versión con el cupo cambiado sigue activa")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancelar_descarta_lo_no_guardado_y_conserva_lo_guardado(bool conEscape)
    {
        var pantalla = Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        Assert.Equal(TipoGuardado.Aplicado, GuardarRefrigerioEnUno().Tipo);
        await pantalla.Find("#pestana-identidad").ClickAsync(new());
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "88" });

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
        Assert.Equal(2L, Escalar("SELECT comvcVersionConfiguracion FROM tbComedorVersionesConfiguracion WHERE comvcCodigo = (SELECT comvcCodigoActiva FROM tbComedorConfiguracionPuestos)"));
    }

    [Fact(DisplayName = "configuracion-puesto · Escape con el panel de opciones abierto cierra Configuración entera y descarta lo no guardado")]
    public async Task Escape_con_un_panel_abierto_cierra_configuracion()
    {
        var pantalla = Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#abrir-opciones").ClickAsync(new());
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "88" });

        await pantalla.Find("[role=\"dialog\"]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(pantalla.FindAll("[role=\"dialog\"]"));
        Assert.Contains("--opacidad-superficie: 0.76", Estilo(pantalla));
        Assert.Equal(1L, Escalar("SELECT COUNT(*) FROM tbComedorVersionesConfiguracion"));
    }

    [Fact(DisplayName = "configuracion-puesto · La atención sale de reposo con Configuración abierta: se cierra con lo no guardado descartado y no reaparece al volver a reposo")]
    public async Task La_atencion_sale_de_reposo_con_configuracion_abierta()
    {
        var pantalla = Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#pestana-identidad").ClickAsync(new());
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "88" });

        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("[role=\"dialog\"]"));
        await pantalla.Find("#cancelar").ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll("#ingresar"));
        Assert.Empty(pantalla.FindAll("[role=\"dialog\"]"));
        Assert.Contains("--opacidad-superficie: 0.76", Estilo(pantalla));
    }

    [Fact(DisplayName = "configuracion-puesto · Guardar el estilo deja Configuración abierta en Identidad visual y dice que se guardó")]
    public async Task Guardar_el_estilo_deja_configuracion_abierta()
    {
        var pantalla = Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#pestana-identidad").ClickAsync(new());
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "84" });

        await pantalla.Find("#guardar-configuracion").ClickAsync(new());

        Assert.NotEmpty(pantalla.FindAll("[role=\"dialog\"]"));
        Assert.Equal("true", pantalla.Find("#pestana-identidad").GetAttribute("aria-selected"));
        Assert.Equal(MensajesAtencion.EstiloGuardado, pantalla.Find("#aviso-guardado").TextContent.Trim());
        Assert.Equal(new EstiloVisual(84, 4), new AlmacenEstiloVisual(_carpeta).Leer());
    }

    [Fact(DisplayName = "configuracion-puesto · Guardar personas no guarda la vista previa del estilo: después de «Cancelar» existe la versión nueva y el estilo guardado es el de antes")]
    public async Task Guardar_personas_no_guarda_la_vista_previa_del_estilo()
    {
        var pantalla = Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#pestana-identidad").ClickAsync(new());
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "84" });
        await pantalla.Find("#pestana-personas").ClickAsync(new());
        await pantalla.Find("#abrir-personas").ClickAsync(new());

        await pantalla.Find("#guardar-panel").ClickAsync(new());

        Assert.NotEmpty(pantalla.FindAll("[role=\"dialog\"]"));
        Assert.Equal("true", pantalla.Find("#pestana-personas").GetAttribute("aria-selected"));
        Assert.Equal("Se guardó la versión 2 de la configuración", pantalla.Find("#aviso-guardado").TextContent.Trim());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        Assert.Equal(2L, Escalar("SELECT COUNT(*) FROM tbComedorVersionesConfiguracion"));
        Assert.False(File.Exists(ArchivoEstilo));
        Assert.Contains("--opacidad-superficie: 0.76", Estilo(pantalla));
    }

    [Fact(DisplayName = "configuracion-puesto · Guardar sin poder escribir: Configuración sigue abierta en Identidad visual con el aviso y el estilo vigente es el de antes")]
    public async Task Guardar_sin_poder_escribir()
    {
        var carpeta = Path.Combine(_carpeta, "datos");
        File.WriteAllText(carpeta, "no es una carpeta");
        var pantalla = Dibujar(carpeta);
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#pestana-identidad").ClickAsync(new());
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "84" });

        await pantalla.Find("#guardar-configuracion").ClickAsync(new());

        Assert.Equal("true", pantalla.Find("#pestana-identidad").GetAttribute("aria-selected"));
        Assert.Equal(MensajesAtencion.EstiloNoGuardado, pantalla.Find("[role=\"dialog\"]").QuerySelector("[role=\"alert\"]")?.TextContent.Trim());
        Assert.Empty(pantalla.FindAll("#aviso-guardado"));
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        Assert.Contains("--opacidad-superficie: 0.76", Estilo(pantalla));
    }

    private ResultadoGuardado GuardarRefrigerioEnUno()
    {
        var administracion = _servicio.Administracion!;
        var persona = administracion.Leer().Personas.Single(p => p.Nombre == "Persona sintética 01");
        var cupos = persona.Cupos.Select(c => new CupoEditado(c.ServicioId, c.CategoriaId, c.Categoria == "REFRIGERIO" ? 1 : c.Limite)).ToList();
        return administracion.GuardarPersonas([new EdicionPersona(persona.Id, persona.Nombre, cupos)]);
    }

    private IRenderedComponent<PantallaAtencion> Dibujar(string? carpetaEstilo = null)
    {
        _servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: true);
        _contexto.Services.AddSingleton(_servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(_servicio));
        _contexto.Services.AddSingleton(new AlmacenEstiloVisual(carpetaEstilo ?? _carpeta));
        return _contexto.Render<PantallaAtencion>();
    }

    private long Escalar(string sql)
    {
        using var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private static string Estilo(IRenderedComponent<PantallaAtencion> pantalla) => pantalla.Find(".puesto").GetAttribute("style") ?? "";
}
