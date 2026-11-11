using Microsoft.JSInterop;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// El aviso del teclado general (D2 de operar-solo-con-la-pantalla): puesto.js escucha el foco desde que carga la página y,
/// cuando un campo de texto se tocó con el dedo o el foco salió de los campos, llama a este método estático; la pantalla se
/// suscribe al iniciar. Así la pantalla no llama a JS al dibujarse.
/// </summary>
public static class TecladoGeneral
{
    /// <summary>Un campo de texto quedó con el foco por un toque (true), o el foco salió de los campos (false).</summary>
    public static event Action<bool>? CampoTocado;

    [JSInvokable("CampoTocado")]
    public static Task Avisar(bool enfocado)
    {
        CampoTocado?.Invoke(enfocado);
        return Task.CompletedTask;
    }
}
