using System.Globalization;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Lo que la franja de la atención muestra de la sincronización (D11).</summary>
public sealed record EstadoSincronizacion(bool ReceptorConfigurado, int Pendientes, int Retenidos, string? UltimoError, bool ErrorDeCredencial)
{
    public bool AlDia => ReceptorConfigurado && Pendientes == 0 && UltimoError is null;
}

/// <summary>
/// Un ciclo de envío del puesto (D11 de recibir-lotes-idempotente): prepara el lote, lo consulta primero si pudo haber
/// llegado sin que llegara la respuesta, lo envía si hace falta y registra el acuse o el error. Corre de a un ciclo por
/// vez y nunca dentro de una confirmación: la atención no espera a la red (AT01 y HU02-C).
/// </summary>
public sealed class SincronizadorPuesto
{
    private readonly ColaSincronizacion _cola;
    private readonly IReceptorLotes _receptor;
    private readonly SemaphoreSlim _unCicloPorVez = new(1, 1);

    // Lotes que este proceso envió y que el receptor contestó sin acusar: se reenvían sin consultar. Un lote sin respuesta,
    // o uno que este proceso no envió nunca (por ejemplo, tras un reinicio), se consulta primero, porque consultar es inocuo.
    private readonly HashSet<Guid> _contestados = [];

    public SincronizadorPuesto(ColaSincronizacion cola, IReceptorLotes receptor)
    {
        _cola = cola;
        _receptor = receptor;
        var (pendientes, retenidos) = cola.LeerConteos();
        Estado = new EstadoSincronizacion(true, pendientes, retenidos, null, false);
    }

    public EstadoSincronizacion Estado { get; private set; }

    public event Action<EstadoSincronizacion>? EstadoCambiado;

    public async Task EnviarUnaVezAsync(CancellationToken cancelacion = default)
    {
        await _unCicloPorVez.WaitAsync(cancelacion);
        try
        {
            var (error, credencial) = await EnviarLotePendienteAsync(cancelacion);
            var (pendientes, retenidos) = _cola.LeerConteos();
            Estado = new EstadoSincronizacion(true, pendientes, retenidos, error, credencial);
            EstadoCambiado?.Invoke(Estado);
        }
        finally
        {
            _unCicloPorVez.Release();
        }
    }

    private async Task<(string? Error, bool Credencial)> EnviarLotePendienteAsync(CancellationToken cancelacion)
    {
        var lote = _cola.Preparar();
        if (lote is null)
        {
            return (null, false);
        }

        RespuestaReceptor respuesta;
        if (!lote.Nuevo && !_contestados.Contains(lote.BatchId))
        {
            respuesta = await _receptor.ConsultarAsync(lote.BatchId, cancelacion);
            if (respuesta is NoEncontrado)
            {
                respuesta = await _receptor.EnviarAsync(lote, cancelacion);
            }
        }
        else
        {
            respuesta = await _receptor.EnviarAsync(lote, cancelacion);
        }

        switch (respuesta)
        {
            case Acusado acusado:
                if (_cola.RegistrarAcuse(lote.BatchId, acusado.Acuse) == ResultadoRegistroAcuse.AcuseInvalido)
                {
                    _contestados.Add(lote.BatchId);
                    return ("ACUSE_INVALIDO", false);
                }

                _contestados.Remove(lote.BatchId);
                return (null, false);

            case Rechazado rechazado:
                var estado = rechazado.Estado.ToString(CultureInfo.InvariantCulture);
                _cola.RegistrarError(lote.BatchId, estado, rechazado.Codigo);
                _contestados.Add(lote.BatchId);
                return (rechazado.Codigo is null ? estado : $"{estado} {rechazado.Codigo}", rechazado.Estado is 401 or 403);

            case NoEncontrado:
                _cola.RegistrarError(lote.BatchId, "404");
                _contestados.Add(lote.BatchId);
                return ("404", false);

            default:
                _cola.RegistrarError(lote.BatchId, "NETWORK");
                _contestados.Remove(lote.BatchId);
                return ("NETWORK", false);
        }
    }
}
