using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Las dos bitácoras mensuales fuera de SQLite (escribir-bitacoras-externas): una carpeta de datos temporal real por caso,
/// con la base adentro, el fixture sintético, un reloj fijo y la pasada del escritor llamada sin temporizador.
/// </summary>
public class BitacorasExternasTests : IDisposable
{
    private const string Generacion = "00000000-0000-0000-0000-000000000001";

    private static readonly string[] CamposDelContrato =
    [
        "SchemaVersion", "AuditId", "KioskId", "GenerationId", "Sequence", "BootId", "OccurredAtUtc", "RecordedAtUtc", "TimeZone",
        "Offset", "LogMonth", "Stream", "EventType", "Severity", "Component", "ExportedAtUtc", "Recovered", "Context",
    ];

    private readonly ImpresoraDePrueba _impresora = new();

    public BitacorasExternasTests()
    {
        Directory.CreateDirectory(Carpeta);
        Base = BaseComedor.Abrir(Path.Combine(Carpeta, "incomedor.db"));
        Fixture = FixtureSintetico.Cargar(Base);
    }

    private string Carpeta { get; } = Path.Combine(Path.GetTempPath(), "incomedor-tests", "bitacoras-" + Guid.NewGuid().ToString("N"));

    private BaseComedor Base { get; }

    private FixtureSintetico Fixture { get; }

    /// <summary>06/11/2026 a las 12:00 en Buenos Aires.</summary>
    private RelojFijo Reloj { get; } = new("2026-11-06T15:00:00Z");

    private string CarpetaEsperada => Path.Combine(Carpeta, "bitacoras", Fixture.PuestoId.ToString());

    public void Dispose()
    {
        if (!Directory.Exists(Carpeta))
        {
            return;
        }

        foreach (var archivo in Directory.EnumerateFiles(Carpeta, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(archivo, FileAttributes.Normal);
        }

        Directory.Delete(Carpeta, recursive: true);
    }

    [Fact(DisplayName = "bitacoras-externas · Nombres del mes: un consumo del 06/11/2026 deja INComedor-general-202611.log e INComedor-interacciones-202611.log en bitacoras\\<KioskId>\\, con un objeto JSON por línea y ninguna bitácora fuera de esa carpeta")]
    public void Nombres_del_mes()
    {
        Atender();
        var escritor = NuevoEscritor();

        escritor.Entregar();

        Assert.Equal(CarpetaEsperada, escritor.CarpetaPuesto);
        foreach (var nombre in new[] { "INComedor-general-202611.log", "INComedor-interacciones-202611.log" })
        {
            var ruta = Path.Combine(CarpetaEsperada, nombre);
            Assert.True(File.Exists(ruta), $"Falta {nombre}");
            var bytes = File.ReadAllBytes(ruta);
            Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), "El archivo no lleva BOM");
            Assert.Equal((byte)'\n', bytes[^1]);
            Assert.All(Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries),
                linea => Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(linea).RootElement.ValueKind));
        }

        Assert.All(Directory.EnumerateFiles(Carpeta, "*.log", SearchOption.AllDirectories),
            archivo => Assert.Equal(CarpetaEsperada, Path.GetDirectoryName(archivo)));
    }

    [Fact(DisplayName = "bitacoras-externas · Hecho del último minuto del mes: un consumo del 30/11 a las 23:59 locales, ya diciembre en UTC, va al archivo de 202611 aunque se entregue el 01/12")]
    public void Hecho_del_ultimo_minuto_del_mes()
    {
        Reloj.Ahora = DateTimeOffset.Parse("2026-12-01T02:59:00Z");
        var resultado = Atender();
        Reloj.Ahora = DateTimeOffset.Parse("2026-12-01T15:00:00Z");

        NuevoEscritor().Entregar();

        var autorizacion = Assert.Single(Lineas(DestinoBitacora.Interacciones, "202611"), l => Tipo(l) == "CONSUMPTION_AUTHORIZED");
        Assert.Equal(resultado.TicketId, autorizacion.GetProperty("Context").GetProperty("TicketId").GetGuid());
        Assert.Equal("202611", autorizacion.GetProperty("LogMonth").GetString());
        Assert.False(File.Exists(Path.Combine(CarpetaEsperada, "INComedor-interacciones-202612.log")));
    }

    [Fact(DisplayName = "bitacoras-externas · Campos de una autorización: la línea tiene todos los campos del contrato, el mismo AuditId y la misma Sequence que la base, y en Context el IntentId, el DecisionId, el ConsumptionId y el TicketId")]
    public void Campos_de_una_autorizacion()
    {
        var resultado = Atender(out var intentId);

        NuevoEscritor().Entregar();

        var linea = Assert.Single(Lineas(DestinoBitacora.Interacciones, "202611"), l => Tipo(l) == "CONSUMPTION_AUTHORIZED");
        Assert.Equal(CamposDelContrato, linea.EnumerateObject().Select(p => p.Name));
        var registro = Assert.Single(RegistrosDeLaBase(), r => r.Tipo == "CONSUMPTION_AUTHORIZED");
        Assert.Equal(registro.AuditId, linea.GetProperty("AuditId").GetGuid());
        Assert.Equal(registro.Secuencia, linea.GetProperty("Sequence").GetInt64());
        Assert.Equal(Fixture.PuestoId, linea.GetProperty("KioskId").GetGuid());
        Assert.Equal(Guid.Parse(Generacion), linea.GetProperty("GenerationId").GetGuid());
        Assert.Equal(registro.Arranque, linea.GetProperty("BootId").GetGuid());
        Assert.Equal("2026-11-06T15:00:00.000Z", linea.GetProperty("OccurredAtUtc").GetString());
        Assert.Equal("2026-11-06T15:00:00.000Z", linea.GetProperty("RecordedAtUtc").GetString());
        Assert.Equal(FixtureSintetico.ZonaHoraria, linea.GetProperty("TimeZone").GetString());
        Assert.Equal("-03:00", linea.GetProperty("Offset").GetString());
        Assert.Equal("INTERACTIONS", linea.GetProperty("Stream").GetString());
        Assert.Equal("INFO", linea.GetProperty("Severity").GetString());
        Assert.Equal("Infrastructure.Sqlite.UnidadConfirmacion", linea.GetProperty("Component").GetString());
        Assert.Equal("2026-11-06T15:00:00.000Z", linea.GetProperty("ExportedAtUtc").GetString());
        Assert.False(linea.GetProperty("Recovered").GetBoolean());
        var contexto = linea.GetProperty("Context");
        Assert.Equal(intentId, contexto.GetProperty("IntentId").GetGuid());
        Assert.Equal(resultado.DecisionId, contexto.GetProperty("DecisionId").GetGuid());
        Assert.Equal(resultado.ConsumptionId, contexto.GetProperty("ConsumptionId").GetGuid());
        Assert.Equal(resultado.TicketId, contexto.GetProperty("TicketId").GetGuid());
    }

    [Fact(DisplayName = "bitacoras-externas · Ningún código en los archivos: un código sintético reconocible que se rechaza una vez por desconocido y después se autoriza no aparece en ninguna línea")]
    public void Ningun_codigo_en_los_archivos()
    {
        const string codigo = "7351984602";
        var identificacion = new IdentificacionPorCodigo(Base, Reloj);
        Assert.False(identificacion.Identificar(codigo, PropositoSesion.Consumption).Abierta);
        using (var conexion = Base.AbrirConexion())
        {
            Sql.Ejecutar(conexion,
                "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, NULL, NULL)",
                ("$vc", Fixture.SnapshotId), ("$cre", Guid.NewGuid()), ("$per", Fixture.Habilitada.Id), ("$valor", codigo));
        }

        Assert.Equal(TipoResultado.Autorizada, Atender(codigo, out _).Tipo);

        NuevoEscritor().Entregar();

        var archivos = Directory.GetFiles(CarpetaEsperada, "*.log");
        Assert.Equal(2, archivos.Length);
        Assert.All(archivos, archivo => Assert.DoesNotContain(codigo, File.ReadAllText(archivo)));
    }

    [Fact(DisplayName = "bitacoras-externas · Identidad no resuelta: la línea del rechazo de un código desconocido no tiene PersonId y tiene el motivo IDENTITY_NOT_RESOLVED")]
    public void Identidad_no_resuelta()
    {
        new IdentificacionPorCodigo(Base, Reloj).Identificar("999999", PropositoSesion.Consumption);

        NuevoEscritor().Entregar();

        var linea = Assert.Single(Lineas(DestinoBitacora.Interacciones, "202611"), l => Tipo(l) == "IDENTIFICATION_REJECTED");
        var contexto = linea.GetProperty("Context");
        Assert.False(contexto.TryGetProperty("PersonId", out _));
        Assert.Equal("IDENTITY_NOT_RESOLVED", contexto.GetProperty("ReasonCode").GetString());
    }

    [Fact(DisplayName = "bitacoras-externas · Impresión en las dos: el trabajo que pasa de UNKNOWN a SUBMITTED está en la general y en la de interacciones con el mismo AuditId y cada una con su Stream")]
    public void Impresion_en_las_dos()
    {
        Atender();

        NuevoEscritor().Entregar();

        var general = Lineas(DestinoBitacora.General, "202611").Where(l => Tipo(l) == "PRINT_JOB_UPDATED").ToList();
        var interacciones = Lineas(DestinoBitacora.Interacciones, "202611").Where(l => Tipo(l) == "PRINT_JOB_UPDATED").ToList();
        var enLaBase = RegistrosDeLaBase().Where(r => r.Tipo == "PRINT_JOB_UPDATED").Select(r => r.AuditId).ToList();
        Assert.Equal(2, enLaBase.Count);
        Assert.Equal(enLaBase, general.Select(l => l.GetProperty("AuditId").GetGuid()));
        Assert.Equal(enLaBase, interacciones.Select(l => l.GetProperty("AuditId").GetGuid()));
        Assert.Contains(general, l => l.GetProperty("Context").GetProperty("State").GetString() == "SUBMITTED");
        Assert.All(general, l => Assert.Equal("GENERAL", l.GetProperty("Stream").GetString()));
        Assert.All(interacciones, l => Assert.Equal("INTERACTIONS", l.GetProperty("Stream").GetString()));
    }

    [Fact(DisplayName = "bitacoras-externas · Tipo sin destino conocido: un registro de un tipo que no figura en el reparto está en la general y no en la de interacciones")]
    public void Tipo_sin_destino_conocido()
    {
        new IdentificacionPorCodigo(Base, Reloj).Identificar("999999", PropositoSesion.Consumption);
        var desconocido = Registrar("TIPO_NUEVO_SIN_REPARTO", new { Prueba = 1 });

        NuevoEscritor().Entregar();

        Assert.Contains(Lineas(DestinoBitacora.General, "202611"), l => l.GetProperty("AuditId").GetGuid() == desconocido);
        Assert.DoesNotContain(Lineas(DestinoBitacora.Interacciones, "202611"), l => l.GetProperty("AuditId").GetGuid() == desconocido);
    }

    [Fact(DisplayName = "bitacoras-externas · Orden de las líneas: identificación, oferta, confirmación e impresión quedan en orden creciente de Sequence y sin huecos entre los registros de cada destino")]
    public void Orden_de_las_lineas()
    {
        Atender();

        NuevoEscritor().Entregar();

        var enLaBase = RegistrosDeLaBase();
        Assert.True(enLaBase.Count >= 4);
        Assert.Equal(enLaBase.Select(r => r.Secuencia), Secuencias(DestinoBitacora.Interacciones));
        Assert.Equal(enLaBase.Where(r => r.Tipo == "PRINT_JOB_UPDATED").Select(r => r.Secuencia), Secuencias(DestinoBitacora.General));
    }

    [Fact(DisplayName = "bitacoras-externas · Caída después de escribir y antes de seguir: con las líneas hasta la Sequence 40 ya escritas, el arranque siguiente deja una sola línea por AuditId, de la 1 a la última de la base")]
    public void Caida_despues_de_escribir_y_antes_de_seguir()
    {
        var registros = RegistrarVarios(45);
        EscribirAMano(DestinoBitacora.Interacciones, registros.Take(40));

        NuevoEscritor().Entregar();

        var auditIds = Lineas(DestinoBitacora.Interacciones, "202611").Select(l => l.GetProperty("AuditId").GetGuid()).ToList();
        Assert.Equal(registros.Select(r => r.AuditId), auditIds);
        Assert.Equal(auditIds.Count, auditIds.Distinct().Count());
    }

    [Fact(DisplayName = "bitacoras-externas · Línea parcial: los bytes cortados pasan a <archivo>.reparacion.log, el archivo termina en una línea completa de ese mismo registro y ninguna línea válida anterior se pierde")]
    public void Linea_parcial()
    {
        var registros = RegistrarVarios(12);
        var ruta = EscribirAMano(DestinoBitacora.Interacciones, registros.Take(10));
        var completas = File.ReadAllBytes(ruta);
        var cortada = Encoding.UTF8.GetBytes(LineaAMano(registros[10]))[..25];
        File.AppendAllText(ruta, Encoding.UTF8.GetString(cortada), new UTF8Encoding(false));

        NuevoEscritor().Entregar();

        Assert.Equal(cortada.Concat("\n"u8.ToArray()), File.ReadAllBytes(ruta + ".reparacion.log"));
        Assert.Equal(completas, File.ReadAllBytes(ruta)[..completas.Length]);
        var lineas = Lineas(DestinoBitacora.Interacciones, "202611");
        Assert.Equal(registros.Select(r => r.AuditId), lineas.Select(l => l.GetProperty("AuditId").GetGuid()));
        Assert.Equal(registros[10].AuditId, lineas[10].GetProperty("AuditId").GetGuid());
        Assert.Equal("OPTION_SELECTED", Tipo(lineas[10]));
        var reparada = Assert.Single(Lineas(DestinoBitacora.General, "202611"), l => Tipo(l) == "LOG_LINE_REPAIRED");
        Assert.Equal("WARNING", reparada.GetProperty("Severity").GetString());
        Assert.Equal(Path.GetFileName(ruta), reparada.GetProperty("Context").GetProperty("File").GetString());
        Assert.Equal(25, reparada.GetProperty("Context").GetProperty("Bytes").GetInt32());
    }

    [Fact(DisplayName = "bitacoras-externas · Base y archivo no coinciden: si la Sequence 12 del archivo tiene otro AuditId, ese archivo no cambia, la base conserva sus registros y la general recibe una sola LOG_SEQUENCE_MISMATCH con el archivo y la secuencia")]
    public void Base_y_archivo_no_coinciden()
    {
        var registros = RegistrarVarios(15);
        var ajeno = registros.Take(12).Select((r, i) => i == 11 ? r with { AuditId = Guid.NewGuid() } : r).ToList();
        var ruta = EscribirAMano(DestinoBitacora.Interacciones, ajeno);
        var antes = File.ReadAllBytes(ruta);
        var escritor = NuevoEscritor();

        escritor.Entregar();
        escritor.Entregar();

        Assert.Equal(antes, File.ReadAllBytes(ruta));
        var enLaBase = RegistrosDeLaBase();
        Assert.Equal(registros.Select(r => r.AuditId), enLaBase.Where(r => r.Tipo == "OPTION_SELECTED").Select(r => r.AuditId));
        var desajuste = Assert.Single(enLaBase, r => r.Tipo == "LOG_SEQUENCE_MISMATCH");
        var linea = Assert.Single(Lineas(DestinoBitacora.General, "202611"), l => Tipo(l) == "LOG_SEQUENCE_MISMATCH");
        Assert.Equal(desajuste.AuditId, linea.GetProperty("AuditId").GetGuid());
        Assert.Equal("ERROR", linea.GetProperty("Severity").GetString());
        Assert.Equal(Path.GetFileName(ruta), linea.GetProperty("Context").GetProperty("File").GetString());
        Assert.Equal(12, linea.GetProperty("Context").GetProperty("Sequence").GetInt64());
    }

    [Fact(DisplayName = "bitacoras-externas · Archivo bloqueado durante una confirmación: con la de interacciones sin permiso de escritura, la confirmación termina autorizada con su comprobante y su registro queda en la base pendiente de entrega")]
    public void Archivo_bloqueado_durante_una_confirmacion()
    {
        var ruta = ArchivoDeSoloLectura(DestinoBitacora.Interacciones, "202611");
        var escritor = NuevoEscritor();

        var resultado = Atender();
        escritor.Entregar();

        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        Assert.NotNull(resultado.TicketId);
        Assert.Equal(0, new FileInfo(ruta).Length);
        Assert.Contains(RegistrosDeLaBase(), r => r.Tipo == "CONSUMPTION_AUTHORIZED");
        var interacciones = Assert.Single(escritor.Estado(), e => e.Destino == DestinoBitacora.Interacciones);
        Assert.False(interacciones.AlDia);
        Assert.Equal(RegistrosDeLaBase().Count(r => r.Tipo != "LOG_DELIVERY_FAILED"), interacciones.Pendientes);
    }

    [Fact(DisplayName = "bitacoras-externas · Falla la general: con la general sin permiso de escritura, la atención termina autorizada, la de interacciones tiene sus líneas y la base tiene una sola LOG_DELIVERY_FAILED abierta para la general y sus pendientes")]
    public void Falla_la_general()
    {
        ArchivoDeSoloLectura(DestinoBitacora.General, "202611");
        var escritor = NuevoEscritor();

        var resultado = Atender();
        escritor.Entregar();
        escritor.Entregar();
        escritor.Entregar();

        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        var enLaBase = RegistrosDeLaBase();
        Assert.Equal(enLaBase.Where(r => r.Tipo != "LOG_DELIVERY_FAILED").Select(r => r.AuditId),
            Lineas(DestinoBitacora.Interacciones, "202611").Select(l => l.GetProperty("AuditId").GetGuid()));
        var falla = Assert.Single(enLaBase, r => r.Tipo == "LOG_DELIVERY_FAILED");
        var contexto = ContextoDe(falla.AuditId);
        Assert.Equal("GENERAL", contexto.GetProperty("Stream").GetString());
        Assert.Equal("2026-11-06T15:00:00.000Z", contexto.GetProperty("OpenedAtUtc").GetString());
        Assert.False(string.IsNullOrEmpty(contexto.GetProperty("Cause").GetString()));
        Assert.Equal(enLaBase.First(r => r.Tipo == "PRINT_JOB_UPDATED").AuditId, contexto.GetProperty("FirstPendingAuditId").GetGuid());
        Assert.Equal("ERROR", SeveridadDe(falla.AuditId));
        Assert.DoesNotContain(enLaBase, r => r.Tipo == "LOG_DELIVERY_RECOVERED");
        var general = Assert.Single(escritor.Estado(), e => e.Destino == DestinoBitacora.General);
        Assert.False(general.AlDia);
        Assert.Equal(3, general.Pendientes);
        Assert.Equal(DateTimeOffset.Parse("2026-11-06T15:00:00Z"), general.DegradadaDesde);
    }

    [Fact(DisplayName = "bitacoras-externas · Falla repetida: tres pasadas, la segunda y la tercera después de un arranque nuevo del escritor, dejan una sola incidencia abierta")]
    public void Falla_repetida_deja_una_sola_incidencia()
    {
        ArchivoDeSoloLectura(DestinoBitacora.General, "202611");
        Atender();

        NuevoEscritor().Entregar();
        var otroArranque = new EscritorBitacoras(Base, Carpeta, Reloj, Guid.NewGuid());
        otroArranque.Entregar();
        otroArranque.Entregar();

        Assert.Single(RegistrosDeLaBase(), r => r.Tipo == "LOG_DELIVERY_FAILED");
        Assert.False(Assert.Single(otroArranque.Estado(), e => e.Destino == DestinoBitacora.General).AlDia);
    }

    [Fact(DisplayName = "bitacoras-externas · Fallan las dos: con la carpeta de bitácoras sin permiso de escritura, dos personas consumen con sus comprobantes y la base tiene una incidencia abierta por archivo y todos los registros pendientes")]
    public void Fallan_las_dos()
    {
        File.WriteAllText(Path.Combine(Carpeta, "bitacoras"), "no es una carpeta");
        var segunda = NuevaPersonaHabilitada("4242");
        var escritor = NuevoEscritor();

        var primera = Atender();
        escritor.Entregar();
        var otra = Atender("4242", out _);
        escritor.Entregar();

        Assert.Equal(TipoResultado.Autorizada, primera.Tipo);
        Assert.Equal(TipoResultado.Autorizada, otra.Tipo);
        Assert.NotEqual(primera.TicketId, otra.TicketId);
        using (var conexion = Base.AbrirConexion())
        {
            Assert.Equal(2L, Sql.Contar(conexion, "tbComedorComprobantes"));
            Assert.Equal(1L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorConsumos WHERE comperCodigo = $per", ("$per", segunda)));
        }

        var fallas = RegistrosDeLaBase().Where(r => r.Tipo == "LOG_DELIVERY_FAILED").Select(r => ContextoDe(r.AuditId).GetProperty("Stream").GetString());
        Assert.Equal(["GENERAL", "INTERACTIONS"], fallas.Order());
        var enLaBase = RegistrosDeLaBase();
        var estado = escritor.Estado();
        Assert.All(estado, e => Assert.False(e.AlDia));
        Assert.Equal(enLaBase.Count(r => r.Tipo != "LOG_DELIVERY_FAILED"), estado.Single(e => e.Destino == DestinoBitacora.Interacciones).Pendientes);
        Assert.Equal(enLaBase.Count(r => r.Tipo is "PRINT_JOB_UPDATED" or "LOG_DELIVERY_FAILED"), estado.Single(e => e.Destino == DestinoBitacora.General).Pendientes);
    }

    [Fact(DisplayName = "bitacoras-externas · Recuperación de la general: al volver el permiso, la general recibe los tres pendientes con Recovered y su hora original, LOG_DELIVERY_RECOVERED con la apertura, el cierre y la cantidad queda en las dos, y los consumos no cambian")]
    public void Recuperacion_de_la_general()
    {
        Atender();
        var escritor = NuevoEscritor();
        escritor.Entregar();
        var ruta = Path.Combine(CarpetaEsperada, EscritorBitacoras.NombreArchivo(DestinoBitacora.General, "202611"));
        File.SetAttributes(ruta, FileAttributes.ReadOnly);
        Reloj.Ahora = DateTimeOffset.Parse("2026-11-06T15:01:00Z");
        Registrar("TIPO_DE_PRUEBA_GENERAL", new { Prueba = 1 });
        Registrar("TIPO_DE_PRUEBA_GENERAL", new { Prueba = 2 });
        escritor.Entregar();
        var pendientes = RegistrosDeLaBase().Where(r => r.Tipo is "TIPO_DE_PRUEBA_GENERAL" or "LOG_DELIVERY_FAILED").ToList();
        Assert.Equal(3, pendientes.Count);
        var consumosAntes = ContarConsumos();

        Reloj.Ahora = DateTimeOffset.Parse("2026-11-06T15:05:00Z");
        File.SetAttributes(ruta, FileAttributes.Normal);
        escritor.Entregar();

        var general = Lineas(DestinoBitacora.General, "202611");
        foreach (var pendiente in pendientes)
        {
            var linea = Assert.Single(general, l => l.GetProperty("AuditId").GetGuid() == pendiente.AuditId);
            Assert.True(linea.GetProperty("Recovered").GetBoolean());
            Assert.Equal("2026-11-06T15:01:00.000Z", linea.GetProperty("OccurredAtUtc").GetString());
            Assert.Equal("2026-11-06T15:05:00.000Z", linea.GetProperty("ExportedAtUtc").GetString());
        }

        foreach (var destino in new[] { DestinoBitacora.General, DestinoBitacora.Interacciones })
        {
            var recuperada = Assert.Single(Lineas(destino, "202611"), l => Tipo(l) == "LOG_DELIVERY_RECOVERED");
            var contexto = recuperada.GetProperty("Context");
            Assert.Equal("GENERAL", contexto.GetProperty("Stream").GetString());
            Assert.Equal("2026-11-06T15:01:00.000Z", contexto.GetProperty("OpenedAtUtc").GetString());
            Assert.Equal("2026-11-06T15:05:00.000Z", contexto.GetProperty("ClosedAtUtc").GetString());
            Assert.Equal(3, contexto.GetProperty("DeliveredCount").GetInt32());
            Assert.Equal("WARNING", recuperada.GetProperty("Severity").GetString());
        }

        Assert.Equal(consumosAntes, ContarConsumos());
        Assert.All(escritor.Estado(), e => Assert.True(e.AlDia));
    }

    [Fact(DisplayName = "bitacoras-externas · Pendiente de un mes anterior: registros de octubre pendientes por una falla que se recupera en noviembre van al archivo de 202610, a continuación de lo que ya tenía")]
    public void Pendiente_de_un_mes_anterior()
    {
        Reloj.Ahora = DateTimeOffset.Parse("2026-10-31T15:00:00Z");
        Atender();
        var escritor = NuevoEscritor();
        escritor.Entregar();
        var ruta = Path.Combine(CarpetaEsperada, EscritorBitacoras.NombreArchivo(DestinoBitacora.Interacciones, "202610"));
        var antes = File.ReadAllBytes(ruta);
        File.SetAttributes(ruta, FileAttributes.ReadOnly);
        var primera = Registrar("OPTION_SELECTED", new { Prueba = 1 });
        var segunda = Registrar("OPTION_SELECTED", new { Prueba = 2 });
        escritor.Entregar();

        Reloj.Ahora = DateTimeOffset.Parse("2026-11-02T15:00:00Z");
        File.SetAttributes(ruta, FileAttributes.Normal);
        escritor.Entregar();

        Assert.Equal(antes, File.ReadAllBytes(ruta)[..antes.Length]);
        var octubre = Lineas(DestinoBitacora.Interacciones, "202610");
        Assert.Equal([primera, segunda], octubre.TakeLast(2).Select(l => l.GetProperty("AuditId").GetGuid()));
        Assert.All(octubre.TakeLast(2), l => Assert.True(l.GetProperty("Recovered").GetBoolean()));
        Assert.DoesNotContain(Lineas(DestinoBitacora.Interacciones, "202611"), l => l.GetProperty("AuditId").GetGuid() == primera);
    }

    private EscritorBitacoras NuevoEscritor() => new(Base, Carpeta, Reloj);

    /// <summary>Un archivo vacío de sólo lectura: el escritor lo lee pero no puede agregarle nada.</summary>
    private string ArchivoDeSoloLectura(DestinoBitacora destino, string mes)
    {
        Directory.CreateDirectory(CarpetaEsperada);
        var ruta = Path.Combine(CarpetaEsperada, EscritorBitacoras.NombreArchivo(destino, mes));
        File.WriteAllBytes(ruta, []);
        File.SetAttributes(ruta, FileAttributes.ReadOnly);
        return ruta;
    }

    /// <summary>Otra persona con su código y una regla de ALMUERZO de límite 1, para que consuma igual que la habilitada.</summary>
    private Guid NuevaPersonaHabilitada(string codigo)
    {
        var persona = Guid.NewGuid();
        using var conexion = Base.AbrirConexion();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Otra persona habilitada', 1, 1)",
            ("$vc", Fixture.SnapshotId), ("$per", persona));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, NULL, NULL)",
            ("$vc", Fixture.SnapshotId), ("$cre", Guid.NewGuid()), ("$per", persona), ("$valor", codigo));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorReglasHabilitacion VALUES ($vc, $reg, $per, NULL, $srv, $amb, $cat, 1, 1, NULL, NULL)",
            ("$vc", Fixture.SnapshotId), ("$reg", Guid.NewGuid()), ("$per", persona),
            ("$srv", Fixture.ServicioAlmuerzo), ("$amb", Fixture.Ambito), ("$cat", Fixture.CategoriaAlmuerzo));
        return persona;
    }

    private long ContarConsumos()
    {
        using var conexion = Base.AbrirConexion();
        return Sql.Contar(conexion, "tbComedorConsumos");
    }

    private JsonElement ContextoDe(Guid auditId)
    {
        using var conexion = Base.AbrirConexion();
        return JsonDocument.Parse(Sql.Escalar<string>(conexion, "SELECT combitContexto FROM tbComedorBitacora WHERE combitCodigo = $id", ("$id", auditId)))
            .RootElement.Clone();
    }

    private string SeveridadDe(Guid auditId)
    {
        using var conexion = Base.AbrirConexion();
        return Sql.Escalar<string>(conexion, "SELECT combitSeveridad FROM tbComedorBitacora WHERE combitCodigo = $id", ("$id", auditId));
    }

    /// <summary>Registros de interacciones de prueba, como los que deja la atención al elegir una opción.</summary>
    private List<RegistroDeLaBase> RegistrarVarios(int cantidad)
    {
        for (var i = 0; i < cantidad; i++)
        {
            Registrar("OPTION_SELECTED", new { Prueba = i });
        }

        return RegistrosDeLaBase();
    }

    /// <summary>Escribe a mano, en el archivo del destino de 202611, una línea por registro con lo que el escritor lee.</summary>
    private string EscribirAMano(DestinoBitacora destino, IEnumerable<RegistroDeLaBase> registros)
    {
        Directory.CreateDirectory(CarpetaEsperada);
        var ruta = Path.Combine(CarpetaEsperada, EscritorBitacoras.NombreArchivo(destino, "202611"));
        File.WriteAllText(ruta, string.Concat(registros.Select(r => LineaAMano(r) + "\n")), new UTF8Encoding(false));
        return ruta;
    }

    private string LineaAMano(RegistroDeLaBase registro) => JsonSerializer.Serialize(new
    {
        SchemaVersion = 1,
        registro.AuditId,
        KioskId = Fixture.PuestoId,
        GenerationId = Guid.Parse(Generacion),
        Sequence = registro.Secuencia,
        LogMonth = "202611",
        Stream = "INTERACTIONS",
        EventType = registro.Tipo,
    });

    private ResultadoConfirmacion Atender() => Atender(FixtureSintetico.CodigoSintetico, out _);

    private ResultadoConfirmacion Atender(out Guid intentId) => Atender(FixtureSintetico.CodigoSintetico, out intentId);

    /// <summary>Identifica con el código, ofrece, confirma la primera opción ofrecida e imprime el original.</summary>
    private ResultadoConfirmacion Atender(string codigo, out Guid intentId)
    {
        var sesion = new IdentificacionPorCodigo(Base, Reloj).Identificar(codigo, PropositoSesion.Consumption);
        Assert.True(sesion.Abierta);
        var oferta = new ConsultaDisponibilidad(Base, Reloj).Ofrecer(sesion.IdentitySessionId!.Value, sesion.PersonId!.Value);
        var opcion = oferta.Ofrecidas[0];
        intentId = Guid.NewGuid();
        var resultado = new UnidadConfirmacion(Base, Reloj).Confirmar(
            new SolicitudConfirmacion(intentId, sesion.IdentitySessionId.Value, sesion.PersonId.Value, opcion.Id, opcion.Version, 1));
        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        new TrabajosImpresion(Base, Reloj, _impresora).EnviarPendientes();
        return resultado;
    }

    private Guid Registrar(string tipo, object contexto)
    {
        using var conexion = Base.AbrirConexion();
        using var transaccion = BaseComedor.IniciarTransaccionEscritura(conexion);
        new RegistroBitacora("Pruebas").Insertar(conexion, transaccion, PuestoLocal.Leer(conexion, transaccion), Reloj.GetUtcNow(), tipo, contexto);
        transaccion.Commit();
        return Guid.Parse((string)new SqliteCommand(
            "SELECT combitCodigo FROM tbComedorBitacora ORDER BY combitSecuencia DESC LIMIT 1", conexion).ExecuteScalar()!);
    }

    private List<JsonElement> Lineas(DestinoBitacora destino, string mes)
    {
        var ruta = Path.Combine(CarpetaEsperada, EscritorBitacoras.NombreArchivo(destino, mes));
        return !File.Exists(ruta)
            ? []
            : File.ReadAllText(ruta).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => JsonDocument.Parse(l).RootElement.Clone()).ToList();
    }

    private List<long> Secuencias(DestinoBitacora destino) =>
        Lineas(destino, "202611").Select(l => l.GetProperty("Sequence").GetInt64()).ToList();

    private static string? Tipo(JsonElement linea) => linea.GetProperty("EventType").GetString();

    private List<RegistroDeLaBase> RegistrosDeLaBase()
    {
        using var conexion = Base.AbrirConexion();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT combitCodigo, combitSecuencia, combitTipoEvento, combitCodigoArranque FROM tbComedorBitacora ORDER BY combitSecuencia";
        using var lector = comando.ExecuteReader();
        var registros = new List<RegistroDeLaBase>();
        while (lector.Read())
        {
            registros.Add(new RegistroDeLaBase(lector.GetGuid(0), lector.GetInt64(1), lector.GetString(2), lector.GetGuid(3)));
        }

        return registros;
    }

    private sealed record RegistroDeLaBase(Guid AuditId, long Secuencia, string Tipo, Guid Arranque);
}
