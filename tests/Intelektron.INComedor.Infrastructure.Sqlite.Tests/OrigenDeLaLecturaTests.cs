using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// El origen de una lectura en el registro de la identificación (lector-identificacion de elegir-medio-de-identificacion,
/// D7): con el lector, SESSION_OPENED e IDENTIFICATION_REJECTED llevan la clave Origin, la misma de la simulación por
/// lista; el código tipeado sigue sin ella, y ninguno lleva el código.
/// </summary>
public class OrigenDeLaLecturaTests : ConfirmacionTestBase
{
    [Fact(DisplayName = "lector-identificacion · Una lectura identifica como el código tipeado: la sesión y el rechazo por el lector llevan su origen, sin el código")]
    public void El_origen_de_la_lectura()
    {
        var identificacion = new IdentificacionPorCodigo(Base, Reloj);

        var abierta = identificacion.Identificar(FixtureSintetico.CodigoSintetico, PropositoSesion.Consumption, "SERIAL_PORT");
        var rechazada = identificacion.Identificar("SINTETICO-NINGUNO-99", PropositoSesion.Consumption, "SIMULATED_READER");
        identificacion.Identificar(FixtureSintetico.CodigoSintetico, PropositoSesion.Consumption);

        Assert.True(abierta.Abierta);
        Assert.False(rechazada.Abierta);
        var registros = Contextos();
        Assert.Equal(["SERIAL_PORT", "SIMULATED_READER", null], registros.Select(r => r.TryGetProperty("Origin", out var o) ? o.GetString() : null));
        Assert.All(registros, r => Assert.DoesNotContain(FixtureSintetico.CodigoSintetico, r.GetRawText(), StringComparison.Ordinal));
    }

    private List<JsonElement> Contextos()
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento IN ('SESSION_OPENED', 'IDENTIFICATION_REJECTED') ORDER BY combitSecuencia";
        using var lector = comando.ExecuteReader();
        var contextos = new List<JsonElement>();
        while (lector.Read())
        {
            contextos.Add(JsonDocument.Parse(lector.GetString(0)).RootElement.Clone());
        }

        return contextos;
    }
}
