using Intelektron.INComedor.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// La sesión del operador para reimprimir y recuperar desde Consumos (D8 de configurar-puesto-por-pestanas y D5 de
/// buscar-y-exportar-consumos): la asistencia que antes era un estado de la máquina, sobre el mismo <see cref="Asistencia"/>.
/// Se abre con el código de un operador en la pestaña, o adopta la del operador que entró a Configuración con su código.
/// Vive mientras Configuración esté abierta y termina con «Salir», al cerrar Configuración, cuando una acción vuelve sin
/// privilegio o a los 30 s sin un toque; cada salida queda en la bitácora con su causa.
/// </summary>
public sealed class SesionOperador
{
    public static readonly TimeSpan Plazo = TimeSpan.FromSeconds(30);

    private readonly Asistencia _asistencia;
    private readonly TimeProvider _reloj;
    private Guid? _sesion;
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

            Limpiar();
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

    /// <summary>Termina la sesión con su causa: EXIT, CONFIGURATION_CLOSED, TIMEOUT o PERMISSION_LOST.</summary>
    public void Salir(string causa = "EXIT")
    {
        if (_sesion is { } sesion)
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

    /// <summary>A los 30 s sin un toque (el borde incluido) la sesión termina; devuelve si cambió algo.</summary>
    public bool RevisarInactividad()
    {
        if (!Abierta || _reloj.GetUtcNow() - _ultimoToque < Plazo)
        {
            return false;
        }

        Salir("TIMEOUT");
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
        NombreVisible = null;
        ReimpresionPedida = null;
        Mensaje = null;
        Aviso = null;
    }
}
