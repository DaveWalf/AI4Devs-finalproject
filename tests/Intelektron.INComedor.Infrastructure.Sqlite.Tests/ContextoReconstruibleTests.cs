using System.Text.Json;

namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Lo que la bitácora necesita para reconstruir una atención sin consultar la base (escribir-bitacoras-externas, D8 y D10):
/// lo mostrado en la oferta y en la decisión, la sesión abierta y el registro corto de la atención.
/// </summary>
public class ContextoReconstruibleTests : ConfirmacionTestBase
{
    [Fact(DisplayName = "bitacoras-externas · Autorización con sus cargos: el contexto tiene la etiqueta, la versión, el importe, la moneda y la escala, la fecha de negocio y el cargo de ALMUERZO de 1 unidad con límite 1, y cambiar el catálogo después no lo cambia")]
    public void Autorizacion_con_sus_cargos()
    {
        var solicitud = Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun);
        var resultado = Unidad.Confirmar(solicitud);
        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        using (var conexion = Abrir())
        {
            Sql.Ejecutar(conexion, "UPDATE tbComedorOpcionesMenu SET commenNombre = 'Otro nombre', commenImporteEmpleadoUnidadesMenores = 1 WHERE commenCodigo = $men",
                ("$men", Fixture.AlmuerzoComun.Id));
        }

        var contexto = Contexto("CONSUMPTION_AUTHORIZED");

        Assert.Equal(solicitud.IntentId, contexto.GetProperty("IntentId").GetGuid());
        Assert.Equal(resultado.DecisionId, contexto.GetProperty("DecisionId").GetGuid());
        Assert.Equal(resultado.ConsumptionId, contexto.GetProperty("ConsumptionId").GetGuid());
        Assert.Equal(resultado.TicketId, contexto.GetProperty("TicketId").GetGuid());
        Assert.Equal(Fixture.Habilitada.Id, contexto.GetProperty("PersonId").GetGuid());
        Assert.Equal("2026-10-19", contexto.GetProperty("BusinessDate").GetString());
        Assert.Equal(Fixture.AlmuerzoComun.Id, contexto.GetProperty("MenuItemId").GetGuid());
        Assert.Equal(1, contexto.GetProperty("MenuVersion").GetInt32());
        Assert.Equal("Almuerzo común 1", contexto.GetProperty("MenuLabel").GetString());
        var precio = contexto.GetProperty("Price");
        Assert.Equal(("ARS", 2, 150000L), (precio.GetProperty("Currency").GetString(), precio.GetProperty("Scale").GetInt32(), precio.GetProperty("EmployeeChargeMinor").GetInt64()));
        var cargo = Assert.Single(contexto.GetProperty("Charges").EnumerateArray());
        Assert.Equal(Fixture.CategoriaAlmuerzo, cargo.GetProperty("CategoryId").GetGuid());
        Assert.Equal("ALMUERZO", cargo.GetProperty("CategoryName").GetString());
        Assert.Equal(1, cargo.GetProperty("Units").GetInt32());
        Assert.Equal(1, cargo.GetProperty("Limit").GetInt32());
    }

    [Fact(DisplayName = "bitacoras-externas · Rechazo al confirmar: una opción ofrecida que se agota antes de confirmar deja la opción con su etiqueta, el motivo LOCAL_LIMIT_REACHED y la fecha de negocio, sin cargos ni comprobante")]
    public void Rechazo_al_confirmar()
    {
        var sesion = NuevaSesion(Fixture.Habilitada);
        new ConsultaDisponibilidad(Base, Reloj).Ofrecer(sesion, Fixture.Habilitada.Id);
        Assert.Equal(TipoResultado.Autorizada, Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun)).Tipo);

        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoOpcional, sesion));

        Assert.Equal(TipoResultado.Denegada, resultado.Tipo);
        var contexto = Contexto("CONSUMPTION_DENIED");
        Assert.Equal("LOCAL_LIMIT_REACHED", contexto.GetProperty("ReasonCode").GetString());
        Assert.Equal("2026-10-19", contexto.GetProperty("BusinessDate").GetString());
        Assert.Equal(Fixture.AlmuerzoOpcional.Id, contexto.GetProperty("MenuItemId").GetGuid());
        Assert.Equal(1, contexto.GetProperty("MenuVersion").GetInt32());
        Assert.Equal("Almuerzo opcional 1", contexto.GetProperty("MenuLabel").GetString());
        Assert.False(contexto.TryGetProperty("Charges", out _));
        Assert.False(contexto.TryGetProperty("TicketId", out _));
    }

    [Fact(DisplayName = "bitacoras-externas · Oferta con lo mostrado: cada opción ofrecida lleva su etiqueta y su importe en el registro OPTIONS_OFFERED, sin cambiar sus campos de antes")]
    public void Oferta_con_lo_mostrado()
    {
        var sesion = NuevaSesion(Fixture.Habilitada);

        var oferta = new ConsultaDisponibilidad(Base, Reloj).Ofrecer(sesion, Fixture.Habilitada.Id);

        var ofrecidas = Contexto("OPTIONS_OFFERED").GetProperty("Offered").EnumerateArray().ToList();
        Assert.Equal(oferta.Ofrecidas.Count, ofrecidas.Count);
        foreach (var (opcion, registrada) in oferta.Ofrecidas.Zip(ofrecidas))
        {
            Assert.Equal(opcion.Id, registrada.GetProperty("MenuItemId").GetGuid());
            Assert.Equal(opcion.Version, registrada.GetProperty("MenuVersion").GetInt32());
            Assert.Equal(opcion.Nombre, registrada.GetProperty("MenuLabel").GetString());
            var precio = registrada.GetProperty("Price");
            Assert.Equal(("ARS", 2, 150000L), (precio.GetProperty("Currency").GetString(), precio.GetProperty("Scale").GetInt32(), precio.GetProperty("EmployeeChargeMinor").GetInt64()));
        }
    }

    [Fact(DisplayName = "bitacoras-externas · Identificación aceptada: SESSION_OPENED queda en la transacción que abre la sesión, con la sesión, el propósito, la persona y el método, sin el código")]
    public void Identificacion_aceptada_abre_la_sesion_en_la_bitacora()
    {
        var resultado = new IdentificacionPorCodigo(Base, Reloj).Identificar(FixtureSintetico.CodigoSintetico, PropositoSesion.Consumption);

        Assert.True(resultado.Abierta);
        var contexto = Contexto("SESSION_OPENED");
        Assert.Equal(resultado.IdentitySessionId, contexto.GetProperty("IdentitySessionId").GetGuid());
        Assert.Equal("CONSUMPTION", contexto.GetProperty("Purpose").GetString());
        Assert.Equal(Fixture.Habilitada.Id, contexto.GetProperty("PersonId").GetGuid());
        Assert.Equal("CODE", contexto.GetProperty("Method").GetString());
        Assert.DoesNotContain(FixtureSintetico.CodigoSintetico, contexto.GetRawText());
        using var conexion = Abrir();
        Assert.Equal(1L, Sql.Escalar<long>(conexion, "SELECT COUNT(*) FROM tbComedorBitacora WHERE combitTipoEvento = 'SESSION_OPENED'"));
    }

    [Fact(DisplayName = "bitacoras-externas · La atención registra en su propia transacción corta: tipo, severidad, componente y contexto quedan en la bitácora durable con la secuencia siguiente")]
    public void La_atencion_registra_en_su_transaccion_corta()
    {
        new IdentificacionPorCodigo(Base, Reloj).Identificar(FixtureSintetico.CodigoSintetico, PropositoSesion.Consumption);
        var registro = new RegistroDelPuesto(Base, Reloj, "Kiosk.Pruebas");

        registro.Registrar("SESSION_CANCELLED", new { IdentitySessionId = Guid.Empty, MenuItemId = (Guid?)null });
        registro.Registrar("KIOSK_STOPPED", new { Prueba = 1 }, SeveridadBitacora.Warning);

        using var conexion = Abrir();
        Assert.Equal(3L, Sql.Escalar<long>(conexion, "SELECT MAX(combitSecuencia) FROM tbComedorBitacora"));
        Assert.Equal("Kiosk.Pruebas", Sql.Escalar<string>(conexion, "SELECT combitComponente FROM tbComedorBitacora WHERE combitTipoEvento = 'SESSION_CANCELLED'"));
        Assert.Equal("INFO", Sql.Escalar<string>(conexion, "SELECT combitSeveridad FROM tbComedorBitacora WHERE combitTipoEvento = 'SESSION_CANCELLED'"));
        Assert.Equal("WARNING", Sql.Escalar<string>(conexion, "SELECT combitSeveridad FROM tbComedorBitacora WHERE combitTipoEvento = 'KIOSK_STOPPED'"));
        var contexto = Contexto("SESSION_CANCELLED");
        Assert.Equal(JsonValueKind.Null, contexto.GetProperty("MenuItemId").ValueKind);
    }

    private JsonElement Contexto(string tipo)
    {
        using var conexion = Abrir();
        return JsonDocument.Parse(Sql.Escalar<string>(conexion,
            "SELECT combitContexto FROM tbComedorBitacora WHERE combitTipoEvento = $tipo ORDER BY combitSecuencia DESC LIMIT 1", ("$tipo", tipo)))
            .RootElement.Clone();
    }
}
