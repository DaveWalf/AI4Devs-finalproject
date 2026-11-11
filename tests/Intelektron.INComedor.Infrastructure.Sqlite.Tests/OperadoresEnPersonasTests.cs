using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Dar y quitar el permiso OPERATOR desde Personas (D1 y D2 de marcar-operadores-en-personas): marcar da el permiso desde el
/// guardado y sin fin, sobre la fila que la versión nueva ya copió o con una nueva; quitar lo revoca en la versión nueva sin
/// borrar la fila; un operador no cambia el suyo. Sobre una carpeta temporal real con el fixture sintético, al que la prueba
/// le suma un operador vigente y uno vencido, como 9001 y 9002 de la demostración.
/// </summary>
public class OperadoresEnPersonasTests : IDisposable
{
    private static readonly DateTimeOffset Ahora = DateTimeOffset.Parse("2026-11-06T15:00:00Z");

    private readonly Guid _vigente;
    private readonly Guid _vencido;

    public OperadoresEnPersonasTests()
    {
        Directory.CreateDirectory(Carpeta);
        Base = BaseComedor.Abrir(Path.Combine(Carpeta, "incomedor.db"));
        Fixture = FixtureSintetico.Cargar(Base);
        _vigente = InsertarOperador("Operador sintético 01", hasta: null);
        _vencido = InsertarOperador("Operador sintético 02", hasta: Ahora.AddDays(-20));
    }

    private string Carpeta { get; } = Path.Combine(Path.GetTempPath(), "incomedor-tests", "operadores-" + Guid.NewGuid().ToString("N"));

    private BaseComedor Base { get; }

    private FixtureSintetico Fixture { get; }

    private AdministracionLocal Admin => new(Base, new RelojFijo("2026-11-06T15:00:00Z"));

    public void Dispose()
    {
        if (Directory.Exists(Carpeta))
        {
            Directory.Delete(Carpeta, recursive: true);
        }
    }

    [Fact(DisplayName = "administracion-local · La vista dice quién es operador: el vigente sí, el vencido y la persona habilitada no")]
    public void La_vista_dice_quien_es_operador()
    {
        var vista = Admin.Leer();

        Assert.True(vista.Personas.Single(p => p.Id == _vigente).Operador);
        Assert.False(vista.Personas.Single(p => p.Id == _vencido).Operador);
        Assert.False(vista.Personas.Single(p => p.Id == Fixture.Habilitada.Id).Operador);
    }

    [Fact(DisplayName = "administracion-local · Marcar a una persona sin permiso: la versión nueva tiene su fila OPERATOR, vigente desde el guardado y sin fin")]
    public void Dar_a_una_persona_sin_permiso()
    {
        var resultado = Admin.GuardarPersonas([Editar(Fixture.Habilitada.Id, operador: true)]);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        var fila = Assert.Single(Permisos(VersionActiva(), Fixture.Habilitada.Id));
        Assert.Equal(("OPERATOR", Iso(Ahora), null, null), fila);
        Assert.True(Admin.Leer().Personas.Single(p => p.Id == Fixture.Habilitada.Id).Operador);
    }

    [Fact(DisplayName = "administracion-local · Marcar a un operador vencido: la misma fila de la versión nueva queda vigente desde el guardado y sin fin")]
    public void Dar_a_un_operador_vencido()
    {
        var resultado = Admin.GuardarPersonas([Editar(_vencido, operador: true)]);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        var fila = Assert.Single(Permisos(VersionActiva(), _vencido));
        Assert.Equal(("OPERATOR", Iso(Ahora), null, null), fila);
    }

    [Fact(DisplayName = "administracion-local · Desmarcar a un operador: la versión nueva tiene su permiso revocado en el instante del guardado y la anterior lo conserva como estaba")]
    public void Quitar_a_un_operador()
    {
        var anterior = VersionActiva();

        var resultado = Admin.GuardarPersonas([Editar(_vigente, operador: false)]);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        Assert.Equal(("OPERATOR", null, null, Iso(Ahora)), Assert.Single(Permisos(VersionActiva(), _vigente)));
        Assert.Equal(("OPERATOR", null, null, null), Assert.Single(Permisos(anterior, _vigente)));
        Assert.False(Admin.Leer().Personas.Single(p => p.Id == _vigente).Operador);
    }

    [Fact(DisplayName = "administracion-local · El CONFIGURATION_CHANGED de la persona dice el permiso antes y después, sin código")]
    public void El_cambio_en_la_bitacora()
    {
        Admin.GuardarPersonas([Editar(Fixture.Habilitada.Id, operador: true)]);

        var (texto, cambio) = Assert.Single(Cambios("PERSON"));
        Assert.Equal(Fixture.Habilitada.Id, cambio.GetProperty("PersonId").GetGuid());
        Assert.False(cambio.GetProperty("Before").GetProperty("Operator").GetBoolean());
        Assert.True(cambio.GetProperty("After").GetProperty("Operator").GetBoolean());
        Assert.DoesNotContain(FixtureSintetico.CodigoSintetico, texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "administracion-local · Un operador marca a otro, no a sí mismo: dárselo a otra persona se aplica con él como actor; cambiar el propio se rechaza con OWN_PERMISSION, sin versión nueva y con el rechazo en la bitácora")]
    public void Un_operador_no_cambia_el_suyo()
    {
        var aOtro = Admin.GuardarPersonas([Editar(Fixture.Habilitada.Id, operador: true)], _vigente);
        Assert.Equal(TipoGuardado.Aplicado, aOtro.Tipo);
        var version = VersionActiva();

        var propio = Admin.GuardarPersonas([Editar(_vigente, operador: false)], _vigente);

        Assert.Equal(TipoGuardado.Rechazado, propio.Tipo);
        Assert.Equal("OWN_PERMISSION", propio.Motivo);
        Assert.Contains(propio.Campos, c => c.Campo == "Operador" && c.Codigo == "OWN_PERMISSION");
        Assert.Equal(version, VersionActiva());
        var rechazo = Cambios("PERSON").Select(c => c.Contexto).Single(c => c.GetProperty("Result").GetString() == "REJECTED");
        Assert.Equal("OWN_PERMISSION", rechazo.GetProperty("ReasonCode").GetString());
        Assert.Equal(_vigente, rechazo.GetProperty("ActorPersonId").GetGuid());
        Assert.Equal(("OPERATOR", null, null, null), Assert.Single(Permisos(VersionActiva(), _vigente)));
    }

    [Fact(DisplayName = "administracion-local · Guardar a un operador sin tocar su permiso no lo cambia, aunque el actor sea él")]
    public void Sin_tocar_el_permiso()
    {
        var resultado = Admin.GuardarPersonas([Editar(_vigente, operador: null, nombre: "Operador sintético 01 bis")], _vigente);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        Assert.Equal(("OPERATOR", null, null, null), Assert.Single(Permisos(VersionActiva(), _vigente)));
    }

    [Fact(DisplayName = "administracion-local · Una persona dada de baja no se marca: el guardado la rechaza en «Operador»")]
    public void Una_persona_de_baja_no_se_marca()
    {
        Admin.GuardarPersonas([new EdicionPersona(Fixture.Habilitada.Id, "Persona habilitada", Cupos(Fixture.Habilitada.Id), Quitar: true)]);

        var resultado = Admin.GuardarPersonas([Editar(Fixture.Habilitada.Id, operador: true)]);

        Assert.Equal(TipoGuardado.Rechazado, resultado.Tipo);
        Assert.Contains(resultado.Campos, c => c.Campo == "Operador");
    }

    /// <summary>La edición de una persona tal como está, con el permiso elegido (null, sin tocarlo).</summary>
    private EdicionPersona Editar(Guid persona, bool? operador, string? nombre = null)
    {
        var actual = Admin.Leer().Personas.Single(p => p.Id == persona);
        return new EdicionPersona(persona, nombre ?? actual.Nombre, Cupos(persona), Operador: operador);
    }

    private List<CupoEditado> Cupos(Guid persona) =>
        Admin.Leer().Personas.Single(p => p.Id == persona).Cupos.Select(c => new CupoEditado(c.ServicioId, c.CategoriaId, c.Limite)).ToList();

    private Guid InsertarOperador(string nombre, DateTimeOffset? hasta)
    {
        var persona = Guid.NewGuid();
        using var conexion = Base.AbrirConexion();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, $nombre, 1, 1)",
            ("$vc", Fixture.SnapshotId), ("$per", persona), ("$nombre", nombre));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, NULL, NULL)",
            ("$vc", Fixture.SnapshotId), ("$cre", Guid.NewGuid()), ("$per", persona), ("$valor", "OP-" + persona.ToString("N")[..8]));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPermisosOperadores VALUES ($vc, $pop, $per, 'OPERATOR', NULL, $hasta, NULL)",
            ("$vc", Fixture.SnapshotId), ("$pop", Guid.NewGuid()), ("$per", persona), ("$hasta", hasta is null ? null : Iso(hasta.Value)));
        return persona;
    }

    private string VersionActiva()
    {
        using var conexion = Base.AbrirConexion();
        return Sql.Escalar<string>(conexion, "SELECT comvcCodigoActiva FROM tbComedorConfiguracionPuestos");
    }

    private List<(string Rol, string? Desde, string? Hasta, string? Revocado)> Permisos(string version, Guid persona)
    {
        using var conexion = Base.AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            SELECT compopRol, compopFechaVigenciaDesdeUtc, compopFechaVigenciaHastaUtc, compopFechaRevocacionUtc
            FROM tbComedorPermisosOperadores WHERE comvcCodigo = $vc COLLATE NOCASE AND comperCodigo = $per COLLATE NOCASE
            """;
        comando.Parameters.AddWithValue("$vc", version);
        comando.Parameters.AddWithValue("$per", persona.ToString("D"));
        using var lector = comando.ExecuteReader();
        var filas = new List<(string, string?, string?, string?)>();
        while (lector.Read())
        {
            filas.Add((lector.GetString(0), lector.IsDBNull(1) ? null : lector.GetString(1), lector.IsDBNull(2) ? null : lector.GetString(2),
                lector.IsDBNull(3) ? null : lector.GetString(3)));
        }

        return filas;
    }

    private static string Iso(DateTimeOffset instante) => FormatoInstante.Texto(instante);

    private List<(string Texto, JsonElement Contexto)> Cambios(string ajuste)
    {
        using var conexion = Base.AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = 'CONFIGURATION_CHANGED'";
        using var lector = comando.ExecuteReader();
        var cambios = new List<(string, JsonElement)>();
        while (lector.Read())
        {
            var texto = lector.GetString(0);
            var contexto = JsonDocument.Parse(texto).RootElement.Clone();
            if (contexto.GetProperty("Setting").GetString() == ajuste)
            {
                cambios.Add((texto, contexto));
            }
        }

        return cambios;
    }
}
