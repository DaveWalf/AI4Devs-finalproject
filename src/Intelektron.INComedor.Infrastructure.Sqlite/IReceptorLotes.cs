namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>
/// Puerto hacia el receptor central (D11 de recibir-lotes-idempotente). La implementación HTTP vive en el Kiosk, como la
/// impresora detrás de IImpresoraComprobantes; este proyecto nombra su motor y no habla de red.
/// </summary>
public interface IReceptorLotes
{
    /// <summary>Envía el cuerpo del lote tal cual, con Idempotency-Key igual a su BatchId.</summary>
    Task<RespuestaReceptor> EnviarAsync(LoteSincronizacion lote, CancellationToken cancelacion);

    /// <summary>Consulta el acuse de un BatchId del puesto.</summary>
    Task<RespuestaReceptor> ConsultarAsync(Guid batchId, CancellationToken cancelacion);
}

/// <summary>Lo que el receptor contestó, ya clasificado.</summary>
public abstract record RespuestaReceptor;

/// <summary>Un 201 o un 200 con el texto del acuse, sin validar todavía: lo valida la cola al registrarlo.</summary>
public sealed record Acusado(string Acuse) : RespuestaReceptor;

/// <summary>La consulta respondió 404: el receptor no tiene ese lote para este puesto.</summary>
public sealed record NoEncontrado : RespuestaReceptor;

/// <summary>Una respuesta HTTP que no acusa: 202, 4xx o 5xx, con el code del problema si vino.</summary>
public sealed record Rechazado(int Estado, string? Codigo) : RespuestaReceptor;

/// <summary>La red falló o se agotó la espera: no se sabe si el receptor guardó el lote.</summary>
public sealed record SinRespuesta : RespuestaReceptor;
