using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El modo del puesto en su carpeta de datos (D5 de configurar-puesto-por-pestanas): mantenimiento sin archivo, con uno
/// ilegible o con un valor desconocido, y el operativo que se conserva al volver a abrir.
/// </summary>
public class ModoPuestoTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "incomedor-tests", "modo-" + Guid.NewGuid().ToString("N"));

    public ModoPuestoTests()
    {
        Directory.CreateDirectory(_carpeta);
    }

    private string Archivo => Path.Combine(_carpeta, "modo-puesto.json");

    public void Dispose()
    {
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, recursive: true);
        }
    }

    [Fact(DisplayName = "acceso-puesto · Una carpeta nueva arranca en mantenimiento: sin modo guardado, el almacén da mantenimiento")]
    public void Sin_archivo_da_mantenimiento()
    {
        Assert.Equal(ModoPuesto.Mantenimiento, new AlmacenModoPuesto(_carpeta).Leer());
    }

    [Theory(DisplayName = "acceso-puesto · Un modo guardado ilegible o desconocido da mantenimiento")]
    [InlineData("{no es json")]
    [InlineData("{\"modo\":\"TURBO\"}")]
    [InlineData("{\"modo\":1}")]
    [InlineData("[\"OPERATIVO\"]")]
    public void Ilegible_o_desconocido_da_mantenimiento(string contenido)
    {
        File.WriteAllText(Archivo, contenido);

        Assert.Equal(ModoPuesto.Mantenimiento, new AlmacenModoPuesto(_carpeta).Leer());
    }

    [Fact(DisplayName = "acceso-puesto · El modo operativo se conserva al volver a abrir: otro almacén sobre la misma carpeta lo lee, y volver a mantenimiento también")]
    public void El_operativo_se_conserva()
    {
        new AlmacenModoPuesto(_carpeta).Guardar(ModoPuesto.Operativo);

        Assert.Equal("{\"modo\":\"OPERATIVO\"}", File.ReadAllText(Archivo));
        Assert.Equal(ModoPuesto.Operativo, new AlmacenModoPuesto(_carpeta).Leer());

        new AlmacenModoPuesto(_carpeta).Guardar(ModoPuesto.Mantenimiento);

        Assert.Equal(ModoPuesto.Mantenimiento, new AlmacenModoPuesto(_carpeta).Leer());
        Assert.False(File.Exists(Archivo + ".tmp"));
    }
}
