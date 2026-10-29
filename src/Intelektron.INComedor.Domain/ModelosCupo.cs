namespace Intelektron.INComedor.Domain;

/// <summary>Motivos de denegación que decide el dominio; los nombres del contrato los pone Infrastructure.</summary>
public enum MotivoDenegacion
{
    LocalLimitReached,
    ServiceNotEnabled,
}

/// <summary>Carga de cupo de una opción de menú: la categoría y las unidades que consume.</summary>
public sealed record CargaMenu(Guid CategoriaCupoId, int Unidades);

/// <summary>Regla de habilitación por persona ya filtrada por vigencia (RB02 completa queda fuera de este corte).</summary>
public sealed record ReglaPersona(Guid ReglaId, Guid TipoServicioId, Guid AmbitoCupoId, Guid CategoriaCupoId, int Limite);

/// <summary>Carga efectiva que se persiste si la decisión autoriza.</summary>
public sealed record CargaAplicada(ClaveCupo Clave, int Unidades);

public sealed record DecisionCupo(bool Autorizada, MotivoDenegacion? Motivo, IReadOnlyList<CargaAplicada> Cargas);
