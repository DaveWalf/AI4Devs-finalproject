using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Intelektron.INComedor.Infrastructure.Sqlite;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// La exportación de la consulta de consumos a CSV y a Excel (consulta-consumos de buscar-y-exportar-consumos, D6): las
/// columnas de la tabla sin ningún código, el apóstrofo delante de lo que parece una fórmula, el libro mínimo, el temporal
/// que no queda tras un error y el tope de 10 000. Sobre una base SQLite real temporal con el fixture sintético; el reloj
/// está fijo en 2026-10-19T15:00:00Z, las 12:00 del puesto.
/// </summary>
public class ExportacionConsumosTests : ConfirmacionTestBase
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById(FixtureSintetico.ZonaHoraria);
    private const string NombreBase = "INComedor-consumos-20261019-120000";

    private readonly ExportacionConsumos _exportacion;
    private int _secuencia;

    public ExportacionConsumosTests()
    {
        _exportacion = new ExportacionConsumos(new ConsultaConsumos(Base, Reloj));
    }

    private FiltroConsumos Filtro => FiltroConsumos.UltimasHoras(Reloj.GetUtcNow(), Hoy);

    [Fact(DisplayName = "consulta-consumos · Exportar a CSV: UTF-8 con BOM, separado por punto y coma, un encabezado con las columnas de la tabla y dos filas en el orden vigente, sin ningún código")]
    public void Exportar_a_csv()
    {
        using var carpeta = new CarpetaTemporal();
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, new TimeOnly(11, 0));
        Insertar(Fixture.SinDerecho, Fixture.Refrigerio, new TimeOnly(10, 0));

        var resultado = _exportacion.Exportar(Filtro, FormatoExportacion.Csv, carpeta.Ruta, Zona);

        Assert.Null(resultado.Rechazo);
        Assert.Equal(NombreBase + ".csv", resultado.Archivo);
        Assert.Equal(2, resultado.Filas);
        Assert.False(resultado.HayMas);
        var bytes = File.ReadAllBytes(Path.Combine(carpeta.Ruta, resultado.Archivo!));
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var lineas = new UTF8Encoding(false).GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lineas.Length);
        Assert.Equal("\"Fecha y hora\";\"N.º\";\"Persona\";\"Opción\";\"Servicio\";\"Cargo\";\"Moneda\";\"Estado\"", lineas[0]);
        Assert.StartsWith("\"2026-10-19 11:00\";\"\";\"Persona habilitada\";", lineas[1], StringComparison.Ordinal);
        Assert.EndsWith(";\"500,00\";\"ARS\";\"—\"", lineas[1], StringComparison.Ordinal);
        Assert.StartsWith("\"2026-10-19 10:00\";\"\";\"Persona sin derecho\";", lineas[2], StringComparison.Ordinal);
        Assert.DoesNotContain(FixtureSintetico.CodigoSintetico, string.Join('\n', lineas), StringComparison.Ordinal);
        Assert.Equal([resultado.Archivo], Directory.GetFiles(carpeta.Ruta).Select(Path.GetFileName));
    }

    [Theory(DisplayName = "consulta-consumos · Un nombre que empieza como una fórmula: la celda del CSV lleva un apóstrofo delante")]
    [InlineData("=1+1")]
    [InlineData("+54 11 5555")]
    [InlineData("-2")]
    [InlineData("@SUMA(A1)")]
    [InlineData("\t=1+1")]
    [InlineData("\r=1+1")]
    public void Un_nombre_que_empieza_como_formula(string nombre)
    {
        using var carpeta = new CarpetaTemporal();
        Insertar(Fixture.SinDerecho, Fixture.Refrigerio, new TimeOnly(10, 0));
        Ejecutar("UPDATE tbComedorPersonas SET comperNombreVisible = $nombre WHERE comperCodigo = $per",
            ("$nombre", nombre), ("$per", Fixture.SinDerecho.Id));

        var resultado = _exportacion.Exportar(Filtro, FormatoExportacion.Csv, carpeta.Ruta, Zona);

        var texto = File.ReadAllText(Path.Combine(carpeta.Ruta, resultado.Archivo!), Encoding.UTF8);
        Assert.Contains(";\"'" + nombre + "\";", texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "consulta-consumos · Exportar a Excel: un libro que se abre como zip, con una hoja, el encabezado, dos filas y el cargo como número")]
    public void Exportar_a_excel()
    {
        using var carpeta = new CarpetaTemporal();
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, new TimeOnly(11, 0));
        Insertar(Fixture.SinDerecho, Fixture.Refrigerio, new TimeOnly(10, 0));

        var resultado = _exportacion.Exportar(Filtro, FormatoExportacion.Xlsx, carpeta.Ruta, Zona);

        Assert.Equal(NombreBase + ".xlsx", resultado.Archivo);
        using var libro = ZipFile.OpenRead(Path.Combine(carpeta.Ruta, resultado.Archivo!));
        Assert.NotNull(libro.GetEntry("[Content_Types].xml"));
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        var hojas = Leer(libro, "xl/workbook.xml").Descendants(ns + "sheet").ToList();
        Assert.Single(hojas);
        var filas = Leer(libro, "xl/worksheets/sheet1.xml").Descendants(ns + "row").ToList();
        Assert.Equal(3, filas.Count);
        Assert.Equal("Fecha y hora", filas[0].Elements(ns + "c").First().Value);
        Assert.Equal("Persona habilitada", Celda(filas[1], ns, "C2").Value);
        var cargo = Celda(filas[1], ns, "F2");
        Assert.Null(cargo.Attribute("t"));
        Assert.Equal(500m, decimal.Parse(cargo.Element(ns + "v")!.Value, CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "consulta-consumos · Sin poder escribir: con los nueve nombres de ese segundo ya tomados, el error no deja el temporal ni toca lo que ya estaba en la carpeta")]
    public void Un_error_no_deja_el_temporal()
    {
        using var carpeta = new CarpetaTemporal();
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, new TimeOnly(11, 0));
        var existentes = Enumerable.Range(1, ExportacionConsumos.NombresPorSegundo)
            .Select(i => Path.Combine(carpeta.Ruta, i == 1 ? NombreBase + ".csv" : $"{NombreBase}-{i}.csv")).ToList();
        existentes.ForEach(e => File.WriteAllText(e, "de antes"));

        Assert.ThrowsAny<IOException>(() => _exportacion.Exportar(Filtro, FormatoExportacion.Csv, carpeta.Ruta, Zona));

        Assert.Equal(existentes.Order(), Directory.GetFiles(carpeta.Ruta).Order());
        Assert.All(existentes, e => Assert.Equal("de antes", File.ReadAllText(e)));
    }

    [Fact(DisplayName = "consulta-consumos · Dos exportaciones en el mismo segundo: quedan los dos archivos, el segundo con «-2» antes de la extensión")]
    public void Dos_en_el_mismo_segundo()
    {
        using var carpeta = new CarpetaTemporal();
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, new TimeOnly(11, 0));

        var primera = _exportacion.Exportar(Filtro, FormatoExportacion.Csv, carpeta.Ruta, Zona);
        var segunda = _exportacion.Exportar(Filtro, FormatoExportacion.Csv, carpeta.Ruta, Zona);

        Assert.Equal(NombreBase + ".csv", primera.Archivo);
        Assert.Equal(NombreBase + "-2.csv", segunda.Archivo);
        Assert.Equal(new[] { primera.Archivo, segunda.Archivo }.Order(StringComparer.Ordinal), Directory.GetFiles(carpeta.Ruta).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact(DisplayName = "consulta-consumos · Exportar a Excel: un carácter fuera del plano básico en un nombre queda en el libro, como en el CSV")]
    public void Un_caracter_fuera_del_plano_basico()
    {
        using var carpeta = new CarpetaTemporal();
        var nombre = "Persona " + char.ConvertFromUtf32(0x1F355);
        Insertar(Fixture.SinDerecho, Fixture.Refrigerio, new TimeOnly(10, 0));
        Ejecutar("UPDATE tbComedorPersonas SET comperNombreVisible = $nombre WHERE comperCodigo = $per",
            ("$nombre", nombre), ("$per", Fixture.SinDerecho.Id));

        var resultado = _exportacion.Exportar(Filtro, FormatoExportacion.Xlsx, carpeta.Ruta, Zona);

        using var libro = ZipFile.OpenRead(Path.Combine(carpeta.Ruta, resultado.Archivo!));
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        var fila = Leer(libro, "xl/worksheets/sheet1.xml").Descendants(ns + "row").ElementAt(1);
        Assert.Equal(nombre, Celda(fila, ns, "C2").Value);
    }

    [Fact(DisplayName = "consulta-consumos · Sin poder escribir: una carpeta que ya no existe da error y no se crea")]
    public void Una_carpeta_que_ya_no_existe()
    {
        using var carpeta = new CarpetaTemporal();
        Insertar(Fixture.Habilitada, Fixture.AlmuerzoComun, new TimeOnly(11, 0));
        var borrada = Path.Combine(carpeta.Ruta, "borrada");

        Assert.ThrowsAny<IOException>(() => _exportacion.Exportar(Filtro, FormatoExportacion.Csv, borrada, Zona));

        Assert.False(Directory.Exists(borrada));
    }

    [Fact(DisplayName = "consulta-consumos · Exportar hasta 10 000: con 10 001 consumos, el archivo tiene 10 000 filas y el resultado dice que hay más")]
    public void El_tope_de_diez_mil()
    {
        using var carpeta = new CarpetaTemporal();
        Ejecutar(
            """
            WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < 10001)
            INSERT INTO tbComedorConsumos (comconCodigo, comicCodigo, comvcCodigo, comperCodigo, commenCodigo, comicmCodigo, comconFechaNegocio,
                comconCantidad, comconVersionMenu, comconVersionPoliticas, comconVersionCatalogo, comconSecuenciaCliente,
                comconFechaConfirmacionLocalUtc, comconPrecioAplicado)
            SELECT printf('%08x-0000-4000-8000-%012x', i, i), printf('%08x-1111-4000-8000-%012x', i, i), $vc, $per, $men, NULL, $fecha,
                1, $version, 1, 1, 5000 + i, $instante, $precio
            FROM n
            """,
            ("$vc", Fixture.SnapshotId), ("$per", Fixture.Habilitada.Id), ("$men", Fixture.AlmuerzoComun.Id),
            ("$fecha", Hoy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), ("$version", Fixture.AlmuerzoComun.Version),
            ("$instante", Iso(Reloj.GetUtcNow().AddMinutes(-30))), ("$precio", Precio));

        var resultado = _exportacion.Exportar(Filtro, FormatoExportacion.Csv, carpeta.Ruta, Zona);

        Assert.Equal(ExportacionConsumos.Tope, resultado.Filas);
        Assert.True(resultado.HayMas);
        var lineas = File.ReadAllText(Path.Combine(carpeta.Ruta, resultado.Archivo!), Encoding.UTF8).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(ExportacionConsumos.Tope + 1, lineas.Length);
    }

    private static string Precio => JsonSerializer.Serialize(new { Currency = "ARS", Scale = 2, EmployeeChargeMinor = 50000 });

    private static XDocument Leer(ZipArchive libro, string parte)
    {
        using var flujo = libro.GetEntry(parte)!.Open();
        return XDocument.Load(flujo);
    }

    private static XElement Celda(XElement fila, XNamespace ns, string referencia) =>
        fila.Elements(ns + "c").Single(c => (string?)c.Attribute("r") == referencia);

    private void Insertar(PersonaFixture persona, OpcionFixture opcion, TimeOnly horaLocal)
    {
        var instante = new DateTimeOffset(Hoy.ToDateTime(horaLocal), TimeSpan.FromHours(-3));
        Ejecutar(
            """
            INSERT INTO tbComedorConsumos (comconCodigo, comicCodigo, comvcCodigo, comperCodigo, commenCodigo, comicmCodigo, comconFechaNegocio,
                comconCantidad, comconVersionMenu, comconVersionPoliticas, comconVersionCatalogo, comconSecuenciaCliente,
                comconFechaConfirmacionLocalUtc, comconPrecioAplicado)
            VALUES ($con, $ic, $vc, $per, $men, NULL, $fecha, 1, $version, 1, 1, $secuencia, $instante, $precio)
            """,
            ("$con", Guid.NewGuid()), ("$ic", Guid.NewGuid()), ("$vc", Fixture.SnapshotId), ("$per", persona.Id), ("$men", opcion.Id),
            ("$fecha", Hoy.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), ("$version", opcion.Version), ("$secuencia", 1000 + ++_secuencia),
            ("$instante", Iso(instante)), ("$precio", Precio));
    }

    private void Ejecutar(string sql, params (string Nombre, object? Valor)[] parametros)
    {
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "PRAGMA foreign_keys = OFF");
        Sql.Ejecutar(conexion, sql, parametros);
    }

    /// <summary>Una carpeta de exportación temporal propia, que se borra al terminar.</summary>
    private sealed class CarpetaTemporal : IDisposable
    {
        public string Ruta { get; } = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "incomedor-tests", "exportacion-" + Guid.NewGuid().ToString("N"))).FullName;

        public void Dispose()
        {
            if (Directory.Exists(Ruta))
            {
                Directory.Delete(Ruta, recursive: true);
            }
        }
    }
}
