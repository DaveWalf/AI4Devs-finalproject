using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La máquina de la atención identifica por la credencial elegida en el combo con las guardas y el después del código tipeado
/// (D2 de simular-identificacion-por-lista): sólo en reposo, «Ocupado» fuera de él, y la oferta al aceptar. Con SQLite real
/// y la semilla de la atención.
/// </summary>
public class MaquinaSimulacionTests : IDisposable
{
    private static readonly Guid CredencialDe0001 = Guid.Parse("00000000-0000-4000-8000-000000000031");
    private static readonly Guid PersonaDe0001 = Guid.Parse("00000000-0000-4000-8000-000000000027");
    private static readonly Guid CredencialDe9001 = Guid.Parse("00000000-0000-4000-8000-000000000041");

    private readonly ArchivoTemporal _archivo = new();
    private readonly RelojManual _reloj = new("2026-11-10T15:00:00Z");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _archivo.Dispose();
    }

    [Fact(DisplayName = "atencion-puesto · Elegir a una persona abre la selección (máquina): la credencial de 0001 lleva a la selección con su nombre y su catálogo")]
    public void La_credencial_lleva_a_la_seleccion()
    {
        var maquina = NuevaMaquina();

        var toque = maquina.IdentificarPorCredencial(CredencialDe0001);

        Assert.Equal(ResultadoToque.Aceptado, toque);
        Assert.Equal(EstadoAtencion.Seleccion, maquina.Estado);
        Assert.Equal("Persona sintética 01", maquina.NombreVisible);
        Assert.Equal(PersonaDe0001, maquina.PersonaId);
        Assert.NotEmpty(maquina.PaginaActual);
    }

    [Fact(DisplayName = "atencion-puesto · Elegir a una persona abre la selección (máquina, borde): durante una atención, otra credencial da «Ocupado» sin cambiar la persona ni la opción")]
    public void Durante_una_atencion_otra_credencial_da_ocupado()
    {
        var maquina = NuevaMaquina();
        maquina.IdentificarPorCredencial(CredencialDe0001);
        var opcion = maquina.PaginaActual[0].Id;
        maquina.Elegir(opcion);

        var toque = maquina.IdentificarPorCredencial(CredencialDe9001);

        Assert.Equal(ResultadoToque.Ocupado, toque);
        Assert.Equal(MensajesAtencion.Ocupado, maquina.Aviso);
        Assert.Equal(PersonaDe0001, maquina.PersonaId);
        Assert.Equal(opcion, maquina.Elegida?.Id);
        using var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Mode=ReadOnly");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM tbComedorSesionesIdentidad";
        Assert.Equal(1L, Convert.ToInt64(comando.ExecuteScalar()));
    }

    private MaquinaAtencion NuevaMaquina()
    {
        var maquina = new MaquinaAtencion(new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba()));
        maquina.Iniciar();
        return maquina;
    }
}
