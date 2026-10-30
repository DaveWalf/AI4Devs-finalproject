using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Intelektron.INComedor.Sync.Api.Tests;

/// <summary>
/// Autoridad y certificados sintéticos generados en memoria en cada corrida (D2 de recibir-lotes-idempotente): ninguno
/// entra al repo. SChannel no usa claves efímeras para TLS, así que cada certificado con clave se exporta a PFX y se
/// vuelve a importar.
/// </summary>
public sealed class CertificadosDePrueba : IDisposable
{
    private static readonly Oid AutenticacionCliente = new("1.3.6.1.5.5.7.3.2");
    private static readonly Oid AutenticacionServidor = new("1.3.6.1.5.5.7.3.1");

    private readonly List<X509Certificate2> _emitidos = [];
    private readonly DateTimeOffset _ahora = DateTimeOffset.UtcNow;

    public CertificadosDePrueba()
    {
        Autoridad = CrearAutoridad("CN=INComedor Autoridad de Prueba");
        OtraAutoridad = CrearAutoridad("CN=INComedor Autoridad Ajena de Prueba");
        Servidor = Emitir(Autoridad, "CN=localhost", _ahora.AddDays(-1), _ahora.AddDays(30), servidor: true);
        Puesto = NuevoPuesto("CN=INComedor Puesto de Prueba");
        OtroPuesto = NuevoPuesto("CN=INComedor Otro Puesto de Prueba");
        PuestoVencido = NuevoPuesto("CN=INComedor Puesto Vencido de Prueba", _ahora.AddDays(-60), _ahora.AddDays(-1));
        PuestoDeOtraAutoridad = NuevoPuesto(Puesto.Subject, autoridad: OtraAutoridad);
        PuestoSinRegistrar = NuevoPuesto("CN=INComedor Puesto Sin Registrar de Prueba");
    }

    /// <summary>La autoridad que el receptor confía para los certificados de equipo y que emite el del servidor.</summary>
    public X509Certificate2 Autoridad { get; }

    /// <summary>Una autoridad que el receptor no confía.</summary>
    public X509Certificate2 OtraAutoridad { get; }

    public X509Certificate2 Servidor { get; }

    public X509Certificate2 Puesto { get; }

    public X509Certificate2 OtroPuesto { get; }

    public X509Certificate2 PuestoVencido { get; }

    /// <summary>Misma identidad que <see cref="Puesto"/>, emitido por <see cref="OtraAutoridad"/>.</summary>
    public X509Certificate2 PuestoDeOtraAutoridad { get; }

    public X509Certificate2 PuestoSinRegistrar { get; }

    /// <summary>La autoridad sin clave privada, como la lee el receptor.</summary>
    public byte[] AutoridadPublica => Autoridad.Export(X509ContentType.Cert);

    /// <summary>Un certificado de equipo para autenticación de cliente, vigente un año salvo que se indique otra cosa.</summary>
    public X509Certificate2 NuevoPuesto(string sujeto, DateTimeOffset? desde = null, DateTimeOffset? hasta = null, X509Certificate2? autoridad = null) =>
        Emitir(autoridad ?? Autoridad, sujeto, desde ?? _ahora.AddDays(-1), hasta ?? _ahora.AddYears(1), servidor: false);

    public static string Huella(X509Certificate2 certificado) => certificado.GetCertHashString(HashAlgorithmName.SHA256);

    public void Dispose()
    {
        foreach (var certificado in _emitidos)
        {
            certificado.Dispose();
        }
    }

    private X509Certificate2 CrearAutoridad(string sujeto)
    {
        using var clave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pedido = new CertificateRequest(sujeto, clave, HashAlgorithmName.SHA256);
        pedido.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        pedido.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(pedido.PublicKey, false));
        using var autoFirmado = pedido.CreateSelfSigned(_ahora.AddYears(-5), _ahora.AddYears(5));
        return Persistir(autoFirmado);
    }

    private X509Certificate2 Emitir(X509Certificate2 autoridad, string sujeto, DateTimeOffset desde, DateTimeOffset hasta, bool servidor)
    {
        using var clave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pedido = new CertificateRequest(sujeto, clave, HashAlgorithmName.SHA256);
        pedido.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        pedido.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([servidor ? AutenticacionServidor : AutenticacionCliente], false));
        pedido.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(autoridad, true, false));
        if (servidor)
        {
            var nombres = new SubjectAlternativeNameBuilder();
            nombres.AddDnsName("localhost");
            nombres.AddIpAddress(System.Net.IPAddress.Loopback);
            pedido.CertificateExtensions.Add(nombres.Build());
        }

        var serie = RandomNumberGenerator.GetBytes(16);
        serie[0] &= 0x7F;
        using var publico = pedido.Create(autoridad, desde, hasta, serie);
        using var conClave = publico.CopyWithPrivateKey(clave);
        return Persistir(conClave);
    }

    private X509Certificate2 Persistir(X509Certificate2 certificado)
    {
        var importado = X509CertificateLoader.LoadPkcs12(certificado.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
        _emitidos.Add(importado);
        return importado;
    }
}
