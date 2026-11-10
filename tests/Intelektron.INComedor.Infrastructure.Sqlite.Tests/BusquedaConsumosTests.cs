using System.Globalization;
using System.Text.Json;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// La consulta de consumos con las últimas 24 horas, el texto, el número de comprobante y el código (consulta-consumos de
/// buscar-y-exportar-consumos, D1 a D3). Sobre una base SQLite real temporal con el fixture sintético; el reloj está fijo
/// en 2026-10-19T15:00:00Z, las 12:00 del puesto.
/// </summary>
public class BusquedaConsumosTests : ConfirmacionTestBase
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    private readonly ConsultaConsumos _consulta;
    private int _secuencia;

    public BusquedaConsumosTests()
    {
        _consulta = new ConsultaConsumos(Base, Reloj);
    }

    [Fact(DisplayName = "consulta-consumos · Las últimas 24 horas por defecto: entran los de las 24 horas anteriores, también los de ayer, y no el de hace 24 horas y un minuto")]
    public void Las_ultimas_24_horas()
    {
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy, new TimeOnly(11, 0));
        Insertar(Fixture.Habilitada, Fixture.Refrigerio, Hoy, new TimeOnly(8, 0));
        var ayer = Insertar(Fixture.SinDerecho, Fixture.Refrigerio, Hoy.AddDays(-1), new TimeOnly(13, 0));
        var fuera = Insertar(Fixture.SinDerecho, Fixture.AlmuerzoComun, Hoy.AddDays(-1), new TimeOnly(11, 59));

        var resultado = _consulta.Leer(FiltroConsumos.UltimasHoras(Reloj.GetUtcNow(), Hoy));

        Assert.Null(resultado.Rechazo);
        Assert.Equal(3, resultado.Filas.Count);
        Assert.Contains(resultado.Filas, f => f.ConsumoId == ayer);
        Assert.DoesNotContain(resultado.Filas, f => f.ConsumoId == fuera);
        Assert.Equal(resultado.Filas.OrderByDescending(f => f.ConfirmadoUtc).Select(f => f.ConsumoId), resultado.Filas.Select(f => f.ConsumoId));
    }

    [Fact(DisplayName = "consulta-consumos · Buscar por texto: sin distinguir mayúsculas ni acentos, «SIN DÉRECHO» encuentra a «Persona sin derecho» y no a «Persona habilitada»")]
    public void Un_texto_sin_acentos_ni_mayusculas()
    {
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy, new TimeOnly(11, 0));
        var buscado = Insertar(Fixture.SinDerecho, Fixture.Refrigerio, Hoy, new TimeOnly(10, 0));

        var resultado = _consulta.Leer(FiltroConsumos.UltimasHoras(Reloj.GetUtcNow(), Hoy) with { Texto = "SIN DÉRECHO" });

        Assert.Equal([buscado], resultado.Filas.Select(f => f.ConsumoId));
    }

    [Fact(DisplayName = "consulta-consumos · Buscar un número de comprobante: los primeros ocho caracteres del comprobante dan sólo su consumo, que lleva el comprobante y su trabajo original")]
    public void Un_numero_de_comprobante()
    {
        var confirmado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));
        Insertar(Fixture.SinDerecho, Fixture.Refrigerio, Hoy, new TimeOnly(10, 0));
        var ticket = Guid.Parse(Texto("SELECT comcmpCodigo FROM tbComedorComprobantes"));
        var numero = ticket.ToString("D")[..8];

        var resultado = _consulta.Leer(FiltroConsumos.UltimasHoras(Reloj.GetUtcNow(), Hoy) with { Texto = numero.ToUpperInvariant() });

        var fila = Assert.Single(resultado.Filas);
        Assert.Equal(ticket, fila.TicketId);
        Assert.Equal(numero, fila.Numero);
        Assert.NotNull(fila.TrabajoOriginalId);
        Assert.Equal(confirmado.ConsumptionId, fila.ConsumoId);
        Assert.Equal(confirmado.TicketId, ticket);
    }

    [Theory(DisplayName = "consulta-consumos · Comodines en el texto: «%», «_» y «\\» se buscan como caracteres, y ningún nombre los tiene")]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("\\")]
    public void Comodines_en_el_texto(string texto)
    {
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy, new TimeOnly(11, 0));
        Insertar(Fixture.SinDerecho, Fixture.Refrigerio, Hoy, new TimeOnly(10, 0));

        var resultado = _consulta.Leer(FiltroConsumos.UltimasHoras(Reloj.GetUtcNow(), Hoy) with { Texto = texto });

        Assert.Null(resultado.Rechazo);
        Assert.Empty(resultado.Filas);
    }

    [Fact(DisplayName = "consulta-consumos · El código de una persona: filtra por su persona, con los ceros y los espacios como al identificar")]
    public void El_codigo_de_una_persona()
    {
        var suyo = Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy, new TimeOnly(11, 0));
        Insertar(Fixture.SinDerecho, Fixture.Refrigerio, Hoy, new TimeOnly(10, 0));

        var resultado = _consulta.Leer(FiltroConsumos.UltimasHoras(Reloj.GetUtcNow(), Hoy) with { Codigo = $"  {FixtureSintetico.CodigoSintetico} " });

        Assert.Equal([suyo], resultado.Filas.Select(f => f.ConsumoId));
    }

    [Fact(DisplayName = "consulta-consumos · Un código que no es de nadie: se rechaza sin consultar, con UNKNOWN_CODE")]
    public void Un_codigo_que_no_es_de_nadie()
    {
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy, new TimeOnly(11, 0));

        var resultado = _consulta.Leer(FiltroConsumos.UltimasHoras(Reloj.GetUtcNow(), Hoy) with { Codigo = "7777" });

        Assert.Equal(ConsultaConsumos.CodigoDesconocido, resultado.Rechazo);
        Assert.Empty(resultado.Filas);
    }

    private Guid Insertar(PersonaFixture persona, OpcionFixture opcion, DateOnly fecha, TimeOnly horaLocal)
    {
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "PRAGMA foreign_keys = OFF");
        var id = Guid.NewGuid();
        var instante = new DateTimeOffset(fecha.ToDateTime(horaLocal), TimeSpan.FromHours(-3));
        Sql.Ejecutar(conexion,
            """
            INSERT INTO tbComedorConsumos (comconCodigo, comicCodigo, comvcCodigo, comperCodigo, commenCodigo, comicmCodigo, comconFechaNegocio,
                comconCantidad, comconVersionMenu, comconVersionPoliticas, comconVersionCatalogo, comconSecuenciaCliente,
                comconFechaConfirmacionLocalUtc, comconPrecioAplicado)
            VALUES ($con, $ic, $vc, $per, $men, NULL, $fecha, 1, $version, 1, 1, $secuencia, $instante, $precio)
            """,
            ("$con", id), ("$ic", Guid.NewGuid()), ("$vc", Fixture.SnapshotId), ("$per", persona.Id), ("$men", opcion.Id),
            ("$fecha", fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), ("$version", opcion.Version), ("$secuencia", 1000 + ++_secuencia),
            ("$instante", Iso(instante)), ("$precio", JsonSerializer.Serialize(new { Currency = "ARS", Scale = 2, EmployeeChargeMinor = 50000 })));
        return id;
    }

    private string Texto(string sql)
    {
        using var conexion = Abrir();
        return Sql.Escalar<string>(conexion, sql);
    }
}
