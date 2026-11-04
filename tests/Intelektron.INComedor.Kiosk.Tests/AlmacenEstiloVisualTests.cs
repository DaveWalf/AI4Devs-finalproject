using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El estilo visual guardado en la carpeta de datos (D5 de presentar-atencion-tactil): estilo-visual.json en una carpeta
/// temporal, con los valores por defecto ante cualquier archivo que falte, no se lea o esté fuera de rango.
/// </summary>
public class AlmacenEstiloVisualTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "incomedor-tests", "estilo-" + Guid.NewGuid().ToString("N"));

    public AlmacenEstiloVisualTests()
    {
        Directory.CreateDirectory(_carpeta);
    }

    private string Archivo => Path.Combine(_carpeta, "estilo-visual.json");

    public void Dispose()
    {
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, recursive: true);
        }
    }

    [Fact(DisplayName = "presentacion-puesto · Sin estilo guardado: 76 % de opacidad y 4 px de desenfoque")]
    public void Sin_archivo_los_valores_por_defecto()
    {
        var estilo = new AlmacenEstiloVisual(_carpeta).Leer();

        Assert.Equal(new EstiloVisual(76, 4), estilo);
        Assert.Equal(EstiloVisual.PorDefecto, estilo);
    }

    [Fact(DisplayName = "presentacion-puesto · Guardar y volver a leer: 84 % y 8 px, sin el archivo temporal")]
    public void Guardar_y_volver_a_leer()
    {
        var almacen = new AlmacenEstiloVisual(_carpeta);

        almacen.Guardar(new EstiloVisual(84, 8));

        Assert.Equal(new EstiloVisual(84, 8), new AlmacenEstiloVisual(_carpeta).Leer());
        Assert.Equal(["estilo-visual.json"], Directory.GetFiles(_carpeta).Select(Path.GetFileName));
    }

    [Theory(DisplayName = "presentacion-puesto · Archivo fuera de rango o roto: 76 % y 4 px")]
    [InlineData("""{"opacidad":150,"desenfoque":-3}""")]
    [InlineData("""{"opacidad":150,"desenfoque":4}""")]
    [InlineData("""{"opacidad":80,"desenfoque":-3}""")]
    [InlineData("""{"opacidad":71,"desenfoque":13}""")]
    [InlineData("""{"opacidad":"80","desenfoque":4}""")]
    [InlineData("""{"opacidad":80""")]
    [InlineData("no es json")]
    [InlineData("")]
    public void Archivo_invalido_da_los_valores_por_defecto(string contenido)
    {
        File.WriteAllText(Archivo, contenido);

        Assert.Equal(EstiloVisual.PorDefecto, new AlmacenEstiloVisual(_carpeta).Leer());
    }

    [Fact(DisplayName = "presentacion-puesto · Los extremos del rango se aceptan: 72 % y 0 px, 88 % y 12 px")]
    public void Extremos_del_rango()
    {
        var almacen = new AlmacenEstiloVisual(_carpeta);

        almacen.Guardar(new EstiloVisual(72, 0));
        Assert.Equal(new EstiloVisual(72, 0), almacen.Leer());
        almacen.Guardar(new EstiloVisual(88, 12));
        Assert.Equal(new EstiloVisual(88, 12), almacen.Leer());
    }
}
