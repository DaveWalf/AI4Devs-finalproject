using System.IO;
using System.Windows;
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
            var atencion = new ServicioAtencion(entorno.RutaBase, TimeProvider.System, new ImpresoraSimulada(entorno.CarpetaComprobantes), receptor,
                entorno.CatalogoNueve)
            {
                BitacorasAutomaticas = true,
            };
            // El contenedor no libera una instancia que recibió armada: el cierre de la ventana es el cierre ordenado del
            // puesto, con KIOSK_STOPPED y la última pasada de las bitácoras (escribir-bitacoras-externas, D4 y D9).
            Closed += (_, _) => atencion.Dispose();
            servicios.AddSingleton(atencion);
            servicios.AddSingleton(new AlmacenModoPuesto(entorno.Carpeta));
            servicios.AddSingleton(new AlmacenImpresora(entorno.Carpeta));
            servicios.AddSingleton(PrepararCierre());
            servicios.AddSingleton(new MaquinaAtencion(atencion));
            servicios.AddSingleton(new LogosLocales(entorno.Carpeta));
            servicios.AddSingleton(new AlmacenEstiloVisual(entorno.Carpeta));
        }

        blazorWebView.Services = servicios.BuildServiceProvider();
        blazorWebView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = demostracion ? typeof(PantallaDemostracion) : typeof(PantallaAtencion),
        });
    }

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
    /// una app elevada ignora WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS (D2). Sin las variables no se toca nada.
    /// </summary>
    private void ConfigurarWebView2(EntornoDemostracion entorno)
    {
        if (!entorno.AjustaWebView2)
        {
            return;
        }

        blazorWebView.BlazorWebViewInitializing = (_, e) =>
        {
            if (entorno.CarpetaDatosWebView2 is { } datos)
            {
                e.UserDataFolder = datos;
            }

            if (entorno.ArgumentosNavegador is { } argumentos)
            {
                e.EnvironmentOptions = new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = argumentos };
            }
        };
    }
}
