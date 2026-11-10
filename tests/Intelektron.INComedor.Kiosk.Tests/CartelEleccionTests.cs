using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El cartel de la elección (atencion-puesto de confirmar-eleccion-en-cartel, D1 a D3): tocar una opción abre un cartel
/// que la repite con su cargo, con «Confirmar» y «Elegir otra»; «Elegir otra» y Escape lo cierran sin escribir nada, y la
/// inactividad lo cierra con la atención. Con bUnit sobre un <see cref="ServicioAtencion"/> real y una base temporal.
/// </summary>
public class CartelEleccionTests : IDisposable
{
    private const string Almuerzo = "Almuerzo común";
    private const string Refrigerio = "Café con leche";

    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
    }

    [Fact(DisplayName = "atencion-puesto · Elegir abre el cartel con lo elegido: sin opción elegida no hay «Confirmar»; al tocar una, el cartel la repite con «Cargo informado: ARS 1.500,00», «Confirmar» y «Elegir otra», sin ninguna intención")]
    public async Task Elegir_abre_el_cartel_con_lo_elegido()
    {
        var pantalla = Dibujar();
        await Ingresar(pantalla, "0001");
        Assert.Empty(pantalla.FindAll("#confirmar"));
        Assert.Empty(pantalla.FindAll("#cartel-eleccion"));

        await Opcion(pantalla, Almuerzo).ClickAsync(new());

        var cartel = pantalla.Find("#cartel-eleccion");
        Assert.Equal("dialog", cartel.GetAttribute("role"));
        Assert.Equal("true", cartel.GetAttribute("aria-modal"));
        Assert.Equal("titulo-cartel", cartel.GetAttribute("aria-labelledby"));
        Assert.Equal("¿Confirmás tu elección?", pantalla.Find("#titulo-cartel").TextContent.Trim());
        var eleccion = pantalla.Find("#cartel-eleccion #eleccion").TextContent;
        Assert.Contains(Almuerzo, eleccion);
        Assert.Contains("Cargo informado: ARS 1.500,00", eleccion);
        Assert.Equal("Confirmar", pantalla.Find("#cartel-eleccion #confirmar").TextContent.Trim());
        Assert.Equal("Elegir otra", pantalla.Find("#cartel-eleccion #elegir-otra").TextContent.Trim());
        Assert.All(new[] { "div.titulo", ".lista", ".paginas", "header", "footer" },
            s => Assert.True(pantalla.Find(s).HasAttribute("inert"), $"{s} no está inerte con el cartel abierto"));
        Assert.False(cartel.HasAttribute("inert"));
        Assert.Equal(0L, Intenciones());
    }

    [Fact(DisplayName = "atencion-puesto · Elegir otra vuelve a la selección: cierra el cartel con la misma persona y la marca en la primera opción, y tocar otra abre el cartel con la otra, sin ninguna intención")]
    public async Task Elegir_otra_vuelve_a_la_seleccion()
    {
        var pantalla = Dibujar();
        await Ingresar(pantalla, "0001");
        await Opcion(pantalla, Almuerzo).ClickAsync(new());

        await pantalla.Find("#elegir-otra").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#cartel-eleccion"));
        Assert.Empty(pantalla.FindAll("#confirmar"));
        Assert.Contains("Persona sintética 01", pantalla.Find("#saludo").TextContent);
        Assert.Equal("true", Opcion(pantalla, Almuerzo).GetAttribute("aria-pressed"));
        Assert.Contains("Elegida", Opcion(pantalla, Almuerzo).TextContent);

        await Opcion(pantalla, Refrigerio).ClickAsync(new());

        Assert.Contains(Refrigerio, pantalla.Find("#cartel-eleccion #eleccion").TextContent);
        Assert.DoesNotContain(Almuerzo, pantalla.Find("#cartel-eleccion #eleccion").TextContent);
        Assert.Equal("true", Opcion(pantalla, Refrigerio).GetAttribute("aria-pressed"));
        Assert.Equal("false", Opcion(pantalla, Almuerzo).GetAttribute("aria-pressed"));
        Assert.Equal(0L, Intenciones());
    }

    [Fact(DisplayName = "atencion-puesto · Escape en el cartel: lo cierra como «Elegir otra», con la misma persona y la misma elección, el puesto abierto y sin ninguna intención")]
    public async Task Escape_en_el_cartel()
    {
        var pantalla = Dibujar();
        await Ingresar(pantalla, "0001");
        await Opcion(pantalla, Almuerzo).ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll("#cartel-eleccion"));

        await pantalla.Find(".puesto").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(pantalla.FindAll("#cartel-eleccion"));
        Assert.Contains("Persona sintética 01", pantalla.Find("#saludo").TextContent);
        Assert.Equal("true", Opcion(pantalla, Almuerzo).GetAttribute("aria-pressed"));
        Assert.NotEmpty(pantalla.FindAll("#cancelar"));
        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        Assert.Equal(0L, Intenciones());
    }

    [Fact(DisplayName = "atencion-puesto · Opción sin cargo: con importe cero, ni la opción ni el cartel muestran importe, y el «Confirmar» del cartel queda habilitado")]
    public async Task Opcion_sin_cargo()
    {
        var pantalla = Dibujar(sinCargo: SemillaAtencion.PrimeraOpcionId);
        await Ingresar(pantalla, "0001");
        Assert.Empty(Opcion(pantalla, Almuerzo).QuerySelectorAll(".cargo"));

        await Opcion(pantalla, Almuerzo).ClickAsync(new());

        var eleccion = pantalla.Find("#cartel-eleccion #eleccion").TextContent;
        Assert.Contains(Almuerzo, eleccion);
        Assert.DoesNotContain("Cargo informado", eleccion);
        Assert.DoesNotContain("ARS", eleccion);
        Assert.False(pantalla.Find("#cartel-eleccion #confirmar").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "atencion-puesto · Treinta segundos sin actividad: con el cartel abierto, a los 30 s vuelve a reposo sin intención ni consumo, y la persona siguiente no ve el nombre, la elección ni el cartel de la anterior")]
    public async Task Treinta_segundos_con_el_cartel_abierto()
    {
        var pantalla = Dibujar();
        await Ingresar(pantalla, "0001");
        await Opcion(pantalla, Almuerzo).ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll("#cartel-eleccion"));

        _reloj.Avanzar(TimeSpan.FromSeconds(30));

        pantalla.WaitForAssertion(() => Assert.NotEmpty(pantalla.FindAll("#codigo")), TimeSpan.FromSeconds(5));
        Assert.Empty(pantalla.FindAll("#cartel-eleccion"));
        Assert.Equal(0L, Intenciones());
        Assert.Equal(0L, Escalar("SELECT COUNT(*) FROM tbComedorConsumos"));

        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "9001" });
        await pantalla.Find("#ingresar").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#codigo"));
        Assert.DoesNotContain("Persona sintética 01", pantalla.Markup);
        Assert.DoesNotContain(Almuerzo, pantalla.Markup);
        Assert.Empty(pantalla.FindAll("#cartel-eleccion"));
        Assert.Empty(pantalla.FindAll(".elegida"));
    }

    private IRenderedComponent<PantallaAtencion> Dibujar(Guid? sinCargo = null)
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba());
        Assert.Equal(Demostracion.EstadoPreparacion.Lista, servicio.Preparar());
        if (sinCargo is { } opcion)
        {
            using var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "UPDATE tbComedorOpcionesMenu SET commenImporteEmpleadoUnidadesMenores = 0 WHERE commenCodigo = $men";
            comando.Parameters.AddWithValue("$men", opcion.ToString("D"));
            Assert.Equal(1, comando.ExecuteNonQuery());
        }

        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        return _contexto.Render<PantallaAtencion>();
    }

    private static async Task Ingresar(IRenderedComponent<PantallaAtencion> pantalla, string codigo)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = codigo });
        await pantalla.Find("#ingresar").ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll(".opcion"));
    }

    private static AngleSharp.Dom.IElement Opcion(IRenderedComponent<PantallaAtencion> pantalla, string nombre) =>
        pantalla.FindAll(".opcion").Single(o => o.QuerySelector(".nombre")!.TextContent.Trim() == nombre);

    private long Intenciones() => Escalar("SELECT COUNT(*) FROM tbComedorIntencionesConsumo");

    private long Escalar(string sql)
    {
        using var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Mode=ReadOnly;Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(comando.ExecuteScalar());
    }
}
