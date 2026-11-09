namespace Intelektron.INComedor.Kiosk.Red;

/// <summary>Un comando de <c>netsh</c> como lista de argumentos, ninguno con espacios ni comillas.</summary>
public sealed record ComandoNetsh(IReadOnlyList<string> Argumentos)
{
    public string Texto => string.Join(' ', Argumentos);
}

/// <summary>
/// Los comandos de <c>netsh</c> de una IPv4 validada, con el adaptador por su índice (D11): una función pura, sin
/// ejecutar nada.
/// </summary>
public static class ArgumentosNetsh
{
    /// <summary>
    /// Un comando por paso: la dirección y los DNS, más el segundo DNS con su índice. Cada valor se vuelve a comprobar, así
    /// que ni una <see cref="IPv4Validada"/> armada de otra forma lleva otra cosa que una IPv4 a un argumento.
    /// </summary>
    public static IReadOnlyList<ComandoNetsh> Armar(int indice, IPv4Validada configuracion)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(indice);
        var nombre = $"name={indice.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (configuracion.Dhcp)
        {
            return
            [
                Comando("interface", "ipv4", "set", "address", nombre, "source=dhcp"),
                Comando("interface", "ipv4", "set", "dnsservers", nombre, "source=dhcp"),
            ];
        }

        var comandos = new List<ComandoNetsh>
        {
            Comando("interface", "ipv4", "set", "address", nombre, "source=static", $"address={IPv4(configuracion.Direccion)}",
                $"mask={IPv4(configuracion.Mascara)}", configuracion.Puerta is null ? "gateway=none" : $"gateway={IPv4(configuracion.Puerta)}"),
        };
        if (configuracion.Dns.Count == 0)
        {
            comandos.Add(Comando("interface", "ipv4", "set", "dnsservers", nombre, "source=static", "address=none"));
            return comandos;
        }

        comandos.Add(Comando("interface", "ipv4", "set", "dnsservers", nombre, "source=static", $"address={IPv4(configuracion.Dns[0])}",
            "register=primary", "validate=no"));
        if (configuracion.Dns.Count > 1)
        {
            comandos.Add(Comando("interface", "ipv4", "add", "dnsservers", nombre, $"address={IPv4(configuracion.Dns[1])}", "index=2", "validate=no"));
        }

        return comandos;
    }

    private static ComandoNetsh Comando(params string[] argumentos) => new(argumentos);

    private static string IPv4(string? valor) =>
        ConfiguracionIPv4.EsIPv4(valor, out var direccion) && direccion.ToString() == valor
            ? valor
            : throw new ArgumentException("Un valor de la IPv4 no está validado.", nameof(valor));
}
