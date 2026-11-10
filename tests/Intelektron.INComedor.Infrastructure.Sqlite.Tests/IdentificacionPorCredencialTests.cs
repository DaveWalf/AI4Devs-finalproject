using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// La identificación por la credencial elegida en el combo del modo simulación y la lista de ese combo (atencion-puesto de
/// simular-identificacion-por-lista, D2, D3 y D7), sobre el fixture sintético. La semilla de la atención, con sus ocho
/// códigos, vive en el Kiosk: la lista de esas ocho personas se prueba en ComboSimulacionTests.
/// </summary>
public class IdentificacionPorCredencialTests : ConfirmacionTestBase
{
    private readonly IdentificacionPorCodigo _identificacion;

    public IdentificacionPorCredencialTests()
    {
        _identificacion = new IdentificacionPorCodigo(Base, Reloj);
    }

    [Fact(DisplayName = "atencion-puesto · Elegir a una persona abre la selección: su credencial abre una sesión CONSUMPTION con método CODE y SESSION_OPENED dice Origin SIMULATION_LIST")]
    public void La_credencial_vigente_abre_la_sesion()
    {
        var resultado = _identificacion.IdentificarPorCredencial(Fixture.Habilitada.CredencialId!.Value, PropositoSesion.Consumption);

        Assert.True(resultado.Abierta);
        Assert.Equal(Fixture.Habilitada.Id, resultado.PersonId);
        Assert.Equal("Persona habilitada", resultado.NombreVisible);
        using var conexion = Abrir();
        Assert.Equal("CODE", Sql.Escalar<string>(conexion, "SELECT comsidMetodo FROM tbComedorSesionesIdentidad"));
        Assert.Equal("CONSUMPTION", Sql.Escalar<string>(conexion, "SELECT comsidProposito FROM tbComedorSesionesIdentidad"));
        Assert.Equal(Fixture.Habilitada.CredencialId, Guid.Parse(Sql.Escalar<string>(conexion, "SELECT comcreCodigo FROM tbComedorSesionesIdentidad")));
        var abierta = Contexto("SESSION_OPENED");
        Assert.Equal("SIMULATION_LIST", abierta.GetProperty("Origin").GetString());
        Assert.Equal("CODE", abierta.GetProperty("Method").GetString());
        Assert.Equal(resultado.IdentitySessionId, abierta.GetProperty("IdentitySessionId").GetGuid());
    }

    [Fact(DisplayName = "atencion-puesto · Una credencial vencida elegida en el combo: CREDENTIAL_EXPIRED, ninguna sesión y el rechazo dice Origin SIMULATION_LIST")]
    public void La_credencial_vencida_se_rechaza()
    {
        var (persona, credencial) = InsertarCredencial("Persona sintética vencida", "SINTETICO-VENCIDO-77", hasta: Reloj.Ahora.AddDays(-1));

        var resultado = _identificacion.IdentificarPorCredencial(credencial, PropositoSesion.Consumption);

        Assert.False(resultado.Abierta);
        Assert.Equal("CREDENTIAL_EXPIRED", resultado.ReasonCode);
        using var conexion = Abrir();
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorSesionesIdentidad"));
        var rechazo = Contexto("IDENTIFICATION_REJECTED");
        Assert.Equal("CREDENTIAL_EXPIRED", rechazo.GetProperty("ReasonCode").GetString());
        Assert.Equal("SIMULATION_LIST", rechazo.GetProperty("Origin").GetString());
        Assert.Equal(persona, rechazo.GetProperty("PersonId").GetGuid());
    }

    [Fact(DisplayName = "atencion-puesto · Una credencial que no está en la versión activa: IDENTITY_NOT_RESOLVED con UNKNOWN_CODE, sin sesión y sin PersonId en el rechazo")]
    public void Una_credencial_desconocida_no_se_resuelve()
    {
        var resultado = _identificacion.IdentificarPorCredencial(Guid.NewGuid(), PropositoSesion.Consumption);

        Assert.False(resultado.Abierta);
        Assert.Equal("IDENTITY_NOT_RESOLVED", resultado.ReasonCode);
        Assert.Null(resultado.PersonId);
        using var conexion = Abrir();
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorSesionesIdentidad"));
        var rechazo = Contexto("IDENTIFICATION_REJECTED");
        Assert.Equal("UNKNOWN_CODE", rechazo.GetProperty("InternalCause").GetString());
        Assert.Equal("SIMULATION_LIST", rechazo.GetProperty("Origin").GetString());
        Assert.False(rechazo.TryGetProperty("PersonId", out _));
    }

    [Fact(DisplayName = "atencion-puesto · El combo no muestra ni lleva códigos (lista): una entrada por credencial de código del snapshot activo, vigentes o no, en orden alfabético es-AR, con el GUID de la credencial y el nombre visible, sin códigos")]
    public void La_lista_tiene_una_entrada_por_credencial()
    {
        var (_, nandu) = InsertarCredencial("Ñandú sintético", "SINTETICO-ENIE-77");
        var (_, alvaro) = InsertarCredencial("Álvaro sintético", "SINTETICO-ALVARO-77", revocada: Reloj.Ahora.AddDays(-1));
        var (_, nora) = InsertarCredencial("Nora sintética", "SINTETICO-NORA-77", hasta: Reloj.Ahora.AddDays(-1));
        var (_, oscar) = InsertarCredencial("Oscar sintético", "SINTETICO-OSCAR-77", activa: false);

        var lista = _identificacion.ListarParaSimulacion();

        Assert.Equal(
            ["Álvaro sintético", "Nora sintética", "Ñandú sintético", "Oscar sintético", "Persona habilitada"],
            lista.Select(e => e.NombreVisible));
        Assert.Equal([alvaro, nora, nandu, oscar, Fixture.Habilitada.CredencialId!.Value], lista.Select(e => e.CredencialId));
        var codigos = new[] { FixtureSintetico.CodigoSintetico, "SINTETICO-ENIE-77", "SINTETICO-ALVARO-77", "SINTETICO-NORA-77", "SINTETICO-OSCAR-77" };
        Assert.All(lista, e => Assert.DoesNotContain(codigos, c => e.NombreVisible.Contains(c, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Una persona nueva con una credencial SERIAL_CODE en el snapshot del fixture.</summary>
    private (Guid Persona, Guid Credencial) InsertarCredencial(
        string nombre, string valor, DateTimeOffset? hasta = null, DateTimeOffset? revocada = null, bool activa = true)
    {
        var persona = Guid.NewGuid();
        var credencial = Guid.NewGuid();
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, $nombre, $activa, 1)",
            ("$vc", Fixture.SnapshotId), ("$per", persona), ("$nombre", nombre), ("$activa", activa ? 1 : 0));
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, $hasta, $revocada)",
            ("$vc", Fixture.SnapshotId), ("$cre", credencial), ("$per", persona), ("$valor", valor),
            ("$hasta", hasta is null ? null : Iso(hasta.Value)), ("$revocada", revocada is null ? null : Iso(revocada.Value)));
        return (persona, credencial);
    }

    private JsonElement Contexto(string tipo)
    {
        using var conexion = Abrir();
        Assert.Equal(1L, Sql.Escalar<long>(conexion, $"SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = '{tipo}'"));
        var contexto = Sql.Escalar<string>(conexion, $"SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = '{tipo}'");
        return JsonDocument.Parse(contexto).RootElement.Clone();
    }
}
