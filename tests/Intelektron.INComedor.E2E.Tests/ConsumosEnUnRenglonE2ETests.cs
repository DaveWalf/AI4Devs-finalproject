using System.Text.Json;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// Cada consumo en un renglón (ver-consumos-en-un-renglon, D5): con el puesto compilado, a 1024 × 768 y por CDP, la tabla de
/// Consumos con una persona de 60 caracteres y una opción de 40, «Persona sintética 01» con «Almuerzo con gaseosa» y un
/// original fallido con «Recuperar». Mide que ninguna celda se parta, la letra, el corte con «…», la alineación de los botones
/// de la fila y el globo del dato completo con el mouse, con la tolerancia de 1 px de la regla de prolijidad.
/// </summary>
public class ConsumosEnUnRenglonE2ETests : IAsyncLifetime
{
    private const double Tolerancia = 1;
    private const string PersonaLarga = "Persona sintética con un nombre larguísimo para la tabla 060";
    private const string CodigoLargo = "0106";
    private const string OpcionLarga = "Almuerzo con nombre largo de opción 0040";
    private const string OpcionGaseosa = "Almuerzo con gaseosa";

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("consumos-un-renglon");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "consulta-consumos · Un renglón a 1024 × 768 (puesto compilado): con una persona de 60 caracteres, una opción de 40 y un original fallido, cada fila mide un renglón, la letra queda entre 14 y 16 px, la persona y la opción largas terminan en «…», los botones de la fila se alinean y el mouse abre el globo con el nombre entero")]
    public async Task Un_renglon_a_1024_x_768()
    {
        Assert.Equal(60, PersonaLarga.Length);
        Assert.Equal(40, OpcionLarga.Length);
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await FijarArea(pagina, 1024, 768);
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();

        // La persona larga y las dos opciones, por las pestañas Personas y Opciones, como AdministracionE2ETests.
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-personas").ClickAsync();
        await pagina.Locator("#abrir-personas").ClickAsync();
        var antes = await pagina.Locator("tr.persona").CountAsync();
        await pagina.Locator("#agregar-persona").ClickAsync();
        await Assertions.Expect(pagina.Locator("tr.persona")).ToHaveCountAsync(antes + 1);
        var nueva = pagina.Locator("tr.persona").Last;
        await nueva.Locator("input.nombre-persona").FillAsync(PersonaLarga);
        await nueva.Locator("input.codigo-persona").FillAsync(CodigoLargo);
        await nueva.Locator("td.cupo[data-cupo=\"ALMUERZO\"] input.limite").FillAsync("2");
        await pagina.Locator("#guardar-panel").ClickAsync();
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToContainTextAsync("Se guardó la versión");
        await pagina.Locator("#pestana-opciones").ClickAsync();
        await pagina.Locator("#abrir-opciones").ClickAsync();
        await pagina.Locator("article.opcion-admin").Nth(0).Locator("input.nombre-opcion").FillAsync(OpcionGaseosa);
        await pagina.Locator("article.opcion-admin").Nth(1).Locator("input.nombre-opcion").FillAsync(OpcionLarga);
        await pagina.Locator("#guardar-panel").ClickAsync();
        await Assertions.Expect(pagina.Locator("#aviso-guardado")).ToContainTextAsync("Se guardó la versión");
        await pagina.Locator("#cancelar-configuracion").ClickAsync();
        await Assertions.Expect(pagina.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);

        await Consumir(pagina, "0001", OpcionGaseosa);
        await Consumir(pagina, CodigoLargo, OpcionLarga);

        // El original fallido: la impresora sin papel en la tercera atención, y de nuevo disponible.
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-impresora").ClickAsync();
        await pagina.Locator("#impresora-sinpapel").ClickAsync();
        await Assertions.Expect(pagina.Locator("#impresora-sinpapel")).ToHaveAttributeAsync("aria-pressed", "true");
        await pagina.Locator("#cancelar-configuracion").ClickAsync();
        await Assertions.Expect(pagina.Locator("[role=\"dialog\"]")).ToHaveCountAsync(0);
        await Consumir(pagina, CodigoLargo, OpcionGaseosa);
        await EntradaConfiguracionE2E.EntrarAsync(pagina);
        await pagina.Locator("#pestana-impresora").ClickAsync();
        await pagina.Locator("#impresora-disponible").ClickAsync();
        await pagina.Locator("#pestana-consumos").ClickAsync();

        var filas = pagina.Locator("#seccion-consumos tr.consumo");
        await Assertions.Expect(filas).ToHaveCountAsync(3);
        await Assertions.Expect(pagina.Locator("#seccion-consumos tr.consumo .recuperar")).ToHaveCountAsync(1);
        var medidas = await Medir(pagina);
        foreach (var fila in medidas.Filas)
        {
            _escenario.Registrar($"1024 × 768, fila de {fila.Alto:F1} px de alto: {string.Join("; ", fila.Celdas.Select(c => c.ToString()))}");
            _escenario.Registrar($"1024 × 768, botones de la fila: {string.Join("; ", fila.Botones.Select(b => b.ToString()))}; la celda termina en {fila.AccionesDerecha:F1}");
        }

        _escenario.Registrar($"1024 × 768, letra de la tabla {medidas.LetraTabla}, de los encabezados {medidas.LetraEncabezado}; tabla de {medidas.AnchoTabla} px en una caja de {medidas.AnchoCaja} px");
        await _escenario.CapturarAsync(pagina, "consumos-un-renglon-1024x768");
        // La captura 15 de docs/capturas, como JPEG a la escala CSS, igual que las otras de esa carpeta (tarea 4.1).
        var documento = Path.Combine(_escenario.Resultados, "15-consumos-del-dia.jpg");
        await pagina.ScreenshotAsync(new() { Path = documento, Type = ScreenshotType.Jpeg, Quality = 85, Scale = ScreenshotScale.Css });
        _escenario.Registrar($"captura {Path.GetFileName(documento)}");

        _escenario.Registrar("anchos de texto: " + await pagina.EvaluateAsync<string>(
            """
            () => { const c = document.createElement('canvas').getContext('2d'); const fam = getComputedStyle(document.querySelector('#seccion-consumos td')).fontFamily;
              const t = ['11/11 13:51', 'ffffffff', 'dddddddd', 'REFRIGERIO', 'ALMUERZO', 'ARS 2.000,00', 'Sin cargo', 'Incierto', 'Enviado', 'En cola', 'Persona sintética', 'Almuerzo con', 'Ver', 'Reimprimir', 'Recuperar', '0'];
              const h = ['Hora ▼', 'N.º', 'Servicio ▲', 'Cargo ▲', 'Estado ▲', 'Persona ▲', 'Opción ▲'];
              return [14, 15, 16].map(px => { c.font = px + 'px ' + fam; const r = t.map(x => x + '=' + c.measureText(x).width.toFixed(1));
                c.font = '700 ' + px + 'px ' + fam; return px + 'px: ' + r.concat(h.map(x => x + '=' + c.measureText(x).width.toFixed(1))).join(', '); }).join(' | '); }
            """));
        var letra = double.Parse(medidas.LetraTabla.Replace("px", string.Empty), System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(letra, 14, 16);
        Assert.Equal(medidas.LetraTabla, medidas.LetraEncabezado);
        _escenario.Registrar($"1024 × 768, el texto del encabezado «Persona» empieza en {medidas.TextoEncabezadoPersona:F1} px y el de su celda en {medidas.TextoCeldaPersona:F1} px");
        Assert.True(Math.Abs(medidas.TextoEncabezadoPersona - medidas.TextoCeldaPersona) <= Tolerancia, "El encabezado «Persona» no empieza en el borde del texto de su columna.");
        Assert.True(medidas.AnchoTabla <= medidas.AnchoCaja + Tolerancia, $"La tabla mide {medidas.AnchoTabla} px en una caja de {medidas.AnchoCaja} px.");
        var altoUnRenglon = medidas.Filas.Min(f => f.Alto);
        foreach (var fila in medidas.Filas)
        {
            Assert.True(Math.Abs(fila.Alto - altoUnRenglon) <= Tolerancia, $"Una fila mide {fila.Alto} px, contra {altoUnRenglon} px.");
            Assert.All(fila.Celdas, c => Assert.True(c.Renglones <= 1, $"{c.Clase} ocupa {c.Renglones} renglones: «{c.Texto}»."));
            Assert.All(fila.Celdas.Where(c => c.Clase is not ("persona" or "opcion")),
                c => Assert.True(c.Desborda <= Tolerancia, $"{c.Clase} desborda {c.Desborda} px: «{c.Texto}»."));
            Assert.All(fila.Celdas.Where(c => c.Clase is "persona" or "opcion"), c => Assert.Equal("ellipsis", c.Corte));
            Assert.All(fila.Botones, b => Assert.True(Math.Abs(b.Alto - fila.Botones[0].Alto) <= Tolerancia && b.Alto >= 44 && b.Letra == fila.Botones[0].Letra
                && Math.Abs(b.Arriba - fila.Botones[0].Arriba) <= Tolerancia, $"{b.Clase}: {b}, contra {fila.Botones[0]}"));
        }

        Assert.All(medidas.Filas, f => Assert.True(f.Botones[^1].Derecha <= f.AccionesDerecha + Tolerancia,
            $"«{f.Botones[^1].Clase}» termina en {f.Botones[^1].Derecha} px y su celda en {f.AccionesDerecha} px."));
        var verIzquierda = medidas.Filas.Select(f => f.Botones.Single(b => b.Clase == "ver").Izquierda).ToList();
        Assert.All(verIzquierda, x => Assert.True(Math.Abs(x - verIzquierda[0]) <= Tolerancia, $"«Ver» empieza en {x} px, contra {verIzquierda[0]} px."));
        Assert.Contains(medidas.Filas, f => f.Botones.Any(b => b.Clase == "recuperar"));
        var larga = medidas.Filas.Single(f => f.Celdas.Single(c => c.Clase == "opcion").Texto == OpcionLarga);
        Assert.Equal(PersonaLarga, larga.Celdas.Single(c => c.Clase == "persona").Texto);
        Assert.True(larga.Celdas.Single(c => c.Clase == "persona").Desborda > Tolerancia, "La persona de 60 caracteres no se corta.");
        Assert.True(larga.Celdas.Single(c => c.Clase == "opcion").Desborda > Tolerancia, "La opción de 40 caracteres no se corta.");
        var visiblePersona = medidas.Filas.Min(f => f.Celdas.Single(c => c.Clase == "persona").Visible);
        var visibleOpcion = medidas.Filas.Min(f => f.Celdas.Single(c => c.Clase == "opcion").Visible);
        _escenario.Registrar($"1024 × 768, a la vista: persona {visiblePersona:F1} px para «Persona sintética…» de {medidas.PrefijoPersona:F1} px; opción {visibleOpcion:F1} px para «Almuerzo con…» de {medidas.PrefijoOpcion:F1} px");
        Assert.True(visiblePersona >= medidas.PrefijoPersona, $"La persona muestra {visiblePersona} px y «Persona sintética…» pide {medidas.PrefijoPersona} px.");
        Assert.True(visibleOpcion >= medidas.PrefijoOpcion, $"La opción muestra {visibleOpcion} px y «Almuerzo con…» pide {medidas.PrefijoOpcion} px.");

        // El globo con el mouse: medio segundo sobre la persona larga muestra el nombre entero, anclado debajo de su celda.
        var celda = filas.Locator("td.persona", new() { HasTextString = PersonaLarga }).First;
        await celda.HoverAsync();
        var globo = pagina.Locator("#globo-dato");
        await Assertions.Expect(globo).ToHaveTextAsync(PersonaLarga);
        var cajas = await pagina.EvaluateAsync<string>(
            """
            n => { const c = [...document.querySelectorAll('#seccion-consumos td.persona')].find(t => t.textContent.trim() === n).getBoundingClientRect();
              const g = document.querySelector('#globo-dato').getBoundingClientRect();
              return JSON.stringify({ CeldaIzquierda: c.left, CeldaAbajo: c.bottom, GloboIzquierda: g.left, GloboArriba: g.top, GloboDerecha: g.right, Ancho: window.innerWidth }); }
            """, PersonaLarga);
        _escenario.Registrar($"1024 × 768, el globo de la persona con el mouse: {cajas}");
        await _escenario.CapturarAsync(pagina, "consumos-globo-1024x768");
        var posicion = JsonSerializer.Deserialize<Dictionary<string, double>>(cajas)!;
        Assert.True(Math.Abs(posicion["GloboIzquierda"] - posicion["CeldaIzquierda"]) <= Tolerancia, $"El globo no empieza en el borde de su celda: {cajas}");
        Assert.True(posicion["GloboArriba"] >= posicion["CeldaAbajo"] - Tolerancia, $"El globo tapa su celda: {cajas}");
        Assert.True(posicion["GloboDerecha"] <= posicion["Ancho"], $"El globo sale de la ventana: {cajas}");
        await pagina.Locator("#consultar-consumos").HoverAsync();
        await Assertions.Expect(globo).ToHaveCountAsync(0);
    }

    /// <summary>Una atención completa en reposo: el código, la opción por su nombre (o la primera) y «Listo».</summary>
    private static async Task Consumir(IPage pagina, string codigo, string? opcion)
    {
        await pagina.Locator("#codigo").FillAsync(codigo);
        await Assertions.Expect(pagina.Locator("#codigo")).ToHaveValueAsync(codigo);
        await pagina.Locator("#ingresar").ClickAsync();
        var elegida = opcion is null ? pagina.Locator(".opcion").First : pagina.Locator(".opcion", new() { HasTextString = opcion });
        await elegida.ClickAsync();
        await pagina.Locator("#confirmar").ClickAsync();
        await pagina.Locator("#listo").ClickAsync();
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
    }

    /// <summary>Cada fila con sus celdas (renglones, desborde y corte) y sus botones, y la letra y el ancho de la tabla.</summary>
    private static async Task<Medidas> Medir(IPage pagina)
    {
        var json = await pagina.EvaluateAsync<string>(
            """
            () => {
              const renglones = e => { const r = document.createRange(); r.selectNodeContents(e);
                const tops = new Set([...r.getClientRects()].filter(x => x.width > 0).map(x => Math.round(x.top))); return tops.size; };
              const tabla = document.querySelector('#seccion-consumos .caja-tabla');
              const lienzo = document.createElement('canvas').getContext('2d');
              const inicio = e => { const r = document.createRange(); r.selectNodeContents(e); return r.getClientRects()[0].left; };
              const medir = t => { const e = getComputedStyle(document.querySelector('#seccion-consumos tr.consumo td.persona'));
                lienzo.font = e.fontSize + ' ' + e.fontFamily; return lienzo.measureText(t).width; };
              return JSON.stringify({
                Filas: [...document.querySelectorAll('#seccion-consumos tr.consumo')].map(f => ({
                  Alto: f.getBoundingClientRect().height,
                  Celdas: [...f.querySelectorAll('td:not(.acciones-consumo)')].map(c => { const e = getComputedStyle(c);
                    return { Clase: c.className || 'sin-clase', Texto: c.textContent.trim(), Renglones: renglones(c), Desborda: c.scrollWidth - c.clientWidth,
                      Corte: e.textOverflow, Visible: c.clientWidth - parseFloat(e.paddingLeft) - parseFloat(e.paddingRight) }; }),
                  Botones: [...f.querySelectorAll('.acciones-consumo button')].map(b => { const x = b.getBoundingClientRect();
                    return { Clase: b.className, Izquierda: x.left, Derecha: x.right, Arriba: x.top, Alto: x.height, Letra: getComputedStyle(b).fontSize }; }),
                  AccionesDerecha: f.querySelector('.acciones-consumo').getBoundingClientRect().right
                })),
                LetraTabla: getComputedStyle(document.querySelector('#seccion-consumos tr.consumo td')).fontSize,
                LetraEncabezado: getComputedStyle(document.querySelector('#seccion-consumos th button.ordenar')).fontSize,
                AnchoTabla: tabla.scrollWidth, AnchoCaja: tabla.clientWidth,
                TextoEncabezadoPersona: inicio(document.querySelector('#seccion-consumos th[data-columna="Persona"] button.ordenar')),
                TextoCeldaPersona: inicio(document.querySelector('#seccion-consumos tr.consumo td.persona')),
                PrefijoPersona: medir('Persona sintética…'), PrefijoOpcion: medir('Almuerzo con…')
              });
            }
            """);
        return JsonSerializer.Deserialize<Medidas>(json)!;
    }

    private static async Task FijarArea(IPage pagina, int ancho, int alto)
    {
        var cdp = await pagina.Context.NewCDPSessionAsync(pagina);
        await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>
        {
            ["width"] = ancho, ["height"] = alto, ["deviceScaleFactor"] = 1, ["mobile"] = false,
        });
    }

    private sealed record Celda(string Clase, string Texto, int Renglones, double Desborda, string Corte, double Visible)
    {
        public override string ToString() => $"{Clase} «{Texto}» en {Renglones} renglón, desborda {Desborda:F0} px, corte {Corte}";
    }

    private sealed record Boton(string Clase, double Izquierda, double Derecha, double Arriba, double Alto, string Letra)
    {
        public override string ToString() => $"{Clase} de x {Izquierda:F1} a {Derecha:F1}, arriba {Arriba:F1}, alto {Alto:F1} px, letra {Letra}";
    }

    private sealed record Fila(double Alto, Celda[] Celdas, Boton[] Botones, double AccionesDerecha);

    private sealed record Medidas(Fila[] Filas, string LetraTabla, string LetraEncabezado, double AnchoTabla, double AnchoCaja, double TextoEncabezadoPersona, double TextoCeldaPersona, double PrefijoPersona, double PrefijoOpcion);
}
