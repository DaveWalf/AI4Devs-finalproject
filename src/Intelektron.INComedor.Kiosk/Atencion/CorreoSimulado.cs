using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>Cómo se cifra la conexión con el servidor de correo (D1 de avisar-por-correo).</summary>
public enum CifradoCorreo
{
    StartTls,
    Tls,
}

/// <summary>Cómo se autentica la casilla de origen con el segundo factor activo; la clave o el token no se guardan (D1).</summary>
public enum SegundoFactorCorreo
{
    ContrasenaDeAplicacion,
    OAuth2,
}

/// <summary>
/// La configuración de los correos (D1): activos o apagados, el cliente del asunto, la casilla de origen, hasta cinco
/// casillas de operador, la del Administrador Principal y la hora de la señal de vida. Ninguna clave ni token.
/// </summary>
public sealed partial record ConfiguracionCorreo(
    bool Activos,
    string Cliente,
    string Remitente,
    string Servidor,
    int Puerto,
    CifradoCorreo Cifrado,
    SegundoFactorCorreo SegundoFactor,
    IReadOnlyList<string> Operadores,
    string? Administrador,
    string HoraSenal)
{
    public const int MaximoOperadores = 5;
    public const int LargoCliente = 60;
    public const int LargoCasilla = 254;

    public static readonly ConfiguracionCorreo PorDefecto = new(
        false, "Cliente", string.Empty, string.Empty, 587, CifradoCorreo.StartTls, SegundoFactorCorreo.ContrasenaDeAplicacion, [], null, "08:00");

    [GeneratedRegex(@"\A[A-Za-z0-9](?:[A-Za-z0-9.-]{0,98}[A-Za-z0-9])?\z")]
    private static partial Regex NombreDeServidor();

    // Sin espacios, comas, punto y coma, comillas ni ángulos: ninguna casilla agrega otra ni corta un encabezado.
    [GeneratedRegex(@"\A[A-Za-z0-9._%+\-]{1,64}@[A-Za-z0-9](?:[A-Za-z0-9-]{0,62}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,62}[A-Za-z0-9])?)+\z")]
    private static partial Regex Casilla();

    [GeneratedRegex(@"\A(?:[01][0-9]|2[0-3]):[0-5][0-9]\z")]
    private static partial Regex Hora();

    /// <summary>La hora de la señal de vida como hora del día.</summary>
    public TimeSpan HoraDeLaSenal => TimeSpan.ParseExact(HoraSenal, @"hh\:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Los campos que no tienen forma, en el orden de la pestaña: Cliente (1 a 60, sin caracteres de control), Remitente,
    /// Servidor y Puerto (1 a 65.535), cada Operador (Operador1 a Operador5), Administrador y HoraSenal (HH:mm). Una casilla
    /// vacía no cuenta; con los correos activos, el remitente, el servidor y al menos una casilla son obligatorios.
    /// </summary>
    public static IReadOnlyList<string> Validar(ConfiguracionCorreo c)
    {
        var campos = new List<string>();
        if (c.Cliente.Trim().Length is 0 or > LargoCliente || c.Cliente.Any(char.IsControl))
        {
            campos.Add("Cliente");
        }

        if (c.Remitente.Length > 0 ? !EsCasilla(c.Remitente) : c.Activos)
        {
            campos.Add("Remitente");
        }

        if (c.Servidor.Length > 0 ? !NombreDeServidor().IsMatch(c.Servidor) : c.Activos)
        {
            campos.Add("Servidor");
        }

        if (c.Puerto is < 1 or > 65535)
        {
            campos.Add("Puerto");
        }

        if (c.Operadores.Count > MaximoOperadores)
        {
            campos.Add("Operador" + MaximoOperadores.ToString(CultureInfo.InvariantCulture));
        }

        for (var i = 0; i < Math.Min(c.Operadores.Count, MaximoOperadores); i++)
        {
            if (!EsCasilla(c.Operadores[i]))
            {
                campos.Add("Operador" + (i + 1).ToString(CultureInfo.InvariantCulture));
            }
        }

        if (c.Administrador is { Length: > 0 } administrador ? !EsCasilla(administrador) : c.Activos && c.Operadores.Count == 0)
        {
            campos.Add("Administrador");
        }

        if (!Hora().IsMatch(c.HoraSenal))
        {
            campos.Add("HoraSenal");
        }

        return campos;
    }

    public static bool EsCasilla(string texto) => texto.Length <= LargoCasilla && Casilla().IsMatch(texto);

    public bool Equals(ConfiguracionCorreo? otra) =>
        otra is not null && Activos == otra.Activos && Cliente == otra.Cliente && Remitente == otra.Remitente
        && Servidor == otra.Servidor && Puerto == otra.Puerto && Cifrado == otra.Cifrado && SegundoFactor == otra.SegundoFactor
        && Operadores.SequenceEqual(otra.Operadores) && Administrador == otra.Administrador && HoraSenal == otra.HoraSenal;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Activos);
        hash.Add(Cliente);
        hash.Add(Remitente);
        hash.Add(Servidor);
        hash.Add(Puerto);
        hash.Add(Cifrado);
        hash.Add(SegundoFactor);
        foreach (var operador in Operadores)
        {
            hash.Add(operador);
        }

        hash.Add(Administrador);
        hash.Add(HoraSenal);
        return hash.ToHashCode();
    }
}

/// <summary><c>correo.json</c> de la carpeta de datos, validado al leer y al guardar, con escritura atómica (D1). Del equipo.</summary>
public sealed class AlmacenConfiguracionCorreo
{
    public const string Nombre = "correo.json";

    public AlmacenConfiguracionCorreo(string carpetaDatos)
    {
        CarpetaDatos = carpetaDatos;
    }

    public string CarpetaDatos { get; }

    private string Ruta => Path.Combine(CarpetaDatos, Nombre);

    private ConfiguracionCorreo? _ultima;

    /// <summary>Lo guardado; sin archivo o fuera de forma, lo de fábrica: apagados. Tomado un instante, lo último leído.</summary>
    public ConfiguracionCorreo Leer()
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(Ruta));
            var raiz = documento.RootElement;
            var configuracion = new ConfiguracionCorreo(
                raiz.GetProperty("activos").GetBoolean(),
                Texto(raiz, "cliente"),
                Texto(raiz, "remitente"),
                Texto(raiz, "servidor"),
                raiz.GetProperty("puerto").GetInt32(),
                Texto(raiz, "cifrado") switch
                {
                    "STARTTLS" => CifradoCorreo.StartTls,
                    "TLS" => CifradoCorreo.Tls,
                    _ => throw new JsonException("cifrado"),
                },
                Texto(raiz, "segundoFactor") switch
                {
                    "APP_PASSWORD" => SegundoFactorCorreo.ContrasenaDeAplicacion,
                    "OAUTH2" => SegundoFactorCorreo.OAuth2,
                    _ => throw new JsonException("segundoFactor"),
                },
                raiz.GetProperty("operadores").EnumerateArray().Select(o => o.ValueKind == JsonValueKind.String ? o.GetString()! : throw new JsonException("operadores")).ToList(),
                raiz.GetProperty("administrador").ValueKind == JsonValueKind.Null || Texto(raiz, "administrador").Length == 0 ? null : Texto(raiz, "administrador"),
                Texto(raiz, "horaSenal"));
            var leida = ConfiguracionCorreo.Validar(configuracion).Count == 0 ? configuracion : ConfiguracionCorreo.PorDefecto;
            _ultima = leida;
            return leida;
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return ConfiguracionCorreo.PorDefecto;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Tomado un instante (un antivirus): lo último que se leyó o se guardó, así un aviso no se pierde por eso.
            return _ultima ?? ConfiguracionCorreo.PorDefecto;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return ConfiguracionCorreo.PorDefecto;
        }
    }

    /// <summary>Guarda una configuración ya validada; una fuera de forma es un error de programación.</summary>
    public void Guardar(ConfiguracionCorreo configuracion)
    {
        if (ConfiguracionCorreo.Validar(configuracion).Count > 0)
        {
            throw new ArgumentException("La configuración de los correos no tiene forma.", nameof(configuracion));
        }

        Directory.CreateDirectory(CarpetaDatos);
        EscrituraAtomica.Escribir(Ruta, JsonSerializer.Serialize(new
        {
            activos = configuracion.Activos,
            cliente = configuracion.Cliente,
            remitente = configuracion.Remitente,
            servidor = configuracion.Servidor,
            puerto = configuracion.Puerto,
            cifrado = configuracion.Cifrado == CifradoCorreo.Tls ? "TLS" : "STARTTLS",
            segundoFactor = configuracion.SegundoFactor == SegundoFactorCorreo.OAuth2 ? "OAUTH2" : "APP_PASSWORD",
            operadores = configuracion.Operadores,
            administrador = configuracion.Administrador,
            horaSenal = configuracion.HoraSenal,
        }));
        _ultima = configuracion;
    }

    private static string Texto(JsonElement objeto, string nombre) =>
        objeto.GetProperty(nombre) is { ValueKind: JsonValueKind.String } valor ? valor.GetString()! : throw new JsonException($"{nombre} no es un texto.");
}

/// <summary>Las situaciones de un correo, que van al final del asunto (D2).</summary>
public static class SituacionCorreo
{
    public const string PersonaSinConsumo = "Persona sin consumo";
    public const string Impresora = "Impresora";
    public const string Exportacion = "Exportación";
    public const string Bitacora = "Bitácora";
    public const string SenalDeVida = "Señal de vida";
    public const string Prueba = "Prueba";

    /// <summary>El código de la situación para la bitácora, sin acentos.</summary>
    public static string Codigo(string situacion) => situacion switch
    {
        PersonaSinConsumo => "PERSON_WITHOUT_CONSUMPTION",
        Impresora => "PRINTER",
        Exportacion => "EXPORT",
        Bitacora => "LOG",
        SenalDeVida => "HEARTBEAT",
        _ => "TEST",
    };
}

/// <summary>Un correo: los destinatarios, el asunto y el cuerpo en texto (D2).</summary>
public sealed record MensajeCorreo(Guid Id, DateTimeOffset Creado, string Situacion, string Remitente, IReadOnlyList<string> Para, string Asunto, string Cuerpo);

/// <summary>Un servidor de correo que no responde: el simulado en «Caído».</summary>
public sealed class ServidorCorreoNoResponde() : IOException("El servidor de correo simulado no responde.");

/// <summary>
/// El envío simulado (D3): cada correo, un <c>.eml</c> en <c>correo-simulado\</c> de la carpeta de datos, en UTF-8, con
/// el asunto codificado; con «Caído», falla como un servidor que no responde. La conexión real queda pendiente.
/// </summary>
public sealed class CorreoSimulado
{
    public const string Carpeta = "correo-simulado";

    private readonly string _carpeta;

    public CorreoSimulado(string carpetaDatos)
    {
        _carpeta = Path.Combine(carpetaDatos, Carpeta);
    }

    /// <summary>«Caído»: el próximo envío falla.</summary>
    public bool Caido { get; set; }

    public void Enviar(MensajeCorreo mensaje)
    {
        if (Caido)
        {
            throw new ServidorCorreoNoResponde();
        }

        Directory.CreateDirectory(_carpeta);
        var nombre = mensaje.Creado.UtcDateTime.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture) + "-" + mensaje.Id.ToString("N") + ".eml";
        EscrituraAtomica.Escribir(Path.Combine(_carpeta, nombre), Eml(mensaje));
    }

    /// <summary>
    /// El asunto en palabras codificadas (RFC 2047) de hasta 45 bytes de texto cada una, así ninguna pasa de 75 caracteres,
    /// sin partir un carácter.
    /// </summary>
    public static IReadOnlyList<string> PalabrasCodificadas(string texto)
    {
        var palabras = new List<string>();
        var trozo = new StringBuilder();
        foreach (var elemento in texto.EnumerateRunes())
        {
            if (Encoding.UTF8.GetByteCount(trozo.ToString()) + elemento.Utf8SequenceLength > 45)
            {
                palabras.Add("=?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(trozo.ToString())) + "?=");
                trozo.Clear();
            }

            trozo.Append(elemento.ToString());
        }

        palabras.Add("=?utf-8?B?" + Convert.ToBase64String(Encoding.UTF8.GetBytes(trozo.ToString())) + "?=");
        return palabras;
    }

    /// <summary>El mensaje en RFC 5322: el asunto en palabras codificadas (RFC 2047) y el cuerpo en UTF-8.</summary>
    public static string Eml(MensajeCorreo mensaje)
    {
        var texto = new StringBuilder();
        texto.Append("Message-ID: <").Append(mensaje.Id.ToString("N")).Append("@incomedor.local>\r\n");
        texto.Append("Date: ").Append(mensaje.Creado.UtcDateTime.ToString("ddd, dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture)).Append(" +0000\r\n");
        texto.Append("From: ").Append(mensaje.Remitente).Append("\r\n");
        texto.Append("To: ").Append(string.Join(", ", mensaje.Para)).Append("\r\n");
        texto.Append("Subject: ").Append(string.Join("\r\n ", PalabrasCodificadas(mensaje.Asunto))).Append("\r\n");
        texto.Append("MIME-Version: 1.0\r\n");
        texto.Append("Content-Type: text/plain; charset=utf-8\r\n");
        texto.Append("Content-Transfer-Encoding: 8bit\r\n");
        texto.Append("\r\n");
        texto.Append(mensaje.Cuerpo.ReplaceLineEndings("\r\n")).Append("\r\n");
        return texto.ToString();
    }
}

/// <summary>Lo que se leyó de la cola: los pendientes válidos, en orden, y cuántos se descartaron por ilegibles.</summary>
public sealed record LecturaCola(IReadOnlyList<MensajeCorreo> Validos, int Ilegibles);

/// <summary>
/// La cola de los correos (D3): un archivo por correo en <c>correo-pendiente\</c>, con escritura atómica, que sale en el
/// orden en que entró. Guarda hasta 500; uno más descarta el más viejo. Un pendiente ilegible, incompleto o con una casilla
/// que no tiene forma (que agregaría un destinatario o cortaría un encabezado) se descarta al leer. Segura entre hilos.
/// </summary>
public sealed class ColaCorreo
{
    public const string Carpeta = "correo-pendiente";
    public const int Tope = 500;

    private readonly string _carpeta;
    private readonly Lock _cerrojo = new();

    public ColaCorreo(string carpetaDatos)
    {
        _carpeta = Path.Combine(carpetaDatos, Carpeta);
    }

    /// <summary>Encola el correo; devuelve la situación de cada uno que se descartó por el tope.</summary>
    public IReadOnlyList<string> Encolar(MensajeCorreo mensaje)
    {
        lock (_cerrojo)
        {
            Directory.CreateDirectory(_carpeta);
            var descartados = new List<string>();
            var archivos = Archivos();
            for (var i = 0; i <= archivos.Count - Tope; i++)
            {
                descartados.Add(LeerArchivo(archivos[i]) is { } viejo && Valido(viejo) ? viejo.Situacion : SituacionCorreo.Prueba);
                File.Delete(archivos[i]);
            }

            var nombre = mensaje.Creado.UtcDateTime.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture) + "-" + mensaje.Id.ToString("N") + ".json";
            EscrituraAtomica.Escribir(Path.Combine(_carpeta, nombre), JsonSerializer.Serialize(mensaje));
            return descartados;
        }
    }

    /// <summary>
    /// Los pendientes válidos, del más viejo al más nuevo; los ilegibles o fuera de forma se borran y se cuentan. Uno que
    /// otro programa tiene abierto en ese instante se saltea sin borrarlo.
    /// </summary>
    public LecturaCola Leer()
    {
        lock (_cerrojo)
        {
            var validos = new List<MensajeCorreo>();
            var ilegibles = 0;
            foreach (var archivo in Archivos())
            {
                string texto;
                try
                {
                    texto = File.ReadAllText(archivo);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                MensajeCorreo? mensaje;
                try
                {
                    mensaje = JsonSerializer.Deserialize<MensajeCorreo>(texto);
                }
                catch (Exception e) when (e is JsonException or NotSupportedException)
                {
                    mensaje = null;
                }

                if (mensaje is not null && Valido(mensaje))
                {
                    validos.Add(mensaje);
                    continue;
                }

                File.Delete(archivo);
                ilegibles++;
            }

            return new LecturaCola(validos, ilegibles);
        }
    }

    /// <summary>Los pendientes válidos, del más viejo al más nuevo.</summary>
    public IReadOnlyList<MensajeCorreo> Pendientes() => Leer().Validos;

    public int Cantidad
    {
        get
        {
            lock (_cerrojo)
            {
                return Archivos().Count;
            }
        }
    }

    public void Quitar(MensajeCorreo mensaje)
    {
        lock (_cerrojo)
        {
            foreach (var archivo in Archivos().Where(a => Path.GetFileNameWithoutExtension(a).EndsWith(mensaje.Id.ToString("N"), StringComparison.Ordinal)))
            {
                File.Delete(archivo);
            }
        }
    }

    /// <summary>Un pendiente con todo lo que un correo necesita y cada casilla con su forma.</summary>
    private static bool Valido(MensajeCorreo m) =>
        m.Situacion is not null && m.Asunto is not null && m.Cuerpo is not null && m.Remitente is not null
        && !m.Asunto.Any(char.IsControl) && ConfiguracionCorreo.EsCasilla(m.Remitente)
        && m.Para is { Count: > 0 } && m.Para.All(c => c is not null && ConfiguracionCorreo.EsCasilla(c));

    private List<string> Archivos() =>
        Directory.Exists(_carpeta)
            ? Directory.GetFiles(_carpeta, "*.json").Order(StringComparer.Ordinal).ToList()
            : [];

    private static MensajeCorreo? LeerArchivo(string archivo)
    {
        try
        {
            return JsonSerializer.Deserialize<MensajeCorreo>(File.ReadAllText(archivo));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
