using System.Text.Json;
using Bunit;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Intelektron.INComedor.Kiosk.Red;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Un puesto de bUnit para configurar-puesto-por-pestanas: una carpeta de datos temporal propia con la base adentro, el
/// estilo y el modo en esa carpeta, un <see cref="CierrePuesto"/> que sólo cuenta los cierres (la ventana no existe en
/// bUnit) y la general de la bitácora leída del archivo, después de una pasada del escritor, como en BitacorasDelPuestoTests.
/// </summary>
public sealed class PuestoDePrueba : IDisposable
{
    public PuestoDePrueba(ModoPuesto? modo = null)
    {
        Directory.CreateDirectory(Carpeta);
        if (modo is { } guardado)
        {
            new AlmacenModoPuesto(Carpeta).Guardar(guardado);
        }

        Cierre = new CierrePuesto(() => Cierres++);
        Pantalla = new PantallaDelPuesto(Movimientos.Add);
        Conexion = new PruebaConexion(new Uri("https://localhost:5001/"), Path.Combine(Carpeta, "certificados"));
    }

    public string Carpeta { get; } = Path.Combine(Path.GetTempPath(), "incomedor-tests", "puesto-" + Guid.NewGuid().ToString("N"));

    public string RutaBase => Path.Combine(Carpeta, "incomedor.db");

    public BunitContext Contexto { get; } = new();

    public RelojManual Reloj { get; } = new("2026-11-06T15:00:00Z");

    public ServicioAtencion Servicio { get; private set; } = null!;

    public CierrePuesto Cierre { get; }

    public int Cierres { get; private set; }

    /// <summary>El mediador del monitor: en bUnit no hay ventana, así que sólo anota a qué monitor se pidió moverla (D10).</summary>
    public PantallaDelPuesto Pantalla { get; }

    public List<MonitorDelPuesto> Movimientos { get; } = [];

    /// <summary>La prueba de conexión del puesto (D12): por defecto, la carpeta sin certificados, así que no prueba nada.</summary>
    public PruebaConexion Conexion { get; set; }

    public IRenderedComponent<PantallaAtencion> Dibujar(IImpresoraComprobantes? impresora = null)
    {
        Servicio = new ServicioAtencion(RutaBase, Reloj, impresora ?? new ImpresoraDePrueba());
        Contexto.Services.AddSingleton(Servicio);
        Contexto.Services.AddSingleton(new MaquinaAtencion(Servicio));
        Contexto.Services.AddSingleton(new AlmacenEstiloVisual(Carpeta));
        Contexto.Services.AddSingleton(new AlmacenModoPuesto(Carpeta));
        Contexto.Services.AddSingleton(new AlmacenImpresora(Carpeta));
        Contexto.Services.AddSingleton(Cierre);
        Contexto.Services.AddSingleton(Pantalla);
        Contexto.Services.AddSingleton(new AlmacenPantalla(Carpeta));
        Contexto.Services.AddSingleton(Conexion);
        return Contexto.Render<PantallaAtencion>();
    }

    /// <summary>Corre una pasada del escritor y lee las líneas de la general de noviembre de 2026.</summary>
    public List<JsonElement> General()
    {
        Servicio.Bitacoras!.Entregar();
        var carpeta = Directory.GetDirectories(Path.Combine(Carpeta, "bitacoras")).Single();
        var ruta = Path.Combine(carpeta, EscritorBitacoras.NombreArchivo(DestinoBitacora.General, "202611"));
        return File.Exists(ruta)
            ? File.ReadAllText(ruta).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList()
            : [];
    }

    public static string? Tipo(JsonElement linea) => linea.GetProperty("EventType").GetString();

    public static JsonElement ContextoDe(JsonElement linea) => linea.GetProperty("Context");

    public static bool SinActor(JsonElement contexto) =>
        contexto.GetProperty("ActorPersonId").ValueKind == JsonValueKind.Null && contexto.GetProperty("ActorReason").GetString() == "NO_AUTHENTICATION";

    public void Dispose()
    {
        Contexto.Dispose();
        Servicio?.Dispose();
        if (Directory.Exists(Carpeta))
        {
            foreach (var archivo in Directory.EnumerateFiles(Carpeta, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(archivo, FileAttributes.Normal);
            }

            Directory.Delete(Carpeta, recursive: true);
        }
    }
}
