using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// La franja horaria de la opción en la oferta y en la confirmación, sobre SQLite real (D4 a D6 de
/// administrar-personas-y-opciones). El puesto está en America/Argentina/Buenos_Aires (UTC-3): las 11:00 locales son las
/// 14:00 UTC. Las franjas se insertan por SQL en la versión del fixture.
/// </summary>
public class FranjaEnLaEvaluacionTests : ConfirmacionTestBase
{
    private const string FueraDeHorario = "OUTSIDE_SERVICE_HOURS";

    private ConsultaDisponibilidad Consulta => new(Base, Reloj);

    private IEnumerable<OpcionFixture> OpcionesDeAlmuerzo => Fixture.Opciones.Take(9);

    private void CargarFranja(OpcionFixture opcion, string inicio, string fin)
    {
        using var conexion = Abrir();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorFranjasOpcionesMenu (comvcCodigo, commenCodigo, comfraCodigo, comfraHoraInicio, comfraHoraFin) VALUES ($vc, $men, $fra, $ini, $fin)",
            ("$vc", Fixture.SnapshotId), ("$men", opcion.Id), ("$fra", Guid.NewGuid()), ("$ini", inicio), ("$fin", fin));
    }

    /// <summary>Lleva el reloj a la hora local del puesto del 19/10/2026 (UTC-3).</summary>
    private void HoraLocal(string hora) =>
        Reloj.Ahora = DateTimeOffset.Parse($"2026-10-19T{hora}:00-03:00");

    private PersonaFixture PersonaConReglaCero()
    {
        var id = Guid.NewGuid();
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Persona con regla cero', 1, 1)",
            ("$vc", Fixture.SnapshotId), ("$per", id));
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorReglasHabilitacion VALUES ($vc, $reg, $per, NULL, $srv, $amb, $cat, 0, 1, NULL, NULL)",
            ("$vc", Fixture.SnapshotId), ("$reg", Guid.NewGuid()), ("$per", id), ("$srv", Fixture.ServicioAlmuerzo),
            ("$amb", Fixture.Ambito), ("$cat", Fixture.CategoriaAlmuerzo));
        return new PersonaFixture(id, null);
    }

    private long Consumos()
    {
        using var conexion = Abrir();
        return Sql.Contar(conexion, "tbComedorConsumos");
    }

    private JsonElement UltimoRegistro(string tipo)
    {
        using var conexion = Abrir();
        var contexto = Sql.Escalar<string>(conexion,
            "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = $tipo ORDER BY combitSecuencia DESC LIMIT 1",
            ("$tipo", tipo));
        return JsonDocument.Parse(contexto).RootElement.Clone();
    }

    private JsonElement EventoDeSalida(Guid intentId)
    {
        using var conexion = Abrir();
        var contenido = Sql.Escalar<string>(conexion,
            "SELECT comevsContenido FROM tbComedorEventosSalida WHERE comicCodigo = $ic", ("$ic", intentId));
        return JsonDocument.Parse(contenido).RootElement.Clone();
    }

    private void AfirmarFueraDeHorario(Guid sesion, OfertaDePersona oferta, SolicitudConfirmacion solicitud, ResultadoConfirmacion confirmacion)
    {
        var retenida = Assert.Single(oferta.Retenidas, r => r.Id == solicitud.MenuItemId);
        Assert.Equal("SERVICE_NOT_ENABLED", retenida.Motivo);
        Assert.Equal(FueraDeHorario, retenida.Causa);
        Assert.DoesNotContain(oferta.Ofrecidas, o => o.Id == solicitud.MenuItemId);

        Assert.Equal(TipoResultado.Denegada, confirmacion.Tipo);
        Assert.Equal("SERVICE_NOT_ENABLED", confirmacion.ReasonCode);
        Assert.Equal(FueraDeHorario, confirmacion.ReasonCause);

        var ofertada = UltimoRegistro("OPTIONS_OFFERED");
        Assert.Equal(sesion, ofertada.GetProperty("IdentitySessionId").GetGuid());
        var retenidaRegistrada = Assert.Single(ofertada.GetProperty("Withheld").EnumerateArray(),
            r => r.GetProperty("MenuItemId").GetGuid() == solicitud.MenuItemId);
        Assert.Equal("SERVICE_NOT_ENABLED", retenidaRegistrada.GetProperty("ReasonCode").GetString());
        Assert.Equal(FueraDeHorario, retenidaRegistrada.GetProperty("Cause").GetString());

        var denegada = UltimoRegistro("CONSUMPTION_DENIED");
        Assert.Equal(solicitud.IntentId, denegada.GetProperty("IntentId").GetGuid());
        Assert.Equal("SERVICE_NOT_ENABLED", denegada.GetProperty("ReasonCode").GetString());
        Assert.Equal(FueraDeHorario, denegada.GetProperty("Cause").GetString());

        var evento = EventoDeSalida(solicitud.IntentId);
        Assert.Equal("SERVICE_NOT_ENABLED", evento.GetProperty("ReasonCode").GetString());
        Assert.DoesNotContain(FueraDeHorario, evento.GetRawText());
    }

    [Theory(DisplayName = "habilitacion-local · Antes, dentro y en el fin de la franja de 11:00 a 14:00: a las 11:00 se ofrece y se autoriza; a las 10:59 y a las 14:00 se retiene y se deniega con SERVICE_NOT_ENABLED y OUTSIDE_SERVICE_HOURS, sin consumo")]
    [InlineData("10:59", false)]
    [InlineData("11:00", true)]
    [InlineData("14:00", false)]
    public void Antes_dentro_y_en_el_fin_de_la_franja(string hora, bool autorizada)
    {
        CargarFranja(Fixture.AlmuerzoComun, "11:00", "14:00");
        HoraLocal(hora);
        var persona = Fixture.Habilitada;
        var sesion = NuevaSesion(persona);

        var oferta = Consulta.Ofrecer(sesion, persona.Id);
        var solicitud = Solicitud(persona, Fixture.AlmuerzoComun, sesion);
        var confirmacion = Unidad.Confirmar(solicitud);

        if (autorizada)
        {
            Assert.Contains(oferta.Ofrecidas, o => o.Id == Fixture.AlmuerzoComun.Id);
            Assert.Equal(TipoResultado.Autorizada, confirmacion.Tipo);
            Assert.Null(confirmacion.ReasonCause);
            Assert.Equal(1L, Consumos());
        }
        else
        {
            AfirmarFueraDeHorario(sesion, oferta, solicitud, confirmacion);
            Assert.Equal(0L, Consumos());
        }
    }

    [Fact(DisplayName = "habilitacion-local · La franja cierra entre la oferta y la confirmación: ofrecida a las 13:59, confirmada a las 14:00, DENIED con SERVICE_NOT_ENABLED y OUTSIDE_SERVICE_HOURS, sin consumo")]
    public void La_franja_cierra_entre_la_oferta_y_la_confirmacion()
    {
        CargarFranja(Fixture.AlmuerzoComun, "11:00", "14:00");
        HoraLocal("13:59");
        var persona = Fixture.Habilitada;
        var sesion = NuevaSesion(persona);
        var oferta = Consulta.Ofrecer(sesion, persona.Id);
        Assert.Contains(oferta.Ofrecidas, o => o.Id == Fixture.AlmuerzoComun.Id);

        HoraLocal("14:00");
        var confirmacion = Unidad.Confirmar(Solicitud(persona, Fixture.AlmuerzoComun, sesion));

        Assert.Equal(TipoResultado.Denegada, confirmacion.Tipo);
        Assert.Equal("SERVICE_NOT_ENABLED", confirmacion.ReasonCode);
        Assert.Equal(FueraDeHorario, confirmacion.ReasonCause);
        Assert.Equal(0L, Consumos());
    }

    [Fact(DisplayName = "habilitacion-local · Una regla cero decide antes que la franja: RULE_DENIED, sin causa de horario")]
    public void Regla_cero_antes_que_la_franja()
    {
        CargarFranja(Fixture.AlmuerzoComun, "11:00", "14:00");
        HoraLocal("16:00");
        var persona = PersonaConReglaCero();
        var sesion = NuevaSesion(persona);

        var oferta = Consulta.Ofrecer(sesion, persona.Id);
        var confirmacion = Unidad.Confirmar(Solicitud(persona, Fixture.AlmuerzoComun, sesion));

        Assert.Equal(TipoResultado.Denegada, confirmacion.Tipo);
        Assert.Equal("RULE_DENIED", confirmacion.ReasonCode);
        Assert.Null(confirmacion.ReasonCause);
        var retenida = Assert.Single(oferta.Retenidas, r => r.Id == Fixture.AlmuerzoComun.Id);
        Assert.Equal("RULE_DENIED", retenida.Motivo);
        Assert.Null(retenida.Causa);
        Assert.Equal(JsonValueKind.Null, UltimoRegistro("CONSUMPTION_DENIED").GetProperty("Cause").ValueKind);
    }

    [Fact(DisplayName = "habilitacion-local · El cupo agotado sigue a la vista fuera de horario: con el almuerzo del día consumido y ALMUERZO fuera de su franja, la oferta tiene ALMUERZO 1 de 1")]
    public void El_cupo_agotado_sigue_a_la_vista_fuera_de_horario()
    {
        foreach (var opcion in OpcionesDeAlmuerzo)
        {
            CargarFranja(opcion, "11:00", "14:00");
        }

        HoraLocal("12:00");
        var persona = Fixture.Habilitada;
        Assert.Equal(TipoResultado.Autorizada, Unidad.Confirmar(Solicitud(persona, Fixture.AlmuerzoComun)).Tipo);
        HoraLocal("16:00");
        var sesion = NuevaSesion(persona);

        var oferta = Consulta.Ofrecer(sesion, persona.Id);

        var agotada = Assert.Single(oferta.Agotadas);
        Assert.Equal(("ALMUERZO", 1, 1), (agotada.Nombre, agotada.Usado, agotada.Limite));
        Assert.All(oferta.Retenidas, r => Assert.Equal(FueraDeHorario, r.Causa));
        Assert.Equal(9, oferta.Retenidas.Count);
    }

    [Fact(DisplayName = "habilitacion-local · Fuera de horario, la oferta y la confirmación coinciden: el mismo motivo y la misma causa en el mismo instante")]
    public void Fuera_de_horario_la_oferta_y_la_confirmacion_coinciden()
    {
        CargarFranja(Fixture.AlmuerzoComun, "11:00", "14:00");
        HoraLocal("16:00");
        var persona = Fixture.Habilitada;
        var sesion = NuevaSesion(persona);

        var oferta = Consulta.Ofrecer(sesion, persona.Id);
        var solicitud = Solicitud(persona, Fixture.AlmuerzoComun, sesion);
        var confirmacion = Unidad.Confirmar(solicitud);

        var retenida = Assert.Single(oferta.Retenidas, r => r.Id == Fixture.AlmuerzoComun.Id);
        Assert.Equal((confirmacion.ReasonCode, confirmacion.ReasonCause), (retenida.Motivo, retenida.Causa));
        AfirmarFueraDeHorario(sesion, oferta, solicitud, confirmacion);
        Assert.Equal(0L, Consumos());
    }
}
