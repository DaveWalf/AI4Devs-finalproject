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

    public ResultadoIdentificacionCodigo Identificar(string codigo, PropositoSesion proposito)
    {
        using var conexion = _baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        var ahora = _reloj.GetUtcNow();
        var puesto = PuestoLocal.Leer(conexion, transaccion);

        var credencial = Resolver(conexion, transaccion, puesto, codigo);
        var clasificacion = ClasificadorCredencial.Clasificar(credencial?.Credencial, ahora);

        ResultadoIdentificacionCodigo resultado;
        if (clasificacion.Vigente)
        {
            var sesion = AbrirSesion(conexion, transaccion, puesto, credencial!, proposito, ahora);
            resultado = new(sesion, credencial!.PersonaId, credencial.NombreVisible, null);
        }
        else
        {
            var motivo = CodigoDe(clasificacion.Motivo!.Value);
            _bitacora.Insertar(conexion, transaccion, puesto, ahora, "IDENTIFICATION_REJECTED",
                ContextoSinNulos(
                    ("Purpose", CodigoDe(proposito)),
                    ("ReasonCode", motivo),
                    ("InternalCause", clasificacion.Causa is { } causa ? CodigoDe(causa) : null),
                    ("PersonId", credencial?.PersonaId)));
            resultado = new(null, null, null, motivo);
        }

        transaccion.Commit();
        return resultado;
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

/// <summary>La credencial que resolvió un código, con su persona y su nombre visible.</summary>
internal sealed record CredencialResuelta(Guid CredencialId, Guid PersonaId, string NombreVisible, CredencialIdentificacion Credencial);
