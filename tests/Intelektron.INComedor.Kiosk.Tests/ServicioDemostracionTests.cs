using System.Security.Cryptography;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Demostracion;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>El servicio de la demostración sobre un archivo SQLite real y temporal, sin dobles (D9).</summary>
public class ServicioDemostracionTests : IDisposable
{
    private readonly ArchivoTemporal _archivo = new();
    private readonly RelojFijo _reloj = new("2026-10-19T15:00:00Z");

    public void Dispose() => _archivo.Dispose();

    private ServicioDemostracion NuevoServicio() => new(_archivo.Ruta, _reloj);

    [Fact(DisplayName = "AT51 · AT01 · Primer arranque sobre una base inexistente: esquema 1 con un puesto, una persona, una opción y una regla de límite 1, sin consumos")]
    public void Primer_arranque_siembra_los_datos_sinteticos()
    {
        var servicio = NuevoServicio();

        var estado = servicio.Preparar();

        Assert.Equal(EstadoPreparacion.Lista, estado);
        using var conexion = Abrir();
        Assert.Equal(1L, Escalar<long>(conexion, "PRAGMA user_version"));
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorConfiguracionPuestos"));
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorPersonas WHERE comperActivo = 1"));
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorOpcionesMenu"));
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorReglasHabilitacion WHERE comregLimite = 1"));
        Assert.Equal(0L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorConsumos"));
    }

    [Fact(DisplayName = "RB06 · Un segundo arranque no duplica ni borra: un puesto, una persona, una opción y el consumo autorizado sigue registrado")]
    public void Segundo_arranque_no_duplica_ni_borra()
    {
        var primero = NuevoServicio();
        primero.Preparar();
        var resultado = primero.Confirmar();
        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);

        var segundo = NuevoServicio();
        var estado = segundo.Preparar();

        Assert.Equal(EstadoPreparacion.Lista, estado);
        using var conexion = Abrir();
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorConfiguracionPuestos"));
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorPersonas"));
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorOpcionesMenu"));
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorConsumos"));
    }

    [Fact(DisplayName = "TK03.1 · Base de una versión de esquema desconocida: ErrorAlmacenamiento y el archivo queda con el mismo SHA-256")]
    public void Base_de_version_desconocida_informa_error_y_no_toca_el_archivo()
    {
        CrearBaseConVersion(_archivo.Ruta, 99);
        var hashAntes = Hash(_archivo.Ruta);

        var estado = NuevoServicio().Preparar();

        Assert.Equal(EstadoPreparacion.ErrorAlmacenamiento, estado);
        Assert.Equal(hashAntes, Hash(_archivo.Ruta));
        Assert.False(File.Exists(_archivo.Ruta + "-wal"));
    }

    [Fact(DisplayName = "AT01 · Primera confirmación del día: AUTHORIZED con identificadores de consumo y comprobante, y un solo consumo en la base")]
    public void Primera_confirmacion_autoriza_y_registra_un_consumo()
    {
        var servicio = NuevoServicio();
        servicio.Preparar();

        var resultado = servicio.Confirmar();

        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        Assert.NotNull(resultado.ConsumptionId);
        Assert.NotNull(resultado.TicketId);
        using var conexion = Abrir();
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorConsumos"));
        Assert.Equal(resultado.ConsumptionId, Guid.Parse(Escalar<string>(conexion, "SELECT comconCodigo FROM tbComedorConsumos")));
        Assert.Equal(resultado.TicketId, Guid.Parse(Escalar<string>(conexion, "SELECT comcmpCodigo FROM tbComedorComprobantes")));
    }

    [Fact(DisplayName = "AT02 · Segunda confirmación con el cupo agotado: DENIED con LOCAL_LIMIT_REACHED y un solo consumo")]
    public void Segunda_confirmacion_es_rechazada_por_cupo()
    {
        var servicio = NuevoServicio();
        servicio.Preparar();
        Assert.Equal(TipoResultado.Autorizada, servicio.Confirmar().Tipo);

        var segunda = servicio.Confirmar();

        Assert.Equal(TipoResultado.Denegada, segunda.Tipo);
        Assert.Equal("LOCAL_LIMIT_REACHED", segunda.ReasonCode);
        using var conexion = Abrir();
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorConsumos"));
    }

    [Fact(DisplayName = "D6 de fijar-primer-corte · La sesión de prueba no lleva código: comcreCodigo NULL y ninguna credencial en la base")]
    public void La_sesion_de_prueba_no_lleva_credencial()
    {
        var servicio = NuevoServicio();
        servicio.Preparar();
        Assert.Equal(TipoResultado.Autorizada, servicio.Confirmar().Tipo);

        using var conexion = Abrir();
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorSesionesIdentidad"));
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorSesionesIdentidad WHERE comcreCodigo IS NULL"));
        Assert.Equal(0L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorCredencialesPersonas"));
    }

    private SqliteConnection Abrir() => BaseComedor.Abrir(_archivo.Ruta).AbrirConexion();

    private static T Escalar<T>(SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        var valor = comando.ExecuteScalar();
        return valor is T ya ? ya : (T)Convert.ChangeType(valor!, typeof(T));
    }

    private static void CrearBaseConVersion(string ruta, int version)
    {
        using var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = $"PRAGMA user_version = {version}; CREATE TABLE futura (id INTEGER)";
        comando.ExecuteNonQuery();
    }

    private static string Hash(string ruta) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ruta)));
}
