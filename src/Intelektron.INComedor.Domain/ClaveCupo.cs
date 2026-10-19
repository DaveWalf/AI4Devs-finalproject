namespace Intelektron.INComedor.Domain;

/// <summary>Clave de cupo de RB03: persona, ámbito, categoría y fecha de negocio.</summary>
public sealed record ClaveCupo
{
    public Guid PersonaId { get; }
    public Guid AmbitoCupoId { get; }
    public Guid CategoriaCupoId { get; }
    public FechaNegocio Fecha { get; }

    public ClaveCupo(Guid personaId, Guid ambitoCupoId, Guid categoriaCupoId, FechaNegocio fecha)
    {
        if (personaId == Guid.Empty)
        {
            throw new ArgumentException("La persona de la clave de cupo no puede ser un UUID vacío.", nameof(personaId));
        }

        if (ambitoCupoId == Guid.Empty)
        {
            throw new ArgumentException("El ámbito de la clave de cupo no puede ser un UUID vacío.", nameof(ambitoCupoId));
        }

        if (categoriaCupoId == Guid.Empty)
        {
            throw new ArgumentException("La categoría de la clave de cupo no puede ser un UUID vacío.", nameof(categoriaCupoId));
        }

        PersonaId = personaId;
        AmbitoCupoId = ambitoCupoId;
        CategoriaCupoId = categoriaCupoId;
        Fecha = fecha ?? throw new ArgumentNullException(nameof(fecha));
    }
}
