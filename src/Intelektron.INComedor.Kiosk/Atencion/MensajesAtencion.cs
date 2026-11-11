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
    public const string Incertidumbre = "El papel pudo haber salido. La copia nueva va a decir REIMPRESIÓN.";
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
    public const string DatosPuestoGuardados = "Se guardaron los datos del comprobante: valen desde el próximo comprobante.";
    public const string DatosPuestoNoGuardados = "No se pudieron guardar los datos del comprobante: siguen los de antes.";

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
    /// <summary>El aviso antes de elegir el motivo con la clave del día (D7 de reimprimir-sin-codigo).</summary>
    public const string CopiaSinValidez = "La copia va a salir marcada SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD porque se entró con la clave del día.";

    public static string CarpetaElegida(string carpeta) => $"Las exportaciones van a {carpeta}.";

    /// <summary>La exportación de Personas hecha (D5 de editar-raciones-en-personas).</summary>
    public static string ExportacionPersonasHecha(string archivo, int filas, string carpeta) =>
        $"Exportado {archivo} con {filas} {(filas == 1 ? "persona" : "personas")} en {carpeta}.";

    public static string ExportacionHecha(string archivo, int filas, string carpeta) =>
        $"Exportado {archivo} con {filas} {(filas == 1 ? "consumo" : "consumos")} en {carpeta}.";

    // La planilla de ejemplo y la importación de Personas (D4, D5, D6 y D8 de importar-personas-desde-excel).
    public const string ConsumosDeHoyAlImportar = "Lo consumido hoy sigue contando contra los límites nuevos.";
    public const string ArchivoNoLeido = "No se pudo leer el archivo elegido: si está abierto en otro programa, cerralo y volvé a importarlo.";
    public const string PlanillaFallida = "No se pudo guardar la planilla: la carpeta no se puede escribir. Cambiala con «Cambiar carpeta» en Consumos.";
    public const string ImportacionRechazada = "No se aplicó la importación: la configuración cambió después del resumen. Volvé a importar la planilla.";

    public static string PlanillaDeEjemploHecha(string archivo, string carpeta) => $"Planilla de ejemplo guardada como {archivo} en {carpeta}.";

    public static string ResumenImportacion(string archivo, int agregadas, int modificadas, int sinCambios) =>
        $"{archivo}: {Cantidad(agregadas, "persona nueva", "personas nuevas")}, {Cantidad(modificadas, "modificada", "modificadas")} y {Cantidad(sinCambios, "sin cambios", "sin cambios")}.";

    public static string ImportacionReemplazar(int deBaja) => deBaja == 0
        ? "«Reemplazar» no da de baja a nadie: todas las personas activas están en la planilla, son operadores o son el administrador."
        : $"«Reemplazar» además da de baja a {Cantidad(deBaja, "persona", "personas")} que no {(deBaja == 1 ? "está" : "están")} en la planilla y revoca sus códigos, sin vuelta atrás; los operadores y el administrador siguen activos.";

    public static string ImportacionConFilasDeEjemplo(int filas) =>
        $"La planilla todavía tiene {Cantidad(filas, "fila de ejemplo", "filas de ejemplo")}: si no se {(filas == 1 ? "borra, se agrega" : "borran, se agregan")} como {(filas == 1 ? "una persona" : "personas")}.";

    public static string ImportacionCodigosComoNumero(int codigos) =>
        $"{Cantidad(codigos, "código estaba guardado", "códigos estaban guardados")} como número en Excel: si {(codigos == 1 ? "tenía" : "tenían")} ceros a la izquierda, Excel se los quitó y {(codigos == 1 ? "cuenta" : "cuentan")} como {(codigos == 1 ? "otro código" : "otros códigos")}.";

    public static string ImportacionConErrores(int errores) =>
        $"La planilla tiene {Cantidad(errores, "error", "errores")}: no se aplicó nada. Corregila y volvé a importarla.";

    public static string ImportacionAplicada(int version, int agregadas, int modificadas, int dadasDeBaja) =>
        $"Se importó la planilla en la versión {version.ToString(CultureInfo.InvariantCulture)} de la configuración: "
        + $"{Cantidad(agregadas, "persona nueva", "personas nuevas")}, {Cantidad(modificadas, "modificada", "modificadas")} y {Cantidad(dadasDeBaja, "dada de baja", "dadas de baja")}.";

    public static string ImportacionSinCambios(int version) =>
        $"La planilla no cambia nada: la configuración sigue en la versión {version.ToString(CultureInfo.InvariantCulture)}.";

    /// <summary>Un error de la importación en palabras: del archivo (fila 0), del encabezado (fila 1) o de una fila; nunca el valor.</summary>
    public static string ErrorDeImportacion(ErrorImportacion error) => error.Fila switch
    {
        0 => error.Motivo switch
        {
            PlanillaInvalidaException.NoEsUnLibro => "El archivo no es un libro de Excel (.xlsx).",
            PlanillaInvalidaException.SinHojas => "El libro no tiene ninguna hoja.",
            PlanillaInvalidaException.Demasiado => "El archivo es demasiado grande: puede tener hasta 5 MB.",
            PlanillaInvalidaException.DemasiadasFilas => "La planilla tiene más de 2000 filas.",
            CodigosAdministracion.INWebAdministra => INWebAdministra,
            CodigosAdministracion.Requerido => $"La hoja «{error.Campo}» no tiene ninguna persona.",
            _ => "El archivo no se puede importar.",
        },
        1 => error.Motivo switch
        {
            CodigosAdministracion.Requerido => $"Falta la columna «{error.Campo}».",
            CodigosAdministracion.Repetido => $"La columna {error.Campo} repite el título de otra.",
            ImportacionPersonas.ColumnaDesconocida => $"La columna {error.Campo} no es «Nombre», «Código» ni el límite de un cupo.",
            _ => $"La columna {error.Campo} no es válida.",
        },
        _ => $"Fila {error.Fila.ToString(CultureInfo.InvariantCulture)}: " + error.Motivo switch
        {
            CodigosAdministracion.Requerido => $"falta «{error.Campo}».",
            CodigosAdministracion.Repetido => $"«{error.Campo}» está repetido en otra fila.",
            ImportacionPersonas.CodigoDadoDeBaja => "el código es de una persona dada de baja o fue revocado, y no se reasigna.",
            ImportacionPersonas.CodigoComoNumero => "el código tiene más de 15 dígitos y Excel lo guardó como número, así que cambió los últimos: escribilo como texto.",
            ImportacionPersonas.MismaPersona => "la misma persona ya está en otra fila con otro código.",
            CodigosAdministracion.FueraDeRango => $"«{error.Campo}» tiene que ser de 0 a {AdministracionLocal.LimiteMaximo.ToString(CultureInfo.InvariantCulture)}.",
            CodigosAdministracion.NoSonDigitos => $"«{error.Campo}» lleva sólo dígitos.",
            CodigosAdministracion.DemasiadoLargo => $"«{error.Campo}» es demasiado largo.",
            CodigosAdministracion.CaracterDeControl => $"«{error.Campo}» tiene caracteres que no se pueden guardar.",
            _ => $"«{error.Campo}» no es válido.",
        },
    };

    // La personalización de una opción (D2 de personalizar-opciones).
    public const string PresentacionNoGuardada = "No se pudo guardar la presentación: la carpeta de datos no se puede escribir. No cambió nada.";

    public static string PresentacionGuardada(string opcion) => $"Se guardó la presentación de «{opcion}».";

    // Exportar e importar Opciones (D4 y D5 de importar-opciones-desde-excel).
    public const string ReemplazarOpciones = "«Reemplazar» deja el catálogo como la planilla: las opciones que se quitan no se ofrecen más, y sus consumos siguen registrados.";

    public static string ExportacionOpcionesHecha(string archivo, int filas, string carpeta) =>
        $"Exportado {archivo} con {Cantidad(filas, "opción", "opciones")} en {carpeta}.";

    public static string PlanillaBaseHecha(string archivo, string carpeta) => $"Planilla base guardada como {archivo} en {carpeta}.";

    public static string ResumenImportacionOpciones(string archivo, int agregadas, int modificadas, int sinCambios, int quitadas) =>
        $"{archivo}: {Cantidad(agregadas, "opción nueva", "opciones nuevas")}, {Cantidad(modificadas, "modificada", "modificadas")}, "
        + $"{Cantidad(sinCambios, "sin cambios", "sin cambios")} y {Cantidad(quitadas, "que se quita", "que se quitan")}.";

    public static string ImportacionOpcionesConFilasDeEjemplo(int filas) =>
        $"La planilla todavía tiene {Cantidad(filas, "fila de ejemplo", "filas de ejemplo")}: si no se {(filas == 1 ? "borra, se agrega" : "borran, se agregan")} como {(filas == 1 ? "una opción" : "opciones")} del catálogo.";

    /// <summary>
    /// Un error de la importación de Opciones en palabras, con los rangos de Opciones: del archivo (fila 0), del encabezado
    /// (fila 1) o de una fila (D3 de importar-opciones-desde-excel); nunca el valor de la celda.
    /// </summary>
    public static string ErrorDeImportacionOpciones(ErrorImportacion error) => error.Fila switch
    {
        0 when error.Motivo == CodigosAdministracion.Requerido => "La hoja «Opciones» no tiene ninguna opción.",
        0 => ErrorDeImportacion(error),
        1 => error.Motivo switch
        {
            CodigosAdministracion.Requerido => $"Falta la columna «{error.Campo}».",
            CodigosAdministracion.Repetido => $"La columna {error.Campo} repite el título de otra.",
            ImportacionPersonas.ColumnaDesconocida => $"La columna {error.Campo} no es «Nombre», «Servicio», «Importe», «Franjas» ni las unidades de una categoría.",
            _ => $"La columna {error.Campo} no es válida.",
        },
        _ => $"Fila {error.Fila.ToString(CultureInfo.InvariantCulture)}: " + (error.Campo, error.Motivo) switch
        {
            (ImportacionOpciones.ColumnaCargas, _) => "la opción no tiene ninguna carga: completá las unidades de al menos una categoría.",
            (_, CodigosAdministracion.Requerido) => $"falta «{error.Campo}».",
            (_, CodigosAdministracion.Repetido) => "el nombre se repite en otra fila.",
            (ImportacionOpciones.ColumnaServicio, CodigosAdministracion.Desconocido) => "el servicio no es uno del puesto.",
            (ImportacionOpciones.ColumnaImporte, CodigosAdministracion.NoSonDigitos) => "el importe no es un número: escribilo como 1.800,00 o como número de Excel.",
            (ImportacionOpciones.ColumnaImporte, CodigosAdministracion.FueraDeRango) => "el importe tiene que ser de 0 a 9.999.999,99, con hasta dos decimales.",
            (_, CodigosAdministracion.NoSonDigitos) => $"«{error.Campo}» lleva sólo dígitos.",
            (_, CodigosAdministracion.FueraDeRango) => $"«{error.Campo}» tiene que ser de 0 a {AdministracionLocal.UnidadesMaximas.ToString(CultureInfo.InvariantCulture)}.",
            (_, CodigosAdministracion.HoraInvalida) => "las franjas van como «11:00 a 14:00», separadas por punto y coma.",
            (_, CodigosAdministracion.FranjaVacia) => "una franja empieza y termina a la misma hora.",
            (_, CodigosAdministracion.DemasiadoLargo) => $"«{error.Campo}» es demasiado largo.",
            (_, CodigosAdministracion.CaracterDeControl) => $"«{error.Campo}» tiene caracteres que no se pueden guardar.",
            _ => $"«{error.Campo}» no es válido.",
        },
    };

    public static string ImportacionOpcionesAplicada(int version, int agregadas, int modificadas, int quitadas) =>
        $"Se importó la planilla en la versión {version.ToString(CultureInfo.InvariantCulture)} de la configuración: "
        + $"{Cantidad(agregadas, "opción nueva", "opciones nuevas")}, {Cantidad(modificadas, "modificada", "modificadas")} y {Cantidad(quitadas, "quitada", "quitadas")}.";

    /// <summary>El resguardo escrito, por su nombre y su carpeta (D4 de resguardar-configuracion).</summary>
    public static string ResguardoGuardado(string archivo, string carpeta) => $"Guardado {archivo} en {carpeta}.";

    public const string ResguardoNoGuardado = "No se pudo escribir el resguardo en la carpeta elegida: no se guardó nada.";

    /// <summary>
    /// De qué versión y de cuándo es el resguardo, y cuántas personas activas, opciones y permisos de operador vigentes trae
    /// (D2), sin nombres ni códigos.
    /// </summary>
    public static string ResumenResguardo(string archivo, ResguardoRevisado resguardo, TimeZoneInfo zona) =>
        $"{archivo} tiene la versión {resguardo.VersionOrigen.ToString(CultureInfo.InvariantCulture)} de la configuración, guardada el "
        + $"{TimeZoneInfo.ConvertTime(resguardo.FechaUtc, zona).ToString("dd/MM/yyyy 'a las' HH:mm", CultureInfo.InvariantCulture)}, con "
        + $"{Cantidad(resguardo.Personas, "persona activa", "personas activas")}, {Cantidad(resguardo.Opciones, "opción", "opciones")} y "
        + $"{Cantidad(resguardo.Permisos, "permiso de operador vigente", "permisos de operador vigentes")}.";

    /// <summary>Qué ajustes locales trae el resguardo; los que no trae vuelven al de ejemplo al recuperar (D3).</summary>
    public static string AjustesDelResguardo(ResguardoRevisado resguardo)
    {
        var ajustes = new List<string>();
        if (resguardo.Estilo is not null)
        {
            ajustes.Add("el estilo visual");
        }

        if (resguardo.Textos is not null)
        {
            ajustes.Add("los textos del comprobante");
        }

        if (resguardo.Logos.ContainsKey(LogoPuesto.Empresa))
        {
            ajustes.Add("el logo de la empresa");
        }

        if (resguardo.Logos.ContainsKey(LogoPuesto.Concesionaria))
        {
            ajustes.Add("el logo de la concesionaria");
        }

        if (resguardo.Presentacion is { Count: > 0 } presentacion)
        {
            ajustes.Add("la presentación de " + Cantidad(presentacion.Count, "opción", "opciones"));
        }

        return ajustes.Count == 0
            ? "No trae ajustes locales: el estilo, los logos, los textos y la presentación vuelven a los de ejemplo."
            : "Trae " + (ajustes.Count == 1 ? ajustes[0] : string.Join(", ", ajustes[..^1]) + " y " + ajustes[^1])
                + "; lo que no trae vuelve al de ejemplo.";
    }

    public const string RecuperarReemplaza =
        "«Recuperar» reemplaza la configuración y los ajustes locales del puesto por los del resguardo, en una versión nueva; la anterior queda en la base, "
        + "pero los ajustes locales de ahora no: si los querés conservar, guardá antes un resguardo.";

    public const string SinCarpetaResguardo = "No se eligió una carpeta: no se guardó el resguardo.";

    /// <summary>Por qué un resguardo no se recupera (D2), sin mostrar un código ni un nombre.</summary>
    public static string ResguardoRechazado(string archivo, string motivo) => $"No se recupera {archivo}: " + motivo switch
    {
        MotivosResguardo.Ilegible => "no se pudo leer el archivo.",
        MotivosResguardo.NoEsUnZip => "no es un resguardo del puesto, porque no es un .zip.",
        MotivosResguardo.DemasiadoGrande => "es demasiado grande: pasa de 50 MB, comprimido o descomprimido, una de sus partes pasa de 20 MB o tiene más de 500 partes.",
        MotivosResguardo.ParteDesconocida => "tiene un archivo que no es de un resguardo.",
        MotivosResguardo.HuellaDistinta => "una de sus partes cambió después de guardarlo.",
        MotivosResguardo.OtroFormato => "es de otro formato de resguardo.",
        MotivosResguardo.OtroEsquema => "es de otra versión de la base del puesto.",
        MotivosResguardo.AjusteInvalido => "uno de sus ajustes locales no es válido.",
        MotivosResguardo.AjustesNoEscritos => "no se pudieron escribir sus ajustes locales en la carpeta del puesto, y no cambió nada.",
        MotivosResguardo.BaseSinRespuesta => "la base no respondió, y no cambió nada.",
        CodigosAdministracion.ResguardoInvalido => "su configuración no tiene la forma de la de este puesto.",
        CodigosAdministracion.CodigoEnConflicto => "un código de identificación del resguardo es, en este puesto, de otra persona, y los códigos no se reasignan.",
        CodigosAdministracion.INWebAdministra => "la configuración de este puesto la administra INWeb.",
        _ => "no se pudo recuperar.",
    };

    /// <summary>Lo recuperado, con la versión nueva (D3).</summary>
    public static string ResguardoRecuperado(int version) =>
        $"Se recuperó el resguardo en la versión {version.ToString(CultureInfo.InvariantCulture)} de la configuración, con sus ajustes locales.";

    public const string ExportacionGuardada = "Se guardó la exportación automática: vale desde la próxima pasada.";
    public const string ExportacionNoGuardada = "No se pudo guardar la exportación: sigue la de antes.";
    public const string SinCarpetaExportacion = "No se eligió una carpeta: queda la de antes.";
    public const string ExportacionSinDestinos = "No hay ningún destino guardado: elegí una carpeta o el SFTP simulado y tocá «Guardar».";
    public const string ExportacionBaseOcupada = "La base no respondió y no se exportó nada: probá de nuevo en un momento.";

    // La pestaña Lector (D8 de elegir-medio-de-identificacion).
    public const string LectorGuardado = "Se guardó el medio de identificación: vale desde ahora.";
    public const string LectorNoGuardado = "No se pudo guardar el medio de identificación: sigue el de antes.";
    public const string ExplicacionLector =
        "El lector identifica como el código tipeado, sólo en la pantalla de identificación; el código leído no se guarda en ningún registro.";

    /// <summary>Cómo está el lector, sin punto, como el estado de una bitácora.</summary>
    public static string EstadoLector(Lector.EstadoLector estado, Lector.ConfiguracionLector configuracion) => estado switch
    {
        Lector.EstadoLector.Escuchando when configuracion.Medio == Lector.MedioIdentificacion.Simulador => "Lector: escuchando el simulador interno",
        Lector.EstadoLector.Escuchando => $"Lector: escuchando en {configuracion.Puerto} a {configuracion.Formato}",
        Lector.EstadoLector.PuertoNoExiste => $"Lector: {configuracion.Puerto} no está en este equipo; reintenta cada 5 segundos",
        Lector.EstadoLector.PuertoOcupado => $"Lector: {configuracion.Puerto} está ocupado por otro programa; reintenta cada 5 segundos",
        Lector.EstadoLector.PuertoCaido => $"Lector: {configuracion.Puerto} se desconectó; reintenta cada 5 segundos",
        Lector.EstadoLector.SinPuerto => "Lector: este puesto no tiene acceso a puertos serie",
        _ => "Lector: apagado, se identifica con el teclado",
    };

    /// <summary>La última lectura, sin el código ni su largo y sin punto: la hora, qué pasó y por qué se descartó.</summary>
    public static string UltimaLectura(Lector.UltimaLectura? ultima, TimeZoneInfo zona) => ultima switch
    {
        null => "Última lectura: ninguna todavía",
        { Resultado: "IDENTIFIED" } => $"Última lectura: {Hora(ultima.Hora, zona)}, identificó a la persona",
        { Resultado: "REJECTED" } => $"Última lectura: {Hora(ultima.Hora, zona)}, se rechazó como un código tipeado",
        _ => $"Última lectura: {Hora(ultima.Hora, zona)}, se descartó ({MotivoDescarte(ultima.Motivo)})",
    };

    /// <summary>Los puertos serie del equipo, sin punto.</summary>
    public static string PuertosDelEquipo(IReadOnlyList<string> puertos) =>
        puertos.Count == 0 ? "Este equipo no tiene puertos serie" : "Puertos del equipo: " + string.Join(", ", puertos);

    private static string Hora(DateTimeOffset instante, TimeZoneInfo zona) =>
        TimeZoneInfo.ConvertTime(instante, zona).ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string MotivoDescarte(string? motivo) => motivo switch
    {
        "NOT_IDLE" => "había una atención en curso",
        "DIALOG_OPEN" => "había un diálogo abierto",
        "STORAGE_ERROR" => "la base no respondía",
        "STALE_FRAGMENT" => "un resto viejo de una trama",
        "TOO_LONG" => "más de 32 caracteres",
        "INVALID_CHARACTERS" => "caracteres que no son de un código",
        "EMPTY" => "una lectura vacía",
        _ => "un motivo desconocido",
    };

    // La pestaña Mails (D7 de avisar-por-correo).
    public const string CorreoGuardado = "Se guardaron los correos: los avisos salen desde ahora.";
    public const string CorreoNoGuardado = "No se pudieron guardar los correos: siguen los de antes.";
    public const string ExplicacionCorreo =
        "El envío es simulado: cada correo queda como un archivo en la carpeta correo-simulado del puesto. Ninguna clave se guarda.";
    public const string PruebaCorreoEnCola = "La prueba no salió todavía: quedó en la cola y se reintenta cada minuto.";
    public const string PruebaCorreoSinCasillas = "Los correos están apagados o no tienen casillas guardadas: activalos, guardalos y probá de nuevo.";
    public const string PruebaCorreoFallida = "No se pudo enviar la prueba: la carpeta del puesto no respondió; probá de nuevo en un momento.";

    /// <summary>La prueba que salió, con las casillas que la recibieron.</summary>
    public static string PruebaCorreoEnviada(int casillas) => $"Se envió la prueba a {Cantidad(casillas, "casilla", "casillas")}.";

    /// <summary>Los correos que esperan en la cola, sin punto, como el estado de una bitácora.</summary>
    public static string EstadoColaCorreo(int pendientes) =>
        pendientes == 0 ? "Cola: sin pendientes" : $"Cola: {Cantidad(pendientes, "pendiente", "pendientes")}";

    /// <summary>
    /// Lo que hizo «Exportar ahora» (D4 de exportar-consumos-automaticamente): que no hay ningún destino guardado, los
    /// consumos exportados, que no había nuevos, o cada destino que falló con su motivo.
    /// </summary>
    public static string ExportacionAhora(IReadOnlyList<ResultadoDestino> resultados)
    {
        if (resultados.Count == 0)
        {
            return ExportacionSinDestinos;
        }

        var fallas = resultados.Where(r => r.Error is not null).ToList();
        if (fallas.Count > 0)
        {
            return string.Join(" ", fallas.Select(f => $"No se pudo exportar {(f.Destino == ExportacionAutomatica.Sftp ? "al SFTP simulado" : "a la carpeta")}: {MotivoExportacion(f.Error!)}."))
                + " Reintenta en la próxima pasada.";
        }

        var filas = resultados.Max(r => r.Filas);
        return filas == 0
            ? "No había consumos nuevos para exportar."
            : filas == 1 ? "Se exportó 1 consumo." : $"Se exportaron {Cantidad(filas, "consumo", "consumos")}.";
    }

    /// <summary>
    /// El estado de un destino, sin punto, como el de una bitácora (D5): todavía sin exportar, la hora de su último envío
    /// (que no dice «al día»: después pudo haber consumos nuevos), o la hora y el motivo de su última falla.
    /// </summary>
    public static string EstadoExportacion(string destino, EstadoDestino? estado, TimeZoneInfo zona)
    {
        var nombre = char.ToUpper(NombreDestino(destino)[0], CultureInfo.GetCultureInfo("es-AR")) + NombreDestino(destino)[1..];
        string Hora(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, zona).ToString("HH:mm", CultureInfo.InvariantCulture);
        return estado switch
        {
            { Error: { } error, UltimoIntento: { } intento } => $"{nombre}: falló a las {Hora(intento)}, {MotivoExportacion(error)}; reintenta en la próxima pasada",
            { UltimoExito: { } exito } => $"{nombre}: último envío a las {Hora(exito)}",
            _ => $"{nombre}: todavía sin exportar",
        };
    }

    private static string NombreDestino(string destino) => destino == ExportacionAutomatica.Sftp ? "el SFTP simulado" : "la carpeta";

    /// <summary>El motivo de una falla de la exportación en palabras, para la pestaña y para el aviso por correo.</summary>
    internal static string MotivoExportacion(string motivo) => motivo switch
    {
        "SERVER_UNREACHABLE" => "el servidor no responde",
        "FOLDER_NOT_FOUND" => "la carpeta no existe",
        "ACCESS_DENIED" => "no hay permiso para escribir",
        "FILE_IN_USE" => "el archivo está abierto en otro programa",
        "STATE_UNREADABLE" => "no se pudo leer el estado de la exportación",
        _ => "no se pudo escribir",
    };

    private static string Cantidad(int cantidad, string uno, string varios) =>
        $"{cantidad.ToString(CultureInfo.InvariantCulture)} {(cantidad == 1 ? uno : varios)}";

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
        $"Copia {numeroCopia.ToString(CultureInfo.InvariantCulture)} enviada con la leyenda REIMPRESIÓN.";

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
