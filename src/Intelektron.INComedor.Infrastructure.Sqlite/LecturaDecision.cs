using System.Globalization;
using Intelektron.INComedor.Domain;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>
/// Las lecturas de la decisión de disponibilidad (RB02 y RB03), compartidas por la confirmación y por la oferta por persona
/// (D2 de ofrecer-solo-opciones-disponibles). Reciben la conexión y la transacción de quien las pide.
/// </summary>
internal static class LecturaDecision
{
    internal static OpcionMenu? LeerOpcion(SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, Guid menuItemId)
    {
        OpcionMenuLeida? leida;
        using (var comando = ComandosSqlite.Crear(conexion, transaccion,
                   """
                   SELECT commenVersionMenu, comsrvCodigo, commenNombre, commenMoneda, commenEscala, commenImporteEmpleadoUnidadesMenores
                   FROM tbComedorOpcionesMenu WHERE comvcCodigo = $vc AND commenCodigo = $men
                   """,
                   ("$vc", puesto.SnapshotId), ("$men", menuItemId)))
        using (var lector = comando.ExecuteReader())
        {
            leida = lector.Read()
                ? new OpcionMenuLeida(lector.GetInt32(0), lector.GetGuid(1), lector.GetString(2), lector.GetString(3), lector.GetInt32(4), lector.GetInt64(5))
                : null;
        }

        if (leida is null)
        {
            return null;
        }

        var cargas = new List<CargaMenu>();
        using (var comando = ComandosSqlite.Crear(conexion, transaccion,
                   """
                   SELECT comcatCodigo, commccUnidades FROM tbComedorOpcionesMenu_CategoriasCupo
                   WHERE comvcCodigo = $vc AND commenCodigo = $men ORDER BY comcatCodigo
                   """,
                   ("$vc", puesto.SnapshotId), ("$men", menuItemId)))
        using (var lector = comando.ExecuteReader())
        {
            while (lector.Read())
            {
                cargas.Add(new CargaMenu(lector.GetGuid(0), lector.GetInt32(1)));
            }
        }

        if (cargas.Count == 0)
        {
            throw new InvalidOperationException($"La opción de menú {menuItemId} no tiene cargas de cupo.");
        }

        return new OpcionMenu(leida.Version, leida.ServicioId, leida.Nombre, leida.Moneda, leida.Escala, leida.ImporteEmpleadoUnidadesMenores, cargas);
    }

    internal static bool LeerPersonaActiva(SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, Guid personaId) =>
        ComandosSqlite.EscalarLong(conexion, transaccion,
            "SELECT comperActivo FROM tbComedorPersonas WHERE comvcCodigo = $vc AND comperCodigo = $per",
            ("$vc", puesto.SnapshotId), ("$per", personaId)) == 1;

    /// <summary>La credencial con la que se abrió la sesión; null si la sesión no lleva ninguna (demostración).</summary>
    internal static CredencialSesion? LeerCredencialDeSesion(
        SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, Guid sesionId)
    {
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            """
            SELECT cre.comcreFechaVigenciaHastaUtc, cre.comcreFechaRevocacionUtc
            FROM tbComedorSesionesIdentidad ses
            JOIN tbComedorCredencialesPersonas cre ON cre.comvcCodigo = ses.comvcCodigo AND cre.comcreCodigo = ses.comcreCodigo
            WHERE ses.comsidCodigo = $sid AND cre.comvcCodigo = $vc
            """,
            ("$sid", sesionId), ("$vc", puesto.SnapshotId));
        using var lector = comando.ExecuteReader();
        if (!lector.Read())
        {
            return null;
        }

        return new CredencialSesion(LeerInstanteOpcional(lector, 0), LeerInstanteOpcional(lector, 1));
    }

    /// <summary>Todas las reglas por persona del servicio, sin filtrar vigencia: RB02 se decide en el dominio.</summary>
    internal static List<ReglaCandidata> LeerReglasCandidatas(
        SqliteConnection conexion, SqliteTransaction transaccion, PuestoLocal puesto, Guid personaId, Guid servicioId)
    {
        var reglas = new List<ReglaCandidata>();
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            """
            SELECT comregCodigo, comsrvCodigo, comacCodigo, comcatCodigo, comregLimite, comregPrioridad,
                   comregFechaVigenciaDesdeUtc, comregFechaVigenciaHastaUtc
            FROM tbComedorReglasHabilitacion
            WHERE comvcCodigo = $vc AND comperCodigo = $per AND comsrvCodigo = $srv
            """,
            ("$vc", puesto.SnapshotId), ("$per", personaId), ("$srv", servicioId));
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            reglas.Add(new ReglaCandidata(
                lector.GetGuid(0), lector.GetGuid(1), lector.GetGuid(2), lector.GetGuid(3), lector.GetInt32(4), lector.GetInt32(5),
                LeerInstanteOpcional(lector, 6), LeerInstanteOpcional(lector, 7)));
        }

        return reglas;
    }

    internal static DateTimeOffset? LeerInstanteOpcional(SqliteDataReader lector, int columna) =>
        lector.IsDBNull(columna) ? null : FormatoInstante.Leer(lector.GetString(columna));

    internal static Dictionary<ClaveCupo, int> LeerUsos(
        SqliteConnection conexion, SqliteTransaction transaccion, Guid personaId, FechaNegocio fecha)
    {
        var usos = new Dictionary<ClaveCupo, int>();
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            "SELECT comacCodigo, comcatCodigo, comucUsado FROM tbComedorUsosCupo WHERE comperCodigo = $per AND comucFechaNegocio = $fecha",
            ("$per", personaId), ("$fecha", Texto(fecha)));
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            usos[new ClaveCupo(personaId, lector.GetGuid(0), lector.GetGuid(1), fecha)] = lector.GetInt32(2);
        }

        return usos;
    }

    internal static string Texto(FechaNegocio fecha) => fecha.Valor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static string CodigoDeMotivo(MotivoDenegacion motivo) => motivo switch
    {
        MotivoDenegacion.LocalLimitReached => "LOCAL_LIMIT_REACHED",
        MotivoDenegacion.ServiceNotEnabled => "SERVICE_NOT_ENABLED",
        MotivoDenegacion.PersonInactive => "PERSON_INACTIVE",
        MotivoDenegacion.CredentialRevoked => "CREDENTIAL_REVOKED",
        MotivoDenegacion.CredentialExpired => "CREDENTIAL_EXPIRED",
        MotivoDenegacion.RuleDenied => "RULE_DENIED",
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, null),
    };

    private sealed record OpcionMenuLeida(int Version, Guid ServicioId, string Nombre, string Moneda, int Escala, long ImporteEmpleadoUnidadesMenores);
}

internal sealed record OpcionMenu(
    int Version,
    Guid ServicioId,
    string Nombre,
    string Moneda,
    int Escala,
    long ImporteEmpleadoUnidadesMenores,
    IReadOnlyList<CargaMenu> Cargas);
