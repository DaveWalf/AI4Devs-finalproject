using System.Globalization;
using System.Text.Json;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Lo que muestra el comprobante en pantalla, leído de los mismos datos que la impresora simulada escribe en su archivo
/// (D7 de presentar-atencion-tactil): la leyenda, el número (el correlativo o los primeros ocho caracteres del comprobante),
/// la opción, la fecha de negocio, el cargo, el comensal si sus datos lo tienen, la frase del comprobante y, en una copia,
/// quién la habilitó (D6 de enriquecer-comprobante) o, con la clave del día, la marca SIN VALIDEZ en su lugar (D2 de
/// reimprimir-sin-codigo). Nunca el código de identificación.
/// </summary>
public sealed record ComprobanteEnPantalla(
    string Leyenda, string Numero, string Opcion, string FechaNegocio, string Cargo, string? Comensal = null, string Frase = "", string? Habilito = null,
    bool SinValidez = false)
{
    /// <summary>
    /// La línea de una copia hecha con la clave del día, en el lugar de «Habilitó:» (D2 de reimprimir-sin-codigo). No es una
    /// oración sino una marca del papel, por eso no termina en punto.
    /// </summary>
    public const string MarcaSinValidez = "SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD";

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
        var (numero, comensal) = LeerDatos(comprobante) ?? (comprobante.TicketId.ToString("D")[..8], null);
        return new ComprobanteEnPantalla(
            comprobante.Leyenda ?? "ORIGINAL",
            numero,
            raiz.GetProperty("MenuLabel").GetString() ?? string.Empty,
            fechaVisible,
            cargo,
            comensal,
            FrasesComprobante.Para(comprobante.TicketId),
            comprobante.SinValidez ? null : comprobante.Habilito,
            comprobante.SinValidez);
    }

    /// <summary>
    /// El número y el comensal de los datos de un comprobante de consumo, o null si sus datos no son los de un consumo, como
    /// los de la prueba de impresión.
    /// </summary>
    public static (string Numero, string? Comensal)? LeerDatos(ComprobanteAImprimir comprobante)
    {
        try
        {
            using var datos = JsonDocument.Parse(comprobante.DatosImpresion);
            var raiz = datos.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("MenuLabel", out _))
            {
                return null;
            }

            var numero = raiz.TryGetProperty("TicketNumber", out var correlativo) && correlativo.ValueKind == JsonValueKind.Number
                && correlativo.TryGetInt64(out var valor)
                ? valor.ToString(CultureInfo.InvariantCulture)
                : comprobante.TicketId.ToString("D")[..8];
            var comensal = raiz.TryGetProperty("PersonName", out var nombre) && nombre.ValueKind == JsonValueKind.String ? nombre.GetString() : null;
            return (numero, comensal);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
