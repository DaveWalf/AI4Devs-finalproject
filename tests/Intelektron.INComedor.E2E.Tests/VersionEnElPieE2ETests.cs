using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// El pie y la cabecera no cambian de lugar (D1 de mover-version-al-pie): a 1024 × 768 y por CDP, la versión queda en el
/// centro de la ventana con el texto de la sincronización de reposo y otra vez con los tres mensajes de su peor caso (un
/// error de credencial con pendientes y retenidos), que pasan a dos renglones; el pie no cambia de alto, la franja entra
/// en él y su texto no toca la versión, que tampoco toca la marca.
/// </summary>
public class VersionEnElPieE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("version-pie");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "presentacion-puesto · El pie y la cabecera no cambian de lugar: con otro texto de sincronización, más largo, la versión sigue en el centro de la ventana, el pie mide lo mismo y nada se toca")]
    public async Task La_version_no_se_corre_con_la_sincronizacion()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        var antes = await MedirAsync(pagina);
        _escenario.Registrar($"1024 × 768, con la sincronización de reposo: {antes}");
        // Otro estado de la franja, sin tocar el puesto: los tres mensajes de FranjaSincronizacion en su peor caso, un error
        // de credencial con pendientes y retenidos, cada uno en su span como los arma el componente.
        await pagina.EvaluateAsync(
            """
            () => { const f = document.querySelector('footer.pie .franja-sincronizacion');
              f.querySelectorAll('.mensaje-sincronizacion').forEach(m => m.remove());
              for (const t of ['Error técnico: credencial del equipo', '12 pendientes', '3 retenidos para revisión']) {
                const s = document.createElement('span'); s.className = 'mensaje-sincronizacion'; s.textContent = t; f.appendChild(s); } }
            """);
        var despues = await MedirAsync(pagina);
        _escenario.Registrar($"1024 × 768, con un texto de sincronización largo: {despues}");
        await _escenario.CapturarAsync(pagina, "pie-sincronizacion-larga-1024x768");

        foreach (var medida in new[] { antes, despues })
        {
            Assert.True(Math.Abs(medida.CentroVentana - medida.CentroVersion) <= Tolerancia, $"La versión no está centrada: {medida}.");
            Assert.True(medida.DerechaFranja <= medida.IzquierdaVersion, $"La versión toca la franja: {medida}.");
            Assert.True(medida.DerechaVersion <= medida.IzquierdaMarca, $"La versión toca la marca: {medida}.");
            Assert.True(Math.Abs(medida.AltoCabecera - 64) <= Tolerancia, $"La cabecera mide {medida.AltoCabecera} px.");
        }

        Assert.True(Math.Abs(antes.CentroVersion - despues.CentroVersion) <= Tolerancia, "La versión se corrió al cambiar la sincronización.");
        Assert.True(Math.Abs(antes.AltoPie - despues.AltoPie) <= Tolerancia, $"El pie pasó de {antes.AltoPie} px a {despues.AltoPie} px.");
        Assert.True(despues.AltoFranja <= despues.AltoPie, $"La franja no entra en el pie: {despues.AltoFranja} px en {despues.AltoPie} px.");
    }

    private static async Task<Medida> MedirAsync(IPage pagina) => JsonSerializer.Deserialize<Medida>(await pagina.EvaluateAsync<string>(
        """
        () => { const caja = s => document.querySelector(s).getBoundingClientRect();
          const texto = s => { const r = document.createRange(); r.selectNodeContents(document.querySelector(s)); return r.getBoundingClientRect(); };
          const v = texto('footer.pie #version-programa'), f = texto('footer.pie .franja-sincronizacion');
          return JSON.stringify({ CentroVentana: window.innerWidth / 2, CentroVersion: (v.left + v.right) / 2, IzquierdaVersion: v.left, DerechaVersion: v.right,
            DerechaFranja: f.right, AltoFranja: f.height, IzquierdaMarca: caja('footer.pie .marca-intelektron').left,
            AltoPie: caja('footer.pie').height, AltoCabecera: caja('header.cabecera').height }); }
        """))!;

    private sealed record Medida(double CentroVentana, double CentroVersion, double IzquierdaVersion, double DerechaVersion, double DerechaFranja,
        double AltoFranja, double IzquierdaMarca, double AltoPie, double AltoCabecera);
}
