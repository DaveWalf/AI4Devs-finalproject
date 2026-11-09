using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite;

/// <summary>Las dos bitácoras de texto del puesto (D-C13).</summary>
public enum DestinoBitacora
{
    General,
    Interacciones,
}

/// <summary>Lo que Configuración muestra de una bitácora: al día o degradada, cuántos pendientes y desde cuándo.</summary>
public sealed record EstadoBitacora(DestinoBitacora Destino, bool AlDia, int Pendientes, DateTimeOffset? DegradadaDesde);

/// <summary>
/// Lleva a dos archivos mensuales lo que ya quedó en tbComedorBitacora (D1 de escribir-bitacoras-externas), fuera de la
/// atención: la atención nunca espera al archivo. Cada pasada procesa los dos destinos, en orden de secuencia, de a 500
/// líneas con flush durable (D4). La marca de entrega es el propio archivo (D2): lo pendiente es lo que tiene una
/// secuencia mayor que la última línea completa de los archivos de ese destino.
/// </summary>
public sealed class EscritorBitacoras
{
    private const int LineasPorTanda = 500;

    /// <summary>Los tipos que van sólo a interacciones (D3); los de las dos van además a la general.</summary>
    private static readonly string[] SoloInteracciones =
    [
        "SESSION_OPENED", "IDENTIFICATION_REJECTED", "OPTIONS_OFFERED", "OPTION_SELECTED", "CONFIRMATION_REQUESTED",
        "CONSUMPTION_AUTHORIZED", "CONSUMPTION_DENIED", "SESSION_CANCELLED", "SESSION_TIMED_OUT", "SESSION_CLOSED",
        "ASSISTANCE_GRANTED", "ASSISTANCE_DENIED", "TICKET_RECOVERED", "TICKET_REPRINTED", "PRIVILEGED_ACTION_DENIED",
    ];

    private static readonly string[] EnLasDos = ["PRINT_JOB_UPDATED", "LOG_DELIVERY_RECOVERED"];

    private static readonly JsonWriterOptions OpcionesLinea = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false,
    };

    /// <summary>Una pasada vuelve a recorrer los destinos si el escritor registró algo propio, hasta tres vueltas (D7).</summary>
    private const int VueltasPorPasada = 3;

    private readonly BaseComedor _baseComedor;
    private readonly TimeProvider _reloj;
    private readonly Guid _arranque;
    private readonly RegistroBitacora _bitacora;
    private readonly SemaphoreSlim _pasada = new(1, 1);
    private readonly Dictionary<DestinoBitacora, EstadoDestino> _destinos = new()
    {
        [DestinoBitacora.General] = new(),
        [DestinoBitacora.Interacciones] = new(),
    };

    public EscritorBitacoras(BaseComedor baseComedor, string carpetaDatos, TimeProvider reloj)
        : this(baseComedor, carpetaDatos, reloj, RegistroBitacora.ArranqueDelProceso)
    {
    }

    /// <param name="arranque">El arranque actual: lo registrado con otro BootId sale con Recovered verdadero (D5).</param>
    public EscritorBitacoras(BaseComedor baseComedor, string carpetaDatos, TimeProvider reloj, Guid arranque)
    {
        _baseComedor = baseComedor;
        _reloj = reloj;
        _arranque = arranque;
        _bitacora = new RegistroBitacora(arranque, "Infrastructure.Sqlite.EscritorBitacoras");

        using (var conexion = baseComedor.AbrirConexionExistente())
        {
            KioskId = PuestoLocal.Leer(conexion, null).Id;
        }

        CarpetaPuesto = Contenida(carpetaDatos, KioskId);
        using (var conexion = baseComedor.AbrirConexionExistente())
        {
            foreach (var (destino, estado) in _destinos)
            {
                estado.Incidencia = IncidenciaAbierta(conexion, destino);
            }
        }
    }

    public Guid KioskId { get; }

    /// <summary>La carpeta bitacoras\&lt;KioskId&gt; dentro de la carpeta de datos (D11).</summary>
    public string CarpetaPuesto { get; }

    /// <summary>El nombre del archivo de un destino y un mes AAAAMM.</summary>
    public static string NombreArchivo(DestinoBitacora destino, string mes) => $"INComedor-{Prefijo(destino)}-{mes}.log";

    /// <summary>Una pasada: entrega lo pendiente de los dos destinos. Nunca corren dos pasadas a la vez (D4).</summary>
    public void Entregar()
    {
        _pasada.Wait();
        try
        {
            for (var vuelta = 0; vuelta < VueltasPorPasada; vuelta++)
            {
                var registrados = 0;
                using (var conexion = _baseComedor.AbrirConexionExistente())
                {
                    var corte = UltimaSecuencia(conexion);
                    foreach (var (destino, estado) in _destinos)
                    {
                        registrados += EntregarDestino(conexion, destino, estado, corte);
                    }
                }

                if (registrados == 0)
                {
                    break;
                }
            }
        }
        finally
        {
            _pasada.Release();
        }
    }

    /// <summary>El estado de cada bitácora, para Configuración.</summary>
    public IReadOnlyList<EstadoBitacora> Estado()
    {
        _pasada.Wait();
        try
        {
            using var conexion = _baseComedor.AbrirConexionExistente();
            return _destinos
                .Select(par =>
                {
                    var (destino, estado) = par;
                    var pendientes = ContarPendientes(conexion, destino, estado.Marca ?? estado.UltimaMarcaConocida);
                    var degradada = estado.Incidencia?.AbiertaUtc ?? estado.BloqueadaDesde;
                    return new EstadoBitacora(destino, pendientes == 0 && degradada is null, pendientes, degradada);
                })
                .ToList();
        }
        finally
        {
            _pasada.Release();
        }
    }

    /// <summary>Si un tipo de registro va a un destino (D3): un tipo que no está en la tabla va a la general.</summary>
    internal static bool VaA(DestinoBitacora destino, string tipo) => destino == DestinoBitacora.Interacciones
        ? SoloInteracciones.Contains(tipo) || EnLasDos.Contains(tipo)
        : !SoloInteracciones.Contains(tipo);

    /// <summary>Entrega un destino hasta el corte; devuelve cuántos registros propios dejó el escritor en la base.</summary>
    private int EntregarDestino(SqliteConnection conexion, DestinoBitacora destino, EstadoDestino estado, long corte)
    {
        if (estado.BloqueadaDesde is not null)
        {
            return 0;
        }

        var registrados = 0;
        var incidencia = estado.Incidencia;
        var entregados = 0;
        try
        {
            if (estado.Marca is null)
            {
                var (marca, reparaciones, desajuste) = LeerMarca(conexion, destino);
                foreach (var reparacion in reparaciones)
                {
                    Registrar("LOG_LINE_REPAIRED", reparacion, SeveridadBitacora.Warning);
                    registrados++;
                }

                if (desajuste is not null)
                {
                    // La base no es la que escribió ese archivo: no se escribe hasta el próximo arranque (D6).
                    estado.BloqueadaDesde = _reloj.GetUtcNow();
                    Registrar("LOG_SEQUENCE_MISMATCH", desajuste, SeveridadBitacora.Error);
                    return registrados + 1;
                }

                estado.Marca = estado.UltimaMarcaConocida = marca;
            }

            while (estado.Marca < corte)
            {
                var tanda = LeerPendientes(conexion, destino, estado.Marca.Value, corte);
                if (tanda.Count == 0)
                {
                    break;
                }

                // Lo que llega con una incidencia abierta al empezar llega tarde: Recovered (D5).
                Escribir(destino, tanda, recuperados: incidencia is not null);
                estado.Marca = estado.UltimaMarcaConocida = tanda[^1].Secuencia;
                entregados += tanda.Count;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // La marca se vuelve a leer del archivo: lo que haya quedado a medias se repara y nada se repite (D2, D6).
            estado.Marca = null;
            if (estado.Incidencia is not null)
            {
                // Una falla repetida no agrega registros: sería la recursión que el diseño prohíbe (D7).
                return registrados;
            }

            var ahora = _reloj.GetUtcNow();
            var primero = PrimerPendiente(conexion, destino, estado.UltimaMarcaConocida);
            estado.Incidencia = new Incidencia(ahora, primero);
            Registrar("LOG_DELIVERY_FAILED", new
            {
                Stream = Flujo(destino),
                OpenedAtUtc = FormatoInstante.Texto(ahora),
                Cause = e.GetType().Name,
                FirstPendingAuditId = primero,
            }, SeveridadBitacora.Error);
            return registrados + 1;
        }

        if (incidencia is not null)
        {
            // Todo lo pendiente hasta el corte ya está en el archivo: recién ahora se cierra la incidencia (D7).
            estado.Incidencia = null;
            Registrar("LOG_DELIVERY_RECOVERED", new
            {
                Stream = Flujo(destino),
                OpenedAtUtc = FormatoInstante.Texto(incidencia.AbiertaUtc),
                ClosedAtUtc = FormatoInstante.Texto(_reloj.GetUtcNow()),
                DeliveredCount = entregados,
                FirstPendingAuditId = incidencia.PrimerPendiente,
            }, SeveridadBitacora.Warning);
            registrados++;
        }

        return registrados;
    }

    /// <summary>
    /// Si el último LOG_DELIVERY_FAILED o LOG_DELIVERY_RECOVERED de este destino es una falla, la incidencia sigue abierta:
    /// un arranque nuevo no abre otra (D2, D7).
    /// </summary>
    private Incidencia? IncidenciaAbierta(SqliteConnection conexion, DestinoBitacora destino)
    {
        using var comando = ComandosSqlite.Crear(conexion, null,
            """
            SELECT combitTipoEvento, combitContexto FROM tbComedorBitacora
            WHERE compueCodigo = $pue AND combitCodigoGeneracion = $gen AND combitTipoEvento IN ('LOG_DELIVERY_FAILED', 'LOG_DELIVERY_RECOVERED')
            ORDER BY combitSecuencia DESC
            """,
            ("$pue", KioskId), ("$gen", RegistroBitacora.GeneracionBitacora));
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            using var contexto = JsonDocument.Parse(lector.GetString(1));
            var raiz = contexto.RootElement;
            if (!raiz.TryGetProperty("Stream", out var flujo) || flujo.GetString() != Flujo(destino))
            {
                continue;
            }

            return lector.GetString(0) == "LOG_DELIVERY_FAILED"
                ? new Incidencia(
                    FormatoInstante.Leer(raiz.GetProperty("OpenedAtUtc").GetString()!),
                    raiz.TryGetProperty("FirstPendingAuditId", out var primero) && primero.ValueKind == JsonValueKind.String ? primero.GetGuid() : null)
                : null;
        }

        return null;
    }

    private Guid? PrimerPendiente(SqliteConnection conexion, DestinoBitacora destino, long marca)
    {
        var (filtro, parametros) = FiltroDe(destino);
        using var comando = ComandosSqlite.Crear(conexion, null,
            $"""
             SELECT combitCodigo FROM tbComedorBitacora
             WHERE compueCodigo = $pue AND combitCodigoGeneracion = $gen AND combitSecuencia > $marca AND {filtro}
             ORDER BY combitSecuencia LIMIT 1
             """,
            [("$pue", KioskId), ("$gen", RegistroBitacora.GeneracionBitacora), ("$marca", marca), .. parametros]);
        using var lector = comando.ExecuteReader();
        return lector.Read() ? lector.GetGuid(0) : null;
    }

    /// <summary>Un registro propio del escritor, en su transacción corta; pasa por la misma cola que los demás (D7).</summary>
    private void Registrar(string tipo, object contexto, string severidad)
    {
        using var conexion = _baseComedor.AbrirConexionExistente();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        _bitacora.Insertar(conexion, transaccion, PuestoLocal.Leer(conexion, transaccion), _reloj.GetUtcNow(), tipo, contexto, severidad);
        transaccion.Commit();
    }

    /// <summary>
    /// Agrega la tanda a los archivos de los meses de sus registros, con un flush durable por archivo (D4). Va en tramos
    /// consecutivos de un mismo mes y en orden de secuencia: si un archivo falla, todo lo anterior ya quedó escrito y la marca
    /// releída del archivo no saltea nada.
    /// </summary>
    private void Escribir(DestinoBitacora destino, IReadOnlyList<RegistroGuardado> tanda, bool recuperados)
    {
        Directory.CreateDirectory(CarpetaPuesto);
        var exportado = FormatoInstante.Texto(_reloj.GetUtcNow());
        var inicio = 0;
        while (inicio < tanda.Count)
        {
            var mes = tanda[inicio].Mes;
            var buffer = new ArrayBufferWriter<byte>();
            var fin = inicio;
            for (; fin < tanda.Count && tanda[fin].Mes == mes; fin++)
            {
                EscribirLinea(buffer, destino, tanda[fin], exportado, recuperados);
            }

            using (var archivo = new FileStream(Path.Combine(CarpetaPuesto, NombreArchivo(destino, mes)), FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                archivo.Write(buffer.WrittenSpan);
                archivo.Flush(flushToDisk: true);
            }

            inicio = fin;
        }
    }

    private void EscribirLinea(ArrayBufferWriter<byte> buffer, DestinoBitacora destino, RegistroGuardado registro, string exportado, bool recuperado)
    {
        using (var json = new Utf8JsonWriter(buffer, OpcionesLinea))
        {
            json.WriteStartObject();
            json.WriteNumber("SchemaVersion", registro.VersionEsquema);
            json.WriteString("AuditId", registro.AuditId);
            json.WriteString("KioskId", KioskId);
            json.WriteString("GenerationId", RegistroBitacora.GeneracionBitacora);
            json.WriteNumber("Sequence", registro.Secuencia);
            json.WriteString("BootId", registro.Arranque);
            json.WriteString("OccurredAtUtc", registro.HechoUtc);
            json.WriteString("RecordedAtUtc", registro.RegistroUtc);
            json.WriteString("TimeZone", registro.Zona);
            json.WriteString("Offset", registro.Desplazamiento);
            json.WriteString("LogMonth", registro.Mes);
            json.WriteString("Stream", Flujo(destino));
            json.WriteString("EventType", registro.Tipo);
            json.WriteString("Severity", registro.Severidad);
            json.WriteString("Component", registro.Componente);
            json.WriteString("ExportedAtUtc", exportado);
            json.WriteBoolean("Recovered", recuperado || registro.Arranque != _arranque);
            json.WritePropertyName("Context");
            using (var contexto = JsonDocument.Parse(registro.Contexto))
            {
                contexto.RootElement.WriteTo(json);
            }

            json.WriteEndObject();
        }

        buffer.Write("\n"u8);
    }

    /// <summary>
    /// La mayor secuencia de las últimas líneas completas de los archivos del destino; 0 sin archivos (D2). Antes repara
    /// cada archivo que no termina en salto de línea, y después compara el AuditId de esa línea con el registro de la base
    /// que tiene su secuencia: si no existe o no coincide, devuelve el desajuste (D6).
    /// </summary>
    private (long Marca, List<Reparacion> Reparaciones, Desajuste? Desajuste) LeerMarca(SqliteConnection conexion, DestinoBitacora destino)
    {
        var reparaciones = new List<Reparacion>();
        if (!Directory.Exists(CarpetaPuesto))
        {
            return (0, reparaciones, null);
        }

        LineaLeida? ultima = null;
        var archivoUltima = string.Empty;
        foreach (var ruta in ArchivosDe(destino).ToList())
        {
            if (RepararSiHaceFalta(destino, ruta) is { } reparacion)
            {
                reparaciones.Add(reparacion);
            }

            using var archivo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            LineaLeida? linea;
            try
            {
                linea = UltimaLineaCompleta(archivo);
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
            {
                // Una última línea completa que no se puede leer no permite comprobar que la base sea la del archivo.
                return (0, reparaciones, new Desajuste(Flujo(destino), Path.GetFileName(ruta), null, null, null));
            }

            if (linea is not null && (ultima is null || linea.Secuencia > ultima.Secuencia))
            {
                ultima = linea;
                archivoUltima = Path.GetFileName(ruta);
            }
        }

        if (ultima is null)
        {
            return (0, reparaciones, null);
        }

        var enLaBase = AuditIdDe(conexion, ultima.Secuencia);
        return enLaBase == ultima.AuditId
            ? (ultima.Secuencia, reparaciones, null)
            : (0, reparaciones, new Desajuste(Flujo(destino), archivoUltima, ultima.Secuencia, ultima.AuditId, enLaBase));
    }

    /// <summary>
    /// Si el archivo no termina en salto de línea, agrega los bytes desde el último a &lt;archivo&gt;.reparacion.log y corta
    /// el archivo en ese punto (D6): el registro cortado se vuelve a escribir desde la base. Null si no hizo falta.
    /// </summary>
    private static Reparacion? RepararSiHaceFalta(DestinoBitacora destino, string ruta)
    {
        using (var lectura = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            if (lectura.Length == 0)
            {
                return null;
            }

            lectura.Position = lectura.Length - 1;
            if (lectura.ReadByte() == '\n')
            {
                return null;
            }
        }

        using var archivo = new FileStream(ruta, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        var corte = UltimoSaltoAntesDe(archivo, archivo.Length) + 1;
        var cortados = new byte[archivo.Length - corte];
        archivo.Position = corte;
        archivo.ReadExactly(cortados);

        var rutaReparacion = ruta + ".reparacion.log";
        using (var reparacion = new FileStream(rutaReparacion, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            reparacion.Write(cortados);
            reparacion.Write("\n"u8);
            reparacion.Flush(flushToDisk: true);
        }

        archivo.SetLength(corte);
        archivo.Flush(flushToDisk: true);
        return new Reparacion(Flujo(destino), Path.GetFileName(ruta), cortados.Length, Path.GetFileName(rutaReparacion));
    }

    private Guid? AuditIdDe(SqliteConnection conexion, long secuencia)
    {
        using var comando = ComandosSqlite.Crear(conexion, null,
            "SELECT combitCodigo FROM tbComedorBitacora WHERE compueCodigo = $pue AND combitCodigoGeneracion = $gen AND combitSecuencia = $sec",
            ("$pue", KioskId), ("$gen", RegistroBitacora.GeneracionBitacora), ("$sec", secuencia));
        using var lector = comando.ExecuteReader();
        return lector.Read() ? lector.GetGuid(0) : null;
    }

    private IEnumerable<string> ArchivosDe(DestinoBitacora destino) =>
        Directory.EnumerateFiles(CarpetaPuesto, $"INComedor-{Prefijo(destino)}-??????.log")
            .Where(r => Path.GetFileNameWithoutExtension(r)[^6..].All(char.IsAsciiDigit));

    /// <summary>La última línea terminada en salto de línea, leída desde el final del archivo; null si no hay ninguna.</summary>
    private static LineaLeida? UltimaLineaCompleta(FileStream archivo)
    {
        var ultimoSalto = UltimoSaltoAntesDe(archivo, archivo.Length);
        if (ultimoSalto < 0)
        {
            return null;
        }

        var inicio = UltimoSaltoAntesDe(archivo, ultimoSalto) + 1;
        var bytes = new byte[ultimoSalto - inicio];
        archivo.Position = inicio;
        archivo.ReadExactly(bytes);
        using var linea = JsonDocument.Parse(bytes);
        return new LineaLeida(linea.RootElement.GetProperty("Sequence").GetInt64(), linea.RootElement.GetProperty("AuditId").GetGuid());
    }

    /// <summary>La posición del último '\n' antes de <paramref name="limite"/>, leyendo hacia atrás de a 4 KiB; -1 si no hay.</summary>
    private static long UltimoSaltoAntesDe(FileStream archivo, long limite)
    {
        var bloque = new byte[4096];
        var fin = limite;
        while (fin > 0)
        {
            var inicio = Math.Max(0, fin - bloque.Length);
            var largo = (int)(fin - inicio);
            archivo.Position = inicio;
            archivo.ReadExactly(bloque, 0, largo);
            var indice = bloque.AsSpan(0, largo).LastIndexOf((byte)'\n');
            if (indice >= 0)
            {
                return inicio + indice;
            }

            fin = inicio;
        }

        return -1;
    }

    private long UltimaSecuencia(SqliteConnection conexion) => ComandosSqlite.EscalarLong(conexion, null,
        "SELECT COALESCE(MAX(combitSecuencia), 0) FROM tbComedorBitacora WHERE compueCodigo = $pue AND combitCodigoGeneracion = $gen",
        ("$pue", KioskId), ("$gen", RegistroBitacora.GeneracionBitacora));

    private List<RegistroGuardado> LeerPendientes(SqliteConnection conexion, DestinoBitacora destino, long marca, long corte)
    {
        var (filtro, parametros) = FiltroDe(destino);
        using var comando = ComandosSqlite.Crear(conexion, null,
            $"""
             SELECT combitCodigo, combitSecuencia, combitCodigoArranque, combitFechaHechoUtc, combitFechaRegistroUtc, combitZonaHoraria,
                    combitDesplazamientoUtc, combitMesBitacora, combitTipoEvento, combitSeveridad, combitComponente, combitVersionEsquema,
                    combitContexto
             FROM tbComedorBitacora
             WHERE compueCodigo = $pue AND combitCodigoGeneracion = $gen AND combitSecuencia > $marca AND combitSecuencia <= $corte AND {filtro}
             ORDER BY combitSecuencia
             LIMIT {LineasPorTanda}
             """,
            [("$pue", KioskId), ("$gen", RegistroBitacora.GeneracionBitacora), ("$marca", marca), ("$corte", corte), .. parametros]);
        using var lector = comando.ExecuteReader();
        var registros = new List<RegistroGuardado>();
        while (lector.Read())
        {
            registros.Add(new RegistroGuardado(
                lector.GetGuid(0), lector.GetInt64(1), lector.GetGuid(2), lector.GetString(3), lector.GetString(4), lector.GetString(5),
                lector.GetString(6), lector.GetString(7), lector.GetString(8), lector.GetString(9), lector.GetString(10), lector.GetInt32(11),
                lector.GetString(12)));
        }

        return registros;
    }

    private int ContarPendientes(SqliteConnection conexion, DestinoBitacora destino, long marca)
    {
        var (filtro, parametros) = FiltroDe(destino);
        return (int)ComandosSqlite.EscalarLong(conexion, null,
            $"SELECT COUNT(*) FROM tbComedorBitacora WHERE compueCodigo = $pue AND combitCodigoGeneracion = $gen AND combitSecuencia > $marca AND {filtro}",
            [("$pue", KioskId), ("$gen", RegistroBitacora.GeneracionBitacora), ("$marca", marca), .. parametros]);
    }

    /// <summary>El reparto de D3 como condición parametrizada sobre el tipo.</summary>
    private static (string Filtro, (string, object?)[] Parametros) FiltroDe(DestinoBitacora destino)
    {
        var tipos = destino == DestinoBitacora.Interacciones ? SoloInteracciones.Concat(EnLasDos).ToArray() : SoloInteracciones;
        var nombres = tipos.Select((_, i) => "$t" + i).ToArray();
        var condicion = destino == DestinoBitacora.Interacciones ? "IN" : "NOT IN";
        return ($"combitTipoEvento {condicion} ({string.Join(", ", nombres)})",
            nombres.Zip(tipos, (n, t) => (n, (object?)t)).ToArray());
    }

    /// <summary>La carpeta del puesto, resuelta y comprobada dentro de la carpeta de datos (D11, OWASP A01).</summary>
    private static string Contenida(string carpetaDatos, Guid kioskId)
    {
        var raiz = Path.GetFullPath(carpetaDatos);
        var carpeta = Path.GetFullPath(Path.Combine(raiz, "bitacoras", kioskId.ToString()));
        if (!carpeta.StartsWith(Path.TrimEndingDirectorySeparator(raiz) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La carpeta de bitácoras quedó fuera de la carpeta de datos.");
        }

        return carpeta;
    }

    private static string Prefijo(DestinoBitacora destino) => destino == DestinoBitacora.General ? "general" : "interacciones";

    private static string Flujo(DestinoBitacora destino) => destino == DestinoBitacora.General ? "GENERAL" : "INTERACTIONS";

    private sealed record LineaLeida(long Secuencia, Guid AuditId);

    /// <summary>Lo que el escritor sabe de un destino entre pasadas; la marca sólo vive en memoria (D2).</summary>
    private sealed class EstadoDestino
    {
        /// <summary>La última secuencia escrita; null obliga a releerla del archivo (al arrancar o después de un error).</summary>
        public long? Marca { get; set; }

        public long UltimaMarcaConocida { get; set; }

        /// <summary>Desde cuándo el destino no se escribe porque la base no es la de su archivo (D6).</summary>
        public DateTimeOffset? BloqueadaDesde { get; set; }

        /// <summary>La incidencia abierta del destino, una sola aunque la falla se repita (D7).</summary>
        public Incidencia? Incidencia { get; set; }
    }

    private sealed record Incidencia(DateTimeOffset AbiertaUtc, Guid? PrimerPendiente);

    /// <summary>El contexto de LOG_LINE_REPAIRED.</summary>
    private sealed record Reparacion(string Stream, string File, int Bytes, string RepairFile);

    /// <summary>El contexto de LOG_SEQUENCE_MISMATCH: la secuencia del archivo y los dos AuditId que no coinciden.</summary>
    private sealed record Desajuste(string Stream, string File, long? Sequence, Guid? FileAuditId, Guid? DatabaseAuditId);

    private sealed record RegistroGuardado(
        Guid AuditId,
        long Secuencia,
        Guid Arranque,
        string HechoUtc,
        string RegistroUtc,
        string Zona,
        string Desplazamiento,
        string Mes,
        string Tipo,
        string Severidad,
        string Componente,
        int VersionEsquema,
        string Contexto);
}
