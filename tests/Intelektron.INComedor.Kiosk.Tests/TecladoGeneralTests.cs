using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El teclado general (operacion-tactil de operar-solo-con-la-pantalla, D1 a D4): aparece cuando puesto.js avisa que un
/// campo de texto se tocó, escribe con incomedor.teclear, tiene los símbolos de las casillas y las rutas, y en
/// Configuración se oculta por la sesión. bUnit sobre el puesto de prueba; lo que escribe en el campo lo prueba la E2E.
/// </summary>
public class TecladoGeneralTests
{
    [Fact(DisplayName = "operacion-tactil · Una casilla sólo con la pantalla: un campo tocado muestra el teclado con los símbolos, y cada tecla se escribe con incomedor.teclear")]
    public async Task Un_campo_tocado_muestra_el_teclado()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        Assert.Empty(pantalla.FindAll(".teclado-general"));

        await pantalla.InvokeAsync(() => pantalla.Instance.CampoEnfocado(true));
        await pantalla.Find(".teclado-general [data-tecla=\"P\"]").ClickAsync(new());
        await pantalla.Find(".teclado-general [data-tecla=\"@\"]").ClickAsync(new());

        var teclas = puesto.Contexto.JSInterop.VerifyInvoke("incomedor.teclear", 2);
        Assert.Equal(["p", "@"], teclas.Select(t => t.Arguments[0]!.ToString()));
        Assert.Single(pantalla.FindAll(".teclado-general [data-tecla=\"\\\\\"]"));
        Assert.Equal("Arroba", pantalla.Find(".teclado-general [data-tecla=\"@\"]").GetAttribute("aria-label"));
        Assert.Equal("Barra invertida", pantalla.Find(".teclado-general [data-tecla=\"\\\\\"]").GetAttribute("aria-label"));
        Assert.All(pantalla.FindAll(".teclado-general .tecla"), t => Assert.Equal("-1", t.GetAttribute("tabindex")));
        Assert.Empty(pantalla.FindAll(".teclado-general [data-tecla=\"OCULTAR\"]"));
        Assert.Contains("con-teclado", pantalla.Find(".puesto").ClassName, StringComparison.Ordinal);

        await pantalla.InvokeAsync(() => pantalla.Instance.CampoEnfocado(false));
        Assert.Empty(pantalla.FindAll(".teclado-general"));
    }

    [Fact(DisplayName = "operacion-tactil · Ocultar el teclado con un teclado físico: en Configuración, «Ocultar teclado» lo oculta hasta cerrarla; al volver a abrirla, aparece otra vez")]
    public async Task Ocultar_por_la_sesion()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.InvokeAsync(() => pantalla.Instance.CampoEnfocado(true));
        Assert.Contains("ocultar", pantalla.Find(".teclado-general [data-tecla=\"OCULTAR\"]").ClassName, StringComparison.Ordinal);
        await pantalla.Find(".teclado-general [data-tecla=\"OCULTAR\"]").ClickAsync(new());
        Assert.Empty(pantalla.FindAll(".teclado-general"));
        await pantalla.InvokeAsync(() => pantalla.Instance.CampoEnfocado(false));
        await pantalla.InvokeAsync(() => pantalla.Instance.CampoEnfocado(true));
        Assert.Empty(pantalla.FindAll(".teclado-general"));

        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.InvokeAsync(() => pantalla.Instance.CampoEnfocado(true));

        Assert.Single(pantalla.FindAll(".teclado-general"));
    }

    [Fact(DisplayName = "operacion-tactil · El código leído al salir del reposo: con el teclado a la vista en el código, la identificación lo saca con el reposo, y al volver no está hasta tocar otro campo")]
    public async Task Al_salir_del_reposo_se_va()
    {
        using var puesto = Preparar();
        var pantalla = puesto.Dibujar();
        await pantalla.InvokeAsync(() => pantalla.Instance.CampoEnfocado(true));
        Assert.Single(pantalla.FindAll(".teclado-general"));

        // El campo sale de la pantalla sin que puesto.js avise el foco, como con una lectura del lector.
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        Assert.Empty(pantalla.FindAll(".teclado-general"));
        Assert.DoesNotContain("con-teclado", pantalla.Find(".puesto").ClassName, StringComparison.Ordinal);

        await pantalla.Find("#cancelar").ClickAsync(new());
        Assert.NotEmpty(pantalla.FindAll("#codigo"));
        Assert.Empty(pantalla.FindAll(".teclado-general"));
    }

    [Fact(DisplayName = "operacion-tactil · Un teclado en pantalla para cada campo de texto: el de Personas sigue sin los símbolos ni «Ocultar teclado»")]
    public void El_de_personas_sin_simbolos()
    {
        using var contexto = new BunitContext();

        var teclado = contexto.Render<TecladoEnPantalla>();

        Assert.Equal(4, teclado.FindAll(".fila-teclas").Count);
        Assert.Empty(teclado.FindAll("[data-tecla=\"@\"]"));
    }

    private static PuestoDePrueba Preparar()
    {
        var puesto = new PuestoDePrueba();
        puesto.Contexto.JSInterop.Mode = JSRuntimeMode.Loose;
        return puesto;
    }
}
