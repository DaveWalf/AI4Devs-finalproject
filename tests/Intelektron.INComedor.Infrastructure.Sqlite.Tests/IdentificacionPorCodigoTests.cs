using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Identificación simulada por código y rechazo de un código inválido (D3 y D4 de atender-seleccion-y-confirmacion),
/// sobre el fixture sintético. Los códigos de los rechazos llevan letras fuera del hexadecimal, para que un UUID no
/// pueda contenerlos por azar.
/// </summary>
public class IdentificacionPorCodigoTests : ConfirmacionTestBase
{
    private const string CodigoRevocado = "SINTETICO-REVOCADO-77";
    private const string CodigoVencido = "SINTETICO-VENCIDO-77";
    private const string CodigoFuturo = "SINTETICO-FUTURO-77";
    private const string CodigoDesconocido = "SINTETICO-NINGUNO-77";

    private readonly IdentificacionPorCodigo _identificacion;

    public IdentificacionPorCodigoTests()
    {
        _identificacion = new IdentificacionPorCodigo(Base, Reloj);
    }

    [Fact(DisplayName = "AT52 · AT01 · Código vigente abre la selección: sesión CONSUMPTION con la persona y la credencial, el nombre visible y ninguna intención")]
    public void Codigo_vigente_abre_una_sesion_de_consumo()
    {
        var resultado = _identificacion.Identificar(FixtureSintetico.CodigoSintetico, PropositoSesion.Consumption);

        Assert.True(resultado.Abierta);
        Assert.Null(resultado.ReasonCode);
        Assert.Equal(Fixture.Habilitada.Id, resultado.PersonId);
        Assert.Equal("Persona habilitada", resultado.NombreVisible);
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            "SELECT comperCodigo, comcreCodigo, comsidMetodo, comsidProposito, comsidFechaEmisionUtc, comsidFechaVencimientoUtc, comicCodigoConsumidora FROM tbComedorSesionesIdentidad WHERE comsidCodigo = $sid";
        comando.Parameters.AddWithValue("$sid", resultado.IdentitySessionId!.Value);
        using var lector = comando.ExecuteReader();
        Assert.True(lector.Read());
        Assert.Equal(Fixture.Habilitada.Id, lector.GetGuid(0));
        Assert.Equal(Fixture.Habilitada.CredencialId, lector.GetGuid(1));
        Assert.Equal("CODE", lector.GetString(2));
        Assert.Equal("CONSUMPTION", lector.GetString(3));
        Assert.Equal(Iso(Reloj.Ahora), lector.GetString(4));
        Assert.Equal(Iso(Reloj.Ahora.AddMinutes(2)), lector.GetString(5));
        Assert.True(lector.IsDBNull(6));
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorIntencionesConsumo"));
    }

    [Fact(DisplayName = "RB01 · RB25 · Ceros iniciales y espacios alrededor: sin los ceros no se resuelve; con espacios abre la sesión de la misma persona")]
    public void Conserva_ceros_iniciales_y_quita_espacios()
    {
        var sinCeros = _identificacion.Identificar("123", PropositoSesion.Consumption);
        var conEspacios = _identificacion.Identificar("  " + FixtureSintetico.CodigoSintetico + " \t", PropositoSesion.Consumption);

        Assert.False(sinCeros.Abierta);
        Assert.Equal("IDENTITY_NOT_RESOLVED", sinCeros.ReasonCode);
        Assert.True(conEspacios.Abierta);
        Assert.Equal(Fixture.Habilitada.Id, conEspacios.PersonId);
    }

    [Fact(DisplayName = "AT52 · Código revocado: CREDENTIAL_REVOKED, ninguna sesión nueva y un registro de identificación rechazada con su motivo")]
    public void Codigo_revocado_se_rechaza_y_se_audita()
    {
        var persona = InsertarCredencial(CodigoRevocado, null, null, Reloj.Ahora.AddDays(-1));
        var antes = ConteoDeAtencion();

        var resultado = _identificacion.Identificar(CodigoRevocado, PropositoSesion.Consumption);

        Assert.False(resultado.Abierta);
        Assert.Equal("CREDENTIAL_REVOKED", resultado.ReasonCode);
        Assert.Null(resultado.IdentitySessionId);
        Assert.Equal(antes, ConteoDeAtencion());
        var contexto = UnicoRechazo();
        Assert.Equal("CREDENTIAL_REVOKED", contexto.GetProperty("ReasonCode").GetString());
        Assert.Equal(persona, contexto.GetProperty("PersonId").GetGuid());
    }

    [Fact(DisplayName = "AT52 · Código vencido en el límite exacto: ValidToUtc igual al instante da CREDENTIAL_EXPIRED y no abre sesión")]
    public void Codigo_vencido_en_el_limite_exacto()
    {
        InsertarCredencial(CodigoVencido, null, Reloj.Ahora, null);

        var resultado = _identificacion.Identificar(CodigoVencido, PropositoSesion.Consumption);

        Assert.Equal("CREDENTIAL_EXPIRED", resultado.ReasonCode);
        Assert.False(resultado.Abierta);
        using var conexion = Abrir();
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorSesionesIdentidad"));
    }

    [Fact(DisplayName = "RB25 · Revocado y vencido a la vez: CREDENTIAL_REVOKED")]
    public void Revocado_y_vencido_es_revocado()
    {
        InsertarCredencial(CodigoRevocado, null, Reloj.Ahora.AddDays(-2), Reloj.Ahora.AddDays(-1));

        var resultado = _identificacion.Identificar(CodigoRevocado, PropositoSesion.Consumption);

        Assert.Equal("CREDENTIAL_REVOKED", resultado.ReasonCode);
    }

    [Fact(DisplayName = "AT52 · Código todavía no vigente: ValidFromUtc un milisegundo después da IDENTITY_NOT_RESOLVED y la causa interna NOT_YET_VALID")]
    public void Codigo_todavia_no_vigente()
    {
        InsertarCredencial(CodigoFuturo, Reloj.Ahora.AddMilliseconds(1), null, null);

        var resultado = _identificacion.Identificar(CodigoFuturo, PropositoSesion.Consumption);

        Assert.Equal("IDENTITY_NOT_RESOLVED", resultado.ReasonCode);
        Assert.False(resultado.Abierta);
        var contexto = UnicoRechazo();
        Assert.Equal("IDENTITY_NOT_RESOLVED", contexto.GetProperty("ReasonCode").GetString());
        Assert.Equal("NOT_YET_VALID", contexto.GetProperty("InternalCause").GetString());
    }

    [Fact(DisplayName = "AT52 · RB25 · Código desconocido: IDENTITY_NOT_RESOLVED con la causa interna UNKNOWN_CODE, sin PersonId")]
    public void Codigo_desconocido()
    {
        var resultado = _identificacion.Identificar(CodigoDesconocido, PropositoSesion.Consumption);

        Assert.Equal("IDENTITY_NOT_RESOLVED", resultado.ReasonCode);
        Assert.Null(resultado.PersonId);
        var contexto = UnicoRechazo();
        Assert.Equal("UNKNOWN_CODE", contexto.GetProperty("InternalCause").GetString());
        Assert.False(contexto.TryGetProperty("PersonId", out _));
    }

    [Fact(DisplayName = "AT52 · Ningún rechazo registra el código: ni la bitácora ni las sesiones contienen los cuatro códigos rechazados, y no hay intención, consumo ni comprobante")]
    public void Ningun_rechazo_registra_el_codigo()
    {
        InsertarCredencial(CodigoRevocado, null, null, Reloj.Ahora.AddDays(-1));
        InsertarCredencial(CodigoVencido, null, Reloj.Ahora, null);
        InsertarCredencial(CodigoFuturo, Reloj.Ahora.AddMilliseconds(1), null, null);
        var antes = ConteoDeAtencion();

        foreach (var codigo in new[] { CodigoRevocado, CodigoVencido, CodigoFuturo, CodigoDesconocido })
        {
            Assert.False(_identificacion.Identificar(codigo, PropositoSesion.Consumption).Abierta);
        }

        Assert.Equal(antes, ConteoDeAtencion());
        using var conexion = Abrir();
        Assert.Equal(4L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = 'IDENTIFICATION_REJECTED'"));
        var textos = TextosDe(conexion, "tbComedorBitacora").Concat(TextosDe(conexion, "tbComedorSesionesIdentidad")).ToList();
        Assert.NotEmpty(textos);
        foreach (var codigo in new[] { CodigoRevocado, CodigoVencido, CodigoFuturo, CodigoDesconocido })
        {
            Assert.DoesNotContain(textos, t => t.Contains(codigo, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Una persona activa nueva con una credencial SERIAL_CODE en el snapshot del fixture.</summary>
    private Guid InsertarCredencial(string valor, DateTimeOffset? desde, DateTimeOffset? hasta, DateTimeOffset? revocada)
    {
        var persona = Guid.NewGuid();
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Persona sintética rechazada', 1, 1)",
            ("$vc", Fixture.SnapshotId), ("$per", persona));
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, $desde, $hasta, $revocada)",
            ("$vc", Fixture.SnapshotId), ("$cre", Guid.NewGuid()), ("$per", persona), ("$valor", valor),
            ("$desde", desde is null ? null : Iso(desde.Value)), ("$hasta", hasta is null ? null : Iso(hasta.Value)),
            ("$revocada", revocada is null ? null : Iso(revocada.Value)));
        return persona;
    }

    /// <summary>Lo que una identificación rechazada no debe escribir: sesiones, intenciones, consumos y comprobantes.</summary>
    private Dictionary<string, long> ConteoDeAtencion()
    {
        using var conexion = Abrir();
        return new[] { "tbComedorSesionesIdentidad", "tbComedorIntencionesConsumo", "tbComedorConsumos", "tbComedorComprobantes" }
            .ToDictionary(t => t, t => Sql.Contar(conexion, t));
    }

    private JsonElement UnicoRechazo()
    {
        using var conexion = Abrir();
        Assert.Equal(1L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = 'IDENTIFICATION_REJECTED'"));
        var contexto = Sql.Escalar<string>(conexion, "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = 'IDENTIFICATION_REJECTED'");
        return JsonDocument.Parse(contexto).RootElement.Clone();
    }

    /// <summary>Todas las columnas de texto de todas las filas de la tabla.</summary>
    private static IEnumerable<string> TextosDe(SqliteConnection conexion, string tabla)
    {
        var textos = new List<string>();
        using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT * FROM {tabla}";
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            for (var i = 0; i < lector.FieldCount; i++)
            {
                if (!lector.IsDBNull(i) && lector.GetValue(i) is string texto)
                {
                    textos.Add(texto);
                }
            }
        }

        return textos;
    }
}
