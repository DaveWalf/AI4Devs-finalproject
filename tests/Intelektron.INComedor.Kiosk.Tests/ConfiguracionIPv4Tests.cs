using Intelektron.INComedor.Kiosk.Red;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La validación de la IPv4 que se tipea en la pestaña Red (conectividad-puesto de configurar-puesto-por-pestanas, D11): sólo
/// IPv4 en notación decimal con puntos y en su forma canónica, una máscara contigua, una puerta dentro de la subred y DNS
/// válidos. Lo que no pasa se señala con su campo y no se arma ningún comando (OWASP: lo que se tipea en el puesto).
/// </summary>
public class ConfiguracionIPv4Tests
{
    public static TheoryData<string, ConfiguracionIPv4> Validas => new()
    {
        { "fija con puerta y dos DNS", new ConfiguracionIPv4(false, "192.168.10.30", "255.255.255.0", "192.168.10.1", "192.168.10.2", "192.168.10.3") },
        { "fija sin puerta", new ConfiguracionIPv4(false, "192.168.10.30", "255.255.255.0", "", "192.168.10.2") },
        { "fija sin DNS", new ConfiguracionIPv4(false, "10.20.0.5", "255.255.0.0", "10.20.0.1") },
        { "DHCP", new ConfiguracionIPv4(true) },
    };

    [Theory(DisplayName = "conectividad-puesto · Una IPv4 válida pasa la validación con sus valores en forma canónica")]
    [MemberData(nameof(Validas))]
    public void Valida(string caso, ConfiguracionIPv4 configuracion)
    {
        var validacion = configuracion.Validar();

        Assert.True(validacion.Invalidos.Count == 0, $"{caso}: {string.Join(", ", validacion.Invalidos)}");
        var valida = Assert.IsType<IPv4Validada>(validacion.Valida);
        Assert.Equal(configuracion.Dhcp, valida.Dhcp);
        if (!configuracion.Dhcp)
        {
            Assert.Equal((configuracion.Direccion, configuracion.Mascara), (valida.Direccion, valida.Mascara));
            Assert.Equal(configuracion.Puerta.Length == 0 ? null : configuracion.Puerta, valida.Puerta);
            Assert.Equal(new[] { configuracion.Dns1, configuracion.Dns2 }.Where(d => d.Length > 0), valida.Dns);
        }
    }

    public static TheoryData<string, ConfiguracionIPv4, CampoIPv4> Rechazadas => new()
    {
        { "un octeto de más de 255", Fija(direccion: "192.168.10.300"), CampoIPv4.Direccion },
        { "un número solo", Fija(direccion: "1"), CampoIPv4.Direccion },
        { "un cero adelante (octal)", Fija(direccion: "010.1.1.1"), CampoIPv4.Direccion },
        { "un comando pegado", Fija(direccion: "192.168.10.30 & del"), CampoIPv4.Direccion },
        { "loopback", Fija(direccion: "127.0.0.1"), CampoIPv4.Direccion },
        { "la difusión de la subred", Fija(direccion: "192.168.10.255"), CampoIPv4.Direccion },
        { "una máscara no contigua", Fija(mascara: "255.0.255.0"), CampoIPv4.Mascara },
        { "una puerta fuera de la subred", Fija(puerta: "192.168.20.1"), CampoIPv4.Puerta },
        { "un DNS inválido", Fija(dns1: "192.168.10"), CampoIPv4.Dns1 },
        { "un segundo DNS inválido", Fija(dns2: "dns.example"), CampoIPv4.Dns2 },
    };

    [Theory(DisplayName = "conectividad-puesto · Un valor que no es una IPv4: se señala su campo y no hay IPv4 validada")]
    [MemberData(nameof(Rechazadas))]
    public void Rechazada(string caso, ConfiguracionIPv4 configuracion, CampoIPv4 campo)
    {
        var validacion = configuracion.Validar();

        Assert.Null(validacion.Valida);
        Assert.True(validacion.Invalidos.SequenceEqual([campo]), $"{caso}: {string.Join(", ", validacion.Invalidos)}");
    }

    private static ConfiguracionIPv4 Fija(string direccion = "192.168.10.30", string mascara = "255.255.255.0", string puerta = "192.168.10.1",
        string dns1 = "192.168.10.2", string dns2 = "") =>
        new(false, direccion, mascara, puerta, dns1, dns2);
}
