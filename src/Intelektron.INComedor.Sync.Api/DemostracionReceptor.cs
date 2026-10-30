using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Intelektron.INComedor.Sync.Api.Ingesta;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Data.SqlClient;

namespace Intelektron.INComedor.Sync.Api;

/// <summary>
/// El receptor arrancado con --demostracion (D12 de recibir-lotes-idempotente): genera, si faltan, una autoridad, un
/// certificado de servidor y uno del puesto, sintéticos y sin contraseña, en la carpeta de la demostración fuera del
/// repo; usa la base INComedorReceptorDemo de la instancia de INCOMEDOR_SQLSERVER; registra el puesto de la semilla y
/// escucha en https://localhost:5001. Las pruebas cambian carpeta, base y puerto por configuración.
/// </summary>
internal static partial class DemostracionReceptor
{
    // El puesto de semilla_demostracion.sql del Kiosk; ModoDemostracionReceptorTests compara los dos.
    private static readonly Guid PuestoDemostracion = Guid.Parse("00000000-0000-4000-8000-000000000002");
    private static readonly Guid InstalacionDemostracion = Guid.Parse("00000000-0000-4000-8000-000000000003");
    private static readonly Guid OrganizacionDemostracion = Guid.Parse("00000000-0000-4000-8000-000000000004");

    private static readonly Oid AutenticacionCliente = new("1.3.6.1.5.5.7.3.2");
    private static readonly Oid AutenticacionServidor = new("1.3.6.1.5.5.7.3.1");

    public static bool Activa(IConfiguration configuracion, IEnumerable<string> argumentos) =>
        argumentos.Contains("--demostracion", StringComparer.OrdinalIgnoreCase) || configuracion.GetValue<bool>("Receptor:Demostracion");

    public static string Carpeta(IConfiguration configuracion) =>
        configuracion["Receptor:CarpetaDemostracion"] is { Length: > 0 } carpeta
            ? carpeta
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "INComedor", "demo", "certificados");

    public static ConfiguracionReceptor Configuracion(IConfiguration configuracion) =>
        new(CadenaConexion(configuracion, Base(configuracion)), Path.Combine(Carpeta(configuracion), "autoridad.pfx"));

    /// <summary>
    /// HTTPS con el certificado del receptor: localhost:5001, o el puerto que diga la configuración. Kestrel puede armar
    /// sus opciones antes que Preparar (lo hace WebApplicationFactory), así que asegura los certificados por su cuenta.
    /// </summary>
    public static void Escuchar(KestrelServerOptions kestrel, IConfiguration configuracion)
    {
        var puerto = configuracion.GetValue("Receptor:PuertoDemostracion", 5001);
        AsegurarCertificados(Carpeta(configuracion));
        var servidor = X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(Carpeta(configuracion), "receptor.pfx"), null);
        if (puerto == 0)
        {
            kestrel.Listen(IPAddress.Loopback, 0, escucha => escucha.UseHttps(servidor));
        }
        else
        {
            kestrel.ListenLocalhost(puerto, escucha => escucha.UseHttps(servidor));
        }
    }

    /// <summary>Certificados, base, esquema y registro del puesto; corre antes de que Kestrel arranque.</summary>
    public static void Preparar(IConfiguration configuracion)
    {
        var carpeta = Carpeta(configuracion);
        AsegurarCertificados(carpeta);

        var baseDatos = Base(configuracion);
        using (var conexion = new SqlConnection(CadenaConexion(configuracion, "master")))
        {
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText =
                """
                IF DB_ID(@base) IS NULL
                BEGIN
                    DECLARE @crear nvarchar(400) = N'CREATE DATABASE ' + QUOTENAME(@base);
                    EXEC sp_executesql @crear;
                END
                """;
            comando.Parameters.AddWithValue("@base", baseDatos);
            comando.ExecuteNonQuery();
        }

        var cadena = CadenaConexion(configuracion, baseDatos);
        MigracionReceptor.Aplicar(cadena);
        using var puesto = X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(carpeta, "puesto.pfx"), null);
        using var registro = new SqlConnection(cadena);
        registro.Open();
        using var alta = registro.CreateCommand();
        alta.CommandText =
            """
            UPDATE dbo.tbComedorPuestos SET compueIdentidadCertificado = @huella WHERE compueCodigo = @pue;
            IF @@ROWCOUNT = 0
                INSERT INTO dbo.tbComedorPuestos (compueCodigo, comInstalacionCodigo, comOrganizacionCodigo, compueIdentidadCertificado)
                VALUES (@pue, @sitio, @org, @huella);
            """;
        alta.Parameters.AddWithValue("@pue", PuestoDemostracion);
        alta.Parameters.AddWithValue("@sitio", InstalacionDemostracion);
        alta.Parameters.AddWithValue("@org", OrganizacionDemostracion);
        alta.Parameters.AddWithValue("@huella", puesto.GetCertHashString(HashAlgorithmName.SHA256));
        alta.ExecuteNonQuery();
    }

    private static string Base(IConfiguration configuracion)
    {
        var baseDatos = configuracion["Receptor:BaseDemostracion"] is { Length: > 0 } nombre ? nombre : "INComedorReceptorDemo";
        return NombreDeBase().IsMatch(baseDatos)
            ? baseDatos
            : throw new InvalidOperationException("Receptor:BaseDemostracion sólo admite letras, dígitos y guion bajo.");
    }

    private static string CadenaConexion(IConfiguration configuracion, string baseDatos) =>
        new SqlConnectionStringBuilder
        {
            DataSource = configuracion["INCOMEDOR_SQLSERVER"] is { Length: > 0 } instancia ? instancia : @"(localdb)\MSSQLLocalDB",
            InitialCatalog = baseDatos,
            IntegratedSecurity = true,
            Encrypt = SqlConnectionEncryptOption.Optional,
        }.ConnectionString;

    /// <summary>Si falta alguno de los tres PFX, genera los tres de nuevo; el registro del puesto se actualiza al preparar.</summary>
    private static void AsegurarCertificados(string carpeta)
    {
        string[] archivos = ["autoridad.pfx", "receptor.pfx", "puesto.pfx"];
        if (archivos.All(a => File.Exists(Path.Combine(carpeta, a))))
        {
            return;
        }

        Directory.CreateDirectory(carpeta);
        var ahora = DateTimeOffset.UtcNow;
        using var claveAutoridad = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pedido = new CertificateRequest("CN=INComedor Autoridad Demostracion", claveAutoridad, HashAlgorithmName.SHA256);
        pedido.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        pedido.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(pedido.PublicKey, false));
        using var autoridad = pedido.CreateSelfSigned(ahora.AddDays(-1), ahora.AddYears(5));
        using var receptor = Emitir(autoridad, "CN=localhost", servidor: true, ahora);
        using var puesto = Emitir(autoridad, "CN=INComedor Puesto Demostracion", servidor: false, ahora);

        // Sin contraseña: son sintéticos y viven en la carpeta que la demostración borra (Risks del design).
        File.WriteAllBytes(Path.Combine(carpeta, "autoridad.pfx"), autoridad.Export(X509ContentType.Pkcs12));
        File.WriteAllBytes(Path.Combine(carpeta, "receptor.pfx"), receptor.Export(X509ContentType.Pkcs12));
        File.WriteAllBytes(Path.Combine(carpeta, "puesto.pfx"), puesto.Export(X509ContentType.Pkcs12));
    }

    private static X509Certificate2 Emitir(X509Certificate2 autoridad, string sujeto, bool servidor, DateTimeOffset ahora)
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
            nombres.AddIpAddress(IPAddress.Loopback);
            nombres.AddIpAddress(IPAddress.IPv6Loopback);
            pedido.CertificateExtensions.Add(nombres.Build());
        }

        var serie = RandomNumberGenerator.GetBytes(16);
        serie[0] &= 0x7F;
        using var publico = pedido.Create(autoridad, ahora.AddDays(-1), ahora.AddYears(2), serie);
        return publico.CopyWithPrivateKey(clave);
    }

    [GeneratedRegex("^[A-Za-z0-9_]{1,100}$")]
    private static partial Regex NombreDeBase();
}
