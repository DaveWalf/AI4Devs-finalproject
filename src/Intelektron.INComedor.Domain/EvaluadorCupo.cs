namespace Intelektron.INComedor.Domain;

/// <summary>Más de una regla vigente para el mismo servicio y categoría: el evaluador no decide en silencio.</summary>
public sealed class ReglasCupoAmbiguasException : InvalidOperationException
{
    public ReglasCupoAmbiguasException(string mensaje) : base(mensaje)
    {
    }
}

/// <summary>Decide el cupo de una intención nueva por persona, ámbito, categoría y fecha de negocio (RB03).</summary>
public static class EvaluadorCupo
{
    public static DecisionCupo Evaluar(
        Guid personaId,
        Guid tipoServicioId,
        FechaNegocio fecha,
        IReadOnlyList<CargaMenu> cargas,
        IReadOnlyList<ReglaPersona> reglasVigentes,
        IReadOnlyDictionary<ClaveCupo, int> usos)
    {
        var resueltas = new List<(CargaMenu Carga, ReglaPersona Regla)>();

        foreach (var carga in cargas)
        {
            var aplicables = reglasVigentes
                .Where(r => r.TipoServicioId == tipoServicioId && r.CategoriaCupoId == carga.CategoriaCupoId)
                .ToList();

            if (aplicables.Count > 1)
            {
                throw new ReglasCupoAmbiguasException(
                    $"Hay {aplicables.Count} reglas vigentes para el servicio {tipoServicioId} y la categoría {carga.CategoriaCupoId}.");
            }

            if (aplicables.Count == 0)
            {
                return Denegada(MotivoDenegacion.ServiceNotEnabled);
            }

            resueltas.Add((carga, aplicables[0]));
        }

        var aplicadas = new List<CargaAplicada>();

        foreach (var (carga, regla) in resueltas)
        {
            var clave = new ClaveCupo(personaId, regla.AmbitoCupoId, carga.CategoriaCupoId, fecha);
            var usado = usos.GetValueOrDefault(clave);

            if (usado + carga.Unidades > regla.Limite)
            {
                return Denegada(MotivoDenegacion.LocalLimitReached);
            }

            aplicadas.Add(new CargaAplicada(clave, carga.Unidades));
        }

        return new DecisionCupo(true, null, aplicadas);
    }

    private static DecisionCupo Denegada(MotivoDenegacion motivo) => new(false, motivo, []);
}
