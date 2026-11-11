namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// Pide el <c>.xlsx</c> que se importa a Personas (D7 de importar-personas-desde-excel): la ventana lo resuelve con el
/// diálogo de archivos de Windows, sobre el monitor del puesto, y las pruebas con un doble. Empieza en <paramref name="carpeta"/>
/// si se pasa. Devuelve la ruta, o null si se cancela.
/// </summary>
public interface IElectorArchivo
{
    /// <summary>El título del diálogo al importar Opciones (D6 de importar-opciones-desde-excel).</summary>
    public const string TituloOpciones = "Planilla de Opciones para importar";

    string? Elegir(string? carpeta);

    /// <summary>
    /// Con el título del diálogo, que dice qué se importa (D6 de importar-opciones-desde-excel); un elector que no lo usa,
    /// como los dobles de las pruebas, elige igual.
    /// </summary>
    string? Elegir(string? carpeta, string titulo) => Elegir(carpeta);

    /// <summary>El título del diálogo al recuperar un resguardo (D4 de resguardar-configuracion).</summary>
    public const string TituloResguardo = "Resguardo de la configuración para recuperar";

    /// <summary>
    /// El <c>.zip</c> de un resguardo, con su título y su filtro (D4 de resguardar-configuracion); un elector que no los usa,
    /// como los dobles de las pruebas, elige igual.
    /// </summary>
    string? ElegirResguardo(string? carpeta) => Elegir(carpeta, TituloResguardo);
}
