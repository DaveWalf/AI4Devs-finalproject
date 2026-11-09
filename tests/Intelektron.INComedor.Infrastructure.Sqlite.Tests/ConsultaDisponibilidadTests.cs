using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// La oferta por persona sobre SQLite real (D2, D6 y D7 de ofrecer-solo-opciones-disponibles): la misma evaluación que la
/// confirmación, sin escribir hechos de consumo y con su registro OPTIONS_OFFERED sin el código. Las personas de cada
/// motivo se siembran con Sql.Ejecutar, sin cambiar FixtureSintetico.
/// </summary>
public class ConsultaDisponibilidadTests : ConfirmacionTestBase
{
    private const string FechaDeNegocio = "2026-10-19";

    private ConsultaDisponibilidad Consulta => new(Base, Reloj);

    /// <summary>Una persona activa, o no, sin credencial, con una regla de ALMUERZO del límite dado (ninguna si es null).</summary>
    private PersonaFixture NuevaPersona(int? limiteAlmuerzo, bool activa = true)
    {
        var id = Guid.NewGuid();
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, $nombre, $activa, 1)",
            ("$vc", Fixture.SnapshotId), ("$per", id), ("$nombre", "Persona de prueba"), ("$activa", activa ? 1 : 0));
        if (limiteAlmuerzo is { } limite)
        {
            Sql.Ejecutar(conexion,
                "INSERT INTO tbComedorReglasHabilitacion VALUES ($vc, $reg, $per, NULL, $srv, $amb, $cat, $limite, 1, NULL, NULL)",
                ("$vc", Fixture.SnapshotId), ("$reg", Guid.NewGuid()), ("$per", id), ("$srv", Fixture.ServicioAlmuerzo),
                ("$amb", Fixture.Ambito), ("$cat", Fixture.CategoriaAlmuerzo), ("$limite", limite));
        }

        return new PersonaFixture(id, null);
    }

    private void GastarCupoDeAlmuerzo(PersonaFixture persona, int unidades)
    {
        using var conexion = Abrir();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorUsosCupo (comperCodigo, comacCodigo, comcatCodigo, comucFechaNegocio, comucUsado, comucVersion) VALUES ($per, $amb, $cat, $fecha, $usado, 1)",
            ("$per", persona.Id), ("$amb", Fixture.Ambito), ("$cat", Fixture.CategoriaAlmuerzo), ("$fecha", FechaDeNegocio), ("$usado", unidades));
    }

    private JsonElement RegistroOfertado()
    {
        using var conexion = Abrir();
        var contexto = Sql.Escalar<string>(conexion,
            "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = 'OPTIONS_OFFERED' ORDER BY combitSecuencia DESC LIMIT 1");
        return JsonDocument.Parse(contexto).RootElement.Clone();
    }

    [Fact(DisplayName = "habilitacion-local · La oferta y la confirmación coinciden: cada motivo retiene el almuerzo con el mismo código con que la confirmación lo deniega")]
    public void La_oferta_y_la_confirmacion_coinciden()
    {
        var conCupoLibre = NuevaPersona(limiteAlmuerzo: 1);
        var cupoAgotado = NuevaPersona(limiteAlmuerzo: 1);
        GastarCupoDeAlmuerzo(cupoAgotado, 1);
        var sinHabilitacion = NuevaPersona(limiteAlmuerzo: null);
        var reglaCero = NuevaPersona(limiteAlmuerzo: 0);
        var inactiva = NuevaPersona(limiteAlmuerzo: 1, activa: false);
        var casos = new (PersonaFixture Persona, string? MotivoEsperado)[]
        {
            (cupoAgotado, "LOCAL_LIMIT_REACHED"),
            (sinHabilitacion, "SERVICE_NOT_ENABLED"),
            (reglaCero, "RULE_DENIED"),
            (inactiva, "PERSON_INACTIVE"),
            (conCupoLibre, null),
        };

        foreach (var (persona, motivoEsperado) in casos)
        {
            var sesion = NuevaSesion(persona);
            var oferta = Consulta.Ofrecer(sesion, persona.Id);
            var confirmacion = Unidad.Confirmar(Solicitud(persona, Fixture.AlmuerzoComun, sesion));

            if (motivoEsperado is null)
            {
                Assert.Contains(oferta.Ofrecidas, o => o.Id == Fixture.AlmuerzoComun.Id);
                Assert.DoesNotContain(oferta.Retenidas, r => r.Id == Fixture.AlmuerzoComun.Id);
                Assert.Equal(TipoResultado.Autorizada, confirmacion.Tipo);
            }
            else
            {
                var retenida = Assert.Single(oferta.Retenidas, r => r.Id == Fixture.AlmuerzoComun.Id);
                Assert.DoesNotContain(oferta.Ofrecidas, o => o.Id == Fixture.AlmuerzoComun.Id);
                Assert.Equal(TipoResultado.Denegada, confirmacion.Tipo);
                Assert.Equal(motivoEsperado, retenida.Motivo);
                Assert.Equal(confirmacion.ReasonCode, retenida.Motivo);
            }
        }
    }

    [Fact(DisplayName = "habilitacion-local · Calcular la oferta no registra hechos: ninguna intención, consumo, carga de cupo ni evento de salida nuevos")]
    public void Calcular_la_oferta_no_registra_hechos()
    {
        var persona = Fixture.Habilitada;
        var sesion = NuevaSesion(persona);
        using var antes = Abrir();
        var conteoAntes = Sql.ConteoDeEscritura(antes);

        var oferta = Consulta.Ofrecer(sesion, persona.Id);

        using var despues = Abrir();
        var conteoDespues = Sql.ConteoDeEscritura(despues);
        Assert.NotEmpty(oferta.Ofrecidas);
        foreach (var tabla in Sql.TablasDeEscritura.Where(t => t != "tbComedorBitacora"))
        {
            Assert.Equal(conteoAntes[tabla], conteoDespues[tabla]);
        }

        Assert.Equal(conteoAntes["tbComedorBitacora"] + 1, conteoDespues["tbComedorBitacora"]);
    }

    [Fact(DisplayName = "atencion-puesto · Oferta con una categoría agotada: OPTIONS_OFFERED con las ofrecidas, las retenidas con LOCAL_LIMIT_REACHED y ALMUERZO 1 de 1, sin el código")]
    public void Oferta_con_categoria_agotada_queda_en_la_bitacora()
    {
        var persona = Fixture.Habilitada;
        Assert.Equal(TipoResultado.Autorizada, Unidad.Confirmar(Solicitud(persona, Fixture.AlmuerzoComun)).Tipo);
        var sesion = NuevaSesion(persona);

        var oferta = Consulta.Ofrecer(sesion, persona.Id);

        var refrigerios = new ConsultaCatalogo(Base).Leer().Where(o => o.Nombre.StartsWith("Refrigerio", StringComparison.Ordinal)).ToList();
        Assert.Equal(6, refrigerios.Count);
        Assert.Equal(refrigerios.Select(o => o.Id), oferta.Ofrecidas.Select(o => o.Id));
        Assert.Equal(9, oferta.Retenidas.Count);
        Assert.All(oferta.Retenidas, r => Assert.Equal("LOCAL_LIMIT_REACHED", r.Motivo));
        var agotada = Assert.Single(oferta.Agotadas);
        Assert.Equal(("ALMUERZO", 1, 1), (agotada.Nombre, agotada.Usado, agotada.Limite));
        Assert.Equal(Fixture.CategoriaAlmuerzo, agotada.Id);

        var registro = RegistroOfertado();
        Assert.Equal(sesion, registro.GetProperty("IdentitySessionId").GetGuid());
        Assert.Equal(persona.Id, registro.GetProperty("PersonId").GetGuid());
        Assert.True(registro.TryGetProperty("ConfigurationVersion", out _));
        Assert.Equal(6, registro.GetProperty("Offered").GetArrayLength());
        Assert.All(registro.GetProperty("Offered").EnumerateArray(), o =>
        {
            Assert.True(o.TryGetProperty("MenuItemId", out _));
            Assert.Equal(1, o.GetProperty("MenuVersion").GetInt32());
        });
        var retenidas = registro.GetProperty("Withheld").EnumerateArray().ToList();
        Assert.Equal(9, retenidas.Count);
        Assert.All(retenidas, r => Assert.Equal("LOCAL_LIMIT_REACHED", r.GetProperty("ReasonCode").GetString()));
        var agotadaRegistrada = Assert.Single(registro.GetProperty("Exhausted").EnumerateArray());
        Assert.Equal("ALMUERZO", agotadaRegistrada.GetProperty("Name").GetString());
        Assert.Equal(1, agotadaRegistrada.GetProperty("Used").GetInt32());
        Assert.Equal(1, agotadaRegistrada.GetProperty("Limit").GetInt32());
        Assert.Equal(Fixture.CategoriaAlmuerzo, agotadaRegistrada.GetProperty("CategoryId").GetGuid());
        Assert.DoesNotContain(FixtureSintetico.CodigoSintetico, registro.GetRawText());
    }

    [Fact(DisplayName = "atencion-puesto · Oferta vacía: OPTIONS_OFFERED sin opciones ofrecidas y con las quince del catálogo retenidas con SERVICE_NOT_ENABLED")]
    public void Oferta_vacia_queda_en_la_bitacora()
    {
        var persona = Fixture.SinDerecho;
        var sesion = NuevaSesion(persona);

        var oferta = Consulta.Ofrecer(sesion, persona.Id);

        Assert.Empty(oferta.Ofrecidas);
        Assert.Empty(oferta.Agotadas);
        Assert.Equal(15, oferta.Retenidas.Count);
        Assert.All(oferta.Retenidas, r => Assert.Equal("SERVICE_NOT_ENABLED", r.Motivo));

        var registro = RegistroOfertado();
        Assert.Equal(0, registro.GetProperty("Offered").GetArrayLength());
        Assert.Equal(15, registro.GetProperty("Withheld").GetArrayLength());
        Assert.All(registro.GetProperty("Withheld").EnumerateArray(), r => Assert.Equal("SERVICE_NOT_ENABLED", r.GetProperty("ReasonCode").GetString()));
        Assert.Equal(0, registro.GetProperty("Exhausted").GetArrayLength());
    }
}
