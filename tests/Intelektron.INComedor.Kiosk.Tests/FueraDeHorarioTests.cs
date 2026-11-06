using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La atención con franjas horarias (atencion-puesto y habilitacion-local de administrar-personas-y-opciones): la máquina de
/// estados y, con bUnit, la pantalla, sobre la base de la demostración con el catálogo de nueve. El reloj empieza el
/// 30/10/2026 a las 12:00 de Buenos Aires (15:00 UTC).
/// </summary>
public class FueraDeHorarioTests : IDisposable
{
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
    }

    [Fact(DisplayName = "atencion-puesto · Fuera de horario: con todas las opciones de 11:00 a 14:00, 0001 a las 16:00 ve «No tenés opciones disponibles ahora» y «No hay opciones disponibles en este horario», sin «Hoy ya usaste:», opciones ni «Confirmar»")]
    public async Task Fuera_de_horario_sin_opciones()
    {
        var servicio = NuevoServicio();
        var maquina = new MaquinaAtencion(servicio);
        maquina.Iniciar();
        FranjaEnTodas("11:00", "14:00");
        _reloj.Avanzar(TimeSpan.FromHours(4));
        var pantalla = Dibujar(servicio, maquina);

        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());

        Assert.Equal(EstadoAtencion.SinOpciones, maquina.Estado);
        Assert.Equal("No hay opciones disponibles en este horario", maquina.MotivoSinOpciones);
        Assert.Null(maquina.UsoDelDia);
        Assert.Equal("No tenés opciones disponibles ahora", pantalla.Find("#mensaje").TextContent.Trim());
        Assert.Equal("No hay opciones disponibles en este horario", pantalla.Find("#motivo-sin-opciones").TextContent.Trim());
        Assert.Empty(pantalla.FindAll("#uso-del-dia"));
        Assert.Empty(pantalla.FindAll(".opcion"));
        Assert.Empty(pantalla.FindAll("#confirmar"));
    }

    [Fact(DisplayName = "habilitacion-local · La franja cierra entre la oferta y la confirmación: elegida a las 13:59 y confirmada a las 14:00, la pantalla muestra «Esta opción no está disponible en este horario» y no hay consumo")]
    public async Task Rechazo_por_horario_al_confirmar()
    {
        var maquina = new MaquinaAtencion(NuevoServicio());
        maquina.Iniciar();
        FranjaEnTodas("11:00", "14:00");
        _reloj.Avanzar(TimeSpan.FromMinutes(119));
        Assert.Equal(ResultadoToque.Aceptado, maquina.Identificar("0001"));
        maquina.Elegir(maquina.PaginaActual.Single(o => o.Nombre == "Almuerzo opcional").Id);

        _reloj.Avanzar(TimeSpan.FromMinutes(1));
        await maquina.ConfirmarAsync();

        Assert.Equal(EstadoAtencion.Resultado, maquina.Estado);
        Assert.False(maquina.ConsumoRegistrado);
        Assert.Equal("SERVICE_NOT_ENABLED", maquina.MotivoResultado);
        Assert.Equal("Esta opción no está disponible en este horario", maquina.Mensaje);
        Assert.Equal(0L, Contar("tbComedorConsumos"));
    }

    private ServicioAtencion NuevoServicio() => new(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: true);

    private IRenderedComponent<PantallaAtencion> Dibujar(ServicioAtencion servicio, MaquinaAtencion maquina)
    {
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(maquina);
        return _contexto.Render<PantallaAtencion>();
    }

    /// <summary>Una franja en cada opción de la versión activa, insertada por SQL.</summary>
    private void FranjaEnTodas(string inicio, string fin)
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            INSERT INTO tbComedorFranjasOpcionesMenu (comvcCodigo, commenCodigo, comfraCodigo, comfraHoraInicio, comfraHoraFin)
            SELECT comvcCodigo, commenCodigo, lower(hex(randomblob(16))), $inicio, $fin FROM tbComedorOpcionesMenu
            WHERE comvcCodigo = (SELECT comvcCodigoActiva FROM tbComedorConfiguracionPuestos)
            """;
        comando.Parameters.AddWithValue("$inicio", inicio);
        comando.Parameters.AddWithValue("$fin", fin);
        comando.ExecuteNonQuery();
    }

    private long Contar(string tabla)
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT COUNT(*) FROM {tabla}";
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private SqliteConnection Abrir()
    {
        var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
        conexion.Open();
        return conexion;
    }
}
