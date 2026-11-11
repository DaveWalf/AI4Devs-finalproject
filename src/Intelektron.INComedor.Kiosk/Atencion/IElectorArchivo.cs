namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Pide el <c>.xlsx</c> que se importa a Personas (D7 de importar-personas-desde-excel): la ventana lo resuelve con el
/// diálogo de archivos de Windows, sobre el monitor del puesto, y las pruebas con un doble. Empieza en <paramref name="carpeta"/>
/// si se pasa. Devuelve la ruta, o null si se cancela.
/// </summary>
public interface IElectorArchivo
{
    string? Elegir(string? carpeta);

    /// <summary>
    /// Con el título del diálogo, que dice qué se importa (D6 de importar-opciones-desde-excel); un elector que no lo usa,
    /// como los dobles de las pruebas, elige igual.
    /// </summary>
    string? Elegir(string? carpeta, string titulo) => Elegir(carpeta);
}
