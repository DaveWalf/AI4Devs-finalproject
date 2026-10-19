using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Base SQLite del puesto: apertura, versión de esquema y transacción de escritura.</summary>
public sealed class BaseComedor
{
    private const int UltimaVersionConocida = 1;
    private const string RecursoMigracion0001 = "Migraciones.0001_transaccion_consumo.sql";

    private readonly string _cadenaConexion;

    private BaseComedor(string ruta)
    {
        Ruta = ruta;
        _cadenaConexion = new SqliteConnectionStringBuilder
        {
            DataSource = ruta,
            Pooling = false,
        }.ToString();
    }

    public string Ruta { get; }

    /// <summary>
    /// Abre la base: lee la versión de esquema, aplica la migración 0001 a una base vacía y rechaza una
    /// versión desconocida sin modificar el archivo (D6).
    /// </summary>
    public static BaseComedor Abrir(string ruta)
    {
        var baseComedor = new BaseComedor(ruta);

        using var conexion = new SqliteConnection(baseComedor._cadenaConexion);
        conexion.Open();

        var version = LeerVersion(conexion);
        if (version > UltimaVersionConocida)
        {
            throw new EsquemaDesconocidoException(version, UltimaVersionConocida);
        }

        if (version == 0)
        {
            AplicarMigracion0001(conexion);
        }

        Ejecutar(conexion, "PRAGMA journal_mode = WAL");
        Ejecutar(conexion, "PRAGMA synchronous = FULL");
        return baseComedor;
    }

    /// <summary>Abre una conexión nueva con las claves foráneas activas y synchronous FULL.</summary>
    public SqliteConnection AbrirConexion()
    {
        var conexion = new SqliteConnection(_cadenaConexion);
        conexion.Open();
        Ejecutar(conexion, "PRAGMA foreign_keys = ON");
        Ejecutar(conexion, "PRAGMA synchronous = FULL");
        return conexion;
    }

    /// <summary>
    /// Inicia una transacción que toma el bloqueo de escritura al empezar (D7), no al primer INSERT.
    /// </summary>
    public static SqliteTransaction IniciarTransaccionEscritura(SqliteConnection conexion)
    {
        return conexion.BeginTransaction(deferred: false);
    }

    private static void AplicarMigracion0001(SqliteConnection conexion)
    {
        using var transaccion = IniciarTransaccionEscritura(conexion);

        // Otra apertura pudo migrar entre la lectura previa y el bloqueo.
        if (LeerVersion(conexion) == 0)
        {
            Ejecutar(conexion, LeerRecurso(RecursoMigracion0001));
            Ejecutar(conexion, "PRAGMA user_version = 1");
        }

        transaccion.Commit();
    }

    private static int LeerVersion(SqliteConnection conexion)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(comando.ExecuteScalar());
    }

    private static void Ejecutar(SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        comando.ExecuteNonQuery();
    }

    private static string LeerRecurso(string nombre)
    {
        using var flujo = typeof(BaseComedor).Assembly.GetManifestResourceStream(nombre)
            ?? throw new InvalidOperationException($"Falta el recurso embebido {nombre}.");
        using var lector = new StreamReader(flujo);
        return lector.ReadToEnd();
    }
}
