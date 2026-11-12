using System.Diagnostics;
using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// El paquete lleva su versión (D5 de mostrar-version-del-programa): abre el puesto, el compilado o el EXE publicado, y compara
/// la cabecera con <c>INCOMEDOR_VERSION_ESPERADA</c>, que <c>publicar-puesto.ps1</c> pone en el tag del paquete; sin ella,
/// con «desarrollo». El commit corto es el de <c>git rev-parse HEAD</c> del repo. A 1024 × 768 y por CDP mide además el
/// pie (la versión centrada en la ventana, más chica que la letra del pie, sin tocar la franja ni la marca, y el engranaje
/// en su borde; D6 de mover-version-al-pie), la cabecera (64 px, sin la versión) y la pestaña General (los
/// encabezados de sus secciones y los rótulos de la versión alineados), con la tolerancia de 1 px de la regla de prolijidad.
/// </summary>
public class VersionDelPaqueteE2ETests : IAsyncLifetime
{
    public const string VariableVersionEsperada = "INCOMEDOR_VERSION_ESPERADA";
    private const double Tolerancia = 1;

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("version-paquete");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "distribucion-puesto · El tag en el pie del EXE: el pie dice la versión esperada («desarrollo» sin INCOMEDOR_VERSION_ESPERADA) y el commit corto del código, centrada en la ventana, más chica que la letra del pie y sin tocar la franja ni la marca, y la cabecera mide 64 px sin la versión")]
    public async Task Version_del_paquete_en_la_cabecera()
    {
        var esperada = Environment.GetEnvironmentVariable(VariableVersionEsperada) is { Length: > 0 } tag ? tag : "desarrollo";
        var commit = Commit();
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        var texto = (await pagina.Locator("footer.pie #version-programa").InnerTextAsync()).Trim();
        _escenario.Registrar($"el pie dice «{texto}»; se esperaba «{esperada} · {commit[..7]}» (HEAD {commit})");
        Assert.Equal($"{esperada} · {commit[..7]}", texto);

        var pie = JsonSerializer.Deserialize<Pie>(await pagina.EvaluateAsync<string>(
            """
            () => { const caja = s => document.querySelector(s).getBoundingClientRect();
              const r = document.createRange(); r.selectNodeContents(document.querySelector('footer.pie #version-programa')); const v = r.getBoundingClientRect();
              const franja = caja('footer.pie .franja-sincronizacion'), marca = caja('footer.pie .marca-intelektron'), engranaje = caja('footer.pie .engranaje');
              return JSON.stringify({ AltoCabecera: caja('header.cabecera').height, VersionesEnCabecera: document.querySelectorAll('header.cabecera #version-programa').length,
                BotonesEnCabecera: document.querySelectorAll('header.cabecera button').length,
                CentroVentana: window.innerWidth / 2, CentroVersion: (v.left + v.right) / 2, IzquierdaVersion: v.left, DerechaVersion: v.right,
                DerechaFranja: franja.right, IzquierdaMarca: marca.left,
                LetraPie: parseFloat(getComputedStyle(document.querySelector('footer.pie')).fontSize),
                LetraVersion: parseFloat(getComputedStyle(document.querySelector('footer.pie #version-programa')).fontSize),
                MargenDerechoEngranaje: window.innerWidth - engranaje.right, MargenInferiorEngranaje: window.innerHeight - engranaje.bottom }); }
            """))!;
        _escenario.Registrar($"1024 × 768, el pie y la cabecera: {pie}");
        await _escenario.CapturarAsync(pagina, "pie-version-1024x768");
        Assert.True(Math.Abs(pie.AltoCabecera - 64) <= Tolerancia, $"La cabecera mide {pie.AltoCabecera} px.");
        Assert.Equal(0, pie.VersionesEnCabecera);
        Assert.Equal(0, pie.BotonesEnCabecera);
        Assert.True(Math.Abs(pie.CentroVentana - pie.CentroVersion) <= Tolerancia, "La versión no está centrada en la ventana.");
        Assert.True(pie.LetraVersion < pie.LetraPie, "La versión no es más chica que la letra del pie.");
        Assert.True(pie.DerechaFranja <= pie.IzquierdaVersion, "La versión toca la franja de sincronización.");
        Assert.True(pie.DerechaVersion <= pie.IzquierdaMarca, "La versión toca la marca Intelektron.");
        Assert.True(pie.MargenDerechoEngranaje <= 16 && pie.MargenInferiorEngranaje <= 16, "El engranaje dejó el borde inferior derecho.");

        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-general").ClickAsync();
        await pagina.Locator("#version-programa-general").ScrollIntoViewIfNeededAsync();
        await Assertions.Expect(pagina.Locator("#version-programa-general dd").First).ToHaveTextAsync(esperada);
        var general = JsonSerializer.Deserialize<General>(await pagina.EvaluateAsync<string>(
            """
            () => { const caja = e => { const b = e.getBoundingClientRect(); return { Texto: e.textContent.trim(), Izquierda: b.left, Letra: getComputedStyle(e).fontSize }; };
              return JSON.stringify({ Titulos: [...document.querySelectorAll('#seccion-general section > h3')].map(caja),
                Rotulos: [...document.querySelectorAll('#version-programa-general dt')].map(caja),
                Valores: [...document.querySelectorAll('#version-programa-general dd')].map(caja) }); }
            """))!;
        _escenario.Registrar($"1024 × 768, General: títulos {string.Join("; ", general.Titulos.Select(c => c.ToString()))}");
        _escenario.Registrar($"1024 × 768, la versión en General: rótulos {string.Join("; ", general.Rotulos.Select(c => c.ToString()))}; valores {string.Join("; ", general.Valores.Select(c => c.ToString()))}");
        await _escenario.CapturarAsync(pagina, "general-version-1024x768");
        Assert.Equal("Versión del programa", general.Titulos[^1].Texto);
        Assert.All(general.Titulos, t => Assert.True(Math.Abs(t.Izquierda - general.Titulos[0].Izquierda) <= Tolerancia && t.Letra == general.Titulos[0].Letra, $"{t}"));
        Assert.All(general.Rotulos, r => Assert.True(Math.Abs(r.Izquierda - general.Titulos[0].Izquierda) <= Tolerancia, $"{r}"));
        Assert.All(general.Valores, v => Assert.True(Math.Abs(v.Izquierda - general.Valores[0].Izquierda) <= Tolerancia, $"{v}"));
        Assert.Equal(commit, general.Valores[1].Texto);
    }

    /// <summary>El commit de HEAD del repo donde están las pruebas, el que el SDK pone en la versión informativa del build.</summary>
    private static string Commit()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "INComedor.sln")))
        {
            carpeta = carpeta.Parent;
        }

        var inicio = new ProcessStartInfo("git", "rev-parse HEAD")
        {
            WorkingDirectory = carpeta?.FullName ?? throw new DirectoryNotFoundException("No se encontró INComedor.sln."),
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var git = Process.Start(inicio)!;
        var salida = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        Assert.Matches("^[0-9a-f]{40}$", salida);
        return salida;
    }

    private sealed record Pie(double AltoCabecera, int VersionesEnCabecera, int BotonesEnCabecera, double CentroVentana, double CentroVersion,
        double IzquierdaVersion, double DerechaVersion, double DerechaFranja, double IzquierdaMarca, double LetraPie, double LetraVersion,
        double MargenDerechoEngranaje, double MargenInferiorEngranaje);

    private sealed record Caja(string Texto, double Izquierda, string Letra)
    {
        public override string ToString() => $"«{Texto}» en x {Izquierda:F1}, letra {Letra}";
    }

    private sealed record General(Caja[] Titulos, Caja[] Rotulos, Caja[] Valores);
}
