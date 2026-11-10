using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La entrada a Configuración de las pruebas de componentes (D8 de entrar-a-configuracion-con-teclado): toca el engranaje,
/// tipea la clave del día en el teclado en pantalla y toca «ENTER». Desde ese change el engranaje pide la clave también en
/// mantenimiento, y las pruebas que entraban tocándolo pasan por acá.
/// </summary>
public static class EntradaConfiguracion
{
    public static async Task Entrar(IRenderedComponent<PantallaAtencion> pantalla, string clave = "4711")
    {
        await pantalla.Find(".engranaje").ClickAsync(new());
        await Tipear(pantalla, clave);
    }

    /// <summary>Con el diálogo de la clave abierto, tipea en el teclado en pantalla y toca «ENTER».</summary>
    public static async Task Tipear(IRenderedComponent<PantallaAtencion> pantalla, string valor)
    {
        foreach (var digito in valor)
        {
            await pantalla.Find($"#tecla-{digito}").ClickAsync(new());
        }

        await pantalla.Find("#aceptar-clave").ClickAsync(new());
    }
}
