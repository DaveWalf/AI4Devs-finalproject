using Intelektron.INComedor.Kiosk.Atencion;

namespace Intelektron.INComedor.Kiosk.Red;

/// <summary>Cómo terminó una aplicación de red.</summary>
public enum EstadoRed
{
    Aplicada,
    Cancelada,
    Fallida,
}

/// <summary>La salida de un paso: el código de <c>netsh</c>, o que se rechazó el permiso de administrador.</summary>
public readonly record struct SalidaNetsh(bool PermisoRechazado, int Codigo);

/// <summary>
/// El resultado de una aplicación de red (D11): aplicada, cancelada en un permiso o fallida, con el paso en que se cortó y
/// su código. Correr los pasos en orden y cortar en el primero que no termina en 0 es una función de los comandos y de cómo
/// se ejecuta cada uno.
/// </summary>
public sealed record ResultadoRed(EstadoRed Estado, int Pasos, int Aplicados, int? PasoCortado, int? Codigo)
{
    public static async Task<ResultadoRed> AplicarAsync(IReadOnlyList<ComandoNetsh> comandos, Func<ComandoNetsh, Task<SalidaNetsh>> ejecutar)
    {
        for (var i = 0; i < comandos.Count; i++)
        {
            var salida = await ejecutar(comandos[i]);
            if (salida.PermisoRechazado)
            {
                return new ResultadoRed(EstadoRed.Cancelada, comandos.Count, i, i + 1, null);
            }

            if (salida.Codigo != 0)
            {
                return new ResultadoRed(EstadoRed.Fallida, comandos.Count, i, i + 1, salida.Codigo);
            }
        }

        return new ResultadoRed(EstadoRed.Aplicada, comandos.Count, comandos.Count, null, null);
    }

    /// <summary>Lo que la pestaña dice del resultado: si la red no cambió o en qué paso se cortó.</summary>
    public string Mensaje => (Estado, Aplicados) switch
    {
        (EstadoRed.Aplicada, _) => MensajesAtencion.RedAplicada,
        (EstadoRed.Cancelada, 0) => MensajesAtencion.RedSinPermiso,
        (EstadoRed.Cancelada, _) => $"No se dio el permiso de administrador del paso {PasoCortado} de {Pasos}: quedaron aplicados los anteriores",
        (_, 0) => $"Falló el paso {PasoCortado} de {Pasos} con el código {Codigo}: la red no cambió",
        _ => $"Falló el paso {PasoCortado} de {Pasos} con el código {Codigo}: quedaron aplicados los anteriores",
    };

    /// <summary>
    /// El contexto de <c>CONFIGURATION_CHANGED</c> con <c>NETWORK_IPV4</c>: el adaptador, la IPv4 de antes y la pedida, el
    /// resultado con el paso en que se cortó y su código, el actor nulo y cómo se entró a Configuración (D11, RB21).
    /// </summary>
    public object Registro(AdaptadorRed adaptador, IPv4Validada pedida, string acceso) => new
    {
        Setting = "NETWORK_IPV4",
        Adapter = new { Index = adaptador.Indice, Name = adaptador.Nombre },
        Before = new
        {
            adaptador.Dhcp,
            Addresses = adaptador.Direcciones.Select(d => new { Address = d.Direccion, Mask = d.Mascara }).ToList(),
            Gateways = adaptador.Puertas,
            adaptador.Dns,
        },
        After = new { pedida.Dhcp, Address = pedida.Direccion, Mask = pedida.Mascara, Gateway = pedida.Puerta, pedida.Dns },
        Result = Estado switch { EstadoRed.Aplicada => "APPLIED", EstadoRed.Cancelada => "CANCELLED", _ => "FAILED" },
        Steps = Pasos,
        AppliedSteps = Aplicados,
        FailedStep = PasoCortado,
        ExitCode = Codigo,
        Access = acceso,
        ActorPersonId = (Guid?)null,
        ActorReason = "NO_AUTHENTICATION",
    };
}
