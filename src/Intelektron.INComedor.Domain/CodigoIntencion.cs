namespace Intelektron.INComedor.Domain;

/// <summary>Identidad de una intención de consumo (IntentId); estable durante el reintento local.</summary>
public sealed record CodigoIntencion
{
    public Guid Valor { get; }

    public CodigoIntencion(Guid valor)
    {
        if (valor == Guid.Empty)
        {
            throw new ArgumentException("El código de intención no puede ser un UUID vacío.", nameof(valor));
        }

        Valor = valor;
    }
}
