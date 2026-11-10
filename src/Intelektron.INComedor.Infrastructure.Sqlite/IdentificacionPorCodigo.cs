using Intelektron.INComedor.Domain;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

public enum PropositoSesion
{
    Consumption,
    PrivilegedAction,
}

/// <summary>
/// La sesión abierta, o el motivo del rechazo con el nombre del contrato. Nunca lleva el código tipeado.
/// </summary>
public sealed record ResultadoIdentificacionCodigo(Guid? IdentitySessionId, Guid? PersonId, string? NombreVisible, string? ReasonCode)
{
    public bool Abierta => IdentitySessionId is not null;
}

/// <summary>
/// Identificación simulada por código (D3 y D4 de atender-seleccion-y-confirmacion): el código solo abre la sesión,
/// sin huella, porque la biometría es de la segunda ola (D-C24). Todo en una transacción de escritura.
/// </summary>
public sealed class IdentificacionPorCodigo
{
    /// <summary>La misma vigencia que las sesiones de prueba de la demostración.</summary>
    internal static readonly TimeSpan VigenciaSesion = TimeSpan.FromMinutes(2);

    private readonly BaseComedor _baseComedor;
    private readonly TimeProvider _reloj;
    private readonly RegistroBitacora _bitacora = new("Infrastructure.Sqlite.IdentificacionPorCodigo");

    public IdentificacionPorCodigo(BaseComedor baseComedor, TimeProvider reloj)
    {
        _baseComedor = baseComedor;
        _reloj = reloj;
    }

    /// <summary>La marca del registro de una identificación que vino del combo del modo simulación (D7 de simular-identificacion-por-lista).</summary>
    public const string OrigenListaSimulacion = "SIMULATION_LIST";

    private static readonly System.Globalization.CultureInfo CulturaDelPuesto = System.Globalization.CultureInfo.GetCultureInfo("es-AR");

    public ResultadoIdentificacionCodigo Identificar(string codigo, PropositoSesion proposito) =>
        Identificar((conexion, transaccion, puesto) => Resolver(conexion, transaccion, puesto, codigo), proposito, origen: null);

    /// <summary>
    /// La credencial elegida en el combo del modo simulación (D2 de simular-identificacion-por-lista): la misma clasificación
    /// (RB25), la misma sesión con método CODE y el mismo registro que el código tipeado, más la clave Origin. Una credencial
    /// que no está en el snapshot activo se rechaza como un código desconocido.
    /// </summary>
    public ResultadoIdentificacionCodigo IdentificarPorCredencial(Guid credencialId, PropositoSesion proposito) =>
        Identificar((conexion, transaccion, puesto) => ResolverCredencial(conexion, transaccion, puesto, credencialId), proposito,
            OrigenListaSimulacion);

    /// <summary>
    /// Si lo tipeado es un código de identificación del snapshot activo, vigente o no, normalizado como al identificar (RB01:
    /// sólo Trim(), con los ceros iniciales), sin abrir sesión ni registrar nada (D5 de entrar-a-configuracion-con-teclado).
    /// El diálogo de la clave lo usa para tratar un código como el de un operador y nunca como clave del día.
    /// </summary>
    public bool EsCodigoConocido(string codigo)
    {
        using var conexion = _baseComedor.AbrirConexion();
        var puesto = PuestoLocal.Leer(conexion, null);
        using var comando = ComandosSqlite.Crear(conexion, null,
            """
            SELECT 1 FROM tbComedorCredencialesPersonas
            WHERE comvcCodigo = $vc AND comcreTipo = 'SERIAL_CODE' AND comcreValorNormalizado = $valor
            """,
            ("$vc", puesto.SnapshotId), ("$valor", codigo.Trim()));
        return comando.ExecuteScalar() is not null;
    }

    /// <summary>
    /// Una entrada por credencial de código del snapshot activo, vigente o no, con el nombre visible de su persona y en orden
    /// alfabético es-AR (D3 de simular-identificacion-por-lista): el rechazo lo decide la identificación, no la lista.
    /// </summary>
    public IReadOnlyList<EntradaSimulacion> ListarParaSimulacion()
    {
        using var conexion = _baseComedor.AbrirConexion();
        var puesto = PuestoLocal.Leer(conexion, null);
        using var comando = ComandosSqlite.Crear(conexion, null,
            """
            SELECT cre.comcreCodigo, per.comperNombreVisible
            FROM tbComedorCredencialesPersonas cre
            JOIN tbComedorPersonas per ON per.comvcCodigo = cre.comvcCodigo AND per.comperCodigo = cre.comperCodigo
            WHERE cre.comvcCodigo = $vc AND cre.comcreTipo = 'SERIAL_CODE'
            """,
            ("$vc", puesto.SnapshotId));
        using var lector = comando.ExecuteReader();
        var entradas = new List<EntradaSimulacion>();
        while (lector.Read())
        {
            entradas.Add(new EntradaSimulacion(lector.GetGuid(0), lector.GetString(1)));
        }

        var comparador = StringComparer.Create(CulturaDelPuesto, ignoreCase: true);
        return entradas.OrderBy(e => e.NombreVisible, comparador).ThenBy(e => e.CredencialId).ToList();
    }

    private ResultadoIdentificacionCodigo Identificar(
        Func<SqliteConnection, SqliteTransaction, PuestoLocal, CredencialResuelta?> resolver, PropositoSesion proposito, string? origen)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var ahora = _reloj.GetUtcNow();
        var puesto = PuestoLocal.Leer(conexion, transaccion);

        var credencial = resolver(conexion, transaccion, puesto);
        var clasificacion = ClasificadorCredencial.Clasificar(credencial?.Credencial, ahora);

        ResultadoIdentificacionCodigo resultado;
        if (clasificacion.Vigente)
        {
            var sesion = AbrirSesion(conexion, transaccion, puesto, credencial!, proposito, ahora);
            // La identificación aceptada, en la transacción que abre la sesión y sin el código (D8 de escribir-bitacoras-externas).
            _bitacora.Insertar(conexion, transaccion, puesto, ahora, "SESSION_OPENED",
                ContextoSinNulos(
                    ("IdentitySessionId", sesion),
                    ("Purpose", CodigoDe(proposito)),
                    ("PersonId", credencial!.PersonaId),
                    ("Method", "CODE"),
                    ("Origin", origen)));
            resultado = new(sesion, credencial.PersonaId, credencial.NombreVisible, null);
        }
        else
        {
            var motivo = CodigoDe(clasificacion.Motivo!.Value);
            _bitacora.Insertar(conexion, transaccion, puesto, ahora, "IDENTIFICATION_REJECTED",
                ContextoSinNulos(
                    ("Purpose", CodigoDe(proposito)),
                    ("ReasonCode", motivo),
                    ("InternalCause", clasificacion.Causa is { } causa ? CodigoDe(causa) : null),
                    ("PersonId", credencial?.PersonaId),
                    ("Origin", origen)));
            resultado = new(null, null, null, motivo);
        }

        transaccion.Commit();
        return resultado;
    }

    /// <summary>
    /// Busca la credencial de código por su GUID en el snapshot activo. El GUID se compara sin distinguir mayúsculas: la
    /// semilla lo escribe en minúsculas y el enlace de parámetros de Microsoft.Data.Sqlite, en mayúsculas.
    /// </summary>
    private static CredencialResuelta? ResolverCredencial(SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, Guid credencialId)
    {
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            """
            SELECT cre.comcreCodigo, cre.comperCodigo, per.comperNombreVisible,
                   cre.comcreFechaVigenciaDesdeUtc, cre.comcreFechaVigenciaHastaUtc, cre.comcreFechaRevocacionUtc
            FROM tbComedorCredencialesPersonas cre
            JOIN tbComedorPersonas per ON per.comvcCodigo = cre.comvcCodigo AND per.comperCodigo = cre.comperCodigo
            WHERE cre.comvcCodigo = $vc AND cre.comcreTipo = 'SERIAL_CODE' AND cre.comcreCodigo = $cre COLLATE NOCASE
            """,
            ("$vc", puesto.SnapshotId), ("$cre", credencialId.ToString("D")));
        return LeerCredencial(comando);
    }

    /// <summary>Normaliza sólo con Trim(), conservando los ceros iniciales (RB01), y busca en el snapshot activo.</summary>
    internal static CredencialResuelta? Resolver(SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, string codigo)
    {
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            """
            SELECT cre.comcreCodigo, cre.comperCodigo, per.comperNombreVisible,
                   cre.comcreFechaVigenciaDesdeUtc, cre.comcreFechaVigenciaHastaUtc, cre.comcreFechaRevocacionUtc
            FROM tbComedorCredencialesPersonas cre
            JOIN tbComedorPersonas per ON per.comvcCodigo = cre.comvcCodigo AND per.comperCodigo = cre.comperCodigo
            WHERE cre.comvcCodigo = $vc AND cre.comcreTipo = 'SERIAL_CODE' AND cre.comcreValorNormalizado = $valor
            """,
            ("$vc", puesto.SnapshotId), ("$valor", codigo.Trim()));
        return LeerCredencial(comando);
    }

    private static CredencialResuelta? LeerCredencial(SqliteCommand comando)
    {
        using var lector = comando.ExecuteReader();
        if (!lector.Read())
        {
            return null;
        }

        return new CredencialResuelta(
            lector.GetGuid(0), lector.GetGuid(1), lector.GetString(2),
            new CredencialIdentificacion(InstanteOpcional(lector, 3), InstanteOpcional(lector, 4), InstanteOpcional(lector, 5)));
    }

    internal static Guid AbrirSesion(
        SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, CredencialResuelta credencial,
        PropositoSesion proposito, DateTimeOffset ahora)
    {
        var sesion = Guid.NewGuid();
        ComandosSqlite.Ejecutar(conexion, transaccion,
            """
            INSERT INTO tbComedorSesionesIdentidad (comsidCodigo, comvcCodigo, comperCodigo, comcreCodigo, comsidMetodo,
                comsidProposito, comsidFechaEmisionUtc, comsidFechaVencimientoUtc, comicCodigoConsumidora)
            VALUES ($sid, $vc, $per, $cre, 'CODE', $proposito, $emitida, $vence, NULL)
            """,
            ("$sid", sesion), ("$vc", puesto.SnapshotId), ("$per", credencial.PersonaId), ("$cre", credencial.CredencialId),
            ("$proposito", CodigoDe(proposito)), ("$emitida", FormatoInstante.Texto(ahora)),
            ("$vence", FormatoInstante.Texto(ahora + VigenciaSesion)));
        return sesion;
    }

    /// <summary>Un contexto de bitácora sin las claves que no existen (RB26: «sólo si existen»).</summary>
    internal static Dictionary<string, object> ContextoSinNulos(params (string Clave, object? Valor)[] pares) =>
        pares.Where(p => p.Valor is not null).ToDictionary(p => p.Clave, p => p.Valor!);

    internal static string CodigoDe(PropositoSesion proposito) => proposito switch
    {
        PropositoSesion.Consumption => "CONSUMPTION",
        PropositoSesion.PrivilegedAction => "PRIVILEGED_ACTION",
        _ => throw new ArgumentOutOfRangeException(nameof(proposito), proposito, null),
    };

    internal static string CodigoDe(MotivoIdentificacion motivo) => motivo switch
    {
        MotivoIdentificacion.IdentityNotResolved => "IDENTITY_NOT_RESOLVED",
        MotivoIdentificacion.CredentialRevoked => "CREDENTIAL_REVOKED",
        MotivoIdentificacion.CredentialExpired => "CREDENTIAL_EXPIRED",
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, null),
    };

    private static string CodigoDe(CausaIdentificacion causa) => causa switch
    {
        CausaIdentificacion.UnknownCode => "UNKNOWN_CODE",
        CausaIdentificacion.NotYetValid => "NOT_YET_VALID",
        _ => throw new ArgumentOutOfRangeException(nameof(causa), causa, null),
    };

    internal static string? CausaDe(ResultadoIdentificacion clasificacion) =>
        clasificacion.Causa is { } causa ? CodigoDe(causa) : null;

    private static DateTimeOffset? InstanteOpcional(SqliteDataReader lector, int columna) =>
        lector.IsDBNull(columna) ? null : FormatoInstante.Leer(lector.GetString(columna));
}

/// <summary>
/// Una entrada del combo del modo simulación (D1 y D3 de simular-identificacion-por-lista): la credencial y el nombre visible
/// de su persona, nunca el código.
/// </summary>
public sealed record EntradaSimulacion(Guid CredencialId, string NombreVisible);

/// <summary>La credencial que resolvió un código, con su persona y su nombre visible.</summary>
internal sealed record CredencialResuelta(Guid CredencialId, Guid PersonaId, string NombreVisible, CredencialIdentificacion Credencial);
