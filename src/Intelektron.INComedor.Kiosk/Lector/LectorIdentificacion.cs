using System.IO;
using System.Text;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Lector;

/// <summary>
/// Un puerto serie (D2 de elegir-medio-de-identificacion): abre con una configuración, entrega bloques de bytes por un evento
/// y avisa si se cae. Abrir lanza <see cref="FileNotFoundException"/> o <see cref="IOException"/> si el puerto no existe,
/// <see cref="UnauthorizedAccessException"/> si otro programa lo tiene y <see cref="PlatformNotSupportedException"/> si el
/// equipo no tiene puertos serie.
/// </summary>
public interface IPuertoSerie : IDisposable
{
    event Action<byte[]>? BytesRecibidos;

    event Action? Caido;

    /// <summary>
    /// Si el puerto abierto sigue ahí: abierto y todavía en la lista del equipo. Un lector USB que se desenchufa no avisa
    /// por ningún evento, así que el servicio lo pregunta cada 5 segundos (D9).
    /// </summary>
    bool Vivo { get; }

    void Abrir(ConfiguracionLector configuracion);

    void Cerrar();

    /// <summary>Los puertos que el equipo tiene ahora.</summary>
    IReadOnlyList<string> Nombres();
}

/// <summary>
/// El simulador interno y el doble de las pruebas (D2 y D6): lo que se le escribe llega como un bloque de bytes por el mismo
/// evento que un puerto real. Puede simular un puerto que falta, ocupado, sin soporte, que se cae, que se desenchufa (sale
/// de la lista) o que no se deja cerrar.
/// </summary>
public sealed class PuertoSimulado : IPuertoSerie
{
    private string? _abiertoEn;

    public event Action<byte[]>? BytesRecibidos;

    public event Action? Caido;

    public bool Abierto { get; private set; }

    /// <summary>Lo que hace fallar el próximo Abrir: null, «PORT_NOT_FOUND», «PORT_BUSY» o «UNSUPPORTED».</summary>
    public string? Falla { get; set; }

    /// <summary>Cerrar falla como un puerto que se desenchufó y no se deja soltar.</summary>
    public bool FallaAlCerrar { get; set; }

    public List<string> Puertos { get; } = ["COM3"];

    public bool Vivo => Abierto && (_abiertoEn is null || Puertos.Contains(_abiertoEn, StringComparer.OrdinalIgnoreCase));

    public void Abrir(ConfiguracionLector configuracion)
    {
        switch (Falla)
        {
            case "PORT_NOT_FOUND":
                throw new FileNotFoundException("El puerto no existe.");
            case "PORT_BUSY":
                throw new UnauthorizedAccessException("El puerto está ocupado.");
            case "UNSUPPORTED":
                throw new PlatformNotSupportedException("Sin puertos serie.");
        }

        _abiertoEn = configuracion.Medio == MedioIdentificacion.PuertoSerie ? configuracion.Puerto : null;
        Abierto = true;
    }

    public void Cerrar()
    {
        Abierto = false;
        if (FallaAlCerrar)
        {
            throw new UnauthorizedAccessException("El puerto ya no está.");
        }
    }

    public IReadOnlyList<string> Nombres() => Puertos;

    /// <summary>Escribe bytes como si llegaran por el cable; con el puerto cerrado no llegan.</summary>
    public void Escribir(byte[] bytes)
    {
        if (Abierto)
        {
            BytesRecibidos?.Invoke(bytes);
        }
    }

    /// <summary>
    /// Lo tipeado en «Lector simulado», byte por carácter como lo mandaría un lector (una ñ llega como 0xF1, no como un
    /// signo de pregunta), con el terminador de la configuración.
    /// </summary>
    public void Pasar(string texto, ConfiguracionLector configuracion) =>
        Escribir([.. Encoding.Latin1.GetBytes(texto), .. configuracion.BytesDelTerminador]);

    /// <summary>El puerto se desconecta y lo avisa.</summary>
    public void Caer()
    {
        Abierto = false;
        Caido?.Invoke();
    }

    public void Dispose() => Abierto = false;
}

/// <summary>Cómo está el lector, para la pestaña y la bitácora (D9).</summary>
public enum EstadoLector
{
    Apagado,
    Escuchando,
    PuertoNoExiste,
    PuertoOcupado,
    PuertoCaido,
    SinPuerto,
}

/// <summary>La última lectura: la hora, el resultado (IDENTIFIED, REJECTED o DISCARDED) y el motivo, sin el código (D8).</summary>
public sealed record UltimaLectura(DateTimeOffset Hora, string Resultado, string? Motivo);

/// <summary>
/// El lector de la identificación (D1, D5, D7 y D9): según el medio, escucha el puerto real o el simulado, arma las lecturas
/// con un lock y publica cada código por un evento, en el hilo del puerto. Mientras escucha el puerto real, pregunta cada
/// 5 segundos si sigue ahí; uno que falta, está ocupado o se cayó se reintenta cada 5 segundos. Registra en la general cada
/// cambio de estado y los descartes, sin el código ni su largo. El puerto real es del contenedor: el lector lo cierra, no
/// lo libera.
/// </summary>
public sealed class LectorIdentificacion : IDisposable
{
    public static readonly TimeSpan Reintento = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan VentanaDescartes = TimeSpan.FromMinutes(1);

    private readonly IPuertoSerie? _real;
    private readonly AlmacenLector _almacen;
    private readonly RegistroDelPuesto _registro;
    private readonly TimeProvider _reloj;
    private readonly Lock _cerrojo = new();
    private readonly ArmadorTramas _armador = new(TerminadorLector.CrLf, 0);
    private readonly Dictionary<string, (DateTimeOffset Desde, int Cantidad)> _descartes = new(StringComparer.Ordinal);
    private IPuertoSerie? _abierto;
    private ITimer? _reintento;
    private ITimer? _vigilancia;
    private bool _liberado;

    public LectorIdentificacion(IPuertoSerie? real, AlmacenLector almacen, RegistroDelPuesto registro, TimeProvider reloj)
    {
        _real = real;
        _almacen = almacen;
        _registro = registro;
        _reloj = reloj;
        Simulado.BytesRecibidos += RecibirDelSimulado;
        Simulado.Caido += CaerElSimulado;
        if (_real is not null)
        {
            _real.BytesRecibidos += RecibirDelReal;
            _real.Caido += CaerElReal;
        }
    }

    /// <summary>Un código armado y rellenado, con su origen (SERIAL_PORT o SIMULATED_READER), en el hilo del puerto.</summary>
    public event Action<string, string>? Lectura;

    /// <summary>Un cambio de estado o de la última lectura, para que la pestaña se vuelva a dibujar.</summary>
    public event Action? Cambio;

    /// <summary>El simulador interno, que también es el doble de las pruebas.</summary>
    public PuertoSimulado Simulado { get; } = new();

    public EstadoLector Estado { get; private set; } = EstadoLector.Apagado;

    public ConfiguracionLector Configuracion { get; private set; } = ConfiguracionLector.PorDefecto;

    public UltimaLectura? Ultima { get; private set; }

    /// <summary>Los puertos que el equipo tiene ahora; sin el puerto del sistema, los del simulado.</summary>
    public IReadOnlyList<string> Puertos() => (_real ?? Simulado).Nombres();

    /// <summary>Lee la configuración guardada y abre el medio que corresponde, cerrando el anterior aunque no se deje.</summary>
    public void Aplicar()
    {
        lock (_cerrojo)
        {
            if (_liberado)
            {
                return;
            }

            DetenerRelojes();
            CerrarSinFallar(_abierto);
            _abierto = null;
            Configuracion = _almacen.Leer();
            _armador.Configurar(Configuracion.Terminador, Configuracion.Relleno);
            switch (Configuracion.Medio)
            {
                case MedioIdentificacion.Simulador:
                    Abrir(Simulado);
                    break;
                case MedioIdentificacion.PuertoSerie when _real is null:
                    Cambiar(EstadoLector.SinPuerto);
                    break;
                case MedioIdentificacion.PuertoSerie:
                    Abrir(_real);
                    break;
                default:
                    Cambiar(EstadoLector.Apagado);
                    break;
            }
        }
    }

    /// <summary>Lo que la pantalla decidió con una lectura: identificó, se rechazó o se descartó, con su motivo.</summary>
    public void Resultado(string resultado, string? motivo)
    {
        Ultima = new UltimaLectura(_reloj.GetUtcNow(), resultado, motivo);
        if (resultado == "DISCARDED" && motivo is not null)
        {
            Descartar(motivo);
        }

        Cambio?.Invoke();
    }

    /// <summary>Suelta los relojes, cierra el puerto abierto y deja de escuchar; un reintento que llegue tarde no abre nada.</summary>
    public void Dispose()
    {
        lock (_cerrojo)
        {
            _liberado = true;
            DetenerRelojes();
            CerrarSinFallar(_abierto);
            _abierto = null;
        }

        Simulado.BytesRecibidos -= RecibirDelSimulado;
        Simulado.Caido -= CaerElSimulado;
        if (_real is not null)
        {
            _real.BytesRecibidos -= RecibirDelReal;
            _real.Caido -= CaerElReal;
        }
    }

    private void RecibirDelSimulado(byte[] bytes) => Recibir(Simulado, bytes);

    private void RecibirDelReal(byte[] bytes) => Recibir(_real!, bytes);

    private void CaerElSimulado() => Caer(Simulado);

    private void CaerElReal() => Caer(_real!);

    private void Abrir(IPuertoSerie puerto)
    {
        try
        {
            puerto.Abrir(Configuracion);
            _abierto = puerto;
            Cambiar(EstadoLector.Escuchando);
            if (ReferenceEquals(puerto, _real))
            {
                _vigilancia = _reloj.CreateTimer(_ => Vigilar(), null, Reintento, Reintento);
            }
        }
        catch (PlatformNotSupportedException)
        {
            Cambiar(EstadoLector.SinPuerto);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Cambiar(e is UnauthorizedAccessException ? EstadoLector.PuertoOcupado : EstadoLector.PuertoNoExiste);
            Reintentar();
        }
    }

    private void Reintentar()
    {
        _reintento?.Dispose();
        _reintento = _reloj.CreateTimer(_ =>
        {
            lock (_cerrojo)
            {
                if (!_liberado && _abierto is null && Configuracion.Medio == MedioIdentificacion.PuertoSerie && _real is not null)
                {
                    Abrir(_real);
                }
            }
        }, null, Reintento, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Cada 5 segundos, si el puerto real sigue ahí: uno que se desenchufó cae y se reintenta (D9).</summary>
    private void Vigilar()
    {
        lock (_cerrojo)
        {
            if (!_liberado && _real is not null && ReferenceEquals(_abierto, _real) && !_real.Vivo)
            {
                CaerBloqueado(_real);
            }
        }
    }

    private void Caer(IPuertoSerie puerto)
    {
        lock (_cerrojo)
        {
            if (!_liberado && ReferenceEquals(_abierto, puerto))
            {
                CaerBloqueado(puerto);
            }
        }
    }

    private void CaerBloqueado(IPuertoSerie puerto)
    {
        _vigilancia?.Dispose();
        _vigilancia = null;
        CerrarSinFallar(puerto);
        _abierto = null;
        _armador.Reiniciar();
        Cambiar(EstadoLector.PuertoCaido);
        if (ReferenceEquals(puerto, _real))
        {
            Reintentar();
        }
    }

    private void DetenerRelojes()
    {
        _reintento?.Dispose();
        _reintento = null;
        _vigilancia?.Dispose();
        _vigilancia = null;
    }

    /// <summary>Un puerto que se desenchufó puede no dejarse cerrar: se suelta igual y se sigue.</summary>
    private static void CerrarSinFallar(IPuertoSerie? puerto)
    {
        try
        {
            puerto?.Cerrar();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ObjectDisposedException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine($"INComedor: el puerto del lector no se dejó cerrar ({e.GetType().Name}).");
        }
    }

    private void Recibir(IPuertoSerie puerto, byte[] bytes)
    {
        List<string> codigos = [];
        var hubo = false;
        lock (_cerrojo)
        {
            if (_liberado || !ReferenceEquals(_abierto, puerto))
            {
                return;
            }

            foreach (var lectura in _armador.Agregar(bytes, _reloj.GetUtcNow()))
            {
                hubo = true;
                if (lectura.Codigo is { } codigo)
                {
                    codigos.Add(codigo);
                }
                else
                {
                    Ultima = new UltimaLectura(_reloj.GetUtcNow(), "DISCARDED", lectura.Descarte);
                    Descartar(lectura.Descarte!);
                }
            }
        }

        var origen = ReferenceEquals(puerto, Simulado) ? "SIMULATED_READER" : "SERIAL_PORT";
        foreach (var codigo in codigos)
        {
            Lectura?.Invoke(codigo, origen);
        }

        // Sólo un bloque que cerró una lectura cambia lo que la pestaña muestra.
        if (hubo)
        {
            Cambio?.Invoke();
        }
    }

    /// <summary>Un descarte a la general, a lo sumo uno por motivo y por minuto, con los acumulados de la ventana anterior (D7).</summary>
    private void Descartar(string motivo)
    {
        var ahora = _reloj.GetUtcNow();
        int acumulados;
        lock (_descartes)
        {
            if (_descartes.TryGetValue(motivo, out var ventana) && ahora - ventana.Desde < VentanaDescartes)
            {
                _descartes[motivo] = (ventana.Desde, ventana.Cantidad + 1);
                return;
            }

            acumulados = _descartes.TryGetValue(motivo, out var anterior) ? anterior.Cantidad - 1 : 0;
            _descartes[motivo] = (ahora, 1);
        }

        Registrar("READER_READING_DISCARDED", new { Reason = motivo, Medium = Medio(), Accumulated = Math.Max(acumulados, 0) });
    }

    private void Cambiar(EstadoLector nuevo)
    {
        if (nuevo == Estado)
        {
            return;
        }

        var anterior = Estado;
        Estado = nuevo;
        Registrar("READER_STATE_CHANGED", new { From = Codigo(anterior), To = Codigo(nuevo), Medium = Medio(), Port = Configuracion.Puerto });
        Cambio?.Invoke();
    }

    private string Medio() => Configuracion.Medio switch
    {
        MedioIdentificacion.Simulador => "SIMULATOR",
        MedioIdentificacion.PuertoSerie => "SERIAL_PORT",
        _ => "KEYBOARD",
    };

    public static string Codigo(EstadoLector estado) => estado switch
    {
        EstadoLector.Escuchando => "LISTENING",
        EstadoLector.PuertoNoExiste => "PORT_NOT_FOUND",
        EstadoLector.PuertoOcupado => "PORT_BUSY",
        EstadoLector.PuertoCaido => "PORT_LOST",
        EstadoLector.SinPuerto => "NO_SERIAL_SUPPORT",
        _ => "OFF",
    };

    private void Registrar(string tipo, object contexto)
    {
        try
        {
            _registro.Registrar(tipo, contexto);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            System.Diagnostics.Debug.WriteLine($"INComedor: no se pudo registrar {tipo}.");
        }
    }
}
