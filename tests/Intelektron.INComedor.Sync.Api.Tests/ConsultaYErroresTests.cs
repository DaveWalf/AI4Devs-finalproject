using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Sync.Api.Tests;

/// <summary>
/// Consulta del acuse original y errores que no reflejan la entrada (D3, D5 y D8 de recibir-lotes-idempotente), más la
/// prueba de contrato: cada cuerpo de error y de acuse cumple ProblemDetails o CompletedBatchAck de api/openapi.yaml.
/// </summary>
public class ConsultaYErroresTests(ReceptorConKestrel receptor) : IClassFixture<ReceptorConKestrel>
{
    private const string CadenaReconocible = "CADENA-RECONOCIBLE-7F3A9C";

    [Fact(DisplayName = "AT27 · Consulta tras perder el acuse: GET responde 200 con el acuse original, byte por byte, y su X-Correlation-Id")]
    public async Task La_consulta_devuelve_el_acuse_original()
    {
        using var cliente = await ClienteAsync(receptor.Certificados.Puesto);
        var lote = LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado());
        using var envio = await LotesDePrueba.EnviarAsync(cliente, lote);
        var original = await envio.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

        using var consulta = await ConsultarAsync(cliente, (string)lote["BatchId"]!);

        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
        Assert.Equal("application/json", consulta.Content.Headers.ContentType?.MediaType);
        Assert.Equal(original, await consulta.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Assert.Single(envio.Headers.GetValues("X-Correlation-Id")), Assert.Single(consulta.Headers.GetValues("X-Correlation-Id")));
    }

    [Fact(DisplayName = "AT13 · Lote de otro puesto: 404 BATCH_NOT_FOUND, igual campo por campo, salvo el correlationId, a la de un BatchId que nunca existió")]
    public async Task Un_lote_ajeno_es_desconocido()
    {
        using var propio = await ClienteAsync(receptor.Certificados.Puesto);
        using var ajeno = await ClienteAsync(receptor.Certificados.OtroPuesto);
        var lote = LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado());
        using var envio = await LotesDePrueba.EnviarAsync(propio, lote);
        Assert.Equal(HttpStatusCode.Created, envio.StatusCode);

        using var deOtro = await ConsultarAsync(ajeno, (string)lote["BatchId"]!);
        using var inexistente = await ConsultarAsync(ajeno, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.NotFound, deOtro.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        var uno = (JsonObject)JsonNode.Parse(await deOtro.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        var otro = (JsonObject)JsonNode.Parse(await inexistente.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        Assert.Equal("BATCH_NOT_FOUND", (string)uno["code"]!);
        Assert.NotEqual((string)uno["correlationId"]!, (string)otro["correlationId"]!);
        uno.Remove("correlationId");
        otro.Remove("correlationId");
        Assert.Equal(otro.ToJsonString(), uno.ToJsonString());
        Assert.Equal(deOtro.Content.Headers.ContentType?.ToString(), inexistente.Content.Headers.ContentType?.ToString());
    }

    [Fact(DisplayName = "SubmitSyncBatch · Llega la referencia: la consulta del primer lote devuelve su acuse original, con NEEDS_REVIEW/RELATED_EVENT_PENDING")]
    public async Task La_consulta_conserva_el_acuse_original_al_llegar_la_referencia()
    {
        using var cliente = await ClienteAsync(receptor.Certificados.Puesto);
        var consumo = LotesDePrueba.ConsumoRegistrado();
        var impresion = LotesDePrueba.EventoImpresion(Guid.Parse((string)consumo["ConsumptionId"]!));
        var primero = LotesDePrueba.Lote(impresion);
        using var envio = await LotesDePrueba.EnviarAsync(cliente, primero);
        var original = await envio.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        using var segundo = await LotesDePrueba.EnviarAsync(cliente, LotesDePrueba.Lote(consumo));
        Assert.Equal(HttpStatusCode.Created, segundo.StatusCode);

        using var consulta = await ConsultarAsync(cliente, (string)primero["BatchId"]!);

        Assert.Equal(original, await consulta.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        using var acuse = JsonDocument.Parse(original);
        Assert.Equal(("NEEDS_REVIEW", "RELATED_EVENT_PENDING"), LotesDePrueba.ResultadoDe(acuse, impresion));
    }

    [Fact(DisplayName = "OWASP A03 · GetSyncBatch con un batchId que no es UUID: 400 INVALID_REQUEST")]
    public async Task Un_batch_id_ilegible_es_400()
    {
        using var cliente = await ClienteAsync(receptor.Certificados.Puesto);

        using var respuesta = await ConsultarAsync(cliente, "no-es-un-uuid");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("INVALID_REQUEST", (await ProblemaAsync(respuesta)).GetProperty("code").GetString());
    }

    [Fact(DisplayName = "OWASP A03 y A05 · Cuerpo que no es JSON: 400 INVALID_REQUEST, no 500, y la respuesta no contiene la cadena enviada")]
    public async Task Un_cuerpo_que_no_es_json_es_400_sin_reflejarlo()
    {
        using var cliente = await ClienteAsync(receptor.Certificados.Puesto);

        using var respuesta = await LotesDePrueba.EnviarTextoAsync(cliente, $"<<{CadenaReconocible}", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(CadenaReconocible, texto, StringComparison.Ordinal);
        Assert.Equal("INVALID_REQUEST", JsonDocument.Parse(texto).RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(CadenaReconocible, string.Join(" ", respuesta.Headers.SelectMany(h => h.Value)), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "OWASP A08 · RFC 8785 · Propiedad repetida: el objeto raíz repite BatchId con dos valores y responde 400 INVALID_REQUEST sin guardar nada")]
    public async Task Una_propiedad_repetida_es_400()
    {
        using var cliente = await ClienteAsync(receptor.Certificados.Puesto);
        var lote = LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado());
        var texto = lote.ToJsonString();
        var repetido = "{\"BatchId\":\"" + Guid.NewGuid() + "\"," + texto[1..];

        using var respuesta = await LotesDePrueba.EnviarTextoAsync(cliente, repetido, (string)lote["BatchId"]!);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("INVALID_REQUEST", (await ProblemaAsync(respuesta)).GetProperty("code").GetString());
        Assert.Equal(0, await RecibosAsync(lote));
    }

    [Fact(DisplayName = "OWASP A04 · Cuerpo de más de 1 MiB: 400 INVALID_REQUEST y nada guardado")]
    public async Task Un_cuerpo_de_mas_de_un_mib_es_400()
    {
        using var cliente = await ClienteAsync(receptor.Certificados.Puesto);
        var lote = LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado());
        lote["Relleno"] = new string('x', 1024 * 1024);

        using var respuesta = await LotesDePrueba.EnviarAsync(cliente, lote);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("INVALID_REQUEST", (await ProblemaAsync(respuesta)).GetProperty("code").GetString());
        Assert.Equal(0, await RecibosAsync(lote));
    }

    [Theory(DisplayName = "I-JSON · Un número que el contrato no usa (decimal, exponente o fuera de 2^53 − 1): 400 INVALID_REQUEST")]
    [InlineData("1.5")]
    [InlineData("1e2")]
    [InlineData("9007199254740992")]
    public async Task Un_numero_no_entero_es_400(string numero)
    {
        using var cliente = await ClienteAsync(receptor.Certificados.Puesto);
        var lote = LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado());
        var texto = lote.ToJsonString().Replace("\"SchemaVersion\":1", "\"SchemaVersion\":" + numero, StringComparison.Ordinal);

        using var respuesta = await LotesDePrueba.EnviarTextoAsync(cliente, texto, (string)lote["BatchId"]!);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("INVALID_REQUEST", (await ProblemaAsync(respuesta)).GetProperty("code").GetString());
    }

    [Fact(DisplayName = "D1 · Sin cadena de conexión el receptor arranca y las dos operaciones responden 503 SERVICE_UNAVAILABLE con Retry-After")]
    public async Task Sin_cadena_de_conexion_las_operaciones_responden_503()
    {
        await using var fabrica = new WebApplicationFactory<Program>();
        using var cliente = fabrica.CreateClient();

        using var envio = await LotesDePrueba.EnviarAsync(cliente, LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado()));
        using var consulta = await cliente.GetAsync($"{LotesDePrueba.RutaLotes}/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        foreach (var respuesta in new[] { envio, consulta })
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
            Assert.Equal("SERVICE_UNAVAILABLE", (await ProblemaAsync(respuesta)).GetProperty("code").GetString());
            Assert.Equal(TimeSpan.FromSeconds(2), respuesta.Headers.RetryAfter?.Delta);
        }
    }

    [Fact(DisplayName = "OpenAPI · Prueba de contrato: cada acuse cumple CompletedBatchAck y cada error de las dos operaciones cumple ProblemDetails, con correlationId igual a X-Correlation-Id")]
    public async Task Cada_respuesta_cumple_el_contrato()
    {
        var problemDetails = Esquema.Leer("ProblemDetails");
        var acuseCompleto = Esquema.Leer("CompletedBatchAck");
        var resultadoEvento = Esquema.Leer("EventAck");

        // Control del lector de esquemas: lista cada propiedad declarada y rechaza lo que el contrato no admite.
        Assert.Equal(["code", "correlationId", "detail", "status", "title", "type"], problemDetails.Propiedades.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["BatchId", "CorrelationId", "Results", "ServerAcknowledgedAtUtc", "Status"], acuseCompleto.Propiedades.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["EventId", "ReasonCode", "Status"], resultadoEvento.Propiedades.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(12, problemDetails.Propiedades["code"]!.Length);
        const string Problema = """{"type":"https://api.incomedor.example/problems/x","title":"t","status":400,"code":"INVALID_REQUEST","correlationId":"99999999-9999-4999-8999-999999999993"}""";
        problemDetails.Cumple(JsonDocument.Parse(Problema).RootElement);
        Assert.ThrowsAny<Exception>(() => problemDetails.Cumple(JsonDocument.Parse(Problema.Replace("}", ""","stack":"x"}""")).RootElement));
        Assert.ThrowsAny<Exception>(() => problemDetails.Cumple(JsonDocument.Parse(Problema.Replace("INVALID_REQUEST", "SERVER_ERROR")).RootElement));
        Assert.ThrowsAny<Exception>(() => resultadoEvento.Cumple(JsonDocument.Parse(
            """{"EventId":"88888888-8888-4888-8888-888888888888","Status":"DENIED","ReasonCode":"RECORDED"}""").RootElement));

        using var cliente = await ClienteAsync(receptor.Certificados.Puesto);
        using var sinRegistro = receptor.CrearCliente(receptor.Certificados.PuestoSinRegistrar);
        using var sinCertificado = receptor.CrearCliente(null);
        var impresion = LotesDePrueba.EventoImpresion(Guid.NewGuid());
        var lote = LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado(), LotesDePrueba.ConsumoDenegado(), impresion, LotesDePrueba.IncidenciaConectividad());
        var otroCuerpo = LotesDePrueba.Lote(LotesDePrueba.ConsumoDenegado());
        otroCuerpo["BatchId"] = (string)lote["BatchId"]!;
        var invalido = LotesDePrueba.Lote(LotesDePrueba.ConsumoDenegado());
        invalido["SchemaVersion"] = 2;

        var acuses = new List<HttpResponseMessage>
        {
            await LotesDePrueba.EnviarAsync(cliente, lote),
            await LotesDePrueba.EnviarAsync(cliente, lote),
            await ConsultarAsync(cliente, (string)lote["BatchId"]!),
        };
        var errores = new List<HttpResponseMessage>
        {
            await LotesDePrueba.EnviarTextoAsync(cliente, "no es JSON", Guid.NewGuid().ToString()),
            await LotesDePrueba.EnviarAsync(cliente, LotesDePrueba.Lote(LotesDePrueba.ConsumoDenegado()), Guid.NewGuid().ToString()),
            await LotesDePrueba.EnviarAsync(sinCertificado, LotesDePrueba.Lote(LotesDePrueba.ConsumoDenegado())),
            await LotesDePrueba.EnviarAsync(sinRegistro, LotesDePrueba.Lote(LotesDePrueba.ConsumoDenegado())),
            await ConsultarAsync(cliente, Guid.NewGuid().ToString()),
            await LotesDePrueba.EnviarAsync(cliente, otroCuerpo),
            await LotesDePrueba.EnviarAsync(cliente, invalido),
        };

        Assert.Equal(
            [HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.OK],
            acuses.Select(r => r.StatusCode));
        Assert.Equal(
            [HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity],
            errores.Select(r => r.StatusCode));
        foreach (var respuesta in acuses)
        {
            var acuse = (await LotesDePrueba.LeerAsync(respuesta)).RootElement;
            acuseCompleto.Cumple(acuse);
            Assert.Equal("COMPLETED", acuse.GetProperty("Status").GetString());
            Assert.Matches("Z$", acuse.GetProperty("ServerAcknowledgedAtUtc").GetString()!);
            Assert.All(acuse.GetProperty("Results").EnumerateArray(), resultadoEvento.Cumple);
            Assert.Equal(acuse.GetProperty("CorrelationId").GetString(), Assert.Single(respuesta.Headers.GetValues("X-Correlation-Id")));
        }

        foreach (var respuesta in errores)
        {
            var problema = await ProblemaAsync(respuesta);
            problemDetails.Cumple(problema);
            Assert.Equal((int)respuesta.StatusCode, problema.GetProperty("status").GetInt32());
            Assert.True(Uri.TryCreate(problema.GetProperty("type").GetString(), UriKind.Absolute, out _));
            Assert.False(problema.TryGetProperty("detail", out _));
            Assert.Equal(problema.GetProperty("correlationId").GetString(), Assert.Single(respuesta.Headers.GetValues("X-Correlation-Id")));
        }

        foreach (var respuesta in acuses.Concat(errores))
        {
            respuesta.Dispose();
        }
    }

    private async Task<HttpClient> ClienteAsync(System.Security.Cryptography.X509Certificates.X509Certificate2 certificado)
    {
        await receptor.AsegurarPuestoAsync(certificado);
        return receptor.CrearCliente(certificado);
    }

    private static Task<HttpResponseMessage> ConsultarAsync(HttpClient cliente, string batchId) =>
        cliente.GetAsync($"{LotesDePrueba.RutaLotes}/{batchId}", TestContext.Current.CancellationToken);

    private Task<int> RecibosAsync(JsonObject lote) =>
        receptor.EscalarAsync<int>("SELECT COUNT(*) FROM tbComedorAcusesLotes WHERE comlotCodigo = @lot", ("@lot", LotesDePrueba.BatchId(lote)));

    private static async Task<JsonElement> ProblemaAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
    }

    /// <summary>
    /// Un esquema de components/schemas leído del texto de api/openapi.yaml: sus requeridos, sus propiedades declaradas
    /// (todos estos esquemas son additionalProperties: false) y los enums de cada propiedad.
    /// </summary>
    private sealed record Esquema(string Nombre, string[] Requeridos, Dictionary<string, string[]?> Propiedades)
    {
        public static Esquema Leer(string nombre)
        {
            var texto = File.ReadAllText(RutaOpenApi()).ReplaceLineEndings("\n");
            var inicio = texto.IndexOf($"\n    {nombre}:\n", StringComparison.Ordinal);
            Assert.True(inicio >= 0, $"No se encontró el esquema {nombre} en el OpenAPI.");
            var resto = texto[(inicio + 1)..];
            var fin = Regex.Match(resto[1..], @"\n    \S").Index + 1;
            var bloque = resto[..fin];
            Assert.Contains("additionalProperties: false", bloque, StringComparison.Ordinal);
            var requeridos = Regex.Match(bloque, @"\n      required: \[(?<campos>[^\]]+)\]").Groups["campos"].Value
                .Split(',').Select(c => c.Trim()).ToArray();
            var propiedades = bloque[bloque.IndexOf("\n      properties:\n", StringComparison.Ordinal)..];
            var declaradas = new Dictionary<string, string[]?>();
            foreach (Match propiedad in Regex.Matches(propiedades, @"(?m)^ {8}(?<nombre>\w+):\n(?<cuerpo>(?: {10}.*\n)*)"))
            {
                var enumeracion = Regex.Match(propiedad.Groups["cuerpo"].Value, @"enum: \[(?<valores>[^\]]+)\]");
                declaradas[propiedad.Groups["nombre"].Value] = enumeracion.Success
                    ? enumeracion.Groups["valores"].Value.Split(',').Select(v => v.Trim()).ToArray()
                    : null;
            }

            Assert.NotEmpty(requeridos);
            Assert.NotEmpty(declaradas);
            return new Esquema(nombre, requeridos, declaradas);
        }

        public void Cumple(JsonElement elemento)
        {
            Assert.Equal(JsonValueKind.Object, elemento.ValueKind);
            var presentes = elemento.EnumerateObject().Select(p => p.Name).ToList();
            Assert.All(Requeridos, r => Assert.Contains(r, presentes));
            foreach (var propiedad in elemento.EnumerateObject())
            {
                Assert.True(Propiedades.TryGetValue(propiedad.Name, out var valores), $"{Nombre} no declara la propiedad {propiedad.Name}.");
                if (valores is not null)
                {
                    Assert.Contains(propiedad.Value.GetString(), valores);
                }
            }
        }

        private static string RutaOpenApi()
        {
            var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
            while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "INComedor.sln")))
            {
                carpeta = carpeta.Parent;
            }

            return Path.Combine(carpeta!.FullName, "api", "openapi.yaml");
        }
    }
}

/// <summary>Una excepción no prevista no llega a un 500 ni se refleja (D8): el reloj del receptor falla en cada pedido.</summary>
public class ExcepcionNoPrevistaTests(ReceptorConRelojQueFalla receptor) : IClassFixture<ReceptorConRelojQueFalla>
{
    [Fact(DisplayName = "D8 · OWASP A05 · Una excepción no prevista responde 503 SERVICE_UNAVAILABLE con Retry-After: 2, sin el mensaje de la excepción")]
    public async Task Una_excepcion_no_prevista_responde_503()
    {
        await receptor.AsegurarPuestoAsync(receptor.Certificados.Puesto);
        using var cliente = receptor.CrearCliente(receptor.Certificados.Puesto);

        using var respuesta = await LotesDePrueba.EnviarAsync(cliente, LotesDePrueba.Lote(LotesDePrueba.ConsumoRegistrado()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TimeSpan.FromSeconds(2), respuesta.Headers.RetryAfter?.Delta);
        var texto = await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal("SERVICE_UNAVAILABLE", JsonDocument.Parse(texto).RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(ReceptorConRelojQueFalla.Mensaje, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", texto, StringComparison.Ordinal);
    }
}

/// <summary>El receptor de prueba con un reloj que falla en cada lectura.</summary>
public sealed class ReceptorConRelojQueFalla : ReceptorConKestrel
{
    public const string Mensaje = "RELOJ-ROTO-4C1D";

    protected override void ConfigurarServicios(IServiceCollection servicios) =>
        servicios.AddSingleton<TimeProvider>(new RelojQueFalla());

    private sealed class RelojQueFalla : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException(Mensaje);
    }
}
