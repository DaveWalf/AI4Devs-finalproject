namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>Reloj manual de la atención (D5): la prueba lo avanza para cruzar los plazos de inactividad sin esperar.</summary>
public sealed class RelojManual : TimeProvider
{
    public RelojManual(string instanteUtc)
    {
        Ahora = DateTimeOffset.Parse(instanteUtc);
    }

    public DateTimeOffset Ahora { get; private set; }

    public void Avanzar(TimeSpan lapso) => Ahora += lapso;

    public override DateTimeOffset GetUtcNow() => Ahora;
}
