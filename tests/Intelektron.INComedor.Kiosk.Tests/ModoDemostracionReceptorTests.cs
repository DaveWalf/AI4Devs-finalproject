using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Intelektron.INComedor.Kiosk.Demostracion;
using Intelektron.INComedor.Kiosk.Sincronizacion;
using Intelektron.INComedor.Sync.Api.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Receptor y puesto de demostración conectados (D11 y D12 de recibir-lotes-idempotente): el receptor real arrancado en
/// modo demostración, con una carpeta temporal en lugar de %LOCALAPPDATA%\INComedor\demo\certificados y la
/// configuración de Kestrel del propio receptor, y la atención real del puesto.
/// </summary>
public class ModoDemostracionReceptorTests(ReceptorDeDemostracion receptor) : IClassFixture<ReceptorDeDemostracion>, IDisposable
{
    private static readonly Guid PuestoDeLaSemilla = Guid.Parse("00000000-0000-4000-8000-000000000002");

    private readonly ArchivoTemporal _archivo = new();
    private readonly RelojFijo _reloj = new("2026-10-30T15:00:00Z");
    private readonly ImpresoraDePrueba _impresora = new();

    public void Dispose()
    {
        _archivo.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "AT13 · AGENTS.md · Primer arranque del receptor de demostración: genera la autoridad, el certificado del receptor y el del puesto fuera del repo, y registra el puesto de la demostración")]
    public async Task El_primer_arranque_genera_los_certificados_y_registra_el_puesto()
    {
        Assert.False(receptor.CarpetaExistiaAntes);
        Assert.All(new[] { "autoridad.pfx", "receptor.pfx", "puesto.pfx" }, a => Assert.True(File.Exists(Path.Combine(receptor.Carpeta, a)), a));
        using var puesto = X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(receptor.Carpeta, "puesto.pfx"), null);
        Assert.Equal("CN=INComedor Puesto Demostracion", puesto.Subject);
        Assert.Equal(PuestoDeLaSemilla, await receptor.PuestoRegistradoAsync(puesto.GetCertHashString(HashAlgorithmName.SHA256)));
        Assert.Equal(PuestoDeLaSemilla, PuestoDeLaBaseDelKiosk());

        var repo = RaizDelRepo();
        Assert.False(receptor.Carpeta.StartsWith(repo, StringComparison.OrdinalIgnoreCase));
        Assert.Empty(Directory.EnumerateFiles(repo, "*.pfx", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(repo, "*.cer", SearchOption.AllDirectories));
    }

    [Fact(DisplayName = "HU02-C · AT27 · Puesto con receptor: un ciclo de envío deja el consumo consolidado en el receptor y la sincronización del puesto al día")]
    public async Task Con_receptor_el_consumo_llega_y_queda_al_dia()
    {
        var cliente = ClienteReceptorHttp.DesdeCarpetaDemostracion(receptor.Carpeta, receptor.Direccion);
        Assert.NotNull(cliente);
        using var atencion = new ServicioAtencion(_archivo.Ruta, _reloj, _impresora, cliente);
        Assert.Equal(EstadoPreparacion.Lista, atencion.Preparar());
        var consumo = ConfirmarConElCodigo0001(atencion);

        await atencion.EnviarAhoraAsync(TestContext.Current.CancellationToken);

        Assert.True(atencion.Sincronizacion.AlDia);
        Assert.Equal(1, await receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorConsumosConsolidados WHERE comconCodigo = @con", ("@con", consumo)));
    }

    [Fact(DisplayName = "AT01 · HU02-G · Puesto sin certificados: la intención se decide como antes, el estado informa que no hay receptor configurado y el evento queda en PENDING")]
    public async Task Sin_certificados_la_atencion_sigue_sin_receptor()
    {
        var carpetaVacia = Path.Combine(Path.GetTempPath(), "incomedor-tests", $"sin-certificados-{Guid.NewGuid():N}");
        var cliente = ClienteReceptorHttp.DesdeCarpetaDemostracion(carpetaVacia, receptor.Direccion);
        Assert.Null(cliente);
        using var atencion = new ServicioAtencion(_archivo.Ruta, _reloj, _impresora, cliente);
        Assert.Equal(EstadoPreparacion.Lista, atencion.Preparar());

        ConfirmarConElCodigo0001(atencion);
        await atencion.EnviarAhoraAsync(TestContext.Current.CancellationToken);

        Assert.False(atencion.Sincronizacion.ReceptorConfigurado);
        using var conexion = atencion.Base.AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT comevsEstado FROM tbComedorEventosSalida";
        Assert.Equal("PENDING", comando.ExecuteScalar());
    }

    private Guid ConfirmarConElCodigo0001(ServicioAtencion atencion)
    {
        var sesion = atencion.Identificacion.Identificar("0001", PropositoSesion.Consumption);
        Assert.True(sesion.Abierta);
        var opcion = atencion.Catalogo.Leer().First();
        var resultado = atencion.Unidad.Confirmar(new SolicitudConfirmacion(
            Guid.NewGuid(), sesion.IdentitySessionId!.Value, sesion.PersonId!.Value, opcion.Id, opcion.Version, 1));
        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        return resultado.ConsumptionId!.Value;
    }

    private Guid PuestoDeLaBaseDelKiosk()
    {
        using var atencion = new ServicioAtencion(_archivo.Ruta, _reloj, _impresora);
        Assert.Equal(EstadoPreparacion.Lista, atencion.Preparar());
        using var conexion = atencion.Base.AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT compueCodigo FROM tbComedorConfiguracionPuestos";
        return Guid.Parse((string)comando.ExecuteScalar()!);
    }

    private static string RaizDelRepo()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "INComedor.sln")))
        {
            carpeta = carpeta.Parent;
        }

        return carpeta!.FullName;
    }
}

/// <summary>
/// El receptor real en modo demostración: genera sus certificados en una carpeta temporal, usa una base temporal en la
/// instancia de INCOMEDOR_SQLSERVER y escucha con la configuración de Kestrel del propio receptor (D12).
/// </summary>
public sealed class ReceptorDeDemostracion : IAsyncLifetime
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "incomedor-tests", $"demo-{Guid.NewGuid():N}");
    private WebApplicationFactory<Program>? _fabrica;

    public string Carpeta => Path.Combine(_raiz, "certificados");

    public string NombreBase { get; } = $"INComedorPruebas_{Guid.NewGuid():N}";

    public bool CarpetaExistiaAntes { get; private set; }

    public Uri Direccion { get; private set; } = null!;

    private string CadenaConexion => $"Server={ReceptorConKestrel.Instancia};Database={NombreBase};Integrated Security=true;Encrypt=false;Connect Timeout=60";

    public ValueTask InitializeAsync()
    {
        CarpetaExistiaAntes = Directory.Exists(Carpeta);
        _fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(constructor =>
        {
            constructor.UseSetting("Receptor:Demostracion", "true");
            constructor.UseSetting("Receptor:CarpetaDemostracion", Carpeta);
            constructor.UseSetting("Receptor:BaseDemostracion", NombreBase);
            constructor.UseSetting("Receptor:PuertoDemostracion", "0");
            constructor.UseSetting("INCOMEDOR_SQLSERVER", ReceptorConKestrel.Instancia);
        });
        _fabrica.UseKestrel();
        _fabrica.StartServer();
        var direcciones = _fabrica.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>();
        Direccion = new Uri(direcciones.Addresses.Single(d => d.StartsWith("https://", StringComparison.Ordinal)));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_fabrica is not null)
        {
            await _fabrica.DisposeAsync();
        }

        SqlConnection.ClearAllPools();
        await using (var conexion = new SqlConnection($"Server={ReceptorConKestrel.Instancia};Database=master;Integrated Security=true;Encrypt=false;Connect Timeout=60"))
        {
            await conexion.OpenAsync();
            await using var comando = conexion.CreateCommand();
            comando.CommandText = $"IF DB_ID(N'{NombreBase}') IS NOT NULL BEGIN ALTER DATABASE [{NombreBase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{NombreBase}]; END";
            await comando.ExecuteNonQueryAsync();
        }

        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, recursive: true);
        }
    }

    public async Task<Guid?> PuestoRegistradoAsync(string huella)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT compueCodigo FROM tbComedorPuestos WHERE compueIdentidadCertificado = @huella";
        comando.Parameters.AddWithValue("@huella", huella);
        return await comando.ExecuteScalarAsync() as Guid?;
    }

    public async Task<T> EscalarAsync<T>(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor);
        }

        return (T)Convert.ChangeType((await comando.ExecuteScalarAsync())!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }
}
