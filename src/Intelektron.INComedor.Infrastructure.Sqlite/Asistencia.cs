using System.Globalization;
using Intelektron.INComedor.Domain;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Lista fija de motivos de reimpresión (D8): sin texto libre, para que no entren datos personales.</summary>
public enum MotivoReimpresion
{
    PapelAtascado,
    PapelIlegible,
    ComprobanteNoSalio,
    Otro,
}

/// <summary>La sesión privilegiada abierta, o el motivo de la denegación con el nombre del contrato.</summary>
public sealed record ResultadoAsistencia(Guid? SesionId, Guid? PersonId, string? NombreVisible, string? ReasonCode)
{
    public bool Concedida => SesionId is not null;
}

/// <summary>Un comprobante de la fecha de negocio actual, con el estado de su último trabajo de impresión.</summary>
public sealed record ComprobanteDelDia(
    Guid TicketId,
    DateTimeOffset CreadoUtc,
    string Opcion,
    string NombreVisible,
    Guid TrabajoOriginalId,
    EstadoTrabajoImpresion EstadoOriginal,
    int UltimaCopia,
    EstadoTrabajoImpresion EstadoUltimoTrabajo);

/// <summary>Una acción del operador: hecha, con su envío, o denegada con su motivo.</summary>
public sealed record ResultadoAccion(bool Realizada, string? ReasonCode, ResultadoEnvio? Envio);

/// <summary>
/// Asistencia con permiso OPERATOR (D8 de atender-seleccion-y-confirmacion). Recuperar y reimprimir son acciones
/// distintas, cada una vuelve a evaluar el permiso en su instante (RB26) y ninguna toca consumo, cupo ni comprobante.
/// </summary>
public sealed class Asistencia
{
    private const string RolPedido = "OPERATOR";

    private readonly BaseComedor _baseComedor;
    private readonly TimeProvider _reloj;
    private readonly TrabajosImpresion _trabajos;
    private readonly RegistroBitacora _bitacora = new("Infrastructure.Sqlite.Asistencia");

    public Asistencia(BaseComedor baseComedor, TimeProvider reloj, TrabajosImpresion trabajos)
    {
        _baseComedor = baseComedor;
        _reloj = reloj;
        _trabajos = trabajos;
    }

    /// <summary>Identifica al actor por su código, como la atención, y evalúa el permiso en la misma transacción.</summary>
    public ResultadoAsistencia Entrar(string codigo)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var ahora = _reloj.GetUtcNow();
        var puesto = PuestoLocal.Leer(conexion, transaccion);

        var credencial = IdentificacionPorCodigo.Resolver(conexion, transaccion, puesto, codigo);
        var clasificacion = ClasificadorCredencial.Clasificar(credencial?.Credencial, ahora);
        ResultadoAsistencia resultado;

        if (!clasificacion.Vigente)
        {
            var motivo = IdentificacionPorCodigo.CodigoDe(clasificacion.Motivo!.Value);
            _bitacora.Insertar(conexion, transaccion, puesto, ahora, "ASSISTANCE_DENIED",
                IdentificacionPorCodigo.ContextoSinNulos(
                    ("RequestedRole", RolPedido), ("ReasonCode", motivo),
                    ("InternalCause", IdentificacionPorCodigo.CausaDe(clasificacion)), ("PersonId", credencial?.PersonaId)));
            resultado = new(null, null, null, motivo);
        }
        else
        {
            var permiso = EvaluarPermiso(conexion, transaccion, puesto, credencial!.PersonaId, ahora);
            if (permiso.Concedido)
            {
                var sesion = IdentificacionPorCodigo.AbrirSesion(conexion, transaccion, puesto, credencial, PropositoSesion.PrivilegedAction, ahora);
                _bitacora.Insertar(conexion, transaccion, puesto, ahora, "ASSISTANCE_GRANTED",
                    ContextoDePermiso(credencial.PersonaId, permiso, ("IdentitySessionId", sesion)));
                resultado = new(sesion, credencial.PersonaId, credencial.NombreVisible, null);
            }
            else
            {
                var motivo = CodigoDe(permiso.Motivo!.Value);
                _bitacora.Insertar(conexion, transaccion, puesto, ahora, "ASSISTANCE_DENIED",
                    ContextoDePermiso(credencial.PersonaId, permiso, ("ReasonCode", motivo)));
                resultado = new(null, null, null, motivo);
            }
        }

        transaccion.Commit();
        return resultado;
    }

    /// <summary>Los comprobantes de la fecha de negocio actual, del más reciente al más antiguo.</summary>
    public IReadOnlyList<ComprobanteDelDia> ComprobantesDelDia()
    {
        using var conexion = _baseComedor.AbrirConexion();
        var puesto = PuestoLocal.Leer(conexion, null);
        var fecha = CalculadoraFechaNegocio.Calcular(_reloj.GetUtcNow(), puesto.ZonaHoraria.Id, puesto.InicioDiaNegocio);

        using var comando = ComandosSqlite.Crear(conexion, null,
            """
            SELECT cmp.comcmpCodigo, cmp.comcmpFechaCreacionUtc, men.commenNombre, per.comperNombreVisible,
                   ori.comimpCodigo, ori.comimpEstado, ult.comimpNumeroCopia, ult.comimpEstado
            FROM tbComedorComprobantes cmp
            JOIN tbComedorConsumos con ON con.comconCodigo = cmp.comconCodigo
            JOIN tbComedorOpcionesMenu men ON men.comvcCodigo = con.comvcCodigo AND men.commenCodigo = con.commenCodigo
            JOIN tbComedorPersonas per ON per.comvcCodigo = con.comvcCodigo AND per.comperCodigo = con.comperCodigo
            JOIN tbComedorTrabajosImpresion ori ON ori.comcmpCodigo = cmp.comcmpCodigo AND ori.comimpNumeroCopia = 0
            JOIN tbComedorTrabajosImpresion ult ON ult.comcmpCodigo = cmp.comcmpCodigo
                AND ult.comimpNumeroCopia = (SELECT MAX(comimpNumeroCopia) FROM tbComedorTrabajosImpresion WHERE comcmpCodigo = cmp.comcmpCodigo)
            WHERE con.comconFechaNegocio = $fecha
            ORDER BY cmp.comcmpFechaCreacionUtc DESC, con.comconSecuenciaCliente DESC
            """,
            ("$fecha", fecha.Valor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        using var lector = comando.ExecuteReader();
        var comprobantes = new List<ComprobanteDelDia>();
        while (lector.Read())
        {
            comprobantes.Add(new ComprobanteDelDia(
                lector.GetGuid(0), FormatoInstante.Leer(lector.GetString(1)), lector.GetString(2), lector.GetString(3),
                lector.GetGuid(4), TrabajosImpresion.Estado(lector.GetString(5)),
                lector.GetInt32(6), TrabajosImpresion.Estado(lector.GetString(7))));
        }

        return comprobantes;
    }

    /// <summary>Vuelve a QUEUED el original en FAILED y lo envía otra vez: el mismo trabajo, sin copia nueva.</summary>
    public ResultadoAccion Recuperar(Guid sesion, Guid trabajoId)
    {
        var denegada = Actuar(sesion, "RECOVER", (conexion, transaccion, puesto, persona, permiso, ahora) =>
        {
            using (var comando = ComandosSqlite.Crear(conexion, transaccion,
                       """
                       UPDATE tbComedorTrabajosImpresion
                       SET comimpEstado = 'QUEUED', comimpFechaActualizacionUtc = $ahora
                       WHERE comimpCodigo = $job AND comimpNumeroCopia = 0 AND comimpEstado = 'FAILED'
                       """,
                       ("$ahora", FormatoInstante.Texto(ahora)), ("$job", trabajoId)))
            {
                if (comando.ExecuteNonQuery() == 0)
                {
                    return "PRINT_JOB_NOT_RECOVERABLE";
                }
            }

            using var consulta = ComandosSqlite.Crear(conexion, transaccion,
                "SELECT comcmpCodigo FROM tbComedorTrabajosImpresion WHERE comimpCodigo = $job", ("$job", trabajoId));
            var ticket = Guid.Parse((string)consulta.ExecuteScalar()!);
            _bitacora.Insertar(conexion, transaccion, puesto, ahora, "TICKET_RECOVERED",
                ContextoDePermiso(persona, permiso, ("TicketId", ticket), ("PrintJobId", trabajoId), ("CopyNumber", 0)));
            return null;
        });

        return denegada ?? Enviado(trabajoId);
    }

    /// <summary>Crea la copia siguiente del comprobante, con el motivo de la lista fija, y la envía.</summary>
    public ResultadoAccion Reimprimir(Guid sesion, Guid ticketId, MotivoReimpresion motivo)
    {
        var trabajoId = Guid.NewGuid();
        var denegada = Actuar(sesion, "REPRINT", (conexion, transaccion, puesto, persona, permiso, ahora) =>
        {
            if (ComandosSqlite.EscalarLong(conexion, transaccion,
                    "SELECT COUNT(*) FROM tbComedorComprobantes WHERE comcmpCodigo = $tk", ("$tk", ticketId)) == 0)
            {
                return "TICKET_NOT_FOUND";
            }

            var copia = (int)ComandosSqlite.EscalarLong(conexion, transaccion,
                "SELECT MAX(comimpNumeroCopia) + 1 FROM tbComedorTrabajosImpresion WHERE comcmpCodigo = $tk", ("$tk", ticketId));
            ComandosSqlite.Ejecutar(conexion, transaccion,
                """
                INSERT INTO tbComedorTrabajosImpresion (comimpCodigo, comcmpCodigo, comimpNumeroCopia, comimpEstado,
                    comimpIdentificadorTrabajoSpooler, comimpCodigoUltimoError, comimpFechaActualizacionUtc)
                VALUES ($job, $tk, $copia, 'QUEUED', NULL, NULL, $ahora)
                """,
                ("$job", trabajoId), ("$tk", ticketId), ("$copia", copia), ("$ahora", FormatoInstante.Texto(ahora)));
            _bitacora.Insertar(conexion, transaccion, puesto, ahora, "TICKET_REPRINTED",
                ContextoDePermiso(persona, permiso,
                    ("Reason", CodigoDe(motivo)), ("TicketId", ticketId), ("PrintJobId", trabajoId), ("CopyNumber", copia)));
            return null;
        });

        return denegada ?? Enviado(trabajoId);
    }

    /// <summary>Cierra la sesión privilegiada: la sesión no sirve para otra acción (RB26).</summary>
    public void Salir(Guid sesion)
    {
        using var conexion = _baseComedor.AbrirConexion();
        ComandosSqlite.Ejecutar(conexion, null,
            """
            UPDATE tbComedorSesionesIdentidad SET comsidFechaVencimientoUtc = $ahora
            WHERE comsidCodigo = $sid AND comsidProposito = 'PRIVILEGED_ACTION' AND comsidFechaVencimientoUtc > $ahora
            """,
            ("$sid", sesion), ("$ahora", FormatoInstante.Texto(_reloj.GetUtcNow())));
    }

    public static string CodigoDe(MotivoReimpresion motivo) => motivo switch
    {
        MotivoReimpresion.PapelAtascado => "PAPER_JAMMED",
        MotivoReimpresion.PapelIlegible => "PAPER_ILLEGIBLE",
        MotivoReimpresion.ComprobanteNoSalio => "TICKET_NOT_PRINTED",
        MotivoReimpresion.Otro => "OTHER",
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, null),
    };

    private delegate string? AccionPrivilegiada(
        SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, Guid persona, ResultadoPermiso permiso, DateTimeOffset ahora);

    /// <summary>
    /// Corre la acción en BEGIN IMMEDIATE después de comprobar la sesión y el permiso en ese instante. Devuelve null
    /// si la acción quedó hecha; si no, la denegación, que se registra como PRIVILEGED_ACTION_DENIED.
    /// </summary>
    private ResultadoAccion? Actuar(Guid sesion, string accion, AccionPrivilegiada cuerpo)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var ahora = _reloj.GetUtcNow();
        var puesto = PuestoLocal.Leer(conexion, transaccion);

        var persona = PersonaDeSesion(conexion, transaccion, sesion, ahora);
        string? motivo;
        if (persona is null)
        {
            motivo = "SESSION_NOT_USABLE";
            _bitacora.Insertar(conexion, transaccion, puesto, ahora, "PRIVILEGED_ACTION_DENIED",
                new Dictionary<string, object> { ["RequestedRole"] = RolPedido, ["Action"] = accion, ["ReasonCode"] = motivo });
        }
        else
        {
            var permiso = EvaluarPermiso(conexion, transaccion, puesto, persona.Value, ahora);
            if (permiso.Concedido)
            {
                motivo = cuerpo(conexion, transaccion, puesto, persona.Value, permiso, ahora);
                if (motivo is null)
                {
                    transaccion.Commit();
                    return null;
                }
            }
            else
            {
                motivo = CodigoDe(permiso.Motivo!.Value);
                _bitacora.Insertar(conexion, transaccion, puesto, ahora, "PRIVILEGED_ACTION_DENIED",
                    ContextoDePermiso(persona.Value, permiso, ("Action", accion), ("ReasonCode", motivo)));
            }
        }

        transaccion.Commit();
        return new ResultadoAccion(false, motivo, null);
    }

    /// <summary>Envía después del commit y devuelve cómo quedó el trabajo de la acción.</summary>
    private ResultadoAccion Enviado(Guid trabajoId)
    {
        var envio = _trabajos.EnviarPendientes().FirstOrDefault(e => e.TrabajoId == trabajoId);
        return new ResultadoAccion(true, null, envio);
    }

    /// <summary>La persona de una sesión PRIVILEGED_ACTION no vencida; null si no se puede usar.</summary>
    private static Guid? PersonaDeSesion(SqliteConnection conexion, SqliteTransaction transaccion, Guid sesion, DateTimeOffset ahora)
    {
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            "SELECT comperCodigo, comsidProposito, comsidFechaVencimientoUtc FROM tbComedorSesionesIdentidad WHERE comsidCodigo = $sid",
            ("$sid", sesion));
        using var lector = comando.ExecuteReader();
        if (!lector.Read() || lector.GetString(1) != "PRIVILEGED_ACTION" || FormatoInstante.Leer(lector.GetString(2)) <= ahora)
        {
            return null;
        }

        return lector.GetGuid(0);
    }

    private static ResultadoPermiso EvaluarPermiso(
        SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, Guid persona, DateTimeOffset ahora)
    {
        var permisos = new List<PermisoOperador>();
        using (var comando = ComandosSqlite.Crear(conexion, transaccion,
                   """
                   SELECT compopCodigo, compopRol, compopFechaVigenciaDesdeUtc, compopFechaVigenciaHastaUtc, compopFechaRevocacionUtc
                   FROM tbComedorPermisosOperadores WHERE comvcCodigo = $vc AND comperCodigo = $per
                   """,
                   ("$vc", puesto.SnapshotId), ("$per", persona)))
        using (var lector = comando.ExecuteReader())
        {
            while (lector.Read())
            {
                permisos.Add(new PermisoOperador(
                    lector.GetGuid(0), Rol(lector.GetString(1)),
                    Instante(lector, 2), Instante(lector, 3), Instante(lector, 4)));
            }
        }

        return EvaluadorPermisoOperador.Evaluar(permisos, RolOperador.Operator, ahora);
    }

    private static Dictionary<string, object> ContextoDePermiso(Guid persona, ResultadoPermiso permiso, params (string Clave, object? Valor)[] mas) =>
        IdentificacionPorCodigo.ContextoSinNulos(
            [("RequestedRole", RolPedido), ("PersonId", persona), ("GrantId", permiso.GrantId),
             ("GrantedRole", permiso.GrantedRole is { } rol ? CodigoDe(rol) : null), .. mas]);

    private static RolOperador Rol(string rol) => rol switch
    {
        "OPERATOR" => RolOperador.Operator,
        "SYSTEM_ADMINISTRATOR" => RolOperador.SystemAdministrator,
        _ => throw new ArgumentOutOfRangeException(nameof(rol), rol, null),
    };

    private static string CodigoDe(RolOperador rol) => rol switch
    {
        RolOperador.Operator => "OPERATOR",
        RolOperador.SystemAdministrator => "SYSTEM_ADMINISTRATOR",
        _ => throw new ArgumentOutOfRangeException(nameof(rol), rol, null),
    };

    private static string CodigoDe(MotivoPermiso motivo) => motivo switch
    {
        MotivoPermiso.PermissionMissing => "PERMISSION_MISSING",
        MotivoPermiso.PermissionRevoked => "PERMISSION_REVOKED",
        MotivoPermiso.PermissionExpired => "PERMISSION_EXPIRED",
        MotivoPermiso.PermissionNotYetValid => "PERMISSION_NOT_YET_VALID",
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, null),
    };

    private static DateTimeOffset? Instante(SqliteDataReader lector, int columna) =>
        lector.IsDBNull(columna) ? null : FormatoInstante.Leer(lector.GetString(columna));
}
