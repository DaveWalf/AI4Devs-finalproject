using System.Text.Json;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El comprobante del puesto (comprobante-local de enriquecer-comprobante, D3, D4 y D6): las 50 frases, los datos del
/// puesto en <c>puesto.json</c>, el comprobante de la impresora simulada y el de pantalla con el puesto, el número, el
/// comensal, el operador de una copia y la frase antes del pie.
/// </summary>
public sealed class ComprobanteDelPuestoTests : IDisposable
{
    private readonly string _carpeta = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "incomedor-tests", "comprobante-puesto-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_carpeta, recursive: true);

    [Fact(DisplayName = "comprobante-local · Un comprobante con el puesto y la frase: hay 50 frases distintas, de hasta 60 caracteres, cada una con su cierre y sin caracteres de control")]
    public void Las_cincuenta_frases()
    {
        Assert.Equal(50, FrasesComprobante.Todas.Count);
        Assert.Equal(50, FrasesComprobante.Todas.Distinct(StringComparer.Ordinal).Count());
        Assert.All(FrasesComprobante.Todas, f =>
        {
            Assert.InRange(f.Length, 10, FrasesComprobante.LargoMaximo);
            Assert.True(f.EndsWith('.') || f.EndsWith('!'), f);
            Assert.DoesNotContain(f, char.IsControl);
        });
    }

    [Fact(DisplayName = "comprobante-local · La copia repite al comensal y la frase: la frase sale del comprobante, siempre la misma para el mismo, y comprobantes distintos reparten las 50")]
    public void La_frase_es_del_comprobante()
    {
        var comprobante = Guid.NewGuid();

        Assert.Equal(FrasesComprobante.Para(comprobante), FrasesComprobante.Para(comprobante));
        Assert.Contains(FrasesComprobante.Para(comprobante), FrasesComprobante.Todas);
        var repartidas = Enumerable.Range(0, 500).Select(_ => FrasesComprobante.Para(Guid.NewGuid())).Distinct().Count();
        Assert.True(repartidas > 40, $"500 comprobantes usaron {repartidas} frases");
    }

    [Fact(DisplayName = "comprobante-local · La copia repite al comensal y la frase: la lista no se reordena ni se edita, así un comprobante conocido conserva su frase")]
    public void La_frase_de_un_comprobante_conocido()
    {
        Assert.Equal("Gracias por pasar por el comedor.", FrasesComprobante.Para(Guid.Parse("0000001e-0000-4000-8000-000000000000")));
        Assert.Equal("¡Que lo disfrutes!", FrasesComprobante.Para(Guid.Parse("00000000-0000-4000-8000-000000000000")));
    }

    [Fact(DisplayName = "comprobante-local · Resultado incierto: ningún mensaje de la pantalla anuncia DUPLICADO; el aviso de incertidumbre y el de la copia enviada dicen REIMPRESIÓN")]
    public void Ningun_mensaje_dice_duplicado()
    {
        var constantes = typeof(MensajesAtencion).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

        Assert.DoesNotContain(constantes, c => c.Contains("DUPLICADO", StringComparison.Ordinal));
        Assert.Contains("REIMPRESIÓN", MensajesAtencion.Incertidumbre, StringComparison.Ordinal);
        Assert.Contains("REIMPRESIÓN", MensajesAtencion.CopiaEnviada(1), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "comprobante-local · Guardar los datos del comprobante: el puesto y la numeración se guardan en puesto.json y se leen con otro almacén; la línea del puesto dice lo que haya")]
    public void Los_datos_del_puesto()
    {
        var datos = new DatosPuesto(3, "Comedor Norte", true, 1000);

        new AlmacenDatosPuesto(_carpeta).Guardar(datos);

        Assert.Equal(datos, new AlmacenDatosPuesto(_carpeta).Leer());
        Assert.True(File.Exists(Path.Combine(_carpeta, "puesto.json")));
        Assert.Equal("Puesto 3 · Comedor Norte", datos.Linea);
        Assert.Equal("Puesto 3", (datos with { Nombre = string.Empty }).Linea);
        Assert.Equal("Comedor Norte", (datos with { Numero = null }).Linea);
        Assert.Null(DatosPuesto.PorDefecto.Linea);
    }

    [Fact(DisplayName = "comprobante-local · Datos fuera de forma: el puesto 0 o 1000, un nombre con un salto de línea o de más de 30 caracteres y un desde de 0 se señalan, y no se guardan")]
    public void Datos_fuera_de_forma()
    {
        Assert.Equal(["Numero"], DatosPuesto.Validar(0, "Comedor", 1));
        Assert.Equal(["Numero"], DatosPuesto.Validar(1000, "Comedor", 1));
        Assert.Equal(["Nombre"], DatosPuesto.Validar(3, "Comedor\nNorte", 1));
        Assert.Equal(["Nombre"], DatosPuesto.Validar(3, new string('x', 31), 1));
        Assert.Equal(["Desde"], DatosPuesto.Validar(3, "Comedor", 0));
        Assert.Equal(["Desde"], DatosPuesto.Validar(3, "Comedor", DatosPuesto.DesdeMaximo + 1));
        Assert.Empty(DatosPuesto.Validar(null, string.Empty, 1));
        Assert.Throws<ArgumentException>(() => new AlmacenDatosPuesto(_carpeta).Guardar(new DatosPuesto(0, string.Empty, false, 1)));
        Assert.False(File.Exists(Path.Combine(_carpeta, "puesto.json")));
    }

    [Fact(DisplayName = "comprobante-local · Un archivo de datos ilegible: un puesto 5000 o un JSON roto dan los valores por defecto, sin puesto y con el código de siempre")]
    public void Un_archivo_ilegible()
    {
        var ruta = Path.Combine(_carpeta, "puesto.json");
        File.WriteAllText(ruta, """{"numero":5000,"nombre":"Comedor","correlativo":true,"desde":1000}""");
        Assert.Equal(DatosPuesto.PorDefecto, new AlmacenDatosPuesto(_carpeta).Leer());

        File.WriteAllText(ruta, "{ no es json");
        Assert.Equal(DatosPuesto.PorDefecto, new AlmacenDatosPuesto(_carpeta).Leer());
    }

    [Fact(DisplayName = "comprobante-local · Reimpresión con motivo: la impresora simulada escribe el puesto, el número, el comensal y quién habilitó la copia, y la frase antes del pie, que sigue siendo lo último")]
    public void La_impresora_simulada_escribe_el_comprobante()
    {
        var impresora = new ImpresoraSimulada(_carpeta)
        {
            Textos = new TextosComprobante("Comedor de planta", "Gracias por tu visita"),
            Puesto = new DatosPuesto(3, "Comedor Norte", true, 1000),
        };
        var ticket = Guid.NewGuid();

        impresora.Enviar(new ComprobanteAImprimir(Guid.NewGuid(), ticket, 1, Datos(ticket, "Persona sintética 01", 1000), "REIMPRESIÓN · copia 1", "Operador sintético 01"));

        var lineas = File.ReadAllLines(Path.Combine(_carpeta, $"{ticket}-1.txt"));
        Assert.StartsWith("Comedor de planta", lineas[0], StringComparison.Ordinal);
        Assert.Equal("Puesto 3 · Comedor Norte", lineas[1]);
        Assert.Contains("REIMPRESIÓN · copia 1", lineas);
        Assert.Contains("Número: 1000", lineas);
        Assert.Contains("Comensal: Persona sintética 01", lineas);
        Assert.Contains("Habilitó: Operador sintético 01", lineas);
        Assert.Equal(FrasesComprobante.Para(ticket), lineas[^2]);
        Assert.Equal("Gracias por tu visita", lineas[^1]);
    }

    [Fact(DisplayName = "comprobante-local · Un comprobante de antes: sin nombre ni correlativo en sus datos, en pantalla sale sin comensal y con los ocho caracteres de siempre; con ellos, con el comensal, el número, la frase y el operador")]
    public void El_comprobante_en_pantalla_desde_los_datos()
    {
        var ticket = Guid.NewGuid();
        var viejo = ComprobanteEnPantalla.Desde(new ComprobanteAImprimir(Guid.NewGuid(), ticket, 0, Datos(ticket, null, null), null));
        Assert.Null(viejo.Comensal);
        Assert.Equal(ticket.ToString("D")[..8], viejo.Numero);
        Assert.Equal(FrasesComprobante.Para(ticket), viejo.Frase);

        var nuevo = ComprobanteEnPantalla.Desde(new ComprobanteAImprimir(Guid.NewGuid(), ticket, 1, Datos(ticket, "Persona sintética 01", 1000), "REIMPRESIÓN · copia 1", "Operador sintético 01"));
        Assert.Equal("Persona sintética 01", nuevo.Comensal);
        Assert.Equal("1000", nuevo.Numero);
        Assert.Equal("Operador sintético 01", nuevo.Habilito);
    }

    /// <summary>Los datos de impresión como los guarda la confirmación, con o sin el nombre y el correlativo.</summary>
    private static string Datos(Guid ticket, string? nombre, long? numero)
    {
        var datos = new Dictionary<string, object?>
        {
            ["TicketId"] = ticket, ["ConsumptionId"] = Guid.NewGuid(), ["MenuItemId"] = Guid.NewGuid(), ["MenuLabel"] = "Almuerzo común",
            ["BusinessDate"] = "2026-11-11", ["Quantity"] = 1, ["Currency"] = "ARS", ["Scale"] = 2, ["EmployeeChargeMinor"] = 150000,
        };
        if (nombre is not null)
        {
            datos["PersonName"] = nombre;
        }

        if (numero is not null)
        {
            datos["TicketNumber"] = numero.Value;
        }

        return JsonSerializer.Serialize(datos);
    }
}
