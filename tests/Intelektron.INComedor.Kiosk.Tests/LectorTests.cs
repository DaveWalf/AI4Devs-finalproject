using System.Text;
using Intelektron.INComedor.Kiosk.Atencion;
using Intelektron.INComedor.Kiosk.Lector;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El lector sin pantalla (lector-identificacion de elegir-medio-de-identificacion, D2 a D4, D7 y D9): la configuración y su
/// almacén, el armado de las tramas con el terminador, el largo, los caracteres, el resto viejo y el relleno, y el servicio
/// con el puerto simulado como doble; ninguna prueba abre un puerto COM.
/// </summary>
public sealed class LectorTests : IDisposable
{
    private static readonly DateTimeOffset Ahora = DateTimeOffset.Parse("2026-11-06T15:00:00Z");

    private readonly string _carpeta = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "incomedor-tests", "lector-" + Guid.NewGuid().ToString("N"))).FullName;

    private readonly RelojManual _reloj = new("2026-11-06T15:00:00Z");
    private readonly ServicioAtencion _servicio;

    public LectorTests()
    {
        _servicio = new ServicioAtencion(Path.Combine(_carpeta, "incomedor.db"), _reloj, new ImpresoraDePrueba());
        new MaquinaAtencion(_servicio).Iniciar();
    }

    public void Dispose()
    {
        _servicio.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_carpeta, recursive: true);
    }

    [Fact(DisplayName = "lector-identificacion · Un puerto fuera de forma: sin puerto, COM0, 300 baudios, 9 bits, 3 de parada y el relleno 25 se señalan; lo guardado se lee con otro almacén y un archivo roto da el teclado")]
    public void La_configuracion_y_su_validacion()
    {
        Assert.Empty(ConfiguracionLector.Validar(ConfiguracionLector.PorDefecto));
        var serie = ConfiguracionLector.PorDefecto with { Medio = MedioIdentificacion.PuertoSerie, Puerto = "COM3", Terminador = TerminadorLector.Cr, Relleno = 4 };
        Assert.Empty(ConfiguracionLector.Validar(serie));
        Assert.Equal(["Puerto"], ConfiguracionLector.Validar(serie with { Puerto = null }));
        Assert.Equal(["Puerto"], ConfiguracionLector.Validar(serie with { Puerto = "COM0" }));
        Assert.Equal(["Velocidad", "BitsDatos", "BitsParada", "Relleno"],
            ConfiguracionLector.Validar(serie with { Velocidad = 300, BitsDatos = 9, BitsParada = 3, Relleno = 25 }));
        Assert.Equal("9600 8N1", serie.Formato);

        var almacen = new AlmacenLector(_carpeta);
        almacen.Guardar(serie);
        Assert.Equal(serie, new AlmacenLector(_carpeta).Leer());
        Assert.Throws<ArgumentException>(() => almacen.Guardar(serie with { Puerto = null }));
        File.WriteAllText(Path.Combine(_carpeta, AlmacenLector.Nombre), "{ roto");
        Assert.Equal(ConfiguracionLector.PorDefecto, almacen.Leer());
    }

    [Fact(DisplayName = "lector-identificacion · Trama partida y dos tramas juntas: con CR LF, 00 y después 01 CR LF arman 0001, y 0003 CR LF 0004 CR LF en un bloque dan dos lecturas en orden")]
    public void Trama_partida_y_dos_tramas_juntas()
    {
        var armador = new ArmadorTramas(TerminadorLector.CrLf, 0);

        Assert.Empty(armador.Agregar(Bytes("00"), Ahora));
        var primera = armador.Agregar(Bytes("01\r\n"), Ahora.AddMilliseconds(300));
        var dos = armador.Agregar(Bytes("0003\r\n0004\r\n"), Ahora.AddSeconds(1));

        Assert.Equal(["0001"], primera.Select(l => l.Codigo));
        Assert.Equal(["0003", "0004"], dos.Select(l => l.Codigo));
    }

    [Fact(DisplayName = "lector-identificacion · Un resto viejo no se pega: 00, 3 segundos y 01 CR dan sólo 01, con 00 descartado; a los 2 segundos exactos todavía arma")]
    public void Un_resto_viejo_no_se_pega()
    {
        var armador = new ArmadorTramas(TerminadorLector.Cr, 0);
        armador.Agregar(Bytes("00"), Ahora);

        var lecturas = armador.Agregar(Bytes("01\r"), Ahora.AddSeconds(3));

        Assert.Equal([null, "01"], lecturas.Select(l => l.Codigo));
        Assert.Equal("STALE_FRAGMENT", lecturas[0].Descarte);

        armador.Agregar(Bytes("00"), Ahora.AddSeconds(10));
        Assert.Equal(["0001"], armador.Agregar(Bytes("01\r"), Ahora.AddSeconds(12)).Select(l => l.Codigo));
    }

    [Fact(DisplayName = "lector-identificacion · El armado de las tramas: un byte de control, uno fuera de ASCII, un espacio interno o más de 32 caracteres se descartan enteros; los espacios de alrededor se quitan; con TAB, el CR no cierra")]
    public void Ruido_y_exceso()
    {
        var armador = new ArmadorTramas(TerminadorLector.CrLf, 0);

        var lecturas = armador.Agregar([.. Bytes("00\u000701\r\n"), 0xC3, 0xB1, .. Bytes("\r\n00 01\r\n"), .. Bytes(new string('9', 33) + "\r\n  0005 \r\n")], Ahora);

        Assert.Equal(["INVALID_CHARACTERS", "INVALID_CHARACTERS", "INVALID_CHARACTERS", "TOO_LONG", null], lecturas.Select(l => l.Descarte));
        Assert.Equal("0005", lecturas[^1].Codigo);
        Assert.Equal(["TOO_LONG"], armador.Agregar(Bytes(new string('9', 500) + "\r\n"), Ahora).Select(l => l.Descarte));

        var tab = new ArmadorTramas(TerminadorLector.Tab, 0);
        Assert.Equal(["INVALID_CHARACTERS"], tab.Agregar(Bytes("00\r01\t"), Ahora).Select(l => l.Descarte));
    }

    [Fact(DisplayName = "lector-identificacion · La tarjeta de 0001: el relleno 4 completa 1 con ceros, no toca un código con letras ni uno ya largo")]
    public void El_relleno_de_digitos()
    {
        Assert.Equal("0001", ArmadorTramas.Rellenar("1", 4));
        Assert.Equal("A1", ArmadorTramas.Rellenar("A1", 4));
        Assert.Equal("12345", ArmadorTramas.Rellenar("12345", 4));
        Assert.Equal("1", ArmadorTramas.Rellenar("1", 0));
        Assert.Equal(["0001"], new ArmadorTramas(TerminadorLector.Cr, 4).Agregar(Bytes("1\r"), Ahora).Select(l => l.Codigo));
    }

    [Fact(DisplayName = "lector-identificacion · Pasar un código por el lector simulado: con el simulador interno, lo pasado llega como lectura con su origen, sin abrir ningún puerto real")]
    public void El_simulador_interno()
    {
        new AlmacenLector(_carpeta).Guardar(ConfiguracionLector.PorDefecto with { Medio = MedioIdentificacion.Simulador, Relleno = 4 });
        using var lector = Lector(real: null);
        var lecturas = new List<(string, string)>();
        lector.Lectura += (codigo, origen) => lecturas.Add((codigo, origen));

        lector.Aplicar();
        lector.Simulado.Pasar("1", lector.Configuracion);

        Assert.Equal(EstadoLector.Escuchando, lector.Estado);
        Assert.Equal([("0001", "SIMULATED_READER")], lecturas);
        Assert.Contains(Registros("READER_STATE_CHANGED"), r => r.Contains("\"LISTENING\"", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "lector-identificacion · El puerto vuelve: un puerto ocupado reintenta a los 5 segundos y escucha; si se cae, lo dice y vuelve; la general tiene cada cambio sin el código")]
    public void El_puerto_que_falta_o_se_cae()
    {
        var real = new PuertoSimulado { Falla = "PORT_BUSY" };
        new AlmacenLector(_carpeta).Guardar(ConfiguracionLector.PorDefecto with { Medio = MedioIdentificacion.PuertoSerie, Puerto = "COM3" });
        var reloj = new RelojConTemporizadores(_reloj);
        using var lector = new LectorIdentificacion(real, new AlmacenLector(_carpeta), _servicio.RegistroConfiguracion!, reloj);
        var lecturas = new List<string>();
        lector.Lectura += (codigo, _) => lecturas.Add(codigo);

        lector.Aplicar();
        Assert.Equal(EstadoLector.PuertoOcupado, lector.Estado);
        real.Falla = null;
        reloj.Temporizadores[^1].Disparar();
        Assert.Equal(EstadoLector.Escuchando, lector.Estado);

        real.Escribir(Bytes("0001\r\n"));
        real.Caer();
        Assert.Equal(EstadoLector.PuertoCaido, lector.Estado);
        reloj.Temporizadores[^1].Disparar();
        Assert.Equal(EstadoLector.Escuchando, lector.Estado);

        Assert.Equal(["0001"], lecturas);
        var cambios = Registros("READER_STATE_CHANGED");
        Assert.Equal(4, cambios.Count);
        Assert.All(cambios, c => Assert.DoesNotContain("0001", c, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "lector-identificacion · Una lectura durante la selección: los descartes del mismo motivo van a la general una vez por minuto, sin el código ni su largo")]
    public void Los_descartes_una_vez_por_minuto()
    {
        new AlmacenLector(_carpeta).Guardar(ConfiguracionLector.PorDefecto with { Medio = MedioIdentificacion.Simulador });
        using var lector = Lector(real: null);
        lector.Aplicar();

        lector.Resultado("DISCARDED", "NOT_IDLE");
        lector.Resultado("DISCARDED", "NOT_IDLE");
        lector.Resultado("DISCARDED", "DIALOG_OPEN");
        _reloj.Avanzar(TimeSpan.FromSeconds(61));
        lector.Resultado("DISCARDED", "NOT_IDLE");

        var descartes = Registros("READER_READING_DISCARDED");
        Assert.Equal(3, descartes.Count);
        Assert.Contains("\"Accumulated\":1", descartes[^1], StringComparison.Ordinal);
        Assert.Equal(new UltimaLectura(_reloj.GetUtcNow(), "DISCARDED", "NOT_IDLE"), lector.Ultima);
    }

    [Fact(DisplayName = "lector-identificacion · El puerto vuelve: un lector que se desenchufa mientras escucha se ve a los 5 segundos, cae, y al volver a enchufarlo escucha otra vez")]
    public void Un_lector_que_se_desenchufa()
    {
        var real = new PuertoSimulado();
        new AlmacenLector(_carpeta).Guardar(ConfiguracionLector.PorDefecto with { Medio = MedioIdentificacion.PuertoSerie, Puerto = "COM3" });
        var reloj = new RelojConTemporizadores(_reloj);
        using var lector = new LectorIdentificacion(real, new AlmacenLector(_carpeta), _servicio.RegistroConfiguracion!, reloj);

        lector.Aplicar();
        Assert.Equal(EstadoLector.Escuchando, lector.Estado);
        var vigilancia = Assert.Single(reloj.Temporizadores);
        Assert.Equal(LectorIdentificacion.Reintento, vigilancia.Primero);

        real.Puertos.Remove("COM3");
        vigilancia.Disparar();
        Assert.Equal(EstadoLector.PuertoCaido, lector.Estado);
        Assert.False(real.Abierto);

        real.Puertos.Add("COM3");
        reloj.Temporizadores[^1].Disparar();
        Assert.Equal(EstadoLector.Escuchando, lector.Estado);
        Assert.Contains(Registros("READER_STATE_CHANGED"), r => r.Contains("\"PORT_LOST\"", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "lector-identificacion · El puerto vuelve: un puerto que no se deja cerrar no frena el cambio de medio ni el cierre del puesto")]
    public void Un_cierre_que_falla()
    {
        var real = new PuertoSimulado { FallaAlCerrar = true };
        new AlmacenLector(_carpeta).Guardar(ConfiguracionLector.PorDefecto with { Medio = MedioIdentificacion.PuertoSerie, Puerto = "COM3" });
        var lector = Lector(real);
        lector.Aplicar();

        new AlmacenLector(_carpeta).Guardar(ConfiguracionLector.PorDefecto);
        lector.Aplicar();
        Assert.Equal(EstadoLector.Apagado, lector.Estado);

        lector.Dispose();
    }

    [Fact(DisplayName = "lector-identificacion · El puerto que falta o se cae: un puesto sin acceso a puertos serie lo dice, sin tirar la pantalla; un reintento después de cerrar no abre nada")]
    public void Sin_soporte_y_despues_de_cerrar()
    {
        var real = new PuertoSimulado { Falla = "UNSUPPORTED" };
        new AlmacenLector(_carpeta).Guardar(ConfiguracionLector.PorDefecto with { Medio = MedioIdentificacion.PuertoSerie, Puerto = "COM3" });
        var reloj = new RelojConTemporizadores(_reloj);
        var lector = new LectorIdentificacion(real, new AlmacenLector(_carpeta), _servicio.RegistroConfiguracion!, reloj);

        lector.Aplicar();
        Assert.Equal(EstadoLector.SinPuerto, lector.Estado);

        real.Falla = null;
        lector.Dispose();
        foreach (var temporizador in reloj.Temporizadores.ToList())
        {
            temporizador.Disparar();
        }

        Assert.False(real.Abierto);
    }

    [Fact(DisplayName = "lector-identificacion · El armado de las tramas: el simulador manda los bytes que mandaría un lector, así una ñ se descarta como un carácter que no es de un código")]
    public void El_simulador_manda_bytes_crudos()
    {
        new AlmacenLector(_carpeta).Guardar(ConfiguracionLector.PorDefecto with { Medio = MedioIdentificacion.Simulador });
        using var lector = Lector(real: null);
        var lecturas = new List<string>();
        lector.Lectura += (codigo, _) => lecturas.Add(codigo);
        lector.Aplicar();

        lector.Simulado.Pasar("ñ1", lector.Configuracion);

        Assert.Empty(lecturas);
        Assert.Equal("INVALID_CHARACTERS", lector.Ultima!.Motivo);
    }

    [Fact(DisplayName = "lector-identificacion · El armado de las tramas: con el terminador CR y un lector que manda CR LF, el LF que sobra no ensucia la lectura siguiente")]
    public void Un_terminador_que_no_coincide()
    {
        var armador = new ArmadorTramas(TerminadorLector.Cr, 0);

        var lecturas = armador.Agregar(Bytes("0001\r\n0002\r\n"), Ahora);

        Assert.Equal(["0001", "0002"], lecturas.Select(l => l.Codigo));
    }

    [Fact(DisplayName = "lector-identificacion · Fuera de la identificación la lectura se descarta: con el error de almacenamiento, una lectura se descarta como STORAGE_ERROR sin poner ningún aviso")]
    public void Con_el_error_de_almacenamiento()
    {
        var carpeta = Directory.CreateDirectory(Path.Combine(_carpeta, "rota")).FullName;
        var ruta = Path.Combine(carpeta, "incomedor.db");
        using (var conexion = new SqliteConnection($"Data Source={ruta};Pooling=False"))
        {
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "PRAGMA user_version = 99; CREATE TABLE futura (id INTEGER)";
            comando.ExecuteNonQuery();
        }

        using var servicio = new ServicioAtencion(ruta, _reloj, new ImpresoraDePrueba());
        var maquina = new MaquinaAtencion(servicio);
        maquina.Iniciar();

        Assert.Equal(EstadoAtencion.ErrorAlmacenamiento, maquina.Estado);
        Assert.Equal(new ResultadoLectura("DISCARDED", "STORAGE_ERROR"), maquina.IdentificarPorLector("0001", "SERIAL_PORT"));
        Assert.Null(maquina.Aviso);
    }

    private LectorIdentificacion Lector(IPuertoSerie? real) =>
        new(real, new AlmacenLector(_carpeta), _servicio.RegistroConfiguracion!, _reloj);

    private static byte[] Bytes(string texto) => Encoding.Latin1.GetBytes(texto);

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

    private sealed class RelojConTemporizadores(TimeProvider reloj) : TimeProvider
    {
        public List<TemporizadorDePrueba> Temporizadores { get; } = [];

        public override DateTimeOffset GetUtcNow() => reloj.GetUtcNow();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var temporizador = new TemporizadorDePrueba(() => callback(state)) { Primero = dueTime };
            Temporizadores.Add(temporizador);
            return temporizador;
        }
    }

    private sealed class TemporizadorDePrueba(Action disparar) : ITimer
    {
        public TimeSpan Primero { get; init; }

        public void Disparar() => disparar();

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
