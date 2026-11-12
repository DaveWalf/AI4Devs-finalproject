using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>La migración 0003: lotes de sincronización con su cuerpo (D9 de recibir-lotes-idempotente).</summary>
public class MigracionLotesTests
{
    private const int SqliteConstraintCheck = 275;
    private const int SqliteConstraintForeignKey = 787;
    private const int SqliteConstraintPrimaryKey = 1555;

    private static readonly string[] RecursosPorVersion =
    [
        "Migraciones.0001_transaccion_consumo.sql",
        "Migraciones.0002_permisos_operadores.sql",
    ];

    private static readonly string[] ColumnasLotes =
    [
        "comlotCodigo",
        "comlotContenidoLote",
        "comlotHashContenidoLote",
        "comlotEstado",
        "comlotContenidoAcuse",
    ];

    private static readonly string[] ColumnasEventosDelLote = ["comlotCodigo", "comevCodigo"];

    [Fact(DisplayName = "AT51 · AT27 · Base en la versión 2 con eventos pendientes: queda en la versión 3 con las tablas de lotes vacías, y el consumo y su evento siguen iguales, en PENDING")]
    public void Base_en_version_2_con_un_evento_pendiente_queda_en_version_3()
    {
        using var archivo = new ArchivoTemporal();
        CrearBaseConUnConsumoYSuEvento(archivo.Ruta, version: 2);
        var antes = Foto(archivo.Ruta);

        var base_ = BaseComedor.Abrir(archivo.Ruta);

        using var conexion = base_.AbrirConexion();
        Assert.Equal(3L, Sql.Escalar<long>(conexion, "PRAGMA user_version"));
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorLotesSincronizacion"));
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorLotesSincronizacion_Eventos"));
        Assert.Equal(antes, Foto(archivo.Ruta));
        Assert.Equal(4, antes.Count);
        Assert.Equal("PENDING", Sql.Escalar<string>(conexion, "SELECT comevsEstado FROM tbComedorEventosSalida"));
    }

    [Fact(DisplayName = "AT51 · RB06 · Base en la versión 1: queda en la versión 3 con tbComedorPermisosOperadores y las tablas de lotes vacías, y el consumo y su evento siguen iguales")]
    public void Base_en_version_1_queda_en_version_3_con_las_tablas_de_lotes_vacias()
    {
        using var archivo = new ArchivoTemporal();
        CrearBaseConUnConsumoYSuEvento(archivo.Ruta, version: 1);
        var antes = Foto(archivo.Ruta);

        var base_ = BaseComedor.Abrir(archivo.Ruta);

        using var conexion = base_.AbrirConexion();
        Assert.Equal(3L, Sql.Escalar<long>(conexion, "PRAGMA user_version"));
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorPermisosOperadores"));
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorLotesSincronizacion"));
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorLotesSincronizacion_Eventos"));
        Assert.Equal(antes, Foto(archivo.Ruta));
    }

    [Fact(DisplayName = "D9 · La 0003 crea tbComedorLotesSincronizacion con las columnas del mapa (nomenclatura-campos-er.csv, L_SyncBatch) y comlotContenidoLote")]
    public void La_0003_crea_los_lotes_con_las_columnas_del_mapa()
    {
        using var archivo = new ArchivoTemporal();

        var base_ = BaseComedor.Abrir(archivo.Ruta);

        using var conexion = base_.AbrirConexion();
        Assert.Equal(ColumnasLotes, Columnas(conexion, "tbComedorLotesSincronizacion"));
    }

    [Fact(DisplayName = "D9 · La 0003 crea tbComedorLotesSincronizacion_Eventos con las columnas del mapa (nomenclatura-campos-er.csv, L_SyncBatchItem)")]
    public void La_0003_crea_los_eventos_del_lote_con_las_columnas_del_mapa()
    {
        using var archivo = new ArchivoTemporal();

        var base_ = BaseComedor.Abrir(archivo.Ruta);

        using var conexion = base_.AbrirConexion();
        Assert.Equal(ColumnasEventosDelLote, Columnas(conexion, "tbComedorLotesSincronizacion_Eventos"));
    }

    [Fact(DisplayName = "SyncBatchItem · Un evento no se repite dentro de un lote: la segunda inserción del mismo evento en ese lote falla por la clave primaria")]
    public void El_mismo_evento_dos_veces_en_un_lote_falla_por_clave_primaria()
    {
        using var archivo = new ArchivoTemporal();
        var base_ = BaseComedor.Abrir(archivo.Ruta);
        using var conexion = base_.AbrirConexion();
        var evento = InsertarEventoPendiente(conexion);
        var lote = InsertarLote(conexion, "PENDING");
        InsertarEventoDelLote(conexion, lote, evento);

        var excepcion = Assert.Throws<SqliteException>(() => InsertarEventoDelLote(conexion, lote, evento));

        Assert.Equal(SqliteConstraintPrimaryKey, excepcion.SqliteExtendedErrorCode);
    }

    [Fact(DisplayName = "SyncBatchItem · Un evento que no existe en el outbox no entra en un lote: la inserción falla por la clave foránea")]
    public void Un_evento_inexistente_en_un_lote_falla_por_clave_foranea()
    {
        using var archivo = new ArchivoTemporal();
        var base_ = BaseComedor.Abrir(archivo.Ruta);
        using var conexion = base_.AbrirConexion();
        var lote = InsertarLote(conexion, "PENDING");

        var excepcion = Assert.Throws<SqliteException>(() => InsertarEventoDelLote(conexion, lote, Guid.NewGuid()));

        Assert.Equal(SqliteConstraintForeignKey, excepcion.SqliteExtendedErrorCode);
    }

    [Fact(DisplayName = "D9 · El estado del lote sólo admite PENDING o COMPLETED")]
    public void Un_estado_de_lote_desconocido_falla_por_check()
    {
        using var archivo = new ArchivoTemporal();
        var base_ = BaseComedor.Abrir(archivo.Ruta);
        using var conexion = base_.AbrirConexion();

        var excepcion = Assert.Throws<SqliteException>(() => InsertarLote(conexion, "SENT"));

        Assert.Equal(SqliteConstraintCheck, excepcion.SqliteExtendedErrorCode);
    }

    private static Guid InsertarEventoPendiente(SqliteConnection conexion)
    {
        var evento = Guid.NewGuid();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorEventosSalida VALUES ($ev, NULL, NULL, NULL, 'CONSUMPTION_DENIED', 1, '{}', zeroblob(32), 'PENDING')",
            ("$ev", evento));
        return evento;
    }

    private static Guid InsertarLote(SqliteConnection conexion, string estado)
    {
        var lote = Guid.NewGuid();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorLotesSincronizacion VALUES ($lot, '{}', zeroblob(32), $estado, NULL)",
            ("$lot", lote), ("$estado", estado));
        return lote;
    }

    private static void InsertarEventoDelLote(SqliteConnection conexion, Guid lote, Guid evento) =>
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorLotesSincronizacion_Eventos VALUES ($lot, $ev)",
            ("$lot", lote), ("$ev", evento));

    private static List<string> Columnas(SqliteConnection conexion, string tabla)
    {
        var columnas = new List<string>();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT name FROM pragma_table_info($tabla) ORDER BY cid";
        comando.Parameters.AddWithValue("$tabla", tabla);
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            columnas.Add(lector.GetString(0));
        }

        return columnas;
    }

    /// <summary>
    /// Arma una base como la dejaba esa versión: los recursos de migración del ensamblado hasta ella y su
    /// user_version, con un consumo autorizado y su evento de outbox en PENDING.
    /// </summary>
    private static void CrearBaseConUnConsumoYSuEvento(string ruta, int version)
    {
        using var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False");
        conexion.Open();
        foreach (var recurso in RecursosPorVersion.Take(version))
        {
            using var flujo = typeof(BaseComedor).Assembly.GetManifestResourceStream(recurso)!;
            using var lector = new StreamReader(flujo);
            Sql.Ejecutar(conexion, lector.ReadToEnd());
        }

        Sql.Ejecutar(conexion, $"PRAGMA user_version = {version}");

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
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorEventosSalida VALUES ($ev, $ic, NULL, NULL, 'CONSUMPTION_RECORDED', 1, '{\"EventType\":\"CONSUMPTION_RECORDED\"}', randomblob(32), 'PENDING')",
            ("$ev", Guid.NewGuid()), ("$ic", intencion));
    }

    /// <summary>Cada fila del consumo, su comprobante, su trabajo y su evento, como texto comparable.</summary>
    private static List<string> Foto(string ruta)
    {
        using var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False");
        conexion.Open();
        var foto = new List<string>();
        foreach (var tabla in new[] { "tbComedorConsumos", "tbComedorComprobantes", "tbComedorTrabajosImpresion", "tbComedorEventosSalida" })
        {
            using var comando = conexion.CreateCommand();
            comando.CommandText = $"SELECT * FROM {tabla}";
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                var valores = Enumerable.Range(0, lector.FieldCount).Select(i => lector.IsDBNull(i)
                    ? "NULL"
                    : lector.GetValue(i) is byte[] bytes ? Convert.ToHexString(bytes) : lector.GetValue(i).ToString());
                foto.Add(tabla + ": " + string.Join(" | ", valores));
            }
        }

        return foto;
    }
}
