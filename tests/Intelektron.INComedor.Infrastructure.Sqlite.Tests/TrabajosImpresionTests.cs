using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>Envío del comprobante después del commit, con UNKNOWN antes de enviar (D7 de atender-seleccion-y-confirmacion).</summary>
public class TrabajosImpresionTests : ConfirmacionTestBase
{
    private readonly ImpresoraDePrueba _impresora = new();

    private TrabajosImpresion NuevosTrabajos() => new(Base, Reloj, _impresora);

    [Fact(DisplayName = "AT01 · RB17 · Impresora disponible: la copia 0 queda en SUBMITTED, la impresora la recibe sin DUPLICADO ni código, y la bitácora registra el cambio")]
    public void Impresora_disponible_deja_el_original_enviado()
    {
        var ticket = Autorizar();

        var envios = NuevosTrabajos().EnviarPendientes();

        var envio = Assert.Single(envios);
        Assert.Equal(ticket, envio.TicketId);
        Assert.Equal(EstadoTrabajoImpresion.Submitted, envio.Estado);
        Assert.Equal(("SUBMITTED", (string?)null), EstadoDelOriginal(ticket));
        var recibido = Assert.Single(_impresora.Recibidos);
        Assert.Equal(ticket, recibido.TicketId);
        Assert.Equal(0, recibido.NumeroCopia);
        Assert.Null(recibido.Leyenda);
        Assert.DoesNotContain("DUPLICADO", recibido.DatosImpresion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(FixtureSintetico.CodigoSintetico, recibido.DatosImpresion);
        var propiedades = JsonDocument.Parse(recibido.DatosImpresion).RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.DoesNotContain(propiedades, p => p.Contains("Person", StringComparison.OrdinalIgnoreCase) || p.Contains("Credential", StringComparison.OrdinalIgnoreCase) || p.Contains("Code", StringComparison.OrdinalIgnoreCase));
        var registros = RegistrosDeImpresion();
        Assert.Equal(["QUEUED>UNKNOWN", "UNKNOWN>SUBMITTED"], registros);
    }

    [Fact(DisplayName = "AT06 · Impresora sin papel: FAILED con su código de error; consumo, cupo y comprobante iguales y un solo trabajo")]
    public void Impresora_sin_papel_no_toca_el_consumo()
    {
        var ticket = Autorizar();
        _impresora.Modo = ModoImpresoraDePrueba.SinPapel;
        var antes = FotoDelConsumo();

        var envio = Assert.Single(NuevosTrabajos().EnviarPendientes());

        Assert.Equal(EstadoTrabajoImpresion.Failed, envio.Estado);
        Assert.Equal(("FAILED", "PAPER_OUT"), EstadoDelOriginal(ticket));
        Assert.Equal(antes, FotoDelConsumo());
        using var conexion = Abrir();
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorTrabajosImpresion"));
        Assert.Equal(["QUEUED>UNKNOWN", "UNKNOWN>FAILED"], RegistrosDeImpresion());
    }

    [Fact(DisplayName = "HU03-B · RB08 · Respuesta incierta de la impresora: el trabajo queda en UNKNOWN y se registra")]
    public void Respuesta_incierta_deja_el_trabajo_en_unknown()
    {
        var ticket = Autorizar();
        _impresora.Modo = ModoImpresoraDePrueba.Incierta;

        var envio = Assert.Single(NuevosTrabajos().EnviarPendientes());

        Assert.Equal(EstadoTrabajoImpresion.Unknown, envio.Estado);
        Assert.Equal(("UNKNOWN", (string?)null), EstadoDelOriginal(ticket));
        Assert.Equal(["QUEUED>UNKNOWN", "UNKNOWN>UNKNOWN"], RegistrosDeImpresion());
    }

    [Fact(DisplayName = "RB17 · Caída durante el envío: al volver a arrancar el trabajo sigue en UNKNOWN y la impresora no lo recibe otra vez")]
    public void Caida_durante_el_envio_no_reenvia_al_arrancar()
    {
        var ticket = Autorizar();
        _impresora.Modo = ModoImpresoraDePrueba.Caida;
        Assert.Throws<CaidaSimuladaException>(() => NuevosTrabajos().EnviarPendientes());
        Assert.Equal(("UNKNOWN", (string?)null), EstadoDelOriginal(ticket));
        _impresora.Modo = ModoImpresoraDePrueba.Disponible;

        var envios = NuevosTrabajos().EnviarPendientes();

        Assert.Empty(envios);
        Assert.Single(_impresora.Recibidos);
        Assert.Equal(("UNKNOWN", (string?)null), EstadoDelOriginal(ticket));
    }

    [Fact(DisplayName = "AT05 · RB17 · Arranque con un trabajo en cola: envía ese mismo trabajo una sola vez, queda en SUBMITTED y no crea otro")]
    public void Arranque_con_un_trabajo_en_cola_lo_envia_una_vez()
    {
        var ticket = Autorizar();
        Guid trabajo;
        using (var conexion = Abrir())
        {
            trabajo = Sql.EscalarGuid(conexion, "SELECT comimpCodigo FROM tbComedorTrabajosImpresion");
        }

        var primero = NuevosTrabajos().EnviarPendientes();
        var segundo = NuevosTrabajos().EnviarPendientes();

        Assert.Equal(trabajo, Assert.Single(primero).TrabajoId);
        Assert.Empty(segundo);
        Assert.Equal(trabajo, Assert.Single(_impresora.Recibidos).TrabajoId);
        Assert.Equal(("SUBMITTED", (string?)null), EstadoDelOriginal(ticket));
        using var verificacion = Abrir();
        Assert.Equal(1L, Sql.Contar(verificacion, "tbComedorTrabajosImpresion"));
    }

    private Guid Autorizar()
    {
        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));
        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        return resultado.TicketId!.Value;
    }

    private (string Estado, string? Error) EstadoDelOriginal(Guid ticket)
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT comimpEstado, comimpCodigoUltimoError FROM tbComedorTrabajosImpresion WHERE comcmpCodigo = $tk AND comimpNumeroCopia = 0";
        comando.Parameters.AddWithValue("$tk", ticket);
        using var lector = comando.ExecuteReader();
        Assert.True(lector.Read());
        return (lector.GetString(0), lector.IsDBNull(1) ? null : lector.GetString(1));
    }

    /// <summary>Cada registro PRINT_JOB_UPDATED como «anterior>nuevo», en orden de secuencia.</summary>
    private List<string> RegistrosDeImpresion()
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = 'PRINT_JOB_UPDATED' ORDER BY combitSecuencia";
        using var lector = comando.ExecuteReader();
        var registros = new List<string>();
        while (lector.Read())
        {
            var contexto = JsonDocument.Parse(lector.GetString(0)).RootElement;
            registros.Add(contexto.GetProperty("PreviousState").GetString() + ">" + contexto.GetProperty("State").GetString());
        }

        return registros;
    }

    /// <summary>Las filas del consumo, del uso de cupo y del comprobante, como texto comparable.</summary>
    private List<string> FotoDelConsumo()
    {
        using var conexion = Abrir();
        var foto = new List<string>();
        foreach (var tabla in new[] { "tbComedorConsumos", "tbComedorUsosCupo", "tbComedorComprobantes" })
        {
            foto.AddRange(Filas(conexion, tabla));
        }

        return foto;
    }

    private static IEnumerable<string> Filas(SqliteConnection conexion, string tabla)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT * FROM {tabla}";
        using var lector = comando.ExecuteReader();
        var filas = new List<string>();
        while (lector.Read())
        {
            filas.Add(tabla + ": " + string.Join(" | ", Enumerable.Range(0, lector.FieldCount).Select(i => lector.IsDBNull(i) ? "NULL" : lector.GetValue(i).ToString())));
        }

        return filas;
    }
}
