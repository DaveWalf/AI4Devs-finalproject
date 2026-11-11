using System.Reflection;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Las oraciones de la pantalla terminan en punto, y los títulos, rótulos, botones y estados no (presentacion-puesto de
/// puntuar-la-interfaz, D2 y D3). Recorre <see cref="MensajesAtencion"/>, donde viven todas las oraciones del puesto: una
/// constante nueva sin punto, o un título nuevo que no pase por <see cref="SinPunto"/>, hace fallar la prueba y la nombra.
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
        ];

        var sinPunto = armados.Where(m => !m.EndsWith('.')).ToList();

        Assert.True(sinPunto.Count == 0, "Oraciones sin punto final:\n" + string.Join('\n', sinPunto));
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
        ];

        Assert.All(rotulos, r => Assert.False(r.EndsWith('.'), $"«{r}»"));
    }

    private static IEnumerable<(string Nombre, string Texto)> Constantes() =>
        typeof(MensajesAtencion).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (f.Name, (string)f.GetRawConstantValue()!));
}
