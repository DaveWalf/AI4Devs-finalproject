using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El modo simulación en simulacion.json de la carpeta de datos (D4 de simular-identificacion-por-lista): encendido salvo un
/// archivo que diga APAGADA, y una escritura que falla deja el archivo anterior y le avisa a quien llama.
/// </summary>
public class AlmacenSimulacionTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "incomedor-tests", "simulacion-" + Guid.NewGuid().ToString("N"));

    private string Ruta => Path.Combine(_carpeta, "simulacion.json");

    public void Dispose()
    {
        if (Directory.Exists(_carpeta))
        {
            foreach (var archivo in Directory.EnumerateFiles(_carpeta))
            {
                File.SetAttributes(archivo, FileAttributes.Normal);
            }

            Directory.Delete(_carpeta, recursive: true);
        }
    }

    [Theory(DisplayName = "acceso-puesto · Un ajuste de simulación ilegible (almacén): sin archivo, ilegible o con otro valor, está encendida")]
    [InlineData(null)]
    [InlineData("no es json")]
    [InlineData("{\"simulacion\":\"QUIZAS\"}")]
    [InlineData("{\"simulacion\":true}")]
    [InlineData("[\"ENCENDIDA\"]")]
    public void Sin_un_ajuste_valido_esta_encendida(string? contenido)
    {
        Directory.CreateDirectory(_carpeta);
        if (contenido is not null)
        {
            File.WriteAllText(Ruta, contenido);
        }

        Assert.True(new AlmacenSimulacion(_carpeta).Leer());
    }

    [Fact(DisplayName = "acceso-puesto · Encenderla se conserva al volver a abrir (almacén): guardada encendida se lee encendida desde otro almacén, y apagada, apagada")]
    public void Guardar_y_leer()
    {
        new AlmacenSimulacion(_carpeta).Guardar(true);
        Assert.True(new AlmacenSimulacion(_carpeta).Leer());
        Assert.Contains("ENCENDIDA", File.ReadAllText(Ruta));

        new AlmacenSimulacion(_carpeta).Guardar(false);
        Assert.False(new AlmacenSimulacion(_carpeta).Leer());
    }

    [Fact(DisplayName = "acceso-puesto · La simulación no se puede guardar (almacén): una escritura que falla deja el archivo anterior y lanza la excepción a quien llama")]
    public void Una_escritura_que_falla_deja_el_anterior()
    {
        var almacen = new AlmacenSimulacion(_carpeta);
        almacen.Guardar(false);
        var anterior = File.ReadAllText(Ruta);
        File.SetAttributes(Ruta, FileAttributes.ReadOnly);

        var error = Record.Exception(() => almacen.Guardar(true));

        Assert.True(error is IOException or UnauthorizedAccessException, $"Se esperaba IOException o UnauthorizedAccessException y vino {error?.GetType().Name ?? "nada"}");
        Assert.Equal(anterior, File.ReadAllText(Ruta));
        Assert.False(almacen.Leer());
        Assert.False(File.Exists(Ruta + ".tmp"));
    }
}
