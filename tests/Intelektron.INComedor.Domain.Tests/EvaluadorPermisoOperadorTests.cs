namespace Intelektron.INComedor.Domain.Tests;

/// <summary>Evaluación del permiso de operador del snapshot activo (RB26), en memoria (D2 de atender-seleccion-y-confirmacion).</summary>
public class EvaluadorPermisoOperadorTests
{
    private static readonly DateTimeOffset Ahora = DateTimeOffset.Parse("2026-10-30T15:00:00Z");

    [Fact(DisplayName = "HU03-C · Sin ningún permiso: PERMISSION_MISSING, sin GrantId ni GrantedRole")]
    public void Sin_permisos_falta_el_permiso()
    {
        var resultado = EvaluadorPermisoOperador.Evaluar([], RolOperador.Operator, Ahora);

        Assert.Equal(MotivoPermiso.PermissionMissing, resultado.Motivo);
        Assert.Null(resultado.GrantId);
        Assert.Null(resultado.GrantedRole);
        Assert.False(resultado.Concedido);
    }

    [Fact(DisplayName = "AT54 · RB26 · Administrador sin OPERATOR: PERMISSION_MISSING, los roles no se heredan")]
    public void Administrador_no_hereda_operador()
    {
        var administrador = Permiso(RolOperador.SystemAdministrator, null, null, null);

        var resultado = EvaluadorPermisoOperador.Evaluar([administrador], RolOperador.Operator, Ahora);

        Assert.Equal(MotivoPermiso.PermissionMissing, resultado.Motivo);
        Assert.Null(resultado.GrantId);
    }

    [Fact(DisplayName = "AT54 · Operador vigente: concede con su GrantId y GrantedRole OPERATOR")]
    public void Operador_vigente_concede()
    {
        var operador = Permiso(RolOperador.Operator, Ahora.AddDays(-1), Ahora.AddDays(1), null);

        var resultado = EvaluadorPermisoOperador.Evaluar([operador], RolOperador.Operator, Ahora);

        Assert.True(resultado.Concedido);
        Assert.Null(resultado.Motivo);
        Assert.Equal(operador.PermisoId, resultado.GrantId);
        Assert.Equal(RolOperador.Operator, resultado.GrantedRole);
    }

    [Fact(DisplayName = "AT54 · Busca el permiso del rol pedido: entre OPERATOR y SYSTEM_ADMINISTRATOR elige el de OPERATOR")]
    public void Elige_el_permiso_del_rol_pedido()
    {
        var administrador = Permiso(RolOperador.SystemAdministrator, null, null, null);
        var operador = Permiso(RolOperador.Operator, null, null, null);

        var resultado = EvaluadorPermisoOperador.Evaluar([administrador, operador], RolOperador.Operator, Ahora);

        Assert.True(resultado.Concedido);
        Assert.Equal(operador.PermisoId, resultado.GrantId);
    }

    [Fact(DisplayName = "HU03-I · Permiso revocado: PERMISSION_REVOKED con su GrantId")]
    public void Permiso_revocado()
    {
        var operador = Permiso(RolOperador.Operator, null, null, Ahora.AddDays(-1));

        var resultado = EvaluadorPermisoOperador.Evaluar([operador], RolOperador.Operator, Ahora);

        Assert.Equal(MotivoPermiso.PermissionRevoked, resultado.Motivo);
        Assert.Equal(operador.PermisoId, resultado.GrantId);
        Assert.Equal(RolOperador.Operator, resultado.GrantedRole);
    }

    [Fact(DisplayName = "RB26 · Revocado y vencido a la vez: PERMISSION_REVOKED")]
    public void Revocado_y_vencido_es_revocado()
    {
        var operador = Permiso(RolOperador.Operator, null, Ahora.AddDays(-2), Ahora.AddDays(-1));

        var resultado = EvaluadorPermisoOperador.Evaluar([operador], RolOperador.Operator, Ahora);

        Assert.Equal(MotivoPermiso.PermissionRevoked, resultado.Motivo);
    }

    [Fact(DisplayName = "HU03-I · Permiso vencido: ValidToUtc igual al instante da PERMISSION_EXPIRED con su GrantId")]
    public void Permiso_vencido_en_el_limite_exacto()
    {
        var operador = Permiso(RolOperador.Operator, null, Ahora, null);

        var resultado = EvaluadorPermisoOperador.Evaluar([operador], RolOperador.Operator, Ahora);

        Assert.Equal(MotivoPermiso.PermissionExpired, resultado.Motivo);
        Assert.Equal(operador.PermisoId, resultado.GrantId);
    }

    [Fact(DisplayName = "RB26 · ValidToUtc un milisegundo después del instante: concede")]
    public void Vence_un_milisegundo_despues_concede()
    {
        var operador = Permiso(RolOperador.Operator, null, Ahora.AddMilliseconds(1), null);

        Assert.True(EvaluadorPermisoOperador.Evaluar([operador], RolOperador.Operator, Ahora).Concedido);
    }

    [Fact(DisplayName = "D2 · Permiso todavía no vigente: ValidFromUtc un milisegundo después da PERMISSION_NOT_YET_VALID")]
    public void Permiso_todavia_no_vigente()
    {
        var operador = Permiso(RolOperador.Operator, Ahora.AddMilliseconds(1), null, null);

        var resultado = EvaluadorPermisoOperador.Evaluar([operador], RolOperador.Operator, Ahora);

        Assert.Equal(MotivoPermiso.PermissionNotYetValid, resultado.Motivo);
        Assert.Equal(operador.PermisoId, resultado.GrantId);
    }

    [Fact(DisplayName = "RB26 · Extremos ausentes: un permiso sin vigencia ni revocación concede")]
    public void Extremos_ausentes_son_abiertos()
    {
        var operador = Permiso(RolOperador.Operator, null, null, null);

        Assert.True(EvaluadorPermisoOperador.Evaluar([operador], RolOperador.Operator, Ahora).Concedido);
    }

    private static PermisoOperador Permiso(RolOperador rol, DateTimeOffset? desde, DateTimeOffset? hasta, DateTimeOffset? revocado) =>
        new(Guid.NewGuid(), rol, desde, hasta, revocado);
}
