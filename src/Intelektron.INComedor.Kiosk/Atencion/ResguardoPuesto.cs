using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>Los motivos por los que un resguardo no se recupera (D2 de resguardar-configuracion), en el cartel y la bitácora.</summary>
public static class MotivosResguardo
{
    public const string Ilegible = "UNREADABLE";
    public const string NoEsUnZip = "NOT_A_ZIP";
    public const string DemasiadoGrande = "TOO_LARGE";
    public const string ParteDesconocida = "UNKNOWN_PART";
    public const string HuellaDistinta = "HASH_MISMATCH";
    public const string OtroFormato = "OTHER_FORMAT";
    public const string OtroEsquema = "OTHER_SCHEMA";
    public const string AjusteInvalido = "INVALID_LOCAL_FILE";
    public const string AjustesNoEscritos = "LOCAL_FILES_NOT_WRITTEN";
    public const string BaseSinRespuesta = "DATABASE_ERROR";
}

/// <summary>El resguardo escrito: su ruta, la huella SHA-256 del archivo, la versión que lleva y sus partes (D1).</summary>
public sealed record ResguardoGuardado(string Ruta, string Huella, int VersionConfiguracion, IReadOnlyList<string> Partes);

/// <summary>Un resguardo válido, leído entero en memoria: lo que dice el resumen y lo que se recupera (D2).</summary>
public sealed record ResguardoRevisado(
    string Huella,
    int VersionOrigen,
    DateTimeOffset FechaUtc,
    int Personas,
    int Opciones,
    int Permisos,
    IReadOnlyList<string> Partes,
    string Configuracion,
    EstiloVisual? Estilo,
    TextosComprobante? Textos,
    IReadOnlyDictionary<LogoPuesto, LogoElegido> Logos,
    IReadOnlyDictionary<Guid, PresentacionOpcion>? Presentacion,
    IReadOnlyList<LogoElegido> Imagenes);

/// <summary>La revisión de un archivo: el resguardo válido, o el motivo por el que no se recupera, con la huella si se leyó.</summary>
public sealed record RevisionResguardo(string? Huella, ResguardoRevisado? Resguardo, string? Motivo);

/// <summary>El resultado de recuperar, todo o nada (D3): aplicado con la versión nueva, o rechazado con su motivo y nada cambiado.</summary>
public sealed record RecuperacionResguardo(ResultadoGuardado Base);

/// <summary>
/// El resguardo de la configuración del puesto (resguardar-configuracion, D1 a D3): un .zip con el manifiesto, la versión
/// activa de la base y los ajustes locales que la acompañan, sin certificados, consumos ni lo que es del equipo. La revisión
/// lee el archivo una sola vez y entero, con topes, y lo que recupera es lo que revisó (OWASP: la configuración que llega al
/// puesto).
/// </summary>
public sealed partial class ResguardoPuesto
{
    public const int Formato = 1;
    public const long TamanoMaximo = 50L * 1024 * 1024;
    public const long TamanoMaximoParte = 20L * 1024 * 1024;
    public const int PartesMaximas = 500;

    private const string Manifiesto = "manifiesto.json";
    private const string ParteConfiguracion = "configuracion.json";
    private const string ParteEstilo = "estilo-visual.json";
    private const string ParteImpresora = "impresora.json";
    private const string PartePresentacion = "presentacion-opciones.json";
    private const string CarpetaMarcas = "marcas/";
    private const string CarpetaOpciones = "opciones/";

    private static readonly UTF8Encoding Utf8Estricto = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly AdministracionLocal _administracion;
    private readonly AlmacenEstiloVisual _estilo;
    private readonly AlmacenImpresora _impresora;
    private readonly LogosLocales _logos;
    private readonly AlmacenPresentacionOpciones _presentacion;

    public ResguardoPuesto(
        AdministracionLocal administracion, AlmacenEstiloVisual estilo, AlmacenImpresora impresora, LogosLocales logos,
        AlmacenPresentacionOpciones presentacion)
    {
        _administracion = administracion;
        _estilo = estilo;
        _impresora = impresora;
        _logos = logos;
        _presentacion = presentacion;
    }

    /// <summary>Sólo estos nombres son partes de un resguardo: ninguna carpeta más, ninguna ruta con «..» (D2).</summary>
    [GeneratedRegex(@"\A(manifiesto\.json|configuracion\.json|estilo-visual\.json|impresora\.json|presentacion-opciones\.json|marcas/(empresa|concesionaria)\.(png|webp|svg)|opciones/[0-9A-F]{64}\.(png|webp|svg))\z")]
    private static partial Regex NombreDeParte();

    /// <summary>
    /// Escribe <c>INComedor-resguardo-AAAAMMDD-HHmmss.zip</c> en la carpeta, por un temporal (D1): la versión activa, el estilo,
    /// los textos del comprobante, la presentación de las opciones con sus imágenes y los logos propios, más el manifiesto.
    /// </summary>
    public ResguardoGuardado Guardar(string carpeta, TimeZoneInfo zona)
    {
        var configuracion = _administracion.LeerConfiguracion();
        var partes = new List<(string Nombre, byte[] Contenido)>
        {
            (ParteConfiguracion, Encoding.UTF8.GetBytes(configuracion.Json)),
            (ParteEstilo, Encoding.UTF8.GetBytes(AlmacenEstiloVisual.Serializar(_estilo.Leer()))),
            (ParteImpresora, Encoding.UTF8.GetBytes(AlmacenImpresora.Serializar(_impresora.Leer()))),
        };
        foreach (var logo in new[] { LogoPuesto.Empresa, LogoPuesto.Concesionaria })
        {
            if (_logos.Actual(logo) is { } actual)
            {
                partes.Add((CarpetaMarcas + NombreLogo(logo) + actual.Extension, actual.Contenido));
            }
        }

        // La presentación como se dibuja: una imagen que ya no valida no viaja, y la opción queda sin ella.
        var presentaciones = new Dictionary<Guid, PresentacionOpcion>();
        foreach (var (opcion, presentacion) in _presentacion.Leer())
        {
            var imagen = presentacion.Imagen is { } nombre ? _presentacion.ImagenDe(nombre) : null;
            presentaciones[opcion] = imagen is null ? presentacion with { Imagen = null } : presentacion;
            var parte = CarpetaOpciones + presentacion.Imagen;
            if (imagen is not null && partes.All(p => p.Nombre != parte))
            {
                partes.Add((parte, imagen.Contenido));
            }
        }

        partes.Add((PartePresentacion, Encoding.UTF8.GetBytes(AlmacenPresentacionOpciones.Serializar(presentaciones))));

        var ahora = _administracion.Reloj.GetUtcNow();
        var manifiesto = JsonSerializer.SerializeToUtf8Bytes(new
        {
            formato = Formato,
            esquema = configuracion.VersionEsquema,
            versionConfiguracion = configuracion.VersionConfiguracion,
            fechaUtc = ahora.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            partes = partes.Select(p => new { nombre = p.Nombre, tamano = p.Contenido.LongLength, sha256 = Huella(p.Contenido) }),
        }, new JsonSerializerOptions { WriteIndented = true });

        Directory.CreateDirectory(carpeta);
        var raiz = "INComedor-resguardo-" + TimeZoneInfo.ConvertTime(ahora, zona).ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var ruta = Path.Combine(carpeta, raiz + ".zip");
        for (var n = 2; File.Exists(ruta); n++)
        {
            ruta = Path.Combine(carpeta, $"{raiz}-{n}.zip");
        }

        var temporal = ruta + ".tmp";
        try
        {
            using (var flujo = new FileStream(temporal, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(flujo, ZipArchiveMode.Create))
            {
                foreach (var (nombre, contenido) in partes.Prepend((Manifiesto, manifiesto)))
                {
                    using var entrada = zip.CreateEntry(nombre, CompressionLevel.Optimal).Open();
                    entrada.Write(contenido);
                }
            }

            File.Move(temporal, ruta);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            BorrarSiSePuede(temporal);
            throw;
        }

        return new ResguardoGuardado(ruta, Huella(File.ReadAllBytes(ruta)), configuracion.VersionConfiguracion, [.. partes.Select(p => p.Nombre)]);
    }

    /// <summary>
    /// Lee el archivo entero, una vez, y lo valida sin escribir nada (D2): un zip de hasta 50 MB, sólo partes conocidas de
    /// hasta 20 MB, el formato y el esquema de este puesto, las huellas del manifiesto, la configuración con sus tablas y sin
    /// códigos de otra persona, y cada ajuste local con las reglas de su almacén.
    /// </summary>
    public RevisionResguardo Revisar(string archivo)
    {
        byte[] bytes;
        try
        {
            if (new FileInfo(archivo) is not { Exists: true } info)
            {
                return Rechazo(null, MotivosResguardo.Ilegible);
            }

            if (info.Length > TamanoMaximo)
            {
                return Rechazo(null, MotivosResguardo.DemasiadoGrande);
            }

            bytes = File.ReadAllBytes(archivo);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Rechazo(null, MotivosResguardo.Ilegible);
        }

        var huella = Huella(bytes);
        if (bytes.LongLength > TamanoMaximo)
        {
            return Rechazo(huella, MotivosResguardo.DemasiadoGrande);
        }

        if (LeerPartes(bytes, out var partes) is { } sinPartes)
        {
            return Rechazo(huella, sinPartes);
        }

        if (LeerManifiesto(partes, out var version, out var fecha) is { } sinManifiesto)
        {
            return Rechazo(huella, sinManifiesto);
        }

        if (!partes.TryGetValue(ParteConfiguracion, out var crudo) || Texto(crudo) is not { } configuracion)
        {
            return Rechazo(huella, CodigosAdministracion.ResguardoInvalido);
        }

        if (LeerAjustes(partes) is not { } ajustes)
        {
            return Rechazo(huella, MotivosResguardo.AjusteInvalido);
        }

        var revision = _administracion.RevisarConfiguracion(configuracion);
        if (revision.Motivo is { } motivo)
        {
            return Rechazo(huella, motivo);
        }

        return new RevisionResguardo(huella, new ResguardoRevisado(
            huella, version, fecha, revision.Personas, revision.Opciones, revision.Permisos,
            [.. partes.Keys.Order(StringComparer.Ordinal)], configuracion,
            ajustes.Estilo, ajustes.Textos, ajustes.Logos, ajustes.Presentacion, [.. ajustes.Imagenes.Values]), null);
    }

    /// <summary>
    /// Recupera lo revisado, todo o nada (D3): copia en memoria los ajustes locales vigentes, escribe los del resguardo (los
    /// que no trae vuelven al de ejemplo) y recién entonces crea la versión nueva, que vuelve a validar todo en su transacción.
    /// Si un archivo no se escribe o la base no lo acepta, los ajustes vuelven a los de antes y la versión activa no cambia.
    /// </summary>
    public RecuperacionResguardo Recuperar(ResguardoRevisado resguardo)
    {
        var antes = Copiar();
        try
        {
            Escribir(resguardo.Estilo, resguardo.Textos, resguardo.Logos, resguardo.Presentacion ?? new Dictionary<Guid, PresentacionOpcion>(), resguardo.Imagenes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Reponer(antes);
            var actual = _administracion.Leer().VersionConfiguracion;
            return new RecuperacionResguardo(new ResultadoGuardado(TipoGuardado.Rechazado, actual, MotivosResguardo.AjustesNoEscritos, []));
        }

        ResultadoGuardado resultado;
        try
        {
            resultado = _administracion.RecuperarVersion(resguardo.Configuracion);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            Reponer(antes);
            throw;
        }

        if (resultado.Tipo != TipoGuardado.Aplicado)
        {
            Reponer(antes);
        }

        return new RecuperacionResguardo(resultado);
    }

    /// <summary>Los ajustes locales vigentes, como se dibujan, para volver a ellos si la recuperación no termina.</summary>
    private sealed record AjustesVigentes(
        EstiloVisual Estilo,
        TextosComprobante Textos,
        Dictionary<LogoPuesto, LogoElegido> Logos,
        Dictionary<Guid, PresentacionOpcion> Presentacion,
        List<LogoElegido> Imagenes);

    private AjustesVigentes Copiar()
    {
        var logos = new Dictionary<LogoPuesto, LogoElegido>();
        foreach (var logo in new[] { LogoPuesto.Empresa, LogoPuesto.Concesionaria })
        {
            if (_logos.Actual(logo) is { } actual)
            {
                logos.Add(logo, actual);
            }
        }

        var presentacion = new Dictionary<Guid, PresentacionOpcion>(_presentacion.Leer());
        var imagenes = presentacion.Values.Select(p => p.Imagen is { } nombre ? _presentacion.ImagenDe(nombre) : null).OfType<LogoElegido>().ToList();
        return new AjustesVigentes(_estilo.Leer(), _impresora.Leer(), logos, presentacion, imagenes);
    }

    private void Escribir(
        EstiloVisual? estilo, TextosComprobante? textos, IReadOnlyDictionary<LogoPuesto, LogoElegido> logos,
        IReadOnlyDictionary<Guid, PresentacionOpcion> presentacion, IEnumerable<LogoElegido> imagenes)
    {
        if (estilo is not null)
        {
            _estilo.Guardar(estilo);
        }
        else
        {
            _estilo.VolverAlPorDefecto();
        }

        if (textos is not null)
        {
            _impresora.Guardar(textos);
        }
        else
        {
            _impresora.VolverAlPorDefecto();
        }

        foreach (var logo in new[] { LogoPuesto.Empresa, LogoPuesto.Concesionaria })
        {
            if (logos.TryGetValue(logo, out var elegido))
            {
                _logos.Guardar(logo, elegido);
            }
            else if (_logos.Actual(logo) is not null)
            {
                _logos.VolverAlDeEjemplo(logo);
            }
        }

        _presentacion.Reemplazar(presentacion, imagenes);
    }

    /// <summary>
    /// Vuelve a los ajustes copiados. Si tampoco se pueden escribir, quedan como estén: la carpeta del puesto no deja escribir,
    /// y la versión activa, que es lo que la atención usa para autorizar, no cambió.
    /// </summary>
    private void Reponer(AjustesVigentes antes)
    {
        try
        {
            Escribir(antes.Estilo, antes.Textos, antes.Logos, antes.Presentacion, antes.Imagenes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"INComedor: no se pudieron reponer los ajustes locales ({e.GetType().Name}).");
        }
    }

    private static RevisionResguardo Rechazo(string? huella, string motivo) => new(huella, null, motivo);

    private static string? LeerPartes(byte[] bytes, out Dictionary<string, byte[]> partes)
    {
        partes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read);
            if (zip.Entries.Count > PartesMaximas)
            {
                return MotivosResguardo.DemasiadoGrande;
            }

            long total = 0;
            foreach (var entrada in zip.Entries)
            {
                if (!NombreDeParte().IsMatch(entrada.FullName) || partes.ContainsKey(entrada.FullName))
                {
                    return MotivosResguardo.ParteDesconocida;
                }

                if (entrada.Length > TamanoMaximoParte)
                {
                    return MotivosResguardo.DemasiadoGrande;
                }

                // El tamaño declarado puede mentir: se lee con el tope, sin creerle.
                using var flujo = entrada.Open();
                if (LeerAcotado(flujo, TamanoMaximoParte) is not { } contenido || (total += contenido.LongLength) > TamanoMaximo)
                {
                    return MotivosResguardo.DemasiadoGrande;
                }

                partes.Add(entrada.FullName, contenido);
            }
        }
        catch (InvalidDataException)
        {
            return MotivosResguardo.NoEsUnZip;
        }

        return null;
    }

    /// <summary>El manifiesto: formato y esquema de este puesto, y exactamente las otras partes con su tamaño y su huella.</summary>
    private static string? LeerManifiesto(Dictionary<string, byte[]> partes, out int version, out DateTimeOffset fecha)
    {
        version = 0;
        fecha = default;
        if (!partes.Remove(Manifiesto, out var crudo))
        {
            return MotivosResguardo.OtroFormato;
        }

        try
        {
            using var documento = JsonDocument.Parse(crudo);
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object || !Entero(raiz, "formato", out var formato) || formato != Formato)
            {
                return MotivosResguardo.OtroFormato;
            }

            if (!Entero(raiz, "esquema", out var esquema) || esquema != AdministracionLocal.VersionEsquema)
            {
                return MotivosResguardo.OtroEsquema;
            }

            if (!Entero(raiz, "versionConfiguracion", out version) || version < 1
                || !raiz.TryGetProperty("fechaUtc", out var textoFecha) || textoFecha.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(textoFecha.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out fecha)
                || !raiz.TryGetProperty("partes", out var listadas) || listadas.ValueKind != JsonValueKind.Array)
            {
                return MotivosResguardo.OtroFormato;
            }

            var vistas = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parte in listadas.EnumerateArray())
            {
                if (parte.ValueKind != JsonValueKind.Object
                    || !parte.TryGetProperty("nombre", out var nombre) || nombre.ValueKind != JsonValueKind.String
                    || !parte.TryGetProperty("tamano", out var tamano) || tamano.ValueKind != JsonValueKind.Number || !tamano.TryGetInt64(out var largo)
                    || !parte.TryGetProperty("sha256", out var sha) || sha.ValueKind != JsonValueKind.String
                    || !partes.TryGetValue(nombre.GetString()!, out var contenido) || !vistas.Add(nombre.GetString()!)
                    || contenido.LongLength != largo || !string.Equals(Huella(contenido), sha.GetString(), StringComparison.OrdinalIgnoreCase))
                {
                    return MotivosResguardo.HuellaDistinta;
                }
            }

            return vistas.Count == partes.Count ? null : MotivosResguardo.HuellaDistinta;
        }
        catch (JsonException)
        {
            return MotivosResguardo.OtroFormato;
        }
    }

    private sealed record Ajustes(
        EstiloVisual? Estilo,
        TextosComprobante? Textos,
        Dictionary<LogoPuesto, LogoElegido> Logos,
        Dictionary<Guid, PresentacionOpcion>? Presentacion,
        Dictionary<string, LogoElegido> Imagenes);

    /// <summary>Cada ajuste local con las reglas de su almacén; null si alguno no las pasa (D2).</summary>
    private static Ajustes? LeerAjustes(Dictionary<string, byte[]> partes)
    {
        EstiloVisual? estilo = null;
        if (partes.TryGetValue(ParteEstilo, out var crudoEstilo)
            && (estilo = Texto(crudoEstilo) is { } textoEstilo ? AlmacenEstiloVisual.Interpretar(textoEstilo) : null) is null)
        {
            return null;
        }

        TextosComprobante? textos = null;
        if (partes.TryGetValue(ParteImpresora, out var crudoTextos)
            && (textos = Texto(crudoTextos) is { } textoTextos ? AlmacenImpresora.Interpretar(textoTextos) : null) is null)
        {
            return null;
        }

        var logos = new Dictionary<LogoPuesto, LogoElegido>();
        var imagenes = new Dictionary<string, LogoElegido>(StringComparer.Ordinal);
        foreach (var (nombre, contenido) in partes)
        {
            if (nombre.StartsWith(CarpetaMarcas, StringComparison.Ordinal))
            {
                var logo = nombre.StartsWith(CarpetaMarcas + NombreLogo(LogoPuesto.Empresa) + ".", StringComparison.Ordinal)
                    ? LogoPuesto.Empresa
                    : LogoPuesto.Concesionaria;
                if (logos.ContainsKey(logo) || LogosLocales.Validar(nombre, contenido.LongLength, contenido).Logo is not { } elegido)
                {
                    return null;
                }

                logos.Add(logo, elegido);
            }
            else if (nombre.StartsWith(CarpetaOpciones, StringComparison.Ordinal))
            {
                var archivo = nombre[CarpetaOpciones.Length..];
                if (LogosLocales.Validar(archivo, contenido.LongLength, contenido).Logo is not { } imagen
                    || imagen.Sha256 != Path.GetFileNameWithoutExtension(archivo))
                {
                    return null;
                }

                imagenes.Add(archivo, imagen);
            }
        }

        Dictionary<Guid, PresentacionOpcion>? presentacion = null;
        if (partes.TryGetValue(PartePresentacion, out var crudoPresentacion)
            && (presentacion = Texto(crudoPresentacion) is { } textoPresentacion ? LeerPresentacion(textoPresentacion, imagenes) : null) is null)
        {
            return null;
        }

        return new Ajustes(estilo, textos, logos, presentacion, imagenes);
    }

    /// <summary>
    /// La presentación con la forma que escribe su almacén, sin tolerancias: cada opción por su id, un color de la paleta o una
    /// imagen que viaja en el resguardo, una transparencia de los pasos y el precio visible; cualquier otra cosa la invalida.
    /// </summary>
    private static Dictionary<Guid, PresentacionOpcion>? LeerPresentacion(string contenido, IReadOnlyDictionary<string, LogoElegido> imagenes)
    {
        if (contenido.Length > AlmacenPresentacionOpciones.TamanoMaximo)
        {
            return null;
        }

        try
        {
            using var documento = JsonDocument.Parse(contenido);
            if (documento.RootElement.ValueKind != JsonValueKind.Object
                || !documento.RootElement.TryGetProperty("opciones", out var opciones) || opciones.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var leidas = new Dictionary<Guid, PresentacionOpcion>();
            foreach (var opcion in opciones.EnumerateObject())
            {
                if (!Guid.TryParse(opcion.Name, out var id) || leidas.ContainsKey(id) || opcion.Value.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                string? color = null;
                string? imagen = null;
                var transparencia = 0;
                var precio = true;
                foreach (var campo in opcion.Value.EnumerateObject())
                {
                    var valor = campo.Value;
                    switch (campo.Name)
                    {
                        case "color" or "imagen" when valor.ValueKind == JsonValueKind.Null:
                            break;
                        case "color" when valor.ValueKind == JsonValueKind.String && PresentacionOpcion.Paleta.Contains(valor.GetString()!):
                            color = valor.GetString();
                            break;
                        case "imagen" when valor.ValueKind == JsonValueKind.String && imagenes.ContainsKey(valor.GetString()!):
                            imagen = valor.GetString();
                            break;
                        case "transparencia" when valor.ValueKind == JsonValueKind.Number && valor.TryGetInt32(out var paso)
                            && PresentacionOpcion.Transparencias.Contains(paso):
                            transparencia = paso;
                            break;
                        case "precioVisible" when valor.ValueKind is JsonValueKind.True or JsonValueKind.False:
                            precio = valor.GetBoolean();
                            break;
                        default:
                            return null;
                    }
                }

                leidas.Add(id, new PresentacionOpcion(imagen is null ? color : null, imagen, transparencia, precio));
            }

            return leidas;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool Entero(JsonElement raiz, string nombre, out int valor)
    {
        valor = 0;
        return raiz.TryGetProperty(nombre, out var propiedad) && propiedad.ValueKind == JsonValueKind.Number && propiedad.TryGetInt32(out valor);
    }

    private static string? Texto(byte[] contenido)
    {
        try
        {
            return Utf8Estricto.GetString(contenido);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static byte[]? LeerAcotado(Stream flujo, long tope)
    {
        using var memoria = new MemoryStream();
        var buffer = new byte[81920];
        int leidos;
        while ((leidos = flujo.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (memoria.Length + leidos > tope)
            {
                return null;
            }

            memoria.Write(buffer, 0, leidos);
        }

        return memoria.ToArray();
    }

    private static string NombreLogo(LogoPuesto logo) => logo == LogoPuesto.Empresa ? "empresa" : "concesionaria";

    private static string Huella(byte[] contenido) => Convert.ToHexString(SHA256.HashData(contenido));

    private static void BorrarSiSePuede(string ruta)
    {
        try
        {
            File.Delete(ruta);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Sin escritura tampoco se puede borrar: queda un temporal que nadie lee.
        }
    }
}
