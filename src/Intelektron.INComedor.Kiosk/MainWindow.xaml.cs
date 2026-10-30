using System.IO;
using System.Windows;
using Intelektron.INComedor.Kiosk.Atencion;
using Intelektron.INComedor.Kiosk.Demostracion;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk;

/// <summary>Ventana host del BlazorWebView (ADR01): no tiene más XAML que el visor.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var carpeta = CarpetaDemostracion();
        var rutaBase = Path.Combine(carpeta, "incomedor.db");
        var demostracion = Environment.GetCommandLineArgs().Contains("--demostracion", StringComparer.OrdinalIgnoreCase);

        var servicios = new ServiceCollection();
        servicios.AddWpfBlazorWebView();
        if (demostracion)
        {
            servicios.AddSingleton(new ServicioDemostracion(rutaBase, TimeProvider.System));
        }
        else
        {
            var atencion = new ServicioAtencion(rutaBase, TimeProvider.System, new ImpresoraSimulada(Path.Combine(carpeta, "comprobantes")));
            servicios.AddSingleton(atencion);
            servicios.AddSingleton(new MaquinaAtencion(atencion));
        }

        blazorWebView.Services = servicios.BuildServiceProvider();
        blazorWebView.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = demostracion ? typeof(PantallaDemostracion) : typeof(PantallaAtencion),
        });
    }

    /// <summary>%LOCALAPPDATA%\INComedor\demo\ (D5): la base y los comprobantes; se reinicia borrando esa carpeta.</summary>
    private static string CarpetaDemostracion()
    {
        var carpeta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "INComedor", "demo");
        Directory.CreateDirectory(carpeta);
        return carpeta;
    }
}
