using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// El título con la letra de INWeb (D4 y D5 de mover-version-al-pie): a 1024 × 768 y por CDP, la letra que el navegador
/// usó de verdad para «INComedor · tu pausa del día» (<c>CSS.getPlatformFontsForNode</c>, no la pila pedida) es Heebo,
/// de peso 300, desde el archivo del puesto; el título entra entero entre los logos y la cabecera sigue en 64 px. Con el
/// archivo bloqueado, el título sale en la sans-serif del sistema, también entero y en 64 px.
/// </summary>
public class TituloConLaLetraDeInwebE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;
    private const string Titulo = "header.cabecera .servicio";

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("titulo-heebo");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "presentacion-puesto · El título en Heebo: a 1024 × 768, el navegador dibuja «INComedor · tu pausa del día» entero en Heebo de peso 300, desde el archivo del puesto, entre los dos logos, y la cabecera mide 64 px")]
    public async Task El_titulo_en_Heebo()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await AjustarAsync(pagina);

        var familias = await FamiliasUsadasAsync(cdp);
        var medida = await MedirAsync(pagina);
        _escenario.Registrar($"1024 × 768, el título: familias usadas {string.Join(", ", familias)}; {medida}");
        await _escenario.CapturarAsync(pagina, "titulo-heebo-1024x768");

        Assert.Equal(["Heebo"], familias);
        Assert.Equal("300", medida.Peso);
        Assert.Equal(0, medida.ReglasExternas);
        AfirmarSinCambiosDeLugar(medida);
    }

    [Fact(DisplayName = "presentacion-puesto · La fuente que falta: con el archivo de Heebo bloqueado, el título sale entero en una sans-serif del sistema y la cabecera sigue en 64 px")]
    public async Task La_fuente_que_falta()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await AjustarAsync(pagina);
        await cdp.SendAsync("Network.enable");
        await cdp.SendAsync("Network.setCacheDisabled", new Dictionary<string, object> { ["cacheDisabled"] = true });
        await cdp.SendAsync("Network.setBlockedURLs", new Dictionary<string, object> { ["urls"] = new[] { "*heebo-latin.woff2*" } });
        await pagina.ReloadAsync();
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        var familias = await FamiliasUsadasAsync(cdp);
        var medida = await MedirAsync(pagina);
        _escenario.Registrar($"1024 × 768, sin el archivo: familias usadas {string.Join(", ", familias)}; {medida}");
        await _escenario.CapturarAsync(pagina, "titulo-sin-heebo-1024x768");

        // Con peso 300, Windows resuelve «Segoe UI» a su cara liviana, «Segoe UI Light».
        Assert.StartsWith("Segoe UI", Assert.Single(familias), StringComparison.Ordinal);
        Assert.Equal("300", medida.Peso);
        AfirmarSinCambiosDeLugar(medida);
    }

    private static async Task<ICDPSession> AjustarAsync(IPage pagina)
    {
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        await pagina.EvaluateAsync("() => document.fonts.ready.then(() => true)");
        return cdp;
    }

    /// <summary>Las familias que el navegador usó para dibujar el título (D5): la letra efectiva, no la pedida.</summary>
    private static async Task<string[]> FamiliasUsadasAsync(ICDPSession cdp)
    {
        await cdp.SendAsync("DOM.enable");
        await cdp.SendAsync("CSS.enable");
        var documento = (await cdp.SendAsync("DOM.getDocument", new Dictionary<string, object> { ["depth"] = 0 }))!.Value;
        var raiz = documento.GetProperty("root").GetProperty("nodeId").GetInt32();
        var nodo = (await cdp.SendAsync("DOM.querySelector", new Dictionary<string, object> { ["nodeId"] = raiz, ["selector"] = Titulo }))!.Value
            .GetProperty("nodeId").GetInt32();
        var fuentes = (await cdp.SendAsync("CSS.getPlatformFontsForNode", new Dictionary<string, object> { ["nodeId"] = nodo }))!.Value;
        return fuentes.GetProperty("fonts").EnumerateArray().Select(f => f.GetProperty("familyName").GetString()!).Distinct().ToArray();
    }

    private static async Task<Medida> MedirAsync(IPage pagina) => JsonSerializer.Deserialize<Medida>(await pagina.EvaluateAsync<string>(
        """
        () => { const t = document.querySelector('header.cabecera .servicio'); const caja = s => document.querySelector(s).getBoundingClientRect();
          const r = document.createRange(); r.selectNodeContents(t); const texto = r.getBoundingClientRect();
          let externas = 0;
          for (const hoja of document.styleSheets) { let reglas = []; try { reglas = [...hoja.cssRules]; } catch { externas++; continue; }
            for (const regla of reglas) { if (/url\(\s*["']?(https?:)?\/\//i.test(regla.cssText)) externas++; } }
          return JSON.stringify({ Texto: t.textContent.trim(), Peso: getComputedStyle(t).fontWeight, Letra: parseFloat(getComputedStyle(t).fontSize),
            AltoCabecera: caja('header.cabecera').height, IzquierdaTexto: texto.left, DerechaTexto: texto.right, AltoTexto: texto.height,
            DerechaLogoEmpresa: caja('header.cabecera .logo-empresa').right, IzquierdaLogoConcesionaria: caja('header.cabecera .logo-concesionaria').left,
            AnchoUtil: t.clientWidth, AnchoTexto: t.scrollWidth, ReglasExternas: externas }); }
        """))!;

    private static void AfirmarSinCambiosDeLugar(Medida medida)
    {
        Assert.Equal("INComedor · tu pausa del día", medida.Texto);
        Assert.Equal(22, medida.Letra);
        Assert.True(Math.Abs(medida.AltoCabecera - 64) <= Tolerancia, $"La cabecera mide {medida.AltoCabecera} px.");
        Assert.True(medida.AnchoTexto <= medida.AnchoUtil, $"El título no entra entero: {medida.AnchoTexto} px en {medida.AnchoUtil} px.");
        Assert.True(medida.AltoTexto < 2 * medida.Letra, $"El título se partió en dos renglones: {medida.AltoTexto} px.");
        Assert.True(medida.DerechaLogoEmpresa <= medida.IzquierdaTexto && medida.DerechaTexto <= medida.IzquierdaLogoConcesionaria,
            "El título toca un logo.");
    }

    private sealed record Medida(string Texto, string Peso, double Letra, double AltoCabecera, double IzquierdaTexto, double DerechaTexto,
        double AltoTexto, double DerechaLogoEmpresa, double IzquierdaLogoConcesionaria, double AnchoUtil, double AnchoTexto, int ReglasExternas);
}
