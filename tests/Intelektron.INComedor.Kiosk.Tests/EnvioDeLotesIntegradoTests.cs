using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Demostracion;
using Intelektron.INComedor.Kiosk.Sincronizacion;
using Intelektron.INComedor.Sync.Api.Tests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// AT27 y AT13 de punta a punta (D13 de recibir-lotes-idempotente): la base SQLite real del puesto, ClienteReceptorHttp
/// con el certificado del puesto, y el receptor real con Kestrel, TLS mutuo y SQL Server. Ningún doble.
/// </summary>
public class EnvioDeLotesIntegradoTests(ReceptorQueCorta receptor) : IClassFixture<ReceptorQueCorta>, IDisposable
{
    private readonly ArchivoTemporal _archivo = new();
    private readonly RelojFijo _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose()
    {
        _archivo.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "AT27 · Dos ciclos de envío dan un solo consumo consolidado, y el segundo no le pide nada al receptor")]
    public async Task Dos_ciclos_dan_un_solo_consumo_consolidado()
    {
        var (demostracion, cola) = Puesto();
        var consumo = demostracion.Confirmar().ConsumptionId!.Value;
        await receptor.AsegurarPuestoAsync(receptor.Certificados.Puesto);
        using var cliente = Cliente(receptor.Certificados.Puesto);
        var sincronizador = new SincronizadorPuesto(cola, cliente);

        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);
        var recibos = await receptor.ContarAsync("tbComedorAcusesLotes");
        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.True(sincronizador.Estado.AlDia);
        Assert.Equal(recibos, await receptor.ContarAsync("tbComedorAcusesLotes"));
        Assert.Equal(1, await ConsolidadosAsync(consumo));
    }

    [Fact(DisplayName = "AT27 · HU02-D · Un corte después del commit del receptor se resuelve con la consulta: un recibo, un consumo consolidado y la cola al día")]
    public async Task Un_corte_despues_del_commit_se_resuelve_con_la_consulta()
    {
        var (demostracion, cola) = Puesto();
        var consumo = demostracion.Confirmar().ConsumptionId!.Value;
        await receptor.AsegurarPuestoAsync(receptor.Certificados.Puesto);
        using var cliente = Cliente(receptor.Certificados.Puesto);
        var sincronizador = new SincronizadorPuesto(cola, cliente);
        receptor.CortarProximoEnvio = true;

        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.Equal("NETWORK", sincronizador.Estado.UltimoError);
        Assert.Equal(1, sincronizador.Estado.Pendientes);
        Assert.Equal(1, await ConsolidadosAsync(consumo));
        var lote = cola.Preparar()!.BatchId;

        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.True(sincronizador.Estado.AlDia);
        Assert.Equal(1, await ConsolidadosAsync(consumo));
        Assert.Equal(1, await receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorAcusesLotes WHERE comlotCodigo = @lot", ("@lot", lote)));
    }

    [Fact(DisplayName = "AT13 · HU02-G · Un certificado vencido retiene los eventos, el estado informa la credencial y la confirmación siguiente es AUTHORIZED")]
    public async Task Un_certificado_vencido_retiene_los_eventos()
    {
        var (demostracion, cola) = Puesto();
        Assert.Equal(TipoResultado.Autorizada, demostracion.Confirmar().Tipo);
        await receptor.AsegurarPuestoAsync(receptor.Certificados.PuestoVencido);
        using var cliente = Cliente(receptor.Certificados.PuestoVencido);
        var sincronizador = new SincronizadorPuesto(cola, cliente);

        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.True(sincronizador.Estado.ErrorDeCredencial);
        Assert.Equal("401 EQUIPMENT_AUTH_REQUIRED", sincronizador.Estado.UltimoError);
        Assert.Equal(1, sincronizador.Estado.Pendientes);
        _reloj.Ahora = _reloj.Ahora.AddDays(1);
        Assert.Equal(TipoResultado.Autorizada, demostracion.Confirmar().Tipo);
    }

    [Fact(DisplayName = "AT01 · HU02-C · Receptor inalcanzable: la confirmación termina con su decisión sin esperar al envío y el estado informa los pendientes")]
    public async Task Con_el_receptor_inalcanzable_la_atencion_sigue()
    {
        var (demostracion, cola) = Puesto();
        Assert.Equal(TipoResultado.Autorizada, demostracion.Confirmar().Tipo);
        var colgado = new TcpListener(IPAddress.Loopback, 0);
        colgado.Start();
        try
        {
            var puerto = ((IPEndPoint)colgado.LocalEndpoint).Port;
            using var cliente = new ClienteReceptorHttp(new Uri($"https://127.0.0.1:{puerto}/"), receptor.Certificados.Puesto,
                receptor.Certificados.Autoridad, TimeSpan.FromSeconds(3));
            var sincronizador = new SincronizadorPuesto(cola, cliente);

            var ciclo = sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);
            _reloj.Ahora = _reloj.Ahora.AddDays(1);
            var decision = demostracion.Confirmar();

            Assert.Equal(TipoResultado.Autorizada, decision.Tipo);
            Assert.False(ciclo.IsCompleted);
            await ciclo;
            Assert.Equal("NETWORK", sincronizador.Estado.UltimoError);
            Assert.Equal(2, sincronizador.Estado.Pendientes);
        }
        finally
        {
            colgado.Stop();
        }
    }

    [Fact(DisplayName = "OWASP A07 · D11 · El puesto no le envía nada a un servidor cuyo certificado no emitió su autoridad: el ciclo queda sin respuesta y el receptor no recibe el lote")]
    public async Task El_puesto_rechaza_un_servidor_de_otra_autoridad()
    {
        var (demostracion, cola) = Puesto();
        var consumo = demostracion.Confirmar().ConsumptionId!.Value;
        await receptor.AsegurarPuestoAsync(receptor.Certificados.Puesto);
        using var cliente = new ClienteReceptorHttp(receptor.Direccion, receptor.Certificados.Puesto, receptor.Certificados.OtraAutoridad);
        var sincronizador = new SincronizadorPuesto(cola, cliente);

        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.Equal("NETWORK", sincronizador.Estado.UltimoError);
        Assert.Equal(1, sincronizador.Estado.Pendientes);
        Assert.Equal(0, await ConsolidadosAsync(consumo));
    }

    private (ServicioDemostracion Demostracion, ColaSincronizacion Cola) Puesto()
    {
        var demostracion = new ServicioDemostracion(_archivo.Ruta, _reloj);
        Assert.Equal(EstadoPreparacion.Lista, demostracion.Preparar());
        return (demostracion, new ColaSincronizacion(BaseComedor.Abrir(_archivo.Ruta), _reloj));
    }

    private ClienteReceptorHttp Cliente(X509Certificate2 certificado) =>
        new(receptor.Direccion, certificado, receptor.Certificados.Autoridad);

    private Task<int> ConsolidadosAsync(Guid consumo) =>
        receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorConsumosConsolidados WHERE comconCodigo = @con", ("@con", consumo));
}

/// <summary>
/// El receptor real que, si se le pide, deja hacer commit al próximo envío y corta la conexión antes de responder: la
/// respuesta se escribe en un buffer y nunca sale (D13).
/// </summary>
public sealed class ReceptorQueCorta : ReceptorConKestrel
{
    private volatile bool _cortarProximoEnvio;

    public bool CortarProximoEnvio
    {
        get => _cortarProximoEnvio;
        set => _cortarProximoEnvio = value;
    }

    protected override void ConfigurarServicios(IServiceCollection servicios) =>
        servicios.AddSingleton<IStartupFilter>(new Corte(this));

    private sealed class Corte(ReceptorQueCorta receptor) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => aplicacion =>
        {
            aplicacion.Use(async (contexto, proximo) =>
            {
                if (!HttpMethods.IsPost(contexto.Request.Method) || !receptor.CortarProximoEnvio)
                {
                    await proximo(contexto);
                    return;
                }

                receptor.CortarProximoEnvio = false;
                var original = contexto.Response.Body;
                contexto.Response.Body = new MemoryStream();
                await proximo(contexto);
                contexto.Abort();
                contexto.Response.Body = original;
            });
            siguiente(aplicacion);
        };
    }
}
