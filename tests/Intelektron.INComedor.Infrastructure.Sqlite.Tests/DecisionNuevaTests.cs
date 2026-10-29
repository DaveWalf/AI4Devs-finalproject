using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

public class DecisionNuevaTests : ConfirmacionTestBase
{
    [Fact(DisplayName = "AT01 · Autorización con cupo libre y sin red: queda una fila por tabla con los identificadores del resultado")]
    public void Autorizacion_con_cupo_libre_escribe_una_fila_por_tabla()
    {
        var sesion = NuevaSesion(Fixture.Habilitada);
        var solicitud = Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun, sesion);

        var resultado = Unidad.Confirmar(solicitud);

        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        using var conexion = Abrir();
        foreach (var tabla in Sql.TablasDeEscritura)
        {
            Assert.Equal(1L, Sql.Contar(conexion, tabla));
        }

        var intent = ("$ic", (object?)solicitud.IntentId);
        Assert.Equal(resultado.DecisionId, Sql.EscalarGuid(conexion,
            "SELECT comicCodigoDecision FROM tbComedorIntencionesConsumo WHERE comicCodigo = $ic", intent));
        Assert.Equal("AUTHORIZED", Sql.Escalar<string>(conexion,
            "SELECT comicEstado FROM tbComedorIntencionesConsumo WHERE comicCodigo = $ic", intent));
        Assert.Equal(resultado.ConsumptionId, Sql.EscalarGuid(conexion,
            "SELECT comconCodigo FROM tbComedorConsumos WHERE comicCodigo = $ic", intent));
        Assert.Equal(resultado.TicketId, Sql.EscalarGuid(conexion,
            "SELECT comcmpCodigo FROM tbComedorComprobantes WHERE comconCodigo = $con", ("$con", resultado.ConsumptionId)));
        Assert.Equal("OWN", Sql.Escalar<string>(conexion,
            "SELECT comccOrigen FROM tbComedorConsumosConocidos WHERE comconCodigo = $con", ("$con", resultado.ConsumptionId)));
        Assert.Equal(0L, Sql.Escalar<long>(conexion, "SELECT comimpNumeroCopia FROM tbComedorTrabajosImpresion"));
        Assert.Equal(resultado.TicketId, Sql.EscalarGuid(conexion, "SELECT comcmpCodigo FROM tbComedorTrabajosImpresion"));
        Assert.Equal("CONSUMPTION_RECORDED", Sql.Escalar<string>(conexion, "SELECT comevsTipoEvento FROM tbComedorEventosSalida"));
        Assert.Equal(1L, Sql.Escalar<long>(conexion, "SELECT comucUsado FROM tbComedorUsosCupo"));
        Assert.Equal(solicitud.IntentId, Sql.EscalarGuid(conexion,
            "SELECT comicCodigoConsumidora FROM tbComedorSesionesIdentidad WHERE comsidCodigo = $sid", ("$sid", sesion)));
    }

    [Fact(DisplayName = "AT02 · Rechazo por cupo agotado: guarda intención, evento y bitácora; no crea consumo ni toca el cupo")]
    public void Rechazo_por_cupo_agotado_no_crea_consumo()
    {
        Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));
        using var conexion = Abrir();
        var antes = Sql.ConteoDeEscritura(conexion);
        var segunda = Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun);

        var resultado = Unidad.Confirmar(segunda);

        Assert.Equal(TipoResultado.Denegada, resultado.Tipo);
        Assert.Equal("LOCAL_LIMIT_REACHED", resultado.ReasonCode);
        Assert.Null(resultado.ConsumptionId);
        var despues = Sql.ConteoDeEscritura(conexion);
        Assert.Equal(antes["tbComedorIntencionesConsumo"] + 1, despues["tbComedorIntencionesConsumo"]);
        Assert.Equal(antes["tbComedorEventosSalida"] + 1, despues["tbComedorEventosSalida"]);
        Assert.Equal(antes["tbComedorBitacora"] + 1, despues["tbComedorBitacora"]);
        foreach (var tabla in new[] { "tbComedorConsumos", "tbComedorConsumosConocidos", "tbComedorCargosCupoConocidos",
                     "tbComedorUsosCupo", "tbComedorComprobantes", "tbComedorTrabajosImpresion" })
        {
            Assert.Equal(antes[tabla], despues[tabla]);
        }

        Assert.Equal(1L, Sql.Escalar<long>(conexion, "SELECT comucUsado FROM tbComedorUsosCupo"));
        Assert.Equal(1L, Sql.Escalar<long>(conexion, "SELECT comucVersion FROM tbComedorUsosCupo"));
        Assert.Equal("CONSUMPTION_DENIED", Sql.Escalar<string>(conexion,
            "SELECT comevsTipoEvento FROM tbComedorEventosSalida WHERE comicCodigo = $ic", ("$ic", segunda.IntentId)));
        Assert.Equal("DENIED", Sql.Escalar<string>(conexion,
            "SELECT comicEstado FROM tbComedorIntencionesConsumo WHERE comicCodigo = $ic", ("$ic", segunda.IntentId)));
    }

    [Fact(DisplayName = "Persona sin regla aplicable · la persona sin derecho se deniega con SERVICE_NOT_ENABLED y sin consumo")]
    public void Persona_sin_regla_se_deniega_sin_consumo()
    {
        var resultado = Unidad.Confirmar(Solicitud(Fixture.SinDerecho, Fixture.AlmuerzoComun));

        Assert.Equal(TipoResultado.Denegada, resultado.Tipo);
        Assert.Equal("SERVICE_NOT_ENABLED", resultado.ReasonCode);
        using var conexion = Abrir();
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorConsumos"));
    }

    [Fact(DisplayName = "AT02 · Variantes que comparten categoría cuentan juntas: la opcional se deniega tras la común")]
    public void Variantes_con_la_misma_categoria_cuentan_juntas()
    {
        Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoOpcional));

        Assert.Equal(TipoResultado.Denegada, resultado.Tipo);
        Assert.Equal("LOCAL_LIMIT_REACHED", resultado.ReasonCode);
        using var conexion = Abrir();
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorConsumos"));
    }

    [Fact(DisplayName = "AT02 · Otra categoría no consume el mismo cupo: REFRIGERIO se autoriza con ALMUERZO agotado")]
    public void Otra_categoria_no_consume_el_mismo_cupo()
    {
        Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.Refrigerio));

        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        using var conexion = Abrir();
        Assert.Equal(2L, Sql.Contar(conexion, "tbComedorUsosCupo"));
        Assert.Equal(2L, Sql.Escalar<long>(conexion, "SELECT SUM(comucUsado) FROM tbComedorUsosCupo"));
    }

    [Fact(DisplayName = "AT04 · Confirmaciones a ambos lados del corte: dos autorizaciones, días consecutivos y cupos separados")]
    public void Confirmaciones_a_ambos_lados_del_corte_usan_cupos_separados()
    {
        Reloj.Ahora = DateTimeOffset.Parse("2026-10-19T02:59:59Z");
        var antes = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));
        Reloj.Ahora = DateTimeOffset.Parse("2026-10-19T03:00:00Z");
        var despues = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoOpcional));

        Assert.Equal(TipoResultado.Autorizada, antes.Tipo);
        Assert.Equal(TipoResultado.Autorizada, despues.Tipo);
        using var conexion = Abrir();
        Assert.Equal("2026-10-18", Sql.Escalar<string>(conexion,
            "SELECT comconFechaNegocio FROM tbComedorConsumos WHERE comconCodigo = $con", ("$con", antes.ConsumptionId)));
        Assert.Equal("2026-10-19", Sql.Escalar<string>(conexion,
            "SELECT comconFechaNegocio FROM tbComedorConsumos WHERE comconCodigo = $con", ("$con", despues.ConsumptionId)));
        Assert.Equal(2L, Sql.Contar(conexion, "tbComedorUsosCupo"));
        Assert.Equal(2L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorUsosCupo WHERE comucUsado = 1"));
    }

    [Fact(DisplayName = "AT50 · Las cargas llevan la fecha del consumo: cada carga conocida y cada AppliedQuotaCharges")]
    public void Las_cargas_llevan_la_fecha_de_negocio_del_consumo()
    {
        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.ComboAlmuerzoYRefrigerio));

        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        using var conexion = Abrir();
        var fechaConsumo = Sql.Escalar<string>(conexion, "SELECT comconFechaNegocio FROM tbComedorConsumos");
        Assert.Equal(2L, Sql.Contar(conexion, "tbComedorCargosCupoConocidos"));
        Assert.Equal(2L, Sql.Escalar<long>(conexion,
            "SELECT COUNT(*) FROM tbComedorCargosCupoConocidos WHERE comcccFechaNegocio = $fecha", ("$fecha", fechaConsumo)));

        using var evento = JsonDocument.Parse(Sql.Escalar<string>(conexion, "SELECT comevsContenido FROM tbComedorEventosSalida"));
        var cargas = evento.RootElement.GetProperty("AppliedQuotaCharges").EnumerateArray().ToList();
        Assert.Equal(2, cargas.Count);
        Assert.All(cargas, c => Assert.Equal(fechaConsumo, c.GetProperty("BusinessDate").GetString()));
        Assert.Equal(fechaConsumo, evento.RootElement.GetProperty("BusinessDate").GetString());
    }

    [Fact(DisplayName = "AT47 · Versión de menú que dejó de ser la vigente: informa catálogo cambiado y no escribe ninguna fila")]
    public void Version_de_menu_distinta_no_escribe_nada()
    {
        var sesion = NuevaSesion(Fixture.Habilitada);
        var solicitud = Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun, sesion) with { MenuVersion = 2 };

        var resultado = Unidad.Confirmar(solicitud);

        Assert.Equal(TipoResultado.CatalogoCambiado, resultado.Tipo);
        using var conexion = Abrir();
        foreach (var tabla in Sql.TablasDeEscritura)
        {
            Assert.Equal(0L, Sql.Contar(conexion, tabla));
        }

        Assert.Equal(1L, Sql.Escalar<long>(conexion,
            "SELECT COUNT(*) FROM tbComedorSesionesIdentidad WHERE comsidCodigo = $sid AND comicCodigoConsumidora IS NULL",
            ("$sid", sesion)));
    }
}
