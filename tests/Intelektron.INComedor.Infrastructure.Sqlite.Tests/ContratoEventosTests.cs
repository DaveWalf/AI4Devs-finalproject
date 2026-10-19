using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>El JSON de cada evento de outbox coincide con las propiedades requeridas del OpenAPI (D9).</summary>
public class ContratoEventosTests : ConfirmacionTestBase
{
    [Fact(DisplayName = "D9 · ConsumptionRecorded: el evento tiene exactamente las propiedades requeridas del OpenAPI y su hash")]
    public void El_evento_autorizado_cumple_ConsumptionRecorded()
    {
        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        using var conexion = Abrir();
        var contenido = Sql.Escalar<string>(conexion, "SELECT comevsContenido FROM tbComedorEventosSalida");
        using var json = JsonDocument.Parse(contenido);
        Assert.Equal(RequeridasEnOpenApi("ConsumptionRecorded"), Propiedades(json));
        Assert.Equal("CONSUMPTION_RECORDED", json.RootElement.GetProperty("EventType").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("PayloadVersion").GetInt32());
        AssertHashDelContenido(conexion, contenido);
    }

    [Fact(DisplayName = "D9 · ConsumptionDenied: el evento tiene exactamente las propiedades requeridas del OpenAPI y su hash")]
    public void El_evento_denegado_cumple_ConsumptionDenied()
    {
        var resultado = Unidad.Confirmar(Solicitud(Fixture.SinDerecho, Fixture.AlmuerzoComun));

        Assert.Equal(TipoResultado.Denegada, resultado.Tipo);
        using var conexion = Abrir();
        var contenido = Sql.Escalar<string>(conexion, "SELECT comevsContenido FROM tbComedorEventosSalida");
        using var json = JsonDocument.Parse(contenido);
        Assert.Equal(RequeridasEnOpenApi("ConsumptionDenied"), Propiedades(json));
        Assert.Equal("CONSUMPTION_DENIED", json.RootElement.GetProperty("EventType").GetString());
        Assert.Equal("SERVICE_NOT_ENABLED", json.RootElement.GetProperty("ReasonCode").GetString());
        AssertHashDelContenido(conexion, contenido);
    }

    private static void AssertHashDelContenido(Microsoft.Data.Sqlite.SqliteConnection conexion, string contenido)
    {
        var guardado = Sql.Escalar<byte[]>(conexion, "SELECT comevsHashContenido FROM tbComedorEventosSalida");
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(contenido)), guardado);
    }

    private static string[] Propiedades(JsonDocument json) =>
        json.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();

    private static string[] RequeridasEnOpenApi(string esquema)
    {
        var texto = File.ReadAllText(RutaOpenApi());
        var inicio = texto.IndexOf($"\n    {esquema}:\n", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"No se encontró el esquema {esquema} en el OpenAPI.");
        var requeridas = Regex.Match(texto[inicio..], @"required: \[(?<campos>[^\]]+)\]");
        return requeridas.Groups["campos"].Value.Split(',').Select(c => c.Trim()).Order(StringComparer.Ordinal).ToArray();
    }

    private static string RutaOpenApi()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "INComedor.sln")))
        {
            carpeta = carpeta.Parent;
        }

        return Path.Combine(carpeta!.FullName, "api", "openapi.yaml");
    }
}
