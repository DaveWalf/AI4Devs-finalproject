using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Arma catálogos de 8 y de 10 opciones sobre la semilla de nueve, en la base temporal (D9 de presentar-atencion-tactil):
/// quita una opción con su categoría o agrega una de ALMUERZO. Corre después de preparar la base y antes de identificar.
/// </summary>
public static class CatalogoDePrueba
{
    private const string Snapshot = "00000000-0000-4000-8000-000000000001";

    /// <summary>«Yogurt», la última del catálogo de nueve por nombre, sin cargo.</summary>
    public static readonly Guid Yogurt = Guid.Parse("00000000-0000-4000-8000-000000000018");

    /// <summary>La opción que agrega <see cref="Agregar"/>: queda primera por nombre.</summary>
    public static readonly Guid Agregada = Guid.Parse("00000000-0000-4000-8000-000000000099");

    public static void Quitar(string rutaBase, Guid opcion) => Ejecutar(rutaBase,
        "DELETE FROM tbComedorOpcionesMenu_CategoriasCupo WHERE commenCodigo = $men; DELETE FROM tbComedorOpcionesMenu WHERE commenCodigo = $men",
        opcion);

    public static void Agregar(string rutaBase) => Ejecutar(rutaBase,
        $"""
        INSERT INTO tbComedorOpcionesMenu VALUES ('{Snapshot}', $men, 1, '00000000-0000-4000-8000-000000000005', 'Agua mineral', 'ARS', 2, 30000);
        INSERT INTO tbComedorOpcionesMenu_CategoriasCupo VALUES ('{Snapshot}', $men, '00000000-0000-4000-8000-000000000007', 1);
        """,
        Agregada);

    private static void Ejecutar(string rutaBase, string sql, Guid opcion)
    {
        using var conexion = new SqliteConnection($"Data Source={rutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        comando.Parameters.AddWithValue("$men", opcion.ToString("D"));
        Assert.True(comando.ExecuteNonQuery() > 0);
    }
}
