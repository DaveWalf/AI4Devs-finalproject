using System.Globalization;
using System.IO;

namespace Intelektron.INComedor.Kiosk;

/// <summary>
/// Dónde guarda la demostración sus datos, a qué receptor envía y si abre la depuración (D1 a D4 de
/// demostrar-flujo-de-punta-a-punta). Sale de tres variables de entorno, y sin ellas es lo de siempre:
/// %LOCALAPPDATA%\INComedor\demo\, https://localhost:5001/ y ningún puerto. Un valor inválido se ignora.
/// </summary>
public sealed record EntornoDemostracion(string Carpeta, Uri DireccionReceptor, int? PuertoCdp, string? CarpetaDatosWebView2)
{
    public const string VariableCarpeta = "INCOMEDOR_DEMO_DIR";
    public const string VariableReceptor = "INCOMEDOR_RECEPTOR_URL";
    public const string VariablePuertoCdp = "INCOMEDOR_CDP_PORT";

    private static readonly Uri ReceptorPorDefecto = new("https://localhost:5001/");

    public string RutaBase => Path.Combine(Carpeta, "incomedor.db");

    public string CarpetaComprobantes => Path.Combine(Carpeta, "comprobantes");

    public string CarpetaCertificados => Path.Combine(Carpeta, "certificados");

    /// <summary>Lee las variables con <paramref name="variable"/>; <paramref name="datosLocales"/> es %LOCALAPPDATA%.</summary>
    public static EntornoDemostracion Resolver(Func<string, string?> variable, string datosLocales)
    {
        // Sólo una ruta absoluta: «demo», «C:demo» o «\demo» dependen del directorio o de la unidad actuales (OWASP A05).
        var elegida = variable(VariableCarpeta) is { } ruta && Path.IsPathFullyQualified(ruta) ? Path.GetFullPath(ruta) : null;
        var carpeta = elegida ?? Path.Combine(datosLocales, "INComedor", "demo");

        // Sólo https: ningún valor hace que un lote viaje sin TLS (OWASP A02).
        var receptor = Uri.TryCreate(variable(VariableReceptor), UriKind.Absolute, out var direccion) && direccion.Scheme == Uri.UriSchemeHttps
            ? direccion
            : ReceptorPorDefecto;

        // Sólo dígitos, y fuera de los puertos de servicio del sistema (D2).
        int? puerto = int.TryParse(variable(VariablePuertoCdp), NumberStyles.None, CultureInfo.InvariantCulture, out var numero) && numero is >= 1024 and <= 65535
            ? numero
            : null;

        // Dos WebView2 con argumentos distintos no comparten carpeta de datos: la carpeta elegida lleva la suya (D4).
        return new EntornoDemostracion(carpeta, receptor, puerto, elegida is null ? null : Path.Combine(elegida, "webview2"));
    }
}
