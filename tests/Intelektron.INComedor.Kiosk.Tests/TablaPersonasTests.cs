using AngleSharp.Dom;
using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Personas en una tabla (administracion-local de configurar-puesto-por-pestanas, D4): una fila por persona y una columna
/// por cupo que el catálogo usa, «Editar» en el mismo lugar, y el orden por columna que cambia sólo la vista. bUnit sobre un
/// <see cref="ServicioAtencion"/> real con el catálogo de nueve.
/// </summary>
public class TablaPersonasTests : IDisposable
{
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");
    private ServicioAtencion _servicio = null!;

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
    }

    [Fact(DisplayName = "administracion-local · La persona del código 0001: su fila dice «Activa», «0/1» en ALMUERZO, «0/2» en REFRIGERIO y 9 opciones habilitadas")]
    public async Task La_fila_de_0001()
    {
        var pantalla = await AbrirTabla();

        var fila = Fila(pantalla, "Persona sintética 01");

        Assert.Equal("Activa", fila.QuerySelector(".estado-persona")!.TextContent.Trim());
        Assert.Equal([("ALMUERZO", "0/1"), ("REFRIGERIO", "0/2")], Cupos(fila));
        Assert.Equal("9", fila.QuerySelector(".habilitadas")!.TextContent.Trim());
    }

    [Fact(DisplayName = "administracion-local · Las columnas de cupo son sólo los pares que usa el catálogo: ALMUERZO y REFRIGERIO, no cada servicio por cada categoría")]
    public async Task Las_columnas_de_cupo()
    {
        var pantalla = await AbrirTabla();

        var encabezados = pantalla.FindAll("#tabla-personas thead th").Select(th => th.TextContent.Trim()).ToList();

        Assert.Equal(["Nombre", "Estado", "Operador", "ALMUERZO", "REFRIGERIO", "Opciones habilitadas"], encabezados);
    }

    [Fact(DisplayName = "administracion-local · Persona sin cupos: su fila tiene «—» en cada columna de cupo y 0 opciones habilitadas")]
    public async Task Persona_sin_cupos()
    {
        var pantalla = await AbrirTabla();

        var fila = Fila(pantalla, "Persona sintética 02");

        Assert.All(fila.QuerySelectorAll("td.cupo"), c => Assert.Equal("—", c.TextContent.Trim()));
        Assert.Equal("0", fila.QuerySelector(".habilitadas")!.TextContent.Trim());
    }

    [Fact(DisplayName = "administracion-local · Cambiar un límite en su columna: con «Editar», REFRIGERIO de «Persona sintética 01» de 2 a 1; al guardar, la versión 2 está activa y la fila dice «0/1»")]
    public async Task Cambiar_un_limite_en_su_columna()
    {
        var pantalla = await AbrirTabla();
        await pantalla.Find("#abrir-personas").ClickAsync(new());

        await Celda(pantalla, "Persona sintética 01", "REFRIGERIO").InputAsync(new ChangeEventArgs { Value = "1" });
        await pantalla.Find("#guardar-panel").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#panel-personas"));
        Assert.Equal(2L, Escalar("SELECT comvcVersionConfiguracion FROM tbComedorVersionesConfiguracion WHERE comvcCodigo = (SELECT comvcCodigoActiva FROM tbComedorConfiguracionPuestos)"));
        Assert.Equal([("ALMUERZO", "0/1"), ("REFRIGERIO", "0/1")], Cupos(Fila(pantalla, "Persona sintética 01")));
    }

    [Fact(DisplayName = "administracion-local · Vaciar una celda quita el cupo: REFRIGERIO de «Persona sintética 01» vacío dice «—» y sus opciones habilitadas son sólo las de ALMUERZO")]
    public async Task Vaciar_una_celda_quita_el_cupo()
    {
        var pantalla = await AbrirTabla();
        var almuerzos = _servicio.Administracion!.Leer().Opciones.Count(o => o.Servicio == "ALMUERZO");
        await pantalla.Find("#abrir-personas").ClickAsync(new());

        await Celda(pantalla, "Persona sintética 01", "REFRIGERIO").InputAsync(new ChangeEventArgs { Value = "" });
        await pantalla.Find("#guardar-panel").ClickAsync(new());

        var fila = Fila(pantalla, "Persona sintética 01");
        Assert.Equal([("ALMUERZO", "0/1")], Cupos(fila));
        Assert.Equal("—", fila.QuerySelector("td.cupo[data-cupo=\"REFRIGERIO\"]")!.TextContent.Trim());
        Assert.Equal(almuerzos.ToString(), fila.QuerySelector(".habilitadas")!.TextContent.Trim());
    }

    [Fact(DisplayName = "administracion-local · Ordenar por un cupo: dos toques en REFRIGERIO dejan las filas del límite mayor al menor, las personas sin cupo al final, y el encabezado indica el orden descendente")]
    public async Task Ordenar_por_un_cupo()
    {
        var pantalla = await AbrirTabla();
        var columna = pantalla.Find("#tabla-personas th[data-columna=\"REFRIGERIO\"]");

        await columna.QuerySelector("button.ordenar")!.ClickAsync(new());
        Assert.Equal("ascending", pantalla.Find("#tabla-personas th[data-columna=\"REFRIGERIO\"]").GetAttribute("aria-sort"));
        await pantalla.Find("#tabla-personas th[data-columna=\"REFRIGERIO\"] button.ordenar").ClickAsync(new());

        Assert.Equal("descending", pantalla.Find("#tabla-personas th[data-columna=\"REFRIGERIO\"]").GetAttribute("aria-sort"));
        var valores = pantalla.FindAll("#tabla-personas tbody td.cupo[data-cupo=\"REFRIGERIO\"]").Select(c => c.TextContent.Trim()).ToList();
        var conCupo = valores.TakeWhile(v => v != "—").Select(v => int.Parse(v.Split('/')[1])).ToList();
        Assert.NotEmpty(conCupo);
        Assert.Equal(conCupo.OrderByDescending(v => v), conCupo);
        Assert.All(valores.Skip(conCupo.Count), v => Assert.Equal("—", v));
        Assert.Equal("none", pantalla.Find("#tabla-personas th[data-columna=\"Nombre\"]").GetAttribute("aria-sort"));
    }

    [Fact(DisplayName = "administracion-local · Nombres con tilde: «Ana», «Álvaro» y «Beatriz» ordenados por nombre quedan «Álvaro», «Ana» y «Beatriz»")]
    public async Task Nombres_con_tilde()
    {
        var pantalla = Dibujar();
        var administracion = _servicio.Administracion!;
        var almuerzo = administracion.Leer().Personas.Single(p => p.Nombre == "Persona sintética 01").Cupos.Select(c => new CupoEditado(c.ServicioId, c.CategoriaId, c.Limite)).ToList();
        Assert.Equal(TipoGuardado.Aplicado, administracion.GuardarPersonas(
            [new EdicionPersona(null, "Ana", almuerzo, "0201"), new EdicionPersona(null, "Álvaro", almuerzo, "0202"), new EdicionPersona(null, "Beatriz", almuerzo, "0203")]).Tipo);
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-personas").ClickAsync(new());

        await pantalla.Find("#tabla-personas th[data-columna=\"Nombre\"] button.ordenar").ClickAsync(new());

        // Entre todas las filas (la semilla suma, por ejemplo, «Administrador sintético 04»), los tres en el orden del español;
        // en orden ordinal «Álvaro» quedaría después de «Beatriz».
        var nombres = pantalla.FindAll("#tabla-personas tbody tr.persona").Select(Nombre).Where(n => n is "Ana" or "Álvaro" or "Beatriz").ToList();
        Assert.Equal(["Álvaro", "Ana", "Beatriz"], nombres);
        Assert.Equal("ascending", pantalla.Find("#tabla-personas th[data-columna=\"Nombre\"]").GetAttribute("aria-sort"));
    }

    [Fact(DisplayName = "administracion-local · Ordenar no mueve la marca de un campo inválido: la persona nueva sin nombre, señalada por el guardado, sigue marcada en su fila después de ordenar por nombre")]
    public async Task Ordenar_no_mueve_la_marca()
    {
        var pantalla = await AbrirTabla();
        await pantalla.Find("#abrir-personas").ClickAsync(new());
        await pantalla.Find("#agregar-persona").ClickAsync(new());
        await pantalla.Find("#panel-personas input.codigo-persona").InputAsync(new ChangeEventArgs { Value = "0105" });
        await pantalla.Find("#guardar-panel").ClickAsync(new());
        Assert.Equal("Revisá los campos marcados.", pantalla.Find("#aviso-panel").TextContent.Trim());

        await pantalla.Find("#panel-personas th[data-columna=\"Nombre\"] button.ordenar").ClickAsync(new());

        var marcadas = pantalla.FindAll("#panel-personas input.nombre-persona[aria-invalid=\"true\"]");
        var marcada = Assert.Single(marcadas);
        Assert.NotNull(marcada.Closest("tr.persona")!.QuerySelector("input.codigo-persona"));
        Assert.Equal("ascending", pantalla.Find("#panel-personas th[data-columna=\"Nombre\"]").GetAttribute("aria-sort"));
    }

    private async Task<IRenderedComponent<PantallaAtencion>> AbrirTabla()
    {
        var pantalla = Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-personas").ClickAsync(new());
        return pantalla;
    }

    private static IElement Fila(IRenderedComponent<PantallaAtencion> pantalla, string nombre) =>
        pantalla.FindAll("tr.persona").Single(f => Nombre(f) == nombre);

    private static string Nombre(IElement fila) =>
        fila.QuerySelector("input.nombre-persona")?.GetAttribute("value") ?? fila.QuerySelector(".nombre-persona")!.TextContent.Trim();

    private static IElement Celda(IRenderedComponent<PantallaAtencion> pantalla, string nombre, string cupo) =>
        Fila(pantalla, nombre).QuerySelector($"td.cupo[data-cupo=\"{cupo}\"] input.limite")!;

    /// <summary>Los cupos con límite de una fila de lectura, por su columna.</summary>
    private static List<(string, string)> Cupos(IElement fila) =>
        fila.QuerySelectorAll("td.cupo").Select(c => (c.GetAttribute("data-cupo")!, c.TextContent.Trim())).Where(c => c.Item2 != "—").ToList();

    private IRenderedComponent<PantallaAtencion> Dibujar()
    {
        _servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: true);
        _contexto.Services.AddSingleton(_servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(_servicio));
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
}
