using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El comprobante en pantalla con la impresora simulada (D7 de presentar-atencion-tactil), con bUnit sobre un
/// <see cref="ServicioAtencion"/> real, la impresora simulada en una carpeta temporal y el reloj manual. La opción es
/// «Almuerzo común» (ALMUERZO, ARS 1.500,00), la primera del catálogo de quince.
/// </summary>
public class ComprobanteEnPantallaTests : IDisposable
{
    private const string Opcion = "Almuerzo común";
    private const string Persona = "Persona sintética 01";

    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "incomedor-tests", "comprobantes-" + Guid.NewGuid().ToString("N"));
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, recursive: true);
        }
    }

    [Fact(DisplayName = "comprobante-local · AT06 · Autorización con la impresora disponible: la impresora y el comprobante con la opción, el cargo y el nombre, sin el código")]
    public async Task Disponible_muestra_la_impresora_y_el_comprobante()
    {
        var pantalla = Dibujar(new ImpresoraSimulada(_carpeta));

        await Confirmar(pantalla);

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.Exito, pantalla.Find("#mensaje").TextContent.Trim()));
        Assert.NotNull(pantalla.Find(".impresora"));
        var comprobante = pantalla.Find(".comprobante");
        Assert.Equal("BUTTON", comprobante.TagName);
        Assert.Equal("listo", comprobante.Id);
        Assert.Equal("Retirar el comprobante", comprobante.GetAttribute("aria-label"));
        Assert.Contains(Opcion, comprobante.TextContent);
        Assert.Contains("ARS 1.500,00", comprobante.TextContent);
        Assert.Contains("ORIGINAL", comprobante.TextContent);
        Assert.Contains("30/10/2026", comprobante.TextContent);
        Assert.Contains(Persona, comprobante.TextContent);
        Assert.DoesNotContain("0001", comprobante.TextContent);
        var archivo = Assert.Single(Directory.GetFiles(_carpeta));
        var numero = Path.GetFileName(archivo)[..8];
        Assert.Contains(numero, comprobante.TextContent);
    }

    [Fact(DisplayName = "comprobante-local · Tocar el comprobante equivale a «Listo»: vuelve a reposo sin el nombre ni la elección")]
    public async Task Tocar_el_comprobante_vuelve_a_reposo()
    {
        var pantalla = Dibujar(new ImpresoraSimulada(_carpeta));
        await Confirmar(pantalla);
        pantalla.WaitForAssertion(() => pantalla.Find(".comprobante"));

        await pantalla.Find(".comprobante").ClickAsync(new());

        Assert.NotNull(pantalla.Find("#codigo"));
        Assert.DoesNotContain(Persona, pantalla.Markup);
        Assert.Empty(pantalla.FindAll(".impresora"));
    }

    [Theory(DisplayName = "comprobante-local · AT06 · «Sin papel» e «Incierta» no muestran comprobante en pantalla")]
    [InlineData(ModoImpresoraSimulada.SinPapel)]
    [InlineData(ModoImpresoraSimulada.Incierta)]
    public async Task Sin_papel_o_incierta_no_muestran_comprobante(ModoImpresoraSimulada modo)
    {
        var pantalla = Dibujar(new ImpresoraSimulada(_carpeta) { Modo = modo });

        await Confirmar(pantalla);

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.ComprobantePendiente, pantalla.Find("#mensaje").TextContent.Trim()));
        Assert.Empty(pantalla.FindAll(".comprobante"));
        Assert.Equal("Listo", pantalla.Find("#listo").TextContent.Trim());
    }

    [Fact(DisplayName = "comprobante-local · AT02 · Un rechazo LOCAL_LIMIT_REACHED no muestra la impresora")]
    public async Task Un_rechazo_no_muestra_la_impresora()
    {
        var pantalla = Dibujar(new ImpresoraSimulada(_carpeta));
        await Elegir(pantalla);
        GastarElCupoDeAlmuerzoDeLaPersona();

        await pantalla.Find("#confirmar").ClickAsync(new());

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.DeMotivo("LOCAL_LIMIT_REACHED"), pantalla.Find("#mensaje").TextContent.Trim()));
        Assert.Empty(pantalla.FindAll(".impresora"));
        Assert.Empty(pantalla.FindAll(".comprobante"));
    }

    [Fact(DisplayName = "comprobante-local · HU03-E · Diez segundos sin tocar el comprobante: vuelve a reposo sin el nombre")]
    public async Task Diez_segundos_vuelven_a_reposo()
    {
        var pantalla = Dibujar(new ImpresoraSimulada(_carpeta));
        await Confirmar(pantalla);
        pantalla.WaitForAssertion(() => pantalla.Find(".comprobante"));

        _reloj.Avanzar(TimeSpan.FromSeconds(10));

        pantalla.WaitForAssertion(() => Assert.NotNull(pantalla.Find("#codigo")), TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(Persona, pantalla.Markup);
    }

    [Fact(DisplayName = "comprobante-local · Con otra impresora queda «Listo»: el mensaje y el botón, sin impresora en pantalla")]
    public async Task Con_otra_impresora_queda_listo()
    {
        var pantalla = Dibujar(new ImpresoraDePrueba());

        await Confirmar(pantalla);

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.Exito, pantalla.Find("#mensaje").TextContent.Trim()));
        Assert.Equal("Listo", pantalla.Find("#listo").TextContent.Trim());
        Assert.Empty(pantalla.FindAll(".impresora"));
        Assert.Empty(pantalla.FindAll(".comprobante"));
    }

    [Fact(DisplayName = "comprobante-local · Un comprobante con el puesto y la frase: en pantalla, el puesto guardado, el comensal y la frase de ese comprobante, y el archivo de la impresora dice lo mismo")]
    public async Task El_comprobante_con_el_puesto_y_la_frase()
    {
        var datos = Directory.CreateDirectory(Path.Combine(_carpeta, "datos")).FullName;
        new AlmacenDatosPuesto(datos).Guardar(new DatosPuesto(3, "Comedor Norte", false, 1));
        _contexto.Services.AddSingleton(new AlmacenDatosPuesto(datos));
        var impresora = new ImpresoraSimulada(Path.Combine(_carpeta, "papel"));
        var pantalla = Dibujar(impresora);

        await Confirmar(pantalla);

        pantalla.WaitForAssertion(() => Assert.NotNull(pantalla.Find(".comprobante")));
        var texto = pantalla.Find(".comprobante").TextContent;
        var archivo = Assert.Single(Directory.GetFiles(Path.Combine(_carpeta, "papel")));
        var ticket = Guid.Parse(Path.GetFileName(archivo)[..36]);
        Assert.Contains("Puesto 3 · Comedor Norte", texto);
        Assert.Contains(Persona, texto);
        Assert.Contains(FrasesComprobante.Para(ticket), texto);
        var lineas = File.ReadAllLines(archivo);
        Assert.Contains("Puesto 3 · Comedor Norte", lineas);
        Assert.Contains("Comensal: " + Persona, lineas);
        Assert.Contains(FrasesComprobante.Para(ticket), lineas);
    }

    private IRenderedComponent<PantallaAtencion> Dibujar(IImpresoraComprobantes impresora)
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, impresora);
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        return _contexto.Render<PantallaAtencion>();
    }

    private static async Task Confirmar(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await Elegir(pantalla);
        await pantalla.Find("#confirmar").ClickAsync(new());
    }

    private static async Task Elegir(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion").Single(o => o.QuerySelector(".nombre")!.TextContent.Trim() == Opcion).ClickAsync(new());
    }

    /// <summary>El cupo de ALMUERZO del día de negocio de la persona queda usado después de que se le ofreció el almuerzo.</summary>
    private void GastarElCupoDeAlmuerzoDeLaPersona()
    {
        using var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            INSERT INTO tbComedorUsosCupo (comperCodigo, comacCodigo, comcatCodigo, comucFechaNegocio, comucUsado, comucVersion)
            VALUES ((SELECT comperCodigo FROM tbComedorCredencialesPersonas WHERE comcreValorNormalizado = '0001'),
                    $amb, $cat, '2026-10-30', 1, 1)
            """;
        comando.Parameters.AddWithValue("$amb", Guid.Parse("00000000-0000-4000-8000-000000000006"));
        comando.Parameters.AddWithValue("$cat", Guid.Parse("00000000-0000-4000-8000-000000000007"));
        comando.ExecuteNonQuery();
    }
}
