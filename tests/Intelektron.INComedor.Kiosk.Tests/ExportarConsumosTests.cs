using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Principal;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static Intelektron.INComedor.Kiosk.Tests.PuestoDePrueba;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// «Exportar CSV», «Exportar Excel» y «Cambiar carpeta» en Consumos (consulta-consumos de buscar-y-exportar-consumos, D6):
/// la carpeta se pide la primera vez con un <see cref="IElectorCarpeta"/> de prueba y se recuerda en
/// <c>exportacion.json</c>; cada exportación queda en la general como <c>CONSUMPTIONS_EXPORTED</c>, sin nombres ni códigos.
/// bUnit sobre el puesto de prueba, con la carpeta de exportación dentro de su carpeta temporal.
/// </summary>
public class ExportarConsumosTests
{
    [Fact(DisplayName = "consulta-consumos · Exportar a CSV: con dos consumos y la carpeta elegida, «Exportar CSV» deja un .csv con encabezado y dos filas, la pestaña dice su nombre y la general lo registra sin nombres ni códigos")]
    public async Task Exportar_a_csv()
    {
        using var puesto = new PuestoDePrueba();
        var (elector, destino) = Preparar(puesto, guardada: true);
        var pantalla = puesto.Dibujar();
        await DosConsumos(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#exportar-csv").ClickAsync(new());

        var archivo = Assert.Single(Directory.GetFiles(destino));
        Assert.EndsWith(".csv", archivo, StringComparison.Ordinal);
        Assert.Equal(3, File.ReadAllLines(archivo).Length);
        Assert.Equal(MensajesAtencion.ExportacionHecha(Path.GetFileName(archivo), 2, destino), pantalla.Find("#mensaje-exportacion").TextContent.Trim());
        Assert.Equal(0, elector.Pedidos);
        var linea = puesto.General().Single(l => Tipo(l) == "CONSUMPTIONS_EXPORTED");
        var contexto = ContextoDe(linea);
        Assert.True(contexto.GetProperty("LastHours").GetBoolean());
        Assert.False(contexto.GetProperty("PersonCode").GetBoolean());
        Assert.Equal("CSV", contexto.GetProperty("Format").GetString());
        Assert.Equal(2, contexto.GetProperty("Rows").GetInt32());
        Assert.Equal(Path.GetFileName(archivo), contexto.GetProperty("File").GetString());
        Assert.DoesNotContain("Persona sintética", linea.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"0001\"", linea.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain(destino.Replace("\\", "\\\\", StringComparison.Ordinal), linea.GetRawText(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "consulta-consumos · Exportar a Excel: «Exportar Excel» deja un .xlsx que se abre como libro con una hoja")]
    public async Task Exportar_a_excel()
    {
        using var puesto = new PuestoDePrueba();
        var (_, destino) = Preparar(puesto, guardada: true);
        var pantalla = puesto.Dibujar();
        await DosConsumos(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#exportar-xlsx").ClickAsync(new());

        var archivo = Assert.Single(Directory.GetFiles(destino));
        Assert.EndsWith(".xlsx", archivo, StringComparison.Ordinal);
        using var libro = ZipFile.OpenRead(archivo);
        Assert.NotNull(libro.GetEntry("xl/worksheets/sheet1.xml"));
        Assert.Equal("XLSX", ContextoDe(puesto.General().Single(l => Tipo(l) == "CONSUMPTIONS_EXPORTED")).GetProperty("Format").GetString());
    }

    [Fact(DisplayName = "consulta-consumos · La primera vez se elige la carpeta y después se recuerda: la pide una sola vez, la guarda en exportacion.json y las dos exportaciones quedan ahí")]
    public async Task La_primera_vez_se_elige_la_carpeta()
    {
        using var puesto = new PuestoDePrueba();
        var (elector, destino) = Preparar(puesto, guardada: false);
        var pantalla = puesto.Dibujar();
        await DosConsumos(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#exportar-csv").ClickAsync(new());
        puesto.Reloj.Avanzar(TimeSpan.FromSeconds(1));
        await pantalla.Find("#exportar-csv").ClickAsync(new());

        Assert.Equal(1, elector.Pedidos);
        Assert.Equal(destino, new AlmacenExportacion(puesto.Carpeta).Leer());
        Assert.Equal(2, Directory.GetFiles(destino, "*.csv").Length);
    }

    [Fact(DisplayName = "consulta-consumos · Cambiar carpeta: «Cambiar carpeta» la vuelve a pedir, la guarda, y la exportación siguiente va a la nueva")]
    public async Task Cambiar_carpeta()
    {
        using var puesto = new PuestoDePrueba();
        var (elector, anterior) = Preparar(puesto, guardada: true);
        var nueva = Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "exportaciones-nuevas")).FullName;
        elector.Respuesta = nueva;
        var pantalla = puesto.Dibujar();
        await DosConsumos(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#cambiar-carpeta").ClickAsync(new());
        Assert.Equal(MensajesAtencion.CarpetaElegida(nueva), pantalla.Find("#mensaje-exportacion").TextContent.Trim());
        await pantalla.Find("#exportar-csv").ClickAsync(new());

        Assert.Equal(1, elector.Pedidos);
        Assert.Equal(nueva, new AlmacenExportacion(puesto.Carpeta).Leer());
        Assert.Single(Directory.GetFiles(nueva));
        Assert.Empty(Directory.GetFiles(anterior));
    }

    [Fact(DisplayName = "consulta-consumos · Elegir la carpeta y cancelar: la pestaña avisa que no se eligió una carpeta, no se exporta ni se guarda nada y la general no registra una exportación")]
    public async Task Cancelar_la_eleccion()
    {
        using var puesto = new PuestoDePrueba();
        var (elector, _) = Preparar(puesto, guardada: false);
        elector.Respuesta = null;
        var pantalla = puesto.Dibujar();
        await DosConsumos(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#exportar-csv").ClickAsync(new());

        Assert.Equal(1, elector.Pedidos);
        Assert.Equal(MensajesAtencion.SinCarpeta, pantalla.Find("#mensaje-exportacion").TextContent.Trim());
        Assert.Null(new AlmacenExportacion(puesto.Carpeta).Leer());
        Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "CONSUMPTIONS_EXPORTED");
    }

    [Fact(DisplayName = "consulta-consumos · Sin poder escribir: con la carpeta guardada sin permiso de escritura, avisa, no deja ningún archivo y la general no tiene CONSUMPTIONS_EXPORTED")]
    public async Task Sin_poder_escribir()
    {
        using var puesto = new PuestoDePrueba();
        var (_, destino) = Preparar(puesto, guardada: true);
        var pantalla = puesto.Dibujar();
        await DosConsumos(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);
        var carpeta = new DirectoryInfo(destino);
        var regla = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!,
            FileSystemRights.CreateFiles | FileSystemRights.AppendData, AccessControlType.Deny);
        var seguridad = carpeta.GetAccessControl();
        seguridad.AddAccessRule(regla);
        carpeta.SetAccessControl(seguridad);
        try
        {
            await pantalla.Find("#exportar-csv").ClickAsync(new());

            Assert.Equal(MensajesAtencion.ExportacionFallida, pantalla.Find("#mensaje-exportacion").TextContent.Trim());
            Assert.Empty(Directory.GetFiles(destino));
            Assert.DoesNotContain(puesto.General(), l => Tipo(l) == "CONSUMPTIONS_EXPORTED");
        }
        finally
        {
            seguridad.RemoveAccessRule(regla);
            carpeta.SetAccessControl(seguridad);
        }
    }

    [Fact(DisplayName = "consulta-consumos · La carpeta elegida no se puede guardar: exporta igual dos veces a esa carpeta, la pide una sola vez y avisa que se usa hasta cerrar Configuración")]
    public async Task La_carpeta_elegida_no_se_puede_guardar()
    {
        using var puesto = new PuestoDePrueba();
        var (elector, destino) = Preparar(puesto, guardada: false);
        Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "exportacion.json"));
        var pantalla = puesto.Dibujar();
        await DosConsumos(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#exportar-csv").ClickAsync(new());
        var primero = Path.GetFileName(Assert.Single(Directory.GetFiles(destino)));
        Assert.Equal(MensajesAtencion.CarpetaNoGuardada + " " + MensajesAtencion.ExportacionHecha(primero, 2, destino),
            pantalla.Find("#mensaje-exportacion").TextContent.Trim());
        await pantalla.Find("#exportar-xlsx").ClickAsync(new());

        Assert.Equal(1, elector.Pedidos);
        Assert.Equal(2, Directory.GetFiles(destino).Length);
    }

    [Fact(DisplayName = "consulta-consumos · Dos exportaciones en el mismo segundo: dos toques en «Exportar CSV» dejan los dos archivos")]
    public async Task Dos_toques_en_el_mismo_segundo()
    {
        using var puesto = new PuestoDePrueba();
        var (_, destino) = Preparar(puesto, guardada: true);
        var pantalla = puesto.Dibujar();
        await DosConsumos(pantalla);
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#exportar-csv").ClickAsync(new());
        await pantalla.Find("#exportar-csv").ClickAsync(new());

        Assert.Equal(2, Directory.GetFiles(destino, "*.csv").Length);
        Assert.DoesNotContain(MensajesAtencion.ExportacionFallida, pantalla.Find("#mensaje-exportacion").TextContent);
    }

    [Fact(DisplayName = "consulta-consumos · Más de 10 000 consumos: el archivo tiene los primeros 10 000 y la pestaña avisa que hay más")]
    public async Task Mas_de_diez_mil()
    {
        using var puesto = new PuestoDePrueba();
        var (_, destino) = Preparar(puesto, guardada: true);
        var pantalla = puesto.Dibujar();
        await DosConsumos(pantalla);
        Clonar(puesto, 9999);
        await EntradaConfiguracion.Entrar(pantalla);

        await pantalla.Find("#exportar-csv").ClickAsync(new());

        var archivo = Assert.Single(Directory.GetFiles(destino));
        Assert.Equal(10_001, File.ReadAllLines(archivo).Length);
        Assert.Equal(MensajesAtencion.ExportacionHecha(Path.GetFileName(archivo), 10_000, destino) + " " + MensajesAtencion.ExportacionTopada,
            pantalla.Find("#mensaje-exportacion").TextContent.Trim());
    }

    /// <summary>Copias de un consumo ya registrado, en el mismo instante, por SQL y sin claves foráneas: sólo para el tope.</summary>
    private static void Clonar(PuestoDePrueba puesto, int copias)
    {
        using var conexion = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={puesto.RutaBase};Pooling=False");
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText =
            """
            PRAGMA foreign_keys = OFF;
            WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < $copias)
            INSERT INTO tbComedorConsumos (comconCodigo, comicCodigo, comvcCodigo, comperCodigo, commenCodigo, comicmCodigo, comconFechaNegocio,
                comconCantidad, comconVersionMenu, comconVersionPoliticas, comconVersionCatalogo, comconSecuenciaCliente,
                comconFechaConfirmacionLocalUtc, comconPrecioAplicado)
            SELECT printf('%08x-0000-4000-8000-%012x', i, i), printf('%08x-1111-4000-8000-%012x', i, i), c.comvcCodigo, c.comperCodigo,
                c.commenCodigo, NULL, c.comconFechaNegocio, c.comconCantidad, c.comconVersionMenu, c.comconVersionPoliticas,
                c.comconVersionCatalogo, 100000 + i, c.comconFechaConfirmacionLocalUtc, c.comconPrecioAplicado
            FROM n, (SELECT * FROM tbComedorConsumos LIMIT 1) c;
            """;
        comando.Parameters.AddWithValue("$copias", copias);
        comando.ExecuteNonQuery();
    }

    /// <summary>
    /// Registra el elector de prueba y el almacén de la exportación antes de dibujar; con <paramref name="guardada"/>, la carpeta
    /// ya elegida en <c>exportacion.json</c>. El elector responde esa misma carpeta si se le pide.
    /// </summary>
    private static (ElectorDePrueba Elector, string Destino) Preparar(PuestoDePrueba puesto, bool guardada)
    {
        var destino = Directory.CreateDirectory(Path.Combine(puesto.Carpeta, "exportaciones")).FullName;
        var almacen = new AlmacenExportacion(puesto.Carpeta);
        if (guardada)
        {
            almacen.Guardar(destino);
        }

        var elector = new ElectorDePrueba { Respuesta = destino };
        puesto.Contexto.Services.AddSingleton<IElectorCarpeta>(elector);
        puesto.Contexto.Services.AddSingleton(almacen);
        return (elector, destino);
    }

    /// <summary>Un almuerzo y un refrigerio de 0001.</summary>
    private static async Task DosConsumos(IRenderedComponent<PantallaAtencion> pantalla)
    {
        foreach (var refrigerio in new[] { false, true })
        {
            await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
            await pantalla.Find("#ingresar").ClickAsync(new());
            var opciones = pantalla.FindAll(".opcion").Where(o => refrigerio != o.TextContent.Contains("Almuerzo", StringComparison.Ordinal)).ToList();
            await opciones[0].ClickAsync(new());
            await pantalla.Find("#confirmar").ClickAsync(new());
            pantalla.WaitForAssertion(() => pantalla.Find("#listo"));
            await pantalla.Find("#listo").ClickAsync(new());
        }
    }

    private sealed class ElectorDePrueba : IElectorCarpeta
    {
        public string? Respuesta { get; set; }

        public int Pedidos { get; private set; }

        public string? Elegir(string? actual)
        {
            Pedidos++;
            return Respuesta;
        }
    }
}
