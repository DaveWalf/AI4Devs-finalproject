using System.Text.Json;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// Enter en el campo del código envía lo tipeado como «Ingresar» (atencion-puesto de simular-identificacion-por-lista, D8):
/// la misma identificación, los mismos rechazos y una sola por tecla. Sobre un ServicioAtencion real y un SQLite temporal.
/// </summary>
public class EnterEnElCodigoTests : IDisposable
{
    private const string PersonaDe0001 = "00000000-0000-4000-8000-000000000027";

    private readonly ArchivoTemporal _archivo = new();
    private readonly BunitContext _contexto = new();
    private readonly RelojManual _reloj = new("2026-11-10T15:00:00Z");

    public void Dispose()
    {
        _contexto.Dispose();
        SqliteConnection.ClearAllPools();
        _archivo.Dispose();
    }

    [Fact(DisplayName = "atencion-puesto · Enter envía el código: tipear 0001 y pulsar Enter, sin tocar «Ingresar», abre la sesión CONSUMPTION de su persona, muestra su nombre y el catálogo, y el campo queda vacío")]
    public async Task Enter_envia_el_codigo()
    {
        var pantalla = Dibujar();

        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "0001" });
        await pantalla.Find("#codigo").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Contains("Persona sintética 01", pantalla.Find("#saludo").TextContent);
        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        using (var conexion = Abrir())
        {
            using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT comperCodigo, comsidProposito FROM tbComedorSesionesIdentidad";
            using var lector = comando.ExecuteReader();
            Assert.True(lector.Read());
            Assert.Equal(Guid.Parse(PersonaDe0001), lector.GetGuid(0));
            Assert.Equal("CONSUMPTION", lector.GetString(1));
            Assert.False(lector.Read());
        }

        await pantalla.Find("#cancelar").ClickAsync(new());
        Assert.Equal("", pantalla.Find("#codigo").GetAttribute("value") ?? "");
    }

    [Fact(DisplayName = "atencion-puesto · Enter rechaza igual que «Ingresar» y una sola vez: 7777 con Enter muestra el mensaje de IDENTITY_NOT_RESOLVED, sigue en reposo con el campo vacío, sin sesión, con un solo rechazo UNKNOWN_CODE y sin 7777")]
    public async Task Enter_rechaza_una_sola_vez()
    {
        var pantalla = Dibujar();

        await pantalla.Find("#codigo").InputAsync(new ChangeEventArgs { Value = "7777" });
        await pantalla.Find("#codigo").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(MensajesAtencion.DeMotivo("IDENTITY_NOT_RESOLVED"), pantalla.Find("#aviso").TextContent);
        Assert.Equal("", pantalla.Find("#codigo").GetAttribute("value") ?? "");
        Assert.DoesNotContain("7777", pantalla.Markup);
        using var conexion = Abrir();
        Assert.Equal(0L, Escalar(conexion, "SELECT COUNT(*) FROM tbComedorSesionesIdentidad"));
        var rechazos = Textos(conexion, "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = 'IDENTIFICATION_REJECTED'");
        var rechazo = Assert.Single(rechazos);
        Assert.Equal("UNKNOWN_CODE", JsonDocument.Parse(rechazo).RootElement.GetProperty("InternalCause").GetString());
        Assert.DoesNotContain(Textos(conexion, "SELECT combitContexto FROM tbComedorBitacora"), t => t.Contains("7777", StringComparison.Ordinal));
    }

    private IRenderedComponent<PantallaAtencion> Dibujar()
    {
        var servicio = new ServicioAtencion(_archivo.Ruta, _reloj, new ImpresoraDePrueba());
        _contexto.Services.AddSingleton(servicio);
        _contexto.Services.AddSingleton(new MaquinaAtencion(servicio));
        return _contexto.Render<PantallaAtencion>();
    }

    private SqliteConnection Abrir()
    {
        var conexion = new SqliteConnection($"Data Source={_archivo.Ruta};Mode=ReadOnly");
        conexion.Open();
        return conexion;
    }

    private static long Escalar(SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private static List<string> Textos(SqliteConnection conexion, string sql)
    {
        var textos = new List<string>();
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            textos.Add(lector.GetString(0));
        }

        return textos;
    }
}
