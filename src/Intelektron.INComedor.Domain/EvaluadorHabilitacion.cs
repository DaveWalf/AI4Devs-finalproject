namespace Intelektron.INComedor.Domain;

/// <summary>Empate en la mayor prioridad entre reglas positivas que dicen cosas distintas: la configuración no se puede decidir (RB02).</summary>
public sealed class ReglasHabilitacionContradictoriasException : InvalidOperationException
{
    public ReglasHabilitacionContradictoriasException(string mensaje) : base(mensaje)
    {
    }
}

/// <summary>Regla por persona tal como está en la base: con su prioridad y su vigencia sin filtrar.</summary>
public sealed record ReglaCandidata(
    Guid ReglaId,
    Guid TipoServicioId,
    Guid AmbitoCupoId,
    Guid CategoriaCupoId,
    int Limite,
    int Prioridad,
    DateTimeOffset? VigenteDesdeUtc,
    DateTimeOffset? VigenteHastaUtc);

/// <summary>Credencial que trae la sesión: hasta cuándo vale y si fue revocada.</summary>
public sealed record CredencialSesion(DateTimeOffset? VigenteHastaUtc, DateTimeOffset? RevocadaUtc);

/// <summary>Denegación con su motivo o una regla elegida por categoría de la opción, lista para el cupo.</summary>
public sealed record ResultadoHabilitacion(MotivoDenegacion? Motivo, IReadOnlyList<ReglaPersona> Reglas);

/// <summary>Decide en el puesto, sin red, si una persona está habilitada para el servicio que pide (RB02).</summary>
public static class EvaluadorHabilitacion
{
    public static ResultadoHabilitacion Evaluar(
        bool personaActiva,
        CredencialSesion? credencial,
        DateTimeOffset ahora,
        Guid tipoServicioId,
        IReadOnlyList<CargaMenu> cargas,
        IReadOnlyList<ReglaCandidata> candidatas)
    {
        if (!personaActiva)
        {
            return Denegada(MotivoDenegacion.PersonInactive);
        }

        if (credencial?.RevocadaUtc is not null)
        {
            return Denegada(MotivoDenegacion.CredentialRevoked);
        }

        if (credencial?.VigenteHastaUtc is { } hasta && hasta <= ahora)
        {
            return Denegada(MotivoDenegacion.CredentialExpired);
        }

        var vigentesPorCarga = cargas
            .Select(carga => (Carga: carga, Vigentes: candidatas
                .Where(r => r.TipoServicioId == tipoServicioId && r.CategoriaCupoId == carga.CategoriaCupoId && EstaVigente(r, ahora))
                .ToList()))
            .ToList();

        // La regla cero de cualquier categoría deniega la opción entera, antes de buscar permisos.
        if (vigentesPorCarga.Any(v => v.Vigentes.Any(r => r.Limite == 0)))
        {
            return Denegada(MotivoDenegacion.RuleDenied);
        }

        var elegidas = new List<ReglaPersona>();
        foreach (var (carga, vigentes) in vigentesPorCarga)
        {
            if (vigentes.Count == 0)
            {
                return Denegada(MotivoDenegacion.ServiceNotEnabled);
            }

            elegidas.Add(ElegirPorPrioridad(tipoServicioId, carga.CategoriaCupoId, vigentes));
        }

        return new ResultadoHabilitacion(null, elegidas);
    }

    private static bool EstaVigente(ReglaCandidata regla, DateTimeOffset ahora) =>
        (regla.VigenteDesdeUtc is null || regla.VigenteDesdeUtc <= ahora)
        && (regla.VigenteHastaUtc is null || ahora < regla.VigenteHastaUtc);

    private static ReglaPersona ElegirPorPrioridad(Guid tipoServicioId, Guid categoriaId, List<ReglaCandidata> positivas)
    {
        var mayor = positivas.Max(r => r.Prioridad);
        var empatadas = positivas.Where(r => r.Prioridad == mayor).ToList();

        if (empatadas.Select(r => (r.Limite, r.AmbitoCupoId)).Distinct().Count() > 1)
        {
            throw new ReglasHabilitacionContradictoriasException(
                $"Hay {empatadas.Count} reglas positivas con la prioridad {mayor} para el servicio {tipoServicioId} y la categoría {categoriaId}, con distinto límite o ámbito.");
        }

        var elegida = empatadas[0];
        return new ReglaPersona(elegida.ReglaId, elegida.TipoServicioId, elegida.AmbitoCupoId, elegida.CategoriaCupoId, elegida.Limite);
    }

    private static ResultadoHabilitacion Denegada(MotivoDenegacion motivo) => new(motivo, []);
}
