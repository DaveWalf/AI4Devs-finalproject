using Intelektron.INComedor.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>Reimprimir o recuperar el comprobante de una fila de Consumos, a la espera del código de un operador.</summary>
public sealed record AccionPendiente(bool Recuperar, ConsumoConsultado Fila);

/// <summary>
/// La sesión del operador para reimprimir y recuperar desde Consumos (D8 de configurar-puesto-por-pestanas y D5 de
/// buscar-y-exportar-consumos): la asistencia que antes era un estado de la máquina, sobre el mismo <see cref="Asistencia"/>.
/// Se abre con el código de un operador en la pestaña, o adopta la del operador que entró a Configuración con su código.
/// Vive mientras Configuración esté abierta y termina con «Salir», al cerrar Configuración, cuando una acción vuelve sin
/// privilegio o a los 30 s sin un toque; cada salida queda en la bitácora con su causa. La adoptada es prestada: salir la
/// olvida sin cerrarla, porque la cierra Configuración, una sola vez. La acción que espera un código (<see cref="Pendiente"/>)
/// vence a los mismos 30 s sin un toque.
/// </summary>
public sealed class SesionOperador
{
    public static readonly TimeSpan Plazo = TimeSpan.FromSeconds(30);

    private readonly Asistencia _asistencia;
    private readonly TimeProvider _reloj;
    private Guid? _sesion;
    private bool _adoptada;
    private DateTimeOffset _ultimoToque;

    public SesionOperador(Asistencia asistencia, TimeProvider reloj)
    {
        _asistencia = asistencia;
        _reloj = reloj;
    }

    public bool Abierta => _sesion is not null;

    public string? NombreVisible { get; private set; }

    /// <summary>El comprobante cuyo motivo de reimpresión se está pidiendo.</summary>
    public ComprobanteDelDia? ReimpresionPedida { get; private set; }

    /// <summary>El aviso de incertidumbre va antes de pedir el motivo (HU03-B).</summary>
    public bool AvisoIncertidumbre => ReimpresionPedida?.EstadoUltimoTrabajo == EstadoTrabajoImpresion.Unknown;

    /// <summary>El resultado de la última acción del operador.</summary>
    public string? Mensaje { get; private set; }

    /// <summary>Por qué no hay sesión: el permiso que falta o que cayó, o la base que no respondió.</summary>
    public string? Aviso { get; private set; }

    /// <summary>La acción de una fila que espera el código de un operador, para hacerla en cuanto entre (D5).</summary>
    public AccionPendiente? Pendiente { get; private set; }

    /// <summary>Una fila pidió reimprimir o recuperar sin sesión: queda pendiente hasta un código, «Cancelar» o 30 s.</summary>
    public void Pedir(AccionPendiente accion)
    {
        Pendiente = accion;
        Aviso = null;
        Tocar();
    }

    /// <summary>La acción pendiente, que se saca al tomarla: se hace una sola vez.</summary>
    public AccionPendiente? TomarPendiente()
    {
        var pendiente = Pendiente;
        Pendiente = null;
        return pendiente;
    }

    public void DescartarPendiente() => Pendiente = null;

    /// <summary>«Cancelar» después de una denegación: el aviso se va con el pedido.</summary>
    public void DescartarAviso() => Aviso = null;

    public void Entrar(string codigo)
    {
        try
        {
            var resultado = _asistencia.Entrar(codigo);
            if (!resultado.Concedida)
            {
                Aviso = MensajesAtencion.DeMotivo(resultado.ReasonCode);
                return;
            }

            // La acción que esperaba este código sobrevive a la limpieza, para hacerla en cuanto la pestaña la tome.
            var pendiente = Pendiente;
            Limpiar();
            Pendiente = pendiente;
            _sesion = resultado.SesionId;
            NombreVisible = resultado.NombreVisible;
            Tocar();
        }
        catch (SqliteException)
        {
            Limpiar();
            Aviso = MensajesAtencion.ErrorAlmacenamiento;
        }
    }

    /// <summary>
    /// Adopta la sesión de acción privilegiada que abrió un operador al entrar a Configuración con su código, para no pedírselo
    /// otra vez (D5 de buscar-y-exportar-consumos). Asistencia sigue comprobando el permiso en cada acción (RB26).
    /// </summary>
    public void Adoptar(Guid sesion, string nombreVisible)
    {
        Limpiar();
        _sesion = sesion;
        _adoptada = true;
        NombreVisible = nombreVisible;
        Tocar();
    }

    /// <summary>El comprobante de una fila de Consumos, cuyo motivo de reimpresión se pide a continuación.</summary>
    public void PedirReimpresion(ComprobanteDelDia comprobante)
    {
        if (!Abierta)
        {
            return;
        }

        ReimpresionPedida = comprobante;
        Mensaje = null;
        Tocar();
    }

    public void CancelarReimpresion()
    {
        ReimpresionPedida = null;
        Tocar();
    }

    public void Reimprimir(MotivoReimpresion motivo)
    {
        if (_sesion is not { } sesion || ReimpresionPedida is null)
        {
            return;
        }

        var resultado = _asistencia.Reimprimir(sesion, ReimpresionPedida.TicketId, motivo);
        DespuesDeAccion(resultado, resultado.Envio is { } envio ? $"Copia {envio.NumeroCopia} enviada con la leyenda DUPLICADO" : null);
    }

    public void Recuperar(Guid trabajoId)
    {
        if (_sesion is not { } sesion)
        {
            return;
        }

        var resultado = _asistencia.Recuperar(sesion, trabajoId);
        DespuesDeAccion(resultado, "Comprobante original enviado otra vez");
    }

    /// <summary>
    /// Termina la sesión con su causa: EXIT, CONFIGURATION_CLOSED, TIMEOUT o PERMISSION_LOST. La adoptada sólo se olvida: es la
    /// del acceso a Configuración, que la cierra al cerrarse (D5 de buscar-y-exportar-consumos).
    /// </summary>
    public void Salir(string causa = "EXIT")
    {
        if (_sesion is { } sesion && !_adoptada)
        {
            try
            {
                _asistencia.Salir(sesion, causa);
            }
            catch (SqliteException)
            {
                // La sesión vence igual por su plazo en la base; la pestaña ya no la usa.
            }
        }

        Limpiar();
    }

    public void Tocar() => _ultimoToque = _reloj.GetUtcNow();

    /// <summary>
    /// A los 30 s sin un toque (el borde incluido) la sesión termina, y la acción que esperaba un código se descarta sin
    /// hacerse; devuelve si cambió algo.
    /// </summary>
    public bool RevisarInactividad()
    {
        if ((!Abierta && Pendiente is null) || _reloj.GetUtcNow() - _ultimoToque < Plazo)
        {
            return false;
        }

        if (Abierta)
        {
            Salir("TIMEOUT");
        }
        else
        {
            Pendiente = null;
        }

        return true;
    }

    private void DespuesDeAccion(ResultadoAccion resultado, string? exito)
    {
        var sinPrivilegio = resultado.ReasonCode is { } codigo
            && (codigo == "SESSION_NOT_USABLE" || codigo.StartsWith("PERMISSION_", StringComparison.Ordinal));
        if (!resultado.Realizada && sinPrivilegio)
        {
            // El permiso o la sesión cayeron: la sesión termina y la pestaña vuelve a pedir un código (RB26).
            var aviso = MensajesAtencion.DeMotivo(resultado.ReasonCode);
            Salir("PERMISSION_LOST");
            Aviso = aviso;
            return;
        }

        Mensaje = !resultado.Realizada
            ? MensajesAtencion.DeMotivo(resultado.ReasonCode)
            : resultado.Envio?.Estado == EstadoTrabajoImpresion.Submitted ? exito : "El comprobante quedó pendiente: revisá la impresora";
        ReimpresionPedida = null;
        Tocar();
    }

    private void Limpiar()
    {
        _sesion = null;
        _adoptada = false;
        Pendiente = null;
        NombreVisible = null;
        ReimpresionPedida = null;
        Mensaje = null;
        Aviso = null;
    }
}
