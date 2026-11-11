using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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
    private static readonly TimeSpan IntervaloBitacoras = TimeSpan.FromSeconds(2);

    private readonly IReceptorLotes? _receptor;
    private readonly bool _catalogoNueve;
    private BaseComedor? _base;
    private ColaSincronizacion? _cola;
    private SincronizadorPuesto? _sincronizador;
    private CancellationTokenSource? _ciclo;
    private RegistroDelPuesto? _registroPuesto;
    private CancellationTokenSource? _cicloBitacoras;
    private Task? _tareaBitacoras;
    private bool _cerrado;

    /// <param name="catalogoNueve">Si una base nueva se siembra con el catálogo de nueve (D6 de presentar-atencion-tactil).</param>
    public ServicioAtencion(string rutaBase, TimeProvider reloj, IImpresoraComprobantes impresora, IReceptorLotes? receptor = null, bool catalogoNueve = false)
    {
        RutaBase = rutaBase;
        Reloj = reloj;
        Impresora = impresora;
        _receptor = receptor;
        _catalogoNueve = catalogoNueve;
    }

    public string RutaBase { get; }

    public TimeProvider Reloj { get; }

    public IImpresoraComprobantes Impresora { get; }

    public EstadoPreparacion Estado { get; private set; } = EstadoPreparacion.ErrorAlmacenamiento;

    public BaseComedor Base => _base ?? throw new InvalidOperationException("Preparar() debe devolver Lista antes de atender.");

    public IdentificacionPorCodigo Identificacion { get; private set; } = null!;

    public ConsultaCatalogo Catalogo { get; private set; } = null!;

    /// <summary>La oferta por persona: lo que se le ofrece al identificarse (D2 de ofrecer-solo-opciones-disponibles).</summary>
    public ConsultaDisponibilidad Disponibilidad { get; private set; } = null!;

    public UnidadConfirmacion Unidad { get; private set; } = null!;

    public TrabajosImpresion Trabajos { get; private set; } = null!;

    public Asistencia Asistencia { get; private set; } = null!;

    /// <summary>La consulta de consumos de Configuración (configurar-puesto-por-pestanas, D9); null hasta que Preparar devuelva Lista.</summary>
    public ConsultaConsumos? Consumos { get; private set; }

    /// <summary>La edición de personas y opciones de Configuración (administrar-personas-y-opciones); null hasta que Preparar devuelva Lista.</summary>
    public AdministracionLocal? Administracion { get; private set; }

    /// <summary>El escritor de las bitácoras mensuales (escribir-bitacoras-externas); null hasta que Preparar devuelva Lista.</summary>
    public EscritorBitacoras? Bitacoras { get; private set; }

    /// <summary>
    /// Si el puesto entrega solo sus bitácoras (D4): una pasada al terminar Preparar, otra cada 2 s y la última al cerrar. La
    /// ventana del puesto lo activa siempre. Sin él, las pasadas las pide quien usa el servicio (las pruebas, que arman muchos
    /// servicios sobre una carpeta compartida y borran la base al terminar sin liberarlos).
    /// </summary>
    public bool BitacorasAutomaticas { get; init; }

    /// <summary>
    /// El «desde» de la numeración correlativa, leído en cada confirmación; sin él, el código de siempre (D2 de
    /// enriquecer-comprobante). La ventana lo lee de los datos del puesto.
    /// </summary>
    public Func<long?>? CorrelativoDesde { get; init; }

    /// <summary>Cómo se lee la versión de la aplicación para KIOSK_STARTED y KIOSK_STOPPED; una excepción o null la dejan nula.</summary>
    public Func<string?> LeerVersionAplicacion { get; init; } = VersionDeLaAplicacion;

    /// <summary>Lo que registra la máquina de atención, en su propia transacción corta (D8).</summary>
    public RegistroDelPuesto? RegistroAtencion { get; private set; }

    /// <summary>Lo que registra Configuración, sin actor hasta que llegue la clave (D9).</summary>
    public RegistroDelPuesto? RegistroConfiguracion { get; private set; }

    /// <summary>Los avisos por correo, si la ventana los armó: la atención y las bitácoras les cuentan lo que pasa (D4 de avisar-por-correo).</summary>
    public AvisosCorreo? Avisos { get; set; }

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
            SemillaAtencion.Aplicar(baseComedor, _catalogoNueve);
            _base = baseComedor;
            Identificacion = new IdentificacionPorCodigo(baseComedor, Reloj);
            Catalogo = new ConsultaCatalogo(baseComedor);
            Disponibilidad = new ConsultaDisponibilidad(baseComedor, Reloj);
            Unidad = new UnidadConfirmacion(baseComedor, Reloj) { CorrelativoDesde = CorrelativoDesde ?? (() => null) };
            Trabajos = new TrabajosImpresion(baseComedor, Reloj, Impresora);
            Asistencia = new Asistencia(baseComedor, Reloj, Trabajos);
            Administracion = new AdministracionLocal(baseComedor, Reloj);
            Consumos = new ConsultaConsumos(baseComedor, Reloj);
            Trabajos.EnviarPendientes();
            PrepararSincronizacion(baseComedor);
            PrepararBitacoras(baseComedor);
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
        CerrarBitacoras();
    }

    /// <summary>
    /// El escritor de bitácoras y KIOSK_STARTED, una sola vez por servicio (D9). Con las bitácoras automáticas, la primera
    /// pasada corre acá mismo, antes de atender, y las siguientes cada 2 s fuera de la atención (D4).
    /// </summary>
    private void PrepararBitacoras(BaseComedor baseComedor)
    {
        if (Bitacoras is not null)
        {
            return;
        }

        RegistroAtencion = new RegistroDelPuesto(baseComedor, Reloj, "Kiosk.MaquinaAtencion");
        RegistroConfiguracion = new RegistroDelPuesto(baseComedor, Reloj, "Kiosk.Configuracion");
        _registroPuesto = new RegistroDelPuesto(baseComedor, Reloj, "Kiosk.ServicioAtencion");
        RegistrarPuesto("KIOSK_STARTED");
        var carpetaDatos = Path.GetDirectoryName(Path.GetFullPath(RutaBase))!;
        Bitacoras = new EscritorBitacoras(baseComedor, carpetaDatos, Reloj);

        if (BitacorasAutomaticas)
        {
            PasadaDeBitacoras(Bitacoras);
            _cicloBitacoras = new CancellationTokenSource();
            var escritor = Bitacoras;
            var cancelacion = _cicloBitacoras.Token;
            // Fuera del contexto de la pantalla: Dispose espera esta tarea desde el hilo de la ventana, y si sus
            // continuaciones volvieran a ese hilo se esperarían mutuamente al cerrar.
            _tareaBitacoras = Task.Run(() => CicloDeBitacorasAsync(escritor, cancelacion));
        }
    }

    private void RegistrarPuesto(string tipo) => _registroPuesto!.RegistrarPuesto(
        tipo, Leer(LeerVersionAplicacion), Leer(() => Environment.Version.ToString()), Leer(() => RuntimeInformation.OSDescription));

    /// <summary>Una pasada cada 2 s, de a una por vez y fuera de la atención; termina si la base ya no está.</summary>
    private async Task CicloDeBitacorasAsync(EscritorBitacoras escritor, CancellationToken cancelacion)
    {
        using var temporizador = new PeriodicTimer(IntervaloBitacoras);
        try
        {
            while (await temporizador.WaitForNextTickAsync(cancelacion).ConfigureAwait(false))
            {
                if (!PasadaDeBitacoras(escritor))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
        {
            // Se cerró el puesto: la última pasada la hace Dispose.
        }
    }

    /// <summary>
    /// Las fallas de los archivos las resuelve el escritor con sus incidencias. Una base ocupada se reintenta en la pasada
    /// siguiente: la atención no depende del archivo (D-C14). Devuelve false si la base ya no está, para no recrearla.
    /// </summary>
    private bool PasadaDeBitacoras(EscritorBitacoras escritor)
    {
        try
        {
            escritor.Entregar();
            // Una bitácora que pasa a degradada, al Administrador Principal (D4 de avisar-por-correo).
            Avisos?.Bitacoras(escritor.Estado());
            return true;
        }
        catch (SqliteException) when (!File.Exists(RutaBase))
        {
            return false;
        }
        catch (SqliteException)
        {
            return true;
        }
    }

    /// <summary>Espera la pasada en curso, registra KIOSK_STOPPED y, con las bitácoras automáticas, hace la última (D4, D9).</summary>
    private void CerrarBitacoras()
    {
        if (Bitacoras is null || _cerrado)
        {
            return;
        }

        _cerrado = true;
        _cicloBitacoras?.Cancel();
        _tareaBitacoras?.Wait();
        _cicloBitacoras?.Dispose();
        try
        {
            RegistrarPuesto("KIOSK_STOPPED");
        }
        catch (SqliteException)
        {
            // Sin SQLite (la base ya no está o no responde) no hay dónde registrar el cierre, y el puesto cierra igual.
            return;
        }

        if (BitacorasAutomaticas)
        {
            PasadaDeBitacoras(Bitacoras);
        }
    }

    private static string? Leer(Func<string?> lectura)
    {
        try
        {
            return lectura();
        }
        catch (Exception)
        {
            // Una versión que no se puede leer va nula, nunca inventada (D9).
            return null;
        }
    }

    private static string? VersionDeLaAplicacion() =>
        typeof(ServicioAtencion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

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
