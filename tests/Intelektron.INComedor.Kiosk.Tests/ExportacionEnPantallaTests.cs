using System.Text.Json;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La pestaña Exportación (exportacion-consumos y configuracion-puesto de exportar-consumos-automaticamente, D6): antes de
/// General, guarda y valida la configuración, registra CONSUMPTION_EXPORT, «Exportar ahora» corre una pasada y muestra el
/// estado de cada destino; con un operador no existe. bUnit sobre el puesto de prueba con la base de la demostración.
/// </summary>
public class ExportacionEnPantallaTests
{
    [Fact(DisplayName = "exportacion-consumos · Guardar la exportación: activa cada 15 minutos, un archivo por día, una carpeta y el SFTP simulado se guardan, se avisa y la general tiene CONSUMPTION_EXPORT con el antes y el después")]
    public async Task Guardar_la_exportacion()
    {
        using var puesto = Preparar();
        var destino = Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "destino")).FullName;
        var pantalla = puesto.Dibujar();
        await AbrirExportacion(pantalla);

        await pantalla.Find("#exportacion-activa").ClickAsync(new());
        await pantalla.Find("#minutos-exportacion").InputAsync(new ChangeEventArgs { Value = "15" });
        await pantalla.Find("#archivo-por-dia").ClickAsync(new());
        await pantalla.Find("#carpeta-exportacion").InputAsync(new ChangeEventArgs { Value = destino });
        await pantalla.Find("#sftp-activo").ClickAsync(new());
        await pantalla.Find("#servidor-sftp").InputAsync(new ChangeEventArgs { Value = "sftp.ejemplo.local" });
        await pantalla.Find("#usuario-sftp").InputAsync(new ChangeEventArgs { Value = "comedor" });
        await pantalla.Find("#carpeta-remota-sftp").InputAsync(new ChangeEventArgs { Value = "entrada" });
        await pantalla.Find("#guardar-exportacion").ClickAsync(new());

        Assert.Equal(MensajesAtencion.ExportacionGuardada, pantalla.Find("#aviso-guardado").TextContent.Trim());
        var guardada = new AlmacenConfiguracionExportacion(puesto.Carpeta).Leer();
        Assert.Equal(new ConfiguracionExportacion(true, 15, ArchivoExportacion.PorDia, destino, true, "sftp.ejemplo.local", 22, "comedor", "entrada"), guardada);
        var contexto = ContextoDe(puesto.General().Single(l => Tipo(l) == "CONFIGURATION_CHANGED" && ContextoDe(l).GetProperty("Setting").GetString() == "CONSUMPTION_EXPORT"));
        Assert.Equal("APPLIED", contexto.GetProperty("Result").GetString());
        Assert.False(contexto.GetProperty("Before").GetProperty("Active").GetBoolean());
        Assert.True(contexto.GetProperty("After").GetProperty("Active").GetBoolean());
        Assert.Equal("DAILY", contexto.GetProperty("After").GetProperty("File").GetString());
    }

    [Fact(DisplayName = "exportacion-consumos · Datos fuera de forma: 2 minutos, una carpeta relativa y una carpeta remota con .. se señalan y la configuración no cambia")]
    public async Task Datos_fuera_de_forma()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await AbrirExportacion(pantalla);

        await pantalla.Find("#exportacion-activa").ClickAsync(new());
        await pantalla.Find("#minutos-exportacion").InputAsync(new ChangeEventArgs { Value = "2" });
        await pantalla.Find("#carpeta-exportacion").InputAsync(new ChangeEventArgs { Value = "relativa" });
        await pantalla.Find("#sftp-activo").ClickAsync(new());
        await pantalla.Find("#servidor-sftp").InputAsync(new ChangeEventArgs { Value = "sftp.ejemplo.local" });
        await pantalla.Find("#usuario-sftp").InputAsync(new ChangeEventArgs { Value = "comedor" });
        await pantalla.Find("#carpeta-remota-sftp").InputAsync(new ChangeEventArgs { Value = "entrada/../otra" });
        await pantalla.Find("#guardar-exportacion").ClickAsync(new());

        Assert.Equal("true", pantalla.Find("#minutos-exportacion").GetAttribute("aria-invalid"));
        Assert.Equal("true", pantalla.Find("#carpeta-exportacion").GetAttribute("aria-invalid"));
        Assert.Equal("true", pantalla.Find("#carpeta-remota-sftp").GetAttribute("aria-invalid"));
        Assert.Null(pantalla.Find("#servidor-sftp").GetAttribute("aria-invalid"));
        Assert.Equal(MensajesAtencion.PanelInvalido, pantalla.Find("#aviso-exportacion").TextContent.Trim());
        Assert.Equal(ConfiguracionExportacion.PorDefecto, new AlmacenConfiguracionExportacion(puesto.Carpeta).Leer());
    }

    [Fact(DisplayName = "exportacion-consumos · El servidor cae y vuelve: «Exportar ahora» con el SFTP simulado caído exporta a la carpeta y dice la falla del SFTP; levantado, lo deja al día")]
    public async Task Exportar_ahora_con_el_sftp_caido()
    {
        using var puesto = Preparar();
        var destino = Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "destino")).FullName;
        new AlmacenConfiguracionExportacion(puesto.Carpeta).Guardar(ConfiguracionExportacion.PorDefecto with
        {
            Activa = true, Carpeta = destino, SftpActivo = true, Servidor = "sftp.ejemplo.local", Usuario = "comedor", CarpetaRemota = "entrada",
        });
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla);
        await AbrirExportacion(pantalla);

        await pantalla.Find("#sftp-caido").ClickAsync(new());
        await pantalla.Find("#exportar-ahora").ClickAsync(new());

        Assert.Contains("No se pudo exportar al SFTP simulado: el servidor no responde.", pantalla.Find("#mensaje-exportacion-automatica").TextContent);
        Assert.Contains("último envío", pantalla.Find("#estado-exportacion-carpeta").TextContent);
        Assert.Contains("falló", pantalla.Find("#estado-exportacion-sftp").TextContent);
        Assert.Single(Directory.GetFiles(destino));

        await pantalla.Find("#sftp-disponible").ClickAsync(new());
        await pantalla.Find("#exportar-ahora").ClickAsync(new());

        Assert.Equal("Se exportó 1 consumo.", pantalla.Find("#mensaje-exportacion-automatica").TextContent.Trim());
        Assert.Contains("último envío", pantalla.Find("#estado-exportacion-sftp").TextContent);
        Assert.True(File.Exists(Path.Combine(puesto.Carpeta, "sftp-simulado", "sftp.ejemplo.local", "entrada", "INComedor-consumos.csv")));
    }

    [Fact(DisplayName = "exportacion-consumos · Guardar la exportación: sin ningún destino guardado, «Exportar ahora» lo dice en lugar de decir que no había consumos")]
    public async Task Exportar_ahora_sin_destinos()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await Consumir(pantalla);
        await AbrirExportacion(pantalla);

        await pantalla.Find("#exportar-ahora").ClickAsync(new());

        Assert.Equal(MensajesAtencion.ExportacionSinDestinos, pantalla.Find("#mensaje-exportacion-automatica").TextContent.Trim());
    }

    [Fact(DisplayName = "exportacion-consumos · Guardar la exportación: con el código de un operador no existe la pestaña Exportación")]
    public async Task Con_un_operador()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();

        await EntradaConfiguracion.Entrar(pantalla, "9001");

        Assert.Empty(pantalla.FindAll("#pestana-exportacion"));
        Assert.Empty(pantalla.FindAll("#seccion-exportacion"));
    }

    private static PuestoDePrueba Preparar()
    {
        var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        puesto.Contexto.Services.AddSingleton(new AlmacenConfiguracionExportacion(puesto.Carpeta));
        return puesto;
    }

    private static async Task AbrirExportacion(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-exportacion").ClickAsync(new());
    }

    private static async Task Consumir(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        await pantalla.Find("#listo").ClickAsync(new());
    }
}
