using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Xml;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>Los dos logos de la cabecera: una URI data: de un archivo local aceptado, o la ruta del de ejemplo.</summary>
public sealed record Logos(string Empresa, string Concesionaria);

/// <summary>Cuál de los dos logos (D14 de configurar-puesto-por-pestanas).</summary>
public enum LogoPuesto
{
    Empresa,
    Concesionaria,
}

/// <summary>Un archivo de logo que pasó las reglas: su extensión, su tipo, su formato y su contenido.</summary>
public sealed record LogoElegido(string Extension, string Tipo, string Formato, byte[] Contenido)
{
    /// <summary>Para un img, donde el navegador no ejecuta los scripts de un SVG.</summary>
    public string UriData => $"data:{Tipo};base64,{Convert.ToBase64String(Contenido)}";

    /// <summary>La huella con que el logo queda en la bitácora, en lugar de su contenido.</summary>
    public string Sha256 => Convert.ToHexString(SHA256.HashData(Contenido));
}

/// <summary>El logo elegido o, si no sirve, el motivo.</summary>
public sealed record ValidacionLogo(LogoElegido? Logo, string? Motivo);

/// <summary>
/// Los logos de la empresa y la concesionaria desde marcas\ de la carpeta de datos (D4 de presentar-atencion-tactil).
/// Busca empresa.* y concesionaria.* en el orden .png, .webp, .svg y toma el primero que existe; lo acepta si mide
/// hasta 2 MB y su contenido corresponde a su extensión. Un archivo aceptado se entrega como URI data: para un img, donde
/// el navegador no ejecuta los scripts de un SVG; uno rechazado deja el de ejemplo y no interrumpe la atención. Nunca
/// busca en la red (OWASP A05, A08).
/// </summary>
public sealed class LogosLocales
{
    public const int TamanoMaximo = 2 * 1024 * 1024;
    public const string EmpresaDeEjemplo = "img/empresa-ejemplo.svg";
    public const string ConcesionariaDeEjemplo = "img/concesionaria-ejemplo.svg";

    private static readonly (string Extension, string Tipo)[] Formatos =
    [
        (".png", "image/png"),
        (".webp", "image/webp"),
        (".svg", "image/svg+xml"),
    ];

    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly string _marcas;

    public LogosLocales(string carpetaDatos)
    {
        _marcas = Path.Combine(carpetaDatos, "marcas");
    }

    public Logos Leer() => new(Buscar("empresa") ?? EmpresaDeEjemplo, Buscar("concesionaria") ?? ConcesionariaDeEjemplo);

    /// <summary>
    /// Un archivo elegido en Configuración, con las mismas reglas que al leer marcas\ (D14): la extensión, hasta 2 MB y un
    /// contenido que corresponde a la extensión. El tamaño declarado decide antes de leer el contenido.
    /// </summary>
    public static ValidacionLogo Validar(string nombreArchivo, long tamano, byte[] contenido)
    {
        var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        var formato = Formatos.FirstOrDefault(f => f.Extension == extension);
        if (formato.Extension is null)
        {
            return new ValidacionLogo(null, MensajesAtencion.LogoFormato);
        }

        if (tamano > TamanoMaximo || contenido.Length > TamanoMaximo)
        {
            return new ValidacionLogo(null, MensajesAtencion.LogoPasaDe2MB);
        }

        return Corresponde(extension, contenido)
            ? new ValidacionLogo(new LogoElegido(extension, formato.Tipo, Formato(extension), contenido), null)
            : new ValidacionLogo(null, MensajesAtencion.LogoInvalido);
    }

    /// <summary>El logo vigente de la carpeta de datos, o null si se muestra el de ejemplo.</summary>
    public LogoElegido? Actual(LogoPuesto logo) => Encontrar(Nombre(logo));

    /// <summary>Escribe el logo por un temporal y borra las otras extensiones del mismo logo, que si no lo taparían.</summary>
    public void Guardar(LogoPuesto logo, LogoElegido elegido)
    {
        Directory.CreateDirectory(_marcas);
        var nombre = Nombre(logo);
        var destino = Path.Combine(_marcas, nombre + elegido.Extension);
        var temporal = destino + ".tmp";
        try
        {
            File.WriteAllBytes(temporal, elegido.Contenido);
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
                // Queda un temporal que Leer nunca mira.
            }

            throw;
        }

        foreach (var (extension, _) in Formatos.Where(f => f.Extension != elegido.Extension))
        {
            File.Delete(Path.Combine(_marcas, nombre + extension));
        }
    }

    /// <summary>Borra el logo de la carpeta de datos, en sus tres extensiones: vuelve el de ejemplo.</summary>
    public void VolverAlDeEjemplo(LogoPuesto logo)
    {
        foreach (var (extension, _) in Formatos)
        {
            File.Delete(Path.Combine(_marcas, Nombre(logo) + extension));
        }
    }

    private static string Nombre(LogoPuesto logo) => logo == LogoPuesto.Empresa ? "empresa" : "concesionaria";

    private static string Formato(string extension) => extension.TrimStart('.').ToUpperInvariant();

    private string? Buscar(string nombre) => Encontrar(nombre)?.UriData;

    private LogoElegido? Encontrar(string nombre)
    {
        foreach (var (extension, tipo) in Formatos)
        {
            var ruta = Path.Combine(_marcas, nombre + extension);
            if (!File.Exists(ruta))
            {
                continue;
            }

            // El primero que existe decide: si no sirve, queda el de ejemplo, sin probar las otras extensiones.
            var contenido = LeerAcotado(ruta);
            if (contenido is not null && Corresponde(extension, contenido))
            {
                return new LogoElegido(extension, tipo, Formato(extension), contenido);
            }

            Debug.WriteLine($"INComedor: logo local rechazado ({nombre}{extension}); se muestra el de ejemplo.");
            return null;
        }

        return null;
    }

    /// <summary>El contenido, si mide hasta 2 MB; se lee con un tope para no cargar un archivo que creció entre medio.</summary>
    private static byte[]? LeerAcotado(string ruta)
    {
        try
        {
            using var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (flujo.Length > TamanoMaximo)
            {
                return null;
            }

            var buffer = new byte[TamanoMaximo + 1];
            var leidos = 0;
            int parte;
            while (leidos < buffer.Length && (parte = flujo.Read(buffer, leidos, buffer.Length - leidos)) > 0)
            {
                leidos += parte;
            }

            return leidos > TamanoMaximo ? null : buffer[..leidos];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool Corresponde(string extension, byte[] contenido) => extension switch
    {
        ".png" => contenido.AsSpan().StartsWith(FirmaPng),
        ".webp" => contenido.Length >= 12 && contenido.AsSpan(0, 4).SequenceEqual("RIFF"u8) && contenido.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        ".svg" => EsSvg(contenido),
        _ => false,
    };

    /// <summary>XML bien formado, sin DTD ni resolución de recursos externos, cuyo elemento raíz es svg (OWASP A05).</summary>
    private static bool EsSvg(byte[] contenido)
    {
        var opciones = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        try
        {
            using var lector = XmlReader.Create(new MemoryStream(contenido), opciones);
            lector.MoveToContent();
            var raiz = lector.LocalName;
            while (lector.Read())
            {
                // Recorre el documento entero: uno mal formado falla acá.
            }

            return raiz == "svg";
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
