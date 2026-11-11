using System.Reflection;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Las oraciones de la pantalla terminan en punto, y los títulos, rótulos, botones y estados no (presentacion-puesto de
/// puntuar-la-interfaz, D2 y D3). Recorre <see cref="MensajesAtencion"/>, donde viven las oraciones del puesto (los estados
/// cortos de las franjas, el saludo y los rótulos quedan en sus componentes): una constante nueva sin punto, un título
/// nuevo que no pase por <see cref="SinPunto"/> o un método nuevo sin clasificar hacen fallar la prueba y la nombran. Un
/// literal nuevo en un componente no lo ve: para eso está la regla de <c>openspec/config.yaml</c>.
/// </summary>
public class PuntoFinalTests
{
    /// <summary>Las constantes que no son oraciones: el título del reposo y un estado de la pestaña Red (D2).</summary>
    private static readonly string[] SinPunto = [nameof(MensajesAtencion.TituloReposo), nameof(MensajesAtencion.SinRed)];

    private static readonly string[] Motivos =
    [
        "LOCAL_LIMIT_REACHED", "PERSON_INACTIVE", "CREDENTIAL_REVOKED", "CREDENTIAL_EXPIRED", "RULE_DENIED", "SERVICE_NOT_ENABLED",
        "OUTSIDE_SERVICE_HOURS", "IDENTITY_NOT_RESOLVED", "PERMISSION_MISSING", "PERMISSION_EXPIRED", "PERMISSION_REVOKED",
        "PERMISSION_NOT_YET_VALID", "SESSION_NOT_USABLE", "PRINT_JOB_NOT_RECOVERABLE", "UN_MOTIVO_DESCONOCIDO",
    ];

    [Fact(DisplayName = "presentacion-puesto · Un mensaje nuevo sin punto: cada constante de MensajesAtencion que es una oración termina en punto")]
    public void Las_constantes_terminan_en_punto()
    {
        var sinPunto = Constantes()
            .Where(c => !SinPunto.Contains(c.Nombre) && !c.Texto.EndsWith('.'))
            .Select(c => $"{c.Nombre}: «{c.Texto}»")
            .ToList();

        Assert.True(sinPunto.Count == 0, "Oraciones sin punto final:\n" + string.Join('\n', sinPunto));
    }

    [Fact(DisplayName = "presentacion-puesto · Un título y un estado no llevan punto: el título del reposo y «Sin conexión de red» no terminan en punto")]
    public void Un_titulo_y_un_estado_no_llevan_punto()
    {
        Assert.All(Constantes().Where(c => SinPunto.Contains(c.Nombre)), c => Assert.False(c.Texto.EndsWith('.'), $"{c.Nombre}: «{c.Texto}»"));
        Assert.Equal(SinPunto.Length, Constantes().Count(c => SinPunto.Contains(c.Nombre)));
    }

    [Fact(DisplayName = "presentacion-puesto · Un aviso de la atención: los mensajes que se arman con datos, como los motivos de un rechazo, terminan en punto")]
    public void Los_mensajes_armados_terminan_en_punto()
    {
        IEnumerable<string> armados =
        [
            .. Motivos.Select(MensajesAtencion.DeMotivo),
            MensajesAtencion.DeMotivo(null),
            MensajesAtencion.VersionGuardada(2),
            MensajesAtencion.CopiaEnviada(1),
            MensajesAtencion.RedCanceladaEnPaso(2, 3),
            MensajesAtencion.RedFallo(1, 3, 5, conAplicados: false),
            MensajesAtencion.RedFallo(2, 3, 5, conAplicados: true),
            MensajesAtencion.PedidoOperadorPara(recuperar: true, "0a1b2c3d"),
            MensajesAtencion.CarpetaElegida(@"C:\Exportaciones"),
            MensajesAtencion.ExportacionHecha("INComedor-consumos-20261110-120000.csv", 2, @"C:\Exportaciones"),
            MensajesAtencion.ExportacionPersonasHecha("INComedor-personas-20261110-120000.csv", 9, @"C:\Exportaciones"),
            MensajesAtencion.PlanillaDeEjemploHecha("INComedor-planilla-personas-20261111-120000.xlsx", @"C:\Exportaciones"),
            MensajesAtencion.ResumenImportacion("personas.xlsx", 1, 2, 0),
            MensajesAtencion.ImportacionReemplazar(0),
            MensajesAtencion.ImportacionReemplazar(3),
            MensajesAtencion.ImportacionConErrores(1),
            MensajesAtencion.ImportacionAplicada(2, 1, 1, 0),
            MensajesAtencion.ImportacionSinCambios(2),
            MensajesAtencion.ImportacionConFilasDeEjemplo(1),
            MensajesAtencion.ImportacionConFilasDeEjemplo(2),
            MensajesAtencion.ImportacionCodigosComoNumero(1),
            MensajesAtencion.ImportacionCodigosComoNumero(3),
            MensajesAtencion.PresentacionGuardada("Almuerzo opcional"),
            MensajesAtencion.ExportacionOpcionesHecha("INComedor-opciones-20261111-120000.xlsx", 15, @"C:\Exportaciones"),
            MensajesAtencion.PlanillaBaseHecha("INComedor-planilla-opciones-20261111-120000.xlsx", @"C:\Exportaciones"),
            MensajesAtencion.ResumenImportacionOpciones("opciones.xlsx", 1, 1, 12, 1),
            MensajesAtencion.ImportacionOpcionesAplicada(2, 1, 1, 1),
            MensajesAtencion.ImportacionOpcionesConFilasDeEjemplo(1),
            MensajesAtencion.ImportacionOpcionesConFilasDeEjemplo(2),
            .. ErroresDeImportacionOpciones.Select(MensajesAtencion.ErrorDeImportacionOpciones),
            .. ErroresDeImportacion.Select(MensajesAtencion.ErrorDeImportacion),
            MensajesAtencion.ResguardoGuardado("INComedor-resguardo-20261111-120000.zip", @"C:\Exportaciones"),
            MensajesAtencion.ResumenResguardo("INComedor-resguardo-20261111-120000.zip", ResguardoDeEjemplo(conAjustes: true), TimeZoneInfo.Utc),
            MensajesAtencion.AjustesDelResguardo(ResguardoDeEjemplo(conAjustes: true)),
            MensajesAtencion.AjustesDelResguardo(ResguardoDeEjemplo(conAjustes: false)),
            .. MotivosDeResguardo.Select(m => MensajesAtencion.ResguardoRechazado("resguardo.zip", m)),
            MensajesAtencion.ResguardoRecuperado(3),
        ];

        var sinPunto = armados.Where(m => !m.EndsWith('.')).ToList();

        Assert.True(sinPunto.Count == 0, "Oraciones sin punto final:\n" + string.Join('\n', sinPunto));
    }

    /// <summary>
    /// Cada método que arma un texto de la pantalla, con cómo cierra: una oración con punto, el pedido del motivo con dos
    /// puntos (presenta los botones) o un rótulo o estado sin punto (D2). Un método nuevo que no esté acá hace fallar
    /// <see cref="Cada_metodo_esta_clasificado"/>, que obliga a sumarlo con su valor de ejemplo.
    /// </summary>
    private static readonly string[] MetodosConPunto =
    [
        nameof(MensajesAtencion.DeMotivo), nameof(MensajesAtencion.VersionGuardada), nameof(MensajesAtencion.CopiaEnviada),
        nameof(MensajesAtencion.RedCanceladaEnPaso), nameof(MensajesAtencion.RedFallo), nameof(MensajesAtencion.PedidoOperadorPara),
        nameof(MensajesAtencion.CarpetaElegida), nameof(MensajesAtencion.ExportacionHecha), nameof(MensajesAtencion.ExportacionPersonasHecha),
        nameof(MensajesAtencion.PlanillaDeEjemploHecha), nameof(MensajesAtencion.ResumenImportacion), nameof(MensajesAtencion.ImportacionReemplazar),
        nameof(MensajesAtencion.ImportacionConErrores), nameof(MensajesAtencion.ImportacionAplicada), nameof(MensajesAtencion.ImportacionSinCambios),
        nameof(MensajesAtencion.ErrorDeImportacion), nameof(MensajesAtencion.ImportacionConFilasDeEjemplo),
        nameof(MensajesAtencion.ImportacionCodigosComoNumero), nameof(MensajesAtencion.PresentacionGuardada),
        nameof(MensajesAtencion.ExportacionOpcionesHecha), nameof(MensajesAtencion.PlanillaBaseHecha),
        nameof(MensajesAtencion.ResumenImportacionOpciones), nameof(MensajesAtencion.ImportacionOpcionesAplicada),
        nameof(MensajesAtencion.ImportacionOpcionesConFilasDeEjemplo), nameof(MensajesAtencion.ErrorDeImportacionOpciones),
        nameof(MensajesAtencion.ResguardoGuardado), nameof(MensajesAtencion.ResumenResguardo), nameof(MensajesAtencion.AjustesDelResguardo),
        nameof(MensajesAtencion.ResguardoRechazado), nameof(MensajesAtencion.ResguardoRecuperado),
    ];

    /// <summary>Cada motivo por el que un resguardo no se recupera, y uno desconocido (D2 de resguardar-configuracion).</summary>
    private static readonly string[] MotivosDeResguardo =
    [
        MotivosResguardo.Ilegible, MotivosResguardo.NoEsUnZip, MotivosResguardo.DemasiadoGrande, MotivosResguardo.ParteDesconocida,
        MotivosResguardo.HuellaDistinta, MotivosResguardo.OtroFormato, MotivosResguardo.OtroEsquema, MotivosResguardo.AjusteInvalido,
        CodigosAdministracion.ResguardoInvalido, CodigosAdministracion.CodigoEnConflicto, CodigosAdministracion.INWebAdministra,
        "LOCAL_FILES_NOT_WRITTEN", "DATABASE_ERROR", "OTRO",
    ];

    /// <summary>Un resguardo revisado de ejemplo, con todos los ajustes locales o con ninguno.</summary>
    private static ResguardoRevisado ResguardoDeEjemplo(bool conAjustes)
    {
        var logo = new LogoElegido(".png", "image/png", "PNG", [0x89, 0x50, 0x4E, 0x47]);
        return new ResguardoRevisado("ABC", 2, DateTimeOffset.Parse("2026-11-11T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture), 9, 15, 1, [],
            "{}", conAjustes ? EstiloVisual.PorDefecto : null, conAjustes ? TextosComprobante.PorDefecto : null,
            conAjustes ? new Dictionary<LogoPuesto, LogoElegido> { [LogoPuesto.Empresa] = logo, [LogoPuesto.Concesionaria] = logo } : [],
            conAjustes ? new Dictionary<Guid, PresentacionOpcion> { [Guid.NewGuid()] = PresentacionOpcion.PorDefecto } : null, []);
    }

    /// <summary>Un error de cada clase: del archivo, del encabezado y de una fila, con cada motivo y uno desconocido.</summary>
    private static readonly ErrorImportacion[] ErroresDeImportacion =
    [
        new(0, "Archivo", PlanillaInvalidaException.NoEsUnLibro), new(0, "Archivo", PlanillaInvalidaException.SinHojas),
        new(0, "Archivo", PlanillaInvalidaException.Demasiado), new(0, "Archivo", PlanillaInvalidaException.DemasiadasFilas),
        new(0, "Archivo", CodigosAdministracion.INWebAdministra), new(0, "Personas", CodigosAdministracion.Requerido), new(0, "Archivo", "OTRO"),
        new(1, "Código", CodigosAdministracion.Requerido), new(1, "Nombre", CodigosAdministracion.Repetido),
        new(1, "Postre: límite", ImportacionPersonas.ColumnaDesconocida), new(1, "X", "OTRO"),
        new(2, "Código", CodigosAdministracion.Requerido), new(3, "Código", CodigosAdministracion.Repetido),
        new(4, "Código", ImportacionPersonas.CodigoDadoDeBaja), new(5, "ALMUERZO: límite", CodigosAdministracion.FueraDeRango),
        new(4, "Código", ImportacionPersonas.CodigoComoNumero), new(4, "Código", ImportacionPersonas.MismaPersona),

        new(6, "ALMUERZO: límite", CodigosAdministracion.NoSonDigitos), new(7, "Nombre", CodigosAdministracion.DemasiadoLargo),
        new(8, "Nombre", CodigosAdministracion.CaracterDeControl), new(9, "Nombre", "OTRO"),
    ];

    /// <summary>Un error de la importación de Opciones de cada clase, con cada motivo y uno desconocido.</summary>
    private static readonly ErrorImportacion[] ErroresDeImportacionOpciones =
    [
        new(0, "Opciones", CodigosAdministracion.Requerido), new(0, "Archivo", PlanillaInvalidaException.NoEsUnLibro),
        new(1, "Importe", CodigosAdministracion.Requerido), new(1, "C", CodigosAdministracion.Repetido),
        new(1, "G", ImportacionPersonas.ColumnaDesconocida), new(1, "X", "OTRO"),
        new(2, "Cargas", CodigosAdministracion.Requerido), new(3, "Nombre", CodigosAdministracion.Requerido),
        new(4, "Nombre", CodigosAdministracion.Repetido), new(5, "Servicio", CodigosAdministracion.Desconocido),
        new(6, "Importe", CodigosAdministracion.NoSonDigitos), new(7, "Importe", CodigosAdministracion.FueraDeRango),
        new(8, "ALMUERZO: unidades", CodigosAdministracion.NoSonDigitos), new(9, "ALMUERZO: unidades", CodigosAdministracion.FueraDeRango),
        new(10, "Franjas", CodigosAdministracion.HoraInvalida), new(11, "Franjas", CodigosAdministracion.FranjaVacia),
        new(12, "Nombre", CodigosAdministracion.DemasiadoLargo), new(13, "Nombre", CodigosAdministracion.CaracterDeControl),
        new(14, "Nombre", "OTRO"),
    ];

    private static readonly string[] MetodosSinPunto =
    [
        nameof(MensajesAtencion.Motivo), nameof(MensajesAtencion.EstadoBitacora), nameof(MensajesAtencion.UsoDelDia),
        nameof(MensajesAtencion.ResumenOpciones), nameof(MensajesAtencion.Cargo), nameof(MensajesAtencion.PedidoMotivo),
    ];

    [Fact(DisplayName = "presentacion-puesto · Un mensaje nuevo sin punto: cada método de MensajesAtencion que arma un texto está clasificado, con o sin punto")]
    public void Cada_metodo_esta_clasificado()
    {
        var metodos = typeof(MensajesAtencion).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(string) && !m.IsSpecialName)
            .Select(m => m.Name)
            .Distinct()
            .Order(StringComparer.Ordinal);

        Assert.Equal(MetodosConPunto.Concat(MetodosSinPunto).Order(StringComparer.Ordinal), metodos);
    }

    [Fact(DisplayName = "presentacion-puesto · Un título y un estado no llevan punto: el pedido del motivo cierra con dos puntos, porque presenta los botones")]
    public void El_pedido_del_motivo_cierra_con_dos_puntos()
    {
        Assert.EndsWith("Elegí el motivo:", MensajesAtencion.PedidoMotivo("0a1b2c3d", "Almuerzo común"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "presentacion-puesto · Un título y un estado no llevan punto: los botones del motivo, el estado de una bitácora y el uso del día no terminan en punto")]
    public void Los_botones_y_los_estados_no_llevan_punto()
    {
        IEnumerable<string> rotulos =
        [
            .. Enum.GetValues<MotivoReimpresion>().Select(MensajesAtencion.Motivo),
            MensajesAtencion.EstadoBitacora(new EstadoBitacora(DestinoBitacora.General, true, 0, null), TimeZoneInfo.Utc),
            MensajesAtencion.EstadoBitacora(new EstadoBitacora(DestinoBitacora.Interacciones, false, 3, null), TimeZoneInfo.Utc),
            MensajesAtencion.UsoDelDia([new CategoriaAgotada(Guid.NewGuid(), "ALMUERZO", 1, 1)])!,
            MensajesAtencion.ResumenOpciones(9, 2),
            MensajesAtencion.Cargo("ARS", 2, 150000),
        ];

        Assert.All(rotulos, r => Assert.False(r.EndsWith('.'), $"«{r}»"));
    }

    private static IEnumerable<(string Nombre, string Texto)> Constantes() =>
        typeof(MensajesAtencion).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (f.Name, (string)f.GetRawConstantValue()!));
}
