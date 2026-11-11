using System.Text.Json;
using System.Text.RegularExpressions;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Lo que la verificación de ofrecer-solo-opciones-disponibles (04/11/2026) dejó sin prueba (cubrir-pruebas-de-las-verificaciones,
/// grupo 1): la rama de CatalogoCambiado que termina sin opciones y el OPTIONS_OFFERED que se registra otra vez al recalcular
/// la oferta. La máquina de atención sobre SQLite real con el catálogo de nueve, como OfertaPorPersonaTests.
/// </summary>
public class CoberturaDeLaOfertaTests : IDisposable
{
    private const string FechaDeNegocio = "2026-10-30";
    private static readonly Guid Ambito = Guid.Parse("00000000-0000-4000-8000-000000000006");
    private static readonly Guid CategoriaAlmuerzo = Guid.Parse("00000000-0000-4000-8000-000000000007");
    private static readonly Guid CategoriaRefrigerio = Guid.Parse("00000000-0000-4000-8000-000000000026");
    private static readonly Regex Uuid = new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
    private static readonly string[] Refrigerios = ["Gaseosa solamente", "Merienda", "Refrigerio", "Refrigerio opcional", "Yogurt"];

    private readonly ArchivoTemporal _archivo = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose() => _archivo.Dispose();

    [Fact(DisplayName = "atencion-puesto · La opción cambió y ya no queda ninguna: con todo el cupo del día usado entre la elección y la confirmación, la atención queda sin opciones, sin elegida ni consumo, con «Hoy ya usaste: almuerzo 1 de 1 · refrigerio 2 de 2»")]
    public async Task El_catalogo_cambiado_sin_ninguna_opcion()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        var elegida = IdDe(maquina, "Almuerzo con gaseosa");
        maquina.Elegir(elegida);
        CambiarVersion(elegida, 2);
        GastarCupo("0001", CategoriaAlmuerzo, 1);
        GastarCupo("0001", CategoriaRefrigerio, 2);

        await maquina.ConfirmarAsync();

        Assert.Equal(EstadoAtencion.SinOpciones, maquina.Estado);
        Assert.Null(maquina.Elegida);
        Assert.Null(maquina.Aviso);
        Assert.Empty(maquina.PaginaActual);
        Assert.Equal("Hoy ya usaste: almuerzo 1 de 1 · refrigerio 2 de 2", maquina.UsoDelDia);
        Assert.Equal(0L, Contar("tbComedorConsumos"));
        Assert.Equal(0L, Contar("tbComedorIntencionesConsumo"));
        maquina.Listo();
        Assert.Equal(EstadoAtencion.Reposo, maquina.Estado);
        Assert.Equal(nameof(MensajesAtencion.SinOpciones), Registros("SESSION_CLOSED")[^1].GetProperty("MessageCode").GetString());
    }

    [Fact(DisplayName = "atencion-puesto · La oferta recalculada se registra otra vez: OPTIONS_OFFERED de nuevo en la misma sesión, sin la opción retirada, con los refrigerios y la categoría del almuerzo agotada")]
    public async Task La_oferta_recalculada_se_registra_otra_vez()
    {
        var maquina = NuevaMaquina();
        Identificar(maquina, "0001");
        var elegida = IdDe(maquina, "Almuerzo con gaseosa");
        maquina.Elegir(elegida);
        CambiarVersion(elegida, 2);
        GastarCupo("0001", CategoriaAlmuerzo, 1);

        await maquina.ConfirmarAsync();

        var ofertas = Registros("OPTIONS_OFFERED");
        Assert.Equal(2, ofertas.Count);
        Assert.Equal(ofertas[0].GetProperty("IdentitySessionId").GetGuid(), ofertas[1].GetProperty("IdentitySessionId").GetGuid());
        Assert.Contains(Ofrecidas(ofertas[0]), o => o.Id == elegida);
        Assert.DoesNotContain(Ofrecidas(ofertas[1]), o => o.Id == elegida);
        Assert.Equal(Refrigerios, Ofrecidas(ofertas[1]).Select(o => o.Nombre).Order());
        var retenida = Assert.Single(ofertas[1].GetProperty("Withheld").EnumerateArray(), r => r.GetProperty("MenuItemId").GetGuid() == elegida);
        Assert.Equal("LOCAL_LIMIT_REACHED", retenida.GetProperty("ReasonCode").GetString());
        Assert.All(ofertas, o => Assert.DoesNotContain("0001", Uuid.Replace(o.GetRawText(), ""), StringComparison.Ordinal));
        var agotada = Assert.Single(ofertas[1].GetProperty("Exhausted").EnumerateArray());
        Assert.Equal((CategoriaAlmuerzo, 1, 1),
            (agotada.GetProperty("CategoryId").GetGuid(), agotada.GetProperty("Used").GetInt32(), agotada.GetProperty("Limit").GetInt32()));
    }

    private MaquinaAtencion NuevaMaquina()
    {
        var maquina = new MaquinaAtencion(new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: true));
        maquina.Iniciar();
        return maquina;
    }

    private static void Identificar(MaquinaAtencion maquina, string codigo) =>
        Assert.Equal(ResultadoToque.Aceptado, maquina.Identificar(codigo));

    private static Guid IdDe(MaquinaAtencion maquina, string nombre) =>
        maquina.PaginaActual.Single(o => o.Nombre == nombre).Id;

    private static IEnumerable<(Guid Id, string Nombre)> Ofrecidas(JsonElement oferta) =>
        oferta.GetProperty("Offered").EnumerateArray().Select(o => (o.GetProperty("MenuItemId").GetGuid(), o.GetProperty("MenuLabel").GetString()!));

    private void GastarCupo(string codigo, Guid categoria, int unidades) =>
        Ejecutar(
            """
            INSERT INTO tbComedorUsosCupo (comperCodigo, comacCodigo, comcatCodigo, comucFechaNegocio, comucUsado, comucVersion)
            VALUES ((SELECT comperCodigo FROM tbComedorCredencialesPersonas WHERE comcreValorNormalizado = $valor), $amb, $cat, $fecha, $usado, 1)
            """,
            ("$valor", codigo), ("$amb", Ambito), ("$cat", categoria), ("$fecha", FechaDeNegocio), ("$usado", unidades));

    private void CambiarVersion(Guid opcion, int version) =>
        Ejecutar("UPDATE tbComedorOpcionesMenu SET commenVersionMenu = $ver WHERE commenCodigo = $men", ("$ver", version), ("$men", opcion));

    private List<JsonElement> Registros(string tipo)
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = $tipo ORDER BY combitSecuencia";
        comando.Parameters.AddWithValue("$tipo", tipo);
        using var lector = comando.ExecuteReader();
        var registros = new List<JsonElement>();
        while (lector.Read())
        {
            registros.Add(JsonDocument.Parse(lector.GetString(0)).RootElement.Clone());
        }

        return registros;
    }

    private long Contar(string tabla)
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT COUNT(*) FROM {tabla}";
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private void Ejecutar(string sql, params (string Nombre, object Valor)[] parametros)
    {
        using var conexion = Abrir();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nombre, valor is Guid id ? id.ToString("D") : valor);
        }

        Assert.Equal(1, comando.ExecuteNonQuery());
    }

    private SqliteConnection Abrir()
    {
        var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
        conexion.Open();
        return conexion;
    }
}
