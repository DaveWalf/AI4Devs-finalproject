using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// «Guardar» con una carpeta de datos que no admite escritura (sugerencia 4 de la verificación del 03/11): el requisito
/// «Estilo por defecto y validado» pide seguir atendiendo, así que el error no sale del handler. El estilo guardado queda
/// como estaba, el diálogo avisa y la atención sigue.
/// </summary>
public class GuardarEstiloSinEscrituraTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "incomedor-tests", "estilo-sin-escritura-" + Guid.NewGuid().ToString("N"));
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public GuardarEstiloSinEscrituraTests()
    {
        Directory.CreateDirectory(_raiz);
    }

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
        foreach (var archivo in Directory.EnumerateFiles(_raiz, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(archivo, FileAttributes.Normal);
        }

        Directory.Delete(_raiz, recursive: true);
    }

    [Theory(DisplayName = "presentacion-puesto · «Guardar» sin poder escribir: el estilo guardado no cambia, el diálogo avisa y la atención sigue")]
    [InlineData("carpeta-es-un-archivo")]
    [InlineData("archivo-de-solo-lectura")]
    public async Task Guardar_sin_escritura_avisa_y_sigue(string caso)
    {
        var carpeta = PrepararCarpeta(caso, out var archivoEstilo);
        var antes = File.Exists(archivoEstilo) ? File.ReadAllText(archivoEstilo) : null;
        var pantalla = Dibujar(carpeta);
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#opacidad").InputAsync(new ChangeEventArgs { Value = "84" });
        await pantalla.Find("#desenfoque").InputAsync(new ChangeEventArgs { Value = "8" });

        await pantalla.Find("#guardar-configuracion").ClickAsync(new());

        var dialogo = pantalla.Find("[role=\"dialog\"]");
        Assert.Equal(MensajesAtencion.EstiloNoGuardado, dialogo.QuerySelector("[role=\"alert\"]")?.TextContent.Trim());
        Assert.Equal(antes, File.Exists(archivoEstilo) ? File.ReadAllText(archivoEstilo) : null);

        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("[role=\"dialog\"]"));
        Assert.Contains("--opacidad-superficie: 0.76", pantalla.Find(".puesto").GetAttribute("style"));
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll(".opcion"));
    }

    /// <summary>
    /// Dos formas de una carpeta sin escritura que se reproducen sin permisos del sistema: la ruta de la carpeta es un
    /// archivo (IOException al crearla) o el estilo guardado es de sólo lectura (UnauthorizedAccessException al pisarlo).
    /// </summary>
    private string PrepararCarpeta(string caso, out string archivoEstilo)
    {
        if (caso == "carpeta-es-un-archivo")
        {
            var carpeta = Path.Combine(_raiz, "datos");
            File.WriteAllText(carpeta, "no es una carpeta");
            archivoEstilo = Path.Combine(carpeta, "estilo-visual.json");
            return carpeta;
        }

        new AlmacenEstiloVisual(_raiz).Guardar(new EstiloVisual(76, 4));
        archivoEstilo = Path.Combine(_raiz, "estilo-visual.json");
        File.SetAttributes(archivoEstilo, FileAttributes.ReadOnly);
        return _raiz;
    }

    private IRenderedComponent<PantallaAtencion> Dibujar(string carpeta)
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba());
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        _contexto.Services.AddSingleton(new AlmacenEstiloVisual(carpeta));
        return _contexto.Render<PantallaAtencion>();
    }
}
