using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>Ayudante de SQL para las pruebas: siempre con parámetros, los Guid como los guarda el proveedor.</summary>
public static class Sql
{
    /// <summary>Tablas que escribe la transacción de confirmación (RB06).</summary>
    public static readonly string[] TablasDeEscritura =
    [
        "tbComedorIntencionesConsumo",
        "tbComedorConsumos",
        "tbComedorConsumosConocidos",
        "tbComedorCargosCupoConocidos",
        "tbComedorUsosCupo",
        "tbComedorComprobantes",
        "tbComedorTrabajosImpresion",
        "tbComedorEventosSalida",
        "tbComedorBitacora",
    ];

    public static void Ejecutar(SqliteConnection conexion, string sql, params (string Nombre, object? Valor)[] parametros)
    {
        using var comando = Crear(conexion, sql, parametros);
        comando.ExecuteNonQuery();
    }

    public static T Escalar<T>(SqliteConnection conexion, string sql, params (string Nombre, object? Valor)[] parametros)
    {
        using var comando = Crear(conexion, sql, parametros);
        var valor = comando.ExecuteScalar();
        return valor is T ya ? ya : (T)Convert.ChangeType(valor!, typeof(T));
    }

    public static Guid EscalarGuid(SqliteConnection conexion, string sql, params (string Nombre, object? Valor)[] parametros)
    {
        using var comando = Crear(conexion, sql, parametros);
        return Guid.Parse((string)comando.ExecuteScalar()!);
    }

    public static long Contar(SqliteConnection conexion, string tabla) =>
        Escalar<long>(conexion, $"SELECT COUNT(*) FROM {tabla}");

    /// <summary>Cantidad de filas de cada tabla de escritura, para comparar antes y después.</summary>
    public static Dictionary<string, long> ConteoDeEscritura(SqliteConnection conexion) =>
        TablasDeEscritura.ToDictionary(t => t, t => Contar(conexion, t));

    private static SqliteCommand Crear(SqliteConnection conexion, string sql, (string Nombre, object? Valor)[] parametros)
    {
        var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        }

        return comando;
    }
}
