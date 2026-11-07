namespace Intelektron.INComedor.Kiosk;

/// <summary>
/// Los argumentos del navegador del puesto (D15 de configurar-puesto-por-pestanas, decisión de David del 06/11): siempre el
/// que apaga el rebote elástico de Chromium y, en las pruebas, el puerto CDP de EntornoDemostracion, que sigue siendo sólo
/// ese puerto. ElasticOverscroll es la feature de Chromium del rebote elástico en Windows y Android
/// (ui/base/ui_base_features.cc); WebView2 une las de --disable-features con las suyas y quita de las habilitadas las que se
/// deshabilitan (CoreWebView2EnvironmentOptions.AdditionalBrowserArguments).
/// </summary>
public static class ArgumentosWebView2
{
    public const string SinReboteElastico = "--disable-features=ElasticOverscroll";

    public static string Componer(string? argumentosEntorno) =>
        argumentosEntorno is { Length: > 0 } argumentos ? $"{SinReboteElastico} {argumentos}" : SinReboteElastico;
}
