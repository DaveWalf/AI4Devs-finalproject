namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>
/// El registro de la atención, de Configuración y del puesto (D8 y D9 de escribir-bitacoras-externas): cada uno en su propia
/// transacción BEGIN IMMEDIATE corta, con la misma secuencia por puesto que los demás. El contexto nunca lleva el código.
/// </summary>
public sealed class RegistroDelPuesto
{
    private readonly BaseComedor _baseComedor;
    private readonly TimeProvider _reloj;
    private readonly RegistroBitacora _bitacora;

    public RegistroDelPuesto(BaseComedor baseComedor, TimeProvider reloj, string componente)
    {
        _baseComedor = baseComedor;
        _reloj = reloj;
        _bitacora = new RegistroBitacora(componente);
    }

    public void Registrar(string tipoEvento, object contexto, string severidad = SeveridadBitacora.Info)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        _bitacora.Insertar(conexion, transaccion, PuestoLocal.Leer(conexion, transaccion), _reloj.GetUtcNow(), tipoEvento, contexto, severidad);
        transaccion.Commit();
    }

    /// <summary>
    /// KIOSK_STARTED o KIOSK_STOPPED con las versiones de la aplicación, del runtime, del sistema operativo, del esquema y
    /// de la configuración (D9). La que no se pueda leer va nula: nunca una versión inventada.
    /// </summary>
    public void RegistrarPuesto(string tipoEvento, string? versionAplicacion, string? versionRuntime, string? sistemaOperativo)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var puesto = PuestoLocal.Leer(conexion, transaccion);
        var esquema = (int)ComandosSqlite.EscalarLong(conexion, transaccion, "PRAGMA user_version");
        _bitacora.Insertar(conexion, transaccion, puesto, _reloj.GetUtcNow(), tipoEvento, new
        {
            AppVersion = versionAplicacion,
            RuntimeVersion = versionRuntime,
            OsDescription = sistemaOperativo,
            SchemaVersion = esquema,
            ConfigurationVersion = puesto.VersionConfiguracion,
        });
        transaccion.Commit();
    }
}
