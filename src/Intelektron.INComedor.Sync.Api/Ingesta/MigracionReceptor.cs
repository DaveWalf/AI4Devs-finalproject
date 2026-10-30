using Microsoft.Data.SqlClient;

namespace Intelektron.INComedor.Sync.Api.Ingesta;

/// <summary>Aplica el script idempotente del receptor al arrancar con una cadena de conexión (D1).</summary>
internal static class MigracionReceptor
{
    private const string Recurso = "Ingesta.Migraciones.0001_receptor.sql";

    public static void Aplicar(string cadenaConexion)
    {
        using var conexion = new SqlConnection(cadenaConexion);
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = LeerRecurso();
        comando.ExecuteNonQuery();
    }

    private static string LeerRecurso()
    {
        using var flujo = typeof(MigracionReceptor).Assembly.GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException($"Falta el recurso embebido {Recurso}.");
        using var lector = new StreamReader(flujo);
        return lector.ReadToEnd();
    }
}
