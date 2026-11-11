using System.IO;
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
}

/// <summary>
/// Lee y escribe presentacion-opciones.json y las imágenes de opciones\ en la carpeta de datos (D3), con la escritura atómica
/// de <see cref="AlmacenEstiloVisual"/>. Al leer descarta, campo por campo, lo que no valide: un color fuera de la paleta,
/// una transparencia que no es un paso, una imagen cuyo nombre no es una huella con su extensión, que no existe o cuyo
/// contenido no pasa las reglas de los logos. Un archivo ilegible deja todas las opciones sin personalizar (OWASP A05).
/// </summary>
public sealed partial class AlmacenPresentacionOpciones
{
    private const string Nombre = "presentacion-opciones.json";

    private readonly string _carpeta;
    private Dictionary<Guid, PresentacionOpcion>? _leidas;
    private readonly Dictionary<string, string?> _uris = new(StringComparer.Ordinal);

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
    public string? UriImagen(PresentacionOpcion presentacion)
    {
        if (presentacion.Imagen is not { } nombre)
        {
            return null;
        }

        if (!_uris.TryGetValue(nombre, out var uri))
        {
            uri = LeerImagen(nombre)?.UriData;
            _uris[nombre] = uri;
        }

        return uri;
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
            if (!File.Exists(destino))
            {
                EscribirAtomico(destino, ruta => File.WriteAllBytes(ruta, imagenNueva.Contenido));
            }

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
    }

    private Dictionary<Guid, PresentacionOpcion> LeerArchivo()
    {
        var leidas = new Dictionary<Guid, PresentacionOpcion>();
        try
        {
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
                var imagen = Texto(valor, "imagen") is { } i && NombreDeImagen().IsMatch(i) && LeerImagen(i) is not null ? i : null;
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

    /// <summary>La imagen de opciones\, si su nombre es una huella con su extensión y su contenido pasa las reglas de los logos.</summary>
    private LogoElegido? LeerImagen(string nombre)
    {
        if (!NombreDeImagen().IsMatch(nombre))
        {
            return null;
        }

        try
        {
            var ruta = Path.Combine(Imagenes, nombre);
            var tamano = new FileInfo(ruta).Length;
            if (tamano > LogosLocales.TamanoMaximo)
            {
                return null;
            }

            return LogosLocales.Validar(nombre, tamano, File.ReadAllBytes(ruta)).Logo;
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

    [GeneratedRegex("^[0-9A-F]{64}\\.(png|webp|svg)$")]
    private static partial Regex NombreDeImagen();
}
