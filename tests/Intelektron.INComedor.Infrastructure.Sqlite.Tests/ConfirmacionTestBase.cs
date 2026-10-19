using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>Un archivo SQLite nuevo, el fixture sintético y una unidad de confirmación por test (D11).</summary>
public abstract class ConfirmacionTestBase : IDisposable
{
    private readonly ArchivoTemporal _archivo = new();

    protected ConfirmacionTestBase()
    {
        Base = BaseComedor.Abrir(_archivo.Ruta);
        Fixture = FixtureSintetico.Cargar(Base);
        Unidad = new UnidadConfirmacion(Base, Reloj);
    }

    protected BaseComedor Base { get; }

    protected FixtureSintetico Fixture { get; }

    protected RelojFijo Reloj { get; } = new("2026-10-19T15:00:00Z");

    protected UnidadConfirmacion Unidad { get; }

    public void Dispose() => _archivo.Dispose();

    protected SqliteConnection Abrir() => Base.AbrirConexion();

    /// <summary>Sesión de consumo emitida un minuto antes del reloj actual y vigente 30 minutos.</summary>
    protected Guid NuevaSesion(PersonaFixture persona) =>
        Fixture.NuevaSesion(Base, persona, Iso(Reloj.Ahora.AddMinutes(-1)), Iso(Reloj.Ahora.AddMinutes(30)));

    protected SolicitudConfirmacion Solicitud(PersonaFixture persona, OpcionFixture opcion, Guid? sesion = null, Guid? intentId = null) =>
        new(intentId ?? Guid.NewGuid(), sesion ?? NuevaSesion(persona), persona.Id, opcion.Id, opcion.Version, 1);

    protected static string Iso(DateTimeOffset instante) =>
        instante.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
