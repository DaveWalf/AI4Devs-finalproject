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
}
