using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>La franja de la sincronización al pie de la atención, con bUnit: los cinco mensajes de D11 de recibir-lotes-idempotente.</summary>
public class FranjaSincronizacionTests : IDisposable
{
    private readonly BunitContext _contexto = new();

    public void Dispose()
    {
        _contexto.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact(DisplayName = "HU02-C · Sin pendientes, retenidos ni errores: «Sincronización: al día», con la etiqueta una sola vez")]
    public void Al_dia()
    {
        Assert.Equal(["al día"], Mensajes(new EstadoSincronizacion(true, 0, 0, null, false)));
        Assert.Equal("Sincronización: al día", Texto(new EstadoSincronizacion(true, 0, 0, null, false)));
    }

    [Fact(DisplayName = "HU02-C · AT01 · Eventos que esperan acuse: «N pendientes»")]
    public void Pendientes() =>
        Assert.Equal(["2 pendientes"], Mensajes(new EstadoSincronizacion(true, 2, 0, "NETWORK", false)));

    [Fact(DisplayName = "SubmitSyncBatch · Eventos en conflicto, rechazados o en revisión: «N retenidos para revisión»")]
    public void Retenidos() =>
        Assert.Equal(["3 retenidos para revisión"], Mensajes(new EstadoSincronizacion(true, 0, 3, null, false)));

    [Fact(DisplayName = "HU02-G · Un 401 o un 403 del receptor: «Error técnico: credencial del equipo», distinto de una falta de red")]
    public void Error_de_credencial() =>
        Assert.Equal(["Error técnico: credencial del equipo", "1 pendiente"],
            Mensajes(new EstadoSincronizacion(true, 1, 0, "401 EQUIPMENT_AUTH_REQUIRED", true)));

    [Fact(DisplayName = "HU02-G · Sin certificados de la demostración: «Sin receptor configurado»")]
    public void Sin_receptor() =>
        Assert.Equal(["Sin receptor configurado"], Mensajes(new EstadoSincronizacion(false, 1, 0, null, false)));

    [Fact(DisplayName = "D11 · La franja es una región de estado que se anuncia sin interrumpir")]
    public void La_franja_es_una_region_de_estado()
    {
        var franja = _contexto.Render<FranjaSincronizacion>(p => p.Add(f => f.Estado, new EstadoSincronizacion(true, 0, 0, null, false)));

        var seccion = franja.Find("section.franja-sincronizacion");
        Assert.Equal("status", seccion.GetAttribute("role"));
        Assert.Equal("polite", seccion.GetAttribute("aria-live"));
    }

    private List<string> Mensajes(EstadoSincronizacion estado) =>
        _contexto.Render<FranjaSincronizacion>(p => p.Add(f => f.Estado, estado))
            .FindAll(".mensaje-sincronizacion").Select(m => m.TextContent.Trim()).ToList();

    /// <summary>El texto visible de la franja, sin el estilo y con los espacios normalizados.</summary>
    private string Texto(EstadoSincronizacion estado)
    {
        var franja = _contexto.Render<FranjaSincronizacion>(p => p.Add(f => f.Estado, estado));
        var partes = new[] { franja.Find("strong").TextContent }.Concat(franja.FindAll(".mensaje-sincronizacion").Select(m => m.TextContent));
        return string.Join(" ", partes.Select(p => p.Trim()));
    }
}
