using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Demostracion;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

public enum EstadoAtencion
{
    Reposo,
    Seleccion,
    Confirmando,
    Reconfirmacion,
    Resultado,
    Asistencia,
    ErrorAlmacenamiento,
    SinOpciones,
}

/// <summary>Qué pasó con un toque de identificación.</summary>
public enum ResultadoToque
{
    Aceptado,
    Rechazado,
    Ocupado,
    NoDisponible,
}

/// <summary>
/// El modelo de estados de la atención, sin interfaz (D5 de atender-seleccion-y-confirmacion). Las pantallas Razor
/// sólo lo dibujan y le reenvían los toques; los plazos se cuentan con el reloj inyectado.
/// </summary>
public sealed class MaquinaAtencion
{
    /// <summary>Hasta diez opciones van juntas; desde once, en páginas de diez (D1 de presentar-atencion-tactil).</summary>
    public const int TamanoPagina = 10;

    /// <summary>Una página de hasta ocho opciones va en una columna; de nueve o diez, en dos de hasta cinco (D1).</summary>
    public const int MaximoUnaColumna = 8;
    public static readonly TimeSpan PlazoInactividad = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan PlazoResultado = TimeSpan.FromSeconds(10);

    private readonly ServicioAtencion _servicio;
    // La oferta de la persona (D2 de ofrecer-solo-opciones-disponibles): lo que se le muestra, lo que se le retuvo y lo agotado.
    private IReadOnlyList<OpcionCatalogo> _catalogo = [];
    private IReadOnlyList<OpcionRetenida> _retenidas = [];
    private IReadOnlyList<CategoriaAgotada> _agotadas = [];
    private Guid? _sesion;
    private Guid? _sesionAsistencia;
    private Task? _confirmacion;
    private DateTimeOffset _ultimoToque;
    // El mensaje que se está mostrando y la decisión que lo produjo, para el cierre de la atención (D8 de escribir-bitacoras-externas).
    private string? _codigoMensaje;
    private Guid? _decisionId;

    public MaquinaAtencion(ServicioAtencion servicio)
    {
        _servicio = servicio;
    }

    public EstadoAtencion Estado { get; private set; } = EstadoAtencion.Reposo;

    public string? NombreVisible { get; private set; }

    public Guid? PersonaId { get; private set; }

    public int Pagina { get; private set; } = 1;

    public int TotalPaginas => Math.Max(1, (_catalogo.Count + TamanoPagina - 1) / TamanoPagina);

    public bool PuedeRetroceder => Pagina > 1;

    public bool PuedeAvanzar => Pagina < TotalPaginas;

    public IReadOnlyList<OpcionCatalogo> PaginaActual =>
        _catalogo.Skip((Pagina - 1) * TamanoPagina).Take(TamanoPagina).ToList();

    /// <summary>Cuántas columnas lleva la página actual: la pantalla sólo lo dibuja (D1).</summary>
    public int ColumnasPagina => PaginaActual.Count > MaximoUnaColumna ? 2 : 1;

    public OpcionCatalogo? Elegida { get; private set; }

    /// <summary>La opción con su valor vigente, en la reconfirmación.</summary>
    public OpcionCatalogo? Vigente { get; private set; }

    /// <summary>«Hoy ya usaste: almuerzo 1 de 1»: las categorías de cupo agotadas hoy; null si no hay ninguna (D3).</summary>
    public string? UsoDelDia => MensajesAtencion.UsoDelDia(_agotadas);

    /// <summary>
    /// Por qué no hay opciones, cuando no hay una categoría agotada que lo explique: el motivo de la baja si todas las opciones
    /// se retuvieron por eso y, si no, que no hay servicios habilitados (D4). Null con opciones o con lo usado a la vista.
    /// </summary>
    public string? MotivoSinOpciones =>
        _catalogo.Count > 0 || _agotadas.Count > 0 ? null
        : _retenidas.Count > 0 && _retenidas.All(r => r.Motivo == "PERSON_INACTIVE") ? MensajesAtencion.DeMotivo("PERSON_INACTIVE")
        : MensajesAtencion.SinHabilitacion;

    /// <summary>El IntentId de la última confirmación empezada.</summary>
    public Guid? IntentId { get; private set; }

    /// <summary>El mensaje del resultado de la confirmación.</summary>
    public string? Mensaje { get; private set; }

    public string? MotivoResultado { get; private set; }

    public bool ConsumoRegistrado { get; private set; }

    public bool ComprobantePendiente { get; private set; }

    /// <summary>
    /// El comprobante que sale de la impresora en pantalla: sólo con la impresora simulada y cuando el original quedó
    /// enviado (D7 de presentar-atencion-tactil). Sin persona ni código.
    /// </summary>
    public ComprobanteEnPantalla? ComprobanteEnPantalla { get; private set; }

    /// <summary>Un aviso para la pantalla actual: el motivo de un rechazo, el puesto ocupado o la opción retirada.</summary>
    public string? Aviso { get; private set; }

    public IReadOnlyList<ComprobanteDelDia> Comprobantes { get; private set; } = [];

    /// <summary>El comprobante cuyo motivo de reimpresión se está pidiendo.</summary>
    public ComprobanteDelDia? ReimpresionPedida { get; private set; }

    /// <summary>Si el último trabajo del comprobante pedido está en UNKNOWN: el papel pudo haber salido (HU03-B).</summary>
    public bool AvisoIncertidumbre => ReimpresionPedida?.EstadoUltimoTrabajo == EstadoTrabajoImpresion.Unknown;

    /// <summary>El resultado de la última acción del operador.</summary>
    public string? MensajeAsistencia { get; private set; }

    public void Iniciar()
    {
        Estado = _servicio.Preparar() == EstadoPreparacion.Lista ? EstadoAtencion.Reposo : EstadoAtencion.ErrorAlmacenamiento;
        Tocar();
    }

    public ResultadoToque Identificar(string codigo)
    {
        if (Estado == EstadoAtencion.ErrorAlmacenamiento)
        {
            return ResultadoToque.NoDisponible;
        }

        if (Estado != EstadoAtencion.Reposo)
        {
            // Otra identificación no cambia la persona ni la opción (RB05, RB07).
            Aviso = MensajesAtencion.Ocupado;
            return ResultadoToque.Ocupado;
        }

        var resultado = Proteger(() => _servicio.Identificacion.Identificar(codigo, PropositoSesion.Consumption));
        if (resultado is null)
        {
            return ResultadoToque.NoDisponible;
        }

        if (!resultado.Abierta)
        {
            Aviso = MensajesAtencion.DeMotivo(resultado.ReasonCode);
            return ResultadoToque.Rechazado;
        }

        Limpiar();
        _sesion = resultado.IdentitySessionId;
        PersonaId = resultado.PersonId;
        NombreVisible = resultado.NombreVisible;
        if (!CargarOferta())
        {
            return ResultadoToque.NoDisponible;
        }

        // Sin ninguna opción disponible no hay selección: la pantalla avisa y vuelve a reposo con «Listo» o a los 10 s (D4).
        Estado = _catalogo.Count == 0 ? EstadoAtencion.SinOpciones : EstadoAtencion.Seleccion;
        _codigoMensaje = _catalogo.Count == 0 ? nameof(MensajesAtencion.SinOpciones) : null;
        Tocar();
        return ResultadoToque.Aceptado;
    }

    public void Siguiente()
    {
        if (Estado != EstadoAtencion.Seleccion)
        {
            return;
        }

        if (PuedeAvanzar)
        {
            Pagina++;
        }

        Tocar();
    }

    public void Anterior()
    {
        if (Estado != EstadoAtencion.Seleccion)
        {
            return;
        }

        if (PuedeRetroceder)
        {
            Pagina--;
        }

        Tocar();
    }

    public void Elegir(Guid opcionId)
    {
        if (Estado != EstadoAtencion.Seleccion)
        {
            return;
        }

        var opcion = _catalogo.FirstOrDefault(o => o.Id == opcionId);
        // Cada elección queda con lo que se mostró; volver a tocar la misma no es una elección nueva (D8).
        if (opcion is not null && opcion.Id != Elegida?.Id && !Registrar("OPTION_SELECTED", new
            {
                IdentitySessionId = _sesion,
                MenuItemId = opcion.Id,
                MenuVersion = opcion.Version,
                MenuLabel = opcion.Nombre,
                DisplayedCharge = opcion.Cargo,
            }))
        {
            return;
        }

        Elegida = opcion;
        Aviso = null;
        Tocar();
    }

    public void Cancelar()
    {
        if (Estado is EstadoAtencion.Seleccion or EstadoAtencion.Reconfirmacion)
        {
            Cerrar("SESSION_CANCELLED", new
            {
                IdentitySessionId = _sesion,
                MenuItemId = Elegida?.Id,
                MenuVersion = Elegida?.Version,
                MenuLabel = Elegida?.Nombre,
            });
        }
    }

    /// <summary>
    /// Genera el IntentId y pasa a Confirmando antes del primer await. Un toque repetido mientras confirma devuelve
    /// la misma tarea, con el mismo IntentId (AT26).
    /// </summary>
    public Task ConfirmarAsync()
    {
        if (Estado == EstadoAtencion.Confirmando && _confirmacion is not null)
        {
            return _confirmacion;
        }

        if (Estado != EstadoAtencion.Seleccion || Elegida is null)
        {
            return Task.CompletedTask;
        }

        return Confirmar(Elegida);
    }

    /// <summary>Confirma con el valor vigente y un IntentId nuevo (RB10).</summary>
    public Task ReconfirmarAsync()
    {
        if (Estado != EstadoAtencion.Reconfirmacion || Vigente is null)
        {
            return Task.CompletedTask;
        }

        Elegida = Vigente;
        Vigente = null;
        return Confirmar(Elegida);
    }

    /// <summary>Vuelve a la selección sin confirmar; la elección vieja no queda, porque su valor ya no es el vigente.</summary>
    public void Volver()
    {
        if (Estado != EstadoAtencion.Reconfirmacion)
        {
            return;
        }

        Vigente = null;
        Elegida = null;
        Estado = EstadoAtencion.Seleccion;
        Tocar();
    }

    public void Listo()
    {
        if (Estado is EstadoAtencion.Resultado or EstadoAtencion.SinOpciones)
        {
            CerrarConMensaje("DONE");
        }
    }

    /// <summary>Vuelve a reposo si venció el plazo del estado actual; el borde es inclusivo. Devuelve si cambió algo.</summary>
    public bool RevisarInactividad()
    {
        var quieto = _servicio.Reloj.GetUtcNow() - _ultimoToque;
        var vencido = Estado switch
        {
            EstadoAtencion.Seleccion or EstadoAtencion.Reconfirmacion or EstadoAtencion.Asistencia => quieto >= PlazoInactividad,
            EstadoAtencion.Resultado or EstadoAtencion.SinOpciones => quieto >= PlazoResultado,
            _ => false,
        };

        if (!vencido)
        {
            return false;
        }

        if (Estado == EstadoAtencion.Asistencia)
        {
            SalirAsistencia();
        }
        else if (Estado is EstadoAtencion.Resultado or EstadoAtencion.SinOpciones)
        {
            CerrarConMensaje("TIMEOUT");
        }
        else
        {
            Cerrar("SESSION_TIMED_OUT", new
            {
                IdentitySessionId = _sesion,
                FromState = Estado == EstadoAtencion.Seleccion ? "SELECTION" : "RECONFIRMATION",
                MenuItemId = Elegida?.Id,
            });
        }

        return true;
    }

    public ResultadoToque EntrarAsistencia(string codigo)
    {
        if (Estado == EstadoAtencion.ErrorAlmacenamiento)
        {
            return ResultadoToque.NoDisponible;
        }

        if (Estado != EstadoAtencion.Reposo)
        {
            Aviso = MensajesAtencion.Ocupado;
            return ResultadoToque.Ocupado;
        }

        var resultado = Proteger(() => _servicio.Asistencia.Entrar(codigo));
        if (resultado is null)
        {
            return ResultadoToque.NoDisponible;
        }

        if (!resultado.Concedida)
        {
            Aviso = MensajesAtencion.DeMotivo(resultado.ReasonCode);
            return ResultadoToque.Rechazado;
        }

        Limpiar();
        _sesionAsistencia = resultado.SesionId;
        NombreVisible = resultado.NombreVisible;
        Comprobantes = _servicio.Asistencia.ComprobantesDelDia();
        Estado = EstadoAtencion.Asistencia;
        Tocar();
        return ResultadoToque.Aceptado;
    }

    public void PedirReimpresion(Guid ticketId)
    {
        if (Estado != EstadoAtencion.Asistencia)
        {
            return;
        }

        ReimpresionPedida = Comprobantes.FirstOrDefault(c => c.TicketId == ticketId);
        MensajeAsistencia = null;
        Tocar();
    }

    public void CancelarReimpresion()
    {
        ReimpresionPedida = null;
        Tocar();
    }

    public void Reimprimir(MotivoReimpresion motivo)
    {
        if (Estado != EstadoAtencion.Asistencia || ReimpresionPedida is null || _sesionAsistencia is null)
        {
            return;
        }

        var resultado = _servicio.Asistencia.Reimprimir(_sesionAsistencia.Value, ReimpresionPedida.TicketId, motivo);
        DespuesDeAccion(resultado, resultado.Envio is { } envio ? $"Copia {envio.NumeroCopia} enviada con la leyenda DUPLICADO" : null);
    }

    public void Recuperar(Guid trabajoId)
    {
        if (Estado != EstadoAtencion.Asistencia || _sesionAsistencia is null)
        {
            return;
        }

        var resultado = _servicio.Asistencia.Recuperar(_sesionAsistencia.Value, trabajoId);
        DespuesDeAccion(resultado, "Comprobante original enviado otra vez");
    }

    public void SalirAsistencia()
    {
        if (_sesionAsistencia is { } sesion)
        {
            _servicio.Asistencia.Salir(sesion);
        }

        Limpiar();
    }

    private void DespuesDeAccion(ResultadoAccion resultado, string? exito)
    {
        var sinPrivilegio = resultado.ReasonCode is { } codigo
            && (codigo == "SESSION_NOT_USABLE" || codigo.StartsWith("PERMISSION_", StringComparison.Ordinal));
        if (!resultado.Realizada && sinPrivilegio)
        {
            // El permiso o la sesión cayeron: la asistencia termina y el puesto vuelve a reposo (RB26).
            var aviso = MensajesAtencion.DeMotivo(resultado.ReasonCode);
            SalirAsistencia();
            Aviso = aviso;
            return;
        }

        MensajeAsistencia = !resultado.Realizada
            ? MensajesAtencion.DeMotivo(resultado.ReasonCode)
            : resultado.Envio?.Estado == EstadoTrabajoImpresion.Submitted ? exito : "El comprobante quedó pendiente: revisá la impresora";
        ReimpresionPedida = null;
        Comprobantes = _servicio.Asistencia.ComprobantesDelDia();
        Tocar();
    }

    private Task Confirmar(OpcionCatalogo opcion)
    {
        var solicitud = new SolicitudConfirmacion(Guid.NewGuid(), _sesion!.Value, PersonaId!.Value, opcion.Id, opcion.Version, 1);
        IntentId = solicitud.IntentId;
        Estado = EstadoAtencion.Confirmando;
        Aviso = null;
        Tocar();
        _confirmacion = Ejecutar(solicitud);
        return _confirmacion;
    }

    private async Task Ejecutar(SolicitudConfirmacion solicitud)
    {
        ResultadoConfirmacion resultado;
        try
        {
            // El pedido queda registrado antes de confirmar y fuera de la transacción de RB06: un pedido que se cae antes del
            // commit también se reconstruye (D8). Va en el hilo de la confirmación, no en el de la pantalla.
            resultado = await Task.Run(() =>
            {
                _servicio.RegistroAtencion?.Registrar("CONFIRMATION_REQUESTED", new
                {
                    solicitud.IdentitySessionId,
                    solicitud.IntentId,
                    solicitud.MenuItemId,
                    solicitud.MenuVersion,
                });
                return _servicio.Unidad.Confirmar(solicitud);
            });
        }
        catch (SqliteException)
        {
            Estado = EstadoAtencion.ErrorAlmacenamiento;
            return;
        }

        Procesar(resultado);
    }

    private void Procesar(ResultadoConfirmacion resultado)
    {
        switch (resultado.Tipo)
        {
            case TipoResultado.Autorizada:
                // La impresión ocurre después del commit; un fallo de papel no toca el consumo (RB17).
                var envio = _servicio.Trabajos.EnviarPendientes().FirstOrDefault(e => e.TicketId == resultado.TicketId);
                var estadoOriginal = envio?.Estado ?? EstadoDelOriginal(resultado.TicketId!.Value);
                ComprobantePendiente = estadoOriginal != EstadoTrabajoImpresion.Submitted;
                ConsumoRegistrado = true;
                MotivoResultado = null;
                Mensaje = ComprobantePendiente ? MensajesAtencion.ComprobantePendiente : MensajesAtencion.Exito;
                _codigoMensaje = ComprobantePendiente ? nameof(MensajesAtencion.ComprobantePendiente) : nameof(MensajesAtencion.Exito);
                _decisionId = resultado.DecisionId;
                ComprobanteEnPantalla = !ComprobantePendiente && _servicio.Impresora is ImpresoraSimulada { UltimoEnviado: { } enviado }
                                        && enviado.TicketId == resultado.TicketId
                    ? ComprobanteEnPantalla.Desde(enviado)
                    : null;
                Estado = EstadoAtencion.Resultado;
                break;

            case TipoResultado.Denegada:
                ConsumoRegistrado = false;
                MotivoResultado = resultado.ReasonCode;
                Mensaje = MensajesAtencion.DeMotivo(resultado.ReasonCode);
                _codigoMensaje = resultado.ReasonCode;
                _decisionId = resultado.DecisionId;
                Estado = EstadoAtencion.Resultado;
                break;

            case TipoResultado.CatalogoCambiado:
                // La oferta se vuelve a calcular, no se relee el catálogo entero: lo que se agotó entre tanto ya no está (D5).
                var elegida = Elegida!.Id;
                if (!CargarOferta())
                {
                    break;
                }

                Vigente = _catalogo.FirstOrDefault(o => o.Id == elegida);
                if (Vigente is not null)
                {
                    Aviso = MensajesAtencion.CatalogoCambiado;
                    Estado = EstadoAtencion.Reconfirmacion;
                }
                else
                {
                    Elegida = null;
                    if (_catalogo.Count == 0)
                    {
                        Estado = EstadoAtencion.SinOpciones;
                        _codigoMensaje = nameof(MensajesAtencion.SinOpciones);
                    }
                    else
                    {
                        Aviso = MensajesAtencion.OpcionRetirada;
                        Estado = EstadoAtencion.Seleccion;
                    }
                }

                break;

            case TipoResultado.SesionNoUtilizable:
                _codigoMensaje = nameof(MensajesAtencion.SesionVencida);
                CerrarConMensaje("RESULT");
                Aviso = MensajesAtencion.SesionVencida;
                break;

            default:
                _codigoMensaje = nameof(MensajesAtencion.NoConfirmado);
                CerrarConMensaje("RESULT");
                Aviso = MensajesAtencion.NoConfirmado;
                break;
        }

        Tocar();
    }

    private EstadoTrabajoImpresion EstadoDelOriginal(Guid ticketId) =>
        _servicio.Asistencia.ComprobantesDelDia().FirstOrDefault(c => c.TicketId == ticketId)?.EstadoOriginal
        ?? EstadoTrabajoImpresion.Unknown;

    /// <summary>
    /// Evalúa cada opción del snapshot activo para la persona con la evaluación de la confirmación y se queda con las
    /// ofrecidas, las retenidas y las categorías agotadas. Un error de SQLite deja el error de almacenamiento y devuelve false.
    /// </summary>
    private bool CargarOferta()
    {
        var oferta = Proteger(() => _servicio.Disponibilidad.Ofrecer(_sesion!.Value, PersonaId!.Value));
        if (oferta is null)
        {
            return false;
        }

        _catalogo = oferta.Ofrecidas.Select(MensajesAtencion.Opcion).ToList();
        _retenidas = oferta.Retenidas;
        _agotadas = oferta.Agotadas;
        Pagina = Math.Min(Pagina, TotalPaginas);
        return true;
    }

    /// <summary>
    /// Registra lo que cambia el estado de la atención en su transacción corta (D8). Un error de SQLite deja la atención en
    /// error de almacenamiento, como cualquier otra escritura, y devuelve false.
    /// </summary>
    private bool Registrar(string tipo, object contexto)
    {
        try
        {
            _servicio.RegistroAtencion?.Registrar(tipo, contexto);
            return true;
        }
        catch (SqliteException)
        {
            Estado = EstadoAtencion.ErrorAlmacenamiento;
            return false;
        }
    }

    /// <summary>Registra el fin de la atención y vuelve a reposo; si no se pudo registrar, queda el error de almacenamiento.</summary>
    private void Cerrar(string tipo, object contexto)
    {
        var registrado = Registrar(tipo, contexto);
        Limpiar();
        if (!registrado)
        {
            Estado = EstadoAtencion.ErrorAlmacenamiento;
        }
    }

    /// <summary>SESSION_CLOSED con el código del mensaje que se mostró y la decisión, si hubo (D8).</summary>
    private void CerrarConMensaje(string motivoCierre) => Cerrar("SESSION_CLOSED", new
    {
        IdentitySessionId = _sesion,
        MessageCode = _codigoMensaje,
        DecisionId = _decisionId,
        ClosedBy = motivoCierre,
    });

    /// <summary>Vuelve a reposo sin rastro de la persona anterior.</summary>
    private void Limpiar()
    {
        Estado = EstadoAtencion.Reposo;
        _codigoMensaje = null;
        _decisionId = null;
        _sesion = null;
        _sesionAsistencia = null;
        _confirmacion = null;
        _catalogo = [];
        _retenidas = [];
        _agotadas = [];
        PersonaId = null;
        NombreVisible = null;
        Pagina = 1;
        Elegida = null;
        Vigente = null;
        Mensaje = null;
        MotivoResultado = null;
        ConsumoRegistrado = false;
        ComprobantePendiente = false;
        ComprobanteEnPantalla = null;
        Aviso = null;
        Comprobantes = [];
        ReimpresionPedida = null;
        MensajeAsistencia = null;
        Tocar();
    }

    private void Tocar() => _ultimoToque = _servicio.Reloj.GetUtcNow();

    /// <summary>Un error de SQLite deja la atención en error de almacenamiento, sin informar nada que no se guardó.</summary>
    private T? Proteger<T>(Func<T> accion) where T : class
    {
        try
        {
            return accion();
        }
        catch (SqliteException)
        {
            Estado = EstadoAtencion.ErrorAlmacenamiento;
            return null;
        }
    }
}
