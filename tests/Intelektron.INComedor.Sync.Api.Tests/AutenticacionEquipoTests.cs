using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Intelektron.INComedor.Sync.Api.Tests;

/// <summary>
/// Equipo autenticado por certificado (D2 de recibir-lotes-idempotente), contra Kestrel real con TLS mutuo. El vencido
/// y el de otra autoridad también se registran, para que sólo la cadena y la vigencia puedan rechazarlos.
/// </summary>
public class AutenticacionEquipoTests(ReceptorConKestrel receptor) : IClassFixture<ReceptorConKestrel>
{
    private const string TipoProblema = "application/problem+json";

    [Fact(DisplayName = "AT13 · Pedido sin certificado: 401 problem+json EQUIPMENT_AUTH_REQUIRED y el receptor no guarda nada")]
    public async Task Sin_certificado_responde_401()
    {
        using var cliente = receptor.CrearCliente(null);

        using var respuesta = await LotesDePrueba.EnviarAsync(cliente, LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado()));

        await AfirmarProblemaAsync(respuesta, HttpStatusCode.Unauthorized, "EQUIPMENT_AUTH_REQUIRED");
        await AfirmarQueNoSeGuardoNadaAsync();
    }

    [Fact(DisplayName = "HU02-G · AT13 · Certificado vencido de la autoridad confiable y registrado: 401 EQUIPMENT_AUTH_REQUIRED y nada guardado")]
    public async Task Certificado_vencido_responde_401()
    {
        await RegistrarAsync(receptor.Certificados.PuestoVencido);
        using var cliente = receptor.CrearCliente(receptor.Certificados.PuestoVencido);

        using var respuesta = await LotesDePrueba.EnviarAsync(cliente, LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado()));

        await AfirmarProblemaAsync(respuesta, HttpStatusCode.Unauthorized, "EQUIPMENT_AUTH_REQUIRED");
        await AfirmarQueNoSeGuardoNadaAsync();
    }

    [Fact(DisplayName = "AT13 · OWASP A07 · Certificado vigente con la misma identidad que uno registrado, pero de otra autoridad: 401 y nada guardado")]
    public async Task Certificado_de_otra_autoridad_responde_401()
    {
        await RegistrarAsync(receptor.Certificados.Puesto);
        await RegistrarAsync(receptor.Certificados.PuestoDeOtraAutoridad);
        Assert.Equal(receptor.Certificados.Puesto.Subject, receptor.Certificados.PuestoDeOtraAutoridad.Subject);
        using var cliente = receptor.CrearCliente(receptor.Certificados.PuestoDeOtraAutoridad);

        using var respuesta = await LotesDePrueba.EnviarAsync(cliente, LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado()));

        await AfirmarProblemaAsync(respuesta, HttpStatusCode.Unauthorized, "EQUIPMENT_AUTH_REQUIRED");
        await AfirmarQueNoSeGuardoNadaAsync();
    }

    [Fact(DisplayName = "AT13 · Certificado vigente de la autoridad confiable que no está registrado: 403 EQUIPMENT_FORBIDDEN y nada guardado")]
    public async Task Certificado_sin_registrar_responde_403()
    {
        using var cliente = receptor.CrearCliente(receptor.Certificados.PuestoSinRegistrar);

        using var respuesta = await LotesDePrueba.EnviarAsync(cliente, LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado()));

        await AfirmarProblemaAsync(respuesta, HttpStatusCode.Forbidden, "EQUIPMENT_FORBIDDEN");
        await AfirmarQueNoSeGuardoNadaAsync();
    }

    private async Task RegistrarAsync(X509Certificate2 certificado)
    {
        var huella = CertificadosDePrueba.Huella(certificado);
        if (await receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorPuestos WHERE compueIdentidadCertificado = @huella", ("@huella", huella)) == 0)
        {
            await receptor.RegistrarPuestoAsync(Guid.NewGuid(), certificado);
        }
    }

    private static async Task AfirmarProblemaAsync(HttpResponseMessage respuesta, HttpStatusCode estado, string codigo)
    {
        Assert.Equal(estado, respuesta.StatusCode);
        Assert.Equal(TipoProblema, respuesta.Content.Headers.ContentType?.MediaType);
        using var cuerpo = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(codigo, cuerpo.RootElement.GetProperty("code").GetString());
        Assert.Equal((int)estado, cuerpo.RootElement.GetProperty("status").GetInt32());
    }

    private async Task AfirmarQueNoSeGuardoNadaAsync()
    {
        Assert.Equal(0, await receptor.ContarAsync("tbComedorAcusesLotes"));
        Assert.Equal(0, await receptor.ContarAsync("tbComedorEventosEntrada"));
        Assert.Equal(0, await receptor.ContarAsync("tbComedorConsumosConsolidados"));
    }
}
