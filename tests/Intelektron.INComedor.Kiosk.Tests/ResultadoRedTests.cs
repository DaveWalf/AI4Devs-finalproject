using System.Text.Json;
using Intelektron.INComedor.Kiosk.Atencion;
using Intelektron.INComedor.Kiosk.Red;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El resultado de una aplicación de red (conectividad-puesto de configurar-puesto-por-pestanas, D11): los pasos corren en
/// orden y se corta en el primero que no termina en 0. Cómo se ejecuta cada paso es un argumento de la función; acá es una
/// lista de salidas dadas, así que ninguna prueba corre <c>netsh</c>.
/// </summary>
public class ResultadoRedTests
{
    private static readonly AdaptadorRed Adaptador = new(12, "Ethernet", "Adaptador sintético", false,
        [new DireccionIPv4("192.168.10.25", "255.255.255.0")], ["192.168.10.1"], ["192.168.10.2"]);

    private static readonly IPv4Validada Pedida =
        Assert.IsType<IPv4Validada>(new ConfiguracionIPv4(false, "192.168.10.30", "255.255.255.0", "192.168.10.1", "192.168.10.2").Validar().Valida);

    [Fact(DisplayName = "conectividad-puesto · Una aplicación completa: los dos pasos terminan en 0 y es aplicada con sus dos pasos")]
    public async Task Todos_los_pasos_en_cero()
    {
        var (resultado, corridos) = await Aplicar(new SalidaNetsh(false, 0), new SalidaNetsh(false, 0));

        Assert.Equal(new ResultadoRed(EstadoRed.Aplicada, 2, 2, null, null), resultado);
        Assert.Equal(2, corridos);
        Assert.Equal(MensajesAtencion.RedAplicada, resultado.Mensaje);
    }

    [Fact(DisplayName = "conectividad-puesto · El permiso de Windows rechazado en el primer paso: cancelada, sin correr el segundo, y la red no cambió")]
    public async Task Permiso_rechazado_en_el_primero()
    {
        var (resultado, corridos) = await Aplicar(new SalidaNetsh(true, 0), new SalidaNetsh(false, 0));

        Assert.Equal(new ResultadoRed(EstadoRed.Cancelada, 2, 0, 1, null), resultado);
        Assert.Equal(1, corridos);
        Assert.Equal(MensajesAtencion.RedSinPermiso, resultado.Mensaje);
    }

    [Fact(DisplayName = "conectividad-puesto · Un comando que falla: el primero termina en 1, no corre el siguiente y la pestaña dice en qué paso falló")]
    public async Task Un_codigo_distinto_de_cero()
    {
        var (resultado, corridos) = await Aplicar(new SalidaNetsh(false, 1), new SalidaNetsh(false, 0));

        Assert.Equal(new ResultadoRed(EstadoRed.Fallida, 2, 0, 1, 1), resultado);
        Assert.Equal(1, corridos);
        Assert.Contains("paso 1 de 2", resultado.Mensaje);
        Assert.Contains("código 1", resultado.Mensaje);
    }

    [Fact(DisplayName = "conectividad-puesto · El registro: CONFIGURATION_CHANGED con NETWORK_IPV4, el adaptador, la IPv4 de antes y la pedida, el resultado, el actor nulo, NO_AUTHENTICATION y Access")]
    public void El_registro()
    {
        var registro = JsonSerializer.SerializeToElement(new ResultadoRed(EstadoRed.Fallida, 2, 1, 2, 5).Registro(Adaptador, Pedida, "MAINTENANCE_MODE"));

        Assert.Equal("NETWORK_IPV4", registro.GetProperty("Setting").GetString());
        Assert.Equal(12, registro.GetProperty("Adapter").GetProperty("Index").GetInt32());
        Assert.Equal("192.168.10.25", registro.GetProperty("Before").GetProperty("Addresses")[0].GetProperty("Address").GetString());
        Assert.False(registro.GetProperty("Before").GetProperty("Dhcp").GetBoolean());
        Assert.Equal("192.168.10.30", registro.GetProperty("After").GetProperty("Address").GetString());
        Assert.Equal("192.168.10.2", registro.GetProperty("After").GetProperty("Dns")[0].GetString());
        Assert.Equal(("FAILED", 2, 1, 2, 5), (registro.GetProperty("Result").GetString(), registro.GetProperty("Steps").GetInt32(),
            registro.GetProperty("AppliedSteps").GetInt32(), registro.GetProperty("FailedStep").GetInt32(), registro.GetProperty("ExitCode").GetInt32()));
        Assert.Equal(JsonValueKind.Null, registro.GetProperty("ActorPersonId").ValueKind);
        Assert.Equal("NO_AUTHENTICATION", registro.GetProperty("ActorReason").GetString());
        Assert.Equal("MAINTENANCE_MODE", registro.GetProperty("Access").GetString());
    }

    private static async Task<(ResultadoRed Resultado, int Corridos)> Aplicar(params SalidaNetsh[] salidas)
    {
        var comandos = ArgumentosNetsh.Armar(12, Pedida);
        Assert.Equal(salidas.Length, comandos.Count);
        var corridos = 0;
        var resultado = await ResultadoRed.AplicarAsync(comandos, comando =>
        {
            Assert.Same(comandos[corridos], comando);
            return Task.FromResult(salidas[corridos++]);
        });
        return (resultado, corridos);
    }
}
