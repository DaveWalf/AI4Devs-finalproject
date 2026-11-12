namespace Intelektron.INComedor.Domain.Tests;

/// <summary>Clasificación de la credencial al identificarse (RB25), en memoria (D2 de atender-seleccion-y-confirmacion).</summary>
public class ClasificadorCredencialTests
{
    private static readonly DateTimeOffset Ahora = DateTimeOffset.Parse("2026-10-30T15:00:00Z");

    [Fact(DisplayName = "RB25 · Código desconocido: IDENTITY_NOT_RESOLVED con causa interna UNKNOWN_CODE")]
    public void Sin_credencial_no_se_resuelve_con_causa_codigo_desconocido()
    {
        var resultado = ClasificadorCredencial.Clasificar(null, Ahora);

        Assert.Equal(MotivoIdentificacion.IdentityNotResolved, resultado.Motivo);
        Assert.Equal(CausaIdentificacion.UnknownCode, resultado.Causa);
        Assert.False(resultado.Vigente);
    }

    [Fact(DisplayName = "AT52 · Código revocado: CREDENTIAL_REVOKED")]
    public void Credencial_revocada_es_revocada()
    {
        var credencial = new CredencialIdentificacion(null, null, Ahora.AddDays(-1));

        var resultado = ClasificadorCredencial.Clasificar(credencial, Ahora);

        Assert.Equal(MotivoIdentificacion.CredentialRevoked, resultado.Motivo);
        Assert.Null(resultado.Causa);
    }

    [Fact(DisplayName = "RB25 · La revocación vale desde la activación: una fecha de revocación posterior al instante también da CREDENTIAL_REVOKED")]
    public void Revocacion_no_mira_el_reloj()
    {
        var credencial = new CredencialIdentificacion(null, null, Ahora.AddDays(1));

        var resultado = ClasificadorCredencial.Clasificar(credencial, Ahora);

        Assert.Equal(MotivoIdentificacion.CredentialRevoked, resultado.Motivo);
    }

    [Fact(DisplayName = "RB25 · Revocado y vencido a la vez: CREDENTIAL_REVOKED")]
    public void Revocada_y_vencida_es_revocada()
    {
        var credencial = new CredencialIdentificacion(null, Ahora.AddDays(-2), Ahora.AddDays(-1));

        var resultado = ClasificadorCredencial.Clasificar(credencial, Ahora);

        Assert.Equal(MotivoIdentificacion.CredentialRevoked, resultado.Motivo);
    }

    [Fact(DisplayName = "AT52 · Código vencido en el límite exacto: ValidToUtc igual al instante da CREDENTIAL_EXPIRED")]
    public void Vencida_en_el_limite_exacto()
    {
        var credencial = new CredencialIdentificacion(null, Ahora, null);

        var resultado = ClasificadorCredencial.Clasificar(credencial, Ahora);

        Assert.Equal(MotivoIdentificacion.CredentialExpired, resultado.Motivo);
    }

    [Fact(DisplayName = "AT52 · ValidToUtc un milisegundo después del instante: vigente")]
    public void Vence_un_milisegundo_despues_es_vigente()
    {
        var credencial = new CredencialIdentificacion(null, Ahora.AddMilliseconds(1), null);

        var resultado = ClasificadorCredencial.Clasificar(credencial, Ahora);

        Assert.True(resultado.Vigente);
        Assert.Null(resultado.Motivo);
    }

    [Fact(DisplayName = "AT52 · Código todavía no vigente: ValidFromUtc un milisegundo después da IDENTITY_NOT_RESOLVED con causa NOT_YET_VALID")]
    public void Todavia_no_vigente()
    {
        var credencial = new CredencialIdentificacion(Ahora.AddMilliseconds(1), null, null);

        var resultado = ClasificadorCredencial.Clasificar(credencial, Ahora);

        Assert.Equal(MotivoIdentificacion.IdentityNotResolved, resultado.Motivo);
        Assert.Equal(CausaIdentificacion.NotYetValid, resultado.Causa);
    }

    [Fact(DisplayName = "AT52 · ValidFromUtc igual al instante: vigente")]
    public void Vigente_desde_el_instante_exacto()
    {
        var credencial = new CredencialIdentificacion(Ahora, null, null);

        Assert.True(ClasificadorCredencial.Clasificar(credencial, Ahora).Vigente);
    }

    [Fact(DisplayName = "RB25 · Extremos ausentes: una credencial sin vigencia ni revocación es vigente")]
    public void Extremos_ausentes_son_abiertos()
    {
        var resultado = ClasificadorCredencial.Clasificar(new CredencialIdentificacion(null, null, null), Ahora);

        Assert.True(resultado.Vigente);
        Assert.Null(resultado.Causa);
    }

    [Theory(DisplayName = "D2 · La precedencia de revocada y vencida coincide con la de EvaluadorHabilitacion sobre la misma credencial")]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void La_precedencia_coincide_con_la_de_la_habilitacion(bool revocada, bool vencida)
    {
        DateTimeOffset? hasta = vencida ? Ahora.AddDays(-1) : null;
        DateTimeOffset? revocadaUtc = revocada ? Ahora.AddDays(-2) : null;

        var identificacion = ClasificadorCredencial.Clasificar(new CredencialIdentificacion(null, hasta, revocadaUtc), Ahora);
        var habilitacion = EvaluadorHabilitacion.Evaluar(true, new CredencialSesion(hasta, revocadaUtc), Ahora, Guid.NewGuid(), [], []);

        var esperado = habilitacion.Motivo switch
        {
            MotivoDenegacion.CredentialRevoked => MotivoIdentificacion.CredentialRevoked,
            MotivoDenegacion.CredentialExpired => MotivoIdentificacion.CredentialExpired,
            var otro => throw new InvalidOperationException($"Motivo inesperado de la habilitación: {otro}"),
        };
        Assert.Equal(esperado, identificacion.Motivo);
    }
}
