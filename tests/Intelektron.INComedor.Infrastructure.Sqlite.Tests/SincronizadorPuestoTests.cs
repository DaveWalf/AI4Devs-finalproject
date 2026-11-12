using System.Text.Json;
using Intelektron.INComedor.Contracts.Lotes;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// El ciclo de envío del puesto (D11 de recibir-lotes-idempotente) con un IReceptorLotes de prueba, el doble que David
/// aprobó en el gate (D13) para cortar la respuesta en el punto exacto. La misma situación se prueba de punta a punta,
/// contra el receptor real, en EnvioDeLotesIntegradoTests.
/// </summary>
public class SincronizadorPuestoTests : ConfirmacionTestBase
{
    private readonly ColaSincronizacion _cola;
    private readonly ReceptorLotesDePrueba _receptor = new();

    public SincronizadorPuestoTests()
    {
        _cola = new ColaSincronizacion(Base, Reloj);
    }

    [Fact(DisplayName = "AT27 · HU02-D · El receptor guardó el lote y se perdió la respuesta: el ciclo siguiente consulta el mismo BatchId, registra el acuse original y no reenvía")]
    public async Task Un_acuse_perdido_se_recupera_con_la_consulta()
    {
        Confirmar();
        _receptor.AlEnviar = lote => { _receptor.Guardar(lote); return new SinRespuesta(); };
        var sincronizador = new SincronizadorPuesto(_cola, _receptor);
        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);
        var lote = Assert.Single(_receptor.Envios);
        Assert.Equal("PENDING", EstadoDeLosEventos().Single());
        _receptor.AlEnviar = _ => throw new InvalidOperationException("El lote no se tiene que reenviar.");

        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.Equal([lote.BatchId], _receptor.Consultas);
        Assert.Single(_receptor.Envios);
        Assert.Single(_receptor.Guardados);
        Assert.Equal(["ACKNOWLEDGED"], EstadoDeLosEventos());
        using var conexion = Abrir();
        Assert.Equal(_receptor.Guardados[lote.BatchId], Sql.Escalar<string>(conexion,
            "SELECT comlotContenidoAcuse FROM tbComedorLotesSincronizacion WHERE comlotCodigo = $lot", ("$lot", lote.BatchId)));
        Assert.Equal(0, sincronizador.Estado.Pendientes);
    }

    [Fact(DisplayName = "SubmitSyncBatch · El receptor nunca recibió el lote: con la consulta en 404, el puesto reenvía el mismo cuerpo, byte por byte, con el mismo BatchId")]
    public async Task Un_lote_que_no_llego_se_reenvia_identico()
    {
        Confirmar();
        _receptor.AlEnviar = _ => new SinRespuesta();
        var sincronizador = new SincronizadorPuesto(_cola, _receptor);
        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);
        _receptor.AlEnviar = lote => _receptor.Guardar(lote);

        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _receptor.Envios.Count);
        Assert.Equal(_receptor.Envios[0].BatchId, _receptor.Envios[1].BatchId);
        Assert.Equal(_receptor.Envios[0].Cuerpo, _receptor.Envios[1].Cuerpo);
        Assert.Equal([_receptor.Envios[0].BatchId], _receptor.Consultas);
        Assert.Equal(["ACKNOWLEDGED"], EstadoDeLosEventos());
    }

    [Fact(DisplayName = "Conflict · El receptor tiene otro cuerpo con esa clave: 409 BATCH_PAYLOAD_CONFLICT conserva el lote y sus eventos sin acuse, con el mismo BatchId, y el conflicto queda en la bitácora")]
    public async Task Un_409_conserva_el_lote()
    {
        Confirmar();
        _receptor.AlEnviar = _ => new Rechazado(409, "BATCH_PAYLOAD_CONFLICT");
        var sincronizador = new SincronizadorPuesto(_cola, _receptor);

        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);
        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _receptor.Envios.Count);
        Assert.Equal(_receptor.Envios[0].Cuerpo, _receptor.Envios[1].Cuerpo);
        Assert.Equal(["PENDING"], EstadoDeLosEventos());
        using var conexion = Abrir();
        Assert.Equal("PENDING", Sql.Escalar<string>(conexion, "SELECT comlotEstado FROM tbComedorLotesSincronizacion"));
        Assert.Equal(2L, Sql.Escalar<long>(conexion,
            "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = 'SYNC_BATCH_FAILED' AND instr(combitContexto, 'BATCH_PAYLOAD_CONFLICT') > 0"));
        Assert.Equal("409 BATCH_PAYLOAD_CONFLICT", sincronizador.Estado.UltimoError);
        Assert.False(sincronizador.Estado.ErrorDeCredencial);
    }

    [Theory(DisplayName = "AT27 · RB16 · Respuestas que no acusan (202, 409, 422, 503 o la red): ningún evento cambia, el lote sigue sin acuse y conserva su BatchId")]
    [InlineData(202)]
    [InlineData(409)]
    [InlineData(422)]
    [InlineData(503)]
    [InlineData(0)]
    public async Task Una_respuesta_que_no_acusa_no_avanza_la_cola(int estado)
    {
        Confirmar();
        Confirmar();
        _receptor.AlEnviar = _ => estado == 0 ? new SinRespuesta() : new Rechazado(estado, null);
        var sincronizador = new SincronizadorPuesto(_cola, _receptor);

        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["PENDING", "PENDING"], EstadoDeLosEventos());
        var lote = Assert.Single(_receptor.Envios);
        Assert.Equal(lote.BatchId, _cola.Preparar()!.BatchId);
        Assert.Equal(2, sincronizador.Estado.Pendientes);
        Assert.Equal(estado == 0 ? "NETWORK" : estado.ToString(System.Globalization.CultureInfo.InvariantCulture), sincronizador.Estado.UltimoError);
    }

    [Fact(DisplayName = "HU02-C · Nada pendiente: el ciclo no guarda ningún lote ni hace ningún pedido al receptor")]
    public async Task Sin_nada_pendiente_no_llama_al_receptor()
    {
        var sincronizador = new SincronizadorPuesto(_cola, _receptor);

        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_receptor.Envios);
        Assert.Empty(_receptor.Consultas);
        Assert.True(sincronizador.Estado.AlDia);
    }

    [Fact(DisplayName = "HU02-G · AT13 · Credencial de equipo vencida: con 401 los eventos y el lote se conservan, el estado informa el error de credencial y una intención nueva se autoriza como antes")]
    public async Task Un_401_es_un_error_de_credencial_visible()
    {
        Assert.Equal(TipoResultado.Autorizada, Confirmar().Tipo);
        _receptor.AlEnviar = _ => new Rechazado(401, "EQUIPMENT_AUTH_REQUIRED");
        var sincronizador = new SincronizadorPuesto(_cola, _receptor);

        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["PENDING"], EstadoDeLosEventos());
        Assert.True(sincronizador.Estado.ErrorDeCredencial);
        Assert.False(sincronizador.Estado.AlDia);
        Assert.Equal(TipoResultado.Autorizada, Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.Refrigerio)).Tipo);
    }

    [Fact(DisplayName = "D11 · Un 403 también es un error de credencial del equipo; la red caída no lo es")]
    public async Task Un_403_es_credencial_y_la_red_no()
    {
        Confirmar();
        _receptor.AlEnviar = _ => new Rechazado(403, "EQUIPMENT_FORBIDDEN");
        var sincronizador = new SincronizadorPuesto(_cola, _receptor);
        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);
        Assert.True(sincronizador.Estado.ErrorDeCredencial);

        _receptor.AlEnviar = _ => new SinRespuesta();
        _receptor.AlConsultar = _ => new SinRespuesta();
        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.False(sincronizador.Estado.ErrorDeCredencial);
        Assert.Equal("NETWORK", sincronizador.Estado.UltimoError);
    }

    [Fact(DisplayName = "D11 · Tras un reinicio, un lote sin acuse se consulta antes de reenviarlo, porque consultar es inocuo")]
    public async Task Tras_un_reinicio_se_consulta_primero()
    {
        Confirmar();
        var lote = _cola.Preparar()!;
        _receptor.Guardar(lote);
        _receptor.AlEnviar = _ => throw new InvalidOperationException("El lote no se tiene que reenviar.");
        var reiniciado = new SincronizadorPuesto(new ColaSincronizacion(BaseComedor.Abrir(Base.Ruta), Reloj), _receptor);

        await reiniciado.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.Equal([lote.BatchId], _receptor.Consultas);
        Assert.Empty(_receptor.Envios);
        Assert.Equal(["ACKNOWLEDGED"], EstadoDeLosEventos());
    }

    [Fact(DisplayName = "D11 · Un lote nuevo se envía sin consultar antes, y el estado cuenta los retenidos para revisión")]
    public async Task Un_lote_nuevo_no_se_consulta_y_el_estado_cuenta_los_retenidos()
    {
        Confirmar();
        Confirmar();
        _receptor.AlEnviar = lote => _receptor.Guardar(lote, "CONFLICT", "EVENT_PAYLOAD_CONFLICT");
        var sincronizador = new SincronizadorPuesto(_cola, _receptor);

        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.Empty(_receptor.Consultas);
        Assert.Equal(0, sincronizador.Estado.Pendientes);
        Assert.Equal(2, sincronizador.Estado.Retenidos);
        Assert.Null(sincronizador.Estado.UltimoError);
    }

    [Fact(DisplayName = "CompletedBatchAck · Un acuse inválido en un 201 no avanza la cola y queda como error del ciclo")]
    public async Task Un_acuse_invalido_queda_como_error()
    {
        Confirmar();
        _receptor.AlEnviar = lote => new Acusado($$"""{"BatchId":"{{lote.BatchId}}","Status":"COMPLETED","ServerAcknowledgedAtUtc":"2026-10-19T15:00:03.000Z","Results":[],"CorrelationId":"{{Guid.NewGuid()}}"}""");
        var sincronizador = new SincronizadorPuesto(_cola, _receptor);

        await sincronizador.EnviarUnaVezAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["PENDING"], EstadoDeLosEventos());
        Assert.Equal("ACUSE_INVALIDO", sincronizador.Estado.UltimoError);
    }

    private ResultadoConfirmacion Confirmar() => Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));

    private List<string> EstadoDeLosEventos()
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT comevsEstado FROM tbComedorEventosSalida ORDER BY rowid";
        using var lector = comando.ExecuteReader();
        var estados = new List<string>();
        while (lector.Read())
        {
            estados.Add(lector.GetString(0));
        }

        return estados;
    }

    /// <summary>
    /// El doble de IReceptorLotes aprobado en el gate (D13): registra cada envío y cada consulta y responde lo que la
    /// prueba le indica. Guardar simula un receptor que hizo commit del lote y arma su acuse.
    /// </summary>
    private sealed class ReceptorLotesDePrueba : IReceptorLotes
    {
        public List<LoteSincronizacion> Envios { get; } = [];

        public List<Guid> Consultas { get; } = [];

        public Dictionary<Guid, string> Guardados { get; } = [];

        public Func<LoteSincronizacion, RespuestaReceptor> AlEnviar { get; set; } = _ => new SinRespuesta();

        public Func<Guid, RespuestaReceptor>? AlConsultar { get; set; }

        public Task<RespuestaReceptor> EnviarAsync(LoteSincronizacion lote, CancellationToken cancelacion)
        {
            Envios.Add(lote);
            return Task.FromResult(AlEnviar(lote));
        }

        public Task<RespuestaReceptor> ConsultarAsync(Guid batchId, CancellationToken cancelacion)
        {
            Consultas.Add(batchId);
            if (AlConsultar is not null)
            {
                return Task.FromResult(AlConsultar(batchId));
            }

            return Task.FromResult<RespuestaReceptor>(Guardados.TryGetValue(batchId, out var acuse) ? new Acusado(acuse) : new NoEncontrado());
        }

        public RespuestaReceptor Guardar(LoteSincronizacion lote, string estado = "ACCEPTED", string motivo = "RECORDED")
        {
            using var cuerpo = JsonDocument.Parse(lote.Cuerpo);
            var resultados = cuerpo.RootElement.GetProperty("Events").EnumerateArray()
                .Select(e => new EventAck(e.GetProperty("EventId").GetGuid(), estado, motivo)).ToList();
            var acuse = JsonSerializer.Serialize(new CompletedBatchAck(lote.BatchId, "COMPLETED", "2026-10-19T15:00:03.000Z", resultados, Guid.NewGuid()));
            Guardados[lote.BatchId] = acuse;
            return new Acusado(acuse);
        }
    }
}
