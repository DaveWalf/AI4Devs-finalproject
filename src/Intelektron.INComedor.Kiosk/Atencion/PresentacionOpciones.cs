using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Cómo se ve una opción en la atención (D2 y D3 de personalizar-opciones): un color de la paleta o una imagen por su
/// huella, la transparencia en cuatro pasos y si muestra el precio. Es presentación local del puesto, no configuración.
/// </summary>
public sealed record PresentacionOpcion(string? Color, string? Imagen, int Transparencia, bool PrecioVisible)
{
    public static readonly PresentacionOpcion PorDefecto = new(null, null, 0, true);

    /// <summary>Ocho tonos claros que se leen con la tinta oscura del puesto (D2).</summary>
    public static readonly IReadOnlyList<string> Paleta = ["#F4E3C1", "#EBCB9F", "#DDB089", "#E6B8B7", "#D8C7E8", "#BFD7EA", "#C8D5B9", "#A9C5A0"];

    /// <summary>Los cuatro pasos de transparencia, en por ciento (D2).</summary>
    public static readonly IReadOnlyList<int> Transparencias = [0, 25, 50, 75];

    public bool Personalizada => Color is not null || Imagen is not null;

    /// <summary>
    /// Las variables de puesto.css para la tarjeta (D4): el color, la imagen como URI data: y la opacidad del fondo, que es 1
    /// menos la transparencia; null sin fondo. <paramref name="uriImagen"/> es la imagen ya leída, o la elegida sin guardar.
    /// </summary>
    public static string? EstiloCss(PresentacionOpcion presentacion, string? uriImagen)
    {
        var partes = new List<string>();
        if (uriImagen is not null)
        {
            partes.Add($"--imagen-opcion: url('{uriImagen}')");
        }
        else if (presentacion.Color is { } color)
        {
            partes.Add($"--fondo-opcion: {color}");
        }

        if (partes.Count == 0)
        {
            return null;
        }

        partes.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"--opacidad-fondo: {(100 - presentacion.Transparencia) / 100m:0.00}"));
        return string.Join("; ", partes);
    }

    /// <summary>La clase de la tarjeta: «personalizada», y además «con-imagen» si el fondo es una imagen.</summary>
    public static string Clase(PresentacionOpcion presentacion, string? uriImagen) =>
        uriImagen is not null ? "personalizada con-imagen" : presentacion.Color is not null ? "personalizada" : string.Empty;
}

/// <summary>
/// Lee y escribe presentacion-opciones.json y las imágenes de opciones\ en la carpeta de datos (D3), con la escritura atómica
/// de <see cref="AlmacenEstiloVisual"/>. Al leer descarta, campo por campo, lo que no valide: un color fuera de la paleta,
/// una transparencia que no es un paso, una imagen cuyo nombre no es una huella con su extensión, que no existe, cuyo
/// contenido no tiene esa huella o no pasa las reglas de los logos. Un archivo ilegible o de más de 1 MB deja todas las
/// opciones sin personalizar (OWASP A05). La imagen y el estilo de cada opción se leen una vez y se recuerdan hasta el
/// próximo guardado: el estilo con una imagen puede pesar megabytes y la atención lo dibuja en cada toque.
/// </summary>
public sealed partial class AlmacenPresentacionOpciones
{
    private const string Nombre = "presentacion-opciones.json";

    /// <summary>El tamaño máximo del archivo de presentación: quince opciones caben en unos pocos KB.</summary>
    public const long TamanoMaximo = 1024 * 1024;

    private readonly string _carpeta;
    private Dictionary<Guid, PresentacionOpcion>? _leidas;
    private readonly Dictionary<string, LogoElegido?> _imagenes = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, (string? Estilo, string Clase)> _estilos = [];

    public AlmacenPresentacionOpciones(string carpetaDatos)
    {
        _carpeta = carpetaDatos;
    }

    private string Ruta => Path.Combine(_carpeta, Nombre);

    private string Imagenes => Path.Combine(_carpeta, "opciones");

    /// <summary>Las opciones personalizadas, leídas una vez y vueltas a leer después de cada guardado.</summary>
    public IReadOnlyDictionary<Guid, PresentacionOpcion> Leer() => _leidas ??= LeerArchivo();

    public PresentacionOpcion De(Guid opcion) => Leer().GetValueOrDefault(opcion, PresentacionOpcion.PorDefecto);

    /// <summary>La imagen como URI data:, como la de un logo, o null si no tiene o ya no valida.</summary>
    public string? UriImagen(PresentacionOpcion presentacion) =>
        presentacion.Imagen is { } nombre ? Imagen(nombre)?.UriData : null;

    /// <summary>El estilo y la clase de la tarjeta de una opción (D4), armados una vez por guardado.</summary>
    public (string? Estilo, string Clase) EstiloDe(Guid opcion)
    {
        if (!_estilos.TryGetValue(opcion, out var estilo))
        {
            var presentacion = De(opcion);
            var uri = UriImagen(presentacion);
            estilo = (PresentacionOpcion.EstiloCss(presentacion, uri), PresentacionOpcion.Clase(presentacion, uri));
            _estilos[opcion] = estilo;
        }

        return estilo;
    }

    /// <summary>
    /// Guarda la presentación de una opción; con <paramref name="imagenNueva"/>, la escribe en opciones\ con su huella como
    /// nombre y la usa. Una presentación fuera de la paleta o de los pasos se rechaza sin escribir nada.
    /// </summary>
    public void Guardar(Guid opcion, PresentacionOpcion presentacion, LogoElegido? imagenNueva)
    {
        if (presentacion.Color is { } color && !PresentacionOpcion.Paleta.Contains(color))
        {
            throw new ArgumentOutOfRangeException(nameof(presentacion), color, "El color no es de la paleta.");
        }

        if (!PresentacionOpcion.Transparencias.Contains(presentacion.Transparencia))
        {
            throw new ArgumentOutOfRangeException(nameof(presentacion), presentacion.Transparencia, "La transparencia no es uno de los pasos.");
        }

        if (imagenNueva is not null)
        {
            Directory.CreateDirectory(Imagenes);
            var nombre = imagenNueva.Sha256 + imagenNueva.Extension;
            var destino = Path.Combine(Imagenes, nombre);
            // Un archivo con ese nombre y otro contenido (dañado) se reescribe.
            if (LeerImagen(nombre) is null)
            {
                EscribirAtomico(destino, ruta => File.WriteAllBytes(ruta, imagenNueva.Contenido));
            }

            _imagenes.Remove(nombre);

            presentacion = presentacion with { Imagen = nombre, Color = null };
        }

        var todas = new Dictionary<Guid, PresentacionOpcion>(Leer()) { [opcion] = presentacion };
        Directory.CreateDirectory(_carpeta);
        var contenido = JsonSerializer.Serialize(new
        {
            opciones = todas.ToDictionary(
                p => p.Key.ToString("D"),
                p => new { color = p.Value.Color, imagen = p.Value.Imagen, transparencia = p.Value.Transparencia, precioVisible = p.Value.PrecioVisible }),
        });
        EscribirAtomico(Ruta, ruta => File.WriteAllText(ruta, contenido));
        _leidas = null;
        _estilos.Clear();
    }

    private Dictionary<Guid, PresentacionOpcion> LeerArchivo()
    {
        var leidas = new Dictionary<Guid, PresentacionOpcion>();
        try
        {
            if (new FileInfo(Ruta) is { Exists: true, Length: > TamanoMaximo })
            {
                return leidas;
            }

            using var documento = JsonDocument.Parse(File.ReadAllText(Ruta));
            if (!documento.RootElement.TryGetProperty("opciones", out var opciones) || opciones.ValueKind != JsonValueKind.Object)
            {
                return leidas;
            }

            foreach (var propiedad in opciones.EnumerateObject())
            {
                if (!Guid.TryParse(propiedad.Name, out var id) || propiedad.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var valor = propiedad.Value;
                var color = Texto(valor, "color") is { } c && PresentacionOpcion.Paleta.Contains(c) ? c : null;
                var imagen = Texto(valor, "imagen") is { } i && Imagen(i) is not null ? i : null;
                var transparencia = valor.TryGetProperty("transparencia", out var t) && t.ValueKind == JsonValueKind.Number
                    && t.TryGetInt32(out var paso) && PresentacionOpcion.Transparencias.Contains(paso) ? paso : 0;
                var precio = !valor.TryGetProperty("precioVisible", out var p) || p.ValueKind != JsonValueKind.False;
                leidas[id] = new PresentacionOpcion(imagen is null ? color : null, imagen, transparencia, precio);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Sin archivo, o ilegible: ninguna opción personalizada.
            leidas.Clear();
        }

        return leidas;
    }

    /// <summary>La imagen de opciones\ ya leída, o leída ahora y recordada.</summary>
    private LogoElegido? Imagen(string nombre)
    {
        if (!_imagenes.TryGetValue(nombre, out var imagen))
        {
            imagen = LeerImagen(nombre);
            _imagenes[nombre] = imagen;
        }

        return imagen;
    }

    /// <summary>
    /// La imagen de opciones\, si su nombre es una huella con su extensión, su contenido tiene esa huella y pasa las reglas de
    /// los logos. Se lee con un tope, para no cargar un archivo que creció entre medio.
    /// </summary>
    private LogoElegido? LeerImagen(string nombre)
    {
        if (!NombreDeImagen().IsMatch(nombre))
        {
            return null;
        }

        try
        {
            using var flujo = new FileStream(Path.Combine(Imagenes, nombre), FileMode.Open, FileAccess.Read, FileShare.Read);
            if (flujo.Length > LogosLocales.TamanoMaximo)
            {
                return null;
            }

            var contenido = new byte[flujo.Length];
            flujo.ReadExactly(contenido);
            var huella = Path.GetFileNameWithoutExtension(nombre);
            return Convert.ToHexString(SHA256.HashData(contenido)) == huella
                ? LogosLocales.Validar(nombre, contenido.Length, contenido).Logo
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? Texto(JsonElement valor, string nombre) =>
        valor.TryGetProperty(nombre, out var texto) && texto.ValueKind == JsonValueKind.String ? texto.GetString() : null;

    /// <summary>Escribe a un temporal y lo mueve encima: no deja un archivo a medias, y si falla borra el temporal.</summary>
    private static void EscribirAtomico(string destino, Action<string> escribir)
    {
        var temporal = destino + ".tmp";
        try
        {
            escribir(temporal);
            File.Move(temporal, destino, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporal);
            }
            catch (Exception borrado) when (borrado is IOException or UnauthorizedAccessException)
            {
                // Sin escritura tampoco se puede borrar: queda un temporal que nadie lee.
            }

            throw;
        }
    }

    [GeneratedRegex("^[0-9A-F]{64}\\.(png|webp|svg)\\z")]
    private static partial Regex NombreDeImagen();
}
