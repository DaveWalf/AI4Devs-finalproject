using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>«Enviar una prueba»: las casillas a las que salió y lo que quedó en la cola.</summary>
public sealed record ResultadoPrueba(int Casillas, int Pendientes);

/// <summary>
/// Los avisos por correo (D4 a D6 de avisar-por-correo): arma cada aviso con su asunto «Cliente · Puesto · Situación», lo
/// encola para las casillas que le tocan, sin repetirlo antes de 15 minutos, y un reloj de cada minuto envía la cola y,
/// una vez por día, la señal de vida. La atención sólo deja el aviso en memoria: leer la configuración, escribir la cola
/// y registrarlo pasa fuera de su hilo, así un archivo o una base ocupada nunca la demoran. Nada de esto lleva un código
/// de identificación, y una falla nunca cierra el puesto.
/// </summary>
public sealed class AvisosCorreo : IDisposable
{
    public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan Periodo = TimeSpan.FromMinutes(1);
    public const string NombreEstado = "correo-estado.json";

    private readonly RegistroDelPuesto _registro;
    private readonly TimeProvider _reloj;
    private readonly TimeZoneInfo _zona;
    private readonly string _carpetaDatos;
    private readonly AlmacenConfiguracionCorreo _configuracion;
    private readonly AlmacenDatosPuesto _puesto;
    private readonly Func<DateTimeOffset, DateTimeOffset, ResumenDia> _resumen;
    private readonly ColaCorreo _cola;
    private readonly Lock _pasada = new();
    private readonly Lock _avisos = new();
    private readonly Lock _drenaje = new();
    private readonly ConcurrentQueue<Action> _porEncolar = new();
    private readonly Dictionary<string, DateTimeOffset> _ultimos = new(StringComparer.Ordinal);
    private readonly Dictionary<DestinoBitacora, DateTimeOffset> _degradadas = [];
    private readonly HashSet<Guid> _enviados = [];
    private DateOnly? _ultimaSenal;
    private ITimer? _temporizador;
    private bool _ultimoEnvioFallo;
    private volatile bool _liberado;
    private int _pendientes;

    public AvisosCorreo(
        RegistroDelPuesto registro, TimeProvider reloj, TimeZoneInfo zona, string carpetaDatos, AlmacenConfiguracionCorreo configuracion,
        AlmacenDatosPuesto puesto, Func<DateTimeOffset, DateTimeOffset, ResumenDia> resumen)
    {
        _registro = registro;
        _reloj = reloj;
        _zona = zona;
        _carpetaDatos = carpetaDatos;
        _configuracion = configuracion;
        _puesto = puesto;
        _resumen = resumen;
        _cola = new ColaCorreo(carpetaDatos);
        Servidor = new CorreoSimulado(carpetaDatos);
        _pendientes = ContarPendientes();
    }

    /// <summary>Al terminar cada pasada, para que la pestaña muestre la cola de ahora.</summary>
    public event Action? PasadaTerminada;

    /// <summary>El servidor simulado, con su modo «Caído».</summary>
    public CorreoSimulado Servidor { get; }

    /// <summary>Los correos que esperan en la cola, contados al encolar y al terminar cada pasada: dibujar no lee la carpeta.</summary>
    public int Pendientes => Volatile.Read(ref _pendientes);

    /// <summary>
    /// Una persona con una credencial conocida que no pudo comer al marcar, a los operadores: su nombre visible y el motivo
    /// en palabras. La ventana es por persona y motivo: dos personas avisan cada una la suya. Sin nombre (un código
    /// desconocido) no hay aviso (D4).
    /// </summary>
    public void PersonaSinConsumo(Guid? personaId, string? nombre, string motivo)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return;
        }

        Diferir(() => Avisar(SituacionCorreo.PersonaSinConsumo, motivo + "|" + (personaId?.ToString("N") ?? nombre), Operadores,
            $"{nombre} no pudo comer al marcar: {MotivoPersona(motivo)}."));
    }

    /// <summary>Un comprobante que no se imprimió, a los operadores: sin papel, una falla o un resultado incierto (D4).</summary>
    public void ComprobanteSinImprimir(string codigo) =>
        Diferir(() => Avisar(SituacionCorreo.Impresora, codigo, Operadores, codigo switch
        {
            "PAPER_OUT" => "La impresora no tiene papel: el consumo quedó registrado y el comprobante, pendiente.",
            "UNKNOWN" => "La impresora no confirmó un comprobante: el consumo quedó registrado y el comprobante puede no haber salido.",
            _ => "La impresora falló al imprimir un comprobante: el consumo quedó registrado y el comprobante, pendiente.",
        }));

    /// <summary>Una exportación automática que falla, al Administrador Principal, con el destino y el motivo en palabras (D4).</summary>
    public void ExportacionFallida(string destino, string motivo) =>
        Diferir(() => Avisar(SituacionCorreo.Exportacion, destino + "|" + motivo, Administrador,
            "La exportación automática" + destino switch
            {
                ExportacionAutomatica.Sftp => " al SFTP simulado",
                ExportacionAutomatica.Carpeta => " a la carpeta",
                _ => string.Empty,
            } + $" falló: {MensajesAtencion.MotivoExportacion(motivo)}. Reintenta en la próxima pasada."));

    /// <summary>
    /// El estado de las bitácoras después de cada pasada, en el hilo de las bitácoras: una que pasa a degradada avisa al
    /// Administrador Principal, no mientras sigue así, y no antes de 15 minutos si vuelve a degradarse (D4).
    /// </summary>
    public void Bitacoras(IReadOnlyList<EstadoBitacora> estados)
    {
        foreach (var estado in estados)
        {
            lock (_avisos)
            {
                if (estado.DegradadaDesde is not { } desde)
                {
                    _degradadas.Remove(estado.Destino);
                    continue;
                }

                if (_degradadas.TryGetValue(estado.Destino, out var conocida) && conocida == desde)
                {
                    continue;
                }

                _degradadas[estado.Destino] = desde;
            }

            var nombre = estado.Destino == DestinoBitacora.General ? "general" : "de interacciones";
            var cuerpo = $"La bitácora {nombre} está degradada desde las {Hora(estado.DegradadaDesde!.Value)}: " +
                         $"{Cantidad(estado.Pendientes, "registro espera", "registros esperan")} su archivo.";
            var destino = estado.Destino.ToString();
            Diferir(() => Avisar(SituacionCorreo.Bitacora, destino, Administrador, cuerpo));
        }
    }

    /// <summary>
    /// «Enviar una prueba»: un correo a todas las casillas guardadas, por la cola, y un intento de envío en el acto.
    /// Devuelve las casillas y lo que quedó pendiente, o null si no hay casillas o los correos están apagados.
    /// </summary>
    public ResultadoPrueba? EnviarPrueba()
    {
        Drenar();
        var configuracion = _configuracion.Leer();
        var todas = Operadores(configuracion).Concat(Administrador(configuracion)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!configuracion.Activos || todas.Count == 0)
        {
            return null;
        }

        Encolar(SituacionCorreo.Prueba, _ => todas, "Correo de prueba del puesto: si llegó, los avisos llegan a esta casilla.");
        Pasada(soloCola: true);
        return new ResultadoPrueba(todas.Count, Pendientes);
    }

    /// <summary>Vuelve a programar el reloj: cada minuto, la primera al minuto de arrancar (D6).</summary>
    public void Programar()
    {
        lock (_pasada)
        {
            _temporizador?.Dispose();
            _temporizador = _reloj.CreateTimer(_ => PasadaDelReloj(), null, Periodo, Periodo);
        }
    }

    public void Dispose()
    {
        _liberado = true;
        lock (_pasada)
        {
            _temporizador?.Dispose();
            _temporizador = null;
        }
    }

    /// <summary>
    /// Una pasada: encola los avisos que esperan, la señal de vida si toca, y envía la cola en orden hasta la primera falla.
    /// La señal va en su propio intento: si falla, la cola igual sale.
    /// </summary>
    public void Pasada(bool soloCola = false)
    {
        try
        {
            Drenar();
            lock (_pasada)
            {
                if (!soloCola)
                {
                    try
                    {
                        RevisarSenalDeVida();
                    }
                    catch (Exception error)
                    {
                        System.Diagnostics.Debug.WriteLine($"INComedor: falló la señal de vida ({error.GetType().Name}).");
                        Registrar("NOTIFICATION_FAILED", new { Situation = SituacionCorreo.Codigo(SituacionCorreo.SenalDeVida), ReasonCode = "HEARTBEAT_FAILED", Error = error.GetType().Name },
                            SeveridadBitacora.Warning);
                    }
                }

                EnviarCola();
            }
        }
        finally
        {
            PasadaTerminada?.Invoke();
        }
    }

    /// <summary>El asunto: «Cliente · Puesto N Nombre · Situación», o el nombre solo sin número, sin caracteres de control (D2).</summary>
    public static string Asunto(string cliente, DatosPuesto puesto, string situacion)
    {
        var nombre = puesto.Nombre.Trim();
        var parte = puesto.Numero is { } numero
            ? "Puesto " + numero.ToString(CultureInfo.InvariantCulture) + (nombre.Length > 0 ? " " + nombre : string.Empty)
            : nombre.Length > 0 ? nombre : "Puesto";
        return new string($"{cliente.Trim()} · {parte} · {situacion}".Select(c => char.IsControl(c) ? ' ' : c).ToArray());
    }

    private static IReadOnlyList<string> Operadores(ConfiguracionCorreo c) => c.Operadores.Where(o => o.Length > 0).ToList();

    private static IReadOnlyList<string> Administrador(ConfiguracionCorreo c) => c.Administrador is { Length: > 0 } a ? [a] : [];

    /// <summary>Deja el aviso en memoria y lo encola fuera del hilo de quien avisa; la pasada siguiente también drena lo que espera.</summary>
    private void Diferir(Action aviso)
    {
        if (_liberado)
        {
            return;
        }

        _porEncolar.Enqueue(aviso);
        _ = Task.Run(Drenar);
    }

    /// <summary>
    /// Encola los avisos que esperan, de a uno por vez. Un aviso que falla de una forma que nadie previó queda registrado y
    /// no frena a los demás: la atención no depende de los correos.
    /// </summary>
    private void Drenar()
    {
        lock (_drenaje)
        {
            while (_porEncolar.TryDequeue(out var aviso))
            {
                try
                {
                    aviso();
                }
                catch (Exception error)
                {
                    System.Diagnostics.Debug.WriteLine($"INComedor: falló un aviso por correo ({error.GetType().Name}).");
                    Registrar("NOTIFICATION_FAILED", new { Situation = "ALL", ReasonCode = "UNEXPECTED_ERROR", Error = error.GetType().Name }, SeveridadBitacora.Warning);
                }
            }
        }
    }

    /// <summary>Un aviso con su clave: no se repite antes de la ventana, que corre recién si el aviso quedó en la cola.</summary>
    private void Avisar(string situacion, string clave, Func<ConfiguracionCorreo, IReadOnlyList<string>> destinatarios, string cuerpo)
    {
        var ahora = _reloj.GetUtcNow();
        var llave = situacion + "|" + clave;
        lock (_avisos)
        {
            if (_ultimos.TryGetValue(llave, out var ultimo) && ahora - ultimo < Ventana)
            {
                return;
            }
        }

        if (Encolar(situacion, destinatarios, cuerpo))
        {
            lock (_avisos)
            {
                _ultimos[llave] = ahora;
            }
        }
    }

    /// <summary>Encola si los correos están activos y la situación tiene casillas; una falla de la cola sólo se registra.</summary>
    private bool Encolar(string situacion, Func<ConfiguracionCorreo, IReadOnlyList<string>> destinatarios, string cuerpo)
    {
        var configuracion = _configuracion.Leer();
        var para = destinatarios(configuracion);
        if (!configuracion.Activos || para.Count == 0)
        {
            return false;
        }

        var ahora = _reloj.GetUtcNow();
        var mensaje = new MensajeCorreo(Guid.NewGuid(), ahora, situacion, configuracion.Remitente, para,
            Asunto(configuracion.Cliente, _puesto.Leer(), situacion), cuerpo + "\n\nHora del puesto: " + Hora(ahora) + ".");
        try
        {
            foreach (var descartado in _cola.Encolar(mensaje))
            {
                Registrar("NOTIFICATION_DISCARDED", new { Situation = SituacionCorreo.Codigo(descartado), ReasonCode = "QUEUE_FULL" }, SeveridadBitacora.Warning);
            }

            Registrar("NOTIFICATION_QUEUED", new { Situation = SituacionCorreo.Codigo(situacion), Recipients = para.Count });
            Volatile.Write(ref _pendientes, ContarPendientes());
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registrar("NOTIFICATION_FAILED", new { Situation = SituacionCorreo.Codigo(situacion), ReasonCode = "QUEUE_UNWRITABLE" }, SeveridadBitacora.Warning);
            return false;
        }
    }

    /// <summary>
    /// Envía la cola en orden hasta la primera falla del servidor. Un pendiente ilegible o incompleto se descarta y se
    /// registra. Si un correo salió pero su pendiente no se pudo quitar, la pasada siguiente lo quita sin enviarlo de nuevo.
    /// </summary>
    private void EnviarCola()
    {
        LecturaCola cola;
        try
        {
            cola = _cola.Leer();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registrar("NOTIFICATION_FAILED", new { Situation = "ALL", ReasonCode = "QUEUE_UNREADABLE" }, SeveridadBitacora.Warning);
            return;
        }

        for (var i = 0; i < cola.Ilegibles; i++)
        {
            Registrar("NOTIFICATION_DISCARDED", new { Situation = "UNKNOWN", ReasonCode = "UNREADABLE" }, SeveridadBitacora.Warning);
        }

        foreach (var mensaje in cola.Validos)
        {
            if (!_enviados.Contains(mensaje.Id))
            {
                try
                {
                    Servidor.Enviar(mensaje);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    if (!_ultimoEnvioFallo)
                    {
                        Registrar("NOTIFICATION_FAILED", new { Situation = SituacionCorreo.Codigo(mensaje.Situacion), ReasonCode = e is ServidorCorreoNoResponde ? "SERVER_UNREACHABLE" : "WRITE_FAILED" },
                            SeveridadBitacora.Warning);
                    }

                    _ultimoEnvioFallo = true;
                    break;
                }

                _ultimoEnvioFallo = false;
                _enviados.Add(mensaje.Id);
                Registrar("NOTIFICATION_SENT", new { Situation = SituacionCorreo.Codigo(mensaje.Situacion) });
            }

            try
            {
                _cola.Quitar(mensaje);
                _enviados.Remove(mensaje.Id);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Registrar("NOTIFICATION_FAILED", new { Situation = SituacionCorreo.Codigo(mensaje.Situacion), ReasonCode = "QUEUE_UNWRITABLE" }, SeveridadBitacora.Warning);
            }
        }

        Volatile.Write(ref _pendientes, ContarPendientes());
    }

    private int ContarPendientes()
    {
        try
        {
            return _cola.Cantidad;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Volatile.Read(ref _pendientes);
        }
    }

    /// <summary>
    /// La señal de vida (D5): pasada la hora elegida y sin una señal de hoy, en memoria o en <c>correo-estado.json</c>, el
    /// resumen del día anterior al Administrador Principal. La fecha se marca recién cuando la señal quedó en la cola.
    /// </summary>
    private void RevisarSenalDeVida()
    {
        var configuracion = _configuracion.Leer();
        if (!configuracion.Activos || Administrador(configuracion).Count == 0)
        {
            return;
        }

        var local = TimeZoneInfo.ConvertTime(_reloj.GetUtcNow(), _zona);
        var hoy = DateOnly.FromDateTime(local.DateTime);
        if (local.TimeOfDay < configuracion.HoraDeLaSenal || (_ultimaSenal ??= UltimaSenal()) >= hoy)
        {
            return;
        }

        var inicioHoy = Inicio(hoy);
        var resumen = _resumen(Inicio(hoy.AddDays(-1)), inicioHoy);
        if (!Encolar(SituacionCorreo.SenalDeVida, Administrador,
                $"El puesto está en marcha. Resumen del {hoy.AddDays(-1).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}: " +
                $"{Cantidad(resumen.Consumos, "consumo", "consumos")}, {Cantidad(resumen.Rechazos, "rechazo", "rechazos")}, " +
                $"{Cantidad(resumen.SinImprimir, "comprobante sin imprimir", "comprobantes sin imprimir")} y " +
                $"{Cantidad(resumen.Advertencias, "advertencia o error", "advertencias o errores")} en la bitácora."))
        {
            return;
        }

        _ultimaSenal = hoy;
        try
        {
            EscrituraAtomica.Escribir(Path.Combine(_carpetaDatos, NombreEstado),
                JsonSerializer.Serialize(new { ultimaSenal = hoy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // La fecha queda en memoria: esta sesión no la repite, y un reinicio a lo sumo la manda otra vez.
            System.Diagnostics.Debug.WriteLine($"INComedor: no se guardó la fecha de la señal de vida ({e.GetType().Name}).");
        }
    }

    private DateOnly UltimaSenal()
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(Path.Combine(_carpetaDatos, NombreEstado)));
            return DateOnly.ParseExact(documento.RootElement.GetProperty("ultimaSenal").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException
                                       or InvalidOperationException or FormatException or ArgumentNullException)
        {
            return DateOnly.MinValue;
        }
    }

    private DateTimeOffset Inicio(DateOnly dia)
    {
        var medianoche = dia.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(medianoche, _zona.GetUtcOffset(medianoche));
    }

    private static string Cantidad(int cantidad, string uno, string varios) =>
        cantidad.ToString(CultureInfo.InvariantCulture) + " " + (cantidad == 1 ? uno : varios);

    private string Hora(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, _zona).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>El reloj corre fuera del hilo de la pantalla: cualquier falla queda registrada y no cierra el puesto (D6).</summary>
    private void PasadaDelReloj()
    {
        try
        {
            Pasada();
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"INComedor: falló una pasada de los correos ({error.GetType().Name}).");
            Registrar("NOTIFICATION_FAILED", new { Situation = "ALL", ReasonCode = "UNEXPECTED_ERROR", Error = error.GetType().Name }, SeveridadBitacora.Warning);
        }
    }

    /// <summary>El motivo de una persona sin consumo, en tercera persona para el operador.</summary>
    private static string MotivoPersona(string motivo) => motivo switch
    {
        "CREDENTIAL_EXPIRED" => "su credencial está vencida",
        "CREDENTIAL_REVOKED" => "su credencial está revocada",
        "IDENTITY_NOT_RESOLVED" => "su credencial todavía no está vigente",
        "PERSON_INACTIVE" => "su habilitación no está activa",
        "LOCAL_LIMIT_REACHED" => "ya usó su cupo",
        "RULE_DENIED" => "la regla vigente no le permite el servicio",
        "SERVICE_NOT_ENABLED" => "no tiene una habilitación para el servicio",
        "OUTSIDE_SERVICE_HOURS" => "la opción no está disponible en este horario",
        "NO_OPTIONS" => "no tenía ninguna opción disponible",
        _ => "no se autorizó el consumo",
    };

    private void Registrar(string tipo, object contexto, string severidad = SeveridadBitacora.Info)
    {
        try
        {
            _registro.Registrar(tipo, contexto, severidad);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            System.Diagnostics.Debug.WriteLine($"INComedor: no se pudo registrar {tipo}.");
        }
    }
}
