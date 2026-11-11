namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Pide una carpeta a quien está en el puesto (D6 de buscar-y-exportar-consumos): la ventana lo resuelve con el diálogo de
/// carpetas de Windows, sobre el monitor del puesto, y las pruebas con un doble. Null si se cancela.
/// </summary>
public interface IElectorCarpeta
{
    string? Elegir(string? actual);
}
