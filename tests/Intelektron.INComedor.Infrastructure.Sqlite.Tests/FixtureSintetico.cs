using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

public sealed record PersonaFixture(Guid Id, Guid? CredencialId);

public sealed record OpcionFixture(Guid Id, int Version);

/// <summary>
/// Escenario sintético de docs/pruebas.md (D11): un puesto SINGLE_KIOSK en America/Argentina/Buenos_Aires con
/// corte 00:00, tres personas, ALMUERZO compartido por las variantes común y opcional, REFRIGERIO independiente
/// y quince opciones en ARS con escala 2. Todos los valores son sintéticos.
/// </summary>
public sealed class FixtureSintetico
{
    public const string CodigoSintetico = "000123";
    public const string ZonaHoraria = "America/Argentina/Buenos_Aires";

    public Guid PuestoId { get; } = Guid.NewGuid();
    public Guid SnapshotId { get; } = Guid.NewGuid();
    public Guid ServicioAlmuerzo { get; } = Guid.NewGuid();
    public Guid ServicioRefrigerio { get; } = Guid.NewGuid();
    public Guid Ambito { get; } = Guid.NewGuid();
    public Guid CategoriaAlmuerzo { get; } = Guid.NewGuid();
    public Guid CategoriaRefrigerio { get; } = Guid.NewGuid();

    public PersonaFixture Habilitada { get; private set; } = null!;
    public PersonaFixture Inactiva { get; private set; } = null!;
    public PersonaFixture SinDerecho { get; private set; } = null!;

    public OpcionFixture AlmuerzoComun { get; private set; } = null!;
    public OpcionFixture AlmuerzoOpcional { get; private set; } = null!;
    public OpcionFixture ComboAlmuerzoYRefrigerio { get; private set; } = null!;
    public OpcionFixture Refrigerio { get; private set; } = null!;
    public List<OpcionFixture> Opciones { get; } = [];

    public static FixtureSintetico Cargar(BaseComedor baseComedor)
    {
        var fixture = new FixtureSintetico();
        using var conexion = baseComedor.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);

        fixture.CargarConfiguracion(conexion);
        fixture.CargarPersonas(conexion);
        fixture.CargarMenu(conexion);
        fixture.CargarReglas(conexion);

        transaccion.Commit();
        return fixture;
    }

    /// <summary>Crea una sesión de identidad de consumo para la persona y devuelve su token.</summary>
    public Guid NuevaSesion(BaseComedor baseComedor, PersonaFixture persona, string emitidaUtc, string venceUtc)
    {
        var token = Guid.NewGuid();
        using var conexion = baseComedor.AbrirConexion();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorSesionesIdentidad VALUES ($sid, $vc, $per, $cre, 'CODE', 'CONSUMPTION', $emitida, $vence, NULL)",
            ("$sid", token), ("$vc", SnapshotId), ("$per", persona.Id), ("$cre", persona.CredencialId),
            ("$emitida", emitidaUtc), ("$vence", venceUtc));
        return token;
    }

    private void CargarConfiguracion(SqliteConnection conexion)
    {
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorVersionesConfiguracion VALUES ($vc, 1, 1, 1, 1, 3, zeroblob(32), 'ACTIVE', '2026-10-01T00:00:00.000Z', '2026-10-01T00:00:00.000Z')",
            ("$vc", SnapshotId));
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorConfiguracionPuestos VALUES ($pue, $sitio, $org, $vc, $zona, '00:00', 'SINGLE_KIOSK', 'CONTINUE_LOCAL')",
            ("$pue", PuestoId), ("$sitio", Guid.NewGuid()), ("$org", Guid.NewGuid()), ("$vc", SnapshotId), ("$zona", ZonaHoraria));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorTiposServicio VALUES ($vc, $srv, 'ALMUERZO')",
            ("$vc", SnapshotId), ("$srv", ServicioAlmuerzo));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorTiposServicio VALUES ($vc, $srv, 'REFRIGERIO')",
            ("$vc", SnapshotId), ("$srv", ServicioRefrigerio));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorAmbitosCupo VALUES ($vc, $amb, 'Por día en el puesto')",
            ("$vc", SnapshotId), ("$amb", Ambito));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorCategoriasCupo VALUES ($vc, $cat, 'ALMUERZO')",
            ("$vc", SnapshotId), ("$cat", CategoriaAlmuerzo));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorCategoriasCupo VALUES ($vc, $cat, 'REFRIGERIO')",
            ("$vc", SnapshotId), ("$cat", CategoriaRefrigerio));
    }

    private void CargarPersonas(SqliteConnection conexion)
    {
        var credencial = Guid.NewGuid();
        Habilitada = InsertarPersona(conexion, "Persona habilitada", activa: true, credencial);
        Inactiva = InsertarPersona(conexion, "Persona inactiva", activa: false, credencial: null);
        SinDerecho = InsertarPersona(conexion, "Persona sin derecho", activa: true, credencial: null);
    }

    private PersonaFixture InsertarPersona(SqliteConnection conexion, string nombre, bool activa, Guid? credencial)
    {
        var id = Guid.NewGuid();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, $nombre, $activa, 1)",
            ("$vc", SnapshotId), ("$per", id), ("$nombre", nombre), ("$activa", activa ? 1 : 0));

        if (credencial is not null)
        {
            Sql.Ejecutar(conexion,
                "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, NULL, NULL)",
                ("$vc", SnapshotId), ("$cre", credencial), ("$per", id), ("$valor", CodigoSintetico));
        }

        return new PersonaFixture(id, credencial);
    }

    private void CargarMenu(SqliteConnection conexion)
    {
        for (var i = 1; i <= 4; i++)
        {
            var comun = InsertarOpcion(conexion, $"Almuerzo común {i}", ServicioAlmuerzo, CategoriaAlmuerzo);
            var opcional = InsertarOpcion(conexion, $"Almuerzo opcional {i}", ServicioAlmuerzo, CategoriaAlmuerzo);
            AlmuerzoComun ??= comun;
            AlmuerzoOpcional ??= opcional;
        }

        ComboAlmuerzoYRefrigerio = InsertarOpcion(conexion, "Almuerzo con refrigerio", ServicioAlmuerzo, CategoriaAlmuerzo, CategoriaRefrigerio);

        for (var i = 1; i <= 6; i++)
        {
            var refrigerio = InsertarOpcion(conexion, $"Refrigerio {i}", ServicioRefrigerio, CategoriaRefrigerio);
            Refrigerio ??= refrigerio;
        }
    }

    private OpcionFixture InsertarOpcion(SqliteConnection conexion, string nombre, Guid servicio, params Guid[] categorias)
    {
        var id = Guid.NewGuid();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorOpcionesMenu VALUES ($vc, $men, 1, $srv, $nombre, 'ARS', 2, 150000)",
            ("$vc", SnapshotId), ("$men", id), ("$srv", servicio), ("$nombre", nombre));

        foreach (var categoria in categorias)
        {
            Sql.Ejecutar(conexion, "INSERT INTO tbComedorOpcionesMenu_CategoriasCupo VALUES ($vc, $men, $cat, 1)",
                ("$vc", SnapshotId), ("$men", id), ("$cat", categoria));
        }

        var opcion = new OpcionFixture(id, 1);
        Opciones.Add(opcion);
        return opcion;
    }

    /// <summary>
    /// La persona habilitada tiene una regla de límite 1 por servicio y categoría. La tercera regla permite que
    /// la opción combinada, que es de ALMUERZO, también cargue la categoría REFRIGERIO.
    /// </summary>
    private void CargarReglas(SqliteConnection conexion)
    {
        InsertarRegla(conexion, ServicioAlmuerzo, CategoriaAlmuerzo);
        InsertarRegla(conexion, ServicioRefrigerio, CategoriaRefrigerio);
        InsertarRegla(conexion, ServicioAlmuerzo, CategoriaRefrigerio);
    }

    private void InsertarRegla(SqliteConnection conexion, Guid servicio, Guid categoria)
    {
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorReglasHabilitacion VALUES ($vc, $reg, $per, NULL, $srv, $amb, $cat, 1, 1, NULL, NULL)",
            ("$vc", SnapshotId), ("$reg", Guid.NewGuid()), ("$per", Habilitada.Id),
            ("$srv", servicio), ("$amb", Ambito), ("$cat", categoria));
    }
}
