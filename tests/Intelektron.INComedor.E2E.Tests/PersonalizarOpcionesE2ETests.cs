using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// El catálogo a la vista, la personalización de una opción y la atención con ella, con el puesto compilado
/// (personalizar-opciones, D6): por CDP y a 1024 × 768, con la tolerancia de 1 px de la regla de prolijidad.
/// </summary>
public class PersonalizarOpcionesE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;

    /// <summary>Un PNG sintético de un punto, que la tarjeta estira de fondo.</summary>
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("personalizar-opciones");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "administracion-local · Personalizar una opción (puesto compilado): a 1024 × 768, las tarjetas del catálogo miden lo mismo con sus botones parejos y «Editar» enfoca el nombre; el cartel tiene sus nueve fondos y cuatro pasos parejos; la atención de 0001 dibuja un color a 75 % y una imagen, oculta el precio de otra y deja el texto alineado con las hermanas")]
    public async Task Catalogo_cartel_y_atencion()
    {
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = 1024, ["height"] = 768, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });

        // Las dos opciones que se van a personalizar: la primera que ve 0001 y la primera con importe.
        await pagina.Locator("#codigo").FillAsync("0001");
        await pagina.Locator("#ingresar").ClickAsync();
        await Assertions.Expect(pagina.Locator(".opcion").First).ToBeVisibleAsync();
        var nombres = (await pagina.Locator(".opcion .nombre").AllTextContentsAsync()).Select(n => n.Trim()).ToList();
        var conFondo = nombres[0];
        var conImagen = nombres[1];
        var sinPrecio = (await pagina.Locator(".opcion:has(.cargo) .nombre").AllTextContentsAsync()).Select(n => n.Trim())
            .First(n => n != conFondo && n != conImagen);
        await pagina.Locator("#cancelar").ClickAsync();
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-opciones").ClickAsync();
        await Assertions.Expect(pagina.Locator("#catalogo-opciones article.opcion-catalogo").First).ToBeVisibleAsync();
        var catalogo = JsonSerializer.Deserialize<Caja[]>(await pagina.EvaluateAsync<string>(
            """
            () => JSON.stringify([...document.querySelectorAll('#catalogo-opciones article.opcion-catalogo')].slice(0, 3).flatMap((a, i) => {
              const caja = b => ({ Arriba: b.top, Alto: b.height, Ancho: b.width, Izquierda: b.left, Derecha: b.right });
              const t = a.getBoundingClientRect();
              return [{ Id: 'tarjeta-' + i, ...caja(t), Letra: getComputedStyle(a).fontSize },
                ...[...a.querySelectorAll('.acciones-opcion button')].map(e => ({ Id: e.className + '-' + i, ...caja(e.getBoundingClientRect()), Letra: getComputedStyle(e).fontSize }))];
            }))
            """))!;
        var cerrar = JsonSerializer.Deserialize<Caja>(await pagina.EvaluateAsync<string>(
            "() => { const e = document.querySelector('#cancelar-configuracion'), b = e.getBoundingClientRect(); return JSON.stringify({ Id: 'cerrar', Arriba: b.top, Alto: b.height, Ancho: b.width, Izquierda: b.left, Derecha: b.right, Letra: getComputedStyle(e).fontSize }); }"))!;
        _escenario.Registrar("1024 × 768, el catálogo de Opciones: " + string.Join("; ", catalogo.Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1} px desde ({c.Izquierda:F1}, {c.Arriba:F1}), letra {c.Letra}"))
            + $"; «Cerrar» de {cerrar.Alto:F1} px, letra {cerrar.Letra}");
        await _escenario.CapturarAsync(pagina, "opciones-catalogo-1024x768");
        var tarjetas = catalogo.Where(c => c.Id.StartsWith("tarjeta-", StringComparison.Ordinal)).ToList();
        Assert.All(tarjetas, c => Assert.True(Math.Abs(c.Ancho - tarjetas[0].Ancho) <= Tolerancia, $"{c.Id} mide {c.Ancho} px y la primera {tarjetas[0].Ancho}"));
        var botones = catalogo.Where(c => !c.Id.StartsWith("tarjeta-", StringComparison.Ordinal)).ToList();
        Assert.All(botones, c => Assert.True(Math.Abs(c.Alto - cerrar.Alto) <= Tolerancia && c.Letra == cerrar.Letra && Math.Abs(c.Ancho - botones[0].Ancho) <= Tolerancia,
            $"{c.Id}: {c.Ancho} × {c.Alto} px, letra {c.Letra}, contra «Cerrar» de {cerrar.Alto} y {cerrar.Letra}"));

        // «Editar» en una opción deja el foco en su nombre, en la edición de siempre.
        await Tarjeta(pagina, conFondo).Locator("button.editar-opcion").ClickAsync();
        await Assertions.Expect(pagina.Locator("#panel-opciones")).ToBeVisibleAsync();
        await Assertions.Expect(pagina.Locator("article.opcion-admin input.nombre-opcion:focus")).ToHaveValueAsync(conFondo);
        await pagina.Locator("#cancelar-panel").ClickAsync();

        await Tarjeta(pagina, conFondo).Locator("button.personalizar-opcion").ClickAsync();
        await Assertions.Expect(pagina.Locator("#personalizar-opcion")).ToBeVisibleAsync();
        var cartel = JsonSerializer.Deserialize<MedidaCartel>(await pagina.EvaluateAsync<string>(
            """
            () => {
              const caja = e => { const b = e.getBoundingClientRect(); return { Id: e.className || e.id, Arriba: b.top, Alto: b.height, Ancho: b.width, Izquierda: b.left, Derecha: b.right, Letra: getComputedStyle(e).fontSize }; };
              return JSON.stringify({
                Fondos: [...document.querySelectorAll('#personalizar-opcion .colores-opcion button')].map(caja),
                Pasos: [...document.querySelectorAll('#personalizar-opcion .transparencias-opcion button')].map(caja),
                Abajo: document.querySelector('#personalizar-opcion').getBoundingClientRect().bottom, AltoVentana: window.innerHeight
              });
            }
            """))!;
        _escenario.Registrar($"1024 × 768, el cartel: {cartel.Fondos.Length} fondos de {cartel.Fondos.Min(f => f.Ancho):F1} a {cartel.Fondos.Max(f => f.Ancho):F1} × {cartel.Fondos[0].Alto:F1} px; "
            + $"{cartel.Pasos.Length} pasos de {cartel.Pasos.Min(f => f.Ancho):F1} a {cartel.Pasos.Max(f => f.Ancho):F1} × {cartel.Pasos[0].Alto:F1} px; termina en {cartel.Abajo:F1} de {cartel.AltoVentana}");
        await _escenario.CapturarAsync(pagina, "opciones-personalizar-1024x768");
        Assert.Equal(9, cartel.Fondos.Length);
        Assert.Equal(4, cartel.Pasos.Length);
        foreach (var grupo in new[] { cartel.Fondos, cartel.Pasos })
        {
            Assert.All(grupo, c => Assert.True(Math.Abs(c.Ancho - grupo[0].Ancho) <= Tolerancia && Math.Abs(c.Alto - grupo[0].Alto) <= Tolerancia && c.Alto >= 56 - Tolerancia,
                $"{c.Id}: {c.Ancho} × {c.Alto} px contra {grupo[0].Ancho} × {grupo[0].Alto}"));
        }

        Assert.True(cartel.Abajo <= cartel.AltoVentana, $"El cartel termina en {cartel.Abajo} px, fuera de la ventana");

        await pagina.Locator("#personalizar-opcion button.color-opcion").Nth(2).ClickAsync();
        await pagina.Locator("#personalizar-opcion button.transparencia-opcion", new() { HasText = "75 %" }).ClickAsync();
        await pagina.Locator("#guardar-presentacion").ClickAsync();
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToContainTextAsync(conFondo);

        var imagen = Path.Combine(_escenario.Carpeta, "fondo-sintetico.png");
        File.WriteAllBytes(imagen, Png);
        await Tarjeta(pagina, conImagen).Locator("button.personalizar-opcion").ClickAsync();
        await pagina.Locator("#archivo-imagen-opcion").SetInputFilesAsync(imagen);
        await Assertions.Expect(pagina.Locator("#personalizar-opcion .muestra-opcion.con-imagen")).ToHaveCountAsync(1);
        await pagina.Locator("#personalizar-opcion button.transparencia-opcion", new() { HasText = "25 %" }).ClickAsync();
        await pagina.Locator("#guardar-presentacion").ClickAsync();
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToContainTextAsync(conImagen);

        await Tarjeta(pagina, sinPrecio).Locator("button.personalizar-opcion").ClickAsync();
        await pagina.Locator("#precio-visible").ClickAsync();
        await pagina.Locator("#guardar-presentacion").ClickAsync();
        await Assertions.Expect(Tarjeta(pagina, sinPrecio).Locator(".muestra-opcion .cargo")).ToHaveCountAsync(0);
        await pagina.Locator("#cancelar-configuracion").ClickAsync();
        // Con el diálogo abierto la atención es inerte: el código se tipea recién cuando el diálogo se fue.
        await Assertions.Expect(pagina.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);

        await pagina.Locator("#codigo").FillAsync("0001");
        await pagina.Locator("#ingresar").ClickAsync();
        await Assertions.Expect(pagina.Locator(".opcion").First).ToBeVisibleAsync();
        var tarjeta = Opcion(pagina, conFondo);
        await Assertions.Expect(tarjeta).ToHaveClassAsync(new Regex("personalizada"));
        var fondo = await tarjeta.EvaluateAsync<string>("e => getComputedStyle(e, '::before').backgroundColor + ' · ' + getComputedStyle(e, '::before').opacity");
        _escenario.Registrar($"1024 × 768, la tarjeta de «{conFondo}» en la atención: fondo {fondo}");
        Assert.EndsWith("0.25", fondo, StringComparison.Ordinal);
        await Assertions.Expect(Opcion(pagina, conImagen)).ToHaveClassAsync(new Regex("con-imagen"));
        await Assertions.Expect(Opcion(pagina, sinPrecio).Locator(".cargo")).ToHaveCountAsync(0);

        // La prolijidad de las tarjetas hermanas: en cada columna, el mismo alto y el nombre desde el mismo borde,
        // personalizadas o no.
        var enAtencion = JsonSerializer.Deserialize<Caja[]>(await pagina.EvaluateAsync<string>(
            """
            () => JSON.stringify([...document.querySelectorAll('.opcion')].map(e => {
              const b = e.getBoundingClientRect(), n = e.querySelector('.nombre').getBoundingClientRect();
              return { Id: e.querySelector('.nombre').textContent.trim(), Arriba: b.top, Alto: b.height, Ancho: b.width, Izquierda: n.left, Derecha: b.left, Letra: getComputedStyle(e.querySelector('.nombre')).fontSize };
            }))
            """))!;
        _escenario.Registrar("1024 × 768, las tarjetas de 0001: " + string.Join("; ", enAtencion.Select(c => $"{c.Id} de {c.Ancho:F1} × {c.Alto:F1}, nombre desde {c.Izquierda:F1}, letra {c.Letra}")));
        foreach (var columna in enAtencion.GroupBy(c => Math.Round(c.Derecha)))
        {
            var primera = columna.First();
            Assert.All(columna, c => Assert.True(Math.Abs(c.Alto - primera.Alto) <= Tolerancia && Math.Abs(c.Izquierda - primera.Izquierda) <= Tolerancia && c.Letra == primera.Letra,
                $"«{c.Id}»: {c.Alto} px, nombre desde {c.Izquierda}, contra «{primera.Id}»: {primera.Alto} px desde {primera.Izquierda}"));
        }

        await _escenario.CapturarAsync(pagina, "atencion-personalizada-1024x768");
    }

    private static ILocator Tarjeta(IPage pagina, string nombre) =>
        pagina.Locator("#catalogo-opciones article.opcion-catalogo", new() { Has = pagina.Locator(".nombre-opcion", new() { HasTextRegex = Exacto(nombre) }) });

    private static ILocator Opcion(IPage pagina, string nombre) =>
        pagina.Locator(".opcion", new() { Has = pagina.Locator(".nombre", new() { HasTextRegex = Exacto(nombre) }) });

    /// <summary>El nombre entero: «Almuerzo» no tiene que encontrar «Almuerzo común».</summary>
    private static Regex Exacto(string nombre) => new($"^\\s*{Regex.Escape(nombre)}\\s*$");

    private sealed record Caja(string Id, double Arriba, double Alto, double Ancho, double Izquierda, double Derecha, string Letra);

    private sealed record MedidaCartel(Caja[] Fondos, Caja[] Pasos, double Abajo, double AltoVentana);
}
