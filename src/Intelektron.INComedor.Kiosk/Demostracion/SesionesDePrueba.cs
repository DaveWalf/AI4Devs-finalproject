using System.Globalization;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Demostracion;

/// <summary>Abre una sesión de identidad de prueba por toque, sin credencial (D4).</summary>
internal static class SesionesDePrueba
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(2);

    public static Guid Abrir(BaseComedor baseComedor, TimeProvider reloj)
    {
        var ahora = reloj.GetUtcNow();
        var sesion = Guid.NewGuid();

        using var conexion = baseComedor.AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            INSERT INTO tbComedorSesionesIdentidad
                (comsidCodigo, comvcCodigo, comperCodigo, comsidMetodo, comsidProposito,
                 comsidFechaEmisionUtc, comsidFechaVencimientoUtc)
            VALUES ($sid, $vc, $per, 'CODE', 'CONSUMPTION', $emitida, $vence)
            """;
        comando.Parameters.AddWithValue("$sid", sesion);
        comando.Parameters.AddWithValue("$vc", SemillaDemostracion.SnapshotId);
        comando.Parameters.AddWithValue("$per", SemillaDemostracion.PersonaId);
        comando.Parameters.AddWithValue("$emitida", Texto(ahora));
        comando.Parameters.AddWithValue("$vence", Texto(ahora + Vigencia));
        comando.ExecuteNonQuery();
        return sesion;
    }

    private static string Texto(DateTimeOffset instante) =>
        instante.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
