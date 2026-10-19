using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>SQL siempre parametrizado (AGENTS.md); los Guid viajan como parámetro, que el proveedor guarda como TEXT.</summary>
internal static class ComandosSqlite
{
    public static SqliteCommand Crear(SqliteConnection conexion, SqliteTransaction? transaccion, string sql, params (string Nombre, object? Valor)[] parametros)
    {
        var comando = conexion.CreateCommand();
        comando.Transaction = transaccion;
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        }

        return comando;
    }

    public static void Ejecutar(SqliteConnection conexion, SqliteTransaction? transaccion, string sql, params (string Nombre, object? Valor)[] parametros)
    {
        using var comando = Crear(conexion, transaccion, sql, parametros);
        comando.ExecuteNonQuery();
    }

    public static long EscalarLong(SqliteConnection conexion, SqliteTransaction? transaccion, string sql, params (string Nombre, object? Valor)[] parametros)
    {
        using var comando = Crear(conexion, transaccion, sql, parametros);
        return Convert.ToInt64(comando.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}

/// <summary>Instantes UTC como TEXT ISO 8601 con Z y milisegundos (D5).</summary>
internal static class FormatoInstante
{
    public static string Texto(DateTimeOffset instante) =>
        instante.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static DateTimeOffset Leer(string texto) =>
        DateTimeOffset.Parse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
}
