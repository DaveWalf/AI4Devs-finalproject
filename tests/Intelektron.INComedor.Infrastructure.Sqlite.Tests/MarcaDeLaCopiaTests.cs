namespace Intelektron.INComedor.Infrastructure.Sqlite.Tests;

/// <summary>
/// Lo que la impresora recibe según quién habilitó la acción (D2 y D3 de reimprimir-sin-codigo): la copia de la clave del
/// día, con SinValidez y sin Habilito; la del operador, con Habilito y sin SinValidez; el original recuperado, sin ninguno.
/// </summary>
public class MarcaDeLaCopiaTests : ConfirmacionTestBase
{
    private const string CodigoOperador = "OPERADOR-MARCA-77";

    private readonly ImpresoraDePrueba _impresora = new();
    private readonly Asistencia _asistencia;

    public MarcaDeLaCopiaTests()
    {
        _asistencia = new Asistencia(Base, Reloj, new TrabajosImpresion(Base, Reloj, _impresora));
    }

    [Fact(DisplayName = "comprobante-local · Reimpresión con la clave del día: la copia llega con SinValidez y sin Habilito")]
    public void La_copia_con_clave_llega_sin_validez()
    {
        var ticket = AutorizarYEnviar();

        Assert.True(_asistencia.ReimprimirConClave(ticket, MotivoReimpresion.PapelIlegible).Realizada);

        var copia = _impresora.Recibidos[^1];
        Assert.Equal(1, copia.NumeroCopia);
        Assert.True(copia.SinValidez);
        Assert.Null(copia.Habilito);
    }

    [Fact(DisplayName = "comprobante-local · Reimpresión con motivo: la copia del operador llega con Habilito y sin SinValidez")]
    public void La_copia_del_operador_llega_con_habilito()
    {
        var ticket = AutorizarYEnviar();
        InsertarOperador();
        var sesion = _asistencia.Entrar(CodigoOperador).SesionId!.Value;

        Assert.True(_asistencia.Reimprimir(sesion, ticket, MotivoReimpresion.PapelIlegible).Realizada);

        var copia = _impresora.Recibidos[^1];
        Assert.Equal("Operador sintético", copia.Habilito);
        Assert.False(copia.SinValidez);
    }

    [Fact(DisplayName = "comprobante-local · Recuperar con la clave del día: el original llega sin leyenda, sin Habilito y sin SinValidez (D3)")]
    public void El_original_recuperado_con_clave_no_lleva_marca()
    {
        _impresora.Modo = ModoImpresoraDePrueba.SinPapel;
        var ticket = AutorizarYEnviar();
        Guid original;
        using (var conexion = Abrir())
        {
            original = Sql.EscalarGuid(conexion,
                "SELECT comimpCodigo FROM tbComedorTrabajosImpresion WHERE comcmpCodigo = $tk AND comimpNumeroCopia = 0", ("$tk", ticket));
        }

        _impresora.Modo = ModoImpresoraDePrueba.Disponible;

        Assert.True(_asistencia.RecuperarConClave(original).Realizada);

        var recuperado = _impresora.Recibidos[^1];
        Assert.Equal(original, recuperado.TrabajoId);
        Assert.Null(recuperado.Leyenda);
        Assert.Null(recuperado.Habilito);
        Assert.False(recuperado.SinValidez);
    }

    [Fact(DisplayName = "D2 · Una copia con la clave después de una del operador: cada una lleva su propia línea, sin mezclarse")]
    public void Cada_copia_lleva_su_propia_linea()
    {
        var ticket = AutorizarYEnviar();
        InsertarOperador();
        var sesion = _asistencia.Entrar(CodigoOperador).SesionId!.Value;

        Assert.True(_asistencia.Reimprimir(sesion, ticket, MotivoReimpresion.PapelAtascado).Realizada);
        Assert.True(_asistencia.ReimprimirConClave(ticket, MotivoReimpresion.Otro).Realizada);

        var delOperador = Assert.Single(_impresora.Recibidos, r => r.NumeroCopia == 1);
        var conClave = Assert.Single(_impresora.Recibidos, r => r.NumeroCopia == 2);
        Assert.Equal(("Operador sintético", false), (delOperador.Habilito, delOperador.SinValidez));
        Assert.Equal(((string?)null, true), (conClave.Habilito, conClave.SinValidez));
    }

    private Guid AutorizarYEnviar()
    {
        var resultado = Unidad.Confirmar(Solicitud(Fixture.Habilitada, Fixture.AlmuerzoComun));
        Assert.Equal(TipoResultado.Autorizada, resultado.Tipo);
        new TrabajosImpresion(Base, Reloj, _impresora).EnviarPendientes();
        return resultado.TicketId!.Value;
    }

    private void InsertarOperador()
    {
        var persona = Guid.NewGuid();
        using var conexion = Abrir();
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPersonas VALUES ($vc, $per, 'Operador sintético', 1, 1)",
            ("$vc", Fixture.SnapshotId), ("$per", persona));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorCredencialesPersonas VALUES ($vc, $cre, $per, 'SERIAL_CODE', $valor, NULL, NULL, NULL)",
            ("$vc", Fixture.SnapshotId), ("$cre", Guid.NewGuid()), ("$per", persona), ("$valor", CodigoOperador));
        Sql.Ejecutar(conexion, "INSERT INTO tbComedorPermisosOperadores VALUES ($vc, $pop, $per, 'OPERATOR', NULL, NULL, NULL)",
            ("$vc", Fixture.SnapshotId), ("$pop", Guid.NewGuid()), ("$per", persona));
    }
}
