using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La exportación automática en el puesto (exportacion-consumos de exportar-consumos-automaticamente, D2 a D5): sobre un
/// servicio real con la base de la demostración y consumos confirmados por la máquina de atención, con 0001 a las 12:00
/// del 06/11/2026; un día después, para otra fecha de negocio.
/// </summary>
public sealed class ExportacionAutomaticaTests : IDisposable
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    private readonly string _carpeta = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "incomedor-tests", "exportacion-" + Guid.NewGuid().ToString("N"))).FullName;

    private readonly RelojManual _reloj = new("2026-11-06T15:00:00Z");
    private readonly ServicioAtencion _servicio;
    private readonly MaquinaAtencion _maquina;

    public ExportacionAutomaticaTests()
    {
        _servicio = new ServicioAtencion(Path.Combine(_carpeta, "incomedor.db"), _reloj, new ImpresoraDePrueba());
        _maquina = new MaquinaAtencion(_servicio);
        _maquina.Iniciar();
    }

    private string Destino => Directory.CreateDirectory(Path.Combine(_carpeta, "destino")).FullName;

    public void Dispose()
    {
        _servicio.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_carpeta, recursive: true);
    }

    [Fact(DisplayName = "exportacion-consumos · Datos fuera de forma: 2 minutos, una carpeta relativa, un servidor con espacios, el puerto 0, sin usuario y una carpeta remota con .. se señalan; lo guardado se lee con otro almacén y un archivo roto da lo de fábrica")]
    public void La_configuracion_y_su_validacion()
    {
        Assert.Empty(ConfiguracionExportacion.Validar(ConfiguracionExportacion.PorDefecto));
        var activa = ConfiguracionExportacion.PorDefecto with { Activa = true, Carpeta = Destino };
        Assert.Empty(ConfiguracionExportacion.Validar(activa));
        Assert.Equal(["Minutos"], ConfiguracionExportacion.Validar(activa with { Minutos = 2 }));
        Assert.Equal(["Carpeta"], ConfiguracionExportacion.Validar(activa with { Carpeta = "relativa" }));
        var sftp = activa with { SftpActivo = true, Servidor = "sftp.ejemplo.local", Usuario = "comedor", CarpetaRemota = "entrada/comedor" };
        Assert.Empty(ConfiguracionExportacion.Validar(sftp));
        Assert.Equal(["Servidor"], ConfiguracionExportacion.Validar(sftp with { Servidor = "un servidor" }));
        Assert.Equal(["Puerto"], ConfiguracionExportacion.Validar(sftp with { Puerto = 0 }));
        Assert.Equal(["Usuario"], ConfiguracionExportacion.Validar(sftp with { Usuario = string.Empty }));
        Assert.Equal(["CarpetaRemota"], ConfiguracionExportacion.Validar(sftp with { CarpetaRemota = "entrada/../otra" }));
        Assert.Equal(["Carpeta"], ConfiguracionExportacion.Validar(ConfiguracionExportacion.PorDefecto with { Activa = true }));

        new AlmacenConfiguracionExportacion(_carpeta).Guardar(sftp);
        Assert.Equal(sftp, new AlmacenConfiguracionExportacion(_carpeta).Leer());
        Assert.Throws<ArgumentException>(() => new AlmacenConfiguracionExportacion(_carpeta).Guardar(sftp with { Minutos = 2 }));
        File.WriteAllText(Path.Combine(_carpeta, "exportacion-automatica.json"), "{ roto");
        Assert.Equal(ConfiguracionExportacion.PorDefecto, new AlmacenConfiguracionExportacion(_carpeta).Leer());
    }

    [Fact(DisplayName = "exportacion-consumos · Un archivo que crece: dos consumos, una pasada, un consumo más y otra pasada dejan INComedor-consumos.csv con el encabezado una vez y los tres en orden, sin el código, y dos CONSUMPTIONS_AUTO_EXPORTED")]
    public async Task Un_archivo_que_crece()
    {
        using var exportacion = Exportacion(ConfiguracionExportacion.PorDefecto with { Activa = true, Carpeta = Destino });
        await Consumir("Almuerzo común");
        await Consumir("Café con leche");

        var primera = Assert.Single(exportacion.Exportar());
        _reloj.Avanzar(TimeSpan.FromDays(1));
        await Consumir("Almuerzo común");
        var segunda = Assert.Single(exportacion.Exportar());

        Assert.Equal((2, 1), (primera.Filas, segunda.Filas));
        Assert.Equal(["INComedor-consumos.csv"], primera.Archivos);
        var lineas = File.ReadAllLines(Path.Combine(Destino, "INComedor-consumos.csv"));
        Assert.Equal(4, lineas.Length);
        Assert.Single(lineas, l => l.Contains("\"Fecha y hora\"", StringComparison.Ordinal));
        Assert.Contains("Almuerzo común", lineas[1], StringComparison.Ordinal);
        Assert.Contains("Café con leche", lineas[2], StringComparison.Ordinal);
        Assert.Contains("2026-11-07", lineas[3], StringComparison.Ordinal);
        Assert.DoesNotContain(lineas, l => l.Contains("\"0001\"", StringComparison.Ordinal));
        Assert.Equal(2, Registros("CONSUMPTIONS_AUTO_EXPORTED").Count);
    }

    [Fact(DisplayName = "exportacion-consumos · Un archivo por día: consumos de dos fechas de negocio quedan en un archivo por fecha, cada uno con los de su día")]
    public async Task Un_archivo_por_dia()
    {
        using var exportacion = Exportacion(ConfiguracionExportacion.PorDefecto with { Activa = true, Carpeta = Destino, Archivo = ArchivoExportacion.PorDia });
        await Consumir("Almuerzo común");
        _reloj.Avanzar(TimeSpan.FromDays(1));
        await Consumir("Almuerzo común");

        var resultado = Assert.Single(exportacion.Exportar());

        Assert.Equal(["INComedor-consumos-20261106.csv", "INComedor-consumos-20261107.csv"], resultado.Archivos);
        Assert.Equal(2, File.ReadAllLines(Path.Combine(Destino, "INComedor-consumos-20261106.csv")).Length);
        Assert.Contains("2026-11-07", File.ReadAllLines(Path.Combine(Destino, "INComedor-consumos-20261107.csv"))[1], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "exportacion-consumos · Sin consumos nuevos: otra pasada no cambia el archivo ni registra nada, y otro arranque conserva la marca")]
    public async Task Sin_consumos_nuevos()
    {
        var configuracion = ConfiguracionExportacion.PorDefecto with { Activa = true, Carpeta = Destino };
        using (var exportacion = Exportacion(configuracion))
        {
            await Consumir("Almuerzo común");
            exportacion.Exportar();
        }

        var archivo = Path.Combine(Destino, "INComedor-consumos.csv");
        var antes = File.ReadAllBytes(archivo);
        using var otroArranque = Exportacion(configuracion);

        var resultado = Assert.Single(otroArranque.Exportar());

        Assert.Equal(0, resultado.Filas);
        Assert.Equal(antes, File.ReadAllBytes(archivo));
        Assert.Single(Registros("CONSUMPTIONS_AUTO_EXPORTED"));
        Assert.True(otroArranque.Estado[ExportacionAutomatica.Carpeta].Marca > 0);
    }

    [Fact(DisplayName = "exportacion-consumos · El servidor cae y vuelve: con el SFTP simulado caído la carpeta recibe los consumos y el SFTP conserva su marca; al volver recibe los mismos, una sola vez")]
    public async Task El_servidor_cae_y_vuelve()
    {
        using var exportacion = Exportacion(ConfiguracionExportacion.PorDefecto with
        {
            Activa = true, Carpeta = Destino, SftpActivo = true, Servidor = "sftp.ejemplo.local", Usuario = "comedor", CarpetaRemota = "entrada/comedor",
        });
        exportacion.SftpCaido = true;
        await Consumir("Almuerzo común");
        await Consumir("Café con leche");

        var primera = exportacion.Exportar();
        var remoto = Path.Combine(_carpeta, "sftp-simulado", "sftp.ejemplo.local", "entrada", "comedor", "INComedor-consumos.csv");
        Assert.Equal(2, primera.Single(r => r.Destino == ExportacionAutomatica.Carpeta).Filas);
        Assert.Equal("SERVER_UNREACHABLE", primera.Single(r => r.Destino == ExportacionAutomatica.Sftp).Error);
        Assert.False(File.Exists(remoto));
        Assert.Equal(0, exportacion.Estado[ExportacionAutomatica.Sftp].Marca);
        Assert.Equal("SERVER_UNREACHABLE", exportacion.Estado[ExportacionAutomatica.Sftp].Error);

        exportacion.SftpCaido = false;
        var segunda = exportacion.Exportar();

        Assert.Equal(0, segunda.Single(r => r.Destino == ExportacionAutomatica.Carpeta).Filas);
        Assert.Equal(2, segunda.Single(r => r.Destino == ExportacionAutomatica.Sftp).Filas);
        Assert.Equal(File.ReadAllLines(Path.Combine(Destino, "INComedor-consumos.csv")), File.ReadAllLines(remoto));
        Assert.Null(exportacion.Estado[ExportacionAutomatica.Sftp].Error);
        var falla = Assert.Single(Registros("CONSUMPTIONS_AUTO_EXPORT_FAILED"));
        Assert.Contains("SERVER_UNREACHABLE", falla, StringComparison.Ordinal);
        Assert.All(Registros("CONSUMPTIONS_AUTO_EXPORTED").Append(falla), r =>
        {
            Assert.DoesNotContain("Persona sintética", r, StringComparison.Ordinal);
            Assert.DoesNotContain("0001", r, StringComparison.Ordinal);
        });
    }

    [Fact(DisplayName = "exportacion-consumos · La exportación automática de consumos: con la exportación activa, el reloj corre una pasada cada 15 minutos; apagada, no queda ningún reloj")]
    public async Task El_reloj_corre_las_pasadas()
    {
        var reloj = new RelojConTemporizadores(_reloj);
        var almacen = new AlmacenConfiguracionExportacion(_carpeta);
        almacen.Guardar(ConfiguracionExportacion.PorDefecto with { Activa = true, Carpeta = Destino });
        using var exportacion = new ExportacionAutomatica(_servicio.Consumos!, _servicio.RegistroConfiguracion!, reloj, Zona, _carpeta, almacen);
        await Consumir("Almuerzo común");

        exportacion.Programar();

        var temporizador = Assert.Single(reloj.Temporizadores);
        Assert.Equal(TimeSpan.FromMinutes(15), temporizador.Periodo);
        temporizador.Disparar();
        Assert.True(File.Exists(Path.Combine(Destino, "INComedor-consumos.csv")));

        almacen.Guardar(ConfiguracionExportacion.PorDefecto);
        exportacion.Programar();
        Assert.True(temporizador.Liberado);
        Assert.Single(reloj.Temporizadores);
    }

    private ExportacionAutomatica Exportacion(ConfiguracionExportacion configuracion)
    {
        var almacen = new AlmacenConfiguracionExportacion(_carpeta);
        almacen.Guardar(configuracion);
        return new ExportacionAutomatica(_servicio.Consumos!, _servicio.RegistroConfiguracion!, _reloj, Zona, _carpeta, almacen);
    }

    /// <summary>0001 confirma la opción con ese nombre, pasando de página si hace falta, y vuelve a reposo.</summary>
    private async Task Consumir(string opcion)
    {
        _maquina.Identificar("0001");
        while (_maquina.PaginaActual.All(o => o.Nombre != opcion))
        {
            _maquina.Siguiente();
        }

        _maquina.Elegir(_maquina.PaginaActual.Single(o => o.Nombre == opcion).Id);
        await _maquina.ConfirmarAsync();
        _maquina.Listo();
    }

    /// <summary>Los contextos de un tipo de registro de la bitácora de la base, como texto.</summary>
    private List<string> Registros(string tipo)
    {
        using var conexion = new SqliteConnection($"Data Source={Path.Combine(_carpeta, "incomedor.db")};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = $tipo ORDER BY combitSecuencia";
        comando.Parameters.AddWithValue("$tipo", tipo);
        using var lector = comando.ExecuteReader();
        var registros = new List<string>();
        while (lector.Read())
        {
            registros.Add(lector.GetString(0));
        }

        return registros;
    }

    /// <summary>Un reloj que anota los temporizadores que se le piden, para dispararlos a mano.</summary>
    private sealed class RelojConTemporizadores(TimeProvider reloj) : TimeProvider
    {
        public List<TemporizadorDePrueba> Temporizadores { get; } = [];

        public override DateTimeOffset GetUtcNow() => reloj.GetUtcNow();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var temporizador = new TemporizadorDePrueba(() => callback(state), period);
            Temporizadores.Add(temporizador);
            return temporizador;
        }
    }

    private sealed class TemporizadorDePrueba(Action disparar, TimeSpan periodo) : ITimer
    {
        public TimeSpan Periodo { get; } = periodo;

        public bool Liberado { get; private set; }

        public void Disparar() => disparar();

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() => Liberado = true;

        public ValueTask DisposeAsync()
        {
            Liberado = true;
            return ValueTask.CompletedTask;
        }
    }
}
