using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La pestaña Mails (avisos-correo y configuracion-puesto de avisar-por-correo, D7): después de Impresora, guarda y valida
/// la configuración sin ninguna clave, registra MAIL sin direcciones, «Enviar una prueba» pasa por la cola con el servidor
/// simulado caído o disponible, y con un operador no existe. bUnit sobre el puesto de prueba con la base de la demostración.
/// </summary>
public class CorreoEnPantallaTests
{
    [Fact(DisplayName = "avisos-correo · Guardar los correos: activos, con el cliente, el remitente, el servidor con STARTTLS y contraseña de aplicación, dos operadores y el Administrador Principal, se guardan sin ninguna clave y la general tiene MAIL sin direcciones")]
    public async Task Guardar_los_correos()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirMails(pantalla);

        await pantalla.Find("#correos-activos").ClickAsync(new());
        await Escribir(pantalla, "#cliente-correo", "Cliente Demo");
        await Escribir(pantalla, "#remitente-correo", "puesto@ejemplo.com");
        await Escribir(pantalla, "#servidor-correo", "smtp.ejemplo.local");
        await pantalla.Find("#cifrado-starttls").ClickAsync(new());
        await pantalla.Find("#factor-aplicacion").ClickAsync(new());
        await Escribir(pantalla, "#operador-correo-1", "operador1@ejemplo.com");
        await Escribir(pantalla, "#operador-correo-3", "operador2@ejemplo.com");
        await Escribir(pantalla, "#administrador-correo", "admin@ejemplo.com");
        await pantalla.Find("#guardar-correo").ClickAsync(new());

        Assert.Equal(MensajesAtencion.CorreoGuardado, pantalla.Find("#aviso-guardado").TextContent.Trim());
        var guardada = new AlmacenConfiguracionCorreo(puesto.Carpeta).Leer();
        Assert.Equal(ConfiguracionCorreo.PorDefecto with
        {
            Activos = true, Cliente = "Cliente Demo", Remitente = "puesto@ejemplo.com", Servidor = "smtp.ejemplo.local",
            Operadores = ["operador1@ejemplo.com", "operador2@ejemplo.com"], Administrador = "admin@ejemplo.com",
        }, guardada);
        var registro = puesto.General().Single(l => Tipo(l) == "CONFIGURATION_CHANGED" && ContextoDe(l).GetProperty("Setting").GetString() == "MAIL");
        var contexto = ContextoDe(registro);
        Assert.Equal("APPLIED", contexto.GetProperty("Result").GetString());
        Assert.False(contexto.GetProperty("Before").GetProperty("Active").GetBoolean());
        Assert.True(contexto.GetProperty("After").GetProperty("Active").GetBoolean());
        Assert.Equal(2, contexto.GetProperty("After").GetProperty("Operators").GetInt32());
        Assert.True(contexto.GetProperty("RecipientsChanged").GetBoolean());
        Assert.DoesNotContain("@", registro.GetRawText(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "avisos-correo · Casillas fuera de forma: una casilla de operador sin arroba, el puerto 0 y la hora 25:00 se señalan en su campo y la configuración no cambia")]
    public async Task Casillas_fuera_de_forma()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirMails(pantalla);

        await Escribir(pantalla, "#operador-correo-2", "sin-arroba");
        await Escribir(pantalla, "#puerto-correo", "0");
        await Escribir(pantalla, "#hora-senal", "25:00");
        await pantalla.Find("#guardar-correo").ClickAsync(new());

        Assert.Equal("true", pantalla.Find("#operador-correo-2").GetAttribute("aria-invalid"));
        Assert.Equal("true", pantalla.Find("#puerto-correo").GetAttribute("aria-invalid"));
        Assert.Equal("true", pantalla.Find("#hora-senal").GetAttribute("aria-invalid"));
        Assert.Null(pantalla.Find("#operador-correo-1").GetAttribute("aria-invalid"));
        Assert.Equal(MensajesAtencion.PanelInvalido, pantalla.Find("#aviso-correo").TextContent.Trim());
        Assert.Equal(ConfiguracionCorreo.PorDefecto, new AlmacenConfiguracionCorreo(puesto.Carpeta).Leer());
    }

    [Fact(DisplayName = "avisos-correo · El servidor de correo cae y vuelve: con el servidor simulado caído la prueba queda en la cola y la pestaña lo dice; disponible, otra prueba sale con la de antes y la cola queda vacía")]
    public async Task Enviar_una_prueba_con_el_servidor_caido()
    {
        using var puesto = Preparar();
        new AlmacenConfiguracionCorreo(puesto.Carpeta).Guardar(ConfiguracionCorreo.PorDefecto with
        {
            Activos = true, Cliente = "Cliente Demo", Remitente = "puesto@ejemplo.com", Servidor = "smtp.ejemplo.local",
            Operadores = ["operador1@ejemplo.com"], Administrador = "admin@ejemplo.com", HoraSenal = "23:59",
        });
        var pantalla = puesto.Dibujar();
        await AbrirMails(pantalla);

        await pantalla.Find("#servidor-correo-caido").ClickAsync(new());
        await pantalla.Find("#enviar-prueba-correo").ClickAsync(new());

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.PruebaCorreoEnCola, pantalla.Find("#mensaje-correo").TextContent.Trim()));
        Assert.Equal(MensajesAtencion.EstadoColaCorreo(1), pantalla.Find("#estado-correo").TextContent.Trim());
        Assert.Contains(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED" && ContextoDe(l).GetProperty("Setting").GetString() == "SIMULATED_MAIL");

        await pantalla.Find("#servidor-correo-disponible").ClickAsync(new());
        await pantalla.Find("#enviar-prueba-correo").ClickAsync(new());

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.PruebaCorreoEnviada(2), pantalla.Find("#mensaje-correo").TextContent.Trim()));
        Assert.Equal(MensajesAtencion.EstadoColaCorreo(0), pantalla.Find("#estado-correo").TextContent.Trim());
        Assert.Equal(2, Directory.GetFiles(Path.Combine(puesto.Carpeta, CorreoSimulado.Carpeta), "*.eml").Length);
    }

    [Fact(DisplayName = "avisos-correo · El servidor de correo cae y vuelve: con los correos apagados, «Enviar una prueba» dice que no hay casillas guardadas")]
    public async Task Enviar_una_prueba_sin_casillas()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirMails(pantalla);

        await pantalla.Find("#enviar-prueba-correo").ClickAsync(new());

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.PruebaCorreoSinCasillas, pantalla.Find("#mensaje-correo").TextContent.Trim()));
    }

    [Fact(DisplayName = "avisos-correo · El servidor de correo cae y vuelve: «Guardar» y enseguida «Enviar una prueba» cuenta las casillas recién guardadas")]
    public async Task Guardar_y_enviar_una_prueba()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirMails(pantalla);

        await pantalla.Find("#correos-activos").ClickAsync(new());
        await Escribir(pantalla, "#remitente-correo", "puesto@ejemplo.com");
        await Escribir(pantalla, "#servidor-correo", "smtp.ejemplo.local");
        await Escribir(pantalla, "#operador-correo-1", "operador1@ejemplo.com");
        await Escribir(pantalla, "#administrador-correo", "admin@ejemplo.com");
        await Escribir(pantalla, "#hora-senal", "23:59");
        await pantalla.Find("#guardar-correo").ClickAsync(new());
        await pantalla.Find("#enviar-prueba-correo").ClickAsync(new());

        pantalla.WaitForAssertion(() => Assert.Equal(MensajesAtencion.PruebaCorreoEnviada(2), pantalla.Find("#mensaje-correo").TextContent.Trim()));
    }

    [Fact(DisplayName = "avisos-correo · La exportación que falla: con los correos activos, «Exportar ahora» con el SFTP simulado caído deja un aviso de «Exportación» para el Administrador Principal")]
    public async Task La_exportacion_avisa_por_correo()
    {
        using var puesto = Preparar();
        puesto.Contexto.Services.AddSingleton(new AlmacenConfiguracionExportacion(puesto.Carpeta));
        new AlmacenConfiguracionExportacion(puesto.Carpeta).Guardar(ConfiguracionExportacion.PorDefecto with
        {
            Activa = true, SftpActivo = true, Servidor = "sftp.ejemplo.local", Usuario = "comedor", CarpetaRemota = "entrada",
        });
        new AlmacenConfiguracionCorreo(puesto.Carpeta).Guardar(ConfiguracionCorreo.PorDefecto with
        {
            Activos = true, Cliente = "Cliente Demo", Remitente = "puesto@ejemplo.com", Servidor = "smtp.ejemplo.local",
            Administrador = "admin@ejemplo.com", HoraSenal = "23:59",
        });
        var pantalla = puesto.Dibujar();
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        await pantalla.Find("#listo").ClickAsync(new());
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-exportacion").ClickAsync(new());

        await pantalla.Find("#sftp-caido").ClickAsync(new());
        await pantalla.Find("#exportar-ahora").ClickAsync(new());

        // El aviso se encola fuera del hilo de la pantalla: se espera por tiempo, no por un dibujo.
        var carpeta = Path.Combine(puesto.Carpeta, ColaCorreo.Carpeta);
        // Un pendiente recién creado puede estar tomado un instante (el antivirus): se vuelve a mirar, como hace la cola.
        static string? Leer(string archivo)
        {
            try
            {
                return File.ReadAllText(archivo);
            }
            catch (IOException)
            {
                return null;
            }
        }

        bool HayAviso() => Directory.Exists(carpeta) && Directory.GetFiles(carpeta, "*.json").Select(Leer).OfType<string>().Any(t =>
            System.Text.Json.JsonSerializer.Deserialize<MensajeCorreo>(t) is { Situacion: SituacionCorreo.Exportacion } m && m.Para.SequenceEqual(["admin@ejemplo.com"]));
        Assert.True(SpinWait.SpinUntil(HayAviso, TimeSpan.FromSeconds(10)), "No llegó el aviso de «Exportación» a la cola.");
    }

    [Fact(DisplayName = "avisos-correo · Guardar los correos: con el código de un operador no existe la pestaña Mails")]
    public async Task Con_un_operador()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();

        await EntradaConfiguracion.Entrar(pantalla, "9001");

        Assert.Empty(pantalla.FindAll("#pestana-mails"));
        Assert.Empty(pantalla.FindAll("#seccion-mails"));
    }

    private static PuestoDePrueba Preparar()
    {
        var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        puesto.Contexto.Services.AddSingleton(new AlmacenConfiguracionCorreo(puesto.Carpeta));
        return puesto;
    }

    private static async Task AbrirMails(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-mails").ClickAsync(new());
    }

    private static Task Escribir(IRenderedComponent<PantallaAtencion> pantalla, string selector, string valor) =>
        pantalla.Find(selector).InputAsync(new ChangeEventArgs { Value = valor });
}
