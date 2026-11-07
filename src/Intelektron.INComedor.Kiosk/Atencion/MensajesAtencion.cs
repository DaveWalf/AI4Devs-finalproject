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
    public const string EstiloNoGuardado = "No se pudo guardar el estilo: queda el anterior";
    public const string SinOpciones = "No tenés opciones disponibles ahora";
    public const string SinHabilitacion = "No tenés servicios habilitados. Consultá al responsable del comedor";

    /// <summary>Sin opciones porque alguna quedó fuera de su franja horaria (atencion-puesto de administrar-personas-y-opciones).</summary>
    public const string FueraDeHorario = "No hay opciones disponibles en este horario";

    // Los paneles «Personas habilitadas» y «Opciones del comedor» (administrar-personas-y-opciones).
    public const string PanelInvalido = "Revisá los campos marcados";
    public const string CodigoYaExiste = "Ese código ya existe. Usá otro";
    public const string INWebAdministra = "Estos datos los administra INWeb";
    public const string PanelNoGuardado = "No se pudo guardar: la base no respondió. No cambió nada";

    /// <summary>«Guardar» de Identidad visual deja Configuración abierta con este aviso (D3 de configurar-puesto-por-pestanas).</summary>
    public const string EstiloGuardado = "Se guardó el estilo visual";

    /// <summary>El aviso del diálogo de la clave del día en este corte (D6 de configurar-puesto-por-pestanas).</summary>
    public const string ClaveDeDesarrollo = "Desarrollo: se acepta cualquier número. En la versión operativa la valida la clave del día de INWeb";

    public const string ClaveInvalida = "La clave no es válida";

    /// <summary>El modo no se pudo escribir en la carpeta de datos: sigue el de antes.</summary>
    public const string ModoNoGuardado = "No se pudo guardar el modo: sigue el de antes";

    // La pestaña Impresora (comprobante-local de configurar-puesto-por-pestanas, D13).
    public const string PruebaSalio = "La prueba salió";
    public const string PruebaSinPapel = "La impresora no tiene papel";
    public const string PruebaFallo = "La prueba no salió: revisá la impresora";
    public const string PruebaIncierta = "No se sabe si la prueba salió: revisá la impresora";
    public const string TextosGuardados = "Se guardaron los textos del comprobante";
    public const string TextosNoGuardados = "No se pudieron guardar los textos: siguen los de antes";

    // La pestaña Pantalla (presentacion-puesto de configurar-puesto-por-pestanas, D10).
    public const string MonitorAusente = "El monitor guardado no está conectado: el puesto abrió en el principal";
    public const string MonitorIlegible = "No se pudo leer el monitor guardado: el puesto abrió en el principal";
    public const string MonitorGuardado = "Se guardó el monitor del puesto";
    public const string MonitorNoGuardado = "No se pudo guardar el monitor: sigue el de antes";

    // La pestaña Consumos (consulta-consumos de configurar-puesto-por-pestanas).
    public const string SinConsumos = "No hay consumos con estos filtros";
    public const string RangoInvertido = "El inicio no puede ser posterior al fin";
    public const string RangoDemasiadoLargo = "El rango no puede pasar de 366 días";
    public const string HayMasConsumos = "Se muestran 500 consumos y hay más: acotá los filtros";

    /// <summary>El resumen de Personas u Opciones cuando la base no respondió al abrir Configuración.</summary>
    public const string ConfiguracionNoLeida = "No se pudo leer la configuración de la base";

    public static string VersionGuardada(int version) =>
        $"Se guardó la versión {version.ToString(CultureInfo.InvariantCulture)} de la configuración";

    /// <summary>El mensaje de cada motivo, distinto del de los demás.</summary>
    public static string DeMotivo(string? motivo) => motivo switch
    {
        "LOCAL_LIMIT_REACHED" => "Ya registraste este servicio. Consultá al responsable del comedor",
        "PERSON_INACTIVE" => "Tu habilitación no está activa. Consultá al responsable",
        "CREDENTIAL_REVOKED" => "Esta credencial ya no está habilitada. Solicitá asistencia",
        "CREDENTIAL_EXPIRED" => "Esta credencial está vencida. Solicitá asistencia",
        "RULE_DENIED" => "Este servicio no está permitido por la regla vigente",
        "SERVICE_NOT_ENABLED" => "No hay una habilitación aplicable para este servicio",
        // La causa interna de un SERVICE_NOT_ENABLED por la franja de la opción (D5 de administrar-personas-y-opciones).
        "OUTSIDE_SERVICE_HOURS" => "Esta opción no está disponible en este horario",
        "IDENTITY_NOT_RESOLVED" => "No pudimos identificarte. Revisá el código o solicitá asistencia",
        "PERMISSION_MISSING" => "No tenés permiso de operador",
        "PERMISSION_EXPIRED" => "Tu permiso de operador está vencido",
        "PERMISSION_REVOKED" => "Tu permiso de operador fue revocado",
        "PERMISSION_NOT_YET_VALID" => "Tu permiso de operador todavía no está vigente",
        "SESSION_NOT_USABLE" => "La sesión de asistencia terminó. Identificate de nuevo",
        "PRINT_JOB_NOT_RECOVERABLE" => "Ese comprobante no tiene un trabajo fallido para recuperar",
        _ => "No se pudo completar la operación",
    };

    /// <summary>
    /// El cargo con la cultura es-AR y la escala de la opción: «ARS 1.500,00» (D6). Con importe cero, vacío: la opción
    /// sin cargo no muestra importe (D2 de presentar-atencion-tactil).
    /// </summary>
    public static string Cargo(string moneda, int escala, long importeUnidadesMenores)
    {
        if (importeUnidadesMenores == 0)
        {
            return string.Empty;
        }

        var valor = importeUnidadesMenores / (decimal)Math.Pow(10, escala);
        return moneda + " " + valor.ToString("N" + escala.ToString(CultureInfo.InvariantCulture), Cultura);
    }

    /// <summary>
    /// «Hoy ya usaste: almuerzo 1 de 1 · refrigerio 2 de 2», con el nombre de la categoría en minúsculas; sólo lo agotado,
    /// que es lo que explica una opción ausente (D3 de ofrecer-solo-opciones-disponibles). Null si no hay ninguna.
    /// </summary>
    public static string? UsoDelDia(IReadOnlyList<CategoriaAgotada> agotadas) =>
        agotadas.Count == 0
            ? null
            : "Hoy ya usaste: " + string.Join(" · ", agotadas.Select(a =>
                $"{a.Nombre.ToLower(Cultura)} {a.Usado.ToString(CultureInfo.InvariantCulture)} de {a.Limite.ToString(CultureInfo.InvariantCulture)}"));

    /// <summary>
    /// «Bitácora general: al día», «… : 3 pendientes por escribir» mientras espera la pasada siguiente, o «… : degradada,
    /// 4 pendientes desde las 12:01» con una incidencia abierta (escribir-bitacoras-externas). Nunca datos de personas.
    /// </summary>
    public static string EstadoBitacora(EstadoBitacora estado, TimeZoneInfo zona)
    {
        var nombre = estado.Destino == DestinoBitacora.General ? "Bitácora general" : "Bitácora de interacciones";
        var pendientes = estado.Pendientes == 1 ? "1 pendiente" : $"{estado.Pendientes.ToString(CultureInfo.InvariantCulture)} pendientes";
        return estado.DegradadaDesde is { } desde
            ? $"{nombre}: degradada, {pendientes} desde las {TimeZoneInfo.ConvertTime(desde, zona).ToString("HH:mm", CultureInfo.InvariantCulture)}"
            : estado.AlDia ? $"{nombre}: al día" : $"{nombre}: {pendientes} por escribir";
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
