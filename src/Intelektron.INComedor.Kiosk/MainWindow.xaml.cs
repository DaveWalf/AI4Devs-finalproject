using System.IO;
using System.Windows;
using Intelektron.INComedor.Kiosk.Demostracion;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk;

/// <summary>Ventana host del BlazorWebView (ADR01): no tiene más XAML que el visor.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var servicios = new ServiceCollection();
        servicios.AddWpfBlazorWebView();
        servicios.AddSingleton(new ServicioDemostracion(RutaBaseDemostracion(), TimeProvider.System));
        blazorWebView.Services = servicios.BuildServiceProvider();
    }

    /// <summary>%LOCALAPPDATA%\INComedor\demo\incomedor.db (D5); se borra eliminando esa carpeta.</summary>
    private static string RutaBaseDemostracion()
    {
        var carpeta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "INComedor", "demo");
        Directory.CreateDirectory(carpeta);
        return Path.Combine(carpeta, "incomedor.db");
    }
}
