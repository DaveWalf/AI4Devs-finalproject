using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Intelektron.INComedor.Infrastructure.Sqlite;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Los correos en el puesto (avisos-correo de avisar-por-correo, D1 a D6 y D9): la configuración, el asunto, la cola y el
/// envío simulado, los avisos desde la atención y la señal de vida, sobre un servicio real con la base de la demostración,
/// con 0001 vigente y 0002 vencida, a las 12:00 del 06/11/2026 en Buenos Aires. Las casillas son de `ejemplo.com`.
/// </summary>
public sealed partial class AvisosCorreoTests : IDisposable
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    private static readonly ConfiguracionCorreo Activos = ConfiguracionCorreo.PorDefecto with
    {
        Activos = true,
        Cliente = "Cliente Demo",
        Remitente = "puesto@ejemplo.com",
        Servidor = "smtp.ejemplo.local",
        Operadores = ["operador1@ejemplo.com", "operador2@ejemplo.com"],
        Administrador = "admin@ejemplo.com",
        // La señal de vida, fuera del horario de las pruebas: sólo la suya la pide a las 08:00.
        HoraSenal = "23:59",
    };

    private readonly string _carpeta = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "incomedor-tests", "correo-" + Guid.NewGuid().ToString("N"))).FullName;

    private readonly RelojManual _reloj = new("2026-11-06T15:00:00Z");
    private readonly ImpresoraDePrueba _impresora = new();
    private readonly ServicioAtencion _servicio;
    private readonly MaquinaAtencion _maquina;

    public AvisosCorreoTests()
    {
        _servicio = new ServicioAtencion(Path.Combine(_carpeta, "incomedor.db"), _reloj, _impresora);
        _maquina = new MaquinaAtencion(_servicio);
        _maquina.Iniciar();
        new AlmacenDatosPuesto(_carpeta).Guardar(new DatosPuesto(3, "Comedor Norte", false, 1));
    }

    public void Dispose()
    {
        _servicio.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_carpeta, recursive: true);
    }

    [Fact(DisplayName = "avisos-correo · Casillas fuera de forma: una casilla sin arroba, el puerto 0, la hora 25:00 y un cliente con un salto de línea se señalan; lo guardado se lee con otro almacén, sin ninguna clave, y un archivo roto o con un nulo da lo de fábrica")]
    public void La_configuracion_y_su_validacion()
    {
        Assert.Empty(ConfiguracionCorreo.Validar(ConfiguracionCorreo.PorDefecto));
        Assert.Empty(ConfiguracionCorreo.Validar(Activos));
        Assert.Equal(["Remitente", "Servidor", "Administrador"],
            ConfiguracionCorreo.Validar(ConfiguracionCorreo.PorDefecto with { Activos = true }));
        Assert.Equal(["Operador1"], ConfiguracionCorreo.Validar(Activos with { Operadores = ["sin-arroba"] }));
        Assert.Equal(["Operador2"], ConfiguracionCorreo.Validar(Activos with { Operadores = ["a@ejemplo.com", "b@ejemplo.com, c@ejemplo.com"] }));
        Assert.Equal(["Puerto"], ConfiguracionCorreo.Validar(Activos with { Puerto = 0 }));
        Assert.Equal(["HoraSenal"], ConfiguracionCorreo.Validar(Activos with { HoraSenal = "25:00" }));
        Assert.Equal(["Cliente"], ConfiguracionCorreo.Validar(Activos with { Cliente = "Cliente\r\nBcc: otro@ejemplo.com" }));
        Assert.Equal(["Operador5"], ConfiguracionCorreo.Validar(Activos with { Operadores = Enumerable.Range(1, 6).Select(i => $"o{i}@ejemplo.com").ToList() }));

        var almacen = new AlmacenConfiguracionCorreo(_carpeta);
        almacen.Guardar(Activos);
        Assert.Equal(Activos, new AlmacenConfiguracionCorreo(_carpeta).Leer());
        var guardado = File.ReadAllText(Path.Combine(_carpeta, AlmacenConfiguracionCorreo.Nombre));
        using (var documento = JsonDocument.Parse(guardado))
        {
            Assert.DoesNotContain(documento.RootElement.EnumerateObject(),
                p => p.Name.Contains("clave", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("password", StringComparison.OrdinalIgnoreCase)
                     || p.Name.Contains("token", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("secret", StringComparison.OrdinalIgnoreCase));
        }

        Assert.Throws<ArgumentException>(() => almacen.Guardar(Activos with { Puerto = 0 }));

        File.WriteAllText(Path.Combine(_carpeta, AlmacenConfiguracionCorreo.Nombre), "{ roto");
        Assert.Equal(ConfiguracionCorreo.PorDefecto, almacen.Leer());
        File.WriteAllText(Path.Combine(_carpeta, AlmacenConfiguracionCorreo.Nombre), guardado.Replace("\"smtp.ejemplo.local\"", "null", StringComparison.Ordinal));
        Assert.Equal(ConfiguracionCorreo.PorDefecto, almacen.Leer());
    }

    [Fact(DisplayName = "avisos-correo · Una persona sin consumo: el asunto es «Cliente · Puesto N Nombre · Situación», sin el número si no hay, y sin caracteres de control")]
    public void El_asunto()
    {
        Assert.Equal("Cliente Demo · Puesto 3 Comedor Norte · Impresora",
            AvisosCorreo.Asunto("Cliente Demo", new DatosPuesto(3, "Comedor Norte", false, 1), SituacionCorreo.Impresora));
        Assert.Equal("Cliente Demo · Puesto 3 · Prueba", AvisosCorreo.Asunto("Cliente Demo", new DatosPuesto(3, "", false, 1), SituacionCorreo.Prueba));
        Assert.Equal("Cliente Demo · Comedor Norte · Prueba", AvisosCorreo.Asunto("Cliente Demo", new DatosPuesto(null, "Comedor Norte", false, 1), SituacionCorreo.Prueba));
        Assert.DoesNotContain('\n', AvisosCorreo.Asunto("Cliente\nDemo", new DatosPuesto(null, "", false, 1), SituacionCorreo.Prueba));
    }

    [Fact(DisplayName = "avisos-correo · Una persona sin consumo: 0002, vencida, avisa a los operadores con su nombre y el motivo, sin su código; un código desconocido no avisa")]
    public void Una_persona_sin_consumo()
    {
        using var avisos = Avisos(Activos);

        _maquina.Identificar("7777");
        _maquina.Identificar("0002");
        avisos.Pasada();

        var correo = Assert.Single(Enviados());
        Assert.Equal("Cliente Demo · Puesto 3 Comedor Norte · Persona sin consumo", correo.Asunto);
        Assert.Equal("operador1@ejemplo.com, operador2@ejemplo.com", correo.Para);
        Assert.Contains("Persona sintética 02 no pudo comer al marcar: su credencial está vencida.", correo.Cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("0002", correo.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("7777", correo.Texto, StringComparison.Ordinal);
        Assert.Null(_maquina.NombreVisible);
    }

    [Fact(DisplayName = "avisos-correo · La impresora sin papel: dos consumos seguidos sin papel dan un solo correo de «Impresora» a los operadores; pasada la ventana, otro")]
    public async Task La_impresora_sin_papel()
    {
        using var avisos = Avisos(Activos);
        _impresora.Modo = ModoImpresoraDePrueba.SinPapel;

        await Consumir("0001");
        await Consumir("0001");
        avisos.Pasada();

        var correo = Assert.Single(Enviados());
        Assert.EndsWith("· Impresora", correo.Asunto, StringComparison.Ordinal);
        Assert.Contains("no tiene papel", correo.Cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("admin@ejemplo.com", correo.Para, StringComparison.Ordinal);

        _reloj.Avanzar(AvisosCorreo.Ventana);
        _maquina.Identificar("0001");
        avisos.ComprobanteSinImprimir("PAPER_OUT");
        avisos.Pasada();
        Assert.Equal(2, Enviados().Count);
    }

    [Fact(DisplayName = "avisos-correo · La exportación que falla: con el SFTP simulado caído, el Administrador Principal recibe un correo de «Exportación» con el destino y el motivo, y los operadores no")]
    public async Task La_exportacion_que_falla()
    {
        using var avisos = Avisos(Activos);
        var almacen = new AlmacenConfiguracionExportacion(_carpeta);
        almacen.Guardar(ConfiguracionExportacion.PorDefecto with { Activa = true, SftpActivo = true, Servidor = "sftp.ejemplo.local", Usuario = "comedor", CarpetaRemota = "entrada" });
        using var exportacion = new ExportacionAutomatica(_servicio.Consumos!, _servicio.RegistroConfiguracion!, _reloj, Zona, _carpeta, almacen)
        {
            SftpCaido = true,
            AlFallar = avisos.ExportacionFallida,
        };
        await Consumir("0001");

        exportacion.Exportar();
        avisos.Pasada();

        var correo = Assert.Single(Enviados(), c => c.Asunto.EndsWith("· Exportación", StringComparison.Ordinal));
        Assert.Equal("admin@ejemplo.com", correo.Para);
        Assert.Contains("al SFTP simulado", correo.Cuerpo, StringComparison.Ordinal);
        Assert.Contains("SERVER_UNREACHABLE", correo.Cuerpo, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "avisos-correo · Una bitácora que se degrada avisa una vez al Administrador Principal, no mientras sigue degradada, y otra vez si vuelve a degradarse")]
    public void Una_bitacora_degradada()
    {
        using var avisos = Avisos(Activos);
        var desde = _reloj.GetUtcNow();

        avisos.Bitacoras([new EstadoBitacora(DestinoBitacora.General, false, 3, desde)]);
        avisos.Bitacoras([new EstadoBitacora(DestinoBitacora.General, false, 5, desde)]);
        avisos.Bitacoras([new EstadoBitacora(DestinoBitacora.General, true, 0, null)]);
        avisos.Bitacoras([new EstadoBitacora(DestinoBitacora.General, false, 1, desde.AddMinutes(1))]);
        avisos.Pasada();

        var correos = Enviados();
        Assert.Equal(2, correos.Count);
        Assert.All(correos, c => Assert.Equal("admin@ejemplo.com", c.Para));
        Assert.All(correos, c => Assert.EndsWith("· Bitácora", c.Asunto, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "avisos-correo · El servidor de correo cae y vuelve: «Enviar una prueba» con el servidor caído queda en la cola; levantado, la pasada del minuto deja el .eml y la cola vacía; la general registra sin direcciones")]
    public void El_servidor_cae_y_vuelve()
    {
        using var avisos = Avisos(Activos);
        avisos.Servidor.Caido = true;

        Assert.Equal(1, avisos.EnviarPrueba());
        Assert.Empty(Enviados());
        avisos.Pasada();
        Assert.Equal(1, avisos.Pendientes);

        avisos.Servidor.Caido = false;
        avisos.Pasada();

        var correo = Assert.Single(Enviados());
        Assert.EndsWith("· Prueba", correo.Asunto, StringComparison.Ordinal);
        Assert.Equal("operador1@ejemplo.com, operador2@ejemplo.com, admin@ejemplo.com", correo.Para);
        Assert.Equal(0, avisos.Pendientes);
        Assert.Single(Registros("NOTIFICATION_QUEUED"));
        Assert.Single(Registros("NOTIFICATION_FAILED"));
        Assert.Single(Registros("NOTIFICATION_SENT"));
        Assert.All(Registros("NOTIFICATION_QUEUED").Concat(Registros("NOTIFICATION_FAILED")).Concat(Registros("NOTIFICATION_SENT")),
            r => Assert.DoesNotContain("@", r, StringComparison.Ordinal));
        Assert.Null(Avisos(ConfiguracionCorreo.PorDefecto).EnviarPrueba());
    }

    [Fact(DisplayName = "avisos-correo · El envío simulado y la cola: con 500 pendientes, uno más descarta el más viejo y la cola sigue en 500")]
    public void El_tope_de_la_cola()
    {
        var cola = new ColaCorreo(_carpeta);
        for (var i = 0; i < ColaCorreo.Tope; i++)
        {
            cola.Encolar(Mensaje(i));
        }

        var descartados = cola.Encolar(Mensaje(ColaCorreo.Tope));

        Assert.Single(descartados);
        Assert.Equal(ColaCorreo.Tope, cola.Cantidad);
        Assert.Equal("1", cola.Pendientes()[0].Cuerpo);
    }

    [Fact(DisplayName = "avisos-correo · La señal de las 08:00: con dos consumos y un rechazo el día anterior, pasadas las 08:00 sale una sola «Señal de vida» al Administrador Principal, aunque el puesto se reinicie")]
    public async Task La_senal_de_vida()
    {
        await Consumir("0001");
        _maquina.Identificar("0002");
        await Consumir("0001");
        _reloj.Avanzar(TimeSpan.FromHours(19)); // 07:00 del 07/11 en Buenos Aires.
        using (var antes = Avisos(Activos with { HoraSenal = "08:00" }))
        {
            antes.Pasada();
            Assert.Empty(Enviados());
        }

        _reloj.Avanzar(TimeSpan.FromMinutes(61));
        using (var avisos = Avisos(Activos with { HoraSenal = "08:00" }))
        {
            avisos.Pasada();
            avisos.Pasada();
        }

        using (var reiniciado = Avisos(Activos with { HoraSenal = "08:00" }))
        {
            reiniciado.Pasada();
        }

        var correo = Assert.Single(Enviados());
        Assert.EndsWith("· Señal de vida", correo.Asunto, StringComparison.Ordinal);
        Assert.Equal("admin@ejemplo.com", correo.Para);
        Assert.Contains("Resumen del 06/11/2026: 2 consumos, 1 rechazo,", correo.Cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("Persona sintética", correo.Cuerpo, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "avisos-correo · El reloj de los correos corre cada minuto, el primero al minuto, y una pasada que falla no sale del reloj")]
    public void El_reloj()
    {
        var reloj = new RelojConTemporizadores(_reloj);
        using var avisos = new AvisosCorreo(_servicio.RegistroConfiguracion!, reloj, Zona, _carpeta, Configuracion(Activos),
            new AlmacenDatosPuesto(_carpeta), new ResumenDelDia(_servicio.Base));

        avisos.Programar();
        File.WriteAllText(Path.Combine(_carpeta, ColaCorreo.Carpeta), "un archivo donde va la carpeta");

        var temporizador = Assert.Single(reloj.Temporizadores);
        Assert.Equal(AvisosCorreo.Periodo, temporizador.Periodo);
        Assert.Equal(AvisosCorreo.Periodo, temporizador.Primero);
        temporizador.Disparar();
    }

    private AlmacenConfiguracionCorreo Configuracion(ConfiguracionCorreo configuracion)
    {
        var almacen = new AlmacenConfiguracionCorreo(_carpeta);
        almacen.Guardar(configuracion);
        return almacen;
    }

    private AvisosCorreo Avisos(ConfiguracionCorreo configuracion)
    {
        var avisos = new AvisosCorreo(_servicio.RegistroConfiguracion!, _reloj, Zona, _carpeta, Configuracion(configuracion),
            new AlmacenDatosPuesto(_carpeta), new ResumenDelDia(_servicio.Base));
        _servicio.Avisos = avisos;
        return avisos;
    }

    private MensajeCorreo Mensaje(int i) => new(Guid.NewGuid(), _reloj.GetUtcNow().AddMilliseconds(i), SituacionCorreo.Prueba,
        "puesto@ejemplo.com", ["admin@ejemplo.com"], "Asunto", i.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>El código confirma la primera opción que se le ofrece y vuelve a reposo.</summary>
    private async Task Consumir(string codigo)
    {
        Assert.Equal(ResultadoToque.Aceptado, _maquina.Identificar(codigo));
        _maquina.Elegir(_maquina.PaginaActual[0].Id);
        await _maquina.ConfirmarAsync();
        _maquina.Listo();
    }

    /// <summary>Los .eml de <c>correo-simulado\</c>, con el asunto decodificado.</summary>
    private List<Correo> Enviados()
    {
        var carpeta = Path.Combine(_carpeta, CorreoSimulado.Carpeta);
        return !Directory.Exists(carpeta)
            ? []
            : Directory.GetFiles(carpeta, "*.eml").Order(StringComparer.Ordinal).Select(a =>
            {
                var texto = File.ReadAllText(a, Encoding.UTF8);
                var asunto = Encoding.UTF8.GetString(Convert.FromBase64String(Subject().Match(texto).Groups[1].Value));
                var para = Para().Match(texto).Groups[1].Value.Trim();
                return new Correo(asunto, para, texto[(texto.IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4)..], texto);
            }).ToList();
    }

    [GeneratedRegex(@"^Subject: =\?utf-8\?B\?([A-Za-z0-9+/=]+)\?=\r$", RegexOptions.Multiline)]
    private static partial Regex Subject();

    [GeneratedRegex(@"^To: (.+)\r$", RegexOptions.Multiline)]
    private static partial Regex Para();

    private List<string> Registros(string tipo)
    {
        using var conexion = new SqliteConnection($"Data Source={Path.Combine(_carpeta, "incomedor.db")};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = $tipo ORDER BY combitSecuencia";
        comando.Parameters.AddWithValue("$tipo", tipo);
        using var lector = comando.ExecuteReader();
        var contextos = new List<string>();
        while (lector.Read())
        {
            contextos.Add(lector.GetString(0));
        }

        return contextos;
    }

    private sealed record Correo(string Asunto, string Para, string Cuerpo, string Texto);

    private sealed class RelojConTemporizadores(TimeProvider reloj) : TimeProvider
    {
        public List<TemporizadorDePrueba> Temporizadores { get; } = [];

        public override DateTimeOffset GetUtcNow() => reloj.GetUtcNow();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var temporizador = new TemporizadorDePrueba(() => callback(state), period) { Primero = dueTime };
            Temporizadores.Add(temporizador);
            return temporizador;
        }
    }

    private sealed class TemporizadorDePrueba(Action disparar, TimeSpan periodo) : ITimer
    {
        public TimeSpan Periodo { get; } = periodo;

        public TimeSpan Primero { get; init; }

        public void Disparar() => disparar();

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
