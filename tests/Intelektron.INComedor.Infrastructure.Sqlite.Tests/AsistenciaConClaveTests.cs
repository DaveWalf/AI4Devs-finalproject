using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Reimprimir y recuperar con la clave del día, sin sesión ni permiso (D1 de reimprimir-sin-codigo): el actor queda nulo
/// con NO_AUTHENTICATION y Access DAILY_KEY, y ninguna acción toca consumo, cupo ni importe (RB08).
/// </summary>
public class AsistenciaConClaveTests : ConfirmacionTestBase
{
    private readonly ImpresoraDePrueba _impresora = new();
    private readonly Asistencia _asistencia;

    public AsistenciaConClaveTests()
    {
        _asistencia = new Asistencia(Base, Reloj, new TrabajosImpresion(Base, Reloj, _impresora));
    }

    [Fact(DisplayName = "comprobante-local · Reimpresión con la clave del día: copia 1 con REIMPRESIÓN; consumo, cupo e importe iguales; TICKET_REPRINTED con el motivo, el comprobante, la copia, el actor nulo con NO_AUTHENTICATION y Access DAILY_KEY, sin PersonId ni GrantId")]
    public void Reimprimir_con_clave_crea_la_copia_sin_actor()
    {
        var ticket = AutorizarYEnviar();
        var antes = FotoDelConsumo();

        var resultado = _asistencia.ReimprimirConClave(ticket, MotivoReimpresion.PapelIlegible);

        Assert.True(resultado.Realizada);
        Assert.Equal(1, resultado.Envio!.NumeroCopia);
        Assert.Equal(EstadoTrabajoImpresion.Submitted, resultado.Envio.Estado);
        Assert.Equal("REIMPRESIÓN · copia 1", _impresora.Recibidos[^1].Leyenda);
        Assert.Equal(antes, FotoDelConsumo());
        using (var conexion = Abrir())
        {
            Assert.Equal(2L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorTrabajosImpresion WHERE comcmpCodigo = $tk", ("$tk", ticket)));
        }

        var contexto = UnicoRegistro("TICKET_REPRINTED");
        Assert.Equal("PAPER_ILLEGIBLE", contexto.GetProperty("Reason").GetString());
        Assert.Equal(ticket, contexto.GetProperty("TicketId").GetGuid());
        Assert.Equal(resultado.Envio.TrabajoId, contexto.GetProperty("PrintJobId").GetGuid());
        Assert.Equal(1, contexto.GetProperty("CopyNumber").GetInt32());
        AssertActorConClave(contexto);
    }

    [Fact(DisplayName = "comprobante-local · Recuperar con la clave del día: el mismo trabajo de copia 0 se envía otra vez, queda en SUBMITTED, sin trabajo nuevo, y TICKET_RECOVERED lleva el actor nulo con NO_AUTHENTICATION y Access DAILY_KEY")]
    public void Recuperar_con_clave_reenvia_el_mismo_original()
    {
        _impresora.Modo = ModoImpresoraDePrueba.SinPapel;
        var ticket = AutorizarYEnviar();
        var original = OriginalDe(ticket);
        _impresora.Modo = ModoImpresoraDePrueba.Disponible;
        var antes = FotoDelConsumo();

        var resultado = _asistencia.RecuperarConClave(original);

        Assert.True(resultado.Realizada);
        Assert.Equal(original, resultado.Envio!.TrabajoId);
        Assert.Equal(EstadoTrabajoImpresion.Submitted, resultado.Envio.Estado);
        Assert.Equal(2, _impresora.Recibidos.Count(r => r.TrabajoId == original));
        Assert.Null(_impresora.Recibidos[^1].Leyenda);
        Assert.Equal(antes, FotoDelConsumo());
        using (var conexion = Abrir())
        {
            Assert.Equal(1L, Sql.Contar(conexion, "tbComedorTrabajosImpresion"));
        }

        var contexto = UnicoRegistro("TICKET_RECOVERED");
        Assert.Equal(original, contexto.GetProperty("PrintJobId").GetGuid());
        Assert.Equal(ticket, contexto.GetProperty("TicketId").GetGuid());
        Assert.Equal(0, contexto.GetProperty("CopyNumber").GetInt32());
        AssertActorConClave(contexto);
    }

    [Fact(DisplayName = "D1 · Recuperar con la clave del día sólo acepta el original en FAILED: uno en SUBMITTED da PRINT_JOB_NOT_RECOVERABLE sin escribir nada")]
    public void Recuperar_con_clave_un_original_enviado_no_escribe()
    {
        var original = OriginalDe(AutorizarYEnviar());
        var antes = ConteoDeEscritura();

        var resultado = _asistencia.RecuperarConClave(original);

        Assert.False(resultado.Realizada);
        Assert.Equal("PRINT_JOB_NOT_RECOVERABLE", resultado.ReasonCode);
        Assert.Null(resultado.Envio);
        Assert.Single(_impresora.Recibidos);
        Assert.Equal(antes, ConteoDeEscritura());
    }

    [Fact(DisplayName = "D1 · Reimprimir con la clave del día un comprobante inexistente da TICKET_NOT_FOUND sin escribir nada")]
    public void Reimprimir_con_clave_un_comprobante_inexistente_no_escribe()
    {
        AutorizarYEnviar();
        var antes = ConteoDeEscritura();

        var resultado = _asistencia.ReimprimirConClave(Guid.NewGuid(), MotivoReimpresion.PapelIlegible);

        Assert.False(resultado.Realizada);
        Assert.Equal("TICKET_NOT_FOUND", resultado.ReasonCode);
        Assert.Null(resultado.Envio);
        Assert.Single(_impresora.Recibidos);
        Assert.Equal(antes, ConteoDeEscritura());
    }

    /// <summary>El actor de la clave del día: nulo, con NO_AUTHENTICATION y DAILY_KEY, sin persona, permiso ni rol.</summary>
    private static void AssertActorConClave(JsonElement contexto)
    {
        Assert.Equal(JsonValueKind.Null, contexto.GetProperty("ActorPersonId").ValueKind);
        Assert.Equal("NO_AUTHENTICATION", contexto.GetProperty("ActorReason").GetString());
        Assert.Equal("DAILY_KEY", contexto.GetProperty("Access").GetString());
        Assert.False(contexto.TryGetProperty("PersonId", out _));
        Assert.False(contexto.TryGetProperty("GrantId", out _));
        Assert.False(contexto.TryGetProperty("GrantedRole", out _));
    }

    /// <summary>Confirma un consumo de la persona habilitada y envía su original con la impresora en su modo actual.</summary>
    private Guid AutorizarYEnviar()
    {
        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));
        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        new TrabajosImpresion(Base, Reloj, _impresora).EnviarPendientes();
        return resultado.TicketId!.Value;
    }

    private Guid OriginalDe(Guid ticket)
    {
        using var conexion = Abrir();
        return Sql.EscalarGuid(conexion,
            "SELECT comimpCodigo FROM tbComedorTrabajosImpresion WHERE comcmpCodigo = $tk AND comimpNumeroCopia = 0", ("$tk", ticket));
    }

    private Dictionary<string, long> ConteoDeEscritura()
    {
        using var conexion = Abrir();
        return Sql.ConteoDeEscritura(conexion);
    }

    private JsonElement UnicoRegistro(string tipo)
    {
        using var conexion = Abrir();
        Assert.Equal(1L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = $tipo", ("$tipo", tipo)));
        var contexto = Sql.Escalar<string>(conexion, "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = $tipo", ("$tipo", tipo));
        return JsonDocument.Parse(contexto).RootElement.Clone();
    }

    /// <summary>Las filas del consumo, del uso de cupo y del comprobante (con su importe), como texto comparable.</summary>
    private List<string> FotoDelConsumo()
    {
        using var conexion = Abrir();
        var foto = new List<string>();
        foreach (var tabla in new[] { "tbComedorConsumos", "tbComedorUsosCupo", "tbComedorComprobantes" })
        {
            using var comando = conexion.CreateCommand();
            comando.CommandText = $"SELECT * FROM {tabla}";
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                foto.Add(tabla + ": " + string.Join(" | ", Enumerable.Range(0, lector.FieldCount).Select(i => lector.IsDBNull(i) ? "NULL" : lector.GetValue(i).ToString())));
            }
        }

        return foto;
    }
}
