using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>Excepción que representa el corte del proceso en un punto de la confirmación.</summary>
public sealed class CaidaSimulada : Exception
{
}

public class CaidasYConcurrenciaTests : ConfirmacionTestBase
{
    private static readonly TimeSpan EsperaDeLaBarrera = TimeSpan.FromSeconds(10);

    [Fact(DisplayName = "AT05 · Caída antes del commit no deja rastro: ninguna fila de la intención y una confirmación posterior produce un solo consumo")]
    public void Caida_antes_del_commit_no_deja_rastro()
    {
        var puntos = new PuntosDeFallaDePrueba { EnAntesDelCommit = () => throw new CaidaSimulada() };
        var sesion = NuevaSesion(Fixture.Habilitada);
        var solicitud = Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun, sesion);

        Assert.Throws<CaidaSimulada>(() => new UnidadConfirmacion(Base, Reloj, puntos).Confirmar(solicitud));

        SqliteConnection.ClearAllPools();
        using var conexion = Abrir();
        foreach (var tabla in Sql.TablasDeEscritura)
        {
            Assert.Equal(0L, Sql.Contar(conexion, tabla));
        }

        Assert.Equal(1L, Sql.Escalar<long>(conexion,
            "SELECT COUNT(*) FROM tbComedorSesionesIdentidad WHERE comsidCodigo = $sid AND comicCodigoConsumidora IS NULL", ("$sid", sesion)));

        var despues = new UnidadConfirmacion(Base, Reloj).Confirmar(solicitud);

        Assert.Equal(TipoResultado.Autorizada, despues.Tipo);
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorConsumos"));
    }

    [Fact(DisplayName = "AT05 · Caída entre el commit y el resultado: la conexión nueva recupera los mismos DecisionId, ConsumptionId y TicketId")]
    public void Caida_entre_el_commit_y_el_resultado_se_recupera_con_los_mismos_ids()
    {
        var puntos = new PuntosDeFallaDePrueba { EnDespuesDelCommit = () => throw new CaidaSimulada() };
        var solicitud = Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun);

        Assert.Throws<CaidaSimulada>(() => new UnidadConfirmacion(Base, Reloj, puntos).Confirmar(solicitud));

        SqliteConnection.ClearAllPools();
        var recuperado = new UnidadConfirmacion(Base, Reloj).Confirmar(solicitud);

        using var conexion = Abrir();
        Assert.Equal(TipoResultado.Autorizada, recuperado.Tipo);
        Assert.Equal(recuperado.DecisionId, Sql.EscalarGuid(conexion, "SELECT comicCodigoDecision FROM tbComedorIntencionesConsumo"));
        Assert.Equal(recuperado.ConsumptionId, Sql.EscalarGuid(conexion, "SELECT comconCodigo FROM tbComedorConsumos"));
        Assert.Equal(recuperado.TicketId, Sql.EscalarGuid(conexion, "SELECT comcmpCodigo FROM tbComedorComprobantes"));
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorConsumos"));
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorComprobantes"));
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorTrabajosImpresion"));
        Assert.Equal(0L, Sql.Escalar<long>(conexion, "SELECT comimpNumeroCopia FROM tbComedorTrabajosImpresion"));
        Assert.Equal("QUEUED", Sql.Escalar<string>(conexion, "SELECT comimpEstado FROM tbComedorTrabajosImpresion"));
    }

    [Fact(DisplayName = "AT05 · Recuperar no reconstruye el trabajo original: dos recuperaciones dejan un único trabajo con el mismo identificador")]
    public void Recuperar_dos_veces_no_crea_otro_trabajo_de_impresion()
    {
        var puntos = new PuntosDeFallaDePrueba { EnDespuesDelCommit = () => throw new CaidaSimulada() };
        var solicitud = Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun);
        Assert.Throws<CaidaSimulada>(() => new UnidadConfirmacion(Base, Reloj, puntos).Confirmar(solicitud));
        SqliteConnection.ClearAllPools();
        using var conexion = Abrir();
        var trabajoOriginal = Sql.EscalarGuid(conexion, "SELECT comimpCodigo FROM tbComedorTrabajosImpresion");

        new UnidadConfirmacion(Base, Reloj).Confirmar(solicitud);
        new UnidadConfirmacion(Base, Reloj).Confirmar(solicitud);

        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorTrabajosImpresion"));
        Assert.Equal(trabajoOriginal, Sql.EscalarGuid(conexion, "SELECT comimpCodigo FROM tbComedorTrabajosImpresion"));
    }

    [Fact(DisplayName = "AT03 · Dos conexiones compiten por el último cupo: una autoriza, la otra rechaza y queda un solo consumo")]
    public async Task Dos_conexiones_compiten_por_el_ultimo_cupo()
    {
        var llegadas = 0;
        var puntos = PuntosConBarrera(() => Interlocked.Increment(ref llegadas));
        var primera = Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun);
        var segunda = Solicitud(Fixture.Habilitada, Fixture.AlmuerzoOpcional);

        var resultados = await Task.WhenAll(
            Task.Run(() => new UnidadConfirmacion(Base, Reloj, puntos).Confirmar(primera)),
            Task.Run(() => new UnidadConfirmacion(Base, Reloj, puntos).Confirmar(segunda)));

        Assert.Equal(2, llegadas);
        Assert.Single(resultados, r => r.Tipo == TipoResultado.Autorizada);
        var rechazada = Assert.Single(resultados, r => r.Tipo == TipoResultado.Denegada);
        Assert.Equal("LOCAL_LIMIT_REACHED", rechazada.ReasonCode);
        using var conexion = Abrir();
        Assert.Equal(1L, Sql.Escalar<long>(conexion, "SELECT comucUsado FROM tbComedorUsosCupo"));
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorConsumos"));
    }

    [Fact(DisplayName = "AT03 · Mismo IntentId concurrente en dos conexiones: ambas devuelven la misma decisión durable y queda un solo consumo")]
    public async Task Mismo_intentid_concurrente_devuelve_la_misma_decision()
    {
        var llegadas = 0;
        var puntos = PuntosConBarrera(() => Interlocked.Increment(ref llegadas));
        var solicitud = Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun);

        var resultados = await Task.WhenAll(
            Task.Run(() => new UnidadConfirmacion(Base, Reloj, puntos).Confirmar(solicitud)),
            Task.Run(() => new UnidadConfirmacion(Base, Reloj, puntos).Confirmar(solicitud)));

        Assert.Equal(2, llegadas);
        Assert.Equal(TipoResultado.Autorizada, resultados[0].Tipo);
        Assert.Equal(resultados[0], resultados[1]);
        using var conexion = Abrir();
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorConsumos"));
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorIntencionesConsumo"));
    }

    /// <summary>Las dos conexiones esperan en el punto posterior a la lectura previa: ambas vieron disponibilidad antes de bloquear.</summary>
    private static PuntosDeFallaDePrueba PuntosConBarrera(Action alLlegar)
    {
        var barrera = new Barrier(2);
        return new PuntosDeFallaDePrueba
        {
            EnDespuesDeLecturaPrevia = () =>
            {
                alLlegar();
                if (!barrera.SignalAndWait(EsperaDeLaBarrera))
                {
                    throw new TimeoutException("La segunda conexión no llegó al punto de sincronización.");
                }
            },
        };
    }
}
