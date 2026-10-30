using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Demostracion;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Prepara la base de la atención y arma los servicios que usa la máquina de estados (D5, D7 y D10 de
/// atender-seleccion-y-confirmacion): siembra la demostración si hace falta, agrega los datos de atención y envía los
/// trabajos que una caída dejó en cola. Con un receptor, arranca además el ciclo de envío de lotes cada 30 s, fuera de
/// la atención (D11 de recibir-lotes-idempotente); el servicio se queda con el receptor y lo libera.
/// </summary>
public sealed class ServicioAtencion : IDisposable
{
    private static readonly TimeSpan IntervaloEnvio = TimeSpan.FromSeconds(30);

    private readonly IReceptorLotes? _receptor;
    private BaseComedor? _base;
    private ColaSincronizacion? _cola;
    private SincronizadorPuesto? _sincronizador;
    private CancellationTokenSource? _ciclo;

    public ServicioAtencion(string rutaBase, TimeProvider reloj, IImpresoraComprobantes impresora, IReceptorLotes? receptor = null)
    {
        RutaBase = rutaBase;
        Reloj = reloj;
        Impresora = impresora;
        _receptor = receptor;
    }

    public string RutaBase { get; }

    public TimeProvider Reloj { get; }

    public IImpresoraComprobantes Impresora { get; }

    public EstadoPreparacion Estado { get; private set; } = EstadoPreparacion.ErrorAlmacenamiento;

    public BaseComedor Base => _base ?? throw new InvalidOperationException("Preparar() debe devolver Lista antes de atender.");

    public IdentificacionPorCodigo Identificacion { get; private set; } = null!;

    public ConsultaCatalogo Catalogo { get; private set; } = null!;

    public UnidadConfirmacion Unidad { get; private set; } = null!;

    public TrabajosImpresion Trabajos { get; private set; } = null!;

    public Asistencia Asistencia { get; private set; } = null!;

    /// <summary>Lo que muestra la franja de la sincronización; sin receptor, «Sin receptor configurado».</summary>
    public EstadoSincronizacion Sincronizacion { get; private set; } = new(false, 0, 0, null, false);

    /// <summary>Avisa un estado nuevo de la sincronización; puede llegar desde el hilo del ciclo de envío.</summary>
    public event Action? SincronizacionCambiada;

    /// <summary>
    /// Abre la base, la siembra y retoma los trabajos en cola (RB17). Una versión de esquema desconocida o un error
    /// de SQLite se informan como estado, sin tocar el archivo; cualquier otra excepción sigue de largo.
    /// </summary>
    public EstadoPreparacion Preparar()
    {
        try
        {
            var baseComedor = BaseComedor.Abrir(RutaBase);
            SemillaDemostracion.Aplicar(baseComedor);
            SemillaAtencion.Aplicar(baseComedor);
            _base = baseComedor;
            Identificacion = new IdentificacionPorCodigo(baseComedor, Reloj);
            Catalogo = new ConsultaCatalogo(baseComedor);
            Unidad = new UnidadConfirmacion(baseComedor, Reloj);
            Trabajos = new TrabajosImpresion(baseComedor, Reloj, Impresora);
            Asistencia = new Asistencia(baseComedor, Reloj, Trabajos);
            Trabajos.EnviarPendientes();
            PrepararSincronizacion(baseComedor);
            Estado = EstadoPreparacion.Lista;
        }
        catch (EsquemaDesconocidoException)
        {
            Estado = EstadoPreparacion.ErrorAlmacenamiento;
        }
        catch (SqliteException)
        {
            Estado = EstadoPreparacion.ErrorAlmacenamiento;
        }

        return Estado;
    }

    /// <summary>Un ciclo de envío ya, sin esperar al temporizador; sin receptor, sólo relee los conteos.</summary>
    public async Task EnviarAhoraAsync(CancellationToken cancelacion = default)
    {
        if (_sincronizador is not null)
        {
            await _sincronizador.EnviarUnaVezAsync(cancelacion);
        }
        else if (_cola is not null)
        {
            var (pendientes, retenidos) = _cola.LeerConteos();
            Publicar(new EstadoSincronizacion(false, pendientes, retenidos, null, false));
        }
    }

    public void Dispose()
    {
        _ciclo?.Cancel();
        _ciclo?.Dispose();
        (_receptor as IDisposable)?.Dispose();
    }

    private void PrepararSincronizacion(BaseComedor baseComedor)
    {
        _cola = new ColaSincronizacion(baseComedor, Reloj);
        if (_receptor is null)
        {
            var (pendientes, retenidos) = _cola.LeerConteos();
            Sincronizacion = new EstadoSincronizacion(false, pendientes, retenidos, null, false);
            return;
        }

        if (_sincronizador is not null)
        {
            return;
        }

        _sincronizador = new SincronizadorPuesto(_cola, _receptor);
        _sincronizador.EstadoCambiado += Publicar;
        Sincronizacion = _sincronizador.Estado;
        _ciclo = new CancellationTokenSource();
        _ = CicloDeEnvioAsync(_sincronizador, _ciclo.Token);
    }

    /// <summary>
    /// Un ciclo ya y después uno cada 30 s, de a uno por vez. Un error inesperado de un ciclo queda visible en la franja
    /// y el siguiente vuelve a intentar: la atención no depende del envío (HU02-C).
    /// </summary>
    private async Task CicloDeEnvioAsync(SincronizadorPuesto sincronizador, CancellationToken cancelacion)
    {
        await Task.Yield();
        using var temporizador = new PeriodicTimer(IntervaloEnvio);
        try
        {
            do
            {
                try
                {
                    await sincronizador.EnviarUnaVezAsync(cancelacion);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Publicar(Sincronizacion with { UltimoError = "ERROR_INTERNO" });
                }
            }
            while (await temporizador.WaitForNextTickAsync(cancelacion));
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
        {
            // Se cerró la atención.
        }
    }

    private void Publicar(EstadoSincronizacion estado)
    {
        Sincronizacion = estado;
        SincronizacionCambiada?.Invoke();
    }
}
