using Intelektron.INComedor.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Demostracion;

public enum EstadoPreparacion
{
    Lista,
    ErrorAlmacenamiento,
}

/// <summary>Prepara la base de demostración y confirma una intención por toque (D2, D5 y D6).</summary>
public sealed class ServicioDemostracion
{
    private BaseComedor? _base;

    public ServicioDemostracion(string rutaBase, TimeProvider reloj)
    {
        RutaBase = rutaBase;
        Reloj = reloj;
    }

    public string RutaBase { get; }

    public TimeProvider Reloj { get; }

    /// <summary>
    /// Abre la base y la siembra si está vacía. Una versión de esquema desconocida o un error de SQLite se
    /// informan como estado; cualquier otra excepción sigue de largo (D6).
    /// </summary>
    public EstadoPreparacion Preparar()
    {
        try
        {
            var baseComedor = BaseComedor.Abrir(RutaBase);
            SemillaDemostracion.Aplicar(baseComedor);
            _base = baseComedor;
            return EstadoPreparacion.Lista;
        }
        catch (EsquemaDesconocidoException)
        {
            return EstadoPreparacion.ErrorAlmacenamiento;
        }
        catch (SqliteException)
        {
            return EstadoPreparacion.ErrorAlmacenamiento;
        }
    }

    /// <summary>Abre una sesión de prueba, genera un IntentId nuevo y confirma con la unidad de trabajo de RB06.</summary>
    public ResultadoConfirmacion Confirmar()
    {
        var baseComedor = _base
            ?? throw new InvalidOperationException("Preparar() debe devolver Lista antes de confirmar.");

        var sesion = SesionesDePrueba.Abrir(baseComedor, Reloj);
        var solicitud = new SolicitudConfirmacion(
            Guid.NewGuid(), sesion, SemillaDemostracion.PersonaId,
            SemillaDemostracion.OpcionId, SemillaDemostracion.VersionOpcion, 1);
        return new UnidadConfirmacion(baseComedor, Reloj).Confirmar(solicitud);
    }
}
