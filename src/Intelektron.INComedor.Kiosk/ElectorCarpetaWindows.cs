using System.IO;
using System.Windows;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.Win32;

namespace Intelektron.INComedor.Kiosk;

/// <summary>
/// El diálogo de carpetas de Windows para la exportación de Consumos (D6 de buscar-y-exportar-consumos), con la ventana del
/// puesto como dueña: se abre sobre ella, en el monitor del puesto, y la deja inerte mientras está abierto. Empieza en la
/// carpeta recordada si todavía existe.
/// </summary>
public sealed class ElectorCarpetaWindows : IElectorCarpeta
{
    private readonly Window _ventana;

    public ElectorCarpetaWindows(Window ventana)
    {
        _ventana = ventana;
    }

    public string? Elegir(string? actual)
    {
        string? Mostrar()
        {
            var dialogo = new OpenFolderDialog { Title = "Carpeta para exportar los consumos", Multiselect = false };
            if (actual is not null && Directory.Exists(actual))
            {
                dialogo.InitialDirectory = actual;
            }

            return dialogo.ShowDialog(_ventana) == true ? dialogo.FolderName : null;
        }

        return _ventana.Dispatcher.CheckAccess() ? Mostrar() : _ventana.Dispatcher.Invoke(Mostrar);
    }
}
