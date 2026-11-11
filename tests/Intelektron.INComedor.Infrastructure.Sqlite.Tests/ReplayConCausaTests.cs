namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Lo que la verificación de administrar-personas-y-opciones (06/11/2026) dejó sin prueba (cubrir-pruebas-de-las-verificaciones,
/// tarea 2.2): el replay de una denegación fuera de horario conserva su causa interna, porque devuelve la decisión original
/// sin volver a evaluar (RB06). El puesto está en America/Argentina/Buenos_Aires (UTC-3), como en FranjaEnLaEvaluacionTests.
/// </summary>
public class ReplayConCausaTests : ConfirmacionTestBase
{
    private const string FueraDeHorario = "OUTSIDE_SERVICE_HOURS";

    [Fact(DisplayName = "registro-consumo · Replay de una denegación fuera de horario: con el mismo IntentId, ya dentro de la franja, devuelve la decisión original con SERVICE_NOT_ENABLED y OUTSIDE_SERVICE_HOURS, sin consumo ni intención nueva")]
    public void El_replay_conserva_la_causa()
    {
        CargarFranja(Fixture.AlmuerzoComun, "11:00", "14:00");
        HoraLocal("10:00");
        var persona = Fixture.Habilitada;
        var solicitud = Solicitud(persona, Fixture.AlmuerzoComun, NuevaSesion(persona));
        var original = Unidad.Confirmar(solicitud);
        Assert.Equal((TipoResultado.Denegada, "SERVICE_NOT_ENABLED", FueraDeHorario), (original.Tipo, original.ReasonCode, original.ReasonCause));

        HoraLocal("12:00");
        var repetido = Unidad.Confirmar(solicitud);

        Assert.Equal(TipoResultado.Denegada, repetido.Tipo);
        Assert.Equal("SERVICE_NOT_ENABLED", repetido.ReasonCode);
        Assert.Equal(FueraDeHorario, repetido.ReasonCause);
        Assert.Equal(original.DecisionId, repetido.DecisionId);
        using var conexion = Abrir();
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorConsumos"));
        Assert.Equal(1L, Sql.Contar(conexion, "tbComedorIntencionesConsumo"));
    }

    private void CargarFranja(OpcionFixture opcion, string inicio, string fin)
    {
        using var conexion = Abrir();
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorFranjasOpcionesMenu (comvcCodigo, commenCodigo, comfraCodigo, comfraHoraInicio, comfraHoraFin) VALUES ($vc, $men, $fra, $ini, $fin)",
            ("$vc", Fixture.SnapshotId), ("$men", opcion.Id), ("$fra", Guid.NewGuid()), ("$ini", inicio), ("$fin", fin));
    }

    /// <summary>Lleva el reloj a la hora local del puesto del 19/10/2026 (UTC-3).</summary>
    private void HoraLocal(string hora) =>
        Reloj.Ahora = DateTimeOffset.Parse($"2026-10-19T{hora}:00-03:00");
}
