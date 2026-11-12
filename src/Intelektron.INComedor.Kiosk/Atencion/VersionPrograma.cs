using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Intelektron.INComedor.Kiosk.Atencion;

/// <summary>
/// La versión del programa (D2 de mostrar-version-del-programa), leída una vez del ensamblado del Kiosk y validada al leer:
/// la versión del paquete (el tag que pasa <c>publicar-puesto.ps1</c>) si tiene la forma que el script valida, o
/// «desarrollo»; el commit que el SDK agrega a la versión informativa después del «+», si son de 7 a 40 caracteres
/// hexadecimales, o «sin commit»; la fecha de compilación guardada, si es una fecha ISO, o la de escritura del archivo; y la
/// versión de .NET. Ningún dato de la carpeta, el equipo ni el usuario.
/// </summary>
public sealed partial class VersionPrograma
{
    public const string Desarrollo = "desarrollo";
    public const string SinCommit = "sin commit";

    public VersionPrograma(string? versionPuesto, string? versionInformativa, string? fechaCompilacion, DateTimeOffset fechaArchivo, string net)
    {
        Version = versionPuesto is not null && FormaDelTag().IsMatch(versionPuesto) ? versionPuesto : Desarrollo;
        var mas = versionInformativa?.IndexOf('+', StringComparison.Ordinal) ?? -1;
        var commit = mas >= 0 ? versionInformativa![(mas + 1)..] : string.Empty;
        Commit = FormaDelCommit().IsMatch(commit) ? commit : SinCommit;
        Compilado = DateTimeOffset.TryParse(fechaCompilacion, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var fecha)
            && fechaCompilacion!.Contains('T', StringComparison.Ordinal)
            ? fecha
            : fechaArchivo;
        Net = net;
    }

    /// <summary>El tag del paquete, o «desarrollo».</summary>
    public string Version { get; }

    /// <summary>El commit completo, o «sin commit».</summary>
    public string Commit { get; }

    public DateTimeOffset Compilado { get; }

    public string Net { get; }

    /// <summary>Los primeros siete caracteres del commit, o «sin commit».</summary>
    public string CommitCorto => Commit == SinCommit ? SinCommit : Commit[..7];

    /// <summary>La línea de la cabecera, debajo del nombre del servicio: «v1.0.1 · 7b80d45».</summary>
    public string Cabecera => $"{Version} · {CommitCorto}";

    /// <summary>La fecha y hora de compilación en la zona del puesto, «11/11/2026 18:30».</summary>
    public string CompiladoEn(TimeZoneInfo zona) =>
        TimeZoneInfo.ConvertTime(Compilado, zona).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>La versión del Kiosk que corre, de los metadatos de su ensamblado (D1).</summary>
    public static VersionPrograma DelPuesto()
    {
        var ensamblado = typeof(VersionPrograma).Assembly;
        string? Metadato(string clave) => ensamblado.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == clave)?.Value;
        DateTimeOffset archivo;
        try
        {
            archivo = ensamblado.Location.Length > 0 ? File.GetLastWriteTimeUtc(ensamblado.Location) : DateTimeOffset.UtcNow;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            archivo = DateTimeOffset.UtcNow;
        }

        return new VersionPrograma(
            Metadato("VersionPuesto"),
            ensamblado.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            Metadato("FechaCompilacionPuesto"),
            archivo,
            RuntimeInformation.FrameworkDescription);
    }

    /// <summary>La misma forma que valida <c>publicar-puesto.ps1</c> antes de usar el tag.</summary>
    [GeneratedRegex(@"^[0-9A-Za-z][0-9A-Za-z.\-]{0,63}\z")]
    private static partial Regex FormaDelTag();

    [GeneratedRegex(@"^[0-9a-f]{7,40}\z")]
    private static partial Regex FormaDelCommit();
}
