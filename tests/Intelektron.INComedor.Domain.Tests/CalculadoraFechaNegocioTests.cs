using Intelektron.INComedor.Domain;

namespace Intelektron.INComedor.Domain.Tests;

public class CalculadoraFechaNegocioTests
{
    private const string ZonaBuenosAires = "America/Argentina/Buenos_Aires";

    [Fact(DisplayName = "AT04 · Confirmaciones a ambos lados del corte: 02:59:59Z y 03:00:00Z caen en días consecutivos")]
    public void Ambos_lados_del_corte_dan_dias_consecutivos()
    {
        var antes = DateTimeOffset.Parse("2026-10-19T02:59:59Z");
        var despues = DateTimeOffset.Parse("2026-10-19T03:00:00Z");

        var fechaAntes = CalculadoraFechaNegocio.Calcular(antes, ZonaBuenosAires, new TimeOnly(0, 0));
        var fechaDespues = CalculadoraFechaNegocio.Calcular(despues, ZonaBuenosAires, new TimeOnly(0, 0));

        Assert.Equal(new DateOnly(2026, 10, 18), fechaAntes.Valor);
        Assert.Equal(new DateOnly(2026, 10, 19), fechaDespues.Valor);
    }

    [Fact(DisplayName = "AT04 · Confirmaciones a ambos lados del corte: un corte distinto de 00:00 desplaza el día")]
    public void Un_corte_distinto_de_medianoche_desplaza_el_dia()
    {
        var corte = new TimeOnly(4, 0);
        var antesDelCorte = DateTimeOffset.Parse("2026-10-19T06:59:59Z");
        var enElCorte = DateTimeOffset.Parse("2026-10-19T07:00:00Z");

        var fechaAntes = CalculadoraFechaNegocio.Calcular(antesDelCorte, ZonaBuenosAires, corte);
        var fechaEnElCorte = CalculadoraFechaNegocio.Calcular(enElCorte, ZonaBuenosAires, corte);

        Assert.Equal(new DateOnly(2026, 10, 18), fechaAntes.Valor);
        Assert.Equal(new DateOnly(2026, 10, 19), fechaEnElCorte.Valor);
    }
}
