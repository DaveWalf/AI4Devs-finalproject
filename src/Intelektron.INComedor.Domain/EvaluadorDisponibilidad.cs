namespace Intelektron.INComedor.Domain;

/// <summary>La decisión sobre una opción para una persona y las reglas que aplicó la habilitación (vacías si la habilitación la denegó).</summary>
public sealed record ResultadoDisponibilidad(DecisionCupo Decision, IReadOnlyList<ReglaPersona> Reglas);

/// <summary>
/// La única evaluación de disponibilidad: la que ofrece una opción al identificarse y la que confirma una intención.
/// Primero la habilitación (RB02) y, sin motivo, el cupo (RB03). Toda condición nueva, como las franjas horarias, se suma acá.
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
        IReadOnlyDictionary<ClaveCupo, int> usos)
    {
        var habilitacion = EvaluadorHabilitacion.Evaluar(personaActiva, credencial, ahora, tipoServicioId, cargas, candidatas);
        var decision = habilitacion.Motivo is { } motivo
            ? new DecisionCupo(false, motivo, [])
            : EvaluadorCupo.Evaluar(personaId, tipoServicioId, fecha, cargas, habilitacion.Reglas, usos);
        return new ResultadoDisponibilidad(decision, habilitacion.Reglas);
    }
}
