using System.IO;
using System.Windows;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.Win32;

namespace Intelektron.INComedor.Kiosk;

/// <summary>
/// El diálogo de archivos de Windows para importar Personas (D7 de importar-personas-desde-excel), con la ventana del puesto
/// como dueña, como el de carpetas: se abre sobre ella, en el monitor del puesto, y la deja inerte mientras está abierto.
/// Ofrece libros <c>.xlsx</c>, o sólo <c>.zip</c> para un resguardo, y empieza en la carpeta de la exportación si todavía existe.
/// </summary>
public sealed class ElectorArchivoWindows : IElectorArchivo
{
    /// <summary>El título del diálogo de Personas, que la prueba de punta a punta busca para elegir el archivo.</summary>
    public const string Titulo = "Planilla de Personas para importar";

    /// <summary>El título del diálogo de Opciones (D6 de importar-opciones-desde-excel).</summary>
    public const string TituloOpciones = IElectorArchivo.TituloOpciones;

    private readonly Window _ventana;

    public ElectorArchivoWindows(Window ventana)
    {
        _ventana = ventana;
    }

    public string? Elegir(string? carpeta) => Elegir(carpeta, Titulo);

    public string? Elegir(string? carpeta, string titulo) => Mostrar(carpeta, titulo, "Libro de Excel (*.xlsx)|*.xlsx");

    /// <summary>Sólo los <c>.zip</c>, con el título del resguardo (D4 de resguardar-configuracion).</summary>
    public string? ElegirResguardo(string? carpeta) => Mostrar(carpeta, IElectorArchivo.TituloResguardo, "Resguardo del puesto (*.zip)|*.zip");

    private string? Mostrar(string? carpeta, string titulo, string filtro)
    {
        string? Mostrar()
        {
            var dialogo = new OpenFileDialog
            {
                Title = titulo,
                Filter = filtro,
                Multiselect = false,
                CheckFileExists = true,
            };
            if (carpeta is not null && Directory.Exists(carpeta))
            {
                dialogo.InitialDirectory = carpeta;
            }

            return dialogo.ShowDialog(_ventana) == true ? dialogo.FileName : null;
        }

        return _ventana.Dispatcher.CheckAccess() ? Mostrar() : _ventana.Dispatcher.Invoke(Mostrar);
    }
}
