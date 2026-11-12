namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El entorno de la demostración (D1 de demostrar-flujo-de-punta-a-punta): la carpeta, la dirección del receptor, el
/// puerto de depuración y la carpeta de datos del WebView2, resueltos de tres variables. Las variables salen de un
/// diccionario, nunca del entorno del proceso, así que ningún caso abre una ventana ni toca otra prueba.
/// </summary>
public class EntornoDemostracionTests
{
    private const string DatosLocales = @"C:\Users\prueba\AppData\Local";
    private static readonly string CarpetaPorDefecto = Path.Combine(DatosLocales, "INComedor", "demo");
    private static readonly Uri ReceptorPorDefecto = new("https://localhost:5001/");

    [Fact(DisplayName = "docs/pruebas.md:46 · AT01 · Carpeta elegida por la variable: la base, los comprobantes, los certificados y los datos del WebView2 van a la ruta absoluta")]
    public void Con_una_ruta_absoluta_todo_va_a_esa_carpeta()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "incomedor-tests", "e2e-carpeta");

        var entorno = Resolver(("INCOMEDOR_DEMO_DIR", carpeta));

        Assert.Equal(carpeta, entorno.Carpeta);
        Assert.Equal(Path.Combine(carpeta, "incomedor.db"), entorno.RutaBase);
        Assert.Equal(Path.Combine(carpeta, "comprobantes"), entorno.CarpetaComprobantes);
        Assert.Equal(Path.Combine(carpeta, "certificados"), entorno.CarpetaCertificados);
        Assert.Equal(Path.Combine(carpeta, "webview2"), entorno.CarpetaDatosWebView2);
    }

    [Fact(DisplayName = "modo-demostracion · Sin la variable: el puesto usa %LOCALAPPDATA%\\INComedor\\demo\\ y la carpeta de datos del WebView2 de siempre")]
    public void Sin_la_variable_de_la_carpeta_usa_la_de_siempre()
    {
        var entorno = Resolver();

        Assert.Equal(CarpetaPorDefecto, entorno.Carpeta);
        Assert.Equal(Path.Combine(CarpetaPorDefecto, "incomedor.db"), entorno.RutaBase);
        Assert.Equal(Path.Combine(CarpetaPorDefecto, "comprobantes"), entorno.CarpetaComprobantes);
        Assert.Equal(Path.Combine(CarpetaPorDefecto, "certificados"), entorno.CarpetaCertificados);
        Assert.Null(entorno.CarpetaDatosWebView2);
    }

    [Theory(DisplayName = "OWASP A05 · Ruta relativa: el puesto usa la carpeta por defecto y no crea nada bajo el directorio de trabajo")]
    [InlineData("demo-relativa")]
    [InlineData(@"C:demo-relativa")]
    [InlineData(@"\demo-relativa")]
    [InlineData("")]
    public void Con_una_ruta_relativa_usa_la_carpeta_por_defecto(string valor)
    {
        var entorno = Resolver(("INCOMEDOR_DEMO_DIR", valor));

        Assert.Equal(CarpetaPorDefecto, entorno.Carpeta);
        Assert.Null(entorno.CarpetaDatosWebView2);
        Assert.False(Directory.Exists(Path.Combine(Environment.CurrentDirectory, "demo-relativa")));
    }

    [Fact(DisplayName = "HU02-C · AT27 · Dirección https elegida por la variable: el puesto envía sus lotes a otro puerto")]
    public void Con_una_direccion_https_usa_esa_direccion()
    {
        var entorno = Resolver(("INCOMEDOR_RECEPTOR_URL", "https://localhost:49731/"));

        Assert.Equal(new Uri("https://localhost:49731/"), entorno.DireccionReceptor);
    }

    [Theory(DisplayName = "AT13 · OWASP A02 · Dirección sin TLS, sin esquema o ausente: el puesto usa https://localhost:5001/")]
    [InlineData("http://localhost:49731/")]
    [InlineData("localhost:5001")]
    [InlineData("ftp://localhost:49731/")]
    [InlineData("/api/incomedor")]
    [InlineData("")]
    [InlineData(null)]
    public void Sin_una_direccion_https_usa_la_de_siempre(string? valor)
    {
        var entorno = valor is null ? Resolver() : Resolver(("INCOMEDOR_RECEPTOR_URL", valor));

        Assert.Equal(ReceptorPorDefecto, entorno.DireccionReceptor);
        Assert.Equal(Uri.UriSchemeHttps, entorno.DireccionReceptor.Scheme);
    }

    [Theory(DisplayName = "L58:96 · Puerto válido: un entero entre 1024 y 65535 abre la depuración en ese puerto")]
    [InlineData("1024", 1024)]
    [InlineData("9333", 9333)]
    [InlineData("65535", 65535)]
    public void Con_un_puerto_valido_abre_la_depuracion(string valor, int esperado)
    {
        var entorno = Resolver(("INCOMEDOR_CDP_PORT", valor));

        Assert.Equal(esperado, entorno.PuertoCdp);
    }

    [Theory(DisplayName = "OWASP A05 · Sin la variable, fuera de rango o que no es un número: el puesto no abre ningún puerto de depuración")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("80")]
    [InlineData("1023")]
    [InlineData("65536")]
    [InlineData("70000")]
    [InlineData("-9333")]
    [InlineData("+9333")]
    [InlineData(" 9333")]
    public void Sin_un_puerto_valido_no_abre_la_depuracion(string? valor)
    {
        var entorno = valor is null ? Resolver() : Resolver(("INCOMEDOR_CDP_PORT", valor));

        Assert.Null(entorno.PuertoCdp);
    }

    [Fact(DisplayName = "D4 · La carpeta de datos del WebView2 sólo se fija con INCOMEDOR_DEMO_DIR, no con el puerto ni con la dirección")]
    public void La_carpeta_del_webview2_sale_solo_de_la_variable_de_la_carpeta()
    {
        var entorno = Resolver(("INCOMEDOR_CDP_PORT", "9333"), ("INCOMEDOR_RECEPTOR_URL", "https://localhost:49731/"));

        Assert.Equal(9333, entorno.PuertoCdp);
        Assert.Null(entorno.CarpetaDatosWebView2);
        Assert.Equal(CarpetaPorDefecto, entorno.Carpeta);
    }

    [Fact(DisplayName = "modo-demostracion · Sin la variable: sin ninguna de las tres, el puesto no toca el arranque de su WebView2, ni la carpeta de datos ni los argumentos")]
    public void Sin_las_variables_el_webview2_arranca_como_siempre()
    {
        var entorno = Resolver();

        Assert.False(entorno.AjustaWebView2);
        Assert.Null(entorno.ArgumentosNavegador);
    }

    [Fact(DisplayName = "D2 · Puerto válido: el único argumento es --remote-debugging-port, sin --remote-debugging-address, así que Chromium escucha sólo en loopback")]
    public void Con_un_puerto_valido_el_argumento_abre_la_depuracion_en_loopback()
    {
        var entorno = Resolver(("INCOMEDOR_CDP_PORT", "9333"));

        Assert.True(entorno.AjustaWebView2);
        Assert.Equal("--remote-debugging-port=9333", entorno.ArgumentosNavegador);
    }

    [Fact(DisplayName = "D4 · Con la carpeta y sin el puerto, el WebView2 cambia sólo su carpeta de datos, sin ningún argumento")]
    public void Con_la_carpeta_sola_el_webview2_no_lleva_argumentos()
    {
        var entorno = Resolver(("INCOMEDOR_DEMO_DIR", Path.Combine(Path.GetTempPath(), "incomedor-tests", "e2e-carpeta")));

        Assert.True(entorno.AjustaWebView2);
        Assert.Null(entorno.ArgumentosNavegador);
    }

    private static EntornoDemostracion Resolver(params (string Nombre, string Valor)[] variables)
    {
        var diccionario = variables.ToDictionary(v => v.Nombre, v => v.Valor, StringComparer.Ordinal);
        return EntornoDemostracion.Resolver(nombre => diccionario.GetValueOrDefault(nombre), DatosLocales);
    }
}
