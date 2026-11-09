using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>La migración 0004: franjas de las opciones y origen de cada versión (D2 de administrar-personas-y-opciones).</summary>
public class MigracionFranjasTests
{
    private const int SqliteConstraintCheck = 275;

    private static readonly string[] RecursosHastaLa3 =
    [
        "Migraciones.0001_transaccion_consumo.sql",
        "Migraciones.0002_permisos_operadores.sql",
        "Migraciones.0003_lotes_sincronizacion.sql",
    ];

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
        "tbComedorFranjasOpcionesMenu",
        "tbComedorIntencionesConsumo",
        "tbComedorLotesSincronizacion",
        "tbComedorLotesSincronizacion_Eventos",
        "tbComedorOpcionesMenu",
        "tbComedorOpcionesMenu_CategoriasCupo",
        "tbComedorOrigenesVersiones",
        "tbComedorPermisosOperadores",
        "tbComedorPersonas",
        "tbComedorReglasHabilitacion",
        "tbComedorSesionesIdentidad",
        "tbComedorTiposServicio",
        "tbComedorTrabajosImpresion",
        "tbComedorUsosCupo",
        "tbComedorVersionesConfiguracion",
    ];

    [Fact(DisplayName = "D2 · Base vacía: queda en la versión de esquema 4 con las 25 tablas, las dos nuevas incluidas")]
    public void Base_vacia_queda_en_version_4_con_las_25_tablas()
    {
        using var archivo = new ArchivoTemporal();

        var base_ = BaseComedor.Abrir(archivo.Ruta);

        using var conexion = base_.AbrirConexion();
        Assert.Equal(4L, Sql.Escalar<long>(conexion, "PRAGMA user_version"));
        var tablas = new List<string>();
        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                tablas.Add(lector.GetString(0));
            }
        }

        Assert.Equal(TablasEsperadas, tablas);
    }

    [Fact(DisplayName = "D2 · AT51 · Base en la versión 3 con un consumo autorizado: pasa a la 4, las tablas nuevas vacías, y el consumo y su evento siguen iguales")]
    public void Base_en_version_3_con_un_consumo_pasa_a_la_4()
    {
        using var archivo = new ArchivoTemporal();
        CrearBaseEnVersion3ConUnConsumo(archivo.Ruta);
        var antes = Foto(archivo.Ruta);

        var base_ = BaseComedor.Abrir(archivo.Ruta);

        using var conexion = base_.AbrirConexion();
        Assert.Equal(4L, Sql.Escalar<long>(conexion, "PRAGMA user_version"));
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorFranjasOpcionesMenu"));
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorOrigenesVersiones"));
        Assert.Equal(2, antes.Count);
        Assert.Equal(antes, Foto(archivo.Ruta));
    }

    [Fact(DisplayName = "D2 · D6 · Una franja con inicio igual al fin falla por CHECK")]
    public void Franja_con_inicio_igual_al_fin_falla_por_check()
    {
        using var archivo = new ArchivoTemporal();
        var base_ = BaseComedor.Abrir(archivo.Ruta);
        var fixture = FixtureSintetico.Cargar(base_);
        using var conexion = base_.AbrirConexion();

        var excepcion = Assert.Throws<SqliteException>(() => Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorFranjasOpcionesMenu (comvcCodigo, commenCodigo, comfraCodigo, comfraHoraInicio, comfraHoraFin) VALUES ($vc, $men, $fra, '12:00', '12:00')",
            ("$vc", fixture.SnapshotId), ("$men", fixture.AlmuerzoComun.Id), ("$fra", Guid.NewGuid())));

        Assert.Equal(SqliteConstraintCheck, excepcion.SqliteExtendedErrorCode);
    }

    [Fact(DisplayName = "D2 · D3 · El origen de una versión sólo admite LOCAL o INWEB")]
    public void Origen_desconocido_falla_por_check()
    {
        using var archivo = new ArchivoTemporal();
        var base_ = BaseComedor.Abrir(archivo.Ruta);
        var fixture = FixtureSintetico.Cargar(base_);
        using var conexion = base_.AbrirConexion();

        var excepcion = Assert.Throws<SqliteException>(() => Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorOrigenesVersiones (comvcCodigo, comvoOrigen) VALUES ($vc, 'CENTRAL')",
            ("$vc", fixture.SnapshotId)));

        Assert.Equal(SqliteConstraintCheck, excepcion.SqliteExtendedErrorCode);
    }

    /// <summary>Una base como la dejaba la versión 3, con un consumo autorizado y su evento de outbox en PENDING.</summary>
    private static void CrearBaseEnVersion3ConUnConsumo(string ruta)
    {
        using var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False");
        conexion.Open();
        foreach (var recurso in RecursosHastaLa3)
        {
            using var flujo = typeof(BaseComedor).Assembly.GetManifestResourceStream(recurso)!;
            using var lector = new StreamReader(flujo);
            Sql.Ejecutar(conexion, lector.ReadToEnd());
        }

        Sql.Ejecutar(conexion, "PRAGMA user_version = 3");

        var snapshot = Guid.NewGuid();
        var persona = Guid.NewGuid();
        var servicio = Guid.NewGuid();
        var menu = Guid.NewGuid();
        var sesion = Guid.NewGuid();
        var intencion = Guid.NewGuid();

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
            ("$con", Guid.NewGuid()), ("$ic", intencion), ("$vc", snapshot), ("$per", persona), ("$men", menu));
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorEventosSalida VALUES ($ev, $ic, NULL, NULL, 'CONSUMPTION_RECORDED', 1, '{\"EventType\":\"CONSUMPTION_RECORDED\"}', randomblob(32), 'PENDING')",
            ("$ev", Guid.NewGuid()), ("$ic", intencion));
    }

    private static List<string> Foto(string ruta)
    {
        using var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False");
        conexion.Open();
        var foto = new List<string>();
        foreach (var tabla in new[] { "tbComedorConsumos", "tbComedorEventosSalida" })
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
