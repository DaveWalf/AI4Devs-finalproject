using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Leer la versión del programa (D2 de mostrar-version-del-programa): el tag del paquete si tiene la forma del script, o
/// «desarrollo»; el commit de la versión informativa si son de 7 a 40 caracteres hexadecimales, o «sin commit»; la fecha de
/// compilación guardada si es ISO, o la del archivo; y la versión de .NET. Con valores sintéticos, sin depender del build.
/// </summary>
public class VersionProgramaTests
{
    private const string Informativa = "1.0.0+7b80d455e506008a66194ddcfdf8412a306d71eb";
    private static readonly DateTimeOffset FechaDelArchivo = DateTimeOffset.Parse("2026-11-10T12:00:00Z");
    private static readonly TimeZoneInfo BuenosAires = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    [Fact(DisplayName = "presentacion-puesto · Un paquete publicado: con v1.0.1 y el commit en la versión informativa, la cabecera dice «v1.0.1 · 7b80d45»")]
    public void Un_paquete_publicado()
    {
        var version = new VersionPrograma("v1.0.1", Informativa, "2026-11-11T21:30:00Z", FechaDelArchivo, ".NET 11.0.0");

        Assert.Equal("v1.0.1 · 7b80d45", version.Cabecera);
        Assert.Equal("v1.0.1", version.Version);
        Assert.Equal("7b80d455e506008a66194ddcfdf8412a306d71eb", version.Commit);
    }

    [Theory(DisplayName = "presentacion-puesto · Un build de desarrollo: sin versión de paquete, la cabecera dice «desarrollo · 7b80d45»")]
    [InlineData(null)]
    [InlineData("")]
    public void Un_build_de_desarrollo(string? versionPuesto)
    {
        var version = new VersionPrograma(versionPuesto, Informativa, null, FechaDelArchivo, ".NET 11.0.0");

        Assert.Equal("desarrollo · 7b80d45", version.Cabecera);
    }

    [Fact(DisplayName = "presentacion-puesto · Una versión fuera de forma: 80 caracteres y un salto de línea dan «desarrollo», sin ningún carácter del metadato")]
    public void Una_version_fuera_de_forma()
    {
        var metadato = new string('a', 79) + "\n";

        var version = new VersionPrograma(metadato, Informativa, null, FechaDelArchivo, ".NET 11.0.0");

        Assert.Equal("desarrollo", version.Version);
        Assert.Equal("desarrollo · 7b80d45", version.Cabecera);
    }

    [Fact(DisplayName = "presentacion-puesto · Una versión fuera de forma: un tag válido seguido de un salto de línea también da «desarrollo»")]
    public void Un_tag_con_salto_de_linea()
    {
        var version = new VersionPrograma("v1.0.1\n", Informativa, null, FechaDelArchivo, ".NET 11.0.0");

        Assert.Equal("desarrollo", version.Version);
    }

    [Theory(DisplayName = "configuracion-puesto · Sin commit: sin «+», o con algo que no es hexadecimal o de largo fuera de 7 a 40, dice «sin commit»")]
    [InlineData("1.0.0")]
    [InlineData("1.0.0+zz80d455")]
    [InlineData("1.0.0+7b80d4")]
    [InlineData("1.0.0+7b80d455e506008a66194ddcfdf8412a306d71eb00")]
    [InlineData(null)]
    public void Sin_commit(string? informativa)
    {
        var version = new VersionPrograma("v1.0.1", informativa, null, FechaDelArchivo, ".NET 11.0.0");

        Assert.Equal("sin commit", version.Commit);
        Assert.Equal("v1.0.1 · sin commit", version.Cabecera);
    }

    [Fact(DisplayName = "configuracion-puesto · La versión de un paquete publicado: compilado el 11/11/2026 a las 21:30 UTC, en la zona de Buenos Aires dice «11/11/2026 18:30»")]
    public void La_fecha_de_compilacion_en_la_zona_del_puesto()
    {
        var version = new VersionPrograma("v1.0.1", Informativa, "2026-11-11T21:30:00Z", FechaDelArchivo, ".NET 11.0.0");

        Assert.Equal("11/11/2026 18:30", version.CompiladoEn(BuenosAires));
        Assert.Equal(".NET 11.0.0", version.Net);
    }

    [Theory(DisplayName = "configuracion-puesto · La versión de un build de desarrollo: sin fecha de compilación guardada, o con una que no es ISO, usa la del archivo del programa")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ayer")]
    [InlineData("Thu, 12 Nov 2026 00:00:00 GMT")]
    [InlineData("2026-11-11")]
    public void Sin_fecha_usa_la_del_archivo(string? fecha)
    {
        var version = new VersionPrograma(null, Informativa, fecha, FechaDelArchivo, ".NET 11.0.0");

        Assert.Equal("10/11/2026 09:00", version.CompiladoEn(BuenosAires));
    }

    [Fact(DisplayName = "presentacion-puesto · La del puesto: el ensamblado del Kiosk compilado para las pruebas, sin tag, dice «desarrollo» y un commit")]
    public void La_version_del_puesto_compilado()
    {
        var version = VersionPrograma.DelPuesto();

        Assert.Equal("desarrollo", version.Version);
        Assert.Matches("^desarrollo · ([0-9a-f]{7}|sin commit)$", version.Cabecera);
    }
}
