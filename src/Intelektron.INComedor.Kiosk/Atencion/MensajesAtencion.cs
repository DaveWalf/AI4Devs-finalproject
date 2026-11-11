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

    /// <summary>El título de reposo, que saluda e invita a disfrutar la comida (D13 de simular-identificacion-por-lista).</summary>
    public const string TituloReposo = "¡Hola! Identificate y disfrutá tu comida";

    public const string Exito = "Consumo registrado. Retirá tu comprobante.";
    public const string ComprobantePendiente = "Tu consumo está registrado. El comprobante queda pendiente.";
    public const string Ocupado = "Puesto ocupado: hay una confirmación en curso.";
    public const string SesionVencida = "Tu sesión venció. Identificate de nuevo.";
    public const string OpcionRetirada = "La opción elegida ya no está disponible. Elegí otra.";
    public const string CatalogoCambiado = "La opción cambió. Revisá el valor vigente y confirmá de nuevo.";
    public const string Incertidumbre = "El papel pudo haber salido. La copia nueva va a decir DUPLICADO.";
    public const string ErrorAlmacenamiento = "Error de almacenamiento: la base no es de una versión conocida.";
    public const string NoConfirmado = "No se pudo confirmar. Identificate de nuevo.";
    public const string EstiloNoGuardado = "No se pudo guardar el estilo: queda el anterior.";
    public const string SinOpciones = "No tenés opciones disponibles ahora.";
    public const string SinHabilitacion = "No tenés servicios habilitados. Consultá al responsable del comedor.";

    /// <summary>Sin opciones porque alguna quedó fuera de su franja horaria (atencion-puesto de administrar-personas-y-opciones).</summary>
    public const string FueraDeHorario = "No hay opciones disponibles en este horario.";

    // Los paneles «Personas habilitadas» y «Opciones del comedor» (administrar-personas-y-opciones).
    public const string PanelInvalido = "Revisá los campos marcados.";
    public const string CodigoYaExiste = "Ese código ya existe. Usá otro.";
    public const string INWebAdministra = "Estos datos los administra INWeb.";
    public const string PanelNoGuardado = "No se pudo guardar: la base no respondió. No cambió nada.";

    /// <summary>«Guardar» de Identidad visual deja Configuración abierta con este aviso (D3 de configurar-puesto-por-pestanas).</summary>
    public const string EstiloGuardado = "Se guardó el estilo visual.";

    /// <summary>El aviso del diálogo de la clave del día en este corte (D6 de configurar-puesto-por-pestanas).</summary>
    public const string ClaveDeDesarrollo = "Desarrollo: se acepta cualquier número. En la versión operativa la valida la clave del día de INWeb.";

    public const string ClaveInvalida = "La clave no es válida.";

    /// <summary>Arriba de Configuración, mientras se entró con la clave del día sin validar (D4 de entrar-a-configuracion-con-teclado).</summary>
    public const string ClaveSinValidar = "Entraste con una clave del día sin validar: esta versión acepta cualquier número.";

    /// <summary>El código de una persona tipeado para cerrar el puesto o pasar a operativo (D5 de entrar-a-configuracion-con-teclado).</summary>
    public const string CodigoNoEsClave = "El código de un operador no cierra el puesto.";

    /// <summary>El modo no se pudo escribir en la carpeta de datos: sigue el de antes.</summary>
    public const string ModoNoGuardado = "No se pudo guardar el modo: sigue el de antes.";

    /// <summary>El modo simulación no se pudo escribir en la carpeta de datos: sigue como estaba (D6 de simular-identificacion-por-lista).</summary>
    public const string SimulacionNoGuardada = "No se pudo guardar la simulación: sigue como estaba.";

    // La pestaña Impresora (comprobante-local de configurar-puesto-por-pestanas, D13).
    public const string PruebaSalio = "La prueba salió.";
    public const string PruebaSinPapel = "La impresora no tiene papel.";
    public const string PruebaFallo = "La prueba no salió: revisá la impresora.";
    public const string PruebaIncierta = "No se sabe si la prueba salió: revisá la impresora.";
    public const string TextosGuardados = "Se guardaron los textos del comprobante.";
    public const string TextosNoGuardados = "No se pudieron guardar los textos: siguen los de antes.";

    // La pestaña Pantalla (presentacion-puesto de configurar-puesto-por-pestanas, D10).
    public const string MonitorAusente = "El monitor guardado no está conectado: el puesto abrió en el principal.";
    public const string MonitorIlegible = "No se pudo leer el monitor guardado: el puesto abrió en el principal.";
    public const string MonitorGuardado = "Se guardó el monitor del puesto.";
    public const string MonitorNoGuardado = "No se pudo guardar el monitor: sigue el de antes.";
    public const string LogoPasaDe2MB = "El archivo pasa de 2 MB: el logo vigente no cambió.";
    public const string LogoFormato = "El logo tiene que ser un archivo PNG, WebP o SVG: el logo vigente no cambió.";
    public const string LogoInvalido = "El archivo no es una imagen válida: el logo vigente no cambió.";
    public const string LogosGuardados = "Se guardaron los logos.";
    public const string LogosNoGuardados = "No se pudieron guardar los logos: siguen los de antes.";
    public const string SinRed = "Sin conexión de red";
    public const string RedRevisar = "Revisá los campos marcados: cada valor tiene que ser una IPv4 válida.";
    public const string RedAviso = "El cambio puede cortar la conexión del puesto, y Windows pide permiso de administrador en cada paso.";
    public const string RedNoDisponible = "La edición de red no está disponible en este puesto: la red no cambió.";
    public const string RedAplicada = "Se aplicó la IPv4 del adaptador.";
    public const string RedSinPermiso = "No se dio el permiso de administrador: la red no cambió.";
    public const string SinCertificados = "El puesto no tiene certificados: atiende sin receptor y la conexión no se puede probar.";

    // La pestaña Consumos (consulta-consumos de configurar-puesto-por-pestanas).
    public const string SinConsumos = "No hay consumos con estos filtros.";
    public const string RangoInvertido = "El inicio no puede ser posterior al fin.";
    public const string CodigoDeNadie = "Ese código no es de ninguna persona.";
    public const string RangoDemasiadoLargo = "El rango no puede pasar de 366 días.";
    public const string HayMasConsumos = "Se muestran 500 consumos y hay más: acotá los filtros.";

    // La exportación de Consumos (D6 de buscar-y-exportar-consumos). Cada oración termina en punto (pedido de David del
    // 10/11/2026); el resto de los mensajes lo sigue el change puntuar-la-interfaz.
    public const string ExportacionFallida = "No se pudo exportar: la carpeta no se puede escribir. Elegí otra con «Cambiar carpeta».";
    public const string ExportacionTopada = "Se exportaron los primeros 10 000 consumos y hay más: acotá los filtros para el resto.";
    public const string CarpetaNoGuardada = "No se pudo guardar la carpeta elegida: se usa hasta cerrar Configuración.";
    public const string SinCarpeta = "No se eligió una carpeta: no se exportó nada.";
    public const string ExportacionPersonasFallida = "No se pudo exportar: la carpeta no se puede escribir. Cambiala con «Cambiar carpeta» en Consumos.";
    public const string FiltradoPorCodigo = "La tabla está filtrada por el código de una persona; «Consultar» sin código lo quita.";
    public const string PedidoOperador = "Para reimprimir o recuperar un comprobante, ingresá el código de un operador.";

    public static string PedidoOperadorPara(bool recuperar, string? numero) =>
        $"Para {(recuperar ? "recuperar" : "reimprimir")} el comprobante N.º {numero}, ingresá el código de un operador.";

    public static string CarpetaElegida(string carpeta) => $"Las exportaciones van a {carpeta}.";

    /// <summary>La exportación de Personas hecha (D5 de editar-raciones-en-personas).</summary>
    public static string ExportacionPersonasHecha(string archivo, int filas, string carpeta) =>
        $"Exportado {archivo} con {filas} {(filas == 1 ? "persona" : "personas")} en {carpeta}.";

    public static string ExportacionHecha(string archivo, int filas, string carpeta) =>
        $"Exportado {archivo} con {filas} {(filas == 1 ? "consumo" : "consumos")} en {carpeta}.";

    // Las oraciones que vivían en los componentes y en otras clases, para que una sola prueba las vea (D1 de
    // puntuar-la-interfaz).
    public const string Confirmando = "Confirmando: el puesto está ocupado.";
    public const string TocaParaRetirar = "Tocá el comprobante para retirarlo.";
    public const string ClaveParaAbrir = "Ingresá la clave del día para abrir Configuración.";
    public const string ClaveParaCerrar = "Ingresá la clave del día para cerrar el puesto.";
    public const string ClaveParaOperativo = "Ingresá la clave del día para pasar a operativo.";
    public const string EdicionDePersonasAbierta = "Terminá o cancelá la edición de Personas para editar Opciones.";
    public const string EdicionDeOpcionesAbierta = "Terminá o cancelá la edición de Opciones.";
    public const string OriginalEnviado = "Comprobante original enviado otra vez.";
    public const string ComprobanteQuedoPendiente = "El comprobante quedó pendiente: revisá la impresora.";
    public const string ImpresoraDemostracion = "Demostración: elegí cómo responde al próximo comprobante.";
    public const string PruebaImpresionExplicacion = "Envía un comprobante con la leyenda PRUEBA, sin persona ni importe; no registra ningún consumo.";
    public const string MonitorExplicacion = "Elegí el monitor del puesto: la ventana pasa a pantalla completa en él y vuelve a él cada vez que el puesto abre.";
    public const string DemostracionExplicacion = "Datos sintéticos: no es la atención del comedor. Cada toque confirma una intención nueva sobre la base local.";

    /// <summary>El pedido del motivo de una reimpresión: termina en dos puntos porque presenta los botones de los motivos (D2).</summary>
    public static string PedidoMotivo(string numero, string opcion) => $"Reimprimir el comprobante N.º {numero} · {opcion}. Elegí el motivo:";

    /// <summary>El resumen de la pestaña Opciones: un estado, sin punto (D2).</summary>
    public static string ResumenOpciones(int opciones, int version) =>
        $"{opciones.ToString(CultureInfo.InvariantCulture)} opciones en la versión {version.ToString(CultureInfo.InvariantCulture)} de la configuración";

    public static string CopiaEnviada(int numeroCopia) =>
        $"Copia {numeroCopia.ToString(CultureInfo.InvariantCulture)} enviada con la leyenda DUPLICADO.";

    public static string RedCanceladaEnPaso(int? paso, int pasos) =>
        $"No se dio el permiso de administrador del paso {paso?.ToString(CultureInfo.InvariantCulture)} de {pasos.ToString(CultureInfo.InvariantCulture)}: quedaron aplicados los anteriores.";

    public static string RedFallo(int? paso, int pasos, int? codigo, bool conAplicados) =>
        $"Falló el paso {paso?.ToString(CultureInfo.InvariantCulture)} de {pasos.ToString(CultureInfo.InvariantCulture)} con el código {codigo?.ToString(CultureInfo.InvariantCulture)}: "
        + (conAplicados ? "quedaron aplicados los anteriores." : "la red no cambió.");

    /// <summary>El resumen de Personas u Opciones cuando la base no respondió al abrir Configuración.</summary>
    public const string ConfiguracionNoLeida = "No se pudo leer la configuración de la base.";

    public static string VersionGuardada(int version) =>
        $"Se guardó la versión {version.ToString(CultureInfo.InvariantCulture)} de la configuración.";

    /// <summary>El mensaje de cada motivo, distinto del de los demás.</summary>
    public static string DeMotivo(string? motivo) => motivo switch
    {
        "LOCAL_LIMIT_REACHED" => "Ya registraste este servicio. Consultá al responsable del comedor.",
        "PERSON_INACTIVE" => "Tu habilitación no está activa. Consultá al responsable.",
        "CREDENTIAL_REVOKED" => "Esta credencial ya no está habilitada. Solicitá asistencia.",
        "CREDENTIAL_EXPIRED" => "Esta credencial está vencida. Solicitá asistencia.",
        "RULE_DENIED" => "Este servicio no está permitido por la regla vigente.",
        "SERVICE_NOT_ENABLED" => "No hay una habilitación aplicable para este servicio.",
        // La causa interna de un SERVICE_NOT_ENABLED por la franja de la opción (D5 de administrar-personas-y-opciones).
        "OUTSIDE_SERVICE_HOURS" => "Esta opción no está disponible en este horario.",
        "IDENTITY_NOT_RESOLVED" => "No pudimos identificarte. Revisá el código o solicitá asistencia.",
        "PERMISSION_MISSING" => "No tenés permiso de operador.",
        "PERMISSION_EXPIRED" => "Tu permiso de operador está vencido.",
        "PERMISSION_REVOKED" => "Tu permiso de operador fue revocado.",
        "PERMISSION_NOT_YET_VALID" => "Tu permiso de operador todavía no está vigente.",
        "SESSION_NOT_USABLE" => "La sesión de asistencia terminó. Identificate de nuevo.",
        "PRINT_JOB_NOT_RECOVERABLE" => "Ese comprobante no tiene un trabajo fallido para recuperar.",
        _ => "No se pudo completar la operación.",
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
