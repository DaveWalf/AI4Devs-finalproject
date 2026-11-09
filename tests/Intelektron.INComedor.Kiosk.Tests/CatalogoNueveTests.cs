using Intelektron.INComedor.Kiosk.Atencion;
using Intelektron.INComedor.Kiosk.Demostracion;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El catálogo sintético de nueve opciones (D6 de presentar-atencion-tactil): la variable INCOMEDOR_CATALOGO, leída de un
/// diccionario, y la siembra sobre un archivo SQLite real temporal.
/// </summary>
public class CatalogoNueveTests : IDisposable
{
    private const string DatosLocales = @"C:\Users\prueba\AppData\Local";

    private readonly ArchivoTemporal _archivo = new();
    private readonly RelojManual _reloj = new("2026-10-30T15:00:00Z");

    public void Dispose() => _archivo.Dispose();

    [Theory(DisplayName = "modo-demostracion · INCOMEDOR_CATALOGO: «nueve» sin espacios y sin distinguir mayúsculas elige el catálogo de nueve; otro valor o ninguno, el de quince")]
    [InlineData("nueve", true)]
    [InlineData("NUEVE", true)]
    [InlineData("Nueve", true)]
    [InlineData(" NUEVE ", false)]
    [InlineData("9", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void La_variable_elige_el_catalogo(string? valor, bool nueve)
    {
        var entorno = EntornoDemostracion.Resolver(
            nombre => nombre == EntornoDemostracion.VariableCatalogo ? valor : null, DatosLocales);

        Assert.Equal("INCOMEDOR_CATALOGO", EntornoDemostracion.VariableCatalogo);
        Assert.Equal(nueve, entorno.CatalogoNueve);
    }

    [Fact(DisplayName = "modo-demostracion · Base nueva con el catálogo de nueve: nueve opciones, tres con importe cero, con las mismas personas y operadores sintéticos")]
    public void Base_nueva_con_el_catalogo_de_nueve()
    {
        var estado = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: true).Preparar();

        Assert.Equal(EstadoPreparacion.Lista, estado);
        using var conexion = Abrir();
        Assert.Equal(9L, Escalar(conexion, "SELECT COUNT(*) FROM tbComedorOpcionesMenu"));
        Assert.Equal(3L, Escalar(conexion, "SELECT COUNT(*) FROM tbComedorOpcionesMenu WHERE commenImporteEmpleadoUnidadesMenores = 0"));
        Assert.Equal(1L, Escalar(conexion, $"SELECT COUNT(*) FROM tbComedorOpcionesMenu WHERE commenCodigo = '{SemillaAtencion.PrimeraOpcionId}'"));
        Assert.Equal(9L, Escalar(conexion, "SELECT COUNT(*) FROM tbComedorOpcionesMenu_CategoriasCupo"));
        Assert.Equal(9L, Escalar(conexion, "SELECT COUNT(*) FROM tbComedorPersonas"));
        Assert.Equal(8L, Escalar(conexion, "SELECT COUNT(*) FROM tbComedorCredencialesPersonas"));
        Assert.Equal(4L, Escalar(conexion, "SELECT COUNT(*) FROM tbComedorPermisosOperadores"));
        Assert.Equal(0L, Escalar(conexion, "SELECT COUNT(*) FROM tbComedorConsumos"));
    }

    [Fact(DisplayName = "modo-demostracion · Una base ya sembrada con quince no cambia al arrancar con el catálogo de nueve")]
    public void Base_ya_sembrada_no_cambia()
    {
        Assert.Equal(EstadoPreparacion.Lista, new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba()).Preparar());

        var estado = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: true).Preparar();

        Assert.Equal(EstadoPreparacion.Lista, estado);
        using var conexion = Abrir();
        Assert.Equal(15L, Escalar(conexion, "SELECT COUNT(*) FROM tbComedorOpcionesMenu"));
    }

    [Fact(DisplayName = "modo-demostracion · AT01 · Con el catálogo de nueve, 0001 abre la selección como con el de quince")]
    public void El_codigo_0001_abre_la_seleccion_con_nueve()
    {
        var maquina = new MaquinaAtencion(new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba(), catalogoNueve: true));
        maquina.Iniciar();

        Assert.Equal(ResultadoToque.Aceptado, maquina.Identificar("0001"));

        Assert.Equal(EstadoAtencion.Seleccion, maquina.Estado);
        Assert.Equal("Persona sintética 01", maquina.NombreVisible);
        Assert.Contains(maquina.PaginaActual, o => o.Id == SemillaAtencion.PrimeraOpcionId);
    }

    private SqliteConnection Abrir()
    {
        var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Pooling=False");
        conexion.Open();
        return conexion;
    }

    private static long Escalar(SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(comando.ExecuteScalar());
    }
}
