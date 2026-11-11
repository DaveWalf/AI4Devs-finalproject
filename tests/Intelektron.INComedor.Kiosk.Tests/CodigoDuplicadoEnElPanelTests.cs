using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El aviso del panel ante un código ya usado (escenario «Código ya usado, aunque esté revocado» de administracion-local,
/// advertencia de la verificación de administrar-personas-y-opciones): el servicio ya lo rechazaba con DUPLICATE_CODE, y
/// ninguna prueba miraba lo que dice el panel. Sólo usa los id y las clases que el panel conserva al pasar a tabla.
/// </summary>
public class CodigoDuplicadoEnElPanelTests : IDisposable
{
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
    }

    [Fact(DisplayName = "administracion-local · Código ya usado, aunque esté revocado: con 0003, de una baja, el panel dice «Ese código ya existe. Usá otro», sin decir de quién es, señala el código y no se crea ninguna versión")]
    public async Task Codigo_ya_usado_aunque_este_revocado()
    {
        var pantalla = Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#abrir-personas").ClickAsync(new());
        await pantalla.Find("#agregar-persona").ClickAsync(new());
        await pantalla.FindAll("#panel-personas input.nombre-persona").Last().InputAsync(new ChangeEventArgs { Value = "Persona sintética 06" });
        await pantalla.Find("#panel-personas input.codigo-persona").InputAsync(new ChangeEventArgs { Value = "0003" });

        await pantalla.Find("#guardar-panel").ClickAsync(new());

        var aviso = pantalla.Find("#aviso-panel").TextContent.Trim();
        Assert.Equal("Ese código ya existe. Usá otro.", aviso);
        Assert.DoesNotContain("Persona sintética 03", aviso);
        Assert.Equal("true", pantalla.Find("#panel-personas input.codigo-persona").GetAttribute("aria-invalid"));
        Assert.Equal(1L, Versiones());
    }

    private IRenderedComponent<PantallaAtencion> Dibujar()
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: true);
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        return _contexto.Render<PantallaAtencion>();
    }

    private long Versiones()
    {
        using var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM tbComedorVersionesConfiguracion";
        return Convert.ToInt64(comando.ExecuteScalar());
    }
}
