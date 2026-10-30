using System.Security.Cryptography.X509Certificates;

namespace Intelektron.INComedor.Sync.Api;

/// <summary>
/// La cadena de conexión (ConnectionStrings:Receptor) y la autoridad que emite los certificados de equipo
/// (Receptor:Autoridad, la ruta de su certificado, o de un PFX del que se toma sólo la parte pública). Ninguna tiene
/// valor por defecto: sin las dos, el receptor arranca igual y las operaciones responden 503 (D1 y D12).
/// </summary>
internal sealed class ConfiguracionReceptor
{
    public ConfiguracionReceptor(IConfiguration configuracion)
        : this(configuracion.GetConnectionString("Receptor"), configuracion["Receptor:Autoridad"])
    {
    }

    public ConfiguracionReceptor(string? cadenaConexion, string? rutaAutoridad)
    {
        CadenaConexion = cadenaConexion is { Length: > 0 } ? cadenaConexion : null;
        Autoridad = rutaAutoridad is { Length: > 0 } ruta ? CargarAutoridad(ruta) : null;
    }

    public string? CadenaConexion { get; }

    public X509Certificate2? Autoridad { get; }

    public bool Completa => CadenaConexion is not null && Autoridad is not null;

    private static X509Certificate2 CargarAutoridad(string ruta)
    {
        if (!ruta.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase))
        {
            return X509CertificateLoader.LoadCertificateFromFile(ruta);
        }

        using var conClave = X509CertificateLoader.LoadPkcs12FromFile(ruta, null);
        return X509CertificateLoader.LoadCertificate(conClave.RawData);
    }
}
