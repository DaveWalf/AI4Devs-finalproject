namespace Intelektron.INComedor.Domain.Tests;

public class EvaluadorHabilitacionTests
{
    private static readonly Guid Persona = Guid.NewGuid();
    private static readonly Guid ServicioAlmuerzo = Guid.NewGuid();
    private static readonly Guid Ambito = Guid.NewGuid();
    private static readonly Guid CategoriaAlmuerzo = Guid.NewGuid();
    private static readonly Guid CategoriaRefrigerio = Guid.NewGuid();
    private static readonly FechaNegocio Hoy = new(new DateOnly(2026, 10, 19));
    private static readonly DateTimeOffset Ahora = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);

    private static ReglaCandidata Regla(
        int limite,
        int prioridad = 1,
        DateTimeOffset? desde = null,
        DateTimeOffset? hasta = null,
        Guid? categoria = null,
        Guid? ambito = null) =>
        new(Guid.NewGuid(), ServicioAlmuerzo, ambito ?? Ambito, categoria ?? CategoriaAlmuerzo, limite, prioridad, desde, hasta);

    private static ResultadoHabilitacion Almuerzo(
        IReadOnlyList<ReglaCandidata> reglas,
        bool personaActiva = true,
        CredencialSesion? credencial = null,
        DateTimeOffset? ahora = null) =>
        EvaluadorHabilitacion.Evaluar(personaActiva, credencial, ahora ?? Ahora, ServicioAlmuerzo, [new CargaMenu(CategoriaAlmuerzo, 1)], reglas);

    /// <summary>La decisión completa de una intención nueva: la habilitación y después el cupo, como en la confirmación.</summary>
    private static DecisionCupo Decidir(ResultadoHabilitacion habilitacion, IReadOnlyList<CargaMenu> cargas, params int[] usadoAlmuerzo)
    {
        Assert.Null(habilitacion.Motivo);
        var usos = usadoAlmuerzo.Length == 0
            ? new Dictionary<ClaveCupo, int>()
            : new Dictionary<ClaveCupo, int> { [new ClaveCupo(Persona, Ambito, CategoriaAlmuerzo, Hoy)] = usadoAlmuerzo[0] };
        return EvaluadorCupo.Evaluar(Persona, ServicioAlmuerzo, Hoy, cargas, habilitacion.Reglas, usos);
    }

    private static CargaMenu[] SoloAlmuerzo() => [new CargaMenu(CategoriaAlmuerzo, 1)];

    // Persona activa y credencial vigente antes que las reglas

    [Fact(DisplayName = "AT09 · Persona dada de baja con regla vigente y cupo libre: PERSON_INACTIVE")]
    public void Persona_inactiva_con_regla_vigente_se_deniega()
    {
        var resultado = Almuerzo([Regla(1)], personaActiva: false);

        Assert.Equal(MotivoDenegacion.PersonInactive, resultado.Motivo);
        Assert.Empty(resultado.Reglas);
    }

    [Fact(DisplayName = "AT53 · Credencial de la sesión revocada: CREDENTIAL_REVOKED")]
    public void Credencial_revocada_se_deniega()
    {
        var credencial = new CredencialSesion(Ahora.AddDays(30), Ahora.AddDays(-1));

        var resultado = Almuerzo([Regla(1)], credencial: credencial);

        Assert.Equal(MotivoDenegacion.CredentialRevoked, resultado.Motivo);
    }

    [Fact(DisplayName = "RB25 · Credencial revocada y vencida: gana CREDENTIAL_REVOKED")]
    public void Revocada_antes_que_vencida()
    {
        var credencial = new CredencialSesion(Ahora.AddDays(-2), Ahora.AddDays(-1));

        var resultado = Almuerzo([Regla(1)], credencial: credencial);

        Assert.Equal(MotivoDenegacion.CredentialRevoked, resultado.Motivo);
    }

    [Fact(DisplayName = "AT52 · Credencial con ValidToUtc igual al instante de la decisión: CREDENTIAL_EXPIRED")]
    public void Credencial_vencida_en_el_limite_exacto()
    {
        var resultado = Almuerzo([Regla(1)], credencial: new CredencialSesion(Ahora, null));

        Assert.Equal(MotivoDenegacion.CredentialExpired, resultado.Motivo);
    }

    [Fact(DisplayName = "AT52 · Credencial que vence un instante después de la decisión: sigue vigente")]
    public void Credencial_vigente_un_instante_antes_del_limite()
    {
        var resultado = Almuerzo([Regla(1)], credencial: new CredencialSesion(Ahora.AddTicks(1), null));

        Assert.Null(resultado.Motivo);
    }

    [Fact(DisplayName = "RB02 · Persona inactiva con una regla cero vigente: PERSON_INACTIVE, no RULE_DENIED")]
    public void Persona_inactiva_con_regla_cero_conserva_su_motivo()
    {
        var resultado = Almuerzo([Regla(0)], personaActiva: false);

        Assert.Equal(MotivoDenegacion.PersonInactive, resultado.Motivo);
    }

    [Fact(DisplayName = "AT01 · Sesión sin credencial: no hay nada que vencer ni revocar y se autoriza")]
    public void Sesion_sin_credencial_se_autoriza()
    {
        var decision = Decidir(Almuerzo([Regla(1)], credencial: null), SoloAlmuerzo());

        Assert.True(decision.Autorizada);
    }

    // Vigencia de las reglas en el instante de la decisión

    [Fact(DisplayName = "AT29 · Regla con fecha de fin ya alcanzada: SERVICE_NOT_ENABLED")]
    public void Regla_vencida_deniega_servicio_no_habilitado()
    {
        var resultado = Almuerzo([Regla(1, hasta: Ahora.AddSeconds(-1))]);

        Assert.Equal(MotivoDenegacion.ServiceNotEnabled, resultado.Motivo);
    }

    [Fact(DisplayName = "AT36 · Fin exacto de la vigencia: el intervalo es abierto a la derecha y la regla ya no aplica")]
    public void Fin_exacto_de_la_regla_ya_no_aplica()
    {
        var resultado = Almuerzo([Regla(1, hasta: Ahora)]);

        Assert.Equal(MotivoDenegacion.ServiceNotEnabled, resultado.Motivo);
    }

    [Fact(DisplayName = "AT36 · Inicio exacto de la vigencia: el intervalo es cerrado a la izquierda y la regla aplica")]
    public void Inicio_exacto_de_la_regla_aplica()
    {
        var decision = Decidir(Almuerzo([Regla(1, desde: Ahora)]), SoloAlmuerzo());

        Assert.True(decision.Autorizada);
    }

    [Fact(DisplayName = "AT36 · Regla que empieza un instante después de la decisión: todavía no aplica")]
    public void Regla_futura_no_aplica()
    {
        var resultado = Almuerzo([Regla(1, desde: Ahora.AddTicks(1))]);

        Assert.Equal(MotivoDenegacion.ServiceNotEnabled, resultado.Motivo);
    }

    [Fact(DisplayName = "HU01-H · Regla sin vencimiento 400 días después de la activación y sin red: se autoriza")]
    public void Regla_sin_vencimiento_no_caduca_por_tiempo_sin_red()
    {
        var activacion = Ahora.AddDays(-400);

        var decision = Decidir(Almuerzo([Regla(1, desde: activacion)], ahora: activacion.AddDays(400)), SoloAlmuerzo());

        Assert.True(decision.Autorizada);
    }

    [Fact(DisplayName = "AT17 · Regla sin horario a las 03:00 de la hora del puesto: se autoriza, sin restricción horaria ajena")]
    public void Regla_sin_horario_autoriza_de_madrugada()
    {
        var tresDeLaMadrugadaEnElPuesto = new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero);

        var decision = Decidir(Almuerzo([Regla(1)], ahora: tresDeLaMadrugadaEnElPuesto), SoloAlmuerzo());

        Assert.True(decision.Autorizada);
    }

    // La regla cero prevalece sobre cualquier permiso

    [Fact(DisplayName = "AT36 · Regla cero vigente frente a un permiso de mayor prioridad: RULE_DENIED")]
    public void Regla_cero_vigente_gana_al_permiso_de_mayor_prioridad()
    {
        var resultado = Almuerzo([
            Regla(1, prioridad: 10),
            Regla(0, prioridad: 1, desde: Ahora.AddDays(-1), hasta: Ahora.AddDays(1)),
        ]);

        Assert.Equal(MotivoDenegacion.RuleDenied, resultado.Motivo);
        Assert.Empty(resultado.Reglas);
    }

    [Fact(DisplayName = "AT36 · En el fin exacto de la regla cero se evalúa el permiso restante y se autoriza con su límite")]
    public void Regla_cero_en_su_fin_exacto_deja_pasar_al_permiso()
    {
        var resultado = Almuerzo([
            Regla(2, prioridad: 10),
            Regla(0, prioridad: 1, desde: Ahora.AddDays(-1), hasta: Ahora),
        ]);

        Assert.Null(resultado.Motivo);
        Assert.Equal(2, Assert.Single(resultado.Reglas).Limite);
    }

    [Fact(DisplayName = "AT36 · Regla cero ya vencida y sin otro permiso: SERVICE_NOT_ENABLED, no RULE_DENIED")]
    public void Regla_cero_vencida_no_participa()
    {
        var resultado = Almuerzo([Regla(0, hasta: Ahora.AddSeconds(-1))]);

        Assert.Equal(MotivoDenegacion.ServiceNotEnabled, resultado.Motivo);
    }

    [Fact(DisplayName = "AT36 · Una regla cero de otro servicio no deniega este servicio")]
    public void Regla_cero_de_otro_servicio_no_participa()
    {
        var otroServicio = Regla(0) with { TipoServicioId = Guid.NewGuid() };

        var decision = Decidir(Almuerzo([Regla(1), otroServicio]), SoloAlmuerzo());

        Assert.True(decision.Autorizada);
    }

    [Fact(DisplayName = "RB02 · Una regla cero en una de dos categorías deniega la opción entera con RULE_DENIED")]
    public void Regla_cero_en_una_de_dos_categorias_deniega_la_opcion()
    {
        var resultado = EvaluadorHabilitacion.Evaluar(
            true, null, Ahora, ServicioAlmuerzo,
            [new CargaMenu(CategoriaAlmuerzo, 1), new CargaMenu(CategoriaRefrigerio, 1)],
            [Regla(1), Regla(0, categoria: CategoriaRefrigerio)]);

        Assert.Equal(MotivoDenegacion.RuleDenied, resultado.Motivo);
        Assert.Empty(resultado.Reglas);
    }

    [Fact(DisplayName = "RB02 · Regla cero en una categoría y otra sin permiso: la regla cero decide RULE_DENIED")]
    public void Regla_cero_gana_aunque_otra_categoria_no_tenga_permiso()
    {
        var resultado = EvaluadorHabilitacion.Evaluar(
            true, null, Ahora, ServicioAlmuerzo,
            [new CargaMenu(CategoriaAlmuerzo, 1), new CargaMenu(CategoriaRefrigerio, 1)],
            [Regla(0, categoria: CategoriaRefrigerio)]);

        Assert.Equal(MotivoDenegacion.RuleDenied, resultado.Motivo);
    }

    // Entre reglas positivas decide la de mayor prioridad

    [Fact(DisplayName = "RB02 · Gana la de mayor prioridad: con el límite 2 y un uso, se autoriza")]
    public void Gana_la_regla_de_mayor_prioridad()
    {
        var habilitacion = Almuerzo([Regla(1, prioridad: 1), Regla(2, prioridad: 2)]);

        var decision = Decidir(habilitacion, SoloAlmuerzo(), 1);

        Assert.Equal(2, Assert.Single(habilitacion.Reglas).Limite);
        Assert.True(decision.Autorizada);
    }

    [Fact(DisplayName = "RB02 · La de mayor prioridad que no está vigente no gana: decide la vigente de menor prioridad")]
    public void La_mayor_prioridad_fuera_de_vigencia_no_gana()
    {
        var habilitacion = Almuerzo([Regla(1, prioridad: 1), Regla(5, prioridad: 9, hasta: Ahora)]);

        Assert.Equal(1, Assert.Single(habilitacion.Reglas).Limite);
    }

    [Fact(DisplayName = "RB02 · Empate contradictorio en la mayor prioridad: no decide y lanza el error de configuración")]
    public void Empate_contradictorio_lanza_excepcion()
    {
        var excepcion = Assert.Throws<ReglasHabilitacionContradictoriasException>(
            () => Almuerzo([Regla(1, prioridad: 3), Regla(2, prioridad: 3)]));

        Assert.Contains(CategoriaAlmuerzo.ToString(), excepcion.Message);
        Assert.Contains("3", excepcion.Message);
    }

    [Fact(DisplayName = "RB02 · Empate en la mayor prioridad con distinto ámbito pero igual límite: contradictorio")]
    public void Empate_con_distinto_ambito_es_contradictorio()
    {
        Assert.Throws<ReglasHabilitacionContradictoriasException>(
            () => Almuerzo([Regla(1, prioridad: 3), Regla(1, prioridad: 3, ambito: Guid.NewGuid())]));
    }

    [Fact(DisplayName = "RB02 · Empate sin contradicción: mismo límite y mismo ámbito se autoriza con límite 1")]
    public void Empate_sin_contradiccion_se_autoriza()
    {
        var habilitacion = Almuerzo([Regla(1, prioridad: 3), Regla(1, prioridad: 3)]);

        var decision = Decidir(habilitacion, SoloAlmuerzo());

        Assert.Single(habilitacion.Reglas);
        Assert.True(decision.Autorizada);
    }

    [Fact(DisplayName = "RB02 · Un empate de prioridad más baja no cuenta si la mayor es única")]
    public void Empate_en_prioridad_menor_no_es_contradiccion()
    {
        var habilitacion = Almuerzo([Regla(1, prioridad: 1), Regla(2, prioridad: 1), Regla(3, prioridad: 5)]);

        Assert.Equal(3, Assert.Single(habilitacion.Reglas).Limite);
    }
}
