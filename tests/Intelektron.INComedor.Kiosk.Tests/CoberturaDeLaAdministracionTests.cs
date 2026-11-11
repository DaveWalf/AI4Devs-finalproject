using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Lo que la verificación de administrar-personas-y-opciones (06/11/2026) dejó sin prueba o cubierto sólo por composición
/// (cubrir-pruebas-de-las-verificaciones, grupo 2): el modo de la impresora con INWeb como dueño, y la baja de una persona y
/// el cambio de precio de una opción elegida vistos en la pantalla. bUnit sobre el puesto de prueba y SQLite real.
/// </summary>
public class CoberturaDeLaAdministracionTests
{
    private const string Almuerzo = "Almuerzo común";

    [Fact(DisplayName = "administracion-local · Con INWeb, el modo de la impresora sigue editable: «Sin papel» se aplica y Opciones sigue con «Estos datos los administra INWeb»")]
    public async Task Con_INWeb_el_modo_de_la_impresora_sigue_editable()
    {
        using var puesto = Preparar();
        var impresora = new ImpresoraSimulada(Path.Combine(puesto.Carpeta, "comprobantes"));
        var pantalla = puesto.Dibujar(impresora);
        Ejecutar(puesto, "INSERT INTO tbComedorOrigenesVersiones (comvcCodigo, comvoOrigen) SELECT comvcCodigoActiva, 'INWEB' FROM tbComedorConfiguracionPuestos");
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#impresora-sinpapel").ClickAsync(new());

        Assert.Equal(ModoImpresoraSimulada.SinPapel, impresora.Modo);
        Assert.Equal("Modo: Sin papel", pantalla.Find("#modo-impresora").TextContent.Trim());
        await pantalla.Find("#abrir-opciones").ClickAsync(new());
        Assert.Equal("Estos datos los administra INWeb.", pantalla.Find("#aviso-inweb").TextContent.Trim());
        Assert.Empty(pantalla.FindAll("#guardar-panel"));
    }

    [Fact(DisplayName = "administracion-local · Baja de una persona con consumos, en la pantalla: quitar a 0001 y guardar; su código muestra el mensaje de CREDENTIAL_REVOKED sin opciones, su consumo sigue y Personas la muestra de baja")]
    public async Task La_baja_en_la_pantalla()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await Identificar(pantalla, "0001");
        await pantalla.FindAll(".opcion").Single(o => o.QuerySelector(".nombre")!.TextContent.Trim() == Almuerzo).ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        await pantalla.FindAll("#listo")[0].ClickAsync(new());

        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#abrir-personas").ClickAsync(new());
        await Fila(pantalla, "Persona sintética 01").QuerySelector("button.quitar-persona")!.ClickAsync(new());
        await pantalla.Find("#guardar-panel").ClickAsync(new());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        await Identificar(pantalla, "0001");

        Assert.Equal(MensajesAtencion.DeMotivo("CREDENTIAL_REVOKED"), pantalla.Find("#aviso").TextContent.Trim());
        Assert.Empty(pantalla.FindAll(".opcion"));
        Assert.Equal(1L, Contar(puesto, "tbComedorConsumos"));
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#abrir-personas").ClickAsync(new());
        Assert.Contains("De baja", Fila(pantalla, "Persona sintética 01").TextContent, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "administracion-local · Cambio de precio con la opción elegida, en la pantalla: con «Almuerzo común» elegido, la administración lo lleva a 1.800,00 con la versión de menú siguiente y «Confirmar» muestra 1.800,00 y pide reconfirmar, sin consumo")]
    public async Task El_cambio_de_precio_en_la_pantalla()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        var maquina = puesto.Contexto.Services.GetRequiredService<MaquinaAtencion>();
        await Identificar(pantalla, "0001");
        await pantalla.FindAll(".opcion").Single(o => o.QuerySelector(".nombre")!.TextContent.Trim() == Almuerzo).ClickAsync(new());

        var admin = puesto.Servicio.Administracion!;
        var antes = admin.Leer().Opciones.Single(o => o.Nombre == Almuerzo);
        var edicion = new EdicionOpcion(antes.Id, antes.Nombre, antes.ServicioId, 180000,
            antes.Cargas.Select(c => new CargaEditada(c.CategoriaId, c.Unidades)).ToList(),
            antes.Franjas.Select(f => new FranjaEditada(f.Inicio.ToString("HH:mm"), f.Fin.ToString("HH:mm"))).ToList());
        Assert.Equal(TipoGuardado.Aplicado, admin.GuardarOpciones([edicion]).Tipo);
        await pantalla.Find("#confirmar").ClickAsync(new());

        Assert.Equal(antes.Version + 1, admin.Leer().Opciones.Single(o => o.Id == antes.Id).Version);
        Assert.Equal(EstadoAtencion.Reconfirmacion, maquina.Estado);
        Assert.Contains(Almuerzo, pantalla.Find("#vigente").TextContent, StringComparison.Ordinal);
        Assert.Contains("1.800,00", pantalla.Find("#cargo-vigente").TextContent, StringComparison.Ordinal);
        Assert.Equal(0L, Contar(puesto, "tbComedorConsumos"));
    }

    private static PuestoDePrueba Preparar()
    {
        var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        return puesto;
    }

    private static async Task Identificar(IRenderedComponent<PantallaAtencion> pantalla, string codigo)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = codigo });
        await pantalla.Find("#ingresar").ClickAsync(new());
    }

    private static AngleSharp.Dom.IElement Fila(IRenderedComponent<PantallaAtencion> pantalla, string nombre) =>
        pantalla.FindAll("tr.persona").Single(f => f.QuerySelector("input.nombre-persona")?.GetAttribute("value") == nombre);

    private static void Ejecutar(PuestoDePrueba puesto, string sql)
    {
        using var conexion = new SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        comando.ExecuteNonQuery();
    }

    private static long Contar(PuestoDePrueba puesto, string tabla)
    {
        using var conexion = new SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT COUNT(*) FROM {tabla}";
        return Convert.ToInt64(comando.ExecuteScalar());
    }
}
