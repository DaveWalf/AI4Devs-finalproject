using System.IO;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Demostracion;

/// <summary>Carga el conjunto fijo de datos sintéticos de la demostración en una base vacía (D3).</summary>
internal static class SemillaDemostracion
{
    private const string RecursoSemilla = "Demostracion.semilla_demostracion.sql";

    // Los mismos UUID que escribe semilla_demostracion.sql.
    public static readonly Guid SnapshotId = Guid.Parse("00000000-0000-4000-8000-000000000001");
    public static readonly Guid PersonaId = Guid.Parse("00000000-0000-4000-8000-000000000008");
    public static readonly Guid OpcionId = Guid.Parse("00000000-0000-4000-8000-000000000009");
    public const int VersionOpcion = 1;

    /// <summary>
    /// Siembra sólo si la base no tiene ningún puesto. La consulta se hace bajo el bloqueo de escritura, así
    /// que dos arranques simultáneos no siembran dos veces.
    /// </summary>
    public static void Aplicar(BaseComedor baseComedor)
    {
        using var conexion = baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);

        using (var consulta = conexion.CreateCommand())
        {
            consulta.Transaction = transaccion;
            consulta.CommandText = "SELECT COUNT(*) FROM tbComedorConfiguracionPuestos";
            if (Convert.ToInt64(consulta.ExecuteScalar()) == 0)
            {
                using var siembra = conexion.CreateCommand();
                siembra.Transaction = transaccion;
                siembra.CommandText = LeerRecurso();
                siembra.ExecuteNonQuery();
            }
        }

        transaccion.Commit();
    }

    private static string LeerRecurso()
    {
        using var flujo = typeof(SemillaDemostracion).Assembly.GetManifestResourceStream(RecursoSemilla)
            ?? throw new InvalidOperationException($"Falta el recurso embebido {RecursoSemilla}.");
        using var lector = new StreamReader(flujo);
        return lector.ReadToEnd();
    }
}
