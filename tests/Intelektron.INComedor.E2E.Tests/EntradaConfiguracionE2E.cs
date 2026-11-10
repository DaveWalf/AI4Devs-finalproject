using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// La entrada a Configuración del puesto compilado (D8 de entrar-a-configuracion-con-teclado): toca el engranaje, tipea la
/// clave del día en el teclado en pantalla y toca «ENTER». Desde ese change el engranaje pide la clave también en
/// mantenimiento, y las E2E que entraban tocándolo pasan por acá.
/// </summary>
public static class EntradaConfiguracionE2E
{
    public static async Task EntrarAsync(IPage pagina, string clave = "4711")
    {
        await pagina.Locator(".engranaje").ClickAsync();
        await TipearAsync(pagina, clave);
    }

    /// <summary>Con el diálogo de la clave abierto, tipea en el teclado en pantalla y toca «ENTER».</summary>
    public static async Task TipearAsync(IPage pagina, string valor)
    {
        foreach (var digito in valor)
        {
            await pagina.Locator($"#tecla-{digito}").ClickAsync();
        }

        await pagina.Locator("#aceptar-clave").ClickAsync();
    }
}
