using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

public class AperturaBaseTests
{
    private const int SqliteConstraintUnique = 2067;

    private static readonly string[] TablasEsperadas =
    [
        "tbComedorAmbitosCupo",
        "tbComedorBitacora",
        "tbComedorCargosCupoConocidos",
        "tbComedorCategoriasCupo",
        "tbComedorComprobantes",
        "tbComedorConfiguracionPuestos",
        "tbComedorConsumos",
        "tbComedorConsumosConocidos",
        "tbComedorCredencialesPersonas",
        "tbComedorEventosSalida",
        "tbComedorIntencionesConsumo",
        "tbComedorLotesSincronizacion",
        "tbComedorLotesSincronizacion_Eventos",
        "tbComedorOpcionesMenu",
        "tbComedorOpcionesMenu_CategoriasCupo",
        "tbComedorPermisosOperadores",
        "tbComedorPersonas",
        "tbComedorReglasHabilitacion",
        "tbComedorSesionesIdentidad",
        "tbComedorTiposServicio",
        "tbComedorTrabajosImpresion",
        "tbComedorUsosCupo",
        "tbComedorVersionesConfiguracion",
    ];

    [Fact(DisplayName = "TK03.1 · Base vacía: queda en la versión de esquema 3, con claves foráneas activas y las 23 tablas del mapa")]
    public void Base_vacia_queda_en_version_3_con_las_23_tablas()
    {
        using var archivo = new ArchivoTemporal();

        var base_ = BaseComedor.Abrir(archivo.Ruta);

        using var conexion = base_.AbrirConexion();
        Assert.Equal(3L, Escalar(conexion, "PRAGMA user_version"));
        Assert.Equal(1L, Escalar(conexion, "PRAGMA foreign_keys"));
        var tablas = Lista(conexion, "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name");
        Assert.Equal(TablasEsperadas, tablas);
    }

    [Fact(DisplayName = "D6 · Base vacía: el diario queda en WAL y cada conexión usa synchronous FULL")]
    public void Base_vacia_usa_wal_y_synchronous_full()
    {
        using var archivo = new ArchivoTemporal();

        var base_ = BaseComedor.Abrir(archivo.Ruta);

        using var conexion = base_.AbrirConexion();
        Assert.Equal("wal", Escalar(conexion, "PRAGMA journal_mode"));
        Assert.Equal(2L, Escalar(conexion, "PRAGMA synchronous"));
    }

    [Fact(DisplayName = "TK03.1 · Versión de esquema desconocida: la apertura falla y el archivo queda sin cambios")]
    public void Version_desconocida_falla_sin_tocar_el_archivo()
    {
        using var archivo = new ArchivoTemporal();
        CrearBaseConVersion(archivo.Ruta, 99);
        var hashAntes = Hash(archivo.Ruta);

        var excepcion = Assert.Throws<EsquemaDesconocidoException>(() => BaseComedor.Abrir(archivo.Ruta));

        Assert.Equal(99, excepcion.VersionEncontrada);
        Assert.Equal(hashAntes, Hash(archivo.Ruta));
        Assert.False(File.Exists(archivo.Ruta + "-wal"));
    }

    [Fact(DisplayName = "TK03.1 · Unicidad de consumo por intención: la segunda inserción directa falla por UNIQUE")]
    public void Segundo_consumo_con_la_misma_intencion_falla_por_unicidad()
    {
        using var archivo = new ArchivoTemporal();
        var base_ = BaseComedor.Abrir(archivo.Ruta);
        using var conexion = base_.AbrirConexion();
        var intencion = InsertarCadenaMinimaConUnConsumo(conexion);

        var excepcion = Assert.Throws<SqliteException>(() => InsertarConsumo(conexion, Guid.NewGuid(), intencion));

        Assert.Equal(SqliteConstraintUnique, excepcion.SqliteExtendedErrorCode);
    }

    private static Guid InsertarCadenaMinimaConUnConsumo(SqliteConnection conexion)
    {
        var snapshot = Guid.NewGuid();
        var persona = Guid.NewGuid();
        var servicio = Guid.NewGuid();
        var menu = Guid.NewGuid();
        var intencion = Guid.NewGuid();

        Ejecutar(conexion,
            "INSERT INTO tbComedorVersionesConfiguracion VALUES ($vc, 1, 1, 1, 1, 3, zeroblob(32), 'ACTIVE', '2026-10-19T00:00:00Z', '2026-10-19T00:00:00Z')",
            ("$vc", snapshot));
        Ejecutar(conexion,
            "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Persona sintética', 1, 1)",
            ("$vc", snapshot), ("$per", persona));
        Ejecutar(conexion,
            "INSERT INTO tbComedorTiposServicio VALUES ($vc, $srv, 'ALMUERZO')",
            ("$vc", snapshot), ("$srv", servicio));
        Ejecutar(conexion,
            "INSERT INTO tbComedorOpcionesMenu VALUES ($vc, $men, 1, $srv, 'Menú sintético', 'ARS', 2, 150000)",
            ("$vc", snapshot), ("$men", menu), ("$srv", servicio));
        Ejecutar(conexion,
            "INSERT INTO tbComedorIntencionesConsumo VALUES ($ic, NULL, zeroblob(32), 'ConsumptionIntent/1', 'DENIED', '2026-10-19T12:00:00Z', $dec, '2026-10-19T12:00:00Z', NULL)",
            ("$ic", intencion), ("$dec", Guid.NewGuid()));

        InsertarConsumo(conexion, Guid.NewGuid(), intencion, snapshot, persona, menu);
        return intencion;
    }

    private static void InsertarConsumo(SqliteConnection conexion, Guid consumo, Guid intencion,
        Guid? snapshot = null, Guid? persona = null, Guid? menu = null)
    {
        snapshot ??= Guid.Parse(Texto(conexion, "SELECT comvcCodigo FROM tbComedorVersionesConfiguracion"));
        persona ??= Guid.Parse(Texto(conexion, "SELECT comperCodigo FROM tbComedorPersonas"));
        menu ??= Guid.Parse(Texto(conexion, "SELECT commenCodigo FROM tbComedorOpcionesMenu"));

        Ejecutar(conexion,
            "INSERT INTO tbComedorConsumos VALUES ($con, $ic, $vc, $per, $men, NULL, '2026-10-19', 1, 1, 1, 1, 1, '2026-10-19T12:00:00Z', '{}')",
            ("$con", consumo), ("$ic", intencion), ("$vc", snapshot.Value), ("$per", persona.Value), ("$men", menu.Value));
    }

    private static void CrearBaseConVersion(string ruta, int version)
    {
        using var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False");
        conexion.Open();
        Ejecutar(conexion, $"PRAGMA user_version = {version}");
        Ejecutar(conexion, "CREATE TABLE futura (id INTEGER)");
    }

    private static string Hash(string ruta) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ruta)));

    private static void Ejecutar(SqliteConnection conexion, string sql, params (string Nombre, Guid Valor)[] parametros)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor);
        }

        comando.ExecuteNonQuery();
    }

    private static object? Escalar(SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return comando.ExecuteScalar();
    }

    private static string Texto(SqliteConnection conexion, string sql) => (string)Escalar(conexion, sql)!;

    private static List<string> Lista(SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        using var lector = comando.ExecuteReader();
        var resultado = new List<string>();
        while (lector.Read())
        {
            resultado.Add(lector.GetString(0));
        }

        return resultado;
    }
}
