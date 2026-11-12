using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// La entrada del operador desde la identificación (D1 y D2 de operador-desde-la-identificacion): <c>EsOperador</c> sólo lee el
/// permiso OPERATOR del snapshot activo, sin escribir nada; <c>EntrarDesdeIdentificacion</c> vuelve a evaluarlo (RB26), cierra
/// la sesión de consumo y abre la PRIVILEGED_ACTION en una transacción, con ASSISTANCE_GRANTED o ASSISTANCE_DENIED de Source
/// IDENTIFICATION. Los cinco operadores del fixture repiten los casos de 9001 a 9004 y 0001 de la demostración.
/// </summary>
public class EntradaDesdeIdentificacionTests : ConfirmacionTestBase
{
    private const string Vigente = "OPERADOR-9001";
    private const string SinPermiso = "PERSONA-0001";
    private const string Vencido = "OPERADOR-9002";
    private const string Revocado = "OPERADOR-9003";
    private const string Administrador = "ADMINISTRADOR-9004";

    private readonly Asistencia _asistencia;
    private readonly IdentificacionPorCodigo _identificacion;
    private readonly Dictionary<string, Guid> _personas = [];

    public EntradaDesdeIdentificacionTests()
    {
        _asistencia = new Asistencia(Base, Reloj, new TrabajosImpresion(Base, Reloj, new ImpresoraDePrueba()));
        _identificacion = new IdentificacionPorCodigo(Base, Reloj);
        _personas[Vigente] = InsertarOperador(Vigente, "OPERATOR", null, Reloj.Ahora.AddDays(1), null);
        _personas[SinPermiso] = InsertarPersonaConCodigo(SinPermiso);
        _personas[Vencido] = InsertarOperador(Vencido, "OPERATOR", null, Reloj.Ahora.AddDays(-1), null);
        _personas[Revocado] = InsertarOperador(Revocado, "OPERATOR", null, null, Reloj.Ahora.AddDays(-1));
        _personas[Administrador] = InsertarOperador(Administrador, "SYSTEM_ADMINISTRATOR", null, null, null);
    }

    [Fact(DisplayName = "atencion-puesto · EsOperador da sí sólo para el operador vigente, y no para la persona sin permiso, el vencido, el revocado ni el administrador sin OPERATOR, sin escribir ninguna fila")]
    public void Es_operador_solo_lee_el_permiso()
    {
        var antes = Filas();

        Assert.True(_asistencia.EsOperador(_personas[Vigente]));
        Assert.False(_asistencia.EsOperador(_personas[SinPermiso]));
        Assert.False(_asistencia.EsOperador(_personas[Vencido]));
        Assert.False(_asistencia.EsOperador(_personas[Revocado]));
        Assert.False(_asistencia.EsOperador(_personas[Administrador]));

        Assert.Equal(antes, Filas());
    }

    [Fact(DisplayName = "atencion-puesto · Entrar desde la identificación: con la sesión de consumo del operador vigente, la cierra, abre una PRIVILEGED_ACTION y deja ASSISTANCE_GRANTED con Source IDENTIFICATION")]
    public void Entrar_desde_la_identificacion()
    {
        var consumo = Identificar(Vigente);

        var resultado = _asistencia.EntrarDesdeIdentificacion(consumo);

        Assert.True(resultado.Concedida);
        Assert.Equal(_personas[Vigente], resultado.PersonId);
        Assert.Equal("Operador sintético", resultado.NombreVisible);
        using (var conexion = Abrir())
        {
            Assert.Equal("PRIVILEGED_ACTION", Sql.Escalar<string>(conexion,
                "SELECT comsidProposito FROM tbComedorSesionesIdentidad WHERE comsidCodigo = $sid", ("$sid", resultado.SesionId!.Value)));
            Assert.True(SesionVencida(conexion, consumo), "La sesión de consumo sigue usable.");
        }

        var contexto = UnicoRegistro("ASSISTANCE_GRANTED");
        Assert.Equal("IDENTIFICATION", contexto.GetProperty("Source").GetString());
        Assert.Equal("OPERATOR", contexto.GetProperty("RequestedRole").GetString());
        Assert.Equal(_personas[Vigente], contexto.GetProperty("PersonId").GetGuid());
        Assert.Equal(resultado.SesionId, contexto.GetProperty("IdentitySessionId").GetGuid());
        Assert.DoesNotContain(Vigente, contexto.GetRawText());
    }

    [Fact(DisplayName = "atencion-puesto · El permiso cae antes de entrar: vencido entre identificarse y tocar «Configuración», deniega con PERMISSION_EXPIRED, sin sesión nueva, y la de consumo sigue usable")]
    public void El_permiso_cae_antes_de_entrar()
    {
        var consumo = Identificar(Vigente);
        using (var conexion = Abrir())
        {
            Sql.Ejecutar(conexion, "UPDATE tbComedorPermisosOperadores SET compopFechaVigenciaHastaUtc = $ahora WHERE comperCodigo = $per",
                ("$ahora", Iso(Reloj.Ahora)), ("$per", _personas[Vigente]));
        }

        var resultado = _asistencia.EntrarDesdeIdentificacion(consumo);

        Assert.False(resultado.Concedida);
        Assert.Equal("PERMISSION_EXPIRED", resultado.ReasonCode);
        using (var conexion = Abrir())
        {
            Assert.Equal(0L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorSesionesIdentidad WHERE comsidProposito = 'PRIVILEGED_ACTION'"));
            Assert.False(SesionVencida(conexion, consumo), "La sesión de consumo se cerró aunque se denegó la entrada.");
        }

        var contexto = UnicoRegistro("ASSISTANCE_DENIED");
        Assert.Equal("PERMISSION_EXPIRED", contexto.GetProperty("ReasonCode").GetString());
        Assert.Equal("IDENTIFICATION", contexto.GetProperty("Source").GetString());
    }

    [Fact(DisplayName = "atencion-puesto · Una sesión de consumo vencida: deniega con SESSION_NOT_USABLE, sin sesión nueva")]
    public void Una_sesion_de_consumo_vencida()
    {
        var consumo = Identificar(Vigente);
        using (var conexion = Abrir())
        {
            Sql.Ejecutar(conexion, "UPDATE tbComedorSesionesIdentidad SET comsidFechaVencimientoUtc = $ahora WHERE comsidCodigo = $sid",
                ("$ahora", Iso(Reloj.Ahora)), ("$sid", consumo));
        }

        var resultado = _asistencia.EntrarDesdeIdentificacion(consumo);

        Assert.False(resultado.Concedida);
        Assert.Equal("SESSION_NOT_USABLE", resultado.ReasonCode);
        using (var conexion = Abrir())
        {
            Assert.Equal(0L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorSesionesIdentidad WHERE comsidProposito = 'PRIVILEGED_ACTION'"));
        }

        Assert.Equal("SESSION_NOT_USABLE", UnicoRegistro("ASSISTANCE_DENIED").GetProperty("ReasonCode").GetString());
    }

    [Fact(DisplayName = "atencion-puesto · Una sesión que no es de consumo no sirve para entrar: una PRIVILEGED_ACTION deniega con SESSION_NOT_USABLE")]
    public void Una_sesion_privilegiada_no_sirve()
    {
        var privilegiada = _asistencia.Entrar(Vigente).SesionId!.Value;

        var resultado = _asistencia.EntrarDesdeIdentificacion(privilegiada);

        Assert.Equal("SESSION_NOT_USABLE", resultado.ReasonCode);
    }

    private Guid Identificar(string codigo)
    {
        var resultado = _identificacion.Identificar(codigo, PropositoSesion.Consumption);
        Assert.True(resultado.Abierta);
        return resultado.IdentitySessionId!.Value;
    }

    private bool SesionVencida(Microsoft.Data.Sqlite.SqliteConnection conexion, Guid sesion) =>
        string.CompareOrdinal(Sql.Escalar<string>(conexion,
            "SELECT comsidFechaVencimientoUtc FROM tbComedorSesionesIdentidad WHERE comsidCodigo = $sid", ("$sid", sesion)), Iso(Reloj.Ahora)) <= 0;

    /// <summary>Cuántas filas tienen las tablas que una lectura no debe tocar.</summary>
    private (long Sesiones, long Bitacora) Filas()
    {
        using var conexion = Abrir();
        return (Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorSesionesIdentidad"),
            Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorBitacora"));
    }

    private Guid InsertarPersonaConCodigo(string codigo)
    {
        var persona = Guid.NewGuid();
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Operador sintético', 1, 1)",
            ("$vc", Fixture.SnapshotId), ("$per", persona));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, NULL, NULL)",
            ("$vc", Fixture.SnapshotId), ("$cre", Guid.NewGuid()), ("$per", persona), ("$valor", codigo));
        return persona;
    }

    private Guid InsertarOperador(string codigo, string rol, DateTimeOffset? desde, DateTimeOffset? hasta, DateTimeOffset? revocado)
    {
        var persona = InsertarPersonaConCodigo(codigo);
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPermisosOperadores VALUES ($vc, $pop, $per, $rol, $desde, $hasta, $revocado)",
            ("$vc", Fixture.SnapshotId), ("$pop", Guid.NewGuid()), ("$per", persona), ("$rol", rol),
            ("$desde", desde is null ? null : Iso(desde.Value)), ("$hasta", hasta is null ? null : Iso(hasta.Value)),
            ("$revocado", revocado is null ? null : Iso(revocado.Value)));
        return persona;
    }

    private JsonElement UnicoRegistro(string tipo)
    {
        using var conexion = Abrir();
        Assert.Equal(1L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = $tipo", ("$tipo", tipo)));
        var contexto = Sql.Escalar<string>(conexion, "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = $tipo", ("$tipo", tipo));
        return JsonDocument.Parse(contexto).RootElement.Clone();
    }
}
