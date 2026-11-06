namespace Intelektron.INComedor.Domain;

/// <summary>
/// Franja horaria de una opción, en la hora local del puesto (D6 de administrar-personas-y-opciones): el inicio se incluye y
/// el fin no; si el inicio es posterior al fin, la franja cruza la medianoche. Una franja con inicio igual al fin no existe.
/// </summary>
public sealed record FranjaHoraria
{
    public FranjaHoraria(TimeOnly inicio, TimeOnly fin)
    {
        if (inicio == fin)
        {
            throw new ArgumentException("Una franja horaria no puede empezar y terminar a la misma hora.", nameof(fin));
        }

        Inicio = inicio;
        Fin = fin;
    }

    public TimeOnly Inicio { get; }

    public TimeOnly Fin { get; }

    public bool Contiene(TimeOnly hora) => Inicio < Fin
        ? Inicio <= hora && hora < Fin
        : hora >= Inicio || hora < Fin;
}
