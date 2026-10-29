namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

public class FixtureSinteticoTests
{
    [Fact(DisplayName = "D11 · FixtureSintetico: carga tres personas, quince opciones y el código sintético solo en la credencial")]
    public void El_fixture_carga_tres_personas_y_quince_opciones()
    {
        using var archivo = new ArchivoTemporal();
        var baseComedor = BaseComedor.Abrir(archivo.Ruta);

        var fixture = FixtureSintetico.Cargar(baseComedor);

        using var conexion = baseComedor.AbrirConexion();
        Assert.Equal(3L, Sql.Contar(conexion, "tbComedorPersonas"));
        Assert.Equal(15L, Sql.Contar(conexion, "tbComedorOpcionesMenu"));
        Assert.Equal(15, fixture.Opciones.Count);
        Assert.Equal(1L, Sql.Escalar<long>(conexion,
            "SELECT COUNT(*) FROM tbComedorCredencialesPersonas WHERE comcreValorNormalizado = $codigo",
            ("$codigo", FixtureSintetico.CodigoSintetico)));
    }
}
