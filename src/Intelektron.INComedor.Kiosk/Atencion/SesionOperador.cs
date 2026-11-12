using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Quién habilita reimprimir y recuperar desde Consumos (D1, D5 y D6 de reimprimir-sin-codigo): el acceso con que se entró a
/// Configuración, sin pedir otro código. Con el código de un operador, adopta la sesión privilegiada que abrió su entrada y
/// <see cref="Asistencia"/> comprueba su permiso en cada acción (RB26); con la clave del día, actúa sin sesión y con el actor
/// nulo, y la copia sale marcada SIN VALIDEZ. Sin ninguno de los dos, la pestaña no ofrece las acciones. La sesión del
/// operador es de Configuración, que la cierra al cerrarse, una sola vez: <see cref="Salir"/> sólo olvida el acceso.
/// </summary>
public sealed class SesionOperador
{
    private readonly Asistencia _asistencia;
    private Guid? _sesion;

    public SesionOperador(Asistencia asistencia)
    {
        _asistencia = asistencia;
    }

    /// <summary>Si hay un acceso que habilita reimprimir y recuperar: el de un operador o el de la clave del día.</summary>
    public bool Habilitada => _sesion is not null || ConClave;

    /// <summary>Se entró a Configuración con la clave del día: la copia sale marcada SIN VALIDEZ y se avisa antes (D7).</summary>
    public bool ConClave { get; private set; }

    /// <summary>El operador que entró con su código; con la clave del día, ninguno.</summary>
    public string? NombreVisible { get; private set; }

    /// <summary>El comprobante cuyo motivo de reimpresión se está pidiendo.</summary>
    public ComprobanteDelDia? ReimpresionPedida { get; private set; }

    /// <summary>El aviso de incertidumbre va antes de pedir el motivo (HU03-B).</summary>
    public bool AvisoIncertidumbre => ReimpresionPedida?.EstadoUltimoTrabajo == EstadoTrabajoImpresion.Unknown;

    /// <summary>El resultado de la última acción.</summary>
    public string? Mensaje { get; private set; }

    /// <summary>Por qué se denegó la última acción del operador: el permiso que cayó o la sesión que ya no sirve (D6).</summary>
    public string? Aviso { get; private set; }

    /// <summary>
    /// Adopta la sesión de acción privilegiada que abrió un operador al entrar a Configuración con su código, para no pedírselo
    /// otra vez (D5 de buscar-y-exportar-consumos). Asistencia sigue comprobando el permiso en cada acción (RB26).
    /// </summary>
    public void Adoptar(Guid sesion, string nombreVisible)
    {
        Limpiar();
        _sesion = sesion;
        NombreVisible = nombreVisible;
    }

    /// <summary>Se entró a Configuración con la clave del día, que no identifica a nadie (D1 de reimprimir-sin-codigo).</summary>
    public void ConClaveDelDia()
    {
        Limpiar();
        ConClave = true;
    }

    /// <summary>El comprobante de una fila de Consumos, cuyo motivo de reimpresión se pide a continuación.</summary>
    public void PedirReimpresion(ComprobanteDelDia comprobante)
    {
        if (!Habilitada)
        {
            return;
        }

        ReimpresionPedida = comprobante;
        Mensaje = null;
        Aviso = null;
    }

    public void CancelarReimpresion() => ReimpresionPedida = null;

    public void Reimprimir(MotivoReimpresion motivo)
    {
        if (ReimpresionPedida is not { } pedida || !Habilitada)
        {
            return;
        }

        var resultado = _sesion is { } sesion
            ? _asistencia.Reimprimir(sesion, pedida.TicketId, motivo)
            : _asistencia.ReimprimirConClave(pedida.TicketId, motivo);
        DespuesDeAccion(resultado, resultado.Envio is { } envio ? MensajesAtencion.CopiaEnviada(envio.NumeroCopia) : null);
    }

    public void Recuperar(Guid trabajoId)
    {
        if (!Habilitada)
        {
            return;
        }

        var resultado = _sesion is { } sesion ? _asistencia.Recuperar(sesion, trabajoId) : _asistencia.RecuperarConClave(trabajoId);
        DespuesDeAccion(resultado, MensajesAtencion.OriginalEnviado);
    }

    /// <summary>Olvida el acceso al cerrar Configuración; la sesión del operador la cierra Configuración, una sola vez.</summary>
    public void Salir() => Limpiar();

    private void DespuesDeAccion(ResultadoAccion resultado, string? exito)
    {
        ReimpresionPedida = null;
        var sinPrivilegio = resultado.ReasonCode is { } codigo
            && (codigo == "SESSION_NOT_USABLE" || codigo.StartsWith("PERMISSION_", StringComparison.Ordinal));
        if (!resultado.Realizada && sinPrivilegio)
        {
            // El permiso o la sesión cayeron: se avisa el motivo sin pedir un código y la sesión queda como está, así que la
            // acción siguiente vuelve a comprobar el permiso y vuelve a denegarse (RB26, D6 de reimprimir-sin-codigo).
            // La sesión que venció no se renueva acá: Consumos dice que se vuelva a entrar por el engranaje (D6).
            Mensaje = null;
            Aviso = resultado.ReasonCode == "SESSION_NOT_USABLE"
                ? MensajesAtencion.SesionOperadorVencida
                : MensajesAtencion.DeMotivo(resultado.ReasonCode);
            return;
        }

        Aviso = null;
        Mensaje = !resultado.Realizada
            ? MensajesAtencion.DeMotivo(resultado.ReasonCode)
            : resultado.Envio?.Estado == EstadoTrabajoImpresion.Submitted ? exito : MensajesAtencion.ComprobanteQuedoPendiente;
    }

    private void Limpiar()
    {
        _sesion = null;
        ConClave = false;
        NombreVisible = null;
        ReimpresionPedida = null;
        Mensaje = null;
        Aviso = null;
    }
}
