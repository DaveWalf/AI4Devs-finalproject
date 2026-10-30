using System.Globalization;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>Una opción tal como la muestra la atención: el cargo ya formateado.</summary>
public sealed record OpcionCatalogo(Guid Id, int Version, string Nombre, string Cargo);

/// <summary>
/// Los textos de la atención, del catálogo de estados de docs/experiencia.md; los que el boceto no tiene se
/// escriben en el mismo tono. Ninguno muestra el código tipeado.
/// </summary>
public static class MensajesAtencion
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    public const string Exito = "Consumo registrado. Retirá tu comprobante";
    public const string ComprobantePendiente = "Tu consumo está registrado. El comprobante queda pendiente";
    public const string Ocupado = "Puesto ocupado: hay una confirmación en curso";
    public const string SesionVencida = "Tu sesión venció. Identificate de nuevo";
    public const string OpcionRetirada = "La opción elegida ya no está disponible. Elegí otra";
    public const string CatalogoCambiado = "La opción cambió. Revisá el valor vigente y confirmá de nuevo";
    public const string Incertidumbre = "El papel pudo haber salido. La copia nueva va a decir DUPLICADO";
    public const string ErrorAlmacenamiento = "Error de almacenamiento: la base no es de una versión conocida";
    public const string NoConfirmado = "No se pudo confirmar. Identificate de nuevo";

    /// <summary>El mensaje de cada motivo, distinto del de los demás.</summary>
    public static string DeMotivo(string? motivo) => motivo switch
    {
        "LOCAL_LIMIT_REACHED" => "Ya registraste este servicio. Consultá al responsable del comedor",
        "PERSON_INACTIVE" => "Tu habilitación no está activa. Consultá al responsable",
        "CREDENTIAL_REVOKED" => "Esta credencial ya no está habilitada. Solicitá asistencia",
        "CREDENTIAL_EXPIRED" => "Esta credencial está vencida. Solicitá asistencia",
        "RULE_DENIED" => "Este servicio no está permitido por la regla vigente",
        "SERVICE_NOT_ENABLED" => "No hay una habilitación aplicable para este servicio",
        "IDENTITY_NOT_RESOLVED" => "No pudimos identificarte. Revisá el código o solicitá asistencia",
        "PERMISSION_MISSING" => "No tenés permiso de operador",
        "PERMISSION_EXPIRED" => "Tu permiso de operador está vencido",
        "PERMISSION_REVOKED" => "Tu permiso de operador fue revocado",
        "PERMISSION_NOT_YET_VALID" => "Tu permiso de operador todavía no está vigente",
        "SESSION_NOT_USABLE" => "La sesión de asistencia terminó. Identificate de nuevo",
        "PRINT_JOB_NOT_RECOVERABLE" => "Ese comprobante no tiene un trabajo fallido para recuperar",
        _ => "No se pudo completar la operación",
    };

    /// <summary>El cargo con la cultura es-AR y la escala de la opción: «ARS 1.500,00» (D6).</summary>
    public static string Cargo(string moneda, int escala, long importeUnidadesMenores)
    {
        var valor = importeUnidadesMenores / (decimal)Math.Pow(10, escala);
        return moneda + " " + valor.ToString("N" + escala.ToString(CultureInfo.InvariantCulture), Cultura);
    }

    public static OpcionCatalogo Opcion(OpcionDeCatalogo opcion) =>
        new(opcion.Id, opcion.Version, opcion.Nombre, Cargo(opcion.Moneda, opcion.Escala, opcion.ImporteUnidadesMenores));

    public static string Motivo(MotivoReimpresion motivo) => motivo switch
    {
        MotivoReimpresion.PapelAtascado => "Papel atascado",
        MotivoReimpresion.PapelIlegible => "Papel ilegible",
        MotivoReimpresion.ComprobanteNoSalio => "El comprobante no salió",
        MotivoReimpresion.Otro => "Otro",
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, null),
    };
}
