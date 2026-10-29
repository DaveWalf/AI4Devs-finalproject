namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>Puntos de falla de pruebas (D10): cada punto ejecuta la acción que el test le asigne.</summary>
internal sealed class PuntosDeFallaDePrueba : IPuntosDeFalla
{
    public Action? EnDespuesDeLecturaPrevia { get; set; }

    public Action? EnAntesDelCommit { get; set; }

    public Action? EnDespuesDelCommit { get; set; }

    public void DespuesDeLecturaPrevia() => EnDespuesDeLecturaPrevia?.Invoke();

    public void AntesDelCommit() => EnAntesDelCommit?.Invoke();

    public void DespuesDelCommit() => EnDespuesDelCommit?.Invoke();
}
