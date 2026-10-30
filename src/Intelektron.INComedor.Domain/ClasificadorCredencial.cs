namespace Intelektron.INComedor.Domain;

/// <summary>Motivo por el que un código no abre sesión; los nombres del contrato los pone Infrastructure.</summary>
public enum MotivoIdentificacion
{
    IdentityNotResolved,
    CredentialRevoked,
    CredentialExpired,
}

/// <summary>Causa interna de IDENTITY_NOT_RESOLVED, que va sólo a la bitácora (RB25).</summary>
public enum CausaIdentificacion
{
    UnknownCode,
    NotYetValid,
}

/// <summary>Vigencia y revocación de la credencial que resolvió el código.</summary>
public sealed record CredencialIdentificacion(DateTimeOffset? VigenteDesdeUtc, DateTimeOffset? VigenteHastaUtc, DateTimeOffset? RevocadaUtc);

/// <summary>Vigente si no hay motivo; si no, el motivo y, para IDENTITY_NOT_RESOLVED, su causa interna.</summary>
public sealed record ResultadoIdentificacion(MotivoIdentificacion? Motivo, CausaIdentificacion? Causa)
{
    public bool Vigente => Motivo is null;
}

/// <summary>Clasifica la credencial al identificarse, en el orden de RB25 (D2 de atender-seleccion-y-confirmacion).</summary>
public static class ClasificadorCredencial
{
    public static ResultadoIdentificacion Clasificar(CredencialIdentificacion? credencial, DateTimeOffset ahora)
    {
        if (credencial is null)
        {
            return new(MotivoIdentificacion.IdentityNotResolved, CausaIdentificacion.UnknownCode);
        }

        // La revocación vale desde la activación del snapshot que la trae: no se mira el reloj.
        if (credencial.RevocadaUtc is not null)
        {
            return new(MotivoIdentificacion.CredentialRevoked, null);
        }

        if (credencial.VigenteHastaUtc is { } hasta && hasta <= ahora)
        {
            return new(MotivoIdentificacion.CredentialExpired, null);
        }

        if (credencial.VigenteDesdeUtc is { } desde && desde > ahora)
        {
            return new(MotivoIdentificacion.IdentityNotResolved, CausaIdentificacion.NotYetValid);
        }

        return new(null, null);
    }
}
