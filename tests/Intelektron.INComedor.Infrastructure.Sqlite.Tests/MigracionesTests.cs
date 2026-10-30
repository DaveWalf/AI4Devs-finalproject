using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>La migración 0002 y la lista ordenada de migraciones (D1 de atender-seleccion-y-confirmacion).</summary>
public class MigracionesTests
{
    private const int SqliteConstraintUnique = 2067;
    private const int SqliteConstraintCheck = 275;
    private const string RecursoMigracion0001 = "Migraciones.0001_transaccion_consumo.sql";

    private static readonly string[] ColumnasPermisos =
    [
        "comvcCodigo",
        "compopCodigo",
        "comperCodigo",
        "compopRol",
        "compopFechaVigenciaDesdeUtc",
        "compopFechaVigenciaHastaUtc",
        "compopFechaRevocacionUtc",
    ];

    [Fact(DisplayName = "AT51 · RB06 · Base en la versión 1: queda en la versión 2 con tbComedorPermisosOperadores vacía, y el consumo, su comprobante y su trabajo siguen iguales")]
    public void Base_en_version_1_se_actualiza_sin_perder_el_consumo()
    {
        using var archivo = new ArchivoTemporal();
        CrearBaseEnVersion1ConUnConsumo(archivo.Ruta);
        var antes = FotoDelConsumo(archivo.Ruta);

        var base_ = BaseComedor.Abrir(archivo.Ruta);

        using var conexion = base_.AbrirConexion();
        Assert.Equal(2L, Sql.Escalar<long>(conexion, "PRAGMA user_version"));
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorPermisosOperadores"));
        Assert.Equal(antes, FotoDelConsumo(archivo.Ruta));
        Assert.Equal(3, antes.Count);
    }

    [Fact(DisplayName = "D1 · La migración 0002 crea tbComedorPermisosOperadores con las columnas del mapa (nomenclatura-campos-er.csv:44-50)")]
    public void La_0002_crea_la_tabla_con_las_columnas_del_mapa()
    {
        using var archivo = new ArchivoTemporal();

        var base_ = BaseComedor.Abrir(archivo.Ruta);

        using var conexion = base_.AbrirConexion();
        var columnas = new List<string>();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT name FROM pragma_table_info('tbComedorPermisosOperadores') ORDER BY cid";
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            columnas.Add(lector.GetString(0));
        }

        Assert.Equal(ColumnasPermisos, columnas);
    }

    [Fact(DisplayName = "LocalOperatorGrant · Unicidad de permiso por persona y rol: el segundo OPERATOR de la misma persona en el mismo snapshot falla por UNIQUE")]
    public void Segundo_permiso_del_mismo_rol_falla_por_unicidad()
    {
        using var archivo = new ArchivoTemporal();
        var base_ = BaseComedor.Abrir(archivo.Ruta);
        using var conexion = base_.AbrirConexion();
        var snapshot = Guid.NewGuid();
        var persona = Guid.NewGuid();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorVersionesConfiguracion VALUES ($vc, 1, 1, 1, 1, 3, zeroblob(32), 'ACTIVE', '2026-10-01T00:00:00.000Z', '2026-10-01T00:00:00.000Z')",
            ("$vc", snapshot));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Operador sintético', 1, 1)",
            ("$vc", snapshot), ("$per", persona));
        InsertarPermiso(conexion, snapshot, persona, "OPERATOR");

        var excepcion = Assert.Throws<SqliteException>(() => InsertarPermiso(conexion, snapshot, persona, "OPERATOR"));

        Assert.Equal(SqliteConstraintUnique, excepcion.SqliteExtendedErrorCode);
    }

    [Fact(DisplayName = "D1 · El rol del permiso sólo admite OPERATOR o SYSTEM_ADMINISTRATOR")]
    public void Un_rol_desconocido_falla_por_check()
    {
        using var archivo = new ArchivoTemporal();
        var base_ = BaseComedor.Abrir(archivo.Ruta);
        using var conexion = base_.AbrirConexion();
        var snapshot = Guid.NewGuid();
        var persona = Guid.NewGuid();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorVersionesConfiguracion VALUES ($vc, 1, 1, 1, 1, 3, zeroblob(32), 'ACTIVE', '2026-10-01T00:00:00.000Z', '2026-10-01T00:00:00.000Z')",
            ("$vc", snapshot));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Operador sintético', 1, 1)",
            ("$vc", snapshot), ("$per", persona));

        var excepcion = Assert.Throws<SqliteException>(() => InsertarPermiso(conexion, snapshot, persona, "SUPERVISOR"));

        Assert.Equal(SqliteConstraintCheck, excepcion.SqliteExtendedErrorCode);
    }

    private static void InsertarPermiso(SqliteConnection conexion, Guid snapshot, Guid persona, string rol) =>
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorPermisosOperadores VALUES ($vc, $pop, $per, $rol, NULL, NULL, NULL)",
            ("$vc", snapshot), ("$pop", Guid.NewGuid()), ("$per", persona), ("$rol", rol));

    /// <summary>Arma una base como la dejaba la versión 1: el recurso 0001 del ensamblado y user_version = 1.</summary>
    private static void CrearBaseEnVersion1ConUnConsumo(string ruta)
    {
        using var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False");
        conexion.Open();
        using (var flujo = typeof(BaseComedor).Assembly.GetManifestResourceStream(RecursoMigracion0001)!)
        using (var lector = new StreamReader(flujo))
        {
            Sql.Ejecutar(conexion, lector.ReadToEnd());
        }

        Sql.Ejecutar(conexion, "PRAGMA user_version = 1");

        var snapshot = Guid.NewGuid();
        var persona = Guid.NewGuid();
        var servicio = Guid.NewGuid();
        var menu = Guid.NewGuid();
        var sesion = Guid.NewGuid();
        var intencion = Guid.NewGuid();
        var consumo = Guid.NewGuid();
        var comprobante = Guid.NewGuid();

        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorVersionesConfiguracion VALUES ($vc, 1, 1, 1, 1, 3, zeroblob(32), 'ACTIVE', '2026-10-01T00:00:00.000Z', '2026-10-01T00:00:00.000Z')",
            ("$vc", snapshot));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Persona sintética', 1, 1)",
            ("$vc", snapshot), ("$per", persona));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorTiposServicio VALUES ($vc, $srv, 'ALMUERZO')",
            ("$vc", snapshot), ("$srv", servicio));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorOpcionesMenu VALUES ($vc, $men, 1, $srv, 'Menú sintético', 'ARS', 2, 150000)",
            ("$vc", snapshot), ("$men", menu), ("$srv", servicio));
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorSesionesIdentidad VALUES ($sid, $vc, $per, NULL, 'CODE', 'CONSUMPTION', '2026-10-19T11:59:00.000Z', '2026-10-19T12:01:00.000Z', $ic)",
            ("$sid", sesion), ("$vc", snapshot), ("$per", persona), ("$ic", intencion));
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorIntencionesConsumo VALUES ($ic, $sid, zeroblob(32), 'ConsumptionIntent/1', 'AUTHORIZED', '2026-10-19T12:00:00.000Z', $dec, '2026-10-19T12:00:00.000Z', '{}')",
            ("$ic", intencion), ("$sid", sesion), ("$dec", Guid.NewGuid()));
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorConsumos VALUES ($con, $ic, $vc, $per, $men, NULL, '2026-10-19', 1, 1, 1, 1, 1, '2026-10-19T12:00:00.000Z', '{}')",
            ("$con", consumo), ("$ic", intencion), ("$vc", snapshot), ("$per", persona), ("$men", menu));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorComprobantes VALUES ($tk, $con, '{}', '2026-10-19T12:00:00.000Z')",
            ("$tk", comprobante), ("$con", consumo));
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorTrabajosImpresion VALUES ($job, $tk, 0, 'QUEUED', NULL, NULL, '2026-10-19T12:00:00.000Z')",
            ("$job", Guid.NewGuid()), ("$tk", comprobante));
    }

    /// <summary>Cada fila del consumo, su comprobante y su trabajo de impresión, como texto comparable.</summary>
    private static List<string> FotoDelConsumo(string ruta)
    {
        using var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False");
        conexion.Open();
        var foto = new List<string>();
        foreach (var tabla in new[] { "tbComedorConsumos", "tbComedorComprobantes", "tbComedorTrabajosImpresion" })
        {
            using var comando = conexion.CreateCommand();
            comando.CommandText = $"SELECT * FROM {tabla}";
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                var valores = Enumerable.Range(0, lector.FieldCount).Select(i => lector.IsDBNull(i) ? "NULL" : lector.GetValue(i).ToString());
                foto.Add(tabla + ": " + string.Join(" | ", valores));
            }
        }

        return foto;
    }
}
