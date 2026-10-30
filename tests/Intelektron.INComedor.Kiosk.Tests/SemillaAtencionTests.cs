using Intelektron.INComedor.Domain;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Intelektron.INComedor.Kiosk.Demostracion;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>Conjunto sintético de atención (D10 de atender-seleccion-y-confirmacion), sobre un archivo SQLite real.</summary>
public class SemillaAtencionTests : IDisposable
{
    private readonly ArchivoTemporal _archivo = new();
    private readonly RelojFijo _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose() => _archivo.Dispose();

    private ServicioAtencion NuevoServicio() => new(_archivo.Ruta, _reloj, new ImpresoraDePrueba());

    [Fact(DisplayName = "AT16 · AT52 · AT54 · Primer arranque de la atención: 15 opciones, un código en cada estado, permisos OPERATOR vigente, vencido y revocado y un administrador sin OPERATOR, sin consumos")]
    public void Primer_arranque_siembra_la_atencion()
    {
        var estado = NuevoServicio().Preparar();

        Assert.Equal(EstadoPreparacion.Lista, estado);
        using var conexion = Abrir();
        Assert.Equal(15L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorOpcionesMenu"));
        Assert.Equal(0L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorConsumos"));

        var ahora = _reloj.Ahora;
        Assert.True(Clasificar(conexion, "0001", ahora).Vigente);
        Assert.Equal(MotivoIdentificacion.CredentialExpired, Clasificar(conexion, "0002", ahora).Motivo);
        Assert.Equal(MotivoIdentificacion.CredentialRevoked, Clasificar(conexion, "0003", ahora).Motivo);
        Assert.Equal(CausaIdentificacion.NotYetValid, Clasificar(conexion, "0004", ahora).Causa);

        Assert.True(Permiso(conexion, "9001", ahora).Concedido);
        Assert.Equal(MotivoPermiso.PermissionExpired, Permiso(conexion, "9002", ahora).Motivo);
        Assert.Equal(MotivoPermiso.PermissionRevoked, Permiso(conexion, "9003", ahora).Motivo);
        Assert.Equal(MotivoPermiso.PermissionMissing, Permiso(conexion, "9004", ahora).Motivo);
        Assert.Equal(1L, Escalar<long>(conexion,
            "SELECT COUNT(*) FROM tbComedorPermisosOperadores pop JOIN tbComedorCredencialesPersonas cre ON cre.comperCodigo = pop.comperCodigo WHERE cre.comcreValorNormalizado = '9004' AND pop.compopRol = 'SYSTEM_ADMINISTRATOR' AND pop.compopFechaRevocacionUtc IS NULL"));
    }

    [Fact(DisplayName = "RB06 · Un segundo arranque de la atención no duplica: opciones, personas, credenciales y permisos iguales, y el consumo sigue registrado")]
    public void Segundo_arranque_no_duplica()
    {
        Assert.Equal(EstadoPreparacion.Lista, NuevoServicio().Preparar());
        AutorizarConElCodigo0001();
        var antes = Conteos();

        Assert.Equal(EstadoPreparacion.Lista, NuevoServicio().Preparar());

        Assert.Equal(antes, Conteos());
        using var conexion = Abrir();
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorConsumos"));
    }

    [Fact(DisplayName = "RB06 · Atención sobre una base de la demostración en uso: de la versión 1 a la 3 con los datos de atención, un solo puesto y el consumo anterior")]
    public void Atencion_sobre_una_base_de_la_demostracion_en_version_1()
    {
        var demostracion = new ServicioDemostracion(_archivo.Ruta, _reloj);
        Assert.Equal(EstadoPreparacion.Lista, demostracion.Preparar());
        var consumo = demostracion.Confirmar().ConsumptionId!.Value;
        LlevarAVersion1();

        var estado = NuevoServicio().Preparar();

        Assert.Equal(EstadoPreparacion.Lista, estado);
        using var conexion = Abrir();
        Assert.Equal(3L, Escalar<long>(conexion, "PRAGMA user_version"));
        Assert.Equal(15L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorOpcionesMenu"));
        Assert.Equal(4L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorPermisosOperadores"));
        Assert.Equal(1L, Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorConfiguracionPuestos"));
        Assert.Equal(consumo, Guid.Parse(Escalar<string>(conexion, "SELECT comconCodigo FROM tbComedorConsumos")));
    }

    /// <summary>
    /// Deja la base como la dejaba el código anterior a la 0002: el esquema de la 0001 no cambió, así que alcanza
    /// con quitar la tabla nueva y volver user_version a 1.
    /// </summary>
    private void LlevarAVersion1()
    {
        using var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "DROP TABLE tbComedorLotesSincronizacion_Eventos; DROP TABLE tbComedorLotesSincronizacion; DROP TABLE tbComedorPermisosOperadores; PRAGMA user_version = 1";
        comando.ExecuteNonQuery();
    }

    private void AutorizarConElCodigo0001()
    {
        var base_ = BaseComedor.Abrir(_archivo.Ruta);
        var sesion = new IdentificacionPorCodigo(base_, _reloj).Identificar("0001", PropositoSesion.Consumption);
        var resultado = new UnidadConfirmacion(base_, _reloj).Confirmar(new SolicitudConfirmacion(
            Guid.NewGuid(), sesion.IdentitySessionId!.Value, sesion.PersonId!.Value, SemillaAtencion.PrimeraOpcionId, 1, 1));
        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
    }

    private Dictionary<string, long> Conteos()
    {
        using var conexion = Abrir();
        return new[] { "tbComedorOpcionesMenu", "tbComedorPersonas", "tbComedorCredencialesPersonas", "tbComedorPermisosOperadores" }
            .ToDictionary(t => t, t => Escalar<long>(conexion, $"SELECT COUNT(*) FROM {t}"));
    }

    private static ResultadoIdentificacion Clasificar(SqliteConnection conexion, string codigo, DateTimeOffset ahora)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT comcreFechaVigenciaDesdeUtc, comcreFechaVigenciaHastaUtc, comcreFechaRevocacionUtc FROM tbComedorCredencialesPersonas WHERE comcreValorNormalizado = $valor";
        comando.Parameters.AddWithValue("$valor", codigo);
        using var lector = comando.ExecuteReader();
        Assert.True(lector.Read(), $"Falta el código sintético {codigo}.");
        return ClasificadorCredencial.Clasificar(new CredencialIdentificacion(Instante(lector, 0), Instante(lector, 1), Instante(lector, 2)), ahora);
    }

    private static ResultadoPermiso Permiso(SqliteConnection conexion, string codigo, DateTimeOffset ahora)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            SELECT pop.compopCodigo, pop.compopRol, pop.compopFechaVigenciaDesdeUtc, pop.compopFechaVigenciaHastaUtc, pop.compopFechaRevocacionUtc
            FROM tbComedorPermisosOperadores pop
            JOIN tbComedorCredencialesPersonas cre ON cre.comvcCodigo = pop.comvcCodigo AND cre.comperCodigo = pop.comperCodigo
            WHERE cre.comcreValorNormalizado = $valor
            """;
        comando.Parameters.AddWithValue("$valor", codigo);
        using var lector = comando.ExecuteReader();
        var permisos = new List<PermisoOperador>();
        while (lector.Read())
        {
            permisos.Add(new PermisoOperador(Guid.Parse(lector.GetString(0)),
                lector.GetString(1) == "OPERATOR" ? RolOperador.Operator : RolOperador.SystemAdministrator,
                Instante(lector, 2), Instante(lector, 3), Instante(lector, 4)));
        }

        Assert.NotEmpty(permisos);
        return EvaluadorPermisoOperador.Evaluar(permisos, RolOperador.Operator, ahora);
    }

    private static DateTimeOffset? Instante(SqliteDataReader lector, int columna) =>
        lector.IsDBNull(columna) ? null : DateTimeOffset.Parse(lector.GetString(columna));

    private SqliteConnection Abrir()
    {
        var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
        conexion.Open();
        return conexion;
    }

    private static T Escalar<T>(SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        var valor = comando.ExecuteScalar();
        return valor is T ya ? ya : (T)Convert.ChangeType(valor!, typeof(T));
    }
}
