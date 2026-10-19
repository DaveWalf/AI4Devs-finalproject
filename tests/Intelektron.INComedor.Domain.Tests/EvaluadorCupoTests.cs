namespace Intelektron.INComedor.Domain.Tests;

public class EvaluadorCupoTests
{
    private static readonly Guid Persona = Guid.NewGuid();
    private static readonly Guid ServicioAlmuerzo = Guid.NewGuid();
    private static readonly Guid ServicioRefrigerio = Guid.NewGuid();
    private static readonly Guid Ambito = Guid.NewGuid();
    private static readonly Guid CategoriaAlmuerzo = Guid.NewGuid();
    private static readonly Guid CategoriaRefrigerio = Guid.NewGuid();
    private static readonly FechaNegocio Hoy = new(new DateOnly(2026, 10, 19));

    private static ReglaPersona ReglaAlmuerzo(int limite) =>
        new(Guid.NewGuid(), ServicioAlmuerzo, Ambito, CategoriaAlmuerzo, limite);

    private static ReglaPersona ReglaRefrigerio(int limite) =>
        new(Guid.NewGuid(), ServicioRefrigerio, Ambito, CategoriaRefrigerio, limite);

    private static Dictionary<ClaveCupo, int> Usos(params (Guid Categoria, int Usado)[] usos) =>
        usos.ToDictionary(u => new ClaveCupo(Persona, Ambito, u.Categoria, Hoy), u => u.Usado);

    private static DecisionCupo Almuerzo(IReadOnlyList<ReglaPersona> reglas, IReadOnlyDictionary<ClaveCupo, int> usos) =>
        EvaluadorCupo.Evaluar(Persona, ServicioAlmuerzo, Hoy, [new CargaMenu(CategoriaAlmuerzo, 1)], reglas, usos);

    [Fact(DisplayName = "Cupo · Autoriza con cupo libre y devuelve la carga con su clave")]
    public void Autoriza_bajo_el_limite()
    {
        var decision = Almuerzo([ReglaAlmuerzo(2)], Usos((CategoriaAlmuerzo, 1)));

        Assert.True(decision.Autorizada);
        Assert.Null(decision.Motivo);
        var carga = Assert.Single(decision.Cargas);
        Assert.Equal(new ClaveCupo(Persona, Ambito, CategoriaAlmuerzo, Hoy), carga.Clave);
        Assert.Equal(1, carga.Unidades);
    }

    [Fact(DisplayName = "AT02 · Rechazo por cupo agotado: LOCAL_LIMIT_REACHED cuando el uso alcanzó el límite")]
    public void Deniega_en_el_limite()
    {
        var decision = Almuerzo([ReglaAlmuerzo(1)], Usos((CategoriaAlmuerzo, 1)));

        Assert.False(decision.Autorizada);
        Assert.Equal(MotivoDenegacion.LocalLimitReached, decision.Motivo);
        Assert.Empty(decision.Cargas);
    }

    [Fact(DisplayName = "AT02 · Variantes que comparten categoría cuentan juntas: la variante opcional ve el uso de la común")]
    public void Variantes_con_la_misma_categoria_comparten_cupo()
    {
        var usoDeLaVarianteComun = Usos((CategoriaAlmuerzo, 1));
        var cargasVarianteOpcional = new[] { new CargaMenu(CategoriaAlmuerzo, 1) };

        var decision = EvaluadorCupo.Evaluar(Persona, ServicioAlmuerzo, Hoy, cargasVarianteOpcional, [ReglaAlmuerzo(1)], usoDeLaVarianteComun);

        Assert.False(decision.Autorizada);
        Assert.Equal(MotivoDenegacion.LocalLimitReached, decision.Motivo);
    }

    [Fact(DisplayName = "AT02 · Otra categoría no consume el mismo cupo: REFRIGERIO se autoriza con ALMUERZO agotado")]
    public void Otra_categoria_no_consume_el_mismo_cupo()
    {
        var reglas = new[] { ReglaAlmuerzo(1), ReglaRefrigerio(1) };

        var decision = EvaluadorCupo.Evaluar(
            Persona, ServicioRefrigerio, Hoy, [new CargaMenu(CategoriaRefrigerio, 1)], reglas, Usos((CategoriaAlmuerzo, 1)));

        Assert.True(decision.Autorizada);
    }

    [Fact(DisplayName = "Persona sin regla aplicable · sin regla vigente deniega con SERVICE_NOT_ENABLED")]
    public void Sin_regla_deniega_servicio_no_habilitado()
    {
        var decision = Almuerzo([], Usos());

        Assert.False(decision.Autorizada);
        Assert.Equal(MotivoDenegacion.ServiceNotEnabled, decision.Motivo);
    }

    [Fact(DisplayName = "Persona sin regla aplicable · la regla de otro servicio no habilita")]
    public void La_regla_de_otro_servicio_no_habilita()
    {
        var decision = Almuerzo([ReglaRefrigerio(1)], Usos());

        Assert.False(decision.Autorizada);
        Assert.Equal(MotivoDenegacion.ServiceNotEnabled, decision.Motivo);
    }

    [Theory(DisplayName = "Cupo · Una opción con dos cargas se deniega si cualquiera excede")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Dos_cargas_se_deniegan_si_alguna_excede(bool excedeAlmuerzo, bool excedeRefrigerio)
    {
        var reglas = ReglasDeDosCargas();
        var usos = Usos((CategoriaAlmuerzo, excedeAlmuerzo ? 1 : 0), (CategoriaRefrigerio, excedeRefrigerio ? 1 : 0));

        var decision = EvaluadorCupo.Evaluar(Persona, ServicioAlmuerzo, Hoy, DosCargas(), reglas, usos);

        Assert.False(decision.Autorizada);
        Assert.Equal(MotivoDenegacion.LocalLimitReached, decision.Motivo);
    }

    [Fact(DisplayName = "Cupo · Una opción con dos cargas se autoriza si ninguna excede y devuelve las dos cargas")]
    public void Dos_cargas_se_autorizan_si_ninguna_excede()
    {
        var decision = EvaluadorCupo.Evaluar(Persona, ServicioAlmuerzo, Hoy, DosCargas(), ReglasDeDosCargas(), Usos());

        Assert.True(decision.Autorizada);
        Assert.Equal(2, decision.Cargas.Count);
    }

    [Fact(DisplayName = "Cupo · Más de una regla vigente para la misma clave lanza excepción")]
    public void Dos_reglas_para_la_misma_clave_lanzan_excepcion()
    {
        Assert.Throws<ReglasCupoAmbiguasException>(() => Almuerzo([ReglaAlmuerzo(1), ReglaAlmuerzo(2)], Usos()));
    }

    private static CargaMenu[] DosCargas() =>
        [new CargaMenu(CategoriaAlmuerzo, 1), new CargaMenu(CategoriaRefrigerio, 1)];

    private static ReglaPersona[] ReglasDeDosCargas() =>
        [ReglaAlmuerzo(1), ReglaRefrigerio(1) with { TipoServicioId = ServicioAlmuerzo }];
}
