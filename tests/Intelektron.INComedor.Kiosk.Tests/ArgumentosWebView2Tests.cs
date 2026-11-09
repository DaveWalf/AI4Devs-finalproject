namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El argumento del navegador que apaga el rebote elástico (D15 de configurar-puesto-por-pestanas, decisión de David del
/// 06/11): se compone en MainWindow aparte de EntornoDemostracion.ArgumentosNavegador, que sigue siendo sólo el puerto CDP.
/// </summary>
public class ArgumentosWebView2Tests
{
    [Fact(DisplayName = "presentacion-puesto · Sin el puerto de las pruebas, el navegador arranca con --disable-features=ElasticOverscroll")]
    public void Sin_puerto()
    {
        Assert.Equal("--disable-features=ElasticOverscroll", ArgumentosWebView2.Componer(null));
    }

    [Fact(DisplayName = "presentacion-puesto · Con el puerto 9333, el argumento del rebote y --remote-debugging-port=9333")]
    public void Con_puerto()
    {
        var entorno = EntornoDemostracion.Resolver(nombre => nombre == "INCOMEDOR_CDP_PORT" ? "9333" : null, Path.GetTempPath());

        Assert.Equal("--remote-debugging-port=9333", entorno.ArgumentosNavegador);
        Assert.Equal("--disable-features=ElasticOverscroll --remote-debugging-port=9333", ArgumentosWebView2.Componer(entorno.ArgumentosNavegador));
    }
}
