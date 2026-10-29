using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Demostracion;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La pantalla de demostración con bUnit y un <see cref="ServicioDemostracion"/> real sobre un archivo SQLite
/// temporal, sin doble entre la pantalla y la base (D9).
/// </summary>
public class PantallaDemostracionTests : IDisposable
{
    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();

    public void Dispose()
    {
        _contexto.Dispose();
        _archivo.Dispose();
    }

    private IRenderedComponent<PantallaDemostracion> Dibujar()
    {
        _contexto.Services.AddSingleton(new ServicioDemostracion(_archivo.Ruta, new RelojFijo("2026-10-19T15:00:00Z")));
        return _contexto.Render<PantallaDemostracion>();
    }

    [Fact(DisplayName = "AT01 · Primera confirmación del día (pantalla): un toque muestra AUTHORIZED con los identificadores de consumo y comprobante")]
    public void Un_toque_muestra_AUTHORIZED_con_identificadores()
    {
        var pantalla = Dibujar();

        pantalla.Find("#confirmar").Click();

        Assert.Equal("AUTHORIZED", pantalla.Find("#decision").TextContent.Trim());
        var consumo = Guid.Parse(pantalla.Find("#consumo").TextContent.Trim());
        var comprobante = Guid.Parse(pantalla.Find("#comprobante").TextContent.Trim());
        using var conexion = BaseComedor.Abrir(_archivo.Ruta).AbrirConexion();
        Assert.Equal(consumo, Guid.Parse(Escalar(conexion, "SELECT comconCodigo FROM tbComedorConsumos")));
        Assert.Equal(comprobante, Guid.Parse(Escalar(conexion, "SELECT comcmpCodigo FROM tbComedorComprobantes")));
    }

    [Fact(DisplayName = "AT02 · Segunda confirmación con el cupo agotado (pantalla): un segundo toque muestra DENIED y LOCAL_LIMIT_REACHED")]
    public void Un_segundo_toque_muestra_DENIED_por_cupo()
    {
        var pantalla = Dibujar();
        pantalla.Find("#confirmar").Click();

        pantalla.Find("#confirmar").Click();

        Assert.Equal("DENIED", pantalla.Find("#decision").TextContent.Trim());
        Assert.Equal("LOCAL_LIMIT_REACHED", pantalla.Find("#motivo").TextContent.Trim());
        Assert.Empty(pantalla.FindAll("#consumo"));
    }

    [Fact(DisplayName = "TK03.1 · Base de una versión de esquema desconocida (pantalla): muestra el error de almacenamiento y no dibuja el botón")]
    public void Base_de_version_desconocida_muestra_error_y_no_ofrece_confirmar()
    {
        using (var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False"))
        {
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "PRAGMA user_version = 99; CREATE TABLE futura (id INTEGER)";
            comando.ExecuteNonQuery();
        }

        var pantalla = Dibujar();

        Assert.Contains("Error de almacenamiento: la base no es de una versión conocida",
            pantalla.Find("#error-almacenamiento").TextContent);
        Assert.Empty(pantalla.FindAll("#confirmar"));
    }

    private static string Escalar(SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return (string)comando.ExecuteScalar()!;
    }
}
