using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Data.Sqlite;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// La clave del día de desarrollo (D6 de configurar-puesto-por-pestanas): el validador acepta de 1 a 12 dígitos ASCII, el
/// diálogo avisa que es de desarrollo, rechaza sin ejecutar la acción y lo tipeado no queda en ningún lado.
/// </summary>
public class ClaveDelDiaTests : IDisposable
{
    private readonly PuestoDePrueba _puesto = new(ModoPuesto.Operativo);

    public void Dispose() => _puesto.Dispose();

    [Theory(DisplayName = "acceso-puesto · El validador de desarrollo acepta de 1 a 12 dígitos ASCII y nada más")]
    [InlineData("4711", true)]
    [InlineData("0", true)]
    [InlineData("123456789012", true)]
    [InlineData("", false)]
    [InlineData("12a4", false)]
    [InlineData("1234567890123", false)]
    [InlineData("47 11", false)]
    [InlineData("٤٧١١", false)]
    public void El_validador_de_desarrollo(string clave, bool valida)
    {
        Assert.Equal(valida, new ClaveDelDiaDeDesarrollo().Validar(clave, new DateOnly(2026, 11, 6)));
    }

    [Fact(DisplayName = "acceso-puesto · Cualquier número en desarrollo: en operativo el engranaje pide la clave con el aviso de desarrollo, y 4711 abre Configuración")]
    public async Task Cualquier_numero_en_desarrollo()
    {
        var pantalla = _puesto.Dibujar();

        await pantalla.Find(".engranaje").ClickAsync(new());

        Assert.Equal(MensajesAtencion.ClaveDeDesarrollo, pantalla.Find("#aviso-desarrollo").TextContent.Trim());
        Assert.Empty(pantalla.FindAll("[role=\"tablist\"]"));
        var campo = pantalla.Find("#clave-del-dia");
        Assert.Equal(("password", "none", "off"), (campo.GetAttribute("type"), campo.GetAttribute("inputmode"), campo.GetAttribute("autocomplete")));

        await campo.InputAsync(new ChangeEventArgs { Value = "4711" });
        await pantalla.Find("#aceptar-clave").ClickAsync(new());

        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        Assert.NotEmpty(pantalla.FindAll("[role=\"tablist\"]"));
    }

    [Theory(DisplayName = "acceso-puesto · Clave vacía o con letras: el diálogo dice que no es válida y Configuración sigue cerrada")]
    [InlineData("")]
    [InlineData("12a4")]
    public async Task Clave_vacia_o_con_letras(string clave)
    {
        var pantalla = _puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());

        await pantalla.Find("#clave-del-dia").InputAsync(new ChangeEventArgs { Value = clave });
        await pantalla.Find("#aceptar-clave").ClickAsync(new());

        Assert.Equal(MensajesAtencion.ClaveInvalida, pantalla.Find("#clave-invalida").TextContent.Trim());
        Assert.NotEmpty(pantalla.FindAll("#dialogo-clave"));
        Assert.Empty(pantalla.FindAll("[role=\"tablist\"]"));
    }

    [Theory(DisplayName = "acceso-puesto · Cancelar no ejecuta la acción: «Cancelar» o Escape cierran el diálogo de la clave y Configuración sigue cerrada")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancelar_no_ejecuta_la_accion(bool conEscape)
    {
        var pantalla = _puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#clave-del-dia").InputAsync(new ChangeEventArgs { Value = "4711" });

        if (conEscape)
        {
            await pantalla.Find("#dialogo-clave").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        }
        else
        {
            await pantalla.Find("#cancelar-clave").ClickAsync(new());
        }

        Assert.Empty(pantalla.FindAll("#dialogo-clave"));
        Assert.Empty(pantalla.FindAll("[role=\"tablist\"]"));
        Assert.Equal(0, _puesto.Cierres);
        Assert.NotEmpty(pantalla.FindAll("#ingresar"));
    }

    [Fact(DisplayName = "acceso-puesto · La clave no queda en ningún lado: después de entrar con 4711 y cerrar, ni la base, ni las dos bitácoras, ni los archivos de la carpeta de datos tienen 4711 fuera de los UUID")]
    public async Task La_clave_no_queda_en_ningun_lado()
    {
        var pantalla = _puesto.Dibujar();
        await pantalla.Find(".engranaje").ClickAsync(new());
        await pantalla.Find("#clave-del-dia").InputAsync(new ChangeEventArgs { Value = "4711" });
        await pantalla.Find("#aceptar-clave").ClickAsync(new());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        Assert.NotEmpty(_puesto.General());
        Assert.DoesNotContain(pantalla.Markup, "4711");
        Assert.All(TextosDeLaBase(), t => Assert.DoesNotContain("4711", t));
        foreach (var archivo in Directory.EnumerateFiles(_puesto.Carpeta, "*", SearchOption.AllDirectories).Where(a => !a.EndsWith(".db", StringComparison.Ordinal)
                     && !a.EndsWith(".db-wal", StringComparison.Ordinal) && !a.EndsWith(".db-shm", StringComparison.Ordinal)))
        {
            var sinUuid = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(archivo), "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", "");
            Assert.False(sinUuid.Contains("4711", StringComparison.Ordinal), archivo);
        }
    }

    /// <summary>Cada valor de texto de cada tabla de la base, salvo los UUID, que pueden contener esos dígitos.</summary>
    private IEnumerable<string> TextosDeLaBase()
    {
        using var conexion = new SqliteConnection($"Data Source={_puesto.RutaBase};Pooling=False");
        conexion.Open();
        var tablas = new List<string>();
        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                tablas.Add(lector.GetString(0));
            }
        }

        var textos = new List<string>();
        foreach (var tabla in tablas)
        {
            using var comando = conexion.CreateCommand();
            comando.CommandText = $"SELECT * FROM \"{tabla}\"";
            using var lector = comando.ExecuteReader();
            while (lector.Read())
            {
                for (var i = 0; i < lector.FieldCount; i++)
                {
                    if (lector.GetValue(i) is string texto && !Guid.TryParse(texto, out _))
                    {
                        textos.Add(System.Text.RegularExpressions.Regex.Replace(texto, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", ""));
                    }
                }
            }
        }

        return textos;
    }
}
