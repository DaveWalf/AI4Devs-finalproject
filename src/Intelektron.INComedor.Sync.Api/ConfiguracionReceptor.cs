using System.Security.Cryptography.X509Certificates;

namespace Intelektron.INComedor.Sync.Api;

/// <summary>
/// La cadena de conexión (ConnectionStrings:Receptor) y la autoridad que emite los certificados de equipo
/// (Receptor:Autoridad, la ruta de su certificado público). Ninguna tiene valor por defecto: sin las dos, el receptor
/// arranca igual y las operaciones responden 503 (D1 y D12).
/// </summary>
internal sealed class ConfiguracionReceptor
{
    public ConfiguracionReceptor(IConfiguration configuracion)
    {
        CadenaConexion = configuracion.GetConnectionString("Receptor") is { Length: > 0 } cadena ? cadena : null;
        Autoridad = configuracion["Receptor:Autoridad"] is { Length: > 0 } ruta
            ? X509CertificateLoader.LoadCertificateFromFile(ruta)
            : null;
    }

    public string? CadenaConexion { get; }

    public X509Certificate2? Autoridad { get; }

    public bool Completa => CadenaConexion is not null && Autoridad is not null;
}
