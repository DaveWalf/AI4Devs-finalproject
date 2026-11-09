using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Intelektron.INComedor.Kiosk.Demostracion;
using Intelektron.INComedor.Kiosk.Red;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La prueba de conexión por etapas (conectividad-puesto de configurar-puesto-por-pestanas, D12) contra el receptor real en
/// modo demostración, el mismo de <see cref="ModoDemostracionReceptorTests"/>: nombre, conexión, TLS mutuo con los
/// certificados del puesto y la respuesta del receptor a la consulta de un lote que no existe, sin enviar ningún lote.
/// </summary>
public class PruebaConexionTests(ReceptorDeDemostracion receptor) : IClassFixture<ReceptorDeDemostracion>
{
    private static readonly EtapaConexion[] Etapas = [EtapaConexion.Nombre, EtapaConexion.Conexion, EtapaConexion.Tls, EtapaConexion.Receptor];

    [Fact(DisplayName = "conectividad-puesto · El receptor de demostración responde: las cuatro etapas funcionaron, con la dirección probada")]
    public async Task Las_cuatro_etapas()
    {
        var resultado = await new PruebaConexion(receptor.Direccion, receptor.Carpeta).ProbarAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(receptor.Direccion, resultado.Direccion);
        Assert.False(resultado.SinCertificados);
        Assert.Equal(Etapas, resultado.Etapas.Select(e => e.Etapa));
        Assert.All(resultado.Etapas, e => Assert.Equal(EstadoEtapa.Funciono, e.Estado));
        Assert.True(resultado.Funciono);
    }

    [Fact(DisplayName = "conectividad-puesto · Puerto cerrado: el nombre funcionó, la conexión falló con su motivo y las dos siguientes no se probaron")]
    public async Task Puerto_cerrado()
    {
        var direccion = new UriBuilder(receptor.Direccion) { Port = PuertoLibre() }.Uri;

        var resultado = await new PruebaConexion(direccion, receptor.Carpeta).ProbarAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            [EstadoEtapa.Funciono, EstadoEtapa.Fallo, EstadoEtapa.NoProbada, EstadoEtapa.NoProbada],
            resultado.Etapas.Select(e => e.Estado));
        Assert.False(string.IsNullOrWhiteSpace(resultado.Etapas[1].Motivo));
        Assert.False(resultado.Funciono);
    }

    [Fact(DisplayName = "conectividad-puesto · Un nombre que no se resuelve: con un nombre .invalid, la etapa del nombre falló y las demás no se probaron")]
    public async Task Nombre_que_no_se_resuelve()
    {
        var resultado = await new PruebaConexion(new Uri("https://receptor-inexistente.invalid:5001/"), receptor.Carpeta)
            .ProbarAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(
            [EstadoEtapa.Fallo, EstadoEtapa.NoProbada, EstadoEtapa.NoProbada, EstadoEtapa.NoProbada],
            resultado.Etapas.Select(e => e.Estado));
        Assert.False(string.IsNullOrWhiteSpace(resultado.Etapas[0].Motivo));
    }

    [Fact(DisplayName = "conectividad-puesto · Sin certificados: la pestaña dice que el puesto no los tiene y atiende sin receptor, y «Probar conexión» no prueba nada")]
    public async Task Sin_certificados()
    {
        using var puesto = new PuestoDePrueba();
        Assert.False(puesto.Conexion.TieneCertificados);
        var resultado = await puesto.Conexion.ProbarAsync(Xunit.TestContext.Current.CancellationToken);
        Assert.True(resultado.SinCertificados);
        Assert.All(resultado.Etapas, e => Assert.Equal(EstadoEtapa.NoProbada, e.Estado));

        var pantalla = await PestanaRedTests.AbrirRed(puesto);

        Assert.Equal(MensajesAtencion.SinCertificados, pantalla.Find("#sin-certificados").TextContent.Trim());
        Assert.True(pantalla.Find("#probar-conexion").HasAttribute("disabled"));
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "CONNECTIVITY_TESTED");
    }

    [Fact(DisplayName = "conectividad-puesto · La prueba no envía lotes: con un evento pendiente en la cola, después de probar sigue pendiente y el receptor no recibió ningún lote")]
    public async Task La_prueba_no_envia_lotes()
    {
        using var archivo = new ArchivoTemporal();
        using var atencion = new ServicioAtencion(archivo.Ruta, new RelojFijo("2026-11-06T15:00:00Z"), new ImpresoraDePrueba());
        Assert.Equal(EstadoPreparacion.Lista, atencion.Preparar());
        ConfirmarConElCodigo0001(atencion);
        Assert.Equal("PENDING", EstadoDelEvento(atencion));
        var lotesAntes = await receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorAcusesLotes");
        var eventosAntes = await receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorEventosEntrada");

        var resultado = await new PruebaConexion(receptor.Direccion, receptor.Carpeta).ProbarAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.True(resultado.Funciono);
        Assert.Equal("PENDING", EstadoDelEvento(atencion));
        Assert.Equal(lotesAntes, await receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorAcusesLotes"));
        Assert.Equal(eventosAntes, await receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorEventosEntrada"));
    }

    [Fact(DisplayName = "conectividad-puesto · La prueba en la bitácora: «Probar conexión» muestra la dirección y las cuatro etapas, y la general tiene CONNECTIVITY_TESTED con la dirección, las etapas, el actor nulo y MAINTENANCE_MODE, sin certificados")]
    public async Task La_prueba_en_la_bitacora()
    {
        using var puesto = new PuestoDePrueba { Conexion = new PruebaConexion(receptor.Direccion, receptor.Carpeta) };
        var pantalla = await PestanaRedTests.AbrirRed(puesto);
        Assert.Contains(receptor.Direccion.ToString(), pantalla.Find("#direccion-receptor").TextContent);

        await pantalla.Find("#probar-conexion").ClickAsync(new());

        pantalla.WaitForAssertion(() => Assert.Equal(4, pantalla.FindAll("#etapas-conexion li.funciono").Count), TimeSpan.FromSeconds(30));
        var linea = Assert.Single(puesto.General(), l => Tipo(l) == "CONNECTIVITY_TESTED");
        var prueba = ContextoDe(linea);
        Assert.Equal(receptor.Direccion.ToString(), prueba.GetProperty("Address").GetString());
        Assert.Equal("PASSED", prueba.GetProperty("Result").GetString());
        Assert.Equal(["NAME", "CONNECTION", "MUTUAL_TLS", "RECEPTOR"], prueba.GetProperty("Stages").EnumerateArray().Select(e => e.GetProperty("Stage").GetString()));
        Assert.All(prueba.GetProperty("Stages").EnumerateArray(), e => Assert.Equal("PASSED", e.GetProperty("Result").GetString()));
        Assert.Equal("MAINTENANCE_MODE", prueba.GetProperty("Access").GetString());
        Assert.True(SinActor(prueba));

        using var certificado = X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(receptor.Carpeta, "puesto.pfx"), null);
        var texto = linea.GetRawText();
        Assert.DoesNotContain(certificado.GetCertHashString(HashAlgorithmName.SHA256), texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(certificado.Thumbprint, texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Puesto Demostracion", texto, StringComparison.Ordinal);
        Assert.DoesNotContain(".pfx", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Convert.ToBase64String(certificado.RawData)[..40], texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "conectividad-puesto · Un puerto que no habla TLS: el nombre y la conexión funcionaron, el TLS mutuo falló con su motivo y la respuesta del receptor no se probó")]
    public async Task Un_puerto_sin_tls()
    {
        var resultado = await ProbarContraUnServidor(async flujo =>
            await flujo.WriteAsync("HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n"u8.ToArray(), Xunit.TestContext.Current.CancellationToken));

        Assert.Equal(
            [EstadoEtapa.Funciono, EstadoEtapa.Funciono, EstadoEtapa.Fallo, EstadoEtapa.NoProbada],
            resultado.Etapas.Select(e => e.Estado));
        Assert.False(string.IsNullOrWhiteSpace(resultado.Etapas[2].Motivo));
    }

    [Fact(DisplayName = "conectividad-puesto · El TLS se establece y el receptor no responde: el TLS mutuo funcionó y la respuesta del receptor falló, con su motivo")]
    public async Task Tls_sin_respuesta_del_receptor()
    {
        using var certificado = X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(receptor.Carpeta, "receptor.pfx"), null);

        var resultado = await ProbarContraUnServidor(async flujo =>
        {
            await using var seguro = new System.Net.Security.SslStream(flujo, leaveInnerStreamOpen: true);
            await seguro.AuthenticateAsServerAsync(new System.Net.Security.SslServerAuthenticationOptions
            {
                ServerCertificate = certificado,
                ClientCertificateRequired = true,
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            }, Xunit.TestContext.Current.CancellationToken);
        });

        Assert.Equal(
            [EstadoEtapa.Funciono, EstadoEtapa.Funciono, EstadoEtapa.Funciono, EstadoEtapa.Fallo],
            resultado.Etapas.Select(e => e.Estado));
        Assert.False(string.IsNullOrWhiteSpace(resultado.Etapas[3].Motivo));
    }

    /// <summary>
    /// Un servidor en loopback que acepta la conexión de la etapa 2 y, en la del cliente HTTP, hace lo que se le pide y
    /// cierra sin responder HTTP.
    /// </summary>
    private async Task<ResultadoConexion> ProbarContraUnServidor(Func<NetworkStream, Task> atender)
    {
        using var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();
        var servidor = Task.Run(async () =>
        {
            using (await escucha.AcceptTcpClientAsync(Xunit.TestContext.Current.CancellationToken))
            {
            }

            using var cliente = await escucha.AcceptTcpClientAsync(Xunit.TestContext.Current.CancellationToken);
            try
            {
                await atender(cliente.GetStream());
            }
            catch (Exception e) when (e is IOException or System.Security.Authentication.AuthenticationException)
            {
                // El cliente cortó: es lo que se prueba del otro lado.
            }
        });
        var direccion = new UriBuilder(receptor.Direccion) { Port = ((IPEndPoint)escucha.LocalEndpoint).Port }.Uri;

        var resultado = await new PruebaConexion(direccion, receptor.Carpeta).ProbarAsync(Xunit.TestContext.Current.CancellationToken);
        await servidor;
        return resultado;
    }

    private static int PuertoLibre()
    {
        var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();
        var puerto = ((IPEndPoint)escucha.LocalEndpoint).Port;
        escucha.Stop();
        return puerto;
    }

    private static void ConfirmarConElCodigo0001(ServicioAtencion atencion)
    {
        var sesion = atencion.Identificacion.Identificar("0001", PropositoSesion.Consumption);
        Assert.True(sesion.Abierta);
        var opcion = atencion.Catalogo.Leer().First();
        var resultado = atencion.Unidad.Confirmar(new SolicitudConfirmacion(
            Guid.NewGuid(), sesion.IdentitySessionId!.Value, sesion.PersonId!.Value, opcion.Id, opcion.Version, 1));
        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
    }

    private static string? EstadoDelEvento(ServicioAtencion atencion)
    {
        using var conexion = atencion.Base.AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT comevsEstado FROM tbComedorEventosSalida";
        return comando.ExecuteScalar() as string;
    }
}
