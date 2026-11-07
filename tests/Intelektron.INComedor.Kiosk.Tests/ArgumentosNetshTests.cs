using System.Text.RegularExpressions;
using Intelektron.INComedor.Kiosk.Red;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Los comandos de <c>netsh</c> (conectividad-puesto de configurar-puesto-por-pestanas, D11): una función pura que sólo
/// arma, con el adaptador por su índice y sólo con valores validados. Ninguna prueba corre <c>netsh</c>.
/// </summary>
public class ArgumentosNetshTests
{
    [Fact(DisplayName = "conectividad-puesto · Los comandos de una IPv4 fija: con el índice 12, la dirección, la máscara y la puerta, y el DNS primario sin validar")]
    public void Ipv4_fija()
    {
        var comandos = ArgumentosNetsh.Armar(12, Validar(new ConfiguracionIPv4(false, "192.168.10.30", "255.255.255.0", "192.168.10.1", "192.168.10.2")));

        Assert.Equal(
            [
                "interface ipv4 set address name=12 source=static address=192.168.10.30 mask=255.255.255.0 gateway=192.168.10.1",
                "interface ipv4 set dnsservers name=12 source=static address=192.168.10.2 register=primary validate=no",
            ],
            comandos.Select(c => c.Texto));
    }

    [Fact(DisplayName = "conectividad-puesto · Sin puerta, gateway=none")]
    public void Sin_puerta()
    {
        var comandos = ArgumentosNetsh.Armar(12, Validar(new ConfiguracionIPv4(false, "192.168.10.30", "255.255.255.0", "", "192.168.10.2")));

        Assert.Equal("interface ipv4 set address name=12 source=static address=192.168.10.30 mask=255.255.255.0 gateway=none", comandos[0].Texto);
    }

    [Fact(DisplayName = "conectividad-puesto · Sin DNS, address=none")]
    public void Sin_dns()
    {
        var comandos = ArgumentosNetsh.Armar(12, Validar(new ConfiguracionIPv4(false, "192.168.10.30", "255.255.255.0", "192.168.10.1")));

        Assert.Equal(2, comandos.Count);
        Assert.Equal("interface ipv4 set dnsservers name=12 source=static address=none", comandos[1].Texto);
    }

    [Fact(DisplayName = "conectividad-puesto · El segundo DNS va con add dnsservers e index=2")]
    public void Segundo_dns()
    {
        var comandos = ArgumentosNetsh.Armar(12, Validar(new ConfiguracionIPv4(false, "192.168.10.30", "255.255.255.0", "192.168.10.1", "192.168.10.2", "192.168.10.3")));

        Assert.Equal(3, comandos.Count);
        Assert.Equal("interface ipv4 add dnsservers name=12 address=192.168.10.3 index=2 validate=no", comandos[2].Texto);
    }

    [Fact(DisplayName = "conectividad-puesto · Volver a DHCP: la dirección y los DNS con source=dhcp")]
    public void Dhcp()
    {
        var comandos = ArgumentosNetsh.Armar(12, Validar(new ConfiguracionIPv4(true)));

        Assert.Equal(["interface ipv4 set address name=12 source=dhcp", "interface ipv4 set dnsservers name=12 source=dhcp"], comandos.Select(c => c.Texto));
    }

    [Fact(DisplayName = "conectividad-puesto · Ningún argumento lleva espacios ni comillas, y un índice que no es positivo no arma nada")]
    public void Argumentos_sin_espacios_ni_comillas()
    {
        var casos = new[]
        {
            new ConfiguracionIPv4(false, "192.168.10.30", "255.255.255.0", "192.168.10.1", "192.168.10.2", "192.168.10.3"),
            new ConfiguracionIPv4(false, "10.0.0.7", "255.0.0.0"),
            new ConfiguracionIPv4(true),
        };

        var argumentos = casos.SelectMany(c => ArgumentosNetsh.Armar(7, Validar(c))).SelectMany(c => c.Argumentos).ToList();

        Assert.NotEmpty(argumentos);
        Assert.All(argumentos, a => Assert.Matches(new Regex("^[a-z0-9=.]+$"), a));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgumentosNetsh.Armar(0, Validar(casos[0])));
    }

    private static IPv4Validada Validar(ConfiguracionIPv4 configuracion) => Assert.IsType<IPv4Validada>(configuracion.Validar().Valida);
}
