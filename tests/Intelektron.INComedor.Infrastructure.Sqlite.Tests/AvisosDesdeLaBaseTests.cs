namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Lo que los avisos por correo leen de la base (avisos-correo de avisar-por-correo, D5 y D9): la persona de una
/// identificación rechazada con una credencial conocida, sin abrir la sesión, y el resumen de un día de la bitácora
/// general para la señal de vida, sin nombres.
/// </summary>
public class AvisosDesdeLaBaseTests : ConfirmacionTestBase
{
    private const string CodigoVencido = "SINTETICO-VENCIDO-88";
    private const string CodigoDesconocido = "SINTETICO-NINGUNO-88";

    [Fact(DisplayName = "avisos-correo · Una persona sin consumo: una credencial vencida devuelve la persona y su nombre visible, sin abrir la sesión ni cambiar el motivo")]
    public void La_persona_de_un_rechazo()
    {
        var persona = InsertarVencida();

        var resultado = new IdentificacionPorCodigo(Base, Reloj).Identificar(CodigoVencido, PropositoSesion.Consumption);

        Assert.False(resultado.Abierta);
        Assert.Equal("CREDENTIAL_EXPIRED", resultado.ReasonCode);
        Assert.Equal(persona, resultado.PersonId);
        Assert.Equal("Persona sintética vencida", resultado.NombreVisible);
        using var conexion = Abrir();
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorSesionesIdentidad"));
    }

    [Fact(DisplayName = "avisos-correo · Una persona sin consumo: un código desconocido no tiene persona ni nombre")]
    public void Un_codigo_desconocido_no_tiene_persona()
    {
        var resultado = new IdentificacionPorCodigo(Base, Reloj).Identificar(CodigoDesconocido, PropositoSesion.Consumption);

        Assert.Null(resultado.PersonId);
        Assert.Null(resultado.NombreVisible);
    }

    [Fact(DisplayName = "avisos-correo · La señal de las 08:00: el resumen de un día cuenta los consumos, los rechazos, los comprobantes sin imprimir y las advertencias, sin los de otro día")]
    public void El_resumen_de_un_dia()
    {
        var dia = Reloj.Ahora;
        Assert.Equal(TipoResultado.Autorizada, Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun)).Tipo);
        Assert.Equal(TipoResultado.Autorizada, Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.Refrigerio)).Tipo);
        new IdentificacionPorCodigo(Base, Reloj).Identificar(CodigoDesconocido, PropositoSesion.Consumption);
        var trabajos = new TrabajosImpresion(Base, Reloj, new ImpresoraSinPapel());
        trabajos.EnviarPendientes();
        Reloj.Ahora = dia.AddDays(1);
        new IdentificacionPorCodigo(Base, Reloj).Identificar(CodigoDesconocido, PropositoSesion.Consumption);

        var resumen = new ResumenDelDia(Base).Contar(dia.AddHours(-12), dia.AddHours(12));

        Assert.Equal(2, resumen.Consumos);
        Assert.Equal(1, resumen.Rechazos);
        Assert.Equal(2, resumen.SinImprimir);
        Assert.Equal(0, resumen.Advertencias);
        Assert.Equal(1, new ResumenDelDia(Base).Contar(dia.AddHours(12), dia.AddHours(36)).Rechazos);
    }

    private Guid InsertarVencida()
    {
        var persona = Guid.NewGuid();
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Persona sintética vencida', 1, 1)",
            ("$vc", Fixture.SnapshotId), ("$per", persona));
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, $hasta, NULL)",
            ("$vc", Fixture.SnapshotId), ("$cre", Guid.NewGuid()), ("$per", persona), ("$valor", CodigoVencido),
            ("$hasta", Iso(Reloj.Ahora.AddDays(-1))));
        return persona;
    }

    /// <summary>Una impresora que siempre responde sin papel.</summary>
    private sealed class ImpresoraSinPapel : IImpresoraComprobantes
    {
        public RespuestaImpresora Enviar(ComprobanteAImprimir comprobante) => RespuestaImpresora.Fallido("PAPER_OUT");
    }
}
