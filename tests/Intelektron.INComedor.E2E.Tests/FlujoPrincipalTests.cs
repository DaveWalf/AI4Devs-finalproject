using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;

namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// El flujo principal de punta a punta (D5 de demostrar-flujo-de-punta-a-punta), el escenario E2E de docs/pruebas.md:46:
/// identificación, selección, commit local, ticket y sincronización, con el receptor apagado y después recuperado. El
/// puesto y el receptor son los EXE compilados; la pantalla se maneja por CDP, y el ciclo de envío es el real, de 30 s.
/// Cada espera es una aserción con plazo, y cada paso deja una captura.
/// </summary>
public class FlujoPrincipalTests : IAsyncLifetime
{
    // Los textos de las specs vivas: atencion-puesto (el éxito y el cupo) y envio-lotes (la franja).
    private const string Exito = "Consumo registrado. Retirá tu comprobante";
    private const string Cupo = "Ya registraste este servicio. Consultá al responsable del comedor";
    private const string Opcion = "Almuerzo común";

    // El ciclo de envío corre cada 30 s: un estado nuevo de la franja llega en un ciclo, con margen.
    private static readonly LocatorAssertionsToContainTextOptions UnCiclo = new() { Timeout = 45_000 };

    private EscenarioE2E _escenario = null!;

    public ValueTask InitializeAsync()
    {
        _escenario = EscenarioE2E.Preparar("flujo-principal");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => _escenario.DisposeAsync();

    [Fact(DisplayName = "docs/pruebas.md:46 · AT01 · AT02 · AT27 · Flujo principal: identificación, selección, consumo y comprobante con el receptor apagado, cupo, pendientes y sincronización al volver el receptor")]
    public async Task Flujo_principal_de_punta_a_punta()
    {
        var reloj = Stopwatch.StartNew();

        // 1. El receptor genera los certificados y registra el puesto; después se apaga.
        var receptor = await _escenario.ArrancarReceptorAsync();
        await _escenario.DetenerAsync(receptor);
        Paso(reloj, "1. receptor preparado y apagado");

        // 2. El puesto con sus tres variables, en reposo por CDP.
        using var playwright = await Playwright.CreateAsync();
        var (_, pagina) = await _escenario.AbrirPuestoAsync(playwright);
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        await _escenario.CapturarAsync(pagina, "02-reposo");
        Paso(reloj, "2. puesto en reposo");

        // 3. 0001, «Almuerzo común», confirmación y comprobante, sin red.
        await Pedir(pagina, "0001");
        await _escenario.CapturarAsync(pagina, "03a-seleccion");
        await pagina.Locator("#confirmar").ClickAsync();
        await Assertions.Expect(pagina.Locator("#mensaje")).ToHaveTextAsync(Exito);
        await _escenario.CapturarAsync(pagina, "03b-consumo-registrado");
        Assert.Single(Directory.GetFiles(Path.Combine(_escenario.Carpeta, "comprobantes")));
        await pagina.Locator("#listo").ClickAsync();
        await Assertions.Expect(pagina.Locator("#codigo")).ToBeVisibleAsync();
        Paso(reloj, "3. consumo registrado y comprobante impreso");

        // 4. El mismo almuerzo otra vez: el cupo lo rechaza y no hay un segundo consumo (AT02).
        await Pedir(pagina, "0001");
        await pagina.Locator("#confirmar").ClickAsync();
        await Assertions.Expect(pagina.Locator("#mensaje")).ToHaveTextAsync(Cupo);
        await _escenario.CapturarAsync(pagina, "04-cupo");
        var consumo = Assert.Single(ConsumosDelPuesto());
        Assert.Single(Directory.GetFiles(Path.Combine(_escenario.Carpeta, "comprobantes")));
        await pagina.Locator("#listo").ClickAsync();
        Paso(reloj, $"4. cupo: un solo consumo en el puesto, {consumo}");

        // 5. Con el receptor apagado, la franja cuenta el consumo y el rechazo.
        var franja = pagina.GetByRole(AriaRole.Status, new() { Name = "Sincronización con el receptor" });
        await Assertions.Expect(franja).ToContainTextAsync("2 pendientes", UnCiclo);
        await _escenario.CapturarAsync(pagina, "05-pendientes");
        Paso(reloj, "5. franja: 2 pendientes");

        // 6. El receptor vuelve, con la misma base y los mismos certificados, y el puesto queda al día.
        await _escenario.ArrancarReceptorAsync(reintentar: false);
        await Assertions.Expect(franja).ToContainTextAsync("al día", UnCiclo);
        await _escenario.CapturarAsync(pagina, "06-al-dia");
        Paso(reloj, "6. receptor de vuelta, franja al día");

        // 7. El receptor tiene ese consumo, una sola vez (AT27).
        Assert.Equal([consumo], await ConsolidadosDelReceptorAsync());
        Paso(reloj, $"7. receptor: un consumo consolidado, {consumo}");
    }

    /// <summary>Identificación por código y elección de «Almuerzo común», pasando de página si no está en la primera.</summary>
    private static async Task Pedir(IPage pagina, string codigo)
    {
        await pagina.Locator("#codigo").FillAsync(codigo);
        await pagina.Locator("#ingresar").ClickAsync();
        var opcion = pagina.Locator(".opcion", new() { HasText = Opcion });
        await Assertions.Expect(pagina.Locator(".opcion").First).ToBeVisibleAsync();
        while (await opcion.CountAsync() == 0)
        {
            await pagina.Locator("#siguiente").ClickAsync();
        }

        await opcion.ClickAsync();
        await Assertions.Expect(opcion).ToHaveAttributeAsync("aria-pressed", "true");
    }

    /// <summary>Los consumos de la base del puesto, en sólo lectura y sin dejar la conexión abierta.</summary>
    private List<Guid> ConsumosDelPuesto()
    {
        var cadena = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_escenario.Carpeta, "incomedor.db"),
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ConnectionString;
        using var conexion = new SqliteConnection(cadena);
        conexion.Open();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT comconCodigo FROM tbComedorConsumos";
        using var filas = comando.ExecuteReader();
        var consumos = new List<Guid>();
        while (filas.Read())
        {
            consumos.Add(Guid.Parse(filas.GetString(0)));
        }

        return consumos;
    }

    private async Task<List<Guid>> ConsolidadosDelReceptorAsync()
    {
        await using var conexion = new SqlConnection(_escenario.CadenaConexionReceptor);
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT comconCodigo FROM tbComedorConsumosConsolidados";
        await using var filas = await comando.ExecuteReaderAsync();
        var consolidados = new List<Guid>();
        while (await filas.ReadAsync())
        {
            consolidados.Add(filas.GetGuid(0));
        }

        return consolidados;
    }

    private void Paso(Stopwatch reloj, string paso) => _escenario.Registrar($"{paso} · {reloj.Elapsed.TotalSeconds:F1} s");
}
