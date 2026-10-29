namespace Intelektron.INComedor.Domain;

/// <summary>Día de negocio de un consumo: no es un instante UTC (RB04).</summary>
public sealed record FechaNegocio
{
    public DateOnly Valor { get; }

    public FechaNegocio(DateOnly valor)
    {
        if (valor == default)
        {
            throw new ArgumentException("La fecha de negocio no puede estar sin inicializar.", nameof(valor));
        }

        Valor = valor;
    }
}
