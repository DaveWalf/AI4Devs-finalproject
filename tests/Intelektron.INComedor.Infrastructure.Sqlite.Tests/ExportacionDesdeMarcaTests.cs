namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Lo que la exportación automática lee de la base (exportacion-consumos de exportar-consumos-automaticamente, D1): los
/// consumos desde una marca, en orden de secuencia, y la línea CSV de cada uno, con las columnas de la exportación de
/// Consumos y sin ningún código.
/// </summary>
public class ExportacionDesdeMarcaTests : ConfirmacionTestBase
{
    [Fact(DisplayName = "exportacion-consumos · Un archivo que crece: los consumos desde una marca salen en orden de secuencia, sin los ya exportados y hasta el tope")]
    public void Los_consumos_desde_una_marca()
    {
        var primero = Confirmar(Fixture.AlmuerzoComun);
        var segundo = Confirmar(Fixture.Refrigerio);
        var consulta = new ConsultaConsumos(Base, Reloj);

        var todos = consulta.Desde(0, 10);
        Assert.Equal([primero, segundo], todos.Select(c => c.Consumo.TicketId!.Value));
        Assert.True(todos[0].Secuencia < todos[1].Secuencia);

        var nuevos = consulta.Desde(todos[0].Secuencia, 10);
        Assert.Equal(segundo, Assert.Single(nuevos).Consumo.TicketId);
        Assert.Single(consulta.Desde(0, 1));
        Assert.Empty(consulta.Desde(todos[1].Secuencia, 10));
    }

    [Fact(DisplayName = "exportacion-consumos · Un archivo que crece: la línea CSV de un consumo tiene las columnas de la exportación de Consumos, sin el código, y el apóstrofo delante de una fórmula")]
    public void La_linea_de_un_consumo()
    {
        Confirmar(Fixture.AlmuerzoComun);
        var consumo = Assert.Single(new ConsultaConsumos(Base, Reloj).Desde(0, 10)).Consumo;

        var fila = ExportacionConsumos.FilaDe(consumo, TimeZoneInfo.Utc);
        var linea = EscritorPlanilla.LineaCsv(fila);

        Assert.Equal(ExportacionConsumos.Columnas.Count, fila.Count);
        Assert.Equal(["Fecha y hora", "N.º", "Persona", "Opción", "Servicio", "Cargo", "Moneda", "Estado"], ExportacionConsumos.Columnas);
        Assert.Contains("\"Persona habilitada\"", linea, StringComparison.Ordinal);
        Assert.DoesNotContain(FixtureSintetico.CodigoSintetico, linea, StringComparison.Ordinal);
        Assert.Equal("\"'=1+1\";\"texto\";\"1500,50\"", EscritorPlanilla.LineaCsv(["=1+1", "texto", new NumeroPlanilla(1500.5m, 2)]));
    }

    private Guid Confirmar(OpcionFixture opcion)
    {
        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, opcion));
        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        return resultado.TicketId!.Value;
    }
}
