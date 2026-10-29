using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

public class BloqueoInmediatoTests
{
    private const int SqliteBusy = 5;

    [Fact(DisplayName = "D7 · La transacción de escritura toma el bloqueo al empezar: una segunda conexión recibe SQLITE_BUSY")]
    public void La_segunda_conexion_no_puede_abrir_su_transaccion_de_escritura()
    {
        using var archivo = new ArchivoTemporal();
        using var conexionA = Abrir(archivo.Ruta, segundosDeEspera: 30);
        using var conexionB = Abrir(archivo.Ruta, segundosDeEspera: 1);

        using var transaccionA = BaseComedor.IniciarTransaccionEscritura(conexionA);

        var excepcion = Assert.Throws<SqliteException>(() => BaseComedor.IniciarTransaccionEscritura(conexionB));
        Assert.Equal(SqliteBusy, excepcion.SqliteErrorCode);
    }

    private static SqliteConnection Abrir(string ruta, int segundosDeEspera)
    {
        var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False;Default Timeout={segundosDeEspera}");
        conexion.Open();
        return conexion;
    }
}
