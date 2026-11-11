using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Los textos del comprobante en la pestaña Impresora (comprobante-local de configurar-puesto-por-pestanas, D13): el
/// encabezado y el pie en impresora.json, validados al leer y al guardar, que la impresora simulada y el comprobante en
/// pantalla usan desde el próximo que sale, sin cambiar los datos guardados de ningún comprobante.
/// </summary>
public class TextosComprobanteTests
{
    [Theory(DisplayName = "comprobante-local · El encabezado va de 1 a 40 caracteres y el pie de 0 a 60, sin caracteres de control")]
    [InlineData("Comedor de planta", "Gracias por tu visita", true)]
    [InlineData("A", "", true)]
    [InlineData("", "", false)]
    [InlineData("1234567890123456789012345678901234567890", "", true)]
    [InlineData("12345678901234567890123456789012345678901", "", false)]
    [InlineData("Comedor", "123456789012345678901234567890123456789012345678901234567890X", false)]
    [InlineData("Comedor", "Gracias\npor venir", false)]
    [InlineData("Com\tedor", "", false)]
    public void La_forma_de_los_textos(string encabezado, string pie, bool valido)
    {
        Assert.Equal(valido, TextosComprobante.Validar(encabezado, pie).Count == 0);
    }

    [Fact(DisplayName = "comprobante-local · Cambiar el encabezado y el pie: el comprobante en pantalla y el de la impresora empiezan con «Comedor de planta» y terminan con «Gracias por tu visita», y la general tiene TICKET_TEXTS")]
    public async Task Cambiar_el_encabezado_y_el_pie()
    {
        using var puesto = new PuestoDePrueba();
        var impresora = new ImpresoraSimulada(Path.Combine(puesto.Carpeta, "comprobantes"));
        var pantalla = puesto.Dibujar(impresora);
        await AbrirImpresora(pantalla);
        await pantalla.Find("#encabezado-comprobante").InputAsync(new ChangeEventArgs { Value = "Comedor de planta" });
        await pantalla.Find("#pie-comprobante").InputAsync(new ChangeEventArgs { Value = "Gracias por tu visita" });

        await pantalla.Find("#guardar-textos").ClickAsync(new());

        Assert.Equal(MensajesAtencion.TextosGuardados, pantalla.Find("#aviso-guardado").TextContent.Trim());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
        await Confirmar(pantalla);
        Assert.Equal("Comedor de planta", pantalla.Find(".encabezado-comprobante").TextContent.Trim());
        Assert.Equal("Gracias por tu visita", pantalla.Find(".pie-comprobante").TextContent.Trim());
        var lineas = File.ReadAllLines(Assert.Single(Directory.GetFiles(impresora.Carpeta, "*-0.txt"))).Where(l => l.Length > 0).ToList();
        Assert.StartsWith("Comedor de planta", lineas[0]);
        Assert.Equal("Gracias por tu visita", lineas[^1]);
        var cambio = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"));
        Assert.Equal(("TICKET_TEXTS", "APPLIED"), (cambio.GetProperty("Setting").GetString(), cambio.GetProperty("Result").GetString()));
        Assert.Equal("Comedor de planta", cambio.GetProperty("After").GetProperty("Header").GetString());
        Assert.Equal(TextosComprobante.EncabezadoPorDefecto, cambio.GetProperty("Before").GetProperty("Header").GetString());
        Assert.True(SinActor(cambio));
    }

    [Fact(DisplayName = "comprobante-local · Un texto fuera de forma: un encabezado vacío y, después, un pie con un salto de línea se rechazan con su campo señalado y los textos vigentes no cambian")]
    public async Task Un_texto_fuera_de_forma()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await AbrirImpresora(pantalla);

        await pantalla.Find("#encabezado-comprobante").InputAsync(new ChangeEventArgs { Value = "" });
        await pantalla.Find("#guardar-textos").ClickAsync(new());

        Assert.Equal("true", pantalla.Find("#encabezado-comprobante").GetAttribute("aria-invalid"));
        Assert.Equal(MensajesAtencion.PanelInvalido, pantalla.Find("#aviso-textos").TextContent.Trim());

        await pantalla.Find("#encabezado-comprobante").InputAsync(new ChangeEventArgs { Value = "Comedor de planta" });
        await pantalla.Find("#pie-comprobante").InputAsync(new ChangeEventArgs { Value = "Gracias\npor venir" });
        await pantalla.Find("#guardar-textos").ClickAsync(new());

        Assert.Equal("true", pantalla.Find("#pie-comprobante").GetAttribute("aria-invalid"));
        Assert.Null(pantalla.Find("#encabezado-comprobante").GetAttribute("aria-invalid"));
        Assert.Equal(TextosComprobante.PorDefecto, new AlmacenImpresora(puesto.Carpeta).Leer());
        Assert.False(File.Exists(Path.Combine(puesto.Carpeta, "impresora.json")));
    }

    [Fact(DisplayName = "comprobante-local · Una copia lleva los textos vigentes: impreso con los de siempre, la copia de un operador después de guardar un encabezado nuevo lleva ese encabezado, REIMPRESIÓN y los mismos datos")]
    public async Task Una_copia_lleva_los_textos_vigentes()
    {
        using var puesto = new PuestoDePrueba();
        var impresora = new ImpresoraSimulada(Path.Combine(puesto.Carpeta, "comprobantes"));
        var pantalla = puesto.Dibujar(impresora);
        await Confirmar(pantalla);
        await pantalla.Find("#listo").ClickAsync(new());
        var original = File.ReadAllLines(Assert.Single(Directory.GetFiles(impresora.Carpeta, "*-0.txt")));
        Assert.StartsWith(TextosComprobante.EncabezadoPorDefecto, original[0]);
        await AbrirImpresora(pantalla);
        await pantalla.Find("#encabezado-comprobante").InputAsync(new ChangeEventArgs { Value = "Comedor de planta" });
        await pantalla.Find("#guardar-textos").ClickAsync(new());

        await pantalla.Find("#pestana-consumos").ClickAsync(new());
        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());
        await pantalla.Find("#codigo-operador").InputAsync(new ChangeEventArgs { Value = "9001" });
        await pantalla.Find("#entrar-operador").ClickAsync(new());
        await pantalla.FindAll(".motivo")[0].ClickAsync(new());

        var copia = File.ReadAllLines(Assert.Single(Directory.GetFiles(impresora.Carpeta, "*-1.txt")));
        Assert.StartsWith("Comedor de planta", copia[0]);
        Assert.Contains(copia, l => l.Contains("REIMPRESIÓN", StringComparison.Ordinal));
        // Los mismos datos del comprobante: la línea de los datos guardados, el número y el comensal, dondequiera que caigan.
        static IEnumerable<string> Datos(IEnumerable<string> lineas) =>
            lineas.Where(l => l.StartsWith('{') || l.StartsWith("Número:", StringComparison.Ordinal) || l.StartsWith("Comensal:", StringComparison.Ordinal));
        Assert.Equal(Datos(original), Datos(copia));
        Assert.Single(Datos(original), l => l.StartsWith('{'));
    }

    [Fact(DisplayName = "comprobante-local · Un archivo de textos ilegible: con un encabezado de 300 caracteres, el comprobante empieza con «INComedor · comprobante» y no tiene pie")]
    public async Task Un_archivo_de_textos_ilegible()
    {
        using var puesto = new PuestoDePrueba();
        File.WriteAllText(Path.Combine(puesto.Carpeta, "impresora.json"), "{\"encabezado\":\"" + new string('a', 300) + "\",\"pie\":\"Gracias\"}");
        var impresora = new ImpresoraSimulada(Path.Combine(puesto.Carpeta, "comprobantes"));
        var pantalla = puesto.Dibujar(impresora);

        await Confirmar(pantalla);

        Assert.Equal(TextosComprobante.PorDefecto, new AlmacenImpresora(puesto.Carpeta).Leer());
        Assert.Equal("INComedor · comprobante", pantalla.Find(".encabezado-comprobante").TextContent.Trim());
        Assert.Empty(pantalla.FindAll(".pie-comprobante"));
        Assert.StartsWith("INComedor · comprobante", File.ReadAllLines(Assert.Single(Directory.GetFiles(impresora.Carpeta, "*-0.txt")))[0]);
    }

    private static async Task AbrirImpresora(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#pestana-impresora").ClickAsync(new());
    }

    private static async Task Confirmar(IRenderedComponent<PantallaAtencion> pantalla)
    {
        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#ingresar").ClickAsync(new());
        await pantalla.FindAll(".opcion")[0].ClickAsync(new());
        await pantalla.Find("#confirmar").ClickAsync(new());
        pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
    }
}
