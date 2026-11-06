namespace Intelektron.INComedor.Domain.Tests;

/// <summary>
/// La franja horaria de la opción en la evaluación única (D4 y D6 de administrar-personas-y-opciones): inicio incluido, fin
/// excluido, en la hora local del puesto; después de persona, credencial y reglas, y antes del cupo.
/// </summary>
public class FranjasHorariasTests
{
    private static readonly Guid Persona = Guid.NewGuid();
    private static readonly Guid ServicioAlmuerzo = Guid.NewGuid();
    private static readonly Guid Ambito = Guid.NewGuid();
    private static readonly Guid CategoriaAlmuerzo = Guid.NewGuid();
    private static readonly FechaNegocio Hoy = new(new DateOnly(2026, 11, 4));
    private static readonly DateTimeOffset Ahora = new(2026, 11, 4, 15, 0, 0, TimeSpan.Zero);

    private static readonly FranjaHoraria Almuerzo11a14 = new(new TimeOnly(11, 0), new TimeOnly(14, 0));
    private static readonly FranjaHoraria Noche22a02 = new(new TimeOnly(22, 0), new TimeOnly(2, 0));

    private static ReglaCandidata Regla(int limite) =>
        new(Guid.NewGuid(), ServicioAlmuerzo, Ambito, CategoriaAlmuerzo, limite, 1, null, null);

    private static ResultadoDisponibilidad Evaluar(
        string hora,
        IReadOnlyList<FranjaHoraria> franjas,
        IReadOnlyList<ReglaCandidata>? reglas = null,
        int usado = 0)
    {
        var usos = usado == 0
            ? new Dictionary<ClaveCupo, int>()
            : new Dictionary<ClaveCupo, int> { [new ClaveCupo(Persona, Ambito, CategoriaAlmuerzo, Hoy)] = usado };
        return EvaluadorDisponibilidad.Evaluar(
            true, null, Ahora, ServicioAlmuerzo, [new CargaMenu(CategoriaAlmuerzo, 1)], reglas ?? [Regla(1)], Persona, Hoy, usos,
            franjas, TimeOnly.Parse(hora));
    }

    [Theory(DisplayName = "D6 · Franja de 11:00 a 14:00: a las 11:00 se autoriza; a las 10:59 y a las 14:00, OUTSIDE_SERVICE_HOURS")]
    [InlineData("10:59", false)]
    [InlineData("11:00", true)]
    [InlineData("13:59", true)]
    [InlineData("14:00", false)]
    public void Inicio_incluido_y_fin_excluido(string hora, bool autorizada)
    {
        var resultado = Evaluar(hora, [Almuerzo11a14]);

        Assert.Equal(autorizada, resultado.Decision.Autorizada);
        Assert.Equal(autorizada ? null : MotivoDenegacion.OutsideServiceHours, resultado.Decision.Motivo);
        if (!autorizada)
        {
            Assert.Empty(resultado.Decision.Cargas);
        }
    }

    [Theory(DisplayName = "D6 · Franja de 22:00 a 02:00, que cruza la medianoche: a la 01:30 y a las 22:00 se autoriza; a las 02:00 y a las 21:59, no")]
    [InlineData("01:30", true)]
    [InlineData("22:00", true)]
    [InlineData("02:00", false)]
    [InlineData("21:59", false)]
    public void Franja_que_cruza_la_medianoche(string hora, bool autorizada)
    {
        var resultado = Evaluar(hora, [Noche22a02]);

        Assert.Equal(autorizada, resultado.Decision.Autorizada);
        Assert.Equal(autorizada ? null : MotivoDenegacion.OutsideServiceHours, resultado.Decision.Motivo);
    }

    [Theory(DisplayName = "D6 · Una opción con dos franjas está disponible si alguna contiene la hora")]
    [InlineData("12:00", true)]
    [InlineData("23:00", true)]
    [InlineData("16:00", false)]
    public void Opcion_con_dos_franjas(string hora, bool autorizada)
    {
        var resultado = Evaluar(hora, [Almuerzo11a14, Noche22a02]);

        Assert.Equal(autorizada, resultado.Decision.Autorizada);
    }

    [Fact(DisplayName = "AT17 · Sin franjas, a las 03:00: la opción se autoriza, sin restricción horaria ajena a la configuración")]
    public void Sin_franjas_disponible_todo_el_dia()
    {
        var resultado = Evaluar("03:00", []);

        Assert.True(resultado.Decision.Autorizada);
    }

    [Fact(DisplayName = "RB02 · La regla cero decide antes que la franja: RULE_DENIED, sin causa de horario")]
    public void Regla_cero_antes_que_la_franja()
    {
        var resultado = Evaluar("16:00", [Almuerzo11a14], reglas: [Regla(0)]);

        Assert.Equal(MotivoDenegacion.RuleDenied, resultado.Decision.Motivo);
    }

    [Fact(DisplayName = "D4 · La franja decide antes que el cupo: con el cupo agotado y fuera de horario, OUTSIDE_SERVICE_HOURS")]
    public void Franja_antes_que_el_cupo()
    {
        var resultado = Evaluar("16:00", [Almuerzo11a14], usado: 1);

        Assert.Equal(MotivoDenegacion.OutsideServiceHours, resultado.Decision.Motivo);
    }

    [Fact(DisplayName = "D4 · Fuera de la franja, las reglas que eligió la habilitación siguen llegando, para «Hoy ya usaste:»")]
    public void Reglas_elegidas_siguen_llegando_fuera_de_horario()
    {
        var regla = Regla(1);

        var resultado = Evaluar("16:00", [Almuerzo11a14], reglas: [regla], usado: 1);

        Assert.Equal(regla.ReglaId, Assert.Single(resultado.Reglas).ReglaId);
    }

    [Fact(DisplayName = "D6 · Una franja con inicio igual al fin no se puede construir")]
    public void Franja_vacia_no_se_construye()
    {
        Assert.Throws<ArgumentException>(() => new FranjaHoraria(new TimeOnly(12, 0), new TimeOnly(12, 0)));
    }
}
