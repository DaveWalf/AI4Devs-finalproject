using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Base SQLite del puesto: apertura, versión de esquema y transacción de escritura.</summary>
public sealed class BaseComedor
{
    /// <summary>Migraciones conocidas, en orden: cada una lleva la base a su versión (D1 de atender-seleccion-y-confirmacion).</summary>
    private static readonly (int Version, string Recurso)[] Migraciones =
    [
        (1, "Migraciones.0001_transaccion_consumo.sql"),
        (2, "Migraciones.0002_permisos_operadores.sql"),
        (3, "Migraciones.0003_lotes_sincronizacion.sql"),
    ];

    private static readonly int UltimaVersionConocida = Migraciones[^1].Version;

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
    /// Abre la base: lee la versión de esquema, aplica en orden las migraciones que le faltan y rechaza una
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

        foreach (var (versionMigracion, recurso) in Migraciones)
        {
            if (versionMigracion > version)
            {
                AplicarMigracion(conexion, versionMigracion, recurso);
            }
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

    /// <summary>Cada migración en su propia transacción BEGIN IMMEDIATE.</summary>
    private static void AplicarMigracion(SqliteConnection conexion, int version, string recurso)
    {
        using var transaccion = IniciarTransaccionEscritura(conexion);

        // Otra apertura pudo migrar entre la lectura previa y el bloqueo.
        if (LeerVersion(conexion) < version)
        {
            Ejecutar(conexion, LeerRecurso(recurso));
            Ejecutar(conexion, $"PRAGMA user_version = {version}");
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
