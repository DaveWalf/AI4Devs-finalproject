using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Lector;

/// <summary>El medio de identificación (D3 de elegir-medio-de-identificacion): el teclado de siempre, el simulador interno o un puerto serie.</summary>
public enum MedioIdentificacion
{
    Teclado,
    Simulador,
    PuertoSerie,
}

/// <summary>El byte o los bytes que cierran una lectura (D4).</summary>
public enum TerminadorLector
{
    CrLf,
    Cr,
    Lf,
    Tab,
}

/// <summary>La paridad del puerto serie.</summary>
public enum ParidadLector
{
    Ninguna,
    Par,
    Impar,
}

/// <summary>
/// La configuración del lector (D3): el medio, el puerto y su formato, el terminador y el relleno de dígitos. Por defecto,
/// el teclado, 9600 8N1, CR LF y sin relleno: un puesto sin lector se comporta como siempre.
/// </summary>
public sealed partial record ConfiguracionLector(
    MedioIdentificacion Medio,
    string? Puerto,
    int Velocidad,
    int BitsDatos,
    ParidadLector Paridad,
    int BitsParada,
    TerminadorLector Terminador,
    int Relleno)
{
    public const int RellenoMaximo = 20;
    public static readonly int[] Velocidades = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];

    public static readonly ConfiguracionLector PorDefecto = new(
        MedioIdentificacion.Teclado, null, 9600, 8, ParidadLector.Ninguna, 1, TerminadorLector.CrLf, 0);

    [GeneratedRegex(@"\ACOM[1-9][0-9]{0,2}\z")]
    private static partial Regex NombreDePuerto();

    /// <summary>
    /// Los campos que no tienen forma, en el orden de la pestaña: Puerto (COM1 a COM999; obligatorio con el puerto serie),
    /// Velocidad, BitsDatos (7 u 8), BitsParada (1 o 2) y Relleno (0 a 20).
    /// </summary>
    public static IReadOnlyList<string> Validar(ConfiguracionLector c)
    {
        var campos = new List<string>();
        if (c.Puerto is { } puerto ? !NombreDePuerto().IsMatch(puerto) : c.Medio == MedioIdentificacion.PuertoSerie)
        {
            campos.Add("Puerto");
        }

        if (!Velocidades.Contains(c.Velocidad))
        {
            campos.Add("Velocidad");
        }

        if (c.BitsDatos is not (7 or 8))
        {
            campos.Add("BitsDatos");
        }

        if (c.BitsParada is not (1 or 2))
        {
            campos.Add("BitsParada");
        }

        if (c.Relleno is < 0 or > RellenoMaximo)
        {
            campos.Add("Relleno");
        }

        return campos;
    }

    /// <summary>Los bytes del terminador, para el simulador interno.</summary>
    public byte[] BytesDelTerminador => Terminador switch
    {
        TerminadorLector.Cr => [0x0D],
        TerminadorLector.Lf => [0x0A],
        TerminadorLector.Tab => [0x09],
        _ => [0x0D, 0x0A],
    };

    /// <summary>El formato como lo dice la pestaña: «9600 8N1».</summary>
    public string Formato => Velocidad.ToString(CultureInfo.InvariantCulture) + " " + BitsDatos.ToString(CultureInfo.InvariantCulture)
        + (Paridad switch { ParidadLector.Par => "E", ParidadLector.Impar => "O", _ => "N" }) + BitsParada.ToString(CultureInfo.InvariantCulture);
}

/// <summary><c>lector.json</c> de la carpeta de datos, validado al leer y al guardar, con escritura atómica (D3). Del equipo.</summary>
public sealed class AlmacenLector
{
    public const string Nombre = "lector.json";

    public AlmacenLector(string carpetaDatos)
    {
        CarpetaDatos = carpetaDatos;
    }

    public string CarpetaDatos { get; }

    private string Ruta => Path.Combine(CarpetaDatos, Nombre);

    /// <summary>Lo guardado; sin archivo, ilegible o fuera de forma, lo de fábrica: el teclado.</summary>
    public ConfiguracionLector Leer()
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(Ruta));
            var raiz = documento.RootElement;
            var configuracion = new ConfiguracionLector(
                Texto(raiz, "medio") switch
                {
                    "KEYBOARD" => MedioIdentificacion.Teclado,
                    "SIMULATOR" => MedioIdentificacion.Simulador,
                    "SERIAL_PORT" => MedioIdentificacion.PuertoSerie,
                    _ => throw new JsonException("medio"),
                },
                raiz.GetProperty("puerto").ValueKind == JsonValueKind.Null ? null : Texto(raiz, "puerto"),
                raiz.GetProperty("velocidad").GetInt32(),
                raiz.GetProperty("bitsDatos").GetInt32(),
                Texto(raiz, "paridad") switch
                {
                    "NONE" => ParidadLector.Ninguna,
                    "EVEN" => ParidadLector.Par,
                    "ODD" => ParidadLector.Impar,
                    _ => throw new JsonException("paridad"),
                },
                raiz.GetProperty("bitsParada").GetInt32(),
                Texto(raiz, "terminador") switch
                {
                    "CRLF" => TerminadorLector.CrLf,
                    "CR" => TerminadorLector.Cr,
                    "LF" => TerminadorLector.Lf,
                    "TAB" => TerminadorLector.Tab,
                    _ => throw new JsonException("terminador"),
                },
                raiz.GetProperty("relleno").GetInt32());
            return ConfiguracionLector.Validar(configuracion).Count == 0 ? configuracion : ConfiguracionLector.PorDefecto;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException
                                       or InvalidOperationException or FormatException)
        {
            return ConfiguracionLector.PorDefecto;
        }
    }

    /// <summary>Guarda una configuración ya validada; una fuera de forma es un error de programación.</summary>
    public void Guardar(ConfiguracionLector configuracion)
    {
        if (ConfiguracionLector.Validar(configuracion).Count > 0)
        {
            throw new ArgumentException("La configuración del lector no tiene forma.", nameof(configuracion));
        }

        Directory.CreateDirectory(CarpetaDatos);
        EscrituraAtomica.Escribir(Ruta, JsonSerializer.Serialize(new
        {
            medio = configuracion.Medio switch
            {
                MedioIdentificacion.Simulador => "SIMULATOR",
                MedioIdentificacion.PuertoSerie => "SERIAL_PORT",
                _ => "KEYBOARD",
            },
            puerto = configuracion.Puerto,
            velocidad = configuracion.Velocidad,
            bitsDatos = configuracion.BitsDatos,
            paridad = configuracion.Paridad switch { ParidadLector.Par => "EVEN", ParidadLector.Impar => "ODD", _ => "NONE" },
            bitsParada = configuracion.BitsParada,
            terminador = configuracion.Terminador switch
            {
                TerminadorLector.Cr => "CR",
                TerminadorLector.Lf => "LF",
                TerminadorLector.Tab => "TAB",
                _ => "CRLF",
            },
            relleno = configuracion.Relleno,
        }));
    }

    private static string Texto(JsonElement objeto, string nombre) =>
        objeto.GetProperty(nombre) is { ValueKind: JsonValueKind.String } valor ? valor.GetString()! : throw new JsonException($"{nombre} no es un texto.");
}
