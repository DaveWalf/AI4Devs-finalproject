namespace Intelektron.INComedor.Domain;

/// <summary>Roles de LocalOperatorGrant; no se heredan (RB26).</summary>
public enum RolOperador
{
    Operator,
    SystemAdministrator,
}

/// <summary>Motivo por el que se deniega una acción privilegiada; los nombres del contrato los pone Infrastructure.</summary>
public enum MotivoPermiso
{
    PermissionMissing,
    PermissionRevoked,
    PermissionExpired,
    PermissionNotYetValid,
}

/// <summary>Permiso de operador tal como está en el snapshot activo, con su vigencia sin filtrar.</summary>
public sealed record PermisoOperador(
    Guid PermisoId,
    RolOperador Rol,
    DateTimeOffset? VigenteDesdeUtc,
    DateTimeOffset? VigenteHastaUtc,
    DateTimeOffset? RevocadoUtc);

/// <summary>Concedido si no hay motivo; GrantId y GrantedRole, sólo si existía un permiso del rol pedido.</summary>
public sealed record ResultadoPermiso(MotivoPermiso? Motivo, Guid? GrantId, RolOperador? GrantedRole)
{
    public bool Concedido => Motivo is null;
}

/// <summary>Evalúa el permiso del rol pedido en el instante de la acción (D2 de atender-seleccion-y-confirmacion).</summary>
public static class EvaluadorPermisoOperador
{
    public static ResultadoPermiso Evaluar(IReadOnlyList<PermisoOperador> permisos, RolOperador rolPedido, DateTimeOffset ahora)
    {
        // La base tiene a lo sumo un permiso por persona y rol (UQ de la 0002).
        var permiso = permisos.FirstOrDefault(p => p.Rol == rolPedido);
        if (permiso is null)
        {
            return new(MotivoPermiso.PermissionMissing, null, null);
        }

        MotivoPermiso? motivo = permiso switch
        {
            { RevocadoUtc: not null } => MotivoPermiso.PermissionRevoked,
            { VigenteHastaUtc: { } hasta } when hasta <= ahora => MotivoPermiso.PermissionExpired,
            { VigenteDesdeUtc: { } desde } when desde > ahora => MotivoPermiso.PermissionNotYetValid,
            _ => null,
        };

        return new(motivo, permiso.PermisoId, permiso.Rol);
    }
}
