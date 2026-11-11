using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El resguardo de la configuración del puesto (configuracion-puesto de resguardar-configuracion, D1 a D3): el .zip con el
/// manifiesto, la versión activa y los ajustes locales; su revisión sin escribir y su recuperación en otro puesto. Sobre dos
/// puestos con la base de la demostración en carpetas temporales.
/// </summary>
public sealed class ResguardoPuestoTests : IDisposable
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static readonly byte[] OtroPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    private readonly string _carpeta = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "incomedor-tests", "resguardo-puesto-" + Guid.NewGuid().ToString("N"))).FullName;

    private readonly List<Puesto> _puestos = [];

    public void Dispose()
    {
        foreach (var puesto in _puestos)
        {
            puesto.Servicio.Dispose();
        }

        Directory.Delete(_carpeta, recursive: true);
    }

    [Fact(DisplayName = "configuracion-puesto · Guardar el resguardo: el .zip tiene el manifiesto, la configuración, el estilo, los textos, la presentación, el logo y la imagen de una opción, sin certificados, con la huella de cada parte")]
    public void Guardar_el_resguardo()
    {
        var a = Personalizado("a");
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(a.Carpeta, "certificados")).FullName, "puesto.pfx"), "secreto sintético");
        var destino = Path.Combine(_carpeta, "exportacion");

        var guardado = a.Resguardo.Guardar(destino, TimeZoneInfo.Utc);

        Assert.Equal(Path.Combine(destino, "INComedor-resguardo-20261106-150000.zip"), guardado.Ruta);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(guardado.Ruta))), guardado.Huella);
        var imagen = "opciones/" + Convert.ToHexString(SHA256.HashData(Png)) + ".png";
        using var zip = ZipFile.OpenRead(guardado.Ruta);
        Assert.Equal(
            new[] { "configuracion.json", "estilo-visual.json", "impresora.json", "manifiesto.json", "marcas/empresa.png", imagen, "presentacion-opciones.json" }
                .Order(StringComparer.Ordinal),
            zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains("certificados", StringComparison.OrdinalIgnoreCase));

        var manifiesto = JsonNode.Parse(Leer(zip, "manifiesto.json"))!;
        Assert.Equal(1, (int)manifiesto["formato"]!);
        Assert.Equal(AdministracionLocal.VersionEsquema, (int)manifiesto["esquema"]!);
        Assert.Equal(1, (int)manifiesto["versionConfiguracion"]!);
        foreach (var parte in manifiesto["partes"]!.AsArray())
        {
            var contenido = Leer(zip, (string)parte!["nombre"]!);
            Assert.Equal(contenido.LongLength, (long)parte["tamano"]!);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(contenido)), (string)parte["sha256"]!);
        }

        Assert.Equal(6, manifiesto["partes"]!.AsArray().Count);
    }

    [Fact(DisplayName = "configuracion-puesto · Revisar un resguardo válido: el resumen dice la versión de origen, cuántas personas, opciones y permisos y qué ajustes trae, sin escribir nada")]
    public void Revisar_un_resguardo_valido()
    {
        var a = Personalizado("valido");
        var guardado = a.Resguardo.Guardar(Path.Combine(_carpeta, "exportacion"), TimeZoneInfo.Utc);

        var revision = a.Resguardo.Revisar(guardado.Ruta);

        Assert.Null(revision.Motivo);
        Assert.Equal(guardado.Huella, revision.Huella);
        var resguardo = revision.Resguardo!;
        var vista = a.Administracion.Leer();
        Assert.Equal(1, resguardo.VersionOrigen);
        Assert.Equal(vista.Personas.Count, resguardo.Personas);
        Assert.Equal(vista.Opciones.Count, resguardo.Opciones);
        Assert.Equal(new EstiloVisual(80, 6), resguardo.Estilo);
        Assert.Equal(new TextosComprobante("Comedor de prueba", "Buen provecho."), resguardo.Textos);
        Assert.True(resguardo.Logos.ContainsKey(LogoPuesto.Empresa));
        Assert.False(resguardo.Logos.ContainsKey(LogoPuesto.Concesionaria));
        Assert.Single(resguardo.Presentacion!);
        Assert.Equal(1, a.Administracion.Leer().VersionConfiguracion);
    }

    [Fact(DisplayName = "configuracion-puesto · Recuperar en otro puesto: una versión nueva con la configuración del resguardo, y su estilo, textos, logo y presentación en lugar de los del otro puesto")]
    public void Recuperar_en_otro_puesto()
    {
        var a = Personalizado("origen");
        var quitada = a.Administracion.Leer().Opciones[^1];
        a.Administracion.GuardarOpciones([new EdicionOpcion(quitada.Id, quitada.Nombre, quitada.ServicioId, quitada.ImporteUnidadesMenores,
            quitada.Cargas.Select(c => new CargaEditada(c.CategoriaId, c.Unidades)).ToList(), [], Quitar: true)]);
        var guardado = a.Resguardo.Guardar(Path.Combine(_carpeta, "exportacion"), TimeZoneInfo.Utc);
        var b = Nuevo("destino");
        b.Logos.Guardar(LogoPuesto.Concesionaria, LogosLocales.Validar("concesionaria.png", OtroPng.Length, OtroPng).Logo!);
        var otraOpcion = b.Administracion.Leer().Opciones[1].Id;
        b.Presentacion.Guardar(otraOpcion, new PresentacionOpcion(PresentacionOpcion.Paleta[0], null, 25, true), null);

        var recuperacion = b.Resguardo.Recuperar(b.Resguardo.Revisar(guardado.Ruta).Resguardo!);

        Assert.Equal(TipoGuardado.Aplicado, recuperacion.Base.Tipo);
        Assert.True(recuperacion.AjustesRecuperados);
        Assert.Equal(2, b.Administracion.Leer().VersionConfiguracion);
        Assert.DoesNotContain(b.Administracion.Leer().Opciones, o => o.Id == quitada.Id);
        Assert.Equal(new EstiloVisual(80, 6), new AlmacenEstiloVisual(b.Carpeta).Leer());
        Assert.Equal(new TextosComprobante("Comedor de prueba", "Buen provecho."), new AlmacenImpresora(b.Carpeta).Leer());
        Assert.Equal(Png, new LogosLocales(b.Carpeta).Actual(LogoPuesto.Empresa)!.Contenido);
        Assert.Null(new LogosLocales(b.Carpeta).Actual(LogoPuesto.Concesionaria));
        Assert.Equal(a.Presentacion.Leer(), b.Presentacion.Leer());
        Assert.NotNull(b.Presentacion.UriImagen(b.Presentacion.Leer().Values.Single()));
    }

    [Fact(DisplayName = "configuracion-puesto · Un resguardo alterado o de otro esquema: un archivo que no es un zip, una parte cambiada, una desconocida o con .., otro formato, otro esquema, un ajuste inválido y una configuración sin una tabla se rechazan con su motivo")]
    public void Un_resguardo_alterado()
    {
        var a = Personalizado("alterado");
        var valido = a.Resguardo.Guardar(Path.Combine(_carpeta, "exportacion"), TimeZoneInfo.Utc).Ruta;
        var noZip = Path.Combine(_carpeta, "no-es-un-zip.zip");
        File.WriteAllText(noZip, "no soy un zip");

        var casos = new (string Archivo, string Motivo)[]
        {
            (noZip, MotivosResguardo.NoEsUnZip),
            (Rehacer(valido, "parte-cambiada", p => p["configuracion.json"] = [.. p["configuracion.json"], (byte)' '], recalcular: false), MotivosResguardo.HuellaDistinta),
            (Rehacer(valido, "parte-desconocida", p => p["otra.txt"] = "hola"u8.ToArray()), MotivosResguardo.ParteDesconocida),
            (Rehacer(valido, "con-puntos", p => p["../estilo-visual.json"] = p["estilo-visual.json"]), MotivosResguardo.ParteDesconocida),
            (Rehacer(valido, "otro-formato", _ => { }, m => m["formato"] = 2), MotivosResguardo.OtroFormato),
            (Rehacer(valido, "otro-esquema", _ => { }, m => m["esquema"] = AdministracionLocal.VersionEsquema - 1), MotivosResguardo.OtroEsquema),
            (Rehacer(valido, "ajuste-invalido", p => p["estilo-visual.json"] = """{"opacidad":200,"desenfoque":4}"""u8.ToArray()), MotivosResguardo.AjusteInvalido),
            (Rehacer(valido, "sin-tabla", p => p["configuracion.json"] = SinTabla(p["configuracion.json"])), CodigosAdministracion.ResguardoInvalido),
        };

        foreach (var (archivo, motivo) in casos)
        {
            var revision = a.Resguardo.Revisar(archivo);
            Assert.True(motivo == revision.Motivo, $"{Path.GetFileName(archivo)}: se esperaba {motivo} y fue {revision.Motivo}.");
            Assert.Null(revision.Resguardo);
        }

        Assert.Equal(1, a.Administracion.Leer().VersionConfiguracion);
    }

    private Puesto Nuevo(string nombre)
    {
        var puesto = new Puesto(Path.Combine(_carpeta, nombre));
        _puestos.Add(puesto);
        return puesto;
    }

    /// <summary>Un puesto con un estilo, textos, el logo de la empresa y una opción con imagen propios.</summary>
    private Puesto Personalizado(string nombre)
    {
        var puesto = Nuevo(nombre);
        puesto.Estilo.Guardar(new EstiloVisual(80, 6));
        puesto.Impresora.Guardar(new TextosComprobante("Comedor de prueba", "Buen provecho."));
        puesto.Logos.Guardar(LogoPuesto.Empresa, LogosLocales.Validar("empresa.png", Png.Length, Png).Logo!);
        puesto.Presentacion.Guardar(puesto.Administracion.Leer().Opciones[0].Id, new PresentacionOpcion(null, null, 50, false),
            new LogoElegido(".png", "image/png", "PNG", Png));
        return puesto;
    }

    private static byte[] Leer(ZipArchive zip, string nombre)
    {
        using var flujo = zip.GetEntry(nombre)!.Open();
        using var memoria = new MemoryStream();
        flujo.CopyTo(memoria);
        return memoria.ToArray();
    }

    private static byte[] SinTabla(byte[] configuracion)
    {
        var json = JsonNode.Parse(configuracion)!.AsObject();
        json.Remove("tbComedorFranjasOpcionesMenu");
        return Encoding.UTF8.GetBytes(json.ToJsonString());
    }

    /// <summary>
    /// Otro .zip con las partes del válido cambiadas; con <paramref name="recalcular"/>, el manifiesto se rehace con sus
    /// tamaños y huellas, como lo haría quien altera el archivo a sabiendas.
    /// </summary>
    private string Rehacer(string valido, string nombre, Action<Dictionary<string, byte[]>> cambiar, Action<JsonObject>? manifiesto = null, bool recalcular = true)
    {
        var partes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using (var zip = ZipFile.OpenRead(valido))
        {
            foreach (var entrada in zip.Entries)
            {
                partes[entrada.FullName] = Leer(zip, entrada.FullName);
            }
        }

        var original = JsonNode.Parse(partes["manifiesto.json"])!.AsObject();
        partes.Remove("manifiesto.json");
        cambiar(partes);
        if (recalcular)
        {
            original["partes"] = new JsonArray(partes.Select(p => (JsonNode)new JsonObject
            {
                ["nombre"] = p.Key,
                ["tamano"] = p.Value.LongLength,
                ["sha256"] = Convert.ToHexString(SHA256.HashData(p.Value)),
            }).ToArray());
        }

        manifiesto?.Invoke(original);
        var ruta = Path.Combine(_carpeta, nombre + ".zip");
        using (var zip = ZipFile.Open(ruta, ZipArchiveMode.Create))
        {
            foreach (var (parte, contenido) in partes.Prepend(new("manifiesto.json", Encoding.UTF8.GetBytes(original.ToJsonString()))))
            {
                using var flujo = zip.CreateEntry(parte).Open();
                flujo.Write(contenido);
            }
        }

        return ruta;
    }

    private sealed class Puesto
    {
        public Puesto(string carpeta)
        {
            Carpeta = Directory.CreateDirectory(carpeta).FullName;
            Servicio = new ServicioAtencion(Path.Combine(Carpeta, "incomedor.db"), new RelojManual("2026-11-06T15:00:00Z"), new ImpresoraDePrueba());
            Assert.Equal(Demostracion.EstadoPreparacion.Lista, Servicio.Preparar());
            Estilo = new AlmacenEstiloVisual(Carpeta);
            Impresora = new AlmacenImpresora(Carpeta);
            Logos = new LogosLocales(Carpeta);
            Presentacion = new AlmacenPresentacionOpciones(Carpeta);
            Resguardo = new ResguardoPuesto(Administracion, Estilo, Impresora, Logos, Presentacion);
        }

        public string Carpeta { get; }

        public ServicioAtencion Servicio { get; }

        public AdministracionLocal Administracion => Servicio.Administracion!;

        public AlmacenEstiloVisual Estilo { get; }

        public AlmacenImpresora Impresora { get; }

        public LogosLocales Logos { get; }

        public AlmacenPresentacionOpciones Presentacion { get; }

        public ResguardoPuesto Resguardo { get; }
    }
}
