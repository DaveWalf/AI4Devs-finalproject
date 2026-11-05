using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Una opción de menú del snapshot activo, con su versión y su cargo informado en unidades menores.</summary>
public sealed record OpcionDeCatalogo(Guid Id, int Version, string Nombre, string Moneda, int Escala, long ImporteUnidadesMenores);

/// <summary>Las opciones del snapshot activo, ordenadas por nombre y después por código (D6 de atender-seleccion-y-confirmacion).</summary>
public sealed class ConsultaCatalogo
{
    private static readonly StringComparer OrdenPorNombre = StringComparer.Create(CultureInfo.GetCultureInfo("es-AR"), ignoreCase: true);

    private readonly BaseComedor _baseComedor;

    public ConsultaCatalogo(BaseComedor baseComedor)
    {
        _baseComedor = baseComedor;
    }

    public IReadOnlyList<OpcionDeCatalogo> Leer()
    {
        using var conexion = _baseComedor.AbrirConexion();
        var puesto = PuestoLocal.Leer(conexion, null);
        return Leer(conexion, null, puesto);
    }

    /// <summary>El mismo orden, dentro de la conexión y la transacción de quien lo pide: lo usa también la oferta por persona.</summary>
    internal static IReadOnlyList<OpcionDeCatalogo> Leer(SqliteConnection conexion, SqliteTransaction? transaccion, PuestoLocal puesto)
    {
        using var comando = ComandosSqlite.Crear(conexion, transaccion,
            """
            SELECT commenCodigo, commenVersionMenu, commenNombre, commenMoneda, commenEscala, commenImporteEmpleadoUnidadesMenores
            FROM tbComedorOpcionesMenu WHERE comvcCodigo = $vc
            """,
            ("$vc", puesto.SnapshotId));
        using var lector = comando.ExecuteReader();
        var opciones = new List<OpcionDeCatalogo>();
        while (lector.Read())
        {
            opciones.Add(new OpcionDeCatalogo(
                lector.GetGuid(0), lector.GetInt32(1), lector.GetString(2), lector.GetString(3), lector.GetInt32(4), lector.GetInt64(5)));
        }

        return opciones
            .OrderBy(o => o.Nombre, OrdenPorNombre)
            .ThenBy(o => o.Id.ToString("D", CultureInfo.InvariantCulture), StringComparer.Ordinal)
            .ToList();
    }
}
