using System.Net;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Las costuras del puesto con la ventana real (D2 y D5 de demostrar-flujo-de-punta-a-punta): el puerto de depuración
/// abre sólo con un valor válido y sólo en loopback. Cada prueba lanza el EXE compilado en una carpeta propia.
/// </summary>
public class CosturasDelPuestoTests : IAsyncLifetime
{
    private static readonly TimeSpan VentanaDeEscucha = TimeSpan.FromSeconds(5);

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar(TestContext.Current.Test!.TestCase.TestMethod!.MethodName);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "L58:96 · modo-demostracion · Puerto válido: un cliente de CDP se conecta en 127.0.0.1 y encuentra la pantalla de reposo, y el puerto escucha sólo en loopback")]
    public async Task Con_un_puerto_valido_se_conecta_por_cdp_y_escucha_solo_en_loopback()
    {
        var puesto = _escenario.LanzarPuesto(_escenario.PuertoCdp.ToString());
        using var playwright = await Playwright.CreateAsync();

        var pagina = await _escenario.ConectarAsync(playwright);

        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        await _escenario.CapturarAsync(pagina, "01-reposo-por-cdp");
        var escuchas = await _escenario.EscuchasDelArbolAsync(puesto, VentanaDeEscucha);
        Assert.Contains(escuchas, e => e.Puerto == _escenario.PuertoCdp);
        Assert.All(escuchas, e =>
        {
            Assert.Equal(_escenario.PuertoCdp, e.Puerto);
            Assert.True(IPAddress.IsLoopback(e.Direccion), $"{e.Direccion}:{e.Puerto} no es de loopback");
        });
    }

    [Fact(DisplayName = "OWASP A05 · modo-demostracion · Sin la variable: la ventana abre en reposo y ningún proceso del puesto, el EXE o su WebView2, escucha conexiones TCP")]
    public async Task Sin_la_variable_ningun_proceso_del_puesto_escucha()
    {
        var puesto = _escenario.LanzarPuesto(puertoCdp: null);

        await _escenario.EsperarReposoAsync(puesto);

        Assert.Empty(await _escenario.EscuchasDelArbolAsync(puesto, VentanaDeEscucha));
    }

    [Theory(DisplayName = "OWASP A05 · modo-demostracion · Valor fuera de rango o que no es un número: la ventana abre en reposo y ningún proceso del puesto escucha")]
    [InlineData("abc")]
    [InlineData("80")]
    [InlineData("70000")]
    public async Task Con_un_valor_invalido_ningun_proceso_del_puesto_escucha(string valor)
    {
        var puesto = _escenario.LanzarPuesto(valor);

        await _escenario.EsperarReposoAsync(puesto);

        Assert.Empty(await _escenario.EscuchasDelArbolAsync(puesto, VentanaDeEscucha));
    }
}
