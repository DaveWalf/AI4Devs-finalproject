using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>El puesto y su snapshot activo, leídos dentro de la transacción de quien los usa.</summary>
internal sealed record PuestoLocal(
    Guid Id,
    Guid SnapshotId,
    TimeZoneInfo ZonaHoraria,
    TimeOnly InicioDiaNegocio,
    int VersionConfiguracion,
    int VersionPoliticas,
    int VersionCatalogo)
{
    public static PuestoLocal Leer(SqliteConnection conexion, SqliteTransaction? transaccion)
    {
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            """
            SELECT cp.compueCodigo, cp.comvcCodigoActiva, cp.comcpZonaHoraria, cp.comcpHoraInicioDiaNegocio,
                   vc.comvcVersionConfiguracion, vc.comvcVersionPoliticas, vc.comvcVersionCatalogo
            FROM tbComedorConfiguracionPuestos cp
            JOIN tbComedorVersionesConfiguracion vc ON vc.comvcCodigo = cp.comvcCodigoActiva
            """);
        using var lector = comando.ExecuteReader();
        if (!lector.Read())
        {
            throw new InvalidOperationException("La base no tiene la configuración del puesto con un snapshot activo.");
        }

        return new PuestoLocal(
            lector.GetGuid(0),
            lector.GetGuid(1),
            TimeZoneInfo.FindSystemTimeZoneById(lector.GetString(2)),
            TimeOnly.ParseExact(lector.GetString(3), "HH:mm", CultureInfo.InvariantCulture),
            lector.GetInt32(4),
            lector.GetInt32(5),
            lector.GetInt32(6));
    }
}
