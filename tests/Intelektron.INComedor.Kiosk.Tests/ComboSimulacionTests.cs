using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Intelektron.INComedor.Kiosk.Atencion;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Intelektron.INComedor.Kiosk.Tests;

/// <summary>
/// El combo de personas del modo simulación en la pantalla de identificación (atencion-puesto de
/// simular-identificacion-por-lista, D1, D9 y D10): elegir identifica en el acto por la credencial, con los rechazos del
/// código tipeado, y ni el texto ni el valor de una entrada es un código. Sobre el puesto de bUnit y la semilla de la atención.
/// </summary>
public class ComboSimulacionTests
{
    private static readonly string[] CodigosSinteticos = ["0001", "0002", "0003", "0004", "9001", "9002", "9003", "9004"];
    private static readonly Guid PersonaDe0001 = Guid.Parse("00000000-0000-4000-8000-000000000027");
    private static readonly Guid CredencialDe0001 = Guid.Parse("00000000-0000-4000-8000-000000000031");

    [Fact(DisplayName = "atencion-puesto · Elegir a una persona abre la selección: elegir «Persona sintética 01» en el combo, sin «Ingresar», abre su sesión CONSUMPTION, muestra su nombre y el catálogo, y SESSION_OPENED dice que vino de la lista")]
    public async Task Elegir_a_una_persona_abre_la_seleccion()
    {
        using var puesto = Puesto(simulacion: true);
        var pantalla = puesto.Dibujar();

        await Elegir(pantalla, "Persona sintética 01");

        Assert.Contains("Persona sintética 01", pantalla.Find("#saludo").TextContent);
        Assert.NotEmpty(pantalla.FindAll(".opcion"));
        using var conexion = Abrir(puesto);
        using (var comando = conexion.CreateCommand())
        {
            comando.CommandText = "SELECT comperCodigo, comcreCodigo, comsidProposito FROM tbComedorSesionesIdentidad";
            using var lector = comando.ExecuteReader();
            Assert.True(lector.Read());
            Assert.Equal((PersonaDe0001, CredencialDe0001, "CONSUMPTION"), (lector.GetGuid(0), lector.GetGuid(1), lector.GetString(2)));
            Assert.False(lector.Read());
        }

        var abierta = JsonDocument.Parse(Assert.Single(Contextos(conexion, "SESSION_OPENED"))).RootElement;
        Assert.Equal("SIMULATION_LIST", abierta.GetProperty("Origin").GetString());
    }

    [Fact(DisplayName = "atencion-puesto · Una credencial vencida elegida en el combo: muestra el mensaje de CREDENTIAL_EXPIRED, sigue en reposo con el combo sin persona elegida, sin sesión, y el rechazo dice que vino de la lista")]
    public async Task Una_credencial_vencida_elegida_en_el_combo()
    {
        using var puesto = Puesto(simulacion: true);
        var pantalla = puesto.Dibujar();

        await Elegir(pantalla, "Persona sintética 02");

        Assert.Equal(MensajesAtencion.DeMotivo("CREDENTIAL_EXPIRED"), pantalla.Find("#aviso").TextContent);
        Assert.NotEmpty(pantalla.FindAll("#codigo"));
        Assert.Equal("", pantalla.Find("#simulacion-persona").GetAttribute("value") ?? "");
        using var conexion = Abrir(puesto);
        Assert.Equal(0L, Escalar(conexion, "SELECT COUNT(*) FROM tbComedorSesionesIdentidad"));
        var rechazo = JsonDocument.Parse(Assert.Single(Contextos(conexion, "IDENTIFICATION_REJECTED"))).RootElement;
        Assert.Equal("CREDENTIAL_EXPIRED", rechazo.GetProperty("ReasonCode").GetString());
        Assert.Equal("SIMULATION_LIST", rechazo.GetProperty("Origin").GetString());
    }

    [Fact(DisplayName = "atencion-puesto · Sin el modo simulación no hay combo: en mantenimiento con la simulación guardada apagada, tiene el campo del código y «Ingresar», y ningún combo")]
    public void Sin_el_modo_simulacion_no_hay_combo()
    {
        using var puesto = Puesto(simulacion: false);
        var pantalla = puesto.Dibujar();

        Assert.NotEmpty(pantalla.FindAll("#codigo"));
        Assert.NotEmpty(pantalla.FindAll("#ingresar"));
        Assert.Empty(pantalla.FindAll("#simulacion-persona"));
        Assert.Empty(pantalla.FindAll("select"));
    }

    [Fact(DisplayName = "atencion-puesto · El combo no muestra ni lleva códigos: las ocho personas sembradas, en orden alfabético, con textos sin códigos y valores que son GUID de credenciales y no códigos")]
    public void El_combo_no_muestra_ni_lleva_codigos()
    {
        using var puesto = Puesto(simulacion: true);
        var pantalla = puesto.Dibujar();

        var entradas = Entradas(pantalla);

        Assert.Equal(
            ["Administrador sintético 04", "Operador sintético 01", "Operador sintético 02", "Operador sintético 03",
             "Persona sintética 01", "Persona sintética 02", "Persona sintética 03", "Persona sintética 04"],
            entradas.Select(e => e.TextContent.Trim()));
        Assert.All(entradas, e =>
        {
            var valor = e.GetAttribute("value") ?? "";
            Assert.True(Guid.TryParse(valor, out _), $"El valor «{valor}» no es un GUID");
            Assert.DoesNotContain(valor, CodigosSinteticos);
            Assert.DoesNotContain(CodigosSinteticos, c => e.TextContent.Contains(c, StringComparison.Ordinal));
        });
        var reposo = pantalla.Find("main").TextContent;
        Assert.DoesNotContain(CodigosSinteticos, c => reposo.Contains(c, StringComparison.Ordinal));
        Assert.NotNull(pantalla.Find("label[for=\"simulacion-persona\"]"));
    }

    [Fact(DisplayName = "atencion-puesto · Una credencial que no está en la versión activa: llega desde la pantalla y da IDENTITY_NOT_RESOLVED con UNKNOWN_CODE, sin sesión y sin PersonId en el rechazo")]
    public async Task Una_credencial_que_no_esta_en_la_version_activa()
    {
        using var puesto = Puesto(simulacion: true);
        var pantalla = puesto.Dibujar();

        await pantalla.Find("#simulacion-persona").ChangeAsync(new ChangeEventArgs { Value = Guid.NewGuid().ToString() });

        Assert.Equal(MensajesAtencion.DeMotivo("IDENTITY_NOT_RESOLVED"), pantalla.Find("#aviso").TextContent);
        using var conexion = Abrir(puesto);
        Assert.Equal(0L, Escalar(conexion, "SELECT COUNT(*) FROM tbComedorSesionesIdentidad"));
        var rechazo = JsonDocument.Parse(Assert.Single(Contextos(conexion, "IDENTIFICATION_REJECTED"))).RootElement;
        Assert.Equal("IDENTITY_NOT_RESOLVED", rechazo.GetProperty("ReasonCode").GetString());
        Assert.Equal("UNKNOWN_CODE", rechazo.GetProperty("InternalCause").GetString());
        Assert.False(rechazo.TryGetProperty("PersonId", out _));
    }

    [Fact(DisplayName = "atencion-puesto · La lista sigue a la versión activa: agregar «Persona sintética 05» en Personas, guardar y cerrar Configuración la suma al combo en su lugar alfabético")]
    public async Task La_lista_sigue_a_la_version_activa()
    {
        using var puesto = Puesto(simulacion: true);
        var pantalla = puesto.Dibujar();
        await EntradaConfiguracion.Entrar(pantalla);
        await pantalla.Find("#abrir-personas").ClickAsync(new());
        await pantalla.Find("#agregar-persona").ClickAsync(new());
        await pantalla.FindAll("tr.persona").Last().QuerySelector("input.nombre-persona")!.InputAsync(new ChangeEventArgs { Value = "Persona sintética 05" });
        await pantalla.FindAll("tr.persona").Last().QuerySelector("input.codigo-persona")!.InputAsync(new ChangeEventArgs { Value = "0105" });

        await pantalla.Find("#guardar-panel").ClickAsync(new());
        await pantalla.Find("#cancelar-configuracion").ClickAsync(new());

        var nombres = Entradas(pantalla).Select(e => e.TextContent.Trim()).ToList();
        Assert.Equal(9, nombres.Count);
        Assert.Equal("Persona sintética 05", nombres[8]);
        Assert.Equal("Persona sintética 04", nombres[7]);
    }

    [Fact(DisplayName = "atencion-puesto · Identificación eligiendo a la persona en modo simulación (borde, OWASP): un valor del combo que no es un GUID se ignora, sin sesión, sin rechazo y en reposo")]
    public async Task Un_valor_que_no_es_un_guid_se_ignora()
    {
        using var puesto = Puesto(simulacion: true);
        var pantalla = puesto.Dibujar();

        await pantalla.Find("#simulacion-persona").ChangeAsync(new ChangeEventArgs { Value = "0001" });

        Assert.NotEmpty(pantalla.FindAll("#codigo"));
        Assert.Empty(pantalla.FindAll("#aviso"));
        using var conexion = Abrir(puesto);
        Assert.Equal(0L, Escalar(conexion, "SELECT COUNT(*) FROM tbComedorSesionesIdentidad"));
        Assert.Empty(Contextos(conexion, "IDENTIFICATION_REJECTED"));
    }

    /// <summary>El puesto de bUnit con el almacén de la simulación en su carpeta y, si se pide, el ajuste ya guardado.</summary>
    private static PuestoDePrueba Puesto(bool? simulacion = null)
    {
        var puesto = new PuestoDePrueba();
        if (simulacion is { } guardada)
        {
            new AlmacenSimulacion(puesto.Carpeta).Guardar(guardada);
        }

        puesto.Contexto.Services.AddSingleton(new AlmacenSimulacion(puesto.Carpeta));
        return puesto;
    }

    /// <summary>Las entradas del combo con una persona: todas menos la primera, la opción vacía.</summary>
    private static List<IElement> Entradas(IRenderedComponent<PantallaAtencion> pantalla) =>
        pantalla.FindAll("#simulacion-persona option").Where(o => !string.IsNullOrEmpty(o.GetAttribute("value"))).ToList();

    /// <summary>Elige en el combo la entrada con ese nombre, por el valor que la pantalla le dio.</summary>
    private static async Task Elegir(IRenderedComponent<PantallaAtencion> pantalla, string nombre)
    {
        var entrada = Entradas(pantalla).Single(e => e.TextContent.Trim() == nombre);
        await pantalla.Find("#simulacion-persona").ChangeAsync(new ChangeEventArgs { Value = entrada.GetAttribute("value") });
    }

    private static SqliteConnection Abrir(PuestoDePrueba puesto)
    {
        var conexion = new SqliteConnection($"Data Source={puesto.RutaBase};Mode=ReadOnly;Pooling=False");
        conexion.Open();
        return conexion;
    }

    private static long Escalar(SqliteConnection conexion, string sql)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        return Convert.ToInt64(comando.ExecuteScalar());
    }

    private static List<string> Contextos(SqliteConnection conexion, string tipo)
    {
        var contextos = new List<string>();
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = $tipo";
        comando.Parameters.AddWithValue("$tipo", tipo);
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            contextos.Add(lector.GetString(0));
        }

        return contextos;
    }
}
