namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>
/// Costura para ensayar caídas (D10): las pruebas lanzan una excepción en cada punto para simular el corte.
/// Visible sólo al proyecto de pruebas; la implementación por defecto no hace nada.
/// </summary>
internal interface IPuntosDeFalla
{
    void DespuesDeLecturaPrevia();

    void AntesDelCommit();

    void DespuesDelCommit();
}

internal sealed class SinPuntosDeFalla : IPuntosDeFalla
{
    public static readonly SinPuntosDeFalla Instancia = new();

    public void DespuesDeLecturaPrevia()
    {
    }

    public void AntesDelCommit()
    {
    }

    public void DespuesDelCommit()
    {
    }
}
