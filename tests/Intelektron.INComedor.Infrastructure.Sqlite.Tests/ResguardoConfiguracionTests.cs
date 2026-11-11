using System.Text.Json;
using System.Text.Json.Nodes;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Leer la versión activa entera y recuperarla en una versión nueva, en esta base o en otra del mismo esquema
/// (configuracion-puesto de resguardar-configuracion, D1 a D3). Sobre bases SQLite reales temporales con el fixture
/// sintético; cada fixture crea sus personas con ids nuevos, y la persona habilitada siempre tiene el código 000123.
/// </summary>
public sealed class ResguardoConfiguracionTests : IDisposable
{
    private static readonly string[] Tablas =
    [
        "tbComedorPersonas", "tbComedorCredencialesPersonas", "tbComedorTiposServicio", "tbComedorAmbitosCupo", "tbComedorCategoriasCupo",
        "tbComedorOpcionesMenu", "tbComedorOpcionesMenu_CategoriasCupo", "tbComedorReglasHabilitacion", "tbComedorPermisosOperadores",
        "tbComedorFranjasOpcionesMenu",
    ];

    private readonly string _carpeta = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "incomedor-tests", "resguardo-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_carpeta, recursive: true);

    [Fact(DisplayName = "configuracion-puesto · Guardar el resguardo: la versión activa sale entera, las diez tablas con sus filas, el número de versión y el esquema")]
    public void Leer_la_configuracion()
    {
        var (_, admin, fixture) = Base("a");

        var leida = admin.LeerConfiguracion();

        Assert.Equal(1, leida.VersionConfiguracion);
        Assert.Equal(AdministracionLocal.VersionEsquema, leida.VersionEsquema);
        var json = JsonNode.Parse(leida.Json)!.AsObject();
        Assert.Equal(Tablas.Order(StringComparer.Ordinal), json.Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.Equal(admin.Leer().Personas.Count, json["tbComedorPersonas"]!.AsArray().Count);
        Assert.Contains(json["tbComedorCredencialesPersonas"]!.AsArray(), f => (string?)f!["comcreValorNormalizado"] == FixtureSintetico.CodigoSintetico);
        Assert.DoesNotContain(leida.Json, "comvcCodigo", StringComparison.Ordinal);
        Assert.Equal(fixture.Opciones.Count, json["tbComedorOpcionesMenu"]!.AsArray().Count);
    }

    [Fact(DisplayName = "configuracion-puesto · Recuperar en otro puesto: la configuración de una base, recuperada en otra del mismo esquema, deja una versión nueva con sus personas y opciones y conserva la anterior")]
    public void Recuperar_en_otra_base()
    {
        var (_, origen, _) = Base("origen");
        var (baseDestino, destino, _) = Base("destino");
        // En el destino, la persona habilitada tiene otro código: el 000123 del origen no es de nadie acá.
        using (var conexion = baseDestino.AbrirConexion())
        {
            Sql.Ejecutar(conexion, "UPDATE tbComedorCredencialesPersonas SET comcreValorNormalizado = '999999' WHERE comcreValorNormalizado = $c", ("$c", FixtureSintetico.CodigoSintetico));
        }

        var resultado = destino.RecuperarVersion(origen.LeerConfiguracion().Json);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        Assert.Equal(2, resultado.VersionActiva);
        var vista = destino.Leer();
        Assert.Equal(origen.Leer().Personas.Select(p => p.Id).Order(), vista.Personas.Select(p => p.Id).Order());
        Assert.Equal(origen.Leer().Opciones.Select(o => o.Id).Order(), vista.Opciones.Select(o => o.Id).Order());
        using var conexionDestino = baseDestino.AbrirConexion();
        Assert.Equal(2L, Sql.Escalar<long>(conexionDestino, "SELECT COUNT(*) FROM tbComedorVersionesConfiguracion"));
    }

    [Fact(DisplayName = "configuracion-puesto · Volver a la configuración de ayer: una opción quitada después del resguardo vuelve con la recuperación, en una versión nueva")]
    public void Volver_a_la_configuracion_de_ayer()
    {
        var (_, admin, _) = Base("ayer");
        var ayer = admin.LeerConfiguracion().Json;
        var quitada = admin.Leer().Opciones[0];
        admin.GuardarOpciones([new EdicionOpcion(quitada.Id, quitada.Nombre, quitada.ServicioId, quitada.ImporteUnidadesMenores,
            quitada.Cargas.Select(c => new CargaEditada(c.CategoriaId, c.Unidades)).ToList(), [], Quitar: true)]);
        Assert.DoesNotContain(admin.Leer().Opciones, o => o.Id == quitada.Id);

        var resultado = admin.RecuperarVersion(ayer);

        Assert.Equal(3, resultado.VersionActiva);
        Assert.Contains(admin.Leer().Opciones, o => o.Id == quitada.Id);
    }

    [Fact(DisplayName = "configuracion-puesto · Un resguardo alterado: sin una tabla, con una columna de más o con un valor que no es texto ni entero se rechaza como INVALID_BACKUP y la versión activa no cambia")]
    public void Un_resguardo_alterado()
    {
        var (_, admin, _) = Base("alterado");
        var json = admin.LeerConfiguracion().Json;

        var sinTabla = JsonNode.Parse(json)!.AsObject();
        sinTabla.Remove("tbComedorFranjasOpcionesMenu");
        var columnaDeMas = JsonNode.Parse(json)!.AsObject();
        columnaDeMas["tbComedorPersonas"]![0]!.AsObject()["otra"] = "x";
        var valorRaro = JsonNode.Parse(json)!.AsObject();
        valorRaro["tbComedorPersonas"]![0]!.AsObject()["comperActivo"] = new JsonArray();

        foreach (var alterado in new[] { sinTabla, columnaDeMas, valorRaro })
        {
            var resultado = admin.RecuperarVersion(alterado.ToJsonString());
            Assert.Equal(TipoGuardado.Rechazado, resultado.Tipo);
            Assert.Equal(CodigosAdministracion.ResguardoInvalido, resultado.Motivo);
        }

        Assert.Equal(TipoGuardado.Rechazado, admin.RecuperarVersion("no es json").Tipo);
        Assert.Equal(1, admin.Leer().VersionConfiguracion);
    }

    [Fact(DisplayName = "configuracion-puesto · Un código que es de otra persona: un resguardo con el 000123 de otra persona de esta base se rechaza como CODE_CONFLICT, sin reasignarlo")]
    public void Un_codigo_de_otra_persona()
    {
        var (_, origen, _) = Base("conflicto-origen");
        var (_, destino, _) = Base("conflicto-destino");

        var resultado = destino.RecuperarVersion(origen.LeerConfiguracion().Json);

        Assert.Equal(TipoGuardado.Rechazado, resultado.Tipo);
        Assert.Equal(CodigosAdministracion.CodigoEnConflicto, resultado.Motivo);
        Assert.Equal(1, destino.Leer().VersionConfiguracion);
    }

    [Fact(DisplayName = "configuracion-puesto · Revisar un resguardo: sin escribir, dice cuántas personas, opciones y permisos trae, o el motivo por el que no se recupera")]
    public void Revisar_sin_escribir()
    {
        var (_, origen, fixture) = Base("revisar-origen");
        var (_, destino, _) = Base("revisar-destino");
        var json = origen.LeerConfiguracion().Json;

        var propia = origen.RevisarConfiguracion(json);
        Assert.Null(propia.Motivo);
        Assert.Equal(origen.Leer().Personas.Count, propia.Personas);
        Assert.Equal(fixture.Opciones.Count, propia.Opciones);

        var ajena = destino.RevisarConfiguracion(json);
        Assert.Equal(CodigosAdministracion.CodigoEnConflicto, ajena.Motivo);
        Assert.Equal(CodigosAdministracion.ResguardoInvalido, destino.RevisarConfiguracion("[]").Motivo);
        Assert.Equal(1, origen.Leer().VersionConfiguracion);
        Assert.Equal(1, destino.Leer().VersionConfiguracion);
    }

    [Fact(DisplayName = "configuracion-puesto · Recuperar un resguardo: con INWeb como dueño de los datos, se deniega sin escribir")]
    public void Con_inweb_se_deniega()
    {
        var (baseComedor, admin, _) = Base("inweb");
        var json = admin.LeerConfiguracion().Json;
        using (var conexion = baseComedor.AbrirConexion())
        {
            Sql.Ejecutar(conexion, "INSERT INTO tbComedorOrigenesVersiones (comvcCodigo, comvoOrigen) SELECT comvcCodigoActiva, 'INWEB' FROM tbComedorConfiguracionPuestos");
        }

        Assert.Equal(TipoGuardado.Denegado, admin.RecuperarVersion(json).Tipo);
        Assert.Equal(1, admin.Leer().VersionConfiguracion);
    }

    private (BaseComedor Base, AdministracionLocal Admin, FixtureSintetico Fixture) Base(string nombre)
    {
        var baseComedor = BaseComedor.Abrir(Path.Combine(_carpeta, nombre + ".db"));
        var fixture = FixtureSintetico.Cargar(baseComedor);
        return (baseComedor, new AdministracionLocal(baseComedor, new RelojFijo("2026-11-06T15:00:00Z")), fixture);
    }
}
