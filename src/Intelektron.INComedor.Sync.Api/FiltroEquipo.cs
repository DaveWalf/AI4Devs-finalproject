using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient;

namespace Intelektron.INComedor.Sync.Api;

/// <summary>
/// Autenticación del equipo en las dos operaciones de lotes (D2): sin certificado, con una cadena que no termina en la
/// autoridad configurada o fuera de su vigencia según el reloj del receptor, 401; válido pero sin registro en
/// tbComedorPuestos, 403. Los tres 401 responden lo mismo, para no revelar cuál falló. Si pasa, el KioskId registrado
/// acota todo lo que sigue.
/// </summary>
internal sealed class FiltroEquipo(ConfiguracionReceptor configuracion, TimeProvider reloj, ILogger<FiltroEquipo> registro) : IEndpointFilter
{
    private const string ClavePuesto = "INComedor.KioskId";
    private static readonly Oid AutenticacionCliente = new("1.3.6.1.5.5.7.3.2");

    public static Guid PuestoAutenticado(HttpContext contexto) => (Guid)contexto.Items[ClavePuesto]!;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext invocacion, EndpointFilterDelegate siguiente)
    {
        var contexto = invocacion.HttpContext;
        if (!configuracion.Completa)
        {
            return Rechazar("SERVICE_UNAVAILABLE", "receptor sin cadena de conexión o sin autoridad");
        }

        var certificado = await contexto.Connection.GetClientCertificateAsync(contexto.RequestAborted);
        if (certificado is null)
        {
            return Rechazar("EQUIPMENT_AUTH_REQUIRED", "sin certificado");
        }

        if (!CadenaYVigenciaValidas(certificado, configuracion.Autoridad!, reloj.GetUtcNow()))
        {
            return Rechazar("EQUIPMENT_AUTH_REQUIRED", "cadena o vigencia inválida");
        }

        var puesto = await BuscarPuestoAsync(certificado.GetCertHashString(HashAlgorithmName.SHA256), contexto.RequestAborted);
        if (puesto is null)
        {
            return Rechazar("EQUIPMENT_FORBIDDEN", "certificado sin registro");
        }

        contexto.Items[ClavePuesto] = puesto.Value;
        return await siguiente(invocacion);
    }

    private static bool CadenaYVigenciaValidas(X509Certificate2 certificado, X509Certificate2 autoridad, DateTimeOffset ahora)
    {
        if (ahora < new DateTimeOffset(certificado.NotBefore) || ahora > new DateTimeOffset(certificado.NotAfter))
        {
            return false;
        }

        using var cadena = new X509Chain();
        cadena.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        cadena.ChainPolicy.CustomTrustStore.Add(autoridad);
        cadena.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        cadena.ChainPolicy.DisableCertificateDownloads = true;
        cadena.ChainPolicy.VerificationTime = ahora.UtcDateTime;
        cadena.ChainPolicy.ApplicationPolicy.Add(AutenticacionCliente);
        return cadena.Build(certificado);
    }

    private async Task<Guid?> BuscarPuestoAsync(string huella, CancellationToken cancelacion)
    {
        await using var conexion = new SqlConnection(configuracion.CadenaConexion);
        await conexion.OpenAsync(cancelacion);
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT compueCodigo FROM dbo.tbComedorPuestos WHERE compueIdentidadCertificado = @huella";
        comando.Parameters.Add(new SqlParameter("@huella", System.Data.SqlDbType.Char, 64) { Value = huella });
        return await comando.ExecuteScalarAsync(cancelacion) as Guid?;
    }

    private ProblemasReceptor Rechazar(string codigo, string motivo)
    {
        var problema = ProblemasReceptor.Crear(codigo);
        registro.LogWarning("Pedido de lotes rechazado con {Codigo}: {Motivo}. CorrelationId {CorrelationId}", codigo, motivo, problema.CorrelationId);
        return problema;
    }
}
