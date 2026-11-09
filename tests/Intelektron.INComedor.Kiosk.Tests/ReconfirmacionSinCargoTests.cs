using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La reconfirmación cuando el valor vigente quedó sin cargo (D2 de presentar-atencion-tactil): sin importe, sin su
/// separador. Con bUnit sobre un <see cref="ServicioAtencion"/> real y una base temporal.
/// </summary>
public class ReconfirmacionSinCargoTests : IDisposable
{
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
    }

    [Fact(DisplayName = "atencion-puesto · Opción sin cargo: si el valor vigente pasa a cero, la reconfirmación no muestra importe ni separador")]
    public async Task Reconfirmacion_con_cargo_vigente_cero()
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba());
        Assert.Equal(Demostracion.EstadoPreparacion.Lista, servicio.Preparar());
        var maquina = new MaquinaAtencion(servicio);
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(maquina);
        var pantalla = _contexto.Render<PantallaAtencion>();

        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion").Single(o => o.QuerySelector(".nombre")!.TextContent.Trim() == "Almuerzo común")
            .ClickAsync(new());
        SinCargo(SemillaAtencion.PrimeraOpcionId);
        await pantalla.Find("#confirmar").ClickAsync(new());

        Assert.Equal(EstadoAtencion.Reconfirmacion, maquina.Estado);
        var vigente = pantalla.Find("#vigente");
        Assert.Contains("Almuerzo común", vigente.TextContent);
        Assert.DoesNotContain("·", vigente.TextContent);
        Assert.DoesNotContain("ARS", vigente.TextContent);
        Assert.Empty(pantalla.FindAll("#cargo-vigente"));
    }

    private void SinCargo(Guid opcion)
    {
        using var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "UPDATE tbComedorOpcionesMenu SET commenVersionMenu = 2, commenImporteEmpleadoUnidadesMenores = 0 WHERE commenCodigo = $men";
        comando.Parameters.AddWithValue("$men", opcion.ToString("D"));
        Assert.Equal(1, comando.ExecuteNonQuery());
    }
}
