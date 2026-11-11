using System.Text.Json;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La marca de la copia hecha con la clave del día en el papel (D2 de reimprimir-sin-codigo): la impresora simulada y el
/// comprobante en pantalla escriben «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD» donde iría «Habilitó:», nunca las dos líneas.
/// </summary>
public sealed class MarcaSinValidezTests : IDisposable
{
    private const string Marca = "SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD";

    private readonly string _carpeta = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "incomedor-tests", "marca-sin-validez-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_carpeta, recursive: true);

    [Fact(DisplayName = "comprobante-local · Reimpresión con la clave del día: la impresora simulada escribe la marca SIN VALIDEZ donde iría «Habilitó:», después de la copia, y no escribe «Habilitó:»")]
    public void La_impresora_simulada_escribe_la_marca()
    {
        var ticket = Guid.NewGuid();

        var lineas = Imprimir(new ComprobanteAImprimir(Guid.NewGuid(), ticket, 1, Datos(ticket), "REIMPRESIÓN · copia 1", SinValidez: true));

        Assert.Contains("REIMPRESIÓN · copia 1", lineas);
        var copia = Array.IndexOf(lineas, "Copia: 1");
        Assert.True(copia >= 0);
        Assert.Equal(Marca, lineas[copia + 1]);
        Assert.DoesNotContain(lineas, l => l.StartsWith("Habilitó", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "comprobante-local · Reimpresión con motivo: con un operador, la impresora simulada sigue escribiendo «Habilitó:» y no la marca")]
    public void La_copia_del_operador_sigue_con_habilito()
    {
        var ticket = Guid.NewGuid();

        var lineas = Imprimir(new ComprobanteAImprimir(Guid.NewGuid(), ticket, 1, Datos(ticket), "REIMPRESIÓN · copia 1", "Operador sintético 01"));

        Assert.Contains("Habilitó: Operador sintético 01", lineas);
        Assert.DoesNotContain(lineas, l => l.Contains("SIN VALIDEZ", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "comprobante-local · Reimpresión con la clave del día: el comprobante en pantalla lleva la marca y no «Habilitó:»; el del operador, al revés")]
    public void El_comprobante_en_pantalla_lleva_la_marca()
    {
        var ticket = Guid.NewGuid();

        var conClave = ComprobanteEnPantalla.Desde(new ComprobanteAImprimir(Guid.NewGuid(), ticket, 1, Datos(ticket), "REIMPRESIÓN · copia 1", SinValidez: true));
        var delOperador = ComprobanteEnPantalla.Desde(new ComprobanteAImprimir(Guid.NewGuid(), ticket, 1, Datos(ticket), "REIMPRESIÓN · copia 1", "Operador sintético 01"));

        Assert.True(conClave.SinValidez);
        Assert.Null(conClave.Habilito);
        Assert.False(delOperador.SinValidez);
        Assert.Equal("Operador sintético 01", delOperador.Habilito);
        Assert.Equal(Marca, ComprobanteEnPantalla.MarcaSinValidez);
    }

    private string[] Imprimir(ComprobanteAImprimir comprobante)
    {
        Assert.Equal(RespuestaEnvio.Enviado, new ImpresoraSimulada(_carpeta).Enviar(comprobante).Envio);
        return File.ReadAllLines(Path.Combine(_carpeta, $"{comprobante.TicketId}-{comprobante.NumeroCopia}.txt"));
    }

    private static string Datos(Guid ticket) => JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["TicketId"] = ticket, ["ConsumptionId"] = Guid.NewGuid(), ["MenuItemId"] = Guid.NewGuid(), ["MenuLabel"] = "Almuerzo común",
        ["BusinessDate"] = "2026-11-11", ["Quantity"] = 1, ["Currency"] = "ARS", ["Scale"] = 2, ["EmployeeChargeMinor"] = 150000,
        ["PersonName"] = "Persona sintética 01", ["TicketNumber"] = 1000,
    });
}
