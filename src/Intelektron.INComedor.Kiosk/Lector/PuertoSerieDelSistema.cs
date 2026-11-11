using System.IO;
using System.IO.Ports;

namespace Intelektron.INComedor.Kiosk.Lector;

/// <summary>
/// El puerto serie del equipo con <see cref="SerialPort"/> (D2 de elegir-medio-de-identificacion): sin control de flujo, con
/// DTR activo y leyendo bytes crudos, sin la codificación del puerto. Lo registra sólo la ventana, como el ejecutor de
/// netsh: ninguna prueba ni la CI abren un puerto COM; lo prueba David con el lector real.
/// </summary>
public sealed class PuertoSerieDelSistema : IPuertoSerie
{
    private readonly Lock _cerrojo = new();
    private SerialPort? _puerto;

    public event Action<byte[]>? BytesRecibidos;

    public event Action? Caido;

    public void Abrir(ConfiguracionLector configuracion)
    {
        lock (_cerrojo)
        {
            Cerrar();
            var puerto = new SerialPort(configuracion.Puerto!, configuracion.Velocidad, configuracion.Paridad switch
            {
                ParidadLector.Par => Parity.Even,
                ParidadLector.Impar => Parity.Odd,
                _ => Parity.None,
            }, configuracion.BitsDatos, configuracion.BitsParada == 2 ? StopBits.Two : StopBits.One)
            {
                Handshake = Handshake.None,
                DtrEnable = true,
                ReadTimeout = 500,
            };
            puerto.DataReceived += Recibido;
            try
            {
                puerto.Open();
            }
            catch
            {
                puerto.DataReceived -= Recibido;
                puerto.Dispose();
                throw;
            }

            _puerto = puerto;
        }
    }

    public void Cerrar()
    {
        lock (_cerrojo)
        {
            if (_puerto is null)
            {
                return;
            }

            _puerto.DataReceived -= Recibido;
            try
            {
                _puerto.Close();
            }
            catch (IOException)
            {
                // Un puerto que ya se desconectó no se puede cerrar: alcanza con soltarlo.
                System.Diagnostics.Debug.WriteLine("INComedor: el puerto del lector ya no estaba al cerrarlo.");
            }

            _puerto.Dispose();
            _puerto = null;
        }
    }

    public IReadOnlyList<string> Nombres() => SerialPort.GetPortNames().Order(StringComparer.OrdinalIgnoreCase).ToList();

    public void Dispose() => Cerrar();

    /// <summary>Los bytes del hilo del puerto; una falla al leer es un puerto que se cayó.</summary>
    private void Recibido(object remitente, SerialDataReceivedEventArgs argumentos)
    {
        byte[] bytes;
        try
        {
            var puerto = (SerialPort)remitente;
            var cantidad = puerto.BytesToRead;
            if (cantidad <= 0)
            {
                return;
            }

            bytes = new byte[cantidad];
            var leidos = puerto.Read(bytes, 0, cantidad);
            if (leidos < cantidad)
            {
                Array.Resize(ref bytes, leidos);
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or TimeoutException)
        {
            Caido?.Invoke();
            return;
        }

        BytesRecibidos?.Invoke(bytes);
    }
}
