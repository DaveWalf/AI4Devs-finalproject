using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Asistencia con permiso OPERATOR, recuperación y reimpresión (D8 de atender-seleccion-y-confirmacion), sobre el
/// fixture sintético y con la impresora de prueba como único doble.
/// </summary>
public class AsistenciaTests : ConfirmacionTestBase
{
    private const string CodigoOperador = "OPERADOR-VIGENTE-77";
    private const string CodigoSinPermiso = "PERSONA-SIN-PERMISO-77";
    private const string CodigoDesconocido = "SINTETICO-NINGUNO-77";

    private readonly ImpresoraDePrueba _impresora = new();
    private readonly Asistencia _asistencia;

    public AsistenciaTests()
    {
        _asistencia = new Asistencia(Base, Reloj, new TrabajosImpresion(Base, Reloj, _impresora));
    }

    [Fact(DisplayName = "AT54 · Operador con permiso vigente: sesión PRIVILEGED_ACTION, los comprobantes de la fecha de negocio y ASSISTANCE_GRANTED con RequestedRole, PersonId, GrantId y GrantedRole")]
    public void Operador_vigente_abre_la_asistencia()
    {
        var ticket = AutorizarYEnviar();
        var (persona, permiso) = InsertarOperador(CodigoOperador, "OPERATOR", null, Reloj.Ahora.AddDays(1), null);

        var resultado = _asistencia.Entrar(CodigoOperador);

        Assert.True(resultado.Concedida);
        Assert.Equal("Operador sintético", resultado.NombreVisible);
        using (var conexion = Abrir())
        {
            Assert.Equal("PRIVILEGED_ACTION", Sql.Escalar<string>(conexion,
                "SELECT comsidProposito FROM tbComedorSesionesIdentidad WHERE comsidCodigo = $sid", ("$sid", resultado.SesionId!.Value)));
        }

        var comprobante = Assert.Single(_asistencia.ComprobantesDelDia());
        Assert.Equal(ticket, comprobante.TicketId);
        Assert.Equal("Almuerzo común 1", comprobante.Opcion);
        Assert.Equal("Persona habilitada", comprobante.NombreVisible);
        Assert.Equal(EstadoTrabajoImpresion.Submitted, comprobante.EstadoUltimoTrabajo);
        var contexto = UnicoRegistro("ASSISTANCE_GRANTED");
        Assert.Equal("OPERATOR", contexto.GetProperty("RequestedRole").GetString());
        Assert.Equal(persona, contexto.GetProperty("PersonId").GetGuid());
        Assert.Equal(permiso, contexto.GetProperty("GrantId").GetGuid());
        Assert.Equal("OPERATOR", contexto.GetProperty("GrantedRole").GetString());
    }

    [Fact(DisplayName = "HU03-C · Persona sin permiso: deniega sin sesión y la bitácora registra PERMISSION_MISSING con su PersonId, sin GrantId")]
    public void Persona_sin_permiso_es_denegada()
    {
        var persona = InsertarPersonaConCodigo(CodigoSinPermiso);

        var resultado = _asistencia.Entrar(CodigoSinPermiso);

        Assert.False(resultado.Concedida);
        Assert.Equal("PERMISSION_MISSING", resultado.ReasonCode);
        AssertSinSesiones();
        var contexto = UnicoRegistro("ASSISTANCE_DENIED");
        Assert.Equal("PERMISSION_MISSING", contexto.GetProperty("ReasonCode").GetString());
        Assert.Equal(persona, contexto.GetProperty("PersonId").GetGuid());
        Assert.False(contexto.TryGetProperty("GrantId", out _));
    }

    [Fact(DisplayName = "HU03-I · AT54 · Permiso vencido: ValidToUtc igual al instante da PERMISSION_EXPIRED, sin sesión, con GrantId y GrantedRole en la bitácora")]
    public void Permiso_vencido_es_denegado()
    {
        var (_, permiso) = InsertarOperador(CodigoOperador, "OPERATOR", null, Reloj.Ahora, null);

        var resultado = _asistencia.Entrar(CodigoOperador);

        Assert.Equal("PERMISSION_EXPIRED", resultado.ReasonCode);
        AssertSinSesiones();
        var contexto = UnicoRegistro("ASSISTANCE_DENIED");
        Assert.Equal(permiso, contexto.GetProperty("GrantId").GetGuid());
        Assert.Equal("OPERATOR", contexto.GetProperty("GrantedRole").GetString());
    }

    [Fact(DisplayName = "HU03-I · AT54 · Permiso revocado: PERMISSION_REVOKED, sin sesión")]
    public void Permiso_revocado_es_denegado()
    {
        InsertarOperador(CodigoOperador, "OPERATOR", null, null, Reloj.Ahora.AddDays(-1));

        var resultado = _asistencia.Entrar(CodigoOperador);

        Assert.Equal("PERMISSION_REVOKED", resultado.ReasonCode);
        AssertSinSesiones();
    }

    [Fact(DisplayName = "AT54 · RB26 · Administrador sin OPERATOR: PERMISSION_MISSING, sin sesión")]
    public void Administrador_sin_operador_es_denegado()
    {
        InsertarOperador(CodigoOperador, "SYSTEM_ADMINISTRATOR", null, null, null);

        var resultado = _asistencia.Entrar(CodigoOperador);

        Assert.Equal("PERMISSION_MISSING", resultado.ReasonCode);
        AssertSinSesiones();
    }

    [Fact(DisplayName = "AT54 · RB26 · Autenticación fallida: IDENTITY_NOT_RESOLVED, sin sesión, con RequestedRole OPERATOR y sin PersonId ni el código")]
    public void Codigo_desconocido_es_denegado_sin_persona()
    {
        var resultado = _asistencia.Entrar(CodigoDesconocido);

        Assert.Equal("IDENTITY_NOT_RESOLVED", resultado.ReasonCode);
        AssertSinSesiones();
        var contexto = UnicoRegistro("ASSISTANCE_DENIED");
        Assert.Equal("OPERATOR", contexto.GetProperty("RequestedRole").GetString());
        Assert.False(contexto.TryGetProperty("PersonId", out _));
        using var conexion = Abrir();
        Assert.Equal(0L, Sql.Escalar<long>(conexion,
            "SELECT COUNT(*) FROM tbComedorBitacora WHERE instr(combitContexto, $codigo) > 0", ("$codigo", CodigoDesconocido)));
    }

    [Fact(DisplayName = "RB26 · La sesión de asistencia no habilita consumo: la confirmación responde SesionNoUtilizable sin escribir, y al salir la sesión ya no sirve")]
    public void La_sesion_de_asistencia_no_habilita_consumo()
    {
        var ticket = AutorizarYEnviar();
        var (persona, _) = InsertarOperador(CodigoOperador, "OPERATOR", null, null, null);
        var sesion = _asistencia.Entrar(CodigoOperador).SesionId!.Value;
        Dictionary<string, long> antes;
        using (var conexion = Abrir())
        {
            antes = Sql.ConteoDeEscritura(conexion);
        }

        var confirmacion = Unidad.Confirmar(new SolicitudConfirmacion(
            Guid.NewGuid(), sesion, persona, Fixture.AlmuerzoComun.Id, Fixture.AlmuerzoComun.Version, 1));

        Assert.Equal(TipoResultado.SesionNoUtilizable, confirmacion.Tipo);
        using (var conexion = Abrir())
        {
            Assert.Equal(antes, Sql.ConteoDeEscritura(conexion));
        }

        _asistencia.Salir(sesion);
        var despues = _asistencia.Reimprimir(sesion, ticket, MotivoReimpresion.PapelIlegible);
        Assert.False(despues.Realizada);
        Assert.Equal("SESSION_NOT_USABLE", despues.ReasonCode);
    }

    [Fact(DisplayName = "AT54 · RB08 · Reimpresión con motivo: copia 1 con DUPLICADO y su número; consumo, cupo e importe iguales; la bitácora registra motivo, comprobante, copia, PersonId y GrantId")]
    public void Reimpresion_con_motivo_crea_la_copia_1()
    {
        var ticket = AutorizarYEnviar();
        var (persona, permiso) = InsertarOperador(CodigoOperador, "OPERATOR", null, null, null);
        var sesion = _asistencia.Entrar(CodigoOperador).SesionId!.Value;
        var antes = FotoDelConsumo();

        var resultado = _asistencia.Reimprimir(sesion, ticket, MotivoReimpresion.PapelIlegible);

        Assert.True(resultado.Realizada);
        Assert.Equal(1, resultado.Envio!.NumeroCopia);
        Assert.Equal(EstadoTrabajoImpresion.Submitted, resultado.Envio.Estado);
        var copia = _impresora.Recibidos[^1];
        Assert.Equal(ticket, copia.TicketId);
        Assert.Equal(1, copia.NumeroCopia);
        Assert.Equal("DUPLICADO · copia 1", copia.Leyenda);
        Assert.Equal(antes, FotoDelConsumo());
        using (var conexion = Abrir())
        {
            Assert.Equal(2L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorTrabajosImpresion WHERE comcmpCodigo = $tk", ("$tk", ticket)));
        }

        var contexto = UnicoRegistro("TICKET_REPRINTED");
        Assert.Equal("PAPER_ILLEGIBLE", contexto.GetProperty("Reason").GetString());
        Assert.Equal(ticket, contexto.GetProperty("TicketId").GetGuid());
        Assert.Equal(1, contexto.GetProperty("CopyNumber").GetInt32());
        Assert.Equal(persona, contexto.GetProperty("PersonId").GetGuid());
        Assert.Equal(permiso, contexto.GetProperty("GrantId").GetGuid());
    }

    [Fact(DisplayName = "RB08 · Segunda reimpresión: la copia nueva es la 2")]
    public void Segunda_reimpresion_es_la_copia_2()
    {
        var ticket = AutorizarYEnviar();
        InsertarOperador(CodigoOperador, "OPERATOR", null, null, null);
        var sesion = _asistencia.Entrar(CodigoOperador).SesionId!.Value;
        Assert.True(_asistencia.Reimprimir(sesion, ticket, MotivoReimpresion.PapelAtascado).Realizada);

        var segunda = _asistencia.Reimprimir(sesion, ticket, MotivoReimpresion.Otro);

        Assert.Equal(2, segunda.Envio!.NumeroCopia);
        Assert.Equal("DUPLICADO · copia 2", _impresora.Recibidos[^1].Leyenda);
    }

    [Fact(DisplayName = "HU03-A · AT06 · Trabajo fallido por papel: recuperar envía otra vez el mismo trabajo de copia 0, queda en SUBMITTED, sin trabajo nuevo y con TICKET_RECOVERED")]
    public void Recuperar_reenvia_el_mismo_original()
    {
        _impresora.Modo = ModoImpresoraDePrueba.SinPapel;
        var ticket = AutorizarYEnviar();
        Guid original;
        using (var conexion = Abrir())
        {
            original = Sql.EscalarGuid(conexion, "SELECT comimpCodigo FROM tbComedorTrabajosImpresion WHERE comcmpCodigo = $tk", ("$tk", ticket));
        }

        InsertarOperador(CodigoOperador, "OPERATOR", null, null, null);
        var sesion = _asistencia.Entrar(CodigoOperador).SesionId!.Value;
        _impresora.Modo = ModoImpresoraDePrueba.Disponible;
        var antes = FotoDelConsumo();

        var resultado = _asistencia.Recuperar(sesion, original);

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

        Assert.Equal(original, UnicoRegistro("TICKET_RECOVERED").GetProperty("PrintJobId").GetGuid());
    }

    [Fact(DisplayName = "HU03-B · Resultado incierto: el último trabajo figura en UNKNOWN y, al reimprimir, la copia nueva lleva su número y DUPLICADO")]
    public void Resultado_incierto_se_reimprime_como_copia_marcada()
    {
        _impresora.Modo = ModoImpresoraDePrueba.Incierta;
        var ticket = AutorizarYEnviar();
        InsertarOperador(CodigoOperador, "OPERATOR", null, null, null);
        var sesion = _asistencia.Entrar(CodigoOperador).SesionId!.Value;
        Assert.Equal(EstadoTrabajoImpresion.Unknown, Assert.Single(_asistencia.ComprobantesDelDia()).EstadoUltimoTrabajo);
        _impresora.Modo = ModoImpresoraDePrueba.Disponible;

        var resultado = _asistencia.Reimprimir(sesion, ticket, MotivoReimpresion.ComprobanteNoSalio);

        Assert.True(resultado.Realizada);
        Assert.Equal(1, resultado.Envio!.NumeroCopia);
        Assert.Equal("DUPLICADO · copia 1", _impresora.Recibidos[^1].Leyenda);
    }

    [Fact(DisplayName = "RB26 · AT54 · El permiso vence durante la sesión: reimprimir se deniega con PERMISSION_EXPIRED, sin trabajo nuevo y con PRIVILEGED_ACTION_DENIED")]
    public void Permiso_que_vence_durante_la_sesion_deniega_la_accion()
    {
        var ticket = AutorizarYEnviar();
        InsertarOperador(CodigoOperador, "OPERATOR", null, Reloj.Ahora.AddSeconds(30), null);
        var sesion = _asistencia.Entrar(CodigoOperador).SesionId!.Value;
        Reloj.Ahora = Reloj.Ahora.AddSeconds(30);

        var resultado = _asistencia.Reimprimir(sesion, ticket, MotivoReimpresion.PapelIlegible);

        Assert.False(resultado.Realizada);
        Assert.Equal("PERMISSION_EXPIRED", resultado.ReasonCode);
        Assert.Null(resultado.Envio);
        using (var conexion = Abrir())
        {
            Assert.Equal(1L, Sql.Contar(conexion, "tbComedorTrabajosImpresion"));
        }

        Assert.Equal("PERMISSION_EXPIRED", UnicoRegistro("PRIVILEGED_ACTION_DENIED").GetProperty("ReasonCode").GetString());
    }

    [Fact(DisplayName = "D8 · Recuperar sólo acepta el original en FAILED: un original en SUBMITTED no se reenvía")]
    public void Recuperar_un_original_enviado_no_hace_nada()
    {
        var ticket = AutorizarYEnviar();
        InsertarOperador(CodigoOperador, "OPERATOR", null, null, null);
        var sesion = _asistencia.Entrar(CodigoOperador).SesionId!.Value;
        Guid original;
        using (var conexion = Abrir())
        {
            original = Sql.EscalarGuid(conexion, "SELECT comimpCodigo FROM tbComedorTrabajosImpresion WHERE comcmpCodigo = $tk", ("$tk", ticket));
        }

        var resultado = _asistencia.Recuperar(sesion, original);

        Assert.False(resultado.Realizada);
        Assert.Single(_impresora.Recibidos);
    }

    /// <summary>Confirma un consumo de la persona habilitada y envía su original con la impresora en su modo actual.</summary>
    private Guid AutorizarYEnviar()
    {
        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));
        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        new TrabajosImpresion(Base, Reloj, _impresora).EnviarPendientes();
        return resultado.TicketId!.Value;
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

    private (Guid Persona, Guid Permiso) InsertarOperador(string codigo, string rol, DateTimeOffset? desde, DateTimeOffset? hasta, DateTimeOffset? revocado)
    {
        var persona = InsertarPersonaConCodigo(codigo);
        var permiso = Guid.NewGuid();
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPermisosOperadores VALUES ($vc, $pop, $per, $rol, $desde, $hasta, $revocado)",
            ("$vc", Fixture.SnapshotId), ("$pop", permiso), ("$per", persona), ("$rol", rol),
            ("$desde", desde is null ? null : Iso(desde.Value)), ("$hasta", hasta is null ? null : Iso(hasta.Value)),
            ("$revocado", revocado is null ? null : Iso(revocado.Value)));
        return (persona, permiso);
    }

    private void AssertSinSesiones()
    {
        using var conexion = Abrir();
        Assert.Equal(0L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorSesionesIdentidad WHERE comsidProposito = 'PRIVILEGED_ACTION'"));
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
