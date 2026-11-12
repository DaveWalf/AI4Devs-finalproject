using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Intelektron.INComedor.Contracts.Tests;

/// <summary>JSON canónico RFC 8785 del lote y de cada evento (D4 de recibir-lotes-idempotente).</summary>
public class JsonCanonicoTests
{
    [Fact(DisplayName = "RFC 8785 §3.2.3 · Propiedades ordenadas por unidades UTF-16: el emoji (D83D) va antes que U+FB33")]
    public void Ordena_las_propiedades_por_unidades_utf16()
    {
        var entrada = T("""
            {
              "%u20ac": "Euro Sign",
              "%u000d": "Carriage Return",
              "%ufb33": "Hebrew Letter Dalet With Dagesh",
              "1": "One",
              "%ud83d%ude00": "Emoji: Grinning Face",
              "%u0080": "Control",
              "%u00f6": "Latin Small Letter O With Diaeresis"
            }
            """);
        var esperado = T("""{"\r":"Carriage Return","1":"One","%c0080":"Control","%c00F6":"Latin Small Letter O With Diaeresis","%c20AC":"Euro Sign","%cD83D%cDE00":"Emoji: Grinning Face","%cFB33":"Hebrew Letter Dalet With Dagesh"}""");

        Assert.Equal(esperado, Canonico(entrada));
    }

    [Fact(DisplayName = "RFC 8785 §3.2.2.2 · Escape mínimo de cadenas: control en hexadecimal minúscula, comilla y barra invertida escapadas, la barra y lo demás literales")]
    public void Escapa_las_cadenas_como_el_rfc()
    {
        var entrada = T("""
            {
              "string": "%u20ac$%u000F%u000aA'%u0042%u0022%u005c\\\"\/",
              "literals": [null, true, false]
            }
            """);
        var esperado = T("""{"literals":[null,true,false],"string":"%c20AC$%u000f\nA'B\"\\\\\"/"}""");

        Assert.Equal(esperado, Canonico(entrada));
    }

    [Fact(DisplayName = "RFC 8785 · Los escapes cortos del RFC y los caracteres que ECMAScript deja literales (U+007F, U+2028)")]
    public void Escapes_cortos_y_literales()
    {
        var entrada = T("""["%u0008%u0009%u000a%u000c%u000d%u0000%u001f", "%u007f%u2028%u00e9"]""");
        var esperado = T("""["\b\t\n\f\r%u0000%u001f","%c007F%c2028%c00E9"]""");

        Assert.Equal(esperado, Canonico(entrada));
    }

    [Fact(DisplayName = "RFC 8785 · Ordena en cada nivel y conserva el orden de los arreglos")]
    public void Ordena_en_cada_nivel_y_conserva_los_arreglos()
    {
        var entrada = """{ "b" : [ 3 , 1 , { "z" : 1 , "y" : 2 } ] , "a" : { "d" : true , "c" : null } }""";

        Assert.Equal("""{"a":{"c":null,"d":true},"b":[3,1,{"y":2,"z":1}]}""", Canonico(entrada));
    }

    [Theory(DisplayName = "RFC 8785 e I-JSON · Enteros dentro de ±(2^53 − 1), sin exponente")]
    [InlineData("9007199254740991")]
    [InlineData("-9007199254740991")]
    [InlineData("0")]
    [InlineData("150000")]
    public void Enteros_en_los_extremos_quedan_iguales(string numero)
    {
        Assert.Equal($$"""{"n":{{numero}}}""", Canonico($$"""{ "n": {{numero}} }"""));
    }

    [Theory(DisplayName = "RFC 8785 e I-JSON · Rechaza un entero fuera de ±(2^53 − 1), un decimal y un exponente")]
    [InlineData("9007199254740992")]
    [InlineData("-9007199254740992")]
    [InlineData("1.5")]
    [InlineData("1.0")]
    [InlineData("1e2")]
    [InlineData("1E2")]
    [InlineData("-2e-3")]
    public void Rechaza_numeros_que_el_contrato_no_usa(string numero)
    {
        Assert.Throws<JsonCanonicoException>(() => Canonico($$"""{"n":{{numero}}}"""));
    }

    [Theory(DisplayName = "RFC 8785 · Rechaza una propiedad repetida, también en un objeto anidado")]
    [InlineData("""{"BatchId":"a","BatchId":"b"}""")]
    [InlineData("""{"Events":[{"EventId":"a","EventId":"a"}]}""")]
    public void Rechaza_una_propiedad_repetida(string json)
    {
        Assert.Throws<JsonCanonicoException>(() => Canonico(json));
    }

    [Theory(DisplayName = "OWASP A03 · Rechaza lo que no es JSON y una sustituta UTF-16 sin pareja")]
    [InlineData("no es JSON")]
    [InlineData("""{"a":1} extra""")]
    [InlineData("")]
    [InlineData("""{"a":"%ud800"}""")]
    public void Rechaza_lo_que_no_es_json(string json)
    {
        Assert.Throws<JsonCanonicoException>(() => Canonico(T(json)));
    }

    [Fact(DisplayName = "AT27 · El mismo objeto con otro orden y otros espacios da los mismos bytes")]
    public void Mismo_objeto_mismos_bytes()
    {
        var uno = """{"BatchId":"aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa","SchemaVersion":1,"Events":[{"EventId":"88888888-8888-4888-8888-888888888888","Quantity":1}]}""";
        var otro = """
            {
                "Events" : [ { "Quantity" : 1 , "EventId" : "88888888-8888-4888-8888-888888888888" } ],
                "SchemaVersion" : 1,
                "BatchId" : "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"
            }
            """;

        Assert.Equal(
            JsonCanonico.Canonicalizar(Encoding.UTF8.GetBytes(uno)),
            JsonCanonico.Canonicalizar(Encoding.UTF8.GetBytes(otro)));
    }

    [Fact(DisplayName = "D4 · Un evento dentro del lote se canonicaliza igual que suelto, para el hash por evento")]
    public void Un_elemento_se_canonicaliza_igual_que_su_texto()
    {
        var evento = """{ "Quantity": 1, "EventId": "88888888-8888-4888-8888-888888888888" }""";
        using var lote = JsonDocument.Parse($$"""{"Events":[{{evento}}]}""");

        var desdeElemento = JsonCanonico.Canonicalizar(lote.RootElement.GetProperty("Events")[0]);

        Assert.Equal(JsonCanonico.Canonicalizar(Encoding.UTF8.GetBytes(evento)), desdeElemento);
    }

    private static string Canonico(string json) =>
        Encoding.UTF8.GetString(JsonCanonico.Canonicalizar(Encoding.UTF8.GetBytes(json)));

    // En el texto de estas pruebas, «%uXXXX» es la secuencia de escape JSON con esos cuatro dígitos y
    // «%cXXXX» es el carácter UTF-16 de ese valor: así el fuente no lleva caracteres fuera de ASCII.
    private static string T(string texto) =>
        Regex.Replace(texto, "%([uc])([0-9A-Fa-f]{4})", m => m.Groups[1].Value == "u"
            ? "\\" + "u" + m.Groups[2].Value
            : ((char)Convert.ToInt32(m.Groups[2].Value, 16)).ToString());
}
