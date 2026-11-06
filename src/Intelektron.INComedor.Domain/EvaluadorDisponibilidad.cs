namespace Intelektron.INComedor.Domain;

/// <summary>La decisión sobre una opción para una persona y las reglas que aplicó la habilitación (vacías si la habilitación la denegó).</summary>
public sealed record ResultadoDisponibilidad(DecisionCupo Decision, IReadOnlyList<ReglaPersona> Reglas);

/// <summary>
/// La única evaluación de disponibilidad: la que ofrece una opción al identificarse y la que confirma una intención.
/// Primero la habilitación (RB02), después la franja horaria de la opción (D4 de administrar-personas-y-opciones) y, sin
/// motivo, el cupo (RB03). Toda condición nueva se suma acá. La firma sin franjas es «todo el día».
/// </summary>
public static class EvaluadorDisponibilidad
{
    public static ResultadoDisponibilidad Evaluar(
        bool personaActiva,
        CredencialSesion? credencial,
        DateTimeOffset ahora,
        Guid tipoServicioId,
        IReadOnlyList<CargaMenu> cargas,
        IReadOnlyList<ReglaCandidata> candidatas,
        Guid personaId,
        FechaNegocio fecha,
        IReadOnlyDictionary<ClaveCupo, int> usos) =>
        Evaluar(personaActiva, credencial, ahora, tipoServicioId, cargas, candidatas, personaId, fecha, usos, [], TimeOnly.MinValue);

    public static ResultadoDisponibilidad Evaluar(
        bool personaActiva,
        CredencialSesion? credencial,
        DateTimeOffset ahora,
        Guid tipoServicioId,
        IReadOnlyList<CargaMenu> cargas,
        IReadOnlyList<ReglaCandidata> candidatas,
        Guid personaId,
        FechaNegocio fecha,
        IReadOnlyDictionary<ClaveCupo, int> usos,
        IReadOnlyList<FranjaHoraria> franjas,
        TimeOnly horaLocal)
    {
        var habilitacion = EvaluadorHabilitacion.Evaluar(personaActiva, credencial, ahora, tipoServicioId, cargas, candidatas);
        DecisionCupo decision;
        if (habilitacion.Motivo is { } motivo)
        {
            decision = new DecisionCupo(false, motivo, []);
        }
        else if (franjas.Count > 0 && !franjas.Any(f => f.Contiene(horaLocal)))
        {
            // Las reglas elegidas siguen llegando: «Hoy ya usaste:» se calcula con ellas aunque la opción esté fuera de horario.
            decision = new DecisionCupo(false, MotivoDenegacion.OutsideServiceHours, []);
        }
        else
        {
            decision = EvaluadorCupo.Evaluar(personaId, tipoServicioId, fecha, cargas, habilitacion.Reglas, usos);
        }

        return new ResultadoDisponibilidad(decision, habilitacion.Reglas);
    }
}
