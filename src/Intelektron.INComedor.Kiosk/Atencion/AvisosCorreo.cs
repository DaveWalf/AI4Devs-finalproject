using System.Globalization;
using System.IO;
using System.Text.Json;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Los avisos por correo (D4 a D6 de avisar-por-correo): arma cada aviso con su asunto «Cliente · Puesto · Situación», lo
/// encola para las casillas que le tocan, sin repetirlo antes de 15 minutos, y un reloj de cada minuto envía la cola y,
/// una vez por día, la señal de vida. Nada de esto lleva un código de identificación, y una falla nunca cierra el puesto.
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
    private readonly ResumenDelDia _resumen;
    private readonly ColaCorreo _cola;
    private readonly Lock _pasada = new();
    private readonly Lock _avisos = new();
    private readonly Dictionary<string, DateTimeOffset> _ultimos = new(StringComparer.Ordinal);
    private readonly Dictionary<DestinoBitacora, DateTimeOffset> _degradadas = [];
    private ITimer? _temporizador;
    private bool _ultimoEnvioFallo;

    public AvisosCorreo(
        RegistroDelPuesto registro, TimeProvider reloj, TimeZoneInfo zona, string carpetaDatos, AlmacenConfiguracionCorreo configuracion,
        AlmacenDatosPuesto puesto, ResumenDelDia resumen)
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
    }

    /// <summary>Al terminar cada pasada, para que la pestaña muestre la cola de ahora.</summary>
    public event Action? PasadaTerminada;

    /// <summary>El servidor simulado, con su modo «Caído».</summary>
    public CorreoSimulado Servidor { get; }

    /// <summary>Los correos que esperan en la cola.</summary>
    public int Pendientes
    {
        get
        {
            try
            {
                return _cola.Cantidad;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Una persona con una credencial conocida que no pudo comer al marcar, a los operadores: su nombre visible y el motivo
    /// en palabras. Sin nombre (un código desconocido) no hay aviso (D4).
    /// </summary>
    public void PersonaSinConsumo(string? nombre, string motivo)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return;
        }

        Avisar(SituacionCorreo.PersonaSinConsumo, motivo + "|" + nombre, Operadores,
            $"{nombre} no pudo comer al marcar: {MotivoPersona(motivo)}.");
    }

    /// <summary>Un comprobante que no se imprimió, a los operadores: sin papel, una falla o un resultado incierto (D4).</summary>
    public void ComprobanteSinImprimir(string codigo) =>
        Avisar(SituacionCorreo.Impresora, codigo, Operadores, codigo switch
        {
            "PAPER_OUT" => "La impresora no tiene papel: el consumo quedó registrado y el comprobante, pendiente.",
            "UNKNOWN" => "La impresora no confirmó un comprobante: el consumo quedó registrado y el comprobante puede no haber salido.",
            _ => "La impresora falló al imprimir un comprobante: el consumo quedó registrado y el comprobante, pendiente.",
        });

    /// <summary>Una exportación automática que falla, al Administrador Principal, con el destino y el motivo (D4).</summary>
    public void ExportacionFallida(string destino, string motivo) =>
        Avisar(SituacionCorreo.Exportacion, destino + "|" + motivo, Administrador,
            $"La exportación automática {(destino == ExportacionAutomatica.Sftp ? "al SFTP simulado" : "a la carpeta")} falló ({motivo}); reintenta en la próxima pasada.");

    /// <summary>
    /// El estado de las bitácoras después de cada pasada: una que pasa a degradada avisa al Administrador Principal una
    /// vez, y no mientras sigue así (D4).
    /// </summary>
    public void Bitacoras(IReadOnlyList<EstadoBitacora> estados)
    {
        foreach (var estado in estados)
        {
            bool nueva;
            lock (_avisos)
            {
                if (estado.DegradadaDesde is not { } desde)
                {
                    _degradadas.Remove(estado.Destino);
                    continue;
                }

                nueva = !_degradadas.TryGetValue(estado.Destino, out var conocida) || conocida != desde;
                _degradadas[estado.Destino] = desde;
            }

            if (nueva)
            {
                var nombre = estado.Destino == DestinoBitacora.General ? "general" : "de interacciones";
                Encolar(SituacionCorreo.Bitacora, Administrador,
                    $"La bitácora {nombre} está degradada desde las {Hora(estado.DegradadaDesde!.Value)}: {estado.Pendientes} registros esperan su archivo.");
            }
        }
    }

    /// <summary>
    /// «Enviar una prueba»: un correo a todas las casillas guardadas, por la cola, y un intento de envío en el acto.
    /// Devuelve los que quedaron pendientes, o null si no hay casillas o los correos están apagados.
    /// </summary>
    public int? EnviarPrueba()
    {
        var configuracion = _configuracion.Leer();
        var todas = configuracion.Operadores.Concat(configuracion.Administrador is { } a ? [a] : []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!configuracion.Activos || todas.Count == 0)
        {
            return null;
        }

        Encolar(SituacionCorreo.Prueba, _ => todas, "Correo de prueba del puesto: si llegó, los avisos llegan a esta casilla.");
        Pasada(soloCola: true);
        return Pendientes;
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
        lock (_pasada)
        {
            _temporizador?.Dispose();
            _temporizador = null;
        }
    }

    /// <summary>Una pasada: envía la cola en orden hasta la primera falla y, si toca, encola y envía la señal de vida.</summary>
    public void Pasada(bool soloCola = false)
    {
        try
        {
            PasadaBloqueada(soloCola);
        }
        finally
        {
            PasadaTerminada?.Invoke();
        }
    }

    private void PasadaBloqueada(bool soloCola)
    {
        lock (_pasada)
        {
            if (!soloCola)
            {
                RevisarSenalDeVida();
            }

            foreach (var mensaje in _cola.Pendientes())
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
                    return;
                }

                _ultimoEnvioFallo = false;
                _cola.Quitar(mensaje);
                Registrar("NOTIFICATION_SENT", new { Situation = SituacionCorreo.Codigo(mensaje.Situacion) });
            }
        }
    }

    /// <summary>El asunto: «Cliente · Puesto N Nombre · Situación», sin caracteres de control (D2).</summary>
    public static string Asunto(string cliente, DatosPuesto puesto, string situacion)
    {
        var nombre = puesto.Nombre.Trim();
        var parte = puesto.Numero is { } numero
            ? "Puesto " + numero.ToString(CultureInfo.InvariantCulture) + (nombre.Length > 0 ? " " + nombre : string.Empty)
            : nombre.Length > 0 ? nombre : "Puesto";
        return new string($"{cliente.Trim()} · {parte} · {situacion}".Select(c => char.IsControl(c) ? ' ' : c).ToArray());
    }

    private static IReadOnlyList<string> Operadores(ConfiguracionCorreo c) => c.Operadores;

    private static IReadOnlyList<string> Administrador(ConfiguracionCorreo c) => c.Administrador is { } a ? [a] : [];

    /// <summary>Un aviso con su clave, que no se repite antes de la ventana.</summary>
    private void Avisar(string situacion, string clave, Func<ConfiguracionCorreo, IReadOnlyList<string>> destinatarios, string cuerpo)
    {
        var ahora = _reloj.GetUtcNow();
        lock (_avisos)
        {
            var llave = situacion + "|" + clave;
            if (_ultimos.TryGetValue(llave, out var ultimo) && ahora - ultimo < Ventana)
            {
                return;
            }

            _ultimos[llave] = ahora;
        }

        Encolar(situacion, destinatarios, cuerpo);
    }

    /// <summary>Encola si los correos están activos y la situación tiene casillas; una falla de la cola sólo se registra.</summary>
    private void Encolar(string situacion, Func<ConfiguracionCorreo, IReadOnlyList<string>> destinatarios, string cuerpo)
    {
        var configuracion = _configuracion.Leer();
        var para = destinatarios(configuracion);
        if (!configuracion.Activos || para.Count == 0)
        {
            return;
        }

        var ahora = _reloj.GetUtcNow();
        var mensaje = new MensajeCorreo(Guid.NewGuid(), ahora, situacion, configuracion.Remitente, para,
            Asunto(configuracion.Cliente, _puesto.Leer(), situacion), cuerpo + "\n\nHora del puesto: " + Hora(ahora) + ".");
        try
        {
            foreach (var descartado in _cola.Encolar(mensaje))
            {
                Registrar("NOTIFICATION_DISCARDED", new { Situation = SituacionCorreo.Codigo(descartado) }, SeveridadBitacora.Warning);
            }

            Registrar("NOTIFICATION_QUEUED", new { Situation = SituacionCorreo.Codigo(situacion), Recipients = para.Count });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registrar("NOTIFICATION_FAILED", new { Situation = SituacionCorreo.Codigo(situacion), ReasonCode = "QUEUE_UNWRITABLE" }, SeveridadBitacora.Warning);
        }
    }

    /// <summary>
    /// La señal de vida (D5): pasada la hora elegida y sin una señal de hoy en <c>correo-estado.json</c>, el resumen del
    /// día anterior al Administrador Principal.
    /// </summary>
    private void RevisarSenalDeVida()
    {
        var configuracion = _configuracion.Leer();
        if (!configuracion.Activos || configuracion.Administrador is null)
        {
            return;
        }

        var local = TimeZoneInfo.ConvertTime(_reloj.GetUtcNow(), _zona);
        var hoy = DateOnly.FromDateTime(local.DateTime);
        if (local.TimeOfDay < configuracion.HoraDeLaSenal || UltimaSenal() >= hoy)
        {
            return;
        }

        var inicioHoy = Inicio(hoy);
        var resumen = _resumen.Contar(Inicio(hoy.AddDays(-1)), inicioHoy);
        Encolar(SituacionCorreo.SenalDeVida, Administrador,
            $"El puesto está en marcha. Resumen del {hoy.AddDays(-1).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}: " +
            $"{Cantidad(resumen.Consumos, "consumo", "consumos")}, {Cantidad(resumen.Rechazos, "rechazo", "rechazos")}, " +
            $"{Cantidad(resumen.SinImprimir, "comprobante sin imprimir", "comprobantes sin imprimir")} y " +
            $"{Cantidad(resumen.Advertencias, "advertencia o error", "advertencias o errores")} en la bitácora.");
        try
        {
            EscrituraAtomica.Escribir(Path.Combine(_carpetaDatos, NombreEstado),
                JsonSerializer.Serialize(new { ultimaSenal = hoy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
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
        "NO_OPTIONS" => "no tenía ninguna opción disponible (cupo usado o sin habilitación)",
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
