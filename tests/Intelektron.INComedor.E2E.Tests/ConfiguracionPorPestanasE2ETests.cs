using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Configuración por pestañas con el puesto compilado (configurar-puesto-por-pestanas): por CDP y a 1024 × 768, lo que bUnit
/// no mide. Usa los ayudantes del E2E sin cambiarlos; la captura 10 sale en JPEG, en la carpeta de resultados.
/// </summary>
public class ConfiguracionPorPestanasE2ETests : IAsyncLifetime
{
    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("pestanas");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "configuracion-puesto · Las pestañas entran en 1024 × 768: las diez en una fila, cada una de al menos 56 px de alto, sin desplazar la pantalla")]
    public async Task Las_pestanas_entran_en_1024_x_768()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await FijarArea(pagina, 1024, 768);
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await Assertions.Expect(pagina.Locator("[role=\"tablist\"]")).ToBeVisibleAsync();

        var medida = await pagina.EvaluateAsync<JsonElement>(
            """
            () => {
                const pestanas = [...document.querySelectorAll('[role="tablist"] [role="tab"]')].map(p => p.getBoundingClientRect());
                const raiz = document.scrollingElement;
                return {
                    Cantidad: pestanas.length,
                    Arriba: pestanas.map(p => p.top),
                    Alto: Math.min(...pestanas.map(p => p.height)),
                    Derecha: Math.max(...pestanas.map(p => p.right)),
                    Izquierda: Math.min(...pestanas.map(p => p.left)),
                    Ancho: window.innerWidth,
                    AltoVentana: window.innerHeight,
                    AnchoDocumento: raiz.scrollWidth,
                    AltoDocumento: raiz.scrollHeight
                };
            }
            """);
        _escenario.Registrar($"pestañas a 1024 × 768: {medida}");

        Assert.Equal(10, medida.GetProperty("Cantidad").GetInt32());
        var arriba = medida.GetProperty("Arriba").EnumerateArray().Select(a => a.GetDouble()).ToList();
        Assert.True(arriba.Max() - arriba.Min() < 1, $"Las pestañas no están en una fila: arriba de {arriba.Min()} a {arriba.Max()} px.");
        Assert.True(medida.GetProperty("Alto").GetDouble() >= 56, $"La pestaña más baja mide {medida.GetProperty("Alto").GetDouble()} px.");
        Assert.True(medida.GetProperty("Izquierda").GetDouble() >= 0 && medida.GetProperty("Derecha").GetDouble() <= medida.GetProperty("Ancho").GetDouble(),
            $"Las pestañas salen del área: de {medida.GetProperty("Izquierda").GetDouble()} a {medida.GetProperty("Derecha").GetDouble()} px.");
        Assert.True(medida.GetProperty("AnchoDocumento").GetDouble() <= medida.GetProperty("Ancho").GetDouble()
            && medida.GetProperty("AltoDocumento").GetDouble() <= medida.GetProperty("AltoVentana").GetDouble(), "La pantalla se desplaza con Configuración abierta.");

        await Capturar(pagina, "10-configuracion");
    }

    [Fact(DisplayName = "comprobante-local · Reposo sin «Asistencia» a 1024 × 768: el campo del código e «Ingresar» a la vista, y ningún botón «Asistencia»")]
    public async Task Reposo_sin_asistencia()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await FijarArea(pagina, 1024, 768);

        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        await Assertions.Expect(pagina.Locator("#ingresar")).ToBeVisibleAsync();
        await Assertions.Expect(pagina.Locator("#asistencia")).ToHaveCountAsync(0);
        await Assertions.Expect(pagina.Locator("main button", new() { HasText = "Asistencia" })).ToHaveCountAsync(0);

        await Capturar(pagina, "06-reposo-tactil");
    }

    [Fact(DisplayName = "administracion-local · Muchas personas en 1024 × 768: con 200 personas sintéticas más, al menos diez filas de 44 px a la vista y el encabezado fijo al desplazar la tabla")]
    public async Task Muchas_personas_en_1024_x_768()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await FijarArea(pagina, 1024, 768);
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        SumarPersonas(200);
        await EntradaConfiguracionE2E.EntrarAsync(pagina);

        var reloj = Stopwatch.StartNew();
        await pagina.Locator("#pestana-personas").ClickAsync();
        await Assertions.Expect(pagina.Locator("#tabla-personas tbody tr.persona")).ToHaveCountAsync(209);
        reloj.Stop();
        _escenario.Registrar($"tabla de personas con 209 filas dibujada en {reloj.ElapsedMilliseconds} ms desde el toque en la pestaña");

        var medida = await pagina.EvaluateAsync<JsonElement>(
            """
            () => {
                const caja = document.querySelector('#tabla-personas .caja-tabla').getBoundingClientRect();
                const filas = [...document.querySelectorAll('#tabla-personas tbody tr.persona')].map(f => f.getBoundingClientRect());
                const visibles = filas.filter(f => f.top >= caja.top && f.bottom <= caja.bottom);
                return { Visibles: visibles.length, Alto: Math.min(...filas.map(f => f.height)), CajaAbajo: caja.bottom, AltoVentana: window.innerHeight };
            }
            """);
        _escenario.Registrar($"tabla a 1024 × 768: {medida}");
        Assert.True(medida.GetProperty("Visibles").GetInt32() >= 10, $"Se ven {medida.GetProperty("Visibles").GetInt32()} filas enteras.");
        Assert.True(medida.GetProperty("Alto").GetDouble() >= 44, $"La fila más baja mide {medida.GetProperty("Alto").GetDouble()} px.");
        Assert.True(medida.GetProperty("CajaAbajo").GetDouble() <= medida.GetProperty("AltoVentana").GetDouble(), "La tabla sale del área.");

        var encabezado = await pagina.EvaluateAsync<JsonElement>(
            """
            () => {
                const caja = document.querySelector('#tabla-personas .caja-tabla');
                const antes = document.querySelector('#tabla-personas thead th').getBoundingClientRect().top;
                caja.scrollTop = caja.scrollHeight;
                const despues = document.querySelector('#tabla-personas thead th').getBoundingClientRect().top;
                return { Antes: antes, Despues: despues, Desplazado: caja.scrollTop };
            }
            """);
        _escenario.Registrar($"encabezado al desplazar: {encabezado}");
        Assert.True(encabezado.GetProperty("Desplazado").GetDouble() > 0, "La tabla no se desplazó.");
        Assert.True(Math.Abs(encabezado.GetProperty("Antes").GetDouble() - encabezado.GetProperty("Despues").GetDouble()) < 1, "El encabezado se movió al desplazar.");
    }

    [Fact(DisplayName = "consulta-consumos · Los consumos de hoy a 1024 × 768: después de una atención, la pestaña Consumos los muestra sin pedir ningún código")]
    public async Task Los_consumos_de_hoy_a_1024_x_768()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await FijarArea(pagina, 1024, 768);
        await pagina.Locator("#codigo").FillAsync("0001");
        await pagina.Locator("#ingresar").ClickAsync();
        await pagina.Locator(".opcion").First.ClickAsync();
        await pagina.Locator("#confirmar").ClickAsync();
        await pagina.Locator("#listo").ClickAsync();
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-consumos").ClickAsync();

        await Assertions.Expect(pagina.Locator("#seccion-consumos tr.consumo")).ToHaveCountAsync(1);
        await Assertions.Expect(pagina.Locator("#seccion-consumos tr.consumo")).ToContainTextAsync("Persona sintética 01");
        await Assertions.Expect(pagina.Locator("#seccion-consumos input[type=password]")).ToHaveCountAsync(0);
        await Capturar(pagina, "15-consumos-del-dia");
    }

    [Fact(DisplayName = "presentacion-puesto · La página no rebota: en reposo, en la selección y con Configuración abierta, html y body con overscroll-behavior none, y el diálogo, la selección y las tablas con contain")]
    public async Task La_pagina_no_rebota()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await FijarArea(pagina, 1024, 768);
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        var reposo = await Desborde(pagina, "html", "body");
        await pagina.Locator("#codigo").FillAsync("0001");
        await pagina.Locator("#ingresar").ClickAsync();
        await Assertions.Expect(pagina.Locator(".opcion").First).ToBeVisibleAsync();
        var seleccion = await Desborde(pagina, "html", "body", ".lista");
        await pagina.Locator("#cancelar").ClickAsync();
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-personas").ClickAsync();
        await Assertions.Expect(pagina.Locator("#tabla-personas .caja-tabla")).ToBeVisibleAsync();
        var personas = await Desborde(pagina, "html", "body", ".dialogo", "#tabla-personas .caja-tabla");
        await pagina.Locator("#pestana-consumos").ClickAsync();
        await Assertions.Expect(pagina.Locator("#seccion-consumos .caja-tabla")).ToBeVisibleAsync();
        var consumos = await Desborde(pagina, "#seccion-consumos .caja-tabla");
        _escenario.Registrar($"overscroll-behavior-y: reposo {string.Join(", ", reposo)} · selección {string.Join(", ", seleccion)} · personas {string.Join(", ", personas)} · consumos {string.Join(", ", consumos)}");

        Assert.Equal(["none", "none"], reposo);
        Assert.Equal(["none", "none", "contain"], seleccion);
        Assert.Equal(["none", "none", "contain", "contain"], personas);
        Assert.Equal(["contain"], consumos);
    }

    [Fact(DisplayName = "presentacion-puesto · El navegador arranca sin rebote elástico: la línea de comandos del navegador del árbol del puesto lleva ElasticOverscroll en --disable-features")]
    public async Task El_navegador_arranca_sin_rebote_elastico()
    {
        using var playwright = await Playwright.CreateAsync();
        var (puesto, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        var webView2 = ProcesosYPuertos.Arbol(puesto.Id).Where(p => p.Value.Equals("msedgewebview2.exe", StringComparison.OrdinalIgnoreCase)).Select(p => p.Key).ToList();
        var lineas = LineasDeComando(webView2);
        var navegadores = lineas.Where(l => !l.Contains("--type=", StringComparison.Ordinal)).ToList();
        _escenario.Registrar($"navegador del puesto: {string.Join(" | ", navegadores.Select(l => System.Text.RegularExpressions.Regex.Match(l, "--disable-features=[^ \"]*").Value))}");

        Assert.NotEmpty(navegadores);
        Assert.Contains(navegadores, l => System.Text.RegularExpressions.Regex.IsMatch(l, "--disable-features=[^ \"]*ElasticOverscroll"));
    }

    [Fact(DisplayName = "presentacion-puesto · La ventana angosta sigue desplazándose: a 600 px de ancho, la página se desplaza en vertical hasta la última opción, y al tocarla el cartel muestra «Confirmar» dentro de la ventana sin desplazar más")]
    public async Task La_ventana_angosta_sigue_desplazandose()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await FijarArea(pagina, 600, 768);
        await pagina.Locator("#codigo").FillAsync("0001");
        await pagina.Locator("#ingresar").ClickAsync();
        await Assertions.Expect(pagina.Locator(".opcion").First).ToBeVisibleAsync();

        var ultima = pagina.Locator(".opcion").Last;
        await ultima.ScrollIntoViewIfNeededAsync();

        await Assertions.Expect(ultima).ToBeInViewportAsync();
        var desplazada = await pagina.EvaluateAsync<double>("() => document.scrollingElement.scrollTop");
        _escenario.Registrar($"ventana angosta: la página se desplazó {desplazada} px hasta la última opción");
        Assert.True(desplazada > 0, "La página no se desplazó para llegar a la última opción.");

        await ultima.ClickAsync();

        await Assertions.Expect(pagina.Locator("#cartel-eleccion #confirmar")).ToBeInViewportAsync(new() { Ratio = 1 });
        var despues = await pagina.EvaluateAsync<double>("() => document.scrollingElement.scrollTop");
        _escenario.Registrar($"ventana angosta: «Confirmar» del cartel entero en la ventana, con la página en {despues} px");
        Assert.Equal(desplazada, despues);
    }

    /// <summary>El overscroll-behavior-y calculado de cada selector, en orden.</summary>
    private static async Task<List<string>> Desborde(IPage pagina, params string[] selectores) =>
        (await pagina.EvaluateAsync<string[]>(
            "selectores => selectores.map(s => getComputedStyle(document.querySelector(s)).overscrollBehaviorY)", selectores)).ToList();

    /// <summary>La línea de comandos de cada proceso, leída con Get-CimInstance (el E2E no suma paquetes para esto).</summary>
    private static List<string> LineasDeComando(IReadOnlyList<int> pids)
    {
        if (pids.Count == 0)
        {
            return [];
        }

        var filtro = string.Join(" OR ", pids.Select(p => $"ProcessId={p}"));
        var inicio = new ProcessStartInfo("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", $"Get-CimInstance Win32_Process -Filter '{filtro}' | ForEach-Object {{ $_.CommandLine }}"])
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proceso = Process.Start(inicio)!;
        var salida = proceso.StandardOutput.ReadToEnd();
        proceso.WaitForExit(30_000);
        return salida.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>Personas sintéticas sin cupo en la versión activa, por SQL, con el puesto abierto: la pestaña las lee al abrirse.</summary>
    private void SumarPersonas(int cantidad)
    {
        using var conexion = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_escenario.Carpeta, "incomedor.db"),
            Pooling = false,
        }.ConnectionString);
        conexion.Open();
        using var transaccion = conexion.BeginTransaction();
        for (var i = 1; i <= cantidad; i++)
        {
            using var comando = conexion.CreateCommand();
            comando.Transaction = transaccion;
            comando.CommandText =
                """
                INSERT INTO tbComedorPersonas (comvcCodigo, comperCodigo, comperNombreVisible, comperActivo, comperVersionMaterialIdentidad)
                SELECT comvcCodigoActiva, $id, $nombre, 1, 1 FROM tbComedorConfiguracionPuestos
                """;
            comando.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            comando.Parameters.AddWithValue("$nombre", $"Persona de carga {i:000}");
            comando.ExecuteNonQuery();
        }

        transaccion.Commit();
    }

    private static async Task FijarArea(IPage pagina, int ancho, int alto)
    {
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = ancho, ["height"] = alto, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
    }

    /// <summary>JPEG a la escala CSS, como las capturas de docs/capturas, en la carpeta de resultados de la prueba.</summary>
    private async Task Capturar(IPage pagina, string nombre)
    {
        var ruta = Path.Combine(_escenario.Resultados, $"{nombre}.jpg");
        await pagina.ScreenshotAsync(new() { Path = ruta, Type = ScreenshotType.Jpeg, Quality = 85, Scale = ScreenshotScale.Css });
        _escenario.Registrar($"captura {ruta}");
    }
}
