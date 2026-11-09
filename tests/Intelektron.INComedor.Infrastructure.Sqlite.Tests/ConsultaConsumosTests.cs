using System.Globalization;
using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// La consulta de consumos (consulta-consumos de configurar-puesto-por-pestanas, D9): los del día por defecto, los filtros,
/// el rango validado, el tope de 500 y el orden en SQL. Los consumos que el escenario no necesita confirmar se insertan por
/// SQL con las claves foráneas apagadas en esa conexión (la consulta no lee las intenciones), con el precio aplicado que cada
/// prueba elige; el rechazo y un consumo con su comprobante salen de la confirmación real.
/// </summary>
public class ConsultaConsumosTests : ConfirmacionTestBase
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    private readonly ConsultaConsumos _consulta;
    private int _secuencia;

    public ConsultaConsumosTests()
    {
        _consulta = new ConsultaConsumos(Base, Reloj);
    }

    [Fact(DisplayName = "consulta-consumos · Los consumos de hoy: los tres de hoy, del más reciente al más antiguo, con hora, persona, opción, servicio, cargo y estado; el de ayer no")]
    public void Los_consumos_de_hoy()
    {
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy, new TimeOnly(8, 0), 150000);
        var ultimo = Insertar(Fixture.SinDerecho, Fixture.Refrigerio, Hoy, new TimeOnly(12, 30), 50000);
        Insertar(Fixture.Habilitada, Fixture.Refrigerio, Hoy, new TimeOnly(10, 0), 50000);
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy.AddDays(-1), new TimeOnly(12, 0), 150000);

        Assert.Equal(Hoy, _consulta.Hoy());
        var resultado = _consulta.Leer(FiltroConsumos.DelDia(Hoy));

        Assert.Null(resultado.Rechazo);
        Assert.False(resultado.HayMas);
        Assert.Equal(["12:30", "10:00", "08:00"], resultado.Filas.Select(f => Hora(f.ConfirmadoUtc)));
        var primera = resultado.Filas[0];
        Assert.Equal(ultimo, primera.ConsumoId);
        Assert.Equal((Fixture.SinDerecho.Id, Fixture.Refrigerio.Id, Fixture.ServicioRefrigerio), (primera.PersonaId, primera.OpcionId, primera.ServicioId));
        Assert.Equal(("REFRIGERIO", "ARS", 2, 50000L), (primera.Servicio, primera.Moneda, primera.Escala, primera.CargoUnidadesMenores));
        Assert.False(string.IsNullOrEmpty(primera.Persona));
        Assert.False(string.IsNullOrEmpty(primera.Opcion));
    }

    [Fact(DisplayName = "consulta-consumos · Un consumo de otra fecha de negocio: con uno de ayer y ninguno de hoy, la consulta del día está vacía")]
    public void Un_consumo_de_otra_fecha()
    {
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy.AddDays(-1), new TimeOnly(12, 0), 150000);

        Assert.Empty(_consulta.Leer(FiltroConsumos.DelDia(Hoy)).Filas);
    }

    [Fact(DisplayName = "consulta-consumos · Un rechazo no es un consumo: con una confirmación autorizada y otra denegada, sólo aparece la autorizada, con el estado de su comprobante")]
    public void Un_rechazo_no_es_un_consumo()
    {
        var autorizada = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));
        var denegada = Unidad.Confirmar(Solicitud(Fixture.SinDerecho, Fixture.AlmuerzoComun));
        Assert.Equal(TipoResultado.Autorizada, autorizada.Tipo);
        Assert.Equal(TipoResultado.Denegada, denegada.Tipo);

        var fila = Assert.Single(_consulta.Leer(FiltroConsumos.DelDia(Hoy)).Filas);

        Assert.Equal(autorizada.ConsumptionId, fila.ConsumoId);
        Assert.Equal(EstadoTrabajoImpresion.Queued, fila.EstadoComprobante);
        Assert.Equal(150000L, fila.CargoUnidadesMenores);
    }

    [Fact(DisplayName = "consulta-consumos · Una persona renombrada después de consumir: el consumo figura con el nombre de su versión")]
    public void Una_persona_renombrada()
    {
        var nombre = Texto("SELECT comperNombreVisible FROM tbComedorPersonas WHERE comperCodigo = $per", ("$per", Fixture.Habilitada.Id));
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy, new TimeOnly(9, 0), 150000);
        var administracion = new AdministracionLocal(Base, Reloj);
        var persona = administracion.Leer().Personas.Single(p => p.Id == Fixture.Habilitada.Id);
        Assert.Equal(TipoGuardado.Aplicado, administracion.GuardarPersonas(
            [new EdicionPersona(persona.Id, "Persona renombrada", persona.Cupos.Select(c => new CupoEditado(c.ServicioId, c.CategoriaId, c.Limite)).ToList())]).Tipo);

        var fila = Assert.Single(_consulta.Leer(FiltroConsumos.DelDia(Hoy)).Filas);

        Assert.Equal(nombre, fila.Persona);
        Assert.NotEqual("Persona renombrada", fila.Persona);
    }

    [Fact(DisplayName = "consulta-consumos · Una persona en una semana: con consumos de dos personas en ocho días, las siete fechas y la persona dan sólo los suyos en esas fechas")]
    public void Una_persona_en_una_semana()
    {
        for (var dia = 0; dia < 8; dia++)
        {
            Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy.AddDays(-dia), new TimeOnly(12, 0), 150000);
            Insertar(Fixture.SinDerecho, Fixture.AlmuerzoComun, Hoy.AddDays(-dia), new TimeOnly(12, 5), 150000);
        }

        var filas = _consulta.Leer(new FiltroConsumos(Hoy.AddDays(-6), Hoy, PersonaId: Fixture.Habilitada.Id)).Filas;

        Assert.Equal(7, filas.Count);
        Assert.All(filas, f => Assert.Equal(Fixture.Habilitada.Id, f.PersonaId));
        Assert.Equal(Hoy.AddDays(-6), filas.Min(f => f.FechaNegocio));
    }

    [Fact(DisplayName = "consulta-consumos · Un tipo de consumo y una opción: el servicio REFRIGERIO da sólo los refrigerios, y una opción, sólo los suyos")]
    public void Un_tipo_de_consumo_y_una_opcion()
    {
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy, new TimeOnly(12, 0), 150000);
        Insertar(Fixture.Habilitada, Fixture.Refrigerio, Hoy, new TimeOnly(16, 0), 50000);
        Insertar(Fixture.SinDerecho, Fixture.Refrigerio, Hoy, new TimeOnly(16, 5), 50000);
        Insertar(Fixture.SinDerecho, Fixture.AlmuerzoOpcional, Hoy, new TimeOnly(12, 10), 150000);

        var refrigerios = _consulta.Leer(new FiltroConsumos(Hoy, Hoy, ServicioId: Fixture.ServicioRefrigerio)).Filas;
        var opcional = _consulta.Leer(new FiltroConsumos(Hoy, Hoy, OpcionId: Fixture.AlmuerzoOpcional.Id)).Filas;

        Assert.Equal(2, refrigerios.Count);
        Assert.All(refrigerios, f => Assert.Equal("REFRIGERIO", f.Servicio));
        Assert.Equal(Fixture.AlmuerzoOpcional.Id, Assert.Single(opcional).OpcionId);
    }

    [Fact(DisplayName = "consulta-consumos · Una persona dada de baja: figura en el filtro como de baja, con el nombre de su versión más reciente, y la consulta muestra su consumo")]
    public void Una_persona_dada_de_baja()
    {
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy, new TimeOnly(12, 0), 150000);
        var administracion = new AdministracionLocal(Base, Reloj);
        var persona = administracion.Leer().Personas.Single(p => p.Id == Fixture.Habilitada.Id);
        Assert.Equal(TipoGuardado.Aplicado, administracion.GuardarPersonas([new EdicionPersona(persona.Id, persona.Nombre, [], Quitar: true)]).Tipo);

        var enElFiltro = Assert.Single(_consulta.Personas(), p => p.Id == Fixture.Habilitada.Id);
        var fila = Assert.Single(_consulta.Leer(new FiltroConsumos(Hoy, Hoy, PersonaId: Fixture.Habilitada.Id)).Filas);

        Assert.True(enElFiltro.DeBaja);
        Assert.Equal(persona.Nombre, enElFiltro.Nombre);
        Assert.Equal(Fixture.Habilitada.Id, fila.PersonaId);
        Assert.Contains(_consulta.Personas(), p => p.Id == Fixture.SinDerecho.Id && !p.DeBaja);
    }

    [Theory(DisplayName = "consulta-consumos · Un rango invertido o de más de 366 días se rechaza sin consultar; 366 días justos se aceptan")]
    [InlineData("2026-11-06", "2026-11-01", "INVERTED_RANGE")]
    [InlineData("2025-10-18", "2026-10-19", "RANGE_TOO_LONG")]
    [InlineData("2025-10-19", "2026-10-19", null)]
    public void Un_rango_invalido(string desde, string hasta, string? rechazo)
    {
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy, new TimeOnly(12, 0), 150000);

        var resultado = _consulta.Leer(new FiltroConsumos(DateOnly.Parse(desde, CultureInfo.InvariantCulture), DateOnly.Parse(hasta, CultureInfo.InvariantCulture)));

        Assert.Equal(rechazo, resultado.Rechazo);
        Assert.Equal(rechazo is null ? 1 : 0, resultado.Filas.Count);
    }

    [Fact(DisplayName = "consulta-consumos · Más de 500 consumos: con 520 en el rango, la consulta da 500 y dice que hay más")]
    public void Mas_de_500_consumos()
    {
        Cargar520();

        var resultado = _consulta.Leer(new FiltroConsumos(Hoy.AddDays(-30), Hoy));

        Assert.Equal(ConsultaConsumos.Tope, resultado.Filas.Count);
        Assert.Equal(500, ConsultaConsumos.Tope);
        Assert.True(resultado.HayMas);
    }

    [Fact(DisplayName = "consulta-consumos · Ordenar por persona: de la A a la Z, y con otro sentido de la Z a la A")]
    public void Ordenar_por_persona()
    {
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy, new TimeOnly(9, 0), 150000);
        Insertar(Fixture.SinDerecho, Fixture.AlmuerzoComun, Hoy, new TimeOnly(10, 0), 150000);
        Insertar(Fixture.Inactiva, Fixture.AlmuerzoComun, Hoy, new TimeOnly(11, 0), 150000);

        var ascendente = _consulta.Leer(FiltroConsumos.DelDia(Hoy) with { Orden = OrdenConsumos.Persona, Ascendente = true }).Filas.Select(f => f.Persona).ToList();
        var descendente = _consulta.Leer(FiltroConsumos.DelDia(Hoy) with { Orden = OrdenConsumos.Persona, Ascendente = false }).Filas.Select(f => f.Persona).ToList();

        Assert.Equal(ascendente.OrderBy(n => n, StringComparer.Create(CultureInfo.GetCultureInfo("es-AR"), true)), ascendente);
        Assert.Equal(ascendente.AsEnumerable().Reverse(), descendente);
    }

    [Fact(DisplayName = "consulta-consumos · Otro toque invierte el orden: por cargo ascendente y descendente")]
    public void Ordenar_por_cargo()
    {
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, Hoy, new TimeOnly(9, 0), 150000);
        Insertar(Fixture.Habilitada, Fixture.Refrigerio, Hoy, new TimeOnly(10, 0), 0);
        Insertar(Fixture.SinDerecho, Fixture.Refrigerio, Hoy, new TimeOnly(11, 0), 75000);

        var ascendente = _consulta.Leer(FiltroConsumos.DelDia(Hoy) with { Orden = OrdenConsumos.Cargo, Ascendente = true }).Filas.Select(f => f.CargoUnidadesMenores);
        var descendente = _consulta.Leer(FiltroConsumos.DelDia(Hoy) with { Orden = OrdenConsumos.Cargo, Ascendente = false }).Filas.Select(f => f.CargoUnidadesMenores);

        Assert.Equal([0L, 75000L, 150000L], ascendente);
        Assert.Equal([150000L, 75000L, 0L], descendente);
    }

    [Fact(DisplayName = "consulta-consumos · El orden decide qué entra en el tope: con 520 y el de mayor cargo entre los 20 más antiguos, por cargo descendente la primera fila es la de mayor cargo")]
    public void El_orden_decide_que_entra_en_el_tope()
    {
        var mayor = Cargar520();

        var porHora = _consulta.Leer(new FiltroConsumos(Hoy.AddDays(-30), Hoy));
        var porCargo = _consulta.Leer(new FiltroConsumos(Hoy.AddDays(-30), Hoy) with { Orden = OrdenConsumos.Cargo, Ascendente = false });

        Assert.DoesNotContain(porHora.Filas, f => f.ConsumoId == mayor);
        Assert.Equal(mayor, porCargo.Filas[0].ConsumoId);
    }

    /// <summary>520 consumos en los últimos días, uno por minuto; el de mayor cargo es el 5.º más antiguo.</summary>
    private Guid Cargar520()
    {
        var mayor = Guid.Empty;
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "PRAGMA foreign_keys = OFF");
        for (var i = 0; i < 520; i++)
        {
            var instante = new DateTime(2026, 10, 10, 11, 0, 0).AddMinutes(i * 25);
            var fecha = DateOnly.FromDateTime(instante);
            var id = Insertar(conexion, Fixture.Habilitada, Fixture.AlmuerzoComun, fecha, TimeOnly.FromDateTime(instante), i == 4 ? 999999 : 150000);
            if (i == 4)
            {
                mayor = id;
            }
        }

        return mayor;
    }

    private Guid Insertar(PersonaFixture persona, OpcionFixture opcion, DateOnly fecha, TimeOnly horaLocal, long cargo)
    {
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "PRAGMA foreign_keys = OFF");
        return Insertar(conexion, persona, opcion, fecha, horaLocal, cargo);
    }

    private Guid Insertar(Microsoft.Data.Sqlite.SqliteConnection conexion, PersonaFixture persona, OpcionFixture opcion, DateOnly fecha, TimeOnly horaLocal, long cargo)
    {
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
            ("$fecha", fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), ("$version", opcion.Version), ("$secuencia", ++_secuencia),
            ("$instante", Iso(instante)), ("$precio", JsonSerializer.Serialize(new { Currency = "ARS", Scale = 2, EmployeeChargeMinor = cargo })));
        return id;
    }

    private static string Hora(DateTimeOffset instante) =>
        TimeZoneInfo.ConvertTime(instante, TimeZoneInfo.FindSystemTimeZoneById(FixtureSintetico.ZonaHoraria)).ToString("HH:mm", CultureInfo.InvariantCulture);

    private string Texto(string sql, params (string, object?)[] parametros)
    {
        using var conexion = Abrir();
        return Sql.Escalar<string>(conexion, sql, parametros);
    }
}
