namespace Intelektron.INComedor.E2E.Tests;

/// <summary>
/// El árbol de procesos con el que el E2E busca escuchas y cierra el puesto, sobre instantáneas armadas a mano: Windows
/// reusa los PID, y un proceso cuyo padre terminó conserva ese PID como padre aunque ya lo tenga otro proceso.
/// </summary>
public class ProcesosYPuertosTests
{
    [Fact(DisplayName = "Ayudante del E2E · Un PID reusado: el árbol del puesto no suma a los procesos creados antes que su supuesto padre ni a los que no se pueden abrir (en la CI, csrss.exe, winlogon.exe, fontdrvhost.exe y dwm.exe dieron un falso «Quedaron vivos»)")]
    public void Un_PID_reusado_no_suma_procesos_ajenos()
    {
        // El puesto (100) lanzó WebView2 (200 y 300). 200 recibió el PID de un smss.exe que ya terminó: csrss.exe y
        // winlogon.exe, de antes, todavía lo nombran como padre; fontdrvhost.exe, de otra sesión, no se puede abrir.
        (int, int, string)[] procesos =
        [
            (100, 1, "Intelektron.INComedor.Kiosk.exe"),
            (200, 100, "msedgewebview2.exe"),
            (300, 200, "msedgewebview2.exe"),
            (400, 200, "csrss.exe"),
            (500, 200, "winlogon.exe"),
            (600, 200, "fontdrvhost.exe"),
        ];
        var creados = new Dictionary<int, long> { [100] = 1_000, [200] = 2_000, [300] = 3_000, [400] = 10, [500] = 20 };

        var arbol = ProcesosYPuertos.Arbol(100, procesos, pid => creados.TryGetValue(pid, out var hora) ? hora : null);

        Assert.Equal(new[] { 100, 200, 300 }, arbol.Keys.Order());
    }

    [Fact(DisplayName = "Ayudante del E2E · Con el puesto ya terminado: sin su hora de creación, sus hijos vivos siguen en el árbol, y el cierre los cuenta como «Quedaron vivos»")]
    public void Con_el_puesto_terminado_sus_hijos_siguen_en_el_arbol()
    {
        (int, int, string)[] procesos =
        [
            (200, 100, "msedgewebview2.exe"),
            (300, 200, "msedgewebview2.exe"),
        ];
        var creados = new Dictionary<int, long> { [200] = 2_000, [300] = 3_000 };

        var arbol = ProcesosYPuertos.Arbol(100, procesos, pid => creados.TryGetValue(pid, out var hora) ? hora : null);

        Assert.Equal(new[] { 200, 300 }, arbol.Keys.Where(pid => pid != 100).Order());
    }
}
