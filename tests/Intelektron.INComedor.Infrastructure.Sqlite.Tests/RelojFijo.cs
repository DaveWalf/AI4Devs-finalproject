namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>Reloj de pruebas (D10): devuelve el instante que el test le fija.</summary>
public sealed class RelojFijo : TimeProvider
{
    public RelojFijo(string instanteUtc)
    {
        Ahora = DateTimeOffset.Parse(instanteUtc);
    }

    public DateTimeOffset Ahora { get; set; }

    public override DateTimeOffset GetUtcNow() => Ahora;
}
