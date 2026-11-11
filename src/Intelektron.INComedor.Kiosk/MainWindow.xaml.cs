using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Intelektron.INComedor.Kiosk.Red;
using Intelektron.INComedor.Kiosk.Atencion;
using Intelektron.INComedor.Kiosk.Demostracion;
using Intelektron.INComedor.Kiosk.Sincronizacion;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace Intelektron.INComedor.Kiosk;

/// <summary>Ventana host del BlazorWebView (ADR01): no tiene más XAML que el visor.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // La carpeta, el receptor y el WebView2 salen del entorno; sin sus variables, lo de siempre (D1 a D4).
        var entorno = EntornoDemostracion.Resolver(
            Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        Directory.CreateDirectory(entorno.Carpeta);
        ConfigurarWebView2(entorno);
        var demostracion = Environment.GetCommandLineArgs().Contains("--demostracion", StringComparer.OrdinalIgnoreCase);

        var servicios = new ServiceCollection();
        servicios.AddWpfBlazorWebView();
        if (demostracion)
        {
            servicios.AddSingleton(new ServicioDemostracion(entorno.RutaBase, TimeProvider.System));
        }
        else
        {
            // El receptor de demostración (D12) deja sus certificados en certificados\; sin ellos, el puesto atiende sin receptor.
            var receptor = ClienteReceptorHttp.DesdeCarpetaDemostracion(entorno.CarpetaCertificados, entorno.DireccionReceptor);
            // Los datos del comprobante que son del equipo; el «desde» del correlativo se lee en cada confirmación (D2 y D3 de
            // enriquecer-comprobante).
            var datosPuesto = new AlmacenDatosPuesto(entorno.Carpeta);
            var atencion = new ServicioAtencion(entorno.RutaBase, TimeProvider.System, new ImpresoraSimulada(entorno.CarpetaComprobantes), receptor,
                entorno.CatalogoNueve)
            {
                BitacorasAutomaticas = true,
                CorrelativoDesde = () => datosPuesto.Leer() is { Correlativo: true } datos ? datos.Desde : null,
            };
            // El contenedor no libera una instancia que recibió armada: el cierre de la ventana es el cierre ordenado del
            // puesto, con KIOSK_STOPPED y la última pasada de las bitácoras (escribir-bitacoras-externas, D4 y D9).
            Closed += (_, _) => atencion.Dispose();
            servicios.AddSingleton(atencion);
            servicios.AddSingleton(new AlmacenModoPuesto(entorno.Carpeta));
            servicios.AddSingleton(new AlmacenSimulacion(entorno.Carpeta));
            servicios.AddSingleton(new AlmacenImpresora(entorno.Carpeta));
            servicios.AddSingleton(datosPuesto);
            servicios.AddSingleton(PrepararCierre());
            var almacenPantalla = new AlmacenPantalla(entorno.Carpeta);
            servicios.AddSingleton(almacenPantalla);
            servicios.AddSingleton(PrepararPantalla(almacenPantalla));
            // La edición de red corre netsh con permiso de administrador: sólo la ventana real lo registra (D11), y la prueba
            // de conexión usa la dirección y los certificados del receptor de siempre (D12).
            servicios.AddSingleton(new EjecutorNetsh());
            servicios.AddSingleton(new PruebaConexion(entorno.DireccionReceptor, entorno.CarpetaCertificados));
            servicios.AddSingleton(new MaquinaAtencion(atencion));
            servicios.AddSingleton(new LogosLocales(entorno.Carpeta));
            servicios.AddSingleton(new AlmacenEstiloVisual(entorno.Carpeta));
            // La presentación de cada opción, local del puesto como el estilo visual (D3 de personalizar-opciones).
            servicios.AddSingleton(new AlmacenPresentacionOpciones(entorno.Carpeta));
            // La exportación de Consumos: la carpeta recordada en la de datos y el diálogo de carpetas sobre esta ventana, para que
            // se abra en el monitor del puesto (D6 de buscar-y-exportar-consumos).
            servicios.AddSingleton(new AlmacenExportacion(entorno.Carpeta));
            // La exportación automática de consumos: su configuración en la carpeta de datos; la pantalla arma el motor y su reloj
            // cuando la base está lista (D4 y D6 de exportar-consumos-automaticamente).
            servicios.AddSingleton(new AlmacenConfiguracionExportacion(entorno.Carpeta));
            servicios.AddSingleton(new AlmacenConfiguracionCorreo(entorno.Carpeta));
            servicios.AddSingleton<IElectorCarpeta>(new ElectorCarpetaWindows(this));
            // La planilla que se importa a Personas, con el diálogo de archivos sobre esta ventana (D7 de importar-personas-desde-excel).
            servicios.AddSingleton<IElectorArchivo>(new ElectorArchivoWindows(this));
        }

        blazorWebView.Services = servicios.BuildServiceProvider();
        blazorWebView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = demostracion ? typeof(PantallaDemostracion) : typeof(PantallaAtencion),
        });
    }

    /// <summary>
    /// El monitor del puesto (D10 de configurar-puesto-por-pestanas): al abrir, el guardado o, si no está, el principal con el
    /// aviso para la pestaña Pantalla; al guardar otro, la ventana pasa a él. Siempre a pantalla completa.
    /// </summary>
    private PantallaDelPuesto PrepararPantalla(AlmacenPantalla almacen)
    {
        var pantalla = new PantallaDelPuesto(monitor => Dispatcher.BeginInvoke(new Action(() => UbicarEn(monitor))));
        var monitores = Monitores.Leer();
        if (monitores.Count == 0)
        {
            return pantalla;
        }

        var eleccion = Monitores.ElegirMonitor(monitores, almacen.Leer());
        pantalla.Aviso = eleccion.Aviso;
        SourceInitialized += (_, _) => UbicarEn(eleccion.Monitor);
        return pantalla;
    }

    /// <summary>La ventana, en el área del monitor y maximizada: sin bordes, eso es la pantalla completa de ese monitor (D7, D10).</summary>
    private void UbicarEn(MonitorDelPuesto monitor)
    {
        var ventana = new WindowInteropHelper(this).Handle;
        if (ventana == IntPtr.Zero)
        {
            return;
        }

        var area = monitor.Trabajo ?? monitor.Area;
        WindowState = WindowState.Normal;
        SetWindowPos(ventana, IntPtr.Zero, area.Izquierda, area.Arriba, area.Ancho, area.Alto, SinCambiarOrden | SinActivar);
        WindowState = WindowState.Maximized;
    }

    private const uint SinCambiarOrden = 0x0004;
    private const uint SinActivar = 0x0010;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr ventana, IntPtr despues, int x, int y, int ancho, int alto, uint marcas);

    /// <summary>
    /// El cierre según el modo (D7 de configurar-puesto-por-pestanas): en operativo, un pedido de cierre que no viene de la
    /// clave del día (Alt+F4 u otro programa) se frena y la pantalla pide la clave; el fin de la sesión de Windows nunca se
    /// frena. El cierre autorizado sale del manejador de la pantalla y se hace después, en el despachador.
    /// </summary>
    private CierrePuesto PrepararCierre()
    {
        var cierre = new CierrePuesto(() => Dispatcher.BeginInvoke(new Action(Close)));
        var sesionTerminando = false;
        if (Application.Current is { } aplicacion)
        {
            aplicacion.SessionEnding += (_, _) => sesionTerminando = true;
        }

        Closing += (_, e) =>
        {
            if (!sesionTerminando && cierre.DebeFrenar)
            {
                e.Cancel = true;
                cierre.PedirClave();
            }
        };
        return cierre;
    }

    /// <summary>
    /// Con una carpeta elegida, el WebView2 guarda ahí sus datos: dos entornos con argumentos distintos no comparten carpeta
    /// (D4). Con un puerto válido, abre la depuración remota, que Chromium escucha sólo en loopback; entra por código porque
    /// una app elevada ignora WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS (D2). Siempre, con o sin las variables, el navegador
    /// arranca sin el rebote elástico (D15 de configurar-puesto-por-pestanas).
    /// </summary>
    private void ConfigurarWebView2(EntornoDemostracion entorno)
    {
        blazorWebView.BlazorWebViewInitializing = (_, e) =>
        {
            if (entorno.CarpetaDatosWebView2 is { } datos)
            {
                e.UserDataFolder = datos;
            }

            e.EnvironmentOptions = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = ArgumentosWebView2.Componer(entorno.ArgumentosNavegador),
            };
        };
    }
}
