using System.Text;

namespace Intelektron.INComedor.Kiosk.Lector;

/// <summary>Una lectura armada: el código, o el motivo por el que se descartó (D4 de elegir-medio-de-identificacion).</summary>
public sealed record LecturaArmada(string? Codigo, string? Descarte)
{
    public static LecturaArmada Valida(string codigo) => new(codigo, null);

    public static LecturaArmada Descartada(string motivo) => new(null, motivo);
}

/// <summary>
/// El armado de las tramas sobre bytes (D4): acumula hasta el terminador elegido, arma una trama partida, separa varias en
/// un bloque y descarta entera una lectura con un byte de control, fuera de ASCII, con espacios internos o de más de 32
/// caracteres. Un resto sin terminar de más de 2 segundos se descarta antes del byte siguiente, así un <c>00</c> colgado no
/// se pega a un <c>01</c> posterior. Con CR LF, un CR o un LF solo también cierran, y la línea vacía entre los dos no es
/// una lectura. El relleno completa con ceros a la izquierda una lectura de sólo dígitos. No es seguro entre hilos: el
/// servicio lo usa con un lock.
/// </summary>
public sealed class ArmadorTramas
{
    public const int LargoMaximo = 32;
    public static readonly TimeSpan Vencimiento = TimeSpan.FromSeconds(2);

    private readonly List<byte> _resto = [];
    private TerminadorLector _terminador;
    private int _relleno;
    private DateTimeOffset? _ultimoByte;
    private bool _desbordado;

    public ArmadorTramas(TerminadorLector terminador, int relleno)
    {
        _terminador = terminador;
        _relleno = relleno;
    }

    /// <summary>Cambia el terminador y el relleno y descarta lo acumulado.</summary>
    public void Configurar(TerminadorLector terminador, int relleno)
    {
        _terminador = terminador;
        _relleno = relleno;
        Reiniciar();
    }

    /// <summary>Descarta lo acumulado, sin lectura: un puerto que se cae o se cierra.</summary>
    public void Reiniciar()
    {
        _resto.Clear();
        _desbordado = false;
        _ultimoByte = null;
    }

    /// <summary>Los bytes de un bloque, llegados en el instante dado: las lecturas que cerraron, en orden.</summary>
    public IReadOnlyList<LecturaArmada> Agregar(ReadOnlySpan<byte> bloque, DateTimeOffset ahora)
    {
        var lecturas = new List<LecturaArmada>();
        if (bloque.IsEmpty)
        {
            return lecturas;
        }

        if ((_resto.Count > 0 || _desbordado) && _ultimoByte is { } ultimo && ahora - ultimo > Vencimiento)
        {
            lecturas.Add(LecturaArmada.Descartada("STALE_FRAGMENT"));
            _resto.Clear();
            _desbordado = false;
        }

        _ultimoByte = ahora;
        foreach (var b in bloque)
        {
            if (EsTerminador(b))
            {
                if (_resto.Count > 0 || _desbordado)
                {
                    lecturas.Add(Cerrar());
                }

                continue;
            }

            if (_desbordado)
            {
                continue;
            }

            _resto.Add(b);
            if (_resto.Count > LargoMaximo + 64)
            {
                // Sin terminador a la vista: deja de acumular hasta el próximo, que cierra la lectura como demasiado larga.
                _desbordado = true;
                _resto.Clear();
            }
        }

        return lecturas;
    }

    private bool EsTerminador(byte b) => _terminador switch
    {
        TerminadorLector.Cr => b == 0x0D,
        TerminadorLector.Lf => b == 0x0A,
        TerminadorLector.Tab => b == 0x09,
        _ => b is 0x0D or 0x0A,
    };

    private LecturaArmada Cerrar()
    {
        try
        {
            if (_desbordado)
            {
                return LecturaArmada.Descartada("TOO_LONG");
            }

            var inicio = 0;
            var fin = _resto.Count;
            while (inicio < fin && _resto[inicio] == 0x20)
            {
                inicio++;
            }

            while (fin > inicio && _resto[fin - 1] == 0x20)
            {
                fin--;
            }

            if (fin == inicio)
            {
                return LecturaArmada.Descartada("EMPTY");
            }

            for (var i = inicio; i < fin; i++)
            {
                if (_resto[i] is < 0x21 or > 0x7E)
                {
                    return LecturaArmada.Descartada("INVALID_CHARACTERS");
                }
            }

            if (fin - inicio > LargoMaximo)
            {
                return LecturaArmada.Descartada("TOO_LONG");
            }

            var codigo = Encoding.ASCII.GetString(_resto.GetRange(inicio, fin - inicio).ToArray());
            return LecturaArmada.Valida(Rellenar(codigo, _relleno));
        }
        finally
        {
            _resto.Clear();
            _desbordado = false;
        }
    }

    /// <summary>Completa con ceros a la izquierda una lectura de sólo dígitos más corta que el relleno.</summary>
    public static string Rellenar(string codigo, int relleno) =>
        relleno > 0 && codigo.Length < relleno && codigo.All(char.IsAsciiDigit) ? codigo.PadLeft(relleno, '0') : codigo;
}
