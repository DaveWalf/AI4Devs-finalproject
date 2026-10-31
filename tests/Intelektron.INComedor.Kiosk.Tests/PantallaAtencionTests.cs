using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La pantalla de la atención con bUnit sobre un <see cref="ServicioAtencion"/> real y un archivo SQLite temporal; los
/// únicos dobles son la impresora y el reloj manual (D12 de atender-seleccion-y-confirmacion).
/// </summary>
/// <remarks>
/// Cada toque se espera con su versión asíncrona: el temporizador de la pantalla entra al despachador cada segundo, y
/// un <c>Click()</c> síncrono que lo encuentra ocupado vuelve sin haber corrido el manejador (corrida 41141401980).
/// </remarks>
public class PantallaAtencionTests : IDisposable
{
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");
    private readonly ImpresoraDePrueba _impresora = new();

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
    }

    private IRenderedComponent<PantallaAtencion> Dibujar()
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, _impresora);
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        return _contexto.Render<PantallaAtencion>();
    }

    [Fact(DisplayName = "TK02.1 · Opciones y navegación alcanzables con el teclado: cada opción, «Anterior», «Siguiente», «Cancelar» y «Confirmar» son botones sin tabindex negativo")]
    public async Task Controles_de_la_seleccion_son_botones()
    {
        var pantalla = Dibujar();
        await Ingresar(pantalla, "0001");

        var controles = pantalla.FindAll(".opcion").Concat(new[] { "#anterior", "#siguiente", "#cancelar", "#confirmar" }.Select(pantalla.Find)).ToList();

        Assert.Equal(6 + 4, controles.Count);
        Assert.All(controles, c =>
        {
            Assert.Equal("BUTTON", c.TagName);
            Assert.Equal("button", c.GetAttribute("type"));
            Assert.False(c.HasAttribute("disabled"));
            Assert.NotEqual("-1", c.GetAttribute("tabindex"));
        });
        Assert.Equal("true", pantalla.Find("#anterior").GetAttribute("aria-disabled"));
    }

    [Fact(DisplayName = "AT52 · AT01 · Código vigente abre la selección (pantalla): muestra el nombre visible y el catálogo, y el campo del código queda vacío")]
    public async Task Codigo_vigente_muestra_el_nombre_y_el_catalogo()
    {
        var pantalla = Dibujar();

        await Ingresar(pantalla, "0001");

        Assert.Contains("Persona sintética 01", pantalla.Find("#saludo").TextContent);
        Assert.Equal(6, pantalla.FindAll(".opcion").Count);
        await pantalla.Find("#cancelar").ClickAsync(new());
        Assert.Equal("", pantalla.Find("#codigo").GetAttribute("value") ?? "");
    }

    [Fact(DisplayName = "AT52 · Rechazo de un código (pantalla): cada motivo muestra su mensaje, vuelve a reposo con el campo vacío y la pantalla no contiene el código")]
    public async Task Cada_rechazo_muestra_su_mensaje_sin_el_codigo()
    {
        var pantalla = Dibujar();
        var esperados = new Dictionary<string, string>
        {
            ["0002"] = MensajesAtencion.DeMotivo("CREDENTIAL_EXPIRED"),
            ["0003"] = MensajesAtencion.DeMotivo("CREDENTIAL_REVOKED"),
            ["0004"] = MensajesAtencion.DeMotivo("IDENTITY_NOT_RESOLVED"),
            ["SINTETICO-NINGUNO-77"] = MensajesAtencion.DeMotivo("IDENTITY_NOT_RESOLVED"),
        };

        foreach (var (codigo, mensaje) in esperados)
        {
            await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = codigo });
            await pantalla.Find("#ingresar").ClickAsync(new());

            Assert.Equal(mensaje, pantalla.Find("#aviso").TextContent.Trim());
            Assert.Equal("", pantalla.Find("#codigo").GetAttribute("value") ?? "");
            Assert.DoesNotContain(codigo, TextoVisible(pantalla));
            Assert.Empty(pantalla.FindAll(".opcion"));
        }

        Assert.Equal(3, esperados.Values.Distinct().Count());
    }

    [Fact(DisplayName = "AT01 · HU01-A · Autorización sin red (pantalla): «Consumo registrado. Retirá tu comprobante» y «Listo» vuelve a reposo sin el nombre")]
    public async Task Autorizacion_muestra_el_mensaje_y_listo_limpia()
    {
        var pantalla = Dibujar();
        await Ingresar(pantalla, "0001");
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());

        await pantalla.Find("#confirmar").ClickAsync(new());

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.Exito, pantalla.Find("#mensaje").TextContent.Trim()));
        await pantalla.Find("#listo").ClickAsync(new());
        Assert.NotNull(pantalla.Find("#codigo"));
        Assert.DoesNotContain("Persona sintética 01", pantalla.Markup);
    }

    [Fact(DisplayName = "AT06 · Impresora sin papel (pantalla): «Tu consumo está registrado. El comprobante queda pendiente»")]
    public async Task Sin_papel_muestra_el_comprobante_pendiente()
    {
        _impresora.Modo = ModoImpresoraDePrueba.SinPapel;
        var pantalla = Dibujar();
        await Ingresar(pantalla, "0001");
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());

        await pantalla.Find("#confirmar").ClickAsync(new());

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.ComprobantePendiente, pantalla.Find("#mensaje").TextContent.Trim()));
    }

    [Fact(DisplayName = "AT26 · Confirmación en curso (pantalla): el puesto se muestra ocupado y «Confirmar» queda ocupado hasta el resultado")]
    public async Task Confirmacion_en_curso_muestra_ocupado()
    {
        var pantalla = Dibujar();
        await Ingresar(pantalla, "0001");
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());

        using (var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False"))
        {
            conexion.Open();
            using var bloqueo = conexion.BeginTransaction(deferred: false);
            // Sin esperar: la confirmación queda trabada en el bloqueo, y lo que sigue la espera con WaitForAssertion.
            pantalla.Find("#confirmar").Click();

            pantalla.WaitForAssertion(() => Assert.Contains("ocupado", pantalla.Find("#ocupado").TextContent, StringComparison.OrdinalIgnoreCase));
            Assert.Equal("true", pantalla.Find("#confirmar").GetAttribute("aria-busy"));
            Assert.True(pantalla.Find("#confirmar").HasAttribute("disabled"));
        }

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.Exito, pantalla.Find("#mensaje").TextContent.Trim()), TimeSpan.FromSeconds(10));
    }

    [Fact(DisplayName = "HU03-B · Resultado incierto (pantalla): el aviso de que el papel pudo haber salido aparece antes de pedir el motivo")]
    public async Task Aviso_de_incertidumbre_antes_del_motivo()
    {
        _impresora.Modo = ModoImpresoraDePrueba.Incierta;
        var pantalla = Dibujar();
        await Ingresar(pantalla, "0001");
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        await pantalla.Find("#listo").ClickAsync(new());
        await EntrarAsistencia(pantalla, "9001");

        await pantalla.Find(".reimprimir").ClickAsync(new());

        var markup = pantalla.Markup;
        Assert.Equal(MensajesAtencion.Incertidumbre, pantalla.Find("#aviso-incertidumbre").TextContent.Trim());
        Assert.Equal(4, pantalla.FindAll(".motivo").Count);
        Assert.True(markup.IndexOf("aviso-incertidumbre", StringComparison.Ordinal) < markup.IndexOf("class=\"motivo", StringComparison.Ordinal));
        _impresora.Modo = ModoImpresoraDePrueba.Disponible;
        await pantalla.FindAll(".motivo")[1].ClickAsync(new());
        Assert.Contains("Copia 1", pantalla.Find("#mensaje-asistencia").TextContent);
        Assert.Equal("DUPLICADO · copia 1", _impresora.Recibidos[^1].Leyenda);
    }

    [Fact(DisplayName = "HU03-C · Persona sin permiso (pantalla): informa que no tiene permiso y sigue en reposo")]
    public async Task Persona_sin_permiso_sigue_en_reposo()
    {
        var pantalla = Dibujar();

        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#asistencia").ClickAsync(new());

        Assert.Equal(MensajesAtencion.DeMotivo("PERMISSION_MISSING"), pantalla.Find("#aviso").TextContent.Trim());
        Assert.NotNull(pantalla.Find("#codigo"));
        Assert.Empty(pantalla.FindAll(".reimprimir"));
    }

    [Fact(DisplayName = "RB26 · Salir de la asistencia (pantalla): vuelve a reposo sin privilegios ni el nombre del operador")]
    public async Task Salir_de_la_asistencia_vuelve_a_reposo()
    {
        var pantalla = Dibujar();
        await EntrarAsistencia(pantalla, "9001");
        Assert.Contains("Operador sintético 01", pantalla.Markup);

        await pantalla.Find("#salir").ClickAsync(new());

        Assert.NotNull(pantalla.Find("#codigo"));
        Assert.DoesNotContain("Operador sintético 01", pantalla.Markup);
        Assert.Empty(pantalla.FindAll("#salir"));
    }

    [Fact(DisplayName = "RB26 · AT54 · El permiso vence durante la sesión (pantalla): reimprimir vuelve a reposo con el aviso de PERMISSION_EXPIRED")]
    public async Task Permiso_que_vence_durante_la_sesion_vuelve_a_reposo()
    {
        var pantalla = Dibujar();
        await Ingresar(pantalla, "0001");
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        await pantalla.Find("#listo").ClickAsync(new());
        using (var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False"))
        {
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "UPDATE tbComedorPermisosOperadores SET compopFechaVigenciaHastaUtc = '2026-10-30T15:00:20.000Z' WHERE compopCodigo = '00000000-0000-4000-8000-000000000045'";
            Assert.Equal(1, comando.ExecuteNonQuery());
        }

        await EntrarAsistencia(pantalla, "9001");
        _reloj.Avanzar(TimeSpan.FromSeconds(20));

        await pantalla.Find(".reimprimir").ClickAsync(new());
        await pantalla.FindAll(".motivo")[0].ClickAsync(new());

        Assert.Equal(MensajesAtencion.DeMotivo("PERMISSION_EXPIRED"), pantalla.Find("#aviso").TextContent.Trim());
        Assert.NotNull(pantalla.Find("#codigo"));
        Assert.DoesNotContain("Operador sintético 01", pantalla.Markup);
    }

    [Fact(DisplayName = "TK03.1 · Base de una versión de esquema desconocida (pantalla): informa el error de almacenamiento y no ofrece el campo del código")]
    public void Base_de_version_desconocida_no_ofrece_identificarse()
    {
        using (var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False"))
        {
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "PRAGMA user_version = 99; CREATE TABLE futura (id INTEGER)";
            comando.ExecuteNonQuery();
        }

        var pantalla = Dibujar();

        Assert.Equal(MensajesAtencion.ErrorAlmacenamiento, pantalla.Find("#error-almacenamiento").TextContent.Trim());
        Assert.Empty(pantalla.FindAll("#codigo"));
        Assert.Empty(pantalla.FindAll("#asistencia"));
    }

    /// <summary>El texto que se ve, con los valores de los campos: todo menos las hojas de estilo.</summary>
    private static string TextoVisible(IRenderedComponent<PantallaAtencion> pantalla)
    {
        var texto = pantalla.Find("main").TextContent;
        foreach (var estilo in pantalla.FindAll("style"))
        {
            texto = texto.Replace(estilo.TextContent, string.Empty, StringComparison.Ordinal);
        }

        return texto + string.Concat(pantalla.FindAll("input").Select(i => i.GetAttribute("value")));
    }

    private static async Task Ingresar(IRenderedComponent<PantallaAtencion> pantalla, string codigo)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = codigo });
        await pantalla.Find("#ingresar").ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll(".opcion"));
    }

    private static async Task EntrarAsistencia(IRenderedComponent<PantallaAtencion> pantalla, string codigo)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = codigo });
        await pantalla.Find("#asistencia").ClickAsync(new());
        Assert.NotNull(pantalla.Find("#salir"));
    }
}
