using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>El código de identificación no se escribe en ningún hecho de la confirmación (D12, AGENTS.md).</summary>
public class CodigoProtegidoTests : ConfirmacionTestBase
{
    [Fact(DisplayName = "D6 · Una autorización no registra el código: ninguna columna de texto de los hechos lo contiene")]
    public void Una_autorizacion_no_registra_el_codigo()
    {
        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        AssertSinCodigo();
    }

    [Fact(DisplayName = "D6 · Un rechazo tampoco lo registra: ni el evento CONSUMPTION_DENIED ni su bitácora contienen el código")]
    public void Un_rechazo_tampoco_registra_el_codigo()
    {
        Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoOpcional));

        Assert.Equal(TipoResultado.Denegada, resultado.Tipo);
        AssertSinCodigo();
    }

    /// <summary>Busca el código en todas las columnas de texto de las tablas que escribe la confirmación.</summary>
    private void AssertSinCodigo()
    {
        using var conexion = Abrir();
        var revisadas = 0;
        var conCodigo = new List<string>();

        foreach (var tabla in Sql.TablasDeEscritura)
        {
            foreach (var columna in ColumnasDeTexto(conexion, tabla))
            {
                revisadas++;
                var coincidencias = Sql.Escalar<long>(conexion,
                    $"SELECT COUNT(*) FROM {tabla} WHERE {columna} LIKE $patron",
                    ("$patron", $"%{FixtureSintetico.CodigoSintetico}%"));
                if (coincidencias > 0)
                {
                    conCodigo.Add($"{tabla}.{columna}");
                }
            }
        }

        Assert.True(revisadas > 20, $"La búsqueda revisó sólo {revisadas} columnas: no debería pasar en vacío.");
        Assert.Empty(conCodigo);
    }

    private static List<string> ColumnasDeTexto(SqliteConnection conexion, string tabla)
    {
        var columnas = new List<string>();
        using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT name FROM pragma_table_info('{tabla}') WHERE type = 'TEXT'";
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            columnas.Add(lector.GetString(0));
        }

        return columnas;
    }
}
