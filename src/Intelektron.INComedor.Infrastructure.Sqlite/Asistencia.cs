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

/// <summary>
/// Un comprobante con el estado de su último trabajo de impresión: los del día que listaba Comprobantes y, desde
/// buscar-y-exportar-consumos, el de cualquier fila de Consumos que se reimprime, con el número que muestra su fila (el
/// correlativo o los ocho caracteres, D2 de enriquecer-comprobante).
/// </summary>
public sealed record ComprobanteDelDia(
    Guid TicketId,
    DateTimeOffset CreadoUtc,
    string Opcion,
    string NombreVisible,
    Guid TrabajoOriginalId,
    EstadoTrabajoImpresion EstadoOriginal,
    int UltimaCopia,
    EstadoTrabajoImpresion EstadoUltimoTrabajo,
    string? Numero = null);

/// <summary>Una acción del operador: hecha, con su envío, o denegada con su motivo.</summary>
public sealed record ResultadoAccion(bool Realizada, string? ReasonCode, ResultadoEnvio? Envio);

/// <summary>
/// Asistencia con permiso OPERATOR (D8 de atender-seleccion-y-confirmacion). Recuperar y reimprimir son acciones
/// distintas, cada una vuelve a evaluar el permiso en su instante (RB26) y ninguna toca consumo, cupo ni comprobante.
/// Desde reimprimir-sin-codigo, las dos tienen además su variante con la clave del día, sin sesión y con el actor nulo (D1).
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

    /// <summary>
    /// Si la persona tiene un permiso OPERATOR vigente en el snapshot activo, en este instante (D1 de
    /// operador-desde-la-identificacion): sólo lee, sin abrir sesión ni escribir en la bitácora. La atención lo usa para
    /// mostrar «Configuración»; tocarlo lo vuelve a evaluar.
    /// </summary>
    public bool EsOperador(Guid personaId)
    {
        using var conexion = _baseComedor.AbrirConexion();
        var puesto = PuestoLocal.Leer(conexion, null);
        return EvaluarPermiso(conexion, null, puesto, personaId, _reloj.GetUtcNow()).Concedido;
    }

    /// <summary>
    /// Lo mismo que <see cref="Entrar"/>, pero con la credencial de una sesión de consumo vigente en lugar de un código (D2 de
    /// operador-desde-la-identificacion): en una transacción vuelve a evaluar OPERATOR (RB26), cierra la sesión de consumo, abre
    /// la PRIVILEGED_ACTION y deja ASSISTANCE_GRANTED o ASSISTANCE_DENIED con Source IDENTIFICATION. Una denegación deja la
    /// sesión de consumo como estaba. Sirve igual para el código tipeado y el combo, que abren la misma sesión.
    /// </summary>
    public ResultadoAsistencia EntrarDesdeIdentificacion(Guid sesionConsumo)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var ahora = _reloj.GetUtcNow();
        var puesto = PuestoLocal.Leer(conexion, transaccion);
        const string Origen = "IDENTIFICATION";

        var consumo = SesionDeConsumo(conexion, transaccion, puesto, sesionConsumo, ahora);
        ResultadoAsistencia resultado;
        if (consumo is not { } vigente)
        {
            const string Motivo = "SESSION_NOT_USABLE";
            _bitacora.Insertar(conexion, transaccion, puesto, ahora, "ASSISTANCE_DENIED",
                new Dictionary<string, object> { ["RequestedRole"] = RolPedido, ["ReasonCode"] = Motivo, ["Source"] = Origen });
            resultado = new(null, null, null, Motivo);
        }
        else
        {
            var permiso = EvaluarPermiso(conexion, transaccion, puesto, vigente.PersonaId, ahora);
            if (permiso.Concedido)
            {
                ComandosSqlite.Ejecutar(conexion, transaccion,
                    "UPDATE tbComedorSesionesIdentidad SET comsidFechaVencimientoUtc = $ahora WHERE comsidCodigo = $sid",
                    ("$sid", sesionConsumo), ("$ahora", FormatoInstante.Texto(ahora)));
                var sesion = IdentificacionPorCodigo.AbrirSesion(conexion, transaccion, puesto, vigente, PropositoSesion.PrivilegedAction, ahora);
                _bitacora.Insertar(conexion, transaccion, puesto, ahora, "ASSISTANCE_GRANTED",
                    ContextoDePermiso(vigente.PersonaId, permiso, ("IdentitySessionId", sesion), ("Source", Origen)));
                resultado = new(sesion, vigente.PersonaId, vigente.NombreVisible, null);
            }
            else
            {
                var motivo = CodigoDe(permiso.Motivo!.Value);
                _bitacora.Insertar(conexion, transaccion, puesto, ahora, "ASSISTANCE_DENIED",
                    ContextoDePermiso(vigente.PersonaId, permiso, ("ReasonCode", motivo), ("Source", Origen)));
                resultado = new(null, null, null, motivo);
            }
        }

        transaccion.Commit();
        return resultado;
    }

    /// <summary>
    /// La credencial y la persona de una sesión CONSUMPTION del snapshot activo, no vencida y todavía sin consumir; null si no
    /// se puede usar.
    /// </summary>
    private static CredencialResuelta? SesionDeConsumo(
        SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, Guid sesion, DateTimeOffset ahora)
    {
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            """
            SELECT sid.comcreCodigo, sid.comperCodigo, per.comperNombreVisible, sid.comsidProposito, sid.comsidFechaVencimientoUtc,
                   sid.comicCodigoConsumidora
            FROM tbComedorSesionesIdentidad sid
            JOIN tbComedorPersonas per ON per.comvcCodigo = sid.comvcCodigo AND per.comperCodigo = sid.comperCodigo
            WHERE sid.comsidCodigo = $sid AND sid.comvcCodigo = $vc
            """,
            ("$sid", sesion), ("$vc", puesto.SnapshotId));
        using var lector = comando.ExecuteReader();
        if (!lector.Read() || lector.IsDBNull(0) || lector.GetString(3) != "CONSUMPTION"
            || FormatoInstante.Leer(lector.GetString(4)) <= ahora || !lector.IsDBNull(5))
        {
            return null;
        }

        return new CredencialResuelta(lector.GetGuid(0), lector.GetGuid(1), lector.GetString(2), new CredencialIdentificacion(null, null, null));
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
    public ResultadoAccion Recuperar(Guid sesion, Guid trabajoId) =>
        Actuar(sesion, "RECOVER", (conexion, transaccion, puesto, persona, permiso, ahora) =>
            CuerpoRecuperar(conexion, transaccion, puesto, ahora, trabajoId, mas => ContextoDePermiso(persona, permiso, mas)))
        ?? Enviado(trabajoId);

    /// <summary>Crea la copia siguiente del comprobante, con el motivo de la lista fija, y la envía.</summary>
    public ResultadoAccion Reimprimir(Guid sesion, Guid ticketId, MotivoReimpresion motivo)
    {
        var trabajoId = Guid.NewGuid();
        return Actuar(sesion, "REPRINT", (conexion, transaccion, puesto, persona, permiso, ahora) =>
                   CuerpoReimprimir(conexion, transaccion, puesto, ahora, ticketId, motivo, trabajoId,
                       mas => ContextoDePermiso(persona, permiso, mas)))
               ?? Enviado(trabajoId);
    }

    /// <summary>
    /// Reimprime con la clave del día, que no identifica a nadie: sin sesión ni permiso, con el actor nulo, NO_AUTHENTICATION
    /// y Access DAILY_KEY en TICKET_REPRINTED, que es de donde la copia saca la marca SIN VALIDEZ (D1 y D2 de
    /// reimprimir-sin-codigo). Sólo lo llama una Configuración abierta con la clave del día.
    /// </summary>
    public ResultadoAccion ReimprimirConClave(Guid ticketId, MotivoReimpresion motivo)
    {
        var trabajoId = Guid.NewGuid();
        return ActuarConClave((conexion, transaccion, puesto, ahora) =>
                   CuerpoReimprimir(conexion, transaccion, puesto, ahora, ticketId, motivo, trabajoId, ContextoConClave))
               ?? Enviado(trabajoId);
    }

    /// <summary>
    /// Recupera el original en FAILED con la clave del día: el mismo trabajo, sin marca, porque es el comprobante del consumo
    /// (D3 de reimprimir-sin-codigo); sólo TICKET_RECOVERED dice que se hizo con la clave.
    /// </summary>
    public ResultadoAccion RecuperarConClave(Guid trabajoId) =>
        ActuarConClave((conexion, transaccion, puesto, ahora) =>
            CuerpoRecuperar(conexion, transaccion, puesto, ahora, trabajoId, ContextoConClave))
        ?? Enviado(trabajoId);

    /// <summary>Cierra la sesión privilegiada porque el operador tocó «Salir» (RB26).</summary>
    public void Salir(Guid sesion) => Salir(sesion, "EXIT");

    /// <summary>
    /// Cierra la sesión privilegiada: la sesión no sirve para otra acción (RB26). Si estaba abierta, deja
    /// PRIVILEGED_SESSION_CLOSED con la sesión y la causa, sin el código, en la misma transacción (D8 de
    /// configurar-puesto-por-pestanas, la sugerencia de la verificación de escribir-bitacoras-externas).
    /// </summary>
    public void Salir(Guid sesion, string causa)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var ahora = _reloj.GetUtcNow();
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            """
            UPDATE tbComedorSesionesIdentidad SET comsidFechaVencimientoUtc = $ahora
            WHERE comsidCodigo = $sid AND comsidProposito = 'PRIVILEGED_ACTION' AND comsidFechaVencimientoUtc > $ahora
            """,
            ("$sid", sesion), ("$ahora", FormatoInstante.Texto(ahora)));
        if (comando.ExecuteNonQuery() > 0)
        {
            _bitacora.Insertar(conexion, transaccion, PuestoLocal.Leer(conexion, transaccion), ahora, "PRIVILEGED_SESSION_CLOSED",
                new { IdentitySessionId = sesion, Cause = causa });
        }

        transaccion.Commit();
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

    private delegate string? AccionConClave(SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, DateTimeOffset ahora);

    /// <summary>
    /// Corre la acción de la clave del día en BEGIN IMMEDIATE, sin buscar persona ni permiso (D1 de reimprimir-sin-codigo).
    /// Devuelve null si quedó hecha; si no, la denegación, sin nada escrito.
    /// </summary>
    private ResultadoAccion? ActuarConClave(AccionConClave cuerpo)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var motivo = cuerpo(conexion, transaccion, PuestoLocal.Leer(conexion, transaccion), _reloj.GetUtcNow());
        transaccion.Commit();
        return motivo is null ? null : new ResultadoAccion(false, motivo, null);
    }

    /// <summary>Lo común de recuperar, después de habilitar la acción: el contexto del actor lo arma quien la habilitó.</summary>
    private string? CuerpoRecuperar(
        SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, DateTimeOffset ahora, Guid trabajoId,
        Func<(string Clave, object? Valor)[], object> contexto)
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
            contexto([("TicketId", ticket), ("PrintJobId", trabajoId), ("CopyNumber", 0)]));
        return null;
    }

    /// <summary>Lo común de reimprimir, después de habilitar la acción: el contexto del actor lo arma quien la habilitó.</summary>
    private string? CuerpoReimprimir(
        SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, DateTimeOffset ahora, Guid ticketId,
        MotivoReimpresion motivo, Guid trabajoId, Func<(string Clave, object? Valor)[], object> contexto)
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
            contexto([("Reason", CodigoDe(motivo)), ("TicketId", ticketId), ("PrintJobId", trabajoId), ("CopyNumber", copia)]));
        return null;
    }

    /// <summary>
    /// El actor de la clave del día, como CONSUMPTIONS_QUERIED: ActorPersonId nulo (escrito, no omitido), NO_AUTHENTICATION y
    /// Access DAILY_KEY, sin PersonId, GrantId ni rol.
    /// </summary>
    private static object ContextoConClave((string Clave, object? Valor)[] mas)
    {
        var contexto = new Dictionary<string, object?>
        {
            ["ActorPersonId"] = null,
            ["ActorReason"] = "NO_AUTHENTICATION",
            ["Access"] = "DAILY_KEY",
        };
        foreach (var (clave, valor) in mas)
        {
            contexto[clave] = valor;
        }

        return contexto;
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
        SqliteConnection conexion, SqliteTransaction? transaccion, PuestoLocal puesto, Guid persona, DateTimeOffset ahora)
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
