using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Los datos nuevos del comprobante en la base (comprobante-local de enriquecer-comprobante, D1 y D2): el nombre del
/// comensal al confirmar y la numeración correlativa, asignada en la transacción de RB06 y vista en Consumos.
/// </summary>
public class ComprobanteEnriquecidoTests : ConfirmacionTestBase
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    [Fact(DisplayName = "comprobante-local · El contenido del comprobante: los datos guardados llevan el nombre visible del comensal y nunca su código; con el código de siempre, ningún número guardado")]
    public void Los_datos_llevan_el_nombre()
    {
        var ticket = Confirmar(Unidad, Fixture.AlmuerzoComun);

        var datos = Datos(ticket);

        Assert.Equal("Persona habilitada", datos.GetProperty("PersonName").GetString());
        Assert.False(datos.TryGetProperty("TicketNumber", out _));
        Assert.DoesNotContain(FixtureSintetico.CodigoSintetico, datos.GetRawText(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "comprobante-local · Correlativo desde mil: dos confirmaciones dan el 1000 y el 1001, y Consumos los muestra y encuentra el 1001")]
    public void Correlativo_desde_mil()
    {
        var unidad = new UnidadConfirmacion(Base, Reloj) { CorrelativoDesde = () => 1000 };

        var primero = Confirmar(unidad, Fixture.AlmuerzoComun);
        var segundo = Confirmar(unidad, Fixture.Refrigerio);

        Assert.Equal(1000, Datos(primero).GetProperty("TicketNumber").GetInt64());
        Assert.Equal(1001, Datos(segundo).GetProperty("TicketNumber").GetInt64());
        var consulta = new ConsultaConsumos(Base, Reloj);
        var filas = consulta.Leer(FiltroConsumos.UltimasHoras(Reloj.GetUtcNow(), Hoy)).Filas;
        Assert.Equal(["1000", "1001"], filas.Select(f => f.Numero).Order(StringComparer.Ordinal));
        var buscados = consulta.Leer(FiltroConsumos.UltimasHoras(Reloj.GetUtcNow(), Hoy) with { Texto = "1001" }).Filas;
        Assert.Contains(buscados, f => f.TicketId == segundo);
    }

    [Fact(DisplayName = "comprobante-local · Bajar el «desde» no reusa números: después del 1000, con el «desde» en 5, el próximo es el 1001")]
    public void Bajar_el_desde_no_reusa()
    {
        long desde = 1000;
        var unidad = new UnidadConfirmacion(Base, Reloj) { CorrelativoDesde = () => desde };
        Confirmar(unidad, Fixture.AlmuerzoComun);
        desde = 5;

        var siguiente = Confirmar(unidad, Fixture.Refrigerio);

        Assert.Equal(1001, Datos(siguiente).GetProperty("TicketNumber").GetInt64());
    }

    [Fact(DisplayName = "comprobante-local · El código de siempre: sin correlativo, Consumos muestra los primeros ocho caracteres del comprobante")]
    public void El_codigo_de_siempre()
    {
        var ticket = Confirmar(Unidad, Fixture.AlmuerzoComun);

        var fila = Assert.Single(new ConsultaConsumos(Base, Reloj).Leer(FiltroConsumos.UltimasHoras(Reloj.GetUtcNow(), Hoy)).Filas);

        Assert.Equal(ticket.ToString("D")[..8], fila.Numero);
    }

    private Guid Confirmar(UnidadConfirmacion unidad, OpcionFixture opcion)
    {
        var resultado = unidad.Confirmar(Solicitud(Fixture.Habilitada, opcion));
        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        return resultado.TicketId!.Value;
    }

    private JsonElement Datos(Guid ticket)
    {
        using var conexion = Abrir();
        var datos = Sql.Escalar<string>(conexion, "SELECT comcmpDatosImpresion FROM tbComedorComprobantes WHERE comcmpCodigo = $tk", ("$tk", ticket));
        return JsonDocument.Parse(datos).RootElement.Clone();
    }
}
