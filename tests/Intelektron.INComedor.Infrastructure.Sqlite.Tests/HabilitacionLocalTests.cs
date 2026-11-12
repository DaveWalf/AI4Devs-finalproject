using Intelektron.INComedor.Domain;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// RB02 sobre SQLite real: los escenarios de la capability habilitacion-local contra UnidadConfirmacion.
/// Siembran sus propias reglas, bajas y credenciales con Sql.Ejecutar, sin cambiar FixtureSintetico (D9).
/// </summary>
public class HabilitacionLocalTests : ConfirmacionTestBase
{
    private void SembrarRegla(
        PersonaFixture persona, Guid servicio, Guid categoria, int limite, int prioridad,
        DateTimeOffset? desde = null, DateTimeOffset? hasta = null, Guid? ambito = null)
    {
        using var conexion = Abrir();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorReglasHabilitacion VALUES ($vc, $reg, $per, NULL, $srv, $amb, $cat, $limite, $prioridad, $desde, $hasta)",
            ("$vc", Fixture.SnapshotId), ("$reg", Guid.NewGuid()), ("$per", persona.Id), ("$srv", servicio),
            ("$amb", ambito ?? Fixture.Ambito), ("$cat", categoria), ("$limite", limite), ("$prioridad", prioridad),
            ("$desde", desde is null ? null : Iso(desde.Value)), ("$hasta", hasta is null ? null : Iso(hasta.Value)));
    }

    /// <summary>Cambia la vigencia de la regla de ALMUERZO de la persona habilitada del fixture.</summary>
    private void FijarVigenciaDeLaReglaDeAlmuerzo(DateTimeOffset? desde, DateTimeOffset? hasta)
    {
        using var conexion = Abrir();
        Sql.Ejecutar(conexion,
            """
            UPDATE tbComedorReglasHabilitacion SET comregFechaVigenciaDesdeUtc = $desde, comregFechaVigenciaHastaUtc = $hasta
            WHERE comperCodigo = $per AND comsrvCodigo = $srv AND comcatCodigo = $cat
            """,
            ("$per", Fixture.Habilitada.Id), ("$srv", Fixture.ServicioAlmuerzo), ("$cat", Fixture.CategoriaAlmuerzo),
            ("$desde", desde is null ? null : Iso(desde.Value)), ("$hasta", hasta is null ? null : Iso(hasta.Value)));
    }

    private void DarDeBaja(PersonaFixture persona)
    {
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "UPDATE tbComedorPersonas SET comperActivo = 0 WHERE comperCodigo = $per", ("$per", persona.Id));
    }

    private void FijarCredencial(PersonaFixture persona, DateTimeOffset? venceUtc, DateTimeOffset? revocadaUtc)
    {
        using var conexion = Abrir();
        Sql.Ejecutar(conexion,
            "UPDATE tbComedorCredencialesPersonas SET comcreFechaVigenciaHastaUtc = $vence, comcreFechaRevocacionUtc = $revocada WHERE comcreCodigo = $cre",
            ("$cre", persona.CredencialId), ("$vence", venceUtc is null ? null : Iso(venceUtc.Value)),
            ("$revocada", revocadaUtc is null ? null : Iso(revocadaUtc.Value)));
    }

    private void AssertDenegadaSinConsumoNuevo(ResultadoConfirmacion resultado, string motivo, long consumosPrevios = 0)
    {
        Assert.Equal(TipoResultado.Denegada, resultado.Tipo);
        Assert.Equal(motivo, resultado.ReasonCode);
        Assert.Null(resultado.ConsumptionId);
        using var conexion = Abrir();
        Assert.Equal(consumosPrevios, Sql.Contar(conexion, "tbComedorConsumos"));
        Assert.Equal(consumosPrevios, Sql.Contar(conexion, "tbComedorUsosCupo"));
    }

    // Persona activa y credencial vigente antes que las reglas

    [Fact(DisplayName = "AT09 · Persona dada de baja con regla vigente y cupo libre: PERSON_INACTIVE, y su consumo anterior sigue registrado")]
    public void Persona_dada_de_baja_se_deniega_y_conserva_su_consumo_anterior()
    {
        var anterior = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.Refrigerio));
        DarDeBaja(Fixture.Habilitada);

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        AssertDenegadaSinConsumoNuevo(resultado, "PERSON_INACTIVE", consumosPrevios: 1);
        using var conexion = Abrir();
        Assert.Equal(anterior.ConsumptionId, Sql.EscalarGuid(conexion, "SELECT comconCodigo FROM tbComedorConsumos"));
    }

    [Fact(DisplayName = "AT53 · Credencial de la sesión revocada: CREDENTIAL_REVOKED y el replay de la intención anterior devuelve su resultado original")]
    public void Credencial_revocada_se_deniega_y_el_replay_no_cambia()
    {
        var anteriorSolicitud = Solicitud(Fixture.Habilitada, Fixture.Refrigerio);
        var anterior = Unidad.Confirmar(anteriorSolicitud);
        FijarCredencial(Fixture.Habilitada, venceUtc: null, revocadaUtc: Reloj.Ahora.AddMinutes(-5));

        var nueva = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));
        var repetida = Unidad.Confirmar(anteriorSolicitud);

        AssertDenegadaSinConsumoNuevo(nueva, "CREDENTIAL_REVOKED", consumosPrevios: 1);
        Assert.Equal(TipoResultado.Autorizada, anterior.Tipo);
        Assert.Equal(anterior, repetida);
    }

    [Fact(DisplayName = "AT52 · Credencial con ValidToUtc igual al instante de la decisión y sesión abierta: CREDENTIAL_EXPIRED")]
    public void Credencial_vencida_en_el_instante_exacto_se_deniega()
    {
        var sesion = NuevaSesion(Fixture.Habilitada);
        FijarCredencial(Fixture.Habilitada, venceUtc: Reloj.Ahora, revocadaUtc: null);

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun, sesion));

        AssertDenegadaSinConsumoNuevo(resultado, "CREDENTIAL_EXPIRED");
    }

    [Fact(DisplayName = "RB02 · Persona inactiva con una regla cero vigente: PERSON_INACTIVE, no RULE_DENIED")]
    public void Persona_inactiva_con_regla_cero_conserva_su_motivo()
    {
        SembrarRegla(Fixture.Habilitada, Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, limite: 0, prioridad: 1);
        DarDeBaja(Fixture.Habilitada);

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        AssertDenegadaSinConsumoNuevo(resultado, "PERSON_INACTIVE");
    }

    [Fact(DisplayName = "AT01 · Sesión sin credencial: la persona activa con regla vigente y cupo libre se autoriza")]
    public void Sesion_sin_credencial_se_autoriza()
    {
        SembrarRegla(Fixture.SinDerecho, Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, limite: 1, prioridad: 1);
        Assert.Null(Fixture.SinDerecho.CredencialId);

        var resultado = Unidad.Confirmar(Solicitud(Fixture.SinDerecho, Fixture.AlmuerzoComun));

        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
    }

    // Vigencia de las reglas en el instante de la decisión

    [Fact(DisplayName = "AT29 · Regla con fecha de fin ya alcanzada y sin red: SERVICE_NOT_ENABLED")]
    public void Regla_vencida_se_deniega_como_regla_de_negocio()
    {
        FijarVigenciaDeLaReglaDeAlmuerzo(desde: null, hasta: Reloj.Ahora.AddSeconds(-1));

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        AssertDenegadaSinConsumoNuevo(resultado, "SERVICE_NOT_ENABLED");
    }

    [Fact(DisplayName = "AT36 · Inicio exacto de la vigencia: la regla aplica y se autoriza")]
    public void Regla_que_empieza_en_el_instante_exacto_se_autoriza()
    {
        FijarVigenciaDeLaReglaDeAlmuerzo(desde: Reloj.Ahora, hasta: null);

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
    }

    [Fact(DisplayName = "AT36 · Fin exacto de la vigencia: la regla ya no aplica")]
    public void Regla_que_termina_en_el_instante_exacto_ya_no_aplica()
    {
        FijarVigenciaDeLaReglaDeAlmuerzo(desde: null, hasta: Reloj.Ahora);

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        AssertDenegadaSinConsumoNuevo(resultado, "SERVICE_NOT_ENABLED");
    }

    [Fact(DisplayName = "HU01-H · Regla sin vencimiento 400 días después de la activación y sin red: AUTHORIZED")]
    public void Regla_sin_vencimiento_no_caduca_por_tiempo_sin_red()
    {
        FijarVigenciaDeLaReglaDeAlmuerzo(desde: Reloj.Ahora, hasta: null);
        Reloj.Ahora = Reloj.Ahora.AddDays(400);

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
    }

    [Fact(DisplayName = "AT17 · Regla sin horario a las 03:00 de la hora del puesto: AUTHORIZED, sin restricción horaria ajena")]
    public void Regla_sin_horario_se_autoriza_de_madrugada()
    {
        Reloj.Ahora = DateTimeOffset.Parse("2026-10-19T06:00:00Z");

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
    }

    // La regla cero prevalece sobre cualquier permiso

    [Fact(DisplayName = "AT36 · Regla cero vigente frente a un permiso de mayor prioridad: RULE_DENIED, no por baja ni por cupo")]
    public void Regla_cero_vigente_gana_al_permiso_de_mayor_prioridad()
    {
        SembrarRegla(Fixture.Habilitada, Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, limite: 1, prioridad: 10);
        SembrarRegla(Fixture.Habilitada, Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, limite: 0, prioridad: 1,
            desde: Reloj.Ahora.AddDays(-1), hasta: Reloj.Ahora.AddDays(1));

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        AssertDenegadaSinConsumoNuevo(resultado, "RULE_DENIED");
    }

    [Fact(DisplayName = "AT36 · En el fin exacto de la regla cero se evalúa el permiso: AUTHORIZED con el límite 2 del permiso")]
    public void Regla_cero_en_su_fin_exacto_deja_pasar_al_permiso()
    {
        FijarVigenciaDeLaReglaDeAlmuerzo(desde: null, hasta: Reloj.Ahora.AddSeconds(-1));
        SembrarRegla(Fixture.Habilitada, Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, limite: 2, prioridad: 10);
        SembrarRegla(Fixture.Habilitada, Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, limite: 0, prioridad: 1,
            desde: Reloj.Ahora.AddDays(-1), hasta: Reloj.Ahora);

        var primera = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));
        var segunda = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoOpcional));
        var tercera = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        Assert.Equal(TipoResultado.Autorizada, primera.Tipo);
        Assert.Equal(TipoResultado.Autorizada, segunda.Tipo);
        Assert.Equal("LOCAL_LIMIT_REACHED", tercera.ReasonCode);
    }

    [Fact(DisplayName = "RB02 · Una regla cero en una de dos categorías deniega la opción entera y no carga ninguna")]
    public void Regla_cero_en_una_de_dos_categorias_deniega_la_opcion_entera()
    {
        using (var conexion = Abrir())
        {
            Sql.Ejecutar(conexion,
                "UPDATE tbComedorReglasHabilitacion SET comregLimite = 0 WHERE comperCodigo = $per AND comsrvCodigo = $srv AND comcatCodigo = $cat",
                ("$per", Fixture.Habilitada.Id), ("$srv", Fixture.ServicioAlmuerzo), ("$cat", Fixture.CategoriaRefrigerio));
        }

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.ComboAlmuerzoYRefrigerio));

        AssertDenegadaSinConsumoNuevo(resultado, "RULE_DENIED");
        using var verificacion = Abrir();
        Assert.Equal(0L, Sql.Contar(verificacion, "tbComedorCargosCupoConocidos"));
    }

    // Entre reglas positivas decide la de mayor prioridad

    [Fact(DisplayName = "RB02 · Gana la de mayor prioridad: con el límite 2 y un consumo previo en el día se autoriza")]
    public void Gana_la_regla_de_mayor_prioridad()
    {
        SembrarRegla(Fixture.Habilitada, Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, limite: 2, prioridad: 2);

        var primera = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));
        var segunda = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoOpcional));
        var tercera = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        Assert.Equal(TipoResultado.Autorizada, primera.Tipo);
        Assert.Equal(TipoResultado.Autorizada, segunda.Tipo);
        Assert.Equal("LOCAL_LIMIT_REACHED", tercera.ReasonCode);
    }

    [Fact(DisplayName = "RB02 · Empate contradictorio en la mayor prioridad: error de configuración y la base queda igual")]
    public void Empate_contradictorio_no_escribe_nada()
    {
        SembrarRegla(Fixture.Habilitada, Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, limite: 2, prioridad: 1);
        var sesion = NuevaSesion(Fixture.Habilitada);
        using var conexion = Abrir();
        var antes = Sql.ConteoDeEscritura(conexion);

        Assert.Throws<ReglasHabilitacionContradictoriasException>(
            () => Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun, sesion)));

        Assert.Equal(antes, Sql.ConteoDeEscritura(conexion));
        Assert.Equal(1L, Sql.Escalar<long>(conexion,
            "SELECT COUNT(*) FROM tbComedorSesionesIdentidad WHERE comsidCodigo = $sid AND comicCodigoConsumidora IS NULL",
            ("$sid", sesion)));
    }

    [Fact(DisplayName = "RB02 · Empate sin contradicción, mismo límite 1 y mismo ámbito: AUTHORIZED con límite 1")]
    public void Empate_sin_contradiccion_se_autoriza_con_el_limite_comun()
    {
        SembrarRegla(Fixture.Habilitada, Fixture.ServicioAlmuerzo, Fixture.CategoriaAlmuerzo, limite: 1, prioridad: 1);

        var primera = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));
        var segunda = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoOpcional));

        Assert.Equal(TipoResultado.Autorizada, primera.Tipo);
        Assert.Equal("LOCAL_LIMIT_REACHED", segunda.ReasonCode);
    }
}
