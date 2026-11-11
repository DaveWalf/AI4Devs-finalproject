using System.IO;
using System.Text;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Lector;

/// <summary>
/// Un puerto serie (D2 de elegir-medio-de-identificacion): abre con una configuración, entrega bloques de bytes por un evento
/// y avisa si se cae. Abrir lanza <see cref="FileNotFoundException"/> si el puerto no existe y
/// <see cref="UnauthorizedAccessException"/> si otro programa lo tiene.
/// </summary>
public interface IPuertoSerie : IDisposable
{
    event Action<byte[]>? BytesRecibidos;

    event Action? Caido;

    void Abrir(ConfiguracionLector configuracion);

    void Cerrar();

    /// <summary>Los puertos que el equipo tiene ahora.</summary>
    IReadOnlyList<string> Nombres();
}

/// <summary>
/// El simulador interno y el doble de las pruebas (D2 y D6): lo que se le escribe llega como un bloque de bytes por el mismo
/// evento que un puerto real. Puede simular un puerto que falta, ocupado o que se cae.
/// </summary>
public sealed class PuertoSimulado : IPuertoSerie
{
    public event Action<byte[]>? BytesRecibidos;

    public event Action? Caido;

    public bool Abierto { get; private set; }

    /// <summary>Lo que hace fallar el próximo Abrir: null, «PORT_NOT_FOUND» o «PORT_BUSY».</summary>
    public string? Falla { get; set; }

    public List<string> Puertos { get; } = ["COM3"];

    public void Abrir(ConfiguracionLector configuracion)
    {
        switch (Falla)
        {
            case "PORT_NOT_FOUND":
                throw new FileNotFoundException("El puerto no existe.");
            case "PORT_BUSY":
                throw new UnauthorizedAccessException("El puerto está ocupado.");
        }

        Abierto = true;
    }

    public void Cerrar() => Abierto = false;

    public IReadOnlyList<string> Nombres() => Puertos;

    /// <summary>Escribe bytes como si llegaran por el cable; con el puerto cerrado no llegan.</summary>
    public void Escribir(byte[] bytes)
    {
        if (Abierto)
        {
            BytesRecibidos?.Invoke(bytes);
        }
    }

    /// <summary>Lo tipeado en «Lector simulado», en ASCII, con el terminador de la configuración.</summary>
    public void Pasar(string texto, ConfiguracionLector configuracion) =>
        Escribir([.. Encoding.ASCII.GetBytes(texto), .. configuracion.BytesDelTerminador]);

    /// <summary>El puerto se desconecta.</summary>
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
/// con un lock y publica cada código por un evento, en el hilo del puerto; reintenta cada 5 segundos un puerto que falta,
/// está ocupado o se cayó, y registra en la general cada cambio de estado y los descartes, sin el código ni su largo.
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

    public LectorIdentificacion(IPuertoSerie? real, AlmacenLector almacen, RegistroDelPuesto registro, TimeProvider reloj)
    {
        _real = real;
        _almacen = almacen;
        _registro = registro;
        _reloj = reloj;
        Simulado.BytesRecibidos += bytes => Recibir(Simulado, bytes);
        Simulado.Caido += () => Caer(Simulado);
        if (_real is not null)
        {
            _real.BytesRecibidos += bytes => Recibir(_real, bytes);
            _real.Caido += () => Caer(_real);
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

    /// <summary>Lee la configuración guardada y abre el medio que corresponde, cerrando el anterior.</summary>
    public void Aplicar()
    {
        lock (_cerrojo)
        {
            _reintento?.Dispose();
            _reintento = null;
            _abierto?.Cerrar();
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

    public void Dispose()
    {
        lock (_cerrojo)
        {
            _reintento?.Dispose();
            _reintento = null;
            _abierto?.Cerrar();
            _abierto = null;
        }

        _real?.Dispose();
    }

    private void Abrir(IPuertoSerie puerto)
    {
        try
        {
            puerto.Abrir(Configuracion);
            _abierto = puerto;
            Cambiar(EstadoLector.Escuchando);
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
                if (_abierto is null && Configuracion.Medio == MedioIdentificacion.PuertoSerie && _real is not null)
                {
                    Abrir(_real);
                }
            }
        }, null, Reintento, Timeout.InfiniteTimeSpan);
    }

    private void Caer(IPuertoSerie puerto)
    {
        lock (_cerrojo)
        {
            if (!ReferenceEquals(_abierto, puerto))
            {
                return;
            }

            _abierto = null;
            _armador.Reiniciar();
            Cambiar(EstadoLector.PuertoCaido);
            if (ReferenceEquals(puerto, _real))
            {
                Reintentar();
            }
        }
    }

    private void Recibir(IPuertoSerie puerto, byte[] bytes)
    {
        List<string> codigos = [];
        lock (_cerrojo)
        {
            if (!ReferenceEquals(_abierto, puerto))
            {
                return;
            }

            foreach (var lectura in _armador.Agregar(bytes, _reloj.GetUtcNow()))
            {
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

        Cambio?.Invoke();
    }

    /// <summary>Un descarte a la general, a lo sumo uno por motivo y por minuto, con los acumulados (D7).</summary>
    private void Descartar(string motivo)
    {
        var ahora = _reloj.GetUtcNow();
        int cantidad;
        lock (_descartes)
        {
            if (_descartes.TryGetValue(motivo, out var ventana) && ahora - ventana.Desde < VentanaDescartes)
            {
                _descartes[motivo] = (ventana.Desde, ventana.Cantidad + 1);
                return;
            }

            cantidad = _descartes.TryGetValue(motivo, out var anterior) ? anterior.Cantidad : 0;
            _descartes[motivo] = (ahora, 1);
        }

        Registrar("READER_READING_DISCARDED", new { Reason = motivo, Medium = Medio(), Since = cantidad > 1 ? cantidad - 1 : 0 });
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
