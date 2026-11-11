using System.Text.Json;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Las líneas que imprime el comprobante (elegir-lineas-del-comprobante): el nombre del comensal, el aviso de reimpresión y el
/// operador que habilitó una copia se eligen en Impresora, se guardan con los textos en impresora.json y valen desde el
/// próximo comprobante, en el papel y en la pantalla de la confirmación; «Ver» las muestra siempre, y la marca de la clave del
/// día no depende de ellas.
/// </summary>
public class LineasDelComprobanteTests
{
    [Fact(DisplayName = "comprobante-local · Un archivo sin las elecciones las da las tres en «Imprimir»")]
    public void Un_archivo_sin_las_elecciones()
    {
        var textos = AlmacenImpresora.Interpretar("""{"encabezado":"Comedor de planta","pie":"Gracias"}""");

        Assert.NotNull(textos);
        Assert.Equal(("Comedor de planta", "Gracias"), (textos.Encabezado, textos.Pie));
        Assert.True(textos.Comensal);
        Assert.True(textos.AvisoReimpresion);
        Assert.True(textos.Operador);
    }

    [Theory(DisplayName = "comprobante-local · Una elección que no es un booleano JSON vuelve el archivo entero al valor por defecto")]
    [InlineData("""{"encabezado":"Comedor de planta","pie":"","comensal":"no"}""")]
    [InlineData("""{"encabezado":"Comedor de planta","pie":"","avisoReimpresion":0}""")]
    [InlineData("""{"encabezado":"Comedor de planta","pie":"","operador":null}""")]
    public void Una_eleccion_fuera_de_forma_en_el_archivo(string contenido)
    {
        using var puesto = new PuestoDePrueba();
        File.WriteAllText(Path.Combine(puesto.Carpeta, "impresora.json"), contenido);

        Assert.Null(AlmacenImpresora.Interpretar(contenido));
        Assert.Equal(TextosComprobante.PorDefecto, new AlmacenImpresora(puesto.Carpeta).Leer());
    }

    [Fact(DisplayName = "comprobante-local · Guardar y releer conserva los textos y las tres elecciones")]
    public void Guardar_y_releer_conserva_las_cinco()
    {
        using var puesto = new PuestoDePrueba();
        var almacen = new AlmacenImpresora(puesto.Carpeta);
        var textos = new TextosComprobante("Comedor de planta", "Gracias", Comensal: false, AvisoReimpresion: true, Operador: false);

        almacen.Guardar(textos);

        Assert.Equal(textos, almacen.Leer());
    }

    [Fact(DisplayName = "comprobante-local · Serializar escribe los cinco campos")]
    public void Serializar_escribe_los_cinco()
    {
        using var documento = JsonDocument.Parse(AlmacenImpresora.Serializar(new TextosComprobante("Comedor", "", Comensal: false, AvisoReimpresion: false, Operador: true)));
        var raiz = documento.RootElement;

        Assert.Equal(["encabezado", "pie", "comensal", "avisoReimpresion", "operador"], raiz.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.False(raiz.GetProperty("comensal").GetBoolean());
        Assert.False(raiz.GetProperty("avisoReimpresion").GetBoolean());
        Assert.True(raiz.GetProperty("operador").GetBoolean());
    }

    [Fact(DisplayName = "comprobante-local · Elegir qué imprime: «No imprimir» en «Nombre del comensal» y «Guardar» avisan lo guardado, dejan TICKET_TEXTS con las elecciones antes y después, y la elección sigue al volver a abrir Configuración")]
    public async Task Elegir_que_imprime()
    {
        using var puesto = new PuestoDePrueba();
        var pantalla = puesto.Dibujar();
        await AbrirImpresora(pantalla);
        Assert.All(["#comensal-imprimir", "#aviso-reimpresion-imprimir", "#operador-imprimir"],
            id => Assert.Equal("true", pantalla.Find(id).GetAttribute("aria-pressed")));

        await pantalla.Find("#comensal-no-imprimir").ClickAsync(new());
        await pantalla.Find("#guardar-textos").ClickAsync(new());

        Assert.Equal(MensajesAtencion.TextosGuardados, pantalla.Find("#aviso-guardado").TextContent.Trim());
        var cambio = ContextoDe(Assert.Single(puesto.General(), l => Tipo(l) == "CONFIGURATION_CHANGED"));
        Assert.Equal(("TICKET_TEXTS", "APPLIED"), (cambio.GetProperty("Setting").GetString(), cambio.GetProperty("Result").GetString()));
        Assert.True(cambio.GetProperty("Before").GetProperty("PrintPersonName").GetBoolean());
        Assert.False(cambio.GetProperty("After").GetProperty("PrintPersonName").GetBoolean());
        Assert.True(cambio.GetProperty("After").GetProperty("PrintReprintNotice").GetBoolean());
        Assert.True(cambio.GetProperty("After").GetProperty("PrintOperator").GetBoolean());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        await AbrirImpresora(pantalla);
        Assert.Equal("true", pantalla.Find("#comensal-no-imprimir").GetAttribute("aria-pressed"));
        Assert.Equal("false", pantalla.Find("#comensal-imprimir").GetAttribute("aria-pressed"));
    }

    [Fact(DisplayName = "comprobante-local · Un archivo de textos de antes: con encabezado y pie y sin las elecciones, el comprobante lleva el comensal y Impresora muestra las tres en «Imprimir»")]
    public async Task Un_archivo_de_textos_de_antes()
    {
        using var puesto = new PuestoDePrueba();
        File.WriteAllText(Path.Combine(puesto.Carpeta, "impresora.json"), """{"encabezado":"Comedor de planta","pie":"Gracias"}""");
        var impresora = new ImpresoraSimulada(Path.Combine(puesto.Carpeta, "comprobantes"));
        var pantalla = puesto.Dibujar(impresora);

        await Confirmar(pantalla);

        Assert.Contains(Original(impresora), l => l == "Comensal: Persona sintética 01");
        Assert.Equal("Persona sintética 01", pantalla.Find(".comensal-comprobante").TextContent.Trim());
        await pantalla.Find("#listo").ClickAsync(new());
        await AbrirImpresora(pantalla);
        Assert.All(["#comensal-imprimir", "#aviso-reimpresion-imprimir", "#operador-imprimir"],
            id => Assert.Equal("true", pantalla.Find(id).GetAttribute("aria-pressed")));
    }

    [Fact(DisplayName = "comprobante-local · Una elección fuera de forma: con «comensal» igual al texto «no», el puesto usa los textos y las elecciones por defecto")]
    public async Task Una_eleccion_fuera_de_forma()
    {
        using var puesto = new PuestoDePrueba();
        File.WriteAllText(Path.Combine(puesto.Carpeta, "impresora.json"), """{"encabezado":"Comedor de planta","pie":"","comensal":"no"}""");
        var impresora = new ImpresoraSimulada(Path.Combine(puesto.Carpeta, "comprobantes"));
        var pantalla = puesto.Dibujar(impresora);

        await Confirmar(pantalla);

        var papel = Original(impresora);
        Assert.StartsWith(TextosComprobante.EncabezadoPorDefecto, papel[0]);
        Assert.Contains(papel, l => l == "Comensal: Persona sintética 01");
    }

    [Fact(DisplayName = "comprobante-local · Un comprobante sin el comensal: con «Nombre del comensal» en «No imprimir», el papel y la pantalla no tienen la línea, y «Ver» en Consumos muestra «Persona sintética 01»")]
    public async Task Un_comprobante_sin_el_comensal()
    {
        using var puesto = new PuestoDePrueba();
        var impresora = new ImpresoraSimulada(Path.Combine(puesto.Carpeta, "comprobantes"));
        var pantalla = puesto.Dibujar(impresora);
        await Elegir(pantalla, "#comensal-no-imprimir");

        await Confirmar(pantalla);

        Assert.DoesNotContain(Original(impresora), l => l.StartsWith("Comensal:", StringComparison.Ordinal));
        Assert.DoesNotContain("Persona sintética 01", string.Join('\n', Original(impresora)));
        Assert.Empty(pantalla.FindAll(".comensal-comprobante"));
        await pantalla.Find("#listo").ClickAsync(new());
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#seccion-consumos tr.consumo .ver").ClickAsync(new());
        Assert.Equal("Persona sintética 01", pantalla.Find("#comprobante-en-pantalla .comensal-comprobante").TextContent.Trim());
    }

    [Fact(DisplayName = "comprobante-local · Una copia sin aviso ni operador: con los dos en «No imprimir», la copia de un operador dice «Copia: 1» y no lleva REIMPRESIÓN, «ORIGINAL» ni «Habilitó:»")]
    public async Task Una_copia_sin_aviso_ni_operador()
    {
        using var puesto = new PuestoDePrueba();
        var impresora = new ImpresoraSimulada(Path.Combine(puesto.Carpeta, "comprobantes"));
        var pantalla = puesto.Dibujar(impresora);
        await Confirmar(pantalla);
        await pantalla.Find("#listo").ClickAsync(new());
        await Elegir(pantalla, "#aviso-reimpresion-no-imprimir", "#operador-no-imprimir");

        await EntradaConfiguracion.Entrar(pantalla, "9001");
        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());
        await pantalla.FindAll("#seccion-consumos .motivo")[0].ClickAsync(new());

        var copia = File.ReadAllLines(Assert.Single(Directory.GetFiles(impresora.Carpeta, "*-1.txt")));
        Assert.Contains(copia, l => l == "Copia: 1");
        Assert.DoesNotContain(copia, l => l.Contains("REIMPRESIÓN", StringComparison.Ordinal));
        Assert.DoesNotContain(copia, l => l == "ORIGINAL");
        Assert.DoesNotContain(copia, l => l.StartsWith("Habilitó:", StringComparison.Ordinal));
        Assert.DoesNotContain(copia, l => l == ComprobanteEnPantalla.MarcaSinValidez);
    }

    [Fact(DisplayName = "comprobante-local · La marca de la clave del día no se apaga: con el aviso y el operador en «No imprimir», la copia hecha con la clave del día lleva «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD» y «Copia: 1»")]
    public async Task La_marca_de_la_clave_del_dia_no_se_apaga()
    {
        using var puesto = new PuestoDePrueba();
        var impresora = new ImpresoraSimulada(Path.Combine(puesto.Carpeta, "comprobantes"));
        var pantalla = puesto.Dibujar(impresora);
        await Confirmar(pantalla);
        await pantalla.Find("#listo").ClickAsync(new());
        await Elegir(pantalla, "#aviso-reimpresion-no-imprimir", "#operador-no-imprimir");

        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#seccion-consumos tr.consumo .reimprimir").ClickAsync(new());
        await pantalla.FindAll("#seccion-consumos .motivo")[0].ClickAsync(new());

        var copia = File.ReadAllLines(Assert.Single(Directory.GetFiles(impresora.Carpeta, "*-1.txt")));
        Assert.Contains(copia, l => l == ComprobanteEnPantalla.MarcaSinValidez);
        Assert.Contains(copia, l => l == "Copia: 1");
        Assert.DoesNotContain(copia, l => l.Contains("REIMPRESIÓN", StringComparison.Ordinal));
    }

    /// <summary>Con la clave del día, elige «No imprimir» en las líneas dadas, guarda y cierra Configuración.</summary>
    private static async Task Elegir(IRenderedComponent<PantallaAtencion> pantalla, params string[] botones)
    {
        await AbrirImpresora(pantalla);
        foreach (var boton in botones)
        {
            await pantalla.Find(boton).ClickAsync(new());
        }

        await pantalla.Find("#guardar-textos").ClickAsync(new());
        Assert.Equal(MensajesAtencion.TextosGuardados, pantalla.Find("#aviso-guardado").TextContent.Trim());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());
    }

    private static string[] Original(ImpresoraSimulada impresora) =>
        File.ReadAllLines(Assert.Single(Directory.GetFiles(impresora.Carpeta, "*-0.txt")));

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
