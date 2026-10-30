namespace Intelektron.INComedor.Sync.Api.Tests;

/// <summary>
/// Prueba de humo de los auxiliares (tarea 4.2 de recibir-lotes-idempotente): TLS mutuo real contra Kestrel, con un
/// endpoint de prueba que devuelve la huella del certificado que vio el servidor, y la base temporal de LocalDB.
/// </summary>
public class TlsMutuoDePruebaTests(ReceptorConEndpointDePrueba receptor) : IClassFixture<ReceptorConEndpointDePrueba>
{
    [Fact(DisplayName = "D2 · TLS mutuo real: el servidor ve el certificado de equipo que presentó el cliente")]
    public async Task El_servidor_ve_el_certificado_del_puesto()
    {
        using var cliente = receptor.CrearCliente(receptor.Certificados.Puesto);

        var huella = await cliente.GetStringAsync(ReceptorConEndpointDePrueba.Ruta, TestContext.Current.CancellationToken);

        Assert.Equal("https", receptor.Direccion.Scheme);
        Assert.Equal(CertificadosDePrueba.Huella(receptor.Certificados.Puesto), huella);
    }

    [Fact(DisplayName = "D2 · Un certificado de otra autoridad no corta el handshake: el servidor lo ve y la validación queda para la aplicación")]
    public async Task Un_certificado_ajeno_llega_a_la_aplicacion()
    {
        using var cliente = receptor.CrearCliente(receptor.Certificados.PuestoDeOtraAutoridad);

        var huella = await cliente.GetStringAsync(ReceptorConEndpointDePrueba.Ruta, TestContext.Current.CancellationToken);

        Assert.Equal(CertificadosDePrueba.Huella(receptor.Certificados.PuestoDeOtraAutoridad), huella);
    }

    [Fact(DisplayName = "D2 · Sin certificado de cliente el handshake sigue y el servidor no ve ninguno")]
    public async Task Sin_certificado_el_servidor_no_ve_ninguno()
    {
        using var cliente = receptor.CrearCliente(null);

        var huella = await cliente.GetStringAsync(ReceptorConEndpointDePrueba.Ruta, TestContext.Current.CancellationToken);

        Assert.Equal(ReceptorConEndpointDePrueba.SinCertificado, huella);
    }

    [Fact(DisplayName = "D1 · La base temporal de LocalDB de la clase existe y responde")]
    public async Task La_base_temporal_responde()
    {
        Assert.Equal(receptor.NombreBase, await receptor.EscalarAsync<string>("SELECT DB_NAME()"));
    }
}
