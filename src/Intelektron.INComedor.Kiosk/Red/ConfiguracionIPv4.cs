using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Intelektron.INComedor.Kiosk.Red;

/// <summary>Los campos de la edición de la IPv4, para señalar el que no es válido.</summary>
public enum CampoIPv4
{
    Direccion,
    Mascara,
    Puerta,
    Dns1,
    Dns2,
}

/// <summary>
/// Una IPv4 que pasó <see cref="ConfiguracionIPv4.Validar"/>: sólo se crea ahí, y sus valores están en su forma canónica.
/// Es lo único que <see cref="ArgumentosNetsh.Armar"/> acepta (D11).
/// </summary>
public sealed record IPv4Validada
{
    internal IPv4Validada(bool dhcp, string? direccion, string? mascara, string? puerta, IReadOnlyList<string> dns)
    {
        Dhcp = dhcp;
        Direccion = direccion;
        Mascara = mascara;
        Puerta = puerta;
        Dns = dns;
    }

    public bool Dhcp { get; }

    public string? Direccion { get; }

    public string? Mascara { get; }

    public string? Puerta { get; }

    public IReadOnlyList<string> Dns { get; }
}

/// <summary>La IPv4 validada o los campos que no lo son.</summary>
public sealed record ValidacionIPv4(IPv4Validada? Valida, IReadOnlyList<CampoIPv4> Invalidos);

/// <summary>Lo cargado en la pestaña Red: DHCP, o una IPv4 fija con su máscara, una puerta opcional y hasta dos DNS (D11).</summary>
public sealed record ConfiguracionIPv4(bool Dhcp, string Direccion = "", string Mascara = "", string Puerta = "", string Dns1 = "", string Dns2 = "")
{
    /// <summary>
    /// DHCP pasa sin más. Una IPv4 fija pide la dirección (unicast, ni loopback ni la red o la difusión de su subred) y una
    /// máscara contigua; la puerta, si está, tiene que ser otra dirección de la misma subred; cada DNS, si está, una IPv4.
    /// </summary>
    public ValidacionIPv4 Validar()
    {
        if (Dhcp)
        {
            return new ValidacionIPv4(new IPv4Validada(true, null, null, null, []), []);
        }

        var invalidos = new List<CampoIPv4>();
        var direccionValida = EsIPv4(Direccion, out var direccion) && EsUnicast(direccion);
        var mascaraValida = EsIPv4(Mascara, out var mascara) && Prefijo(mascara) >= 1;
        if (direccionValida && mascaraValida && Prefijo(mascara) <= 30)
        {
            var host = Numero(direccion) & ~Numero(mascara);
            direccionValida = host != 0 && host != ~Numero(mascara);
        }

        if (!direccionValida)
        {
            invalidos.Add(CampoIPv4.Direccion);
        }

        if (!mascaraValida)
        {
            invalidos.Add(CampoIPv4.Mascara);
        }

        string? puerta = null;
        if (Puerta.Trim().Length > 0)
        {
            var puertaValida = EsIPv4(Puerta, out var valorPuerta) && EsUnicast(valorPuerta)
                && (!direccionValida || !mascaraValida
                    || ((Numero(valorPuerta) & Numero(mascara)) == (Numero(direccion) & Numero(mascara)) && !valorPuerta.Equals(direccion)));
            if (puertaValida)
            {
                puerta = valorPuerta.ToString();
            }
            else
            {
                invalidos.Add(CampoIPv4.Puerta);
            }
        }

        var dns = new List<string>();
        foreach (var (texto, campo) in new[] { (Dns1, CampoIPv4.Dns1), (Dns2, CampoIPv4.Dns2) })
        {
            if (texto.Trim().Length == 0)
            {
                continue;
            }

            if (EsIPv4(texto, out var servidor) && EsUnicast(servidor))
            {
                dns.Add(servidor.ToString());
            }
            else
            {
                invalidos.Add(campo);
            }
        }

        return invalidos.Count > 0
            ? new ValidacionIPv4(null, invalidos)
            : new ValidacionIPv4(new IPv4Validada(false, direccion.ToString(), mascara.ToString(), puerta, dns), []);
    }

    /// <summary>
    /// Una IPv4 en notación decimal con puntos y en su forma canónica: rechaza «1», «010.1.1.1» (que .NET lee como octal) o
    /// un texto con algo más, que <see cref="IPAddress.TryParse(string?, out IPAddress?)"/> podría aceptar o reinterpretar.
    /// </summary>
    internal static bool EsIPv4(string? texto, out IPAddress direccion)
    {
        direccion = IPAddress.None;
        var limpio = texto?.Trim() ?? string.Empty;
        if (!IPAddress.TryParse(limpio, out var leida) || leida.AddressFamily != AddressFamily.InterNetwork || leida.ToString() != limpio)
        {
            return false;
        }

        direccion = leida;
        return true;
    }

    /// <summary>Del 1 al 223 en el primer octeto y no loopback: ni 0.x, ni 127.x, ni multicast ni reservadas.</summary>
    private static bool EsUnicast(IPAddress direccion) => direccion.GetAddressBytes()[0] is >= 1 and <= 223 and not 127;

    private static uint Numero(IPAddress direccion) => BinaryPrimitives.ReadUInt32BigEndian(direccion.GetAddressBytes());

    /// <summary>La longitud del prefijo de una máscara contigua, o -1 si no es contigua.</summary>
    private static int Prefijo(IPAddress mascara)
    {
        var invertida = ~Numero(mascara);
        return (invertida & (invertida + 1)) == 0 ? 32 - System.Numerics.BitOperations.PopCount(invertida) : -1;
    }
}
