using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El globo del dato completo de Consumos (D4 de ver-consumos-en-un-renglon): tocar la hora, la persona o la opción de una
/// fila, o dejar el mouse medio segundo encima, muestra ese dato entero (la fecha con el año), sin el código; se cierra con
/// otro toque, al salir el mouse o a los 5 s. Los plazos corren con un reloj cuyos temporizadores dispara la prueba.
/// </summary>
public class GloboDeConsumosTests
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    [Fact(DisplayName = "consulta-consumos · El dato completo al tocarlo: tocar la persona abre #globo-dato con el nombre entero, y otro toque lo cierra")]
    public async Task Tocar_la_persona_abre_y_cierra_el_globo()
    {
        using var puesto = new PuestoDePrueba();
        var (pestana, _) = await Pestana(puesto);

        var persona = pestana.Find("tr.consumo td.persona");
        await persona.PointerUpAsync(new PointerEventArgs { PointerType = "touch", ClientX = 200, ClientY = 300 });

        var globo = pestana.Find("#globo-dato");
        Assert.Equal("tooltip", globo.GetAttribute("role"));
        Assert.Equal("Persona sintética 01", globo.TextContent.Trim());

        await pestana.Find("tr.consumo td.persona").PointerUpAsync(new PointerEventArgs { PointerType = "touch" });
        Assert.Empty(pestana.FindAll("#globo-dato"));
    }

    [Fact(DisplayName = "consulta-consumos · El dato completo con el mouse: medio segundo sobre la hora abre el globo con «11/11/2026 13:51», y salir lo cierra")]
    public async Task El_mouse_medio_segundo_sobre_la_hora()
    {
        using var puesto = new PuestoDePrueba();
        var (pestana, reloj) = await Pestana(puesto, avanzarAl: "2026-11-11T16:51:00Z");

        await pestana.Find("tr.consumo td.hora").PointerEnterAsync(new PointerEventArgs { PointerType = "mouse" });
        Assert.Empty(pestana.FindAll("#globo-dato"));
        var espera = Assert.Single(reloj.Pendientes);
        Assert.Equal(TimeSpan.FromMilliseconds(500), espera.Primero);

        await pestana.InvokeAsync(espera.Disparar);
        Assert.Equal("11/11/2026 13:51", pestana.Find("#globo-dato").TextContent.Trim());

        await pestana.Find("tr.consumo td.hora").PointerLeaveAsync(new PointerEventArgs { PointerType = "mouse" });
        Assert.Empty(pestana.FindAll("#globo-dato"));
    }

    [Fact(DisplayName = "consulta-consumos · El mouse que sale antes del medio segundo no abre el globo")]
    public async Task El_mouse_que_sale_antes_no_abre_el_globo()
    {
        using var puesto = new PuestoDePrueba();
        var (pestana, reloj) = await Pestana(puesto);

        await pestana.Find("tr.consumo td.opcion").PointerEnterAsync(new PointerEventArgs { PointerType = "mouse" });
        var espera = Assert.Single(reloj.Pendientes);
        await pestana.Find("tr.consumo td.opcion").PointerLeaveAsync(new PointerEventArgs { PointerType = "mouse" });
        await pestana.InvokeAsync(espera.Disparar);

        Assert.Empty(pestana.FindAll("#globo-dato"));
    }

    [Fact(DisplayName = "consulta-consumos · El globo no queda abierto: con la opción a la vista, a los 5 s sin un toque se cierra y la tabla sigue como estaba")]
    public async Task A_los_cinco_segundos_se_cierra()
    {
        using var puesto = new PuestoDePrueba();
        var (pestana, reloj) = await Pestana(puesto);
        var antes = pestana.Find("table.tabla-consumos").TextContent;

        await pestana.Find("tr.consumo td.opcion").PointerUpAsync(new PointerEventArgs { PointerType = "touch" });
        Assert.NotEmpty(pestana.Find("#globo-dato").TextContent.Trim());
        var cierre = Assert.Single(reloj.Pendientes);
        Assert.Equal(TimeSpan.FromSeconds(5), cierre.Primero);

        await pestana.InvokeAsync(cierre.Disparar);

        Assert.Empty(pestana.FindAll("#globo-dato"));
        Assert.Equal(antes, pestana.Find("table.tabla-consumos").TextContent);
        Assert.Empty(pestana.FindAll("table.tabla-consumos [style], table.tabla-consumos [aria-describedby]"));
    }

    [Fact(DisplayName = "consulta-consumos · El globo no muestra el código: con la tabla consultada por 0001, el globo de la persona dice el nombre y no contiene 0001")]
    public async Task El_globo_no_muestra_el_codigo()
    {
        using var puesto = new PuestoDePrueba();
        var (pestana, _) = await Pestana(puesto);
        await pestana.Find("#consumos-codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pestana.Find("#consultar-consumos").ClickAsync(new());
        Assert.NotNull(pestana.Find("#filtro-codigo"));

        await pestana.Find("tr.consumo td.persona").PointerUpAsync(new PointerEventArgs { PointerType = "touch" });

        var globo = pestana.Find("#globo-dato");
        Assert.Equal("Persona sintética 01", globo.TextContent.Trim());
        Assert.DoesNotContain("0001", globo.OuterHtml);
    }

    [Fact(DisplayName = "consulta-consumos · Cambiar la consulta cierra el globo")]
    public async Task Cambiar_la_consulta_cierra_el_globo()
    {
        using var puesto = new PuestoDePrueba();
        var (pestana, _) = await Pestana(puesto);
        await pestana.Find("tr.consumo td.persona").PointerUpAsync(new PointerEventArgs { PointerType = "touch" });
        Assert.NotNull(pestana.Find("#globo-dato"));

        await pestana.Find("#consultar-consumos").ClickAsync(new());

        Assert.Empty(pestana.FindAll("#globo-dato"));
    }

    /// <summary>
    /// Un consumo de «Persona sintética 01» hecho en la pantalla y, aparte, la pestaña Consumos sobre la misma base, con el
    /// reloj de temporizadores de la prueba.
    /// </summary>
    private static async Task<(IRenderedComponent<PestanaConsumos> Pestana, RelojConTemporizadores Reloj)> Pestana(PuestoDePrueba puesto, string? avanzarAl = null)
    {
        if (avanzarAl is not null)
        {
            puesto.Reloj.Avanzar(DateTimeOffset.Parse(avanzarAl) - puesto.Reloj.Ahora);
        }

        var pantalla = puesto.Dibujar();
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
        await pantalla.Find("#listo").ClickAsync(new());

        var reloj = new RelojConTemporizadores(puesto.Reloj);
        var pestana = puesto.Contexto.Render<PestanaConsumos>(p => p
            .Add(x => x.Consulta, puesto.Servicio.Consumos!)
            .Add(x => x.Visible, true)
            .Add(x => x.Zona, Zona)
            .Add(x => x.Reloj, reloj));
        Assert.NotEmpty(pestana.FindAll("tr.consumo"));
        return (pestana, reloj);
    }

    /// <summary>El reloj de la prueba: la hora del puesto y temporizadores que sólo se disparan a mano.</summary>
    public sealed class RelojConTemporizadores(TimeProvider reloj) : TimeProvider
    {
        private readonly List<Temporizador> _temporizadores = [];

        /// <summary>Los temporizadores creados y todavía no descartados.</summary>
        public IReadOnlyList<Temporizador> Pendientes => _temporizadores.Where(t => !t.Descartado).ToList();

        public override DateTimeOffset GetUtcNow() => reloj.GetUtcNow();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var temporizador = new Temporizador(() => callback(state), dueTime);
            _temporizadores.Add(temporizador);
            return temporizador;
        }
    }

    public sealed class Temporizador(Action disparar, TimeSpan primero) : ITimer
    {
        public TimeSpan Primero { get; } = primero;

        public bool Descartado { get; private set; }

        public void Disparar()
        {
            if (!Descartado)
            {
                disparar();
            }
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) => !Descartado;

        public void Dispose() => Descartado = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
