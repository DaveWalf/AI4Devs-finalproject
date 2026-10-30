using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using Microsoft.Win32;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// La preparación de cada prueba E2E (D5 y D6 de demostrar-flujo-de-punta-a-punta). Antes de lanzar nada comprueba el
/// WebView2 Runtime, SQL Server y la sesión interactiva, y si falta algo falla nombrándolo, nunca con un salto. Aísla la
/// prueba en una carpeta, dos puertos y una base propios; lanza el puesto y el receptor compilados, y al final, siempre,
/// los cierra por su PID con su árbol, borra la base y la carpeta y compara la foto de %LOCALAPPDATA%\INComedor\demo\,
/// que sólo se lee.
/// </summary>
public sealed class EscenarioE2E : IAsyncDisposable
{
    public static readonly string Instancia =
        Environment.GetEnvironmentVariable("INCOMEDOR_SQLSERVER") is { Length: > 0 } instancia ? instancia : @"(localdb)\MSSQLLocalDB";

    private const string ClaveWebView2 = @"Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
    private static readonly TimeSpan PlazoArranque = TimeSpan.FromSeconds(60);
    private static readonly Lock Registros = new();

    private readonly string _hex = Guid.NewGuid().ToString("N")[..8];
    private readonly string _fotoAntes;
    private readonly List<Process> _lanzados = [];
    private int _receptores;

    private EscenarioE2E(string prueba)
    {
        Carpeta = Path.Combine(Path.GetTempPath(), "incomedor-e2e", _hex);
        Base = $"INComedorE2E_{_hex}";
        Resultados = Path.Combine(AppContext.BaseDirectory, "TestResults", "e2e", $"{DateTime.Now:yyyyMMdd-HHmmss}-{prueba}-{_hex}");
        Directory.CreateDirectory(Resultados);
        TomarPuertos();
        _fotoAntes = FotoDeLaDemostracion();
        Registrar($"escenario {_hex}: carpeta {Carpeta} · base {Base} · instancia {Instancia} · CDP {PuertoCdp} · receptor {PuertoReceptor}");
    }

    /// <summary>La carpeta de la demostración de esta prueba (INCOMEDOR_DEMO_DIR); no existe hasta que el puesto o el receptor la crean.</summary>
    public string Carpeta { get; }

    public string Base { get; }

    /// <summary>Las capturas y el registro de la prueba, en la carpeta de resultados junto al ensamblado.</summary>
    public string Resultados { get; }

    public int PuertoCdp { get; private set; }

    public int PuertoReceptor { get; private set; }

    public Uri DireccionReceptor => new($"https://localhost:{PuertoReceptor}/");

    public string CadenaConexionReceptor => CadenaConexion(Base);

    /// <summary>Comprueba el entorno y prepara una prueba; si falta algo, falla antes de lanzar ningún proceso.</summary>
    public static EscenarioE2E Preparar(string prueba)
    {
        ComprobarRequisitos();
        return new EscenarioE2E(prueba);
    }

    /// <summary>
    /// El puesto con su carpeta y su receptor; <paramref name="puertoCdp"/> es el valor crudo de INCOMEDOR_CDP_PORT, o
    /// null para no definirla. Las variables del usuario que tocan el puesto o su WebView2 no pasan.
    /// </summary>
    public Process LanzarPuesto(string? puertoCdp)
    {
        var exe = ExeDelProyecto("Intelektron.INComedor.Kiosk", "net11.0-windows10.0.17763.0");
        var inicio = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! };
        foreach (var heredada in inicio.Environment.Keys.Where(EsDelPuesto).ToList())
        {
            inicio.Environment.Remove(heredada);
        }

        inicio.Environment["INCOMEDOR_DEMO_DIR"] = Carpeta;
        inicio.Environment["INCOMEDOR_RECEPTOR_URL"] = DireccionReceptor.ToString();
        if (puertoCdp is not null)
        {
            inicio.Environment["INCOMEDOR_CDP_PORT"] = puertoCdp;
        }

        var puesto = Lanzar(inicio, "puesto");
        Registrar($"puesto PID {puesto.Id} · {exe} · INCOMEDOR_CDP_PORT={puertoCdp ?? "(sin definir)"}");
        return puesto;
    }

    /// <summary>El receptor de demostración con la carpeta de certificados, la base y el puerto de esta prueba.</summary>
    public Process LanzarReceptor()
    {
        var exe = ExeDelProyecto("Intelektron.INComedor.Sync.Api", "net11.0");
        var inicio = new ProcessStartInfo(exe, "--demostracion")
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        inicio.Environment["Receptor__CarpetaDemostracion"] = Path.Combine(Carpeta, "certificados");
        inicio.Environment["Receptor__BaseDemostracion"] = Base;
        inicio.Environment["Receptor__PuertoDemostracion"] = PuertoReceptor.ToString();
        inicio.Environment["INCOMEDOR_SQLSERVER"] = Instancia;

        var registro = Path.Combine(Resultados, $"receptor-{++_receptores}.log");
        var receptor = Lanzar(inicio, "receptor");
        receptor.OutputDataReceived += (_, e) => Anotar(registro, e.Data);
        receptor.ErrorDataReceived += (_, e) => Anotar(registro, e.Data);
        receptor.BeginOutputReadLine();
        receptor.BeginErrorReadLine();
        Registrar($"receptor PID {receptor.Id} · {exe} · {DireccionReceptor}");
        return receptor;
    }

    /// <summary>Espera a que el receptor acepte conexiones en su puerto; si el proceso termina antes, falla con su registro.</summary>
    public async Task EsperarReceptorAsync(Process receptor)
    {
        var reloj = Stopwatch.StartNew();
        while (reloj.Elapsed < PlazoArranque)
        {
            if (receptor.HasExited)
            {
                throw new InvalidOperationException($"El receptor terminó con {receptor.ExitCode} antes de escuchar; ver {Resultados}.");
            }

            using var cliente = new TcpClient();
            try
            {
                await cliente.ConnectAsync(IPAddress.Loopback, PuertoReceptor);
                Registrar($"receptor escuchando a los {reloj.ElapsedMilliseconds} ms");
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(250);
            }
        }

        throw new TimeoutException($"El receptor no aceptó conexiones en el puerto {PuertoReceptor} en {PlazoArranque.TotalSeconds} s.");
    }

    /// <summary>Se conecta por CDP al WebView2 del puesto y devuelve la página que muestra el campo del código.</summary>
    public async Task<IPage> ConectarAsync(IPlaywright playwright)
    {
        var reloj = Stopwatch.StartNew();
        IBrowser? navegador = null;
        Exception? ultimo = null;
        while (navegador is null && reloj.Elapsed < PlazoArranque)
        {
            try
            {
                navegador = await playwright.Chromium.ConnectOverCDPAsync($"http://127.0.0.1:{PuertoCdp}");
            }
            catch (PlaywrightException e)
            {
                ultimo = e;
                await Task.Delay(500);
            }
        }

        if (navegador is null)
        {
            throw new TimeoutException($"CDP no respondió en 127.0.0.1:{PuertoCdp} en {PlazoArranque.TotalSeconds} s.", ultimo);
        }

        Registrar($"conectado por CDP a los {reloj.ElapsedMilliseconds} ms · WebView2 {navegador.Version}");
        while (reloj.Elapsed < PlazoArranque)
        {
            foreach (var pagina in navegador.Contexts.SelectMany(c => c.Pages))
            {
                if (await pagina.Locator("#codigo").CountAsync() == 1)
                {
                    Registrar($"pantalla de reposo en {pagina.Url} a los {reloj.ElapsedMilliseconds} ms");
                    return pagina;
                }
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Ninguna página del WebView2 mostró #codigo en {PlazoArranque.TotalSeconds} s.");
    }

    /// <summary>
    /// Sin CDP, la ventana abrió en reposo cuando el puesto sigue vivo, tiene su ventana principal, su WebView2 arrancó
    /// y la pantalla de atención preparó su base en la carpeta de la prueba (MaquinaAtencion.Iniciar).
    /// </summary>
    public async Task EsperarReposoAsync(Process puesto)
    {
        var reloj = Stopwatch.StartNew();
        var rutaBase = Path.Combine(Carpeta, "incomedor.db");
        while (reloj.Elapsed < PlazoArranque)
        {
            if (puesto.HasExited)
            {
                throw new InvalidOperationException($"El puesto terminó con {puesto.ExitCode} antes de abrir su ventana.");
            }

            puesto.Refresh();
            var webView2 = ProcesosYPuertos.Arbol(puesto.Id).Values.Count(n => n.Equals("msedgewebview2.exe", StringComparison.OrdinalIgnoreCase));
            if (puesto.MainWindowHandle != IntPtr.Zero && webView2 > 0 && File.Exists(rutaBase))
            {
                Registrar($"ventana «{puesto.MainWindowTitle}» en reposo a los {reloj.ElapsedMilliseconds} ms · {webView2} procesos de WebView2 · base preparada");
                return;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"El puesto no abrió su ventana en reposo en {PlazoArranque.TotalSeconds} s.");
    }

    /// <summary>Las escuchas TCP del árbol del puesto, observadas durante <paramref name="ventana"/>.</summary>
    public async Task<IReadOnlyList<Escucha>> EscuchasDelArbolAsync(Process puesto, TimeSpan ventana)
    {
        var vistas = new HashSet<Escucha>();
        var reloj = Stopwatch.StartNew();
        do
        {
            var arbol = ProcesosYPuertos.Arbol(puesto.Id);
            vistas.UnionWith(ProcesosYPuertos.EnEscucha().Where(e => arbol.ContainsKey(e.Pid)));
            await Task.Delay(250);
        }
        while (reloj.Elapsed < ventana);

        Registrar($"escuchas del árbol del puesto en {ventana.TotalSeconds} s: {(vistas.Count == 0 ? "ninguna" : string.Join(", ", vistas.Select(e => $"{e.Direccion}:{e.Puerto} (PID {e.Pid})")))}");
        return [.. vistas];
    }

    /// <summary>Cierra el proceso por su PID, con su árbol, y espera a que no quede ninguno vivo.</summary>
    public async Task DetenerAsync(Process proceso)
    {
        var arbol = ProcesosYPuertos.Arbol(proceso.Id);
        try
        {
            proceso.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Ya había terminado.
        }

        using (var plazo = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            try
            {
                await proceso.WaitForExitAsync(plazo.Token);
            }
            catch (OperationCanceledException)
            {
                // Lo dice el conteo de vivos de abajo.
            }
        }

        var reloj = Stopwatch.StartNew();
        while (arbol.Any(p => ProcesosYPuertos.Vivo(p.Key, p.Value)) && reloj.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(250);
        }

        var vivos = arbol.Where(p => ProcesosYPuertos.Vivo(p.Key, p.Value)).ToList();
        Registrar($"PID {proceso.Id} cerrado con su árbol: {arbol.Count} procesos, vivos después: {vivos.Count}");
        _lanzados.Remove(proceso);
        proceso.Dispose();
        if (vivos.Count > 0)
        {
            throw new InvalidOperationException($"Quedaron vivos: {string.Join(", ", vivos.Select(v => $"{v.Value} PID {v.Key}"))}.");
        }
    }

    public async Task CapturarAsync(IPage pagina, string nombre)
    {
        var ruta = Path.Combine(Resultados, $"{nombre}.png");
        await pagina.ScreenshotAsync(new() { Path = ruta });
        Registrar($"captura {Path.GetFileName(ruta)}");
    }

    public void Registrar(string linea)
    {
        Anotar(Path.Combine(Resultados, "registro.txt"), linea);
        TestContext.Current.TestOutputHelper?.WriteLine($"{DateTimeOffset.Now:HH:mm:ss.fff} {linea}");
    }

    /// <summary>
    /// Toma otros dos puertos, para el único reintento cuando uno que estaba libre lo tomó otro proceso entre la reserva y
    /// el arranque (Risks del design).
    /// </summary>
    public void TomarPuertos()
    {
        PuertoCdp = PuertoLibre();
        do
        {
            PuertoReceptor = PuertoLibre();
        }
        while (PuertoReceptor == PuertoCdp);
    }

    /// <summary>Siempre: los procesos por su PID con su árbol, la base, la carpeta y, al final, la foto.</summary>
    public async ValueTask DisposeAsync()
    {
        var errores = new List<string>();
        foreach (var proceso in Enumerable.Reverse(_lanzados).ToList())
        {
            try
            {
                await DetenerAsync(proceso);
            }
            catch (Exception e)
            {
                errores.Add(e.Message);
            }
        }

        try
        {
            await BorrarBaseAsync();
            await BorrarCarpetaAsync();
        }
        catch (Exception e)
        {
            errores.Add(e.Message);
        }

        var fotoDespues = FotoDeLaDemostracion();
        Registrar($"foto de %LOCALAPPDATA%\\INComedor\\demo\\: {(fotoDespues == _fotoAntes ? "igual" : "DISTINTA")} ({Primera(_fotoAntes)})");
        if (fotoDespues != _fotoAntes)
        {
            errores.Add($"La carpeta de la demostración del usuario cambió.\nAntes:\n{_fotoAntes}\nDespués:\n{fotoDespues}");
        }

        if (errores.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", errores));
        }
    }

    /// <summary>
    /// Las tres condiciones del entorno (D6). Si falta alguna, la prueba falla con un mensaje que nombra cada una y lo que
    /// la remedia en la CI, cuyo paso en ci.yml es de David.
    /// </summary>
    private static void ComprobarRequisitos()
    {
        var faltan = new List<string>();
        if (VersionWebView2() is null)
        {
            faltan.Add($"el WebView2 Runtime: ninguna clave {ClaveWebView2} tiene un valor «pv» (en la CI, instalarlo en ci.yml)");
        }

        try
        {
            using var conexion = new SqlConnection(CadenaConexion("master"));
            conexion.Open();
        }
        catch (SqlException e)
        {
            faltan.Add($"SQL Server: no hay conexión con «{Instancia}» (INCOMEDOR_SQLSERVER, o LocalDB por defecto; en la CI, «sqllocaldb start MSSQLLocalDB» en ci.yml): {e.Message}");
        }

        if (!Environment.UserInteractive)
        {
            faltan.Add("una sesión con escritorio interactivo: el puesto abre una ventana WPF (en la CI, correr el E2E en una sesión interactiva o filtrarlo en ci.yml)");
        }

        if (faltan.Count > 0)
        {
            throw new InvalidOperationException("El E2E no puede correr en este entorno. Falta:\n- " + string.Join("\n- ", faltan));
        }
    }

    private static string? VersionWebView2()
    {
        foreach (var (raiz, ruta) in new[]
        {
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\" + ClaveWebView2),
            (Registry.LocalMachine, @"SOFTWARE\" + ClaveWebView2),
            (Registry.CurrentUser, @"Software\" + ClaveWebView2),
        })
        {
            using var clave = raiz.OpenSubKey(ruta);
            if (clave?.GetValue("pv") is string version && version.Length > 0 && version != "0.0.0.0")
            {
                return version;
            }
        }

        return null;
    }

    private static string CadenaConexion(string baseDatos) =>
        new SqlConnectionStringBuilder
        {
            DataSource = Instancia,
            InitialCatalog = baseDatos,
            IntegratedSecurity = true,
            Encrypt = SqlConnectionEncryptOption.Optional,
            ConnectTimeout = 15,
        }.ConnectionString;

    private static string ExeDelProyecto(string proyecto, string marco)
    {
        var configuracion = typeof(EscenarioE2E).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "Configuracion").Value!;
        var exe = Path.Combine(RaizDelRepo(), "src", proyecto, "bin", configuracion, marco, proyecto + ".exe");
        return File.Exists(exe) ? exe : throw new FileNotFoundException($"No está compilado {exe}.", exe);
    }

    private static string RaizDelRepo()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "INComedor.sln")))
        {
            carpeta = carpeta.Parent;
        }

        return carpeta?.FullName ?? throw new DirectoryNotFoundException("No se encontró INComedor.sln arriba de " + AppContext.BaseDirectory);
    }

    /// <summary>Las variables que cambian al puesto o a su WebView2 salen del entorno heredado: la prueba fija las suyas.</summary>
    private static bool EsDelPuesto(string variable) =>
        variable.StartsWith("INCOMEDOR_", StringComparison.OrdinalIgnoreCase) || variable.StartsWith("WEBVIEW2_", StringComparison.OrdinalIgnoreCase);

    private static int PuertoLibre()
    {
        var escucha = new TcpListener(IPAddress.Loopback, 0);
        escucha.Start();
        var puerto = ((IPEndPoint)escucha.LocalEndpoint).Port;
        escucha.Stop();
        return puerto;
    }

    /// <summary>Los archivos de %LOCALAPPDATA%\INComedor\demo\ con su tamaño y su fecha, o que no existe. Sólo lee.</summary>
    private static string FotoDeLaDemostracion()
    {
        var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "INComedor", "demo");
        if (!Directory.Exists(carpeta))
        {
            return "no existe";
        }

        var entradas = new DirectoryInfo(carpeta).EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
            .Select(e => $"{Path.GetRelativePath(carpeta, e.FullName)} · {(e is FileInfo f ? f.Length : -1)} · {e.LastWriteTimeUtc:O}")
            .Order(StringComparer.Ordinal);
        return "existe\n" + string.Join("\n", entradas);
    }

    private static string Primera(string foto) => foto.Split('\n')[0];

    private static void Anotar(string archivo, string? linea)
    {
        if (linea is null)
        {
            return;
        }

        lock (Registros)
        {
            File.AppendAllText(archivo, $"{DateTimeOffset.Now:HH:mm:ss.fff} {linea}{Environment.NewLine}");
        }
    }

    private Process Lanzar(ProcessStartInfo inicio, string que)
    {
        var proceso = Process.Start(inicio) ?? throw new InvalidOperationException($"No arrancó el {que}.");
        _lanzados.Add(proceso);
        return proceso;
    }

    private async Task BorrarBaseAsync()
    {
        SqlConnection.ClearAllPools();
        await using var conexion = new SqlConnection(CadenaConexion("master"));
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            IF DB_ID(@base) IS NULL
                SELECT 0;
            ELSE
            BEGIN
                DECLARE @borrar nvarchar(400) = N'ALTER DATABASE ' + QUOTENAME(@base) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@base);
                EXEC sp_executesql @borrar;
                SELECT 1;
            END
            """;
        comando.Parameters.AddWithValue("@base", Base);
        var existia = (int)(await comando.ExecuteScalarAsync())! == 1;
        Registrar(existia ? $"base {Base} borrada" : $"base {Base}: no se creó");
    }

    /// <summary>Los procesos del WebView2 pueden tardar un momento en soltar sus archivos después de cerrarse.</summary>
    private async Task BorrarCarpetaAsync()
    {
        var reloj = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                if (Directory.Exists(Carpeta))
                {
                    Directory.Delete(Carpeta, recursive: true);
                }

                Registrar($"carpeta {Carpeta} borrada");
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && reloj.Elapsed < TimeSpan.FromSeconds(15))
            {
                await Task.Delay(250);
            }
        }
    }
}
