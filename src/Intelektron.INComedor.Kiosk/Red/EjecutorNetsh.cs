using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Intelektron.INComedor.Kiosk.Red;

/// <summary>
/// Corre un comando de <c>netsh</c> con el permiso de administrador que Windows pide en cada uno (D11). Lo registra sólo
/// <c>MainWindow</c>: sin él, como en las pruebas, la edición de red no está disponible y nada cambia la red del equipo.
/// </summary>
public sealed class EjecutorNetsh
{
    private const int CanceladoPorElUsuario = 1223;

    /// <summary>
    /// <c>netsh.exe</c> de System32 con <c>runas</c>: Windows pide el permiso de administrador para ese comando y nada más,
    /// porque el puesto no corre elevado. Un permiso rechazado vuelve como tal; otro error al lanzar, como un código.
    /// </summary>
    public async Task<SalidaNetsh> EjecutarAsync(ComandoNetsh comando, CancellationToken cancelacion)
    {
        var inicio = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "netsh.exe"), comando.Texto)
        {
            Verb = "runas",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            using var proceso = Process.Start(inicio);
            if (proceso is null)
            {
                return new SalidaNetsh(false, -1);
            }

            await proceso.WaitForExitAsync(cancelacion);
            return new SalidaNetsh(false, proceso.ExitCode);
        }
        catch (Win32Exception e) when (e.NativeErrorCode == CanceladoPorElUsuario)
        {
            return new SalidaNetsh(true, 0);
        }
        catch (Win32Exception e)
        {
            return new SalidaNetsh(false, e.NativeErrorCode);
        }
    }
}
