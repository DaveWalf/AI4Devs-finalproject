using System.Globalization;
using System.Text.Json;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Lo que muestra el comprobante en pantalla, leído de los mismos datos que la impresora simulada escribe en su archivo
/// (D7 de presentar-atencion-tactil): la leyenda, los primeros ocho caracteres del comprobante, la opción, la fecha de
/// negocio y el cargo. Los datos de impresión no tienen persona ni código.
/// </summary>
public sealed record ComprobanteEnPantalla(string Leyenda, string Numero, string Opcion, string FechaNegocio, string Cargo)
{
    public static ComprobanteEnPantalla Desde(ComprobanteAImprimir comprobante)
    {
        using var datos = JsonDocument.Parse(comprobante.DatosImpresion);
        var raiz = datos.RootElement;
        var fecha = raiz.GetProperty("BusinessDate").GetString() ?? string.Empty;
        var fechaVisible = DateOnly.TryParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia)
            ? dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : fecha;
        var cargo = MensajesAtencion.Cargo(
            raiz.GetProperty("Currency").GetString() ?? string.Empty,
            raiz.GetProperty("Scale").GetInt32(),
            raiz.GetProperty("EmployeeChargeMinor").GetInt64());
        return new ComprobanteEnPantalla(
            comprobante.Leyenda ?? "ORIGINAL",
            comprobante.TicketId.ToString("D")[..8],
            raiz.GetProperty("MenuLabel").GetString() ?? string.Empty,
            fechaVisible,
            cargo);
    }
}
