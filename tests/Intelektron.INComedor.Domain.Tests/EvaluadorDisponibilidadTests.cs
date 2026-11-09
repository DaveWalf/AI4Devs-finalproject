namespace Intelektron.INComedor.Domain.Tests;

/// <summary>
/// La evaluación única que ofrece y confirma (D1 de ofrecer-solo-opciones-disponibles): persona activa, credencial, reglas
/// vigentes, regla cero, prioridad y cupo, en el orden de RB02 y RB03.
/// </summary>
public class EvaluadorDisponibilidadTests
{
    private static readonly Guid Persona = Guid.NewGuid();
    private static readonly Guid ServicioAlmuerzo = Guid.NewGuid();
    private static readonly Guid Ambito = Guid.NewGuid();
    private static readonly Guid CategoriaAlmuerzo = Guid.NewGuid();
    private static readonly FechaNegocio Hoy = new(new DateOnly(2026, 11, 4));
    private static readonly DateTimeOffset Ahora = new(2026, 11, 4, 15, 0, 0, TimeSpan.Zero);

    private static ReglaCandidata Regla(int limite, int prioridad = 1) =>
        new(Guid.NewGuid(), ServicioAlmuerzo, Ambito, CategoriaAlmuerzo, limite, prioridad, null, null);

    private static ResultadoDisponibilidad Almuerzo(
        IReadOnlyList<ReglaCandidata> reglas,
        int usado = 0,
        bool personaActiva = true,
        CredencialSesion? credencial = null)
    {
        var usos = usado == 0
            ? new Dictionary<ClaveCupo, int>()
            : new Dictionary<ClaveCupo, int> { [new ClaveCupo(Persona, Ambito, CategoriaAlmuerzo, Hoy)] = usado };
        return EvaluadorDisponibilidad.Evaluar(
            personaActiva, credencial, Ahora, ServicioAlmuerzo, [new CargaMenu(CategoriaAlmuerzo, 1)], reglas, Persona, Hoy, usos);
    }

    [Fact(DisplayName = "RB02 · Persona activa, regla vigente y cupo libre: la opción se autoriza con la regla elegida")]
    public void Autoriza_con_cupo_libre()
    {
        var regla = Regla(1);

        var resultado = Almuerzo([regla]);

        Assert.True(resultado.Decision.Autorizada);
        Assert.Null(resultado.Decision.Motivo);
        Assert.Equal(regla.ReglaId, Assert.Single(resultado.Reglas).ReglaId);
        Assert.Equal(new CargaAplicada(new ClaveCupo(Persona, Ambito, CategoriaAlmuerzo, Hoy), 1), Assert.Single(resultado.Decision.Cargas));
    }

    [Fact(DisplayName = "RB03 · Uso del día igual al límite: LOCAL_LIMIT_REACHED, y la regla aplicable queda a la vista para nombrar la categoría agotada")]
    public void Uso_igual_al_limite_agota_el_cupo()
    {
        var regla = Regla(1);

        var resultado = Almuerzo([regla], usado: 1);

        Assert.False(resultado.Decision.Autorizada);
        Assert.Equal(MotivoDenegacion.LocalLimitReached, resultado.Decision.Motivo);
        Assert.Equal(regla.ReglaId, Assert.Single(resultado.Reglas).ReglaId);
    }

    [Fact(DisplayName = "RB03 · Un uso menor que el límite todavía autoriza")]
    public void Uso_menor_que_el_limite_autoriza()
    {
        var resultado = Almuerzo([Regla(2)], usado: 1);

        Assert.True(resultado.Decision.Autorizada);
    }

    [Fact(DisplayName = "RB02 · Persona inactiva antes que la regla cero: PERSON_INACTIVE, no RULE_DENIED")]
    public void Persona_inactiva_antes_que_regla_cero()
    {
        var resultado = Almuerzo([Regla(0)], personaActiva: false);

        Assert.Equal(MotivoDenegacion.PersonInactive, resultado.Decision.Motivo);
        Assert.Empty(resultado.Reglas);
    }

    [Fact(DisplayName = "RB02 · Regla cero antes que el cupo: RULE_DENIED aunque el uso alcance el límite de otra regla")]
    public void Regla_cero_antes_que_el_cupo()
    {
        var resultado = Almuerzo([Regla(0), Regla(1)], usado: 1);

        Assert.Equal(MotivoDenegacion.RuleDenied, resultado.Decision.Motivo);
        Assert.Empty(resultado.Reglas);
    }

    [Fact(DisplayName = "RB02 · Sin ninguna regla: SERVICE_NOT_ENABLED")]
    public void Sin_reglas_no_esta_habilitado()
    {
        var resultado = Almuerzo([]);

        Assert.Equal(MotivoDenegacion.ServiceNotEnabled, resultado.Decision.Motivo);
        Assert.Empty(resultado.Reglas);
    }

    [Fact(DisplayName = "RB25 · Credencial revocada: CREDENTIAL_REVOKED antes de mirar reglas y cupo")]
    public void Credencial_revocada_antes_que_las_reglas()
    {
        var credencial = new CredencialSesion(Ahora.AddDays(30), Ahora.AddDays(-1));

        var resultado = Almuerzo([Regla(1)], usado: 1, credencial: credencial);

        Assert.Equal(MotivoDenegacion.CredentialRevoked, resultado.Decision.Motivo);
    }
}
