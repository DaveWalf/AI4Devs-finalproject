using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>Un archivo que crece con cada pasada, o uno por fecha de negocio (D2 de exportar-consumos-automaticamente).</summary>
public enum ArchivoExportacion
{
    QueCrece,
    PorDia,
}

/// <summary>
/// La configuración de la exportación automática (D3, D4 y D6): activa o no, cada cuántos minutos, el tipo de archivo, la
/// carpeta y el SFTP simulado. Es del equipo: no viaja en un resguardo.
/// </summary>
public sealed partial record ConfiguracionExportacion(
    bool Activa, int Minutos, ArchivoExportacion Archivo, string? Carpeta, bool SftpActivo, string Servidor, int Puerto, string Usuario, string CarpetaRemota)
{
    public const int MinutosMinimos = 5;
    public const int MinutosMaximos = 1440;

    public static readonly ConfiguracionExportacion PorDefecto = new(false, 15, ArchivoExportacion.QueCrece, null, false, string.Empty, 22, string.Empty, string.Empty);

    [GeneratedRegex(@"\A[A-Za-z0-9](?:[A-Za-z0-9.-]{0,98}[A-Za-z0-9])?\z")]
    private static partial Regex NombreDeServidor();

    [GeneratedRegex(@"\A[A-Za-z0-9._-]{1,50}\z")]
    private static partial Regex NombreDeUsuario();

    [GeneratedRegex(@"\A(?:[A-Za-z0-9_-][A-Za-z0-9._-]{0,63})(?:/[A-Za-z0-9_-][A-Za-z0-9._-]{0,63}){0,7}\z")]
    private static partial Regex CarpetaRemotaValida();

    /// <summary>
    /// Los campos que no tienen forma, en el orden de la pestaña: Minutos (5 a 1.440), Carpeta (absoluta, o ninguna si el
    /// SFTP está activo), y con el SFTP activo, Servidor, Puerto (1 a 65.535), Usuario y CarpetaRemota (sin «..» ni «.»).
    /// </summary>
    public static IReadOnlyList<string> Validar(ConfiguracionExportacion configuracion)
    {
        var campos = new List<string>();
        if (configuracion.Minutos is < MinutosMinimos or > MinutosMaximos)
        {
            campos.Add("Minutos");
        }

        if (configuracion.Carpeta is { } carpeta
                ? carpeta.Length > 240 || carpeta.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || !Path.IsPathFullyQualified(carpeta)
                : configuracion.Activa && !configuracion.SftpActivo)
        {
            campos.Add("Carpeta");
        }

        if (configuracion.SftpActivo)
        {
            if (!NombreDeServidor().IsMatch(configuracion.Servidor))
            {
                campos.Add("Servidor");
            }

            if (configuracion.Puerto is < 1 or > 65535)
            {
                campos.Add("Puerto");
            }

            if (!NombreDeUsuario().IsMatch(configuracion.Usuario))
            {
                campos.Add("Usuario");
            }

            if (configuracion.CarpetaRemota.Length > 0
                && (!CarpetaRemotaValida().IsMatch(configuracion.CarpetaRemota) || configuracion.CarpetaRemota.Split('/').Any(s => s is "." or "..")))
            {
                campos.Add("CarpetaRemota");
            }
        }

        return campos;
    }
}

/// <summary><c>exportacion-automatica.json</c> de la carpeta de datos, validado al leer y al guardar (D6).</summary>
public sealed class AlmacenConfiguracionExportacion
{
    private const string Nombre = "exportacion-automatica.json";

    private readonly string _carpeta;

    public AlmacenConfiguracionExportacion(string carpetaDatos)
    {
        _carpeta = carpetaDatos;
    }

    private string Ruta => Path.Combine(_carpeta, Nombre);

    /// <summary>Lo guardado; sin archivo, ilegible o fuera de forma, lo de fábrica: apagada.</summary>
    public ConfiguracionExportacion Leer()
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(Ruta));
            var raiz = documento.RootElement;
            var sftp = raiz.GetProperty("sftp");
            var leida = new ConfiguracionExportacion(
                raiz.GetProperty("activa").GetBoolean(),
                raiz.GetProperty("minutos").GetInt32(),
                raiz.GetProperty("archivo").GetString() switch
                {
                    "QUE_CRECE" => ArchivoExportacion.QueCrece,
                    "POR_DIA" => ArchivoExportacion.PorDia,
                    _ => throw new JsonException("Archivo desconocido."),
                },
                raiz.GetProperty("carpeta").ValueKind == JsonValueKind.Null ? null : raiz.GetProperty("carpeta").GetString(),
                sftp.GetProperty("activo").GetBoolean(),
                sftp.GetProperty("servidor").GetString()!,
                sftp.GetProperty("puerto").GetInt32(),
                sftp.GetProperty("usuario").GetString()!,
                sftp.GetProperty("carpetaRemota").GetString()!);
            return ConfiguracionExportacion.Validar(leida).Count == 0 ? leida : ConfiguracionExportacion.PorDefecto;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException
                                       or InvalidOperationException or FormatException)
        {
            // Sin archivo, ilegible o con otra forma: lo de fábrica.
            return ConfiguracionExportacion.PorDefecto;
        }
    }

    public void Guardar(ConfiguracionExportacion configuracion)
    {
        if (ConfiguracionExportacion.Validar(configuracion).Count > 0)
        {
            throw new ArgumentException("La configuración de la exportación no tiene forma.", nameof(configuracion));
        }

        Directory.CreateDirectory(_carpeta);
        EscrituraAtomica.Escribir(Ruta, JsonSerializer.Serialize(new
        {
            activa = configuracion.Activa,
            minutos = configuracion.Minutos,
            archivo = configuracion.Archivo == ArchivoExportacion.PorDia ? "POR_DIA" : "QUE_CRECE",
            carpeta = configuracion.Carpeta,
            sftp = new
            {
                activo = configuracion.SftpActivo,
                servidor = configuracion.Servidor,
                puerto = configuracion.Puerto,
                usuario = configuracion.Usuario,
                carpetaRemota = configuracion.CarpetaRemota,
            },
        }));
    }
}

/// <summary>Cómo está un destino: su marca, el último intento, el último éxito y el último error (D5).</summary>
public sealed record EstadoDestino(long Marca, DateTimeOffset? UltimoIntento, DateTimeOffset? UltimoExito, string? Error);

/// <summary>
/// <c>exportacion-estado.json</c>: la marca y los intentos de cada destino (D5). Un archivo ilegible es ninguna marca: el
/// destino recibe todo otra vez, porque repetir es preferible a perder.
/// </summary>
public sealed class AlmacenEstadoExportacion
{
    private const string Nombre = "exportacion-estado.json";

    private readonly string _carpeta;

    public AlmacenEstadoExportacion(string carpetaDatos)
    {
        _carpeta = carpetaDatos;
    }

    private string Ruta => Path.Combine(_carpeta, Nombre);

    public Dictionary<string, EstadoDestino> Leer()
    {
        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(Ruta));
            var estados = new Dictionary<string, EstadoDestino>(StringComparer.Ordinal);
            foreach (var destino in documento.RootElement.EnumerateObject())
            {
                var valor = destino.Value;
                estados[destino.Name] = new EstadoDestino(
                    valor.GetProperty("marca").GetInt64(),
                    Instante(valor, "ultimoIntento"),
                    Instante(valor, "ultimoExito"),
                    valor.GetProperty("error").ValueKind == JsonValueKind.Null ? null : valor.GetProperty("error").GetString());
            }

            return estados;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException
                                       or InvalidOperationException or FormatException)
        {
            return new Dictionary<string, EstadoDestino>(StringComparer.Ordinal);
        }
    }

    public void Guardar(IReadOnlyDictionary<string, EstadoDestino> estados)
    {
        Directory.CreateDirectory(_carpeta);
        EscrituraAtomica.Escribir(Ruta, JsonSerializer.Serialize(estados.ToDictionary(e => e.Key, e => new
        {
            marca = e.Value.Marca,
            ultimoIntento = e.Value.UltimoIntento?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            ultimoExito = e.Value.UltimoExito?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            error = e.Value.Error,
        })));
    }

    private static DateTimeOffset? Instante(JsonElement valor, string nombre) =>
        valor.GetProperty(nombre).ValueKind == JsonValueKind.Null
            ? null
            : DateTimeOffset.Parse(valor.GetProperty(nombre).GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
}

/// <summary>El resultado de una pasada en un destino: las filas y los archivos escritos, o el motivo de la falla.</summary>
public sealed record ResultadoDestino(string Destino, int Filas, IReadOnlyList<string> Archivos, string? Error);

/// <summary>El SFTP simulado en «Caído»: falla como un servidor que no responde, antes de escribir nada (D3).</summary>
public sealed class ServidorNoResponde() : IOException("El servidor SFTP simulado no responde.");

/// <summary>
/// La exportación automática de consumos (exportar-consumos-automaticamente, D2 a D5): en cada pasada, por cada destino
/// configurado, los consumos desde su marca, en orden, escritos en su archivo, y la marca nueva recién después de escribir
/// cada archivo. Un destino que falla conserva su marca y la pasada siguiente reintenta desde ahí; los otros siguen.
/// </summary>
public sealed class ExportacionAutomatica : IDisposable
{
    public const string Carpeta = "FOLDER";
    public const string Sftp = "SFTP";
    public const int Tope = ExportacionConsumos.Tope;

    private readonly ConsultaConsumos _consulta;
    private readonly RegistroDelPuesto _registro;
    private readonly TimeProvider _reloj;
    private readonly TimeZoneInfo _zona;
    private readonly string _carpetaDatos;
    private readonly AlmacenConfiguracionExportacion _configuracion;
    private readonly AlmacenEstadoExportacion _estado;
    private readonly Lock _pasada = new();
    private readonly Lock _programacion = new();
    private ITimer? _temporizador;

    public ExportacionAutomatica(
        ConsultaConsumos consulta, RegistroDelPuesto registro, TimeProvider reloj, TimeZoneInfo zona, string carpetaDatos,
        AlmacenConfiguracionExportacion configuracion)
    {
        _consulta = consulta;
        _registro = registro;
        _reloj = reloj;
        _zona = zona;
        _carpetaDatos = carpetaDatos;
        _configuracion = configuracion;
        _estado = new AlmacenEstadoExportacion(carpetaDatos);
    }

    /// <summary>El modo del SFTP simulado: «Caído» falla como un servidor que no responde (D3).</summary>
    public bool SftpCaido { get; set; }

    /// <summary>Después de cada pasada, para que la pestaña vuelva a dibujar el estado; puede llegar desde el hilo del reloj.</summary>
    public event Action? PasadaTerminada;

    /// <summary>El estado de cada destino, por su clave.</summary>
    public IReadOnlyDictionary<string, EstadoDestino> Estado => _estado.Leer();

    /// <summary>La carpeta del SFTP simulado para un servidor y una carpeta remota ya validados (D3).</summary>
    public static string CarpetaSimulada(string carpetaDatos, string servidor, string carpetaRemota) =>
        Path.Combine([carpetaDatos, "sftp-simulado", servidor, .. carpetaRemota.Split('/', StringSplitOptions.RemoveEmptyEntries)]);

    /// <summary>Una pasada, de a una por vez: «Exportar ahora» o el reloj.</summary>
    public IReadOnlyList<ResultadoDestino> Exportar(bool aMano = false)
    {
        IReadOnlyList<ResultadoDestino> resultados;
        lock (_pasada)
        {
            var configuracion = _configuracion.Leer();
            var estados = _estado.Leer();
            var lista = new List<ResultadoDestino>();
            if (configuracion.Carpeta is { } carpeta)
            {
                lista.Add(ExportarA(Carpeta, carpeta, configuracion, estados, aMano));
            }

            if (configuracion.SftpActivo)
            {
                lista.Add(ExportarA(Sftp, CarpetaSimulada(_carpetaDatos, configuracion.Servidor, configuracion.CarpetaRemota), configuracion, estados, aMano));
            }

            resultados = lista;
        }

        PasadaTerminada?.Invoke();
        return resultados;
    }

    /// <summary>Vuelve a programar el reloj con la configuración guardada: cada N minutos si está activa, nada si no.</summary>
    public void Programar()
    {
        lock (_programacion)
        {
            _temporizador?.Dispose();
            _temporizador = null;
            var configuracion = _configuracion.Leer();
            if (configuracion.Activa)
            {
                var periodo = TimeSpan.FromMinutes(configuracion.Minutos);
                _temporizador = _reloj.CreateTimer(_ => PasadaDelReloj(), null, periodo, periodo);
            }
        }
    }

    public void Dispose()
    {
        lock (_programacion)
        {
            _temporizador?.Dispose();
            _temporizador = null;
        }
    }

    /// <summary>
    /// La pasada del reloj corre fuera del hilo de la pantalla: una base ocupada en ese instante no es una falla de un
    /// destino, y la pasada siguiente vuelve a intentar desde las mismas marcas.
    /// </summary>
    private void PasadaDelReloj()
    {
        try
        {
            Exportar();
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            System.Diagnostics.Debug.WriteLine("INComedor: la base no respondió a la exportación automática; reintenta en la pasada siguiente.");
        }
    }

    private ResultadoDestino ExportarA(
        string clave, string carpeta, ConfiguracionExportacion configuracion, Dictionary<string, EstadoDestino> estados, bool aMano)
    {
        var actual = estados.GetValueOrDefault(clave) ?? new EstadoDestino(0, null, null, null);
        var consumos = _consulta.Desde(actual.Marca, Tope);
        var ahora = _reloj.GetUtcNow();
        if (consumos.Count == 0)
        {
            return new ResultadoDestino(clave, 0, [], null);
        }

        var archivos = new List<string>();
        var filas = 0;
        var marca = actual.Marca;
        var exito = actual.UltimoExito;
        try
        {
            // Los consumos, en orden de secuencia, en tramos del mismo archivo: cada tramo escrito mueve la marca, así una
            // falla en el segundo archivo de una pasada no vuelve a escribir el primero.
            foreach (var tramo in Tramos(consumos, configuracion.Archivo))
            {
                if (clave == Sftp && SftpCaido)
                {
                    throw new ServidorNoResponde();
                }

                Escribir(carpeta, tramo.Archivo, tramo.Lineas, crear: clave == Sftp);
                marca = tramo.Ultima;
                exito = ahora;
                filas += tramo.Lineas.Count;
                if (!archivos.Contains(tramo.Archivo))
                {
                    archivos.Add(tramo.Archivo);
                }

                estados[clave] = new EstadoDestino(marca, ahora, exito, null);
                _estado.Guardar(estados);
            }

            Registrar("CONSUMPTIONS_EXPORTED", new { Destination = clave, Files = archivos, Rows = filas, UpToSequence = marca, Trigger = Disparo(aMano) });
            return new ResultadoDestino(clave, filas, archivos, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            var motivo = e switch
            {
                ServidorNoResponde => "SERVER_UNREACHABLE",
                DirectoryNotFoundException => "FOLDER_NOT_FOUND",
                UnauthorizedAccessException => "ACCESS_DENIED",
                _ => "WRITE_FAILED",
            };
            estados[clave] = new EstadoDestino(marca, ahora, exito, motivo);
            try
            {
                _estado.Guardar(estados);
            }
            catch (Exception guardado) when (guardado is IOException or UnauthorizedAccessException)
            {
                // Sin poder guardar el estado, la marca de la última escritura buena queda en memoria hasta la pasada siguiente.
                System.Diagnostics.Debug.WriteLine($"INComedor: no se pudo guardar el estado de la exportación ({guardado.GetType().Name}).");
            }

            Registrar("CONSUMPTIONS_EXPORT_FAILED", new { Destination = clave, ReasonCode = motivo, Rows = filas, UpToSequence = marca, Trigger = Disparo(aMano) },
                SeveridadBitacora.Warning);
            return new ResultadoDestino(clave, filas, archivos, motivo);
        }
    }

    private static string Disparo(bool aMano) => aMano ? "MANUAL" : "TIMER";

    private void Registrar(string tipo, object contexto, string severidad = SeveridadBitacora.Info)
    {
        try
        {
            _registro.Registrar(tipo, contexto, severidad);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // La exportación no depende de la bitácora: el estado del destino dice lo mismo.
            System.Diagnostics.Debug.WriteLine($"INComedor: no se pudo registrar {tipo}.");
        }
    }

    private sealed record Tramo(string Archivo, List<string> Lineas, long Ultima);

    private List<Tramo> Tramos(IReadOnlyList<ConsumoConSecuencia> consumos, ArchivoExportacion tipo)
    {
        var tramos = new List<Tramo>();
        foreach (var (secuencia, consumo) in consumos)
        {
            var archivo = tipo == ArchivoExportacion.PorDia
                ? "INComedor-consumos-" + consumo.FechaNegocio.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".csv"
                : "INComedor-consumos.csv";
            var linea = EscritorPlanilla.LineaCsv(ExportacionConsumos.FilaDe(consumo, _zona));
            if (tramos.Count > 0 && tramos[^1].Archivo == archivo)
            {
                tramos[^1].Lineas.Add(linea);
                tramos[^1] = tramos[^1] with { Ultima = secuencia };
            }
            else
            {
                tramos.Add(new Tramo(archivo, [linea], secuencia));
            }
        }

        return tramos;
    }

    /// <summary>
    /// Agrega las líneas al archivo copiando lo que había a un temporal y reemplazándolo: un corte deja el archivo de antes o
    /// el completo (D2). El encabezado, con la marca de UTF-8, sólo al crearlo. La carpeta del cliente tiene que existir;
    /// la del SFTP simulado la crea el servidor.
    /// </summary>
    private static void Escribir(string carpeta, string archivo, IReadOnlyList<string> lineas, bool crear)
    {
        if (crear)
        {
            Directory.CreateDirectory(carpeta);
        }
        else if (!Directory.Exists(carpeta))
        {
            throw new DirectoryNotFoundException($"No existe la carpeta de la exportación: {carpeta}.");
        }

        var ruta = Path.Combine(carpeta, archivo);
        var temporal = ruta + ".tmp";
        try
        {
            using (var salida = new FileStream(temporal, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (File.Exists(ruta))
                {
                    using var anterior = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
                    anterior.CopyTo(salida);
                }
                else
                {
                    salida.Write(Encoding.UTF8.Preamble);
                    salida.Write(Encoding.UTF8.GetBytes(EscritorPlanilla.LineaCsv(ExportacionConsumos.Columnas) + "\r\n"));
                }

                foreach (var linea in lineas)
                {
                    salida.Write(Encoding.UTF8.GetBytes(linea + "\r\n"));
                }
            }

            File.Move(temporal, ruta, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporal);
            }
            catch (Exception borrado) when (borrado is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"INComedor: quedó un temporal de la exportación ({borrado.GetType().Name}).");
            }

            throw;
        }
    }
}

/// <summary>Escribir un archivo por un temporal y moverlo encima: nunca uno a medias.</summary>
internal static class EscrituraAtomica
{
    public static void Escribir(string ruta, string contenido)
    {
        var temporal = ruta + ".tmp";
        try
        {
            File.WriteAllText(temporal, contenido);
            File.Move(temporal, ruta, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporal);
            }
            catch (Exception borrado) when (borrado is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"INComedor: quedó un temporal ({borrado.GetType().Name}).");
            }

            throw;
        }
    }
}
