using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Intelektron.INComedor.Kiosk.Red;

/// <summary>Una IPv4 de un adaptador con su máscara, en notación decimal con puntos.</summary>
public sealed record DireccionIPv4(string Direccion, string Mascara);

/// <summary>
/// Un adaptador de red activo (D11 de configurar-puesto-por-pestanas): su índice IPv4, que es lo que se le pasa a
/// <c>netsh</c>; su nombre y su descripción, sólo para mostrarlos; si toma la IPv4 por DHCP, sus IPv4, sus puertas y sus DNS.
/// </summary>
public sealed record AdaptadorRed(int Indice, string Nombre, string Descripcion, bool Dhcp,
    IReadOnlyList<DireccionIPv4> Direcciones, IReadOnlyList<string> Puertas, IReadOnlyList<string> Dns);

/// <summary>El nombre del equipo y sus adaptadores activos.</summary>
public sealed record DatosRed(string NombreEquipo, IReadOnlyList<AdaptadorRed> Adaptadores);

/// <summary>Los datos de red del equipo con <c>System.Net.NetworkInformation</c>, sin el adaptador de loopback (D11).</summary>
public static class DatosDeRed
{
    /// <summary>Los adaptadores con <c>OperationalStatus.Up</c> e IPv4, ordenados por su índice.</summary>
    public static DatosRed Leer()
    {
        var adaptadores = new List<AdaptadorRed>();
        foreach (var interfaz in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (interfaz.OperationalStatus != OperationalStatus.Up || interfaz.NetworkInterfaceType == NetworkInterfaceType.Loopback
                || !interfaz.Supports(NetworkInterfaceComponent.IPv4))
            {
                continue;
            }

            var propiedades = interfaz.GetIPProperties();
            IPv4InterfaceProperties ipv4;
            try
            {
                ipv4 = propiedades.GetIPv4Properties();
            }
            catch (NetworkInformationException)
            {
                continue;
            }

            adaptadores.Add(new AdaptadorRed(ipv4.Index, interfaz.Name, interfaz.Description, ipv4.IsDhcpEnabled,
                propiedades.UnicastAddresses.Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(u => new DireccionIPv4(u.Address.ToString(), u.IPv4Mask.ToString())).ToList(),
                propiedades.GatewayAddresses.Where(p => p.Address.AddressFamily == AddressFamily.InterNetwork).Select(p => p.Address.ToString()).ToList(),
                propiedades.DnsAddresses.Where(s => s.AddressFamily == AddressFamily.InterNetwork).Select(s => s.ToString()).ToList()));
        }

        return new DatosRed(Environment.MachineName, adaptadores.OrderBy(a => a.Indice).ToList());
    }
}
