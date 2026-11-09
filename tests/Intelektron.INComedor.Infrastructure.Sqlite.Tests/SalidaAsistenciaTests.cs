using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// La salida de la asistencia queda en la bitácora (D8 de configurar-puesto-por-pestanas, la sugerencia de la verificación de
/// escribir-bitacoras-externas): PRIVILEGED_SESSION_CLOSED con la sesión y la causa, sin el código, una sola vez.
/// </summary>
public class SalidaAsistenciaTests : ConfirmacionTestBase
{
    private const string CodigoOperador = "OPERADOR-SALIDA-77";

    private readonly Asistencia _asistencia;

    public SalidaAsistenciaTests()
    {
        _asistencia = new Asistencia(Base, Reloj, new TrabajosImpresion(Base, Reloj, new ImpresoraDePrueba()));
    }

    [Theory(DisplayName = "comprobante-local · Salir registra PRIVILEGED_SESSION_CLOSED con la sesión y la causa, sin el código, y la sesión deja de servir")]
    [InlineData("EXIT")]
    [InlineData("CONFIGURATION_CLOSED")]
    [InlineData("TIMEOUT")]
    public void Salir_registra_el_cierre(string causa)
    {
        InsertarOperador();
        var sesion = _asistencia.Entrar(CodigoOperador).SesionId!.Value;

        _asistencia.Salir(sesion, causa);

        var (contexto, crudo) = UnicoRegistro("PRIVILEGED_SESSION_CLOSED");
        Assert.Equal(sesion, contexto.GetProperty("IdentitySessionId").GetGuid());
        Assert.Equal(causa, contexto.GetProperty("Cause").GetString());
        Assert.DoesNotContain(CodigoOperador, crudo);
        Assert.Equal("SESSION_NOT_USABLE", _asistencia.Reimprimir(sesion, Guid.NewGuid(), MotivoReimpresion.Otro).ReasonCode);
    }

    [Fact(DisplayName = "comprobante-local · Salir de una sesión ya cerrada no deja otro registro, y Salir sin causa es EXIT")]
    public void Salir_dos_veces_registra_una()
    {
        InsertarOperador();
        var sesion = _asistencia.Entrar(CodigoOperador).SesionId!.Value;

        _asistencia.Salir(sesion);
        _asistencia.Salir(sesion, "TIMEOUT");

        Assert.Equal("EXIT", UnicoRegistro("PRIVILEGED_SESSION_CLOSED").Contexto.GetProperty("Cause").GetString());
    }

    private void InsertarOperador()
    {
        var persona = Guid.NewGuid();
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Operador sintético', 1, 1)",
            ("$vc", Fixture.SnapshotId), ("$per", persona));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, NULL, NULL)",
            ("$vc", Fixture.SnapshotId), ("$cre", Guid.NewGuid()), ("$per", persona), ("$valor", CodigoOperador));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPermisosOperadores VALUES ($vc, $pop, $per, 'OPERATOR', NULL, NULL, NULL)",
            ("$vc", Fixture.SnapshotId), ("$pop", Guid.NewGuid()), ("$per", persona));
    }

    private (JsonElement Contexto, string Crudo) UnicoRegistro(string tipo)
    {
        using var conexion = Abrir();
        Assert.Equal(1L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = $tipo", ("$tipo", tipo)));
        var crudo = Sql.Escalar<string>(conexion, "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = $tipo", ("$tipo", tipo));
        return (JsonDocument.Parse(crudo).RootElement.Clone(), crudo);
    }
}
