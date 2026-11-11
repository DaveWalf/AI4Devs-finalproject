using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La presentación de cada opción en la carpeta de datos (administracion-local de personalizar-opciones, D3): color de la
/// paleta, imagen por su huella en opciones\, transparencia en cuatro pasos y precio visible; lo que no valida se descarta
/// al leer, como un estilo visual roto.
/// </summary>
public sealed class PresentacionOpcionesTests : IDisposable
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly string _carpeta = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "incomedor-tests", "presentacion-" + Guid.NewGuid().ToString("N"))).FullName;

    private readonly Guid _opcion = Guid.NewGuid();

    public void Dispose() => Directory.Delete(_carpeta, recursive: true);

    [Fact(DisplayName = "administracion-local · Un archivo de presentación roto: sin archivo, ninguna opción está personalizada y todas muestran el precio")]
    public void Sin_archivo()
    {
        var almacen = new AlmacenPresentacionOpciones(_carpeta);

        Assert.Empty(almacen.Leer());
        Assert.Equal(PresentacionOpcion.PorDefecto, almacen.De(_opcion));
        Assert.True(almacen.De(_opcion).PrecioVisible);
    }

    [Fact(DisplayName = "administracion-local · Un fondo de color con transparencia: el color, 25 % y el precio oculto se guardan y se leen con otro almacén")]
    public void Un_color_con_transparencia()
    {
        new AlmacenPresentacionOpciones(_carpeta).Guardar(_opcion, new PresentacionOpcion(PresentacionOpcion.Paleta[2], null, 25, false), null);

        var leida = new AlmacenPresentacionOpciones(_carpeta).De(_opcion);

        Assert.Equal(new PresentacionOpcion(PresentacionOpcion.Paleta[2], null, 25, false), leida);
        Assert.True(leida.Personalizada);
        Assert.False(File.Exists(Path.Combine(_carpeta, "presentacion-opciones.json.tmp")));
    }

    [Fact(DisplayName = "administracion-local · Una imagen de fondo: se guarda en opciones\\ con su huella como nombre y se entrega como URI data:")]
    public void Una_imagen()
    {
        var imagen = new LogoElegido(".png", "image/png", "PNG", Png);
        var almacen = new AlmacenPresentacionOpciones(_carpeta);

        almacen.Guardar(_opcion, new PresentacionOpcion(null, null, 50, true), imagen);

        var leida = new AlmacenPresentacionOpciones(_carpeta).De(_opcion);
        Assert.Equal(imagen.Sha256 + ".png", leida.Imagen);
        Assert.True(File.Exists(Path.Combine(_carpeta, "opciones", leida.Imagen!)));
        Assert.Equal(imagen.UriData, new AlmacenPresentacionOpciones(_carpeta).UriImagen(leida));
        Assert.Equal(50, leida.Transparencia);
    }

    [Fact(DisplayName = "administracion-local · Un archivo de presentación roto: un color fuera de la paleta, una transparencia que no es un paso y una imagen que no existe se descartan, y un JSON ilegible no impide leer")]
    public void Un_archivo_roto()
    {
        var otra = Guid.NewGuid();
        var tercera = Guid.NewGuid();
        File.WriteAllText(Path.Combine(_carpeta, "presentacion-opciones.json"),
            $$$"""
            {"opciones": {
              "{{{_opcion}}}": {"color": "#000000", "imagen": null, "transparencia": 30, "precioVisible": false},
              "{{{otra}}}": {"color": null, "imagen": "{{{new string('A', 64)}}}.png", "transparencia": 25, "precioVisible": true},
              "{{{tercera}}}": {"color": null, "imagen": "..\\..\\secreto.png", "transparencia": 0, "precioVisible": true},
              "no-es-un-guid": {"color": "{{{PresentacionOpcion.Paleta[0]}}}"}
            }}
            """);

        var almacen = new AlmacenPresentacionOpciones(_carpeta);

        Assert.Equal(new PresentacionOpcion(null, null, 0, false), almacen.De(_opcion));
        Assert.Equal(new PresentacionOpcion(null, null, 25, true), almacen.De(otra));
        Assert.Null(almacen.De(tercera).Imagen);
        Assert.Equal(3, almacen.Leer().Count);

        File.WriteAllText(Path.Combine(_carpeta, "presentacion-opciones.json"), "{ no es json");
        Assert.Empty(new AlmacenPresentacionOpciones(_carpeta).Leer());
    }

    [Fact(DisplayName = "administracion-local · Un archivo de presentación roto: una imagen con «..» en el nombre no abre otro archivo aunque exista y sea válido")]
    public void Una_ruta_con_puntos_no_abre_otro_archivo()
    {
        File.WriteAllBytes(Path.Combine(_carpeta, "secreto.png"), Png);
        Directory.CreateDirectory(Path.Combine(_carpeta, "opciones"));
        File.WriteAllText(Path.Combine(_carpeta, "presentacion-opciones.json"),
            $$$"""{"opciones": {"{{{_opcion}}}": {"color": null, "imagen": "..\\secreto.png", "transparencia": 0, "precioVisible": true} } }""");

        var almacen = new AlmacenPresentacionOpciones(_carpeta);

        Assert.Null(almacen.De(_opcion).Imagen);
        Assert.Null(almacen.UriImagen(new PresentacionOpcion(null, "..\\secreto.png", 0, true)));
    }

    [Fact(DisplayName = "administracion-local · Un archivo de presentación roto: una imagen de opciones\\ que no es una imagen válida o cuyo contenido no tiene la huella de su nombre se descarta al leerla")]
    public void Una_imagen_que_no_corresponde()
    {
        var opciones = Directory.CreateDirectory(Path.Combine(_carpeta, "opciones")).FullName;
        var texto = "esto no es una imagen"u8.ToArray();
        var falsa = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(texto)) + ".png";
        File.WriteAllBytes(Path.Combine(opciones, falsa), texto);
        var cambiada = new string('B', 64) + ".png";
        File.WriteAllBytes(Path.Combine(opciones, cambiada), Png);
        var otra = Guid.NewGuid();
        File.WriteAllText(Path.Combine(_carpeta, "presentacion-opciones.json"),
            $$$"""
            {"opciones": {
              "{{{_opcion}}}": {"imagen": "{{{falsa}}}", "transparencia": 0, "precioVisible": true},
              "{{{otra}}}": {"imagen": "{{{cambiada}}}", "transparencia": 0, "precioVisible": true}
            }}
            """);

        var almacen = new AlmacenPresentacionOpciones(_carpeta);

        Assert.Null(almacen.De(_opcion).Imagen);
        Assert.Null(almacen.De(otra).Imagen);
    }

    [Fact(DisplayName = "administracion-local · Un archivo de presentación roto: uno de más de 1 MB no se lee y ninguna opción queda personalizada")]
    public void Un_archivo_de_presentacion_demasiado_grande()
    {
        File.WriteAllText(Path.Combine(_carpeta, "presentacion-opciones.json"),
            $$$"""{"opciones": {"{{{_opcion}}}": {"color": "{{{PresentacionOpcion.Paleta[0]}}}", "transparencia": 0, "precioVisible": true} } }""" + new string(' ', 1024 * 1024));

        Assert.Empty(new AlmacenPresentacionOpciones(_carpeta).Leer());
    }

    [Fact(DisplayName = "administracion-local · Personalizar una opción: guardar un color fuera de la paleta o una transparencia que no es un paso se rechaza sin escribir")]
    public void Guardar_fuera_de_rango()
    {
        var almacen = new AlmacenPresentacionOpciones(_carpeta);

        Assert.Throws<ArgumentOutOfRangeException>(() => almacen.Guardar(_opcion, new PresentacionOpcion("#000000", null, 0, true), null));
        Assert.Throws<ArgumentOutOfRangeException>(() => almacen.Guardar(_opcion, new PresentacionOpcion(null, null, 30, true), null));
        Assert.False(File.Exists(Path.Combine(_carpeta, "presentacion-opciones.json")));
    }
}
