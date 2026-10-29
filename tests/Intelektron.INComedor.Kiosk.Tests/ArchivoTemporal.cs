namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>Archivo SQLite temporal por test (D11): se borra con sus archivos -wal y -shm.</summary>
public sealed class ArchivoTemporal : IDisposable
{
    public string Ruta { get; } =
        Path.Combine(Path.GetTempPath(), "incomedor-tests", Guid.NewGuid() + ".db");

    public ArchivoTemporal()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
    }

    public void Dispose()
    {
        foreach (var sufijo in new[] { "", "-wal", "-shm" })
        {
            var archivo = Ruta + sufijo;
            if (File.Exists(archivo))
            {
                File.Delete(archivo);
            }
        }
    }
}
