using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// El actor de lo que se guarda en Personas y Opciones (D7 de entrar-a-configuracion-con-teclado): con un operador, su persona
/// y sin ActorReason; sin actor, el nulo con NO_AUTHENTICATION de antes. Sobre una carpeta de datos temporal real con el
/// fixture sintético, como AdministracionLocalTests.
/// </summary>
public class AdministracionActorTests : IDisposable
{
    public AdministracionActorTests()
    {
        Directory.CreateDirectory(Carpeta);
        Base = BaseComedor.Abrir(Path.Combine(Carpeta, "incomedor.db"));
        Fixture = FixtureSintetico.Cargar(Base);
    }

    private string Carpeta { get; } = Path.Combine(Path.GetTempPath(), "incomedor-tests", "actor-" + Guid.NewGuid().ToString("N"));

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

    [Fact(DisplayName = "acceso-puesto · Lo guardado por un operador (personas): el CONFIGURATION_CHANGED tiene su persona como actor, sin ActorReason ni ningún código")]
    public void Guardar_personas_con_un_actor()
    {
        var operador = Guid.NewGuid();

        var resultado = Admin.GuardarPersonas([Alta("Persona sintética 05", "0105")], operador);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        var (texto, cambio) = Assert.Single(Cambios("PERSON"));
        Assert.Equal(operador, cambio.GetProperty("ActorPersonId").GetGuid());
        Assert.False(cambio.TryGetProperty("ActorReason", out _));
        Assert.DoesNotContain("0105", texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "acceso-puesto · Lo guardado sin operador (personas): el CONFIGURATION_CHANGED sigue con el actor nulo y NO_AUTHENTICATION")]
    public void Guardar_personas_sin_actor()
    {
        Admin.GuardarPersonas([Alta("Persona sintética 05", "0105")]);

        var (_, cambio) = Assert.Single(Cambios("PERSON"));
        Assert.Equal(JsonValueKind.Null, cambio.GetProperty("ActorPersonId").ValueKind);
        Assert.Equal("NO_AUTHENTICATION", cambio.GetProperty("ActorReason").GetString());
    }

    [Fact(DisplayName = "acceso-puesto · Lo guardado por un operador (rechazo y opciones): un guardado rechazado y uno de opciones también llevan su persona como actor")]
    public void El_rechazo_y_las_opciones_con_un_actor()
    {
        var operador = Guid.NewGuid();

        var rechazado = Admin.GuardarPersonas([Alta("", "0106")], operador);
        var opcion = Assert.Single(Admin.Leer().Opciones, o => o.Id == Fixture.AlmuerzoComun.Id);
        var opciones = Admin.GuardarOpciones([new EdicionOpcion(opcion.Id, opcion.Nombre + " del día", opcion.ServicioId, opcion.ImporteUnidadesMenores,
            opcion.Cargas.Select(c => new CargaEditada(c.CategoriaId, c.Unidades)).ToList(),
            opcion.Franjas.Select(f => new FranjaEditada(f.Inicio.ToString("HH:mm"), f.Fin.ToString("HH:mm"))).ToList())], operador);

        Assert.Equal(TipoGuardado.Rechazado, rechazado.Tipo);
        Assert.Equal(TipoGuardado.Aplicado, opciones.Tipo);
        var (_, rechazo) = Assert.Single(Cambios("PERSON"));
        var (_, cambioOpcion) = Assert.Single(Cambios("MENU_OPTION"));
        Assert.All([rechazo, cambioOpcion], c =>
        {
            Assert.Equal(operador, c.GetProperty("ActorPersonId").GetGuid());
            Assert.False(c.TryGetProperty("ActorReason", out _));
        });
    }

    private static EdicionPersona Alta(string nombre, string codigo) => new(null, nombre, [], codigo);

    /// <summary>Los CONFIGURATION_CHANGED de un ajuste, con su texto crudo para buscar códigos.</summary>
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
