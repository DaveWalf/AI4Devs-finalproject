namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Saber si lo tipeado en el diálogo de la clave es un código del snapshot activo, sin abrir sesión ni registrar nada
/// (D5 de entrar-a-configuracion-con-teclado): un código conocido se trata como el de un operador y nunca como clave del día.
/// Sobre el fixture sintético, con el código 000123 de la persona habilitada.
/// </summary>
public class CodigoConocidoTests : ConfirmacionTestBase
{
    private readonly IdentificacionPorCodigo _identificacion;

    public CodigoConocidoTests()
    {
        _identificacion = new IdentificacionPorCodigo(Base, Reloj);
    }

    [Fact(DisplayName = "acceso-puesto · Acceso de operador a Configuración (código conocido): un código vigente y uno vencido son conocidos, 4711 no, y ninguno escribe en la bitácora ni abre una sesión")]
    public void Codigos_conocidos_sin_registrar_nada()
    {
        InsertarVencida("SINTETICO-VENCIDO-88");
        using var conexion = Abrir();
        var registros = Sql.Contar(conexion, "tbComedorBitacora");

        Assert.True(_identificacion.EsCodigoConocido(FixtureSintetico.CodigoSintetico));
        Assert.True(_identificacion.EsCodigoConocido("SINTETICO-VENCIDO-88"));
        Assert.False(_identificacion.EsCodigoConocido("4711"));

        Assert.Equal(registros, Sql.Contar(conexion, "tbComedorBitacora"));
        Assert.Equal(0L, Sql.Contar(conexion, "tbComedorSesionesIdentidad"));
    }

    [Fact(DisplayName = "acceso-puesto · Acceso de operador a Configuración (código conocido, borde): el valor se normaliza como al identificar, con los espacios del borde y los ceros iniciales")]
    public void El_valor_se_normaliza_como_al_identificar()
    {
        Assert.True(_identificacion.EsCodigoConocido("  " + FixtureSintetico.CodigoSintetico + " "));
        Assert.False(_identificacion.EsCodigoConocido(FixtureSintetico.CodigoSintetico.TrimStart('0')));
        Assert.False(_identificacion.EsCodigoConocido(""));
    }

    private void InsertarVencida(string valor)
    {
        var persona = Guid.NewGuid();
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Persona sintética vencida', 1, 1)",
            ("$vc", Fixture.SnapshotId), ("$per", persona));
        Sql.Ejecutar(conexion,
            "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, $hasta, NULL)",
            ("$vc", Fixture.SnapshotId), ("$cre", Guid.NewGuid()), ("$per", persona), ("$valor", valor),
            ("$hasta", Iso(Reloj.Ahora.AddDays(-1))));
    }
}
