using System.Text.Json;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Lo usado hoy en cada cupo y los cupos cambiados en la bitácora (administracion-local de editar-raciones-en-personas, D1 y
/// D3). Sobre una base SQLite real temporal con el fixture sintético; el reloj está fijo en 2026-11-06T15:00:00Z, las 12:00
/// del puesto, así que la fecha de negocio es el 06/11.
/// </summary>
public class RacionesDePersonasTests : IDisposable
{
    public RacionesDePersonasTests()
    {
        Directory.CreateDirectory(Carpeta);
        Base = BaseComedor.Abrir(Path.Combine(Carpeta, "incomedor.db"));
        Fixture = FixtureSintetico.Cargar(Base);
    }

    private string Carpeta { get; } = Path.Combine(Path.GetTempPath(), "incomedor-tests", "raciones-" + Guid.NewGuid().ToString("N"));

    private BaseComedor Base { get; }

    private FixtureSintetico Fixture { get; }

    private RelojFijo Reloj { get; } = new("2026-11-06T15:00:00Z");

    private AdministracionLocal Admin => new(Base, Reloj);

    public void Dispose()
    {
        if (Directory.Exists(Carpeta))
        {
            Directory.Delete(Carpeta, recursive: true);
        }
    }

    [Fact(DisplayName = "administracion-local · Lo usado hoy en cada cupo: un almuerzo hoy y otro ayer dan 1 usado en ALMUERZO, y nada en REFRIGERIO")]
    public void Lo_usado_hoy_en_cada_cupo()
    {
        Usar(Fixture.CategoriaAlmuerzo, "2026-11-06", 1);
        Usar(Fixture.CategoriaAlmuerzo, "2026-11-05", 1);

        var persona = Assert.Single(Admin.Leer().Personas, p => p.Id == Fixture.Habilitada.Id);

        Assert.Equal(1, Assert.Single(persona.Cupos, c => c.ServicioId == Fixture.ServicioAlmuerzo && c.CategoriaId == Fixture.CategoriaAlmuerzo).UsadoHoy);
        Assert.All(persona.Cupos.Where(c => c.CategoriaId == Fixture.CategoriaRefrigerio), c => Assert.Equal(0, c.UsadoHoy));
    }

    [Fact(DisplayName = "administracion-local · Un cupo cambiado por un operador: CONFIGURATION_CHANGED trae QuotaChanges con el límite antes y después, y el operador como actor")]
    public void Un_cupo_cambiado_por_un_operador()
    {
        var persona = Assert.Single(Admin.Leer().Personas, p => p.Id == Fixture.Habilitada.Id);
        var almuerzo = Assert.Single(persona.Cupos, c => c.ServicioId == Fixture.ServicioAlmuerzo && c.CategoriaId == Fixture.CategoriaAlmuerzo);
        var operador = Guid.NewGuid();
        var cupos = persona.Cupos
            .Select(c => new CupoEditado(c.ServicioId, c.CategoriaId, c == almuerzo ? almuerzo.Limite + 1 : c.Limite))
            .ToList();

        var resultado = Admin.GuardarPersonas([new EdicionPersona(persona.Id, persona.Nombre, cupos)], operador);

        Assert.Equal(TipoGuardado.Aplicado, resultado.Tipo);
        var contexto = Contexto();
        Assert.Equal(operador, contexto.GetProperty("ActorPersonId").GetGuid());
        var cambio = Assert.Single(contexto.GetProperty("QuotaChanges").EnumerateArray());
        Assert.Equal(Fixture.CategoriaAlmuerzo, cambio.GetProperty("CategoryId").GetGuid());
        Assert.Equal(almuerzo.Limite, cambio.GetProperty("Before").GetInt32());
        Assert.Equal(almuerzo.Limite + 1, cambio.GetProperty("After").GetInt32());
    }

    [Fact(DisplayName = "administracion-local · Un cupo cambiado por un operador: quitar un cupo lo registra con el después nulo, y sin actor queda NO_AUTHENTICATION")]
    public void Quitar_un_cupo_lo_registra_con_el_despues_nulo()
    {
        var persona = Assert.Single(Admin.Leer().Personas, p => p.Id == Fixture.Habilitada.Id);
        var quitado = persona.Cupos[0];
        var cupos = persona.Cupos.Skip(1).Select(c => new CupoEditado(c.ServicioId, c.CategoriaId, c.Limite)).ToList();

        Admin.GuardarPersonas([new EdicionPersona(persona.Id, persona.Nombre, cupos)]);

        var contexto = Contexto();
        Assert.Equal("NO_AUTHENTICATION", contexto.GetProperty("ActorReason").GetString());
        var cambio = Assert.Single(contexto.GetProperty("QuotaChanges").EnumerateArray());
        Assert.Equal(quitado.CategoriaId, cambio.GetProperty("CategoryId").GetGuid());
        Assert.Equal(quitado.Limite, cambio.GetProperty("Before").GetInt32());
        Assert.Equal(JsonValueKind.Null, cambio.GetProperty("After").ValueKind);
    }

    /// <summary>Lo usado por la persona habilitada en una categoría y una fecha de negocio, por SQL, como lo deja una confirmación.</summary>
    private void Usar(Guid categoria, string fecha, int usado)
    {
        using var conexion = Base.AbrirConexion();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorUsosCupo (comperCodigo, comacCodigo, comcatCodigo, comucFechaNegocio, comucUsado, comucVersion) VALUES ($per, $amb, $cat, $fecha, $usado, 1)",
            ("$per", Fixture.Habilitada.Id), ("$amb", Fixture.Ambito), ("$cat", categoria), ("$fecha", fecha), ("$usado", usado));
    }

    /// <summary>El contexto del único CONFIGURATION_CHANGED de la general, después de una pasada del escritor.</summary>
    private JsonElement Contexto()
    {
        new EscritorBitacoras(Base, Carpeta, Reloj).Entregar();
        var ruta = Path.Combine(Carpeta, "bitacoras", Fixture.PuestoId.ToString(), EscritorBitacoras.NombreArchivo(DestinoBitacora.General, "202611"));
        return File.ReadAllLines(ruta)
            .Select(l => JsonDocument.Parse(l).RootElement.Clone())
            .Single(l => l.GetProperty("EventType").GetString() == "CONFIGURATION_CHANGED")
            .GetProperty("Context");
    }
}
