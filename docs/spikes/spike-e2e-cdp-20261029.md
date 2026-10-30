Veredicto: CDP funciona sobre el WebView2 del Kiosk real: Playwright se conectó con `ConnectOverCDPAsync`, recorrió la pantalla de demostración (`AUTHORIZED` y después `LOCAL_LIMIT_REACHED`) y sacó la captura en 4 corridas de 4, de 3 a 4 s cada una, con una condición: en un proceso elevado WebView2 ignora `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS`, y el puerto tiene que entrar por código, con una costura de seis líneas en `MainWindow`.

# Spike: E2E de la pantalla de demostración por CDP — 29/10/2026

Lo corrió una sesión de IA el 29/10, por decisión de David, antes de proponer el change `demostrar-flujo-de-punta-a-punta`, que lo cita en su design (D2) y en su spec. En ese momento nada entró al repo: todo corrió en un clon de `feature/invariantes-dw` (`48cc5bb`) en una carpeta temporal de la sesión. El 30/10 se trajo a `docs/spikes/` para que la evidencia quede en el repo que se entrega; el texto es el del 29/10, salvo dos referencias a documentos de gestión que no están en el repo.

## Pregunta y tope

¿Alcanza Playwright, por el protocolo de depuración de Chromium (CDP), al WebView2 que hospeda la pantalla Razor del Kiosk, sin instalar navegadores? Tope duro de 2 h: empezó a las 18:16:39 y cerró a las 18:22:58, con unos 6 minutos usados.

## Lo intentado, en orden

1. **Proyecto descartable** `tests/Spike.E2E.Cdp` en el clon: xUnit v3, `Microsoft.Playwright` 1.64.1 por `Directory.Packages.props`, sin `playwright install`. La prueba lanza el Kiosk compilado, guarda su PID, se conecta por CDP, toca `#confirmar` dos veces, comprueba `#decision` y `#motivo`, saca una captura y cierra el Kiosk por su PID, con su árbol de procesos. El paquete trae su driver (`.playwright/node` y `package`); no bajó ningún navegador.
2. **Con la variable documentada** (`WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333`): falló dos veces igual.

   ```text
   System.InvalidOperationException : CDP no respondió en el puerto 9333 en 30 s.
   ---- Microsoft.Playwright.PlaywrightException : connect ECONNREFUSED 127.0.0.1:9333
   Call log:
   - <ws preparing> retrieving websocket url from http://127.0.0.1:9333
   ```

3. **Diagnóstico, sólo lectura.** Con el Kiosk lanzado a mano con la misma variable: el proceso vivo, un hijo `msedgewebview2.exe` 156.0.4314.50 con `--user-data-dir=…\Intelektron.INComedor.Kiosk.WebView2\EBWebView`, **sin** `--remote-debugging-port` en su línea de comandos, y nada escuchando en el 9333. La documentación de WebView2 ([WebView2 browser flags](https://learn.microsoft.com/microsoft-edge/webview2/concepts/webview-features-flags)) dice: «Elevated apps ignore flags that are set via the local device environment». La sesión corría elevada (`High Mandatory Level`), y el Kiosk lanzado desde ella también.
4. **Con el puerto por código.** En el clon, `MainWindow` lee `INCOMEDOR_CDP_PORT` y, si trae un número, asigna `blazorWebView.BlazorWebViewInitializing` para poner `--remote-debugging-port` en `EnvironmentOptions.AdditionalBrowserArguments` ([diff](spike-e2e-cdp-20261029/costura-y-paquete.diff)). La prueba fija las dos variables. Pasó a la primera y tres veces más:

   ```text
   corrida  conexión CDP  página          toques                                    total
   1        1397 ms       https://0.0.0.1/ AUTHORIZED · DENIED LOCAL_LIMIT_REACHED  4 s
   2         924 ms       https://0.0.0.1/ AUTHORIZED · DENIED LOCAL_LIMIT_REACHED  4 s
   3         976 ms       https://0.0.0.1/ AUTHORIZED · DENIED LOCAL_LIMIT_REACHED  4 s
   4         903 ms       https://0.0.0.1/ AUTHORIZED · DENIED LOCAL_LIMIT_REACHED  3 s
   ```

   Cada corrida empezó con la carpeta de demostración borrada y terminó con `Kiosk PID … cerrado: True`. La captura de la primera está en [captura-cdp.png](spike-e2e-cdp-20261029/captura-cdp.png): el segundo toque, `DENIED` con `LOCAL_LIMIT_REACHED`.

## Lo que implica para la acción 3

- **El E2E puede ir por CDP** sobre el Kiosk real, con Playwright como paquete NuGet y sin navegadores: el WebView2 Runtime de la máquina es el navegador. El camino alternativo, entrar por la raíz de composición del Kiosk, no hace falta.
- **La costura va al repo**, porque el entorno no alcanza: el Kiosk necesita leer el puerto de una variable propia y pasarlo por `EnvironmentOptions`. Son seis líneas en `MainWindow`, inertes sin la variable. Tocan `src/`, así que van en el change de la acción 3, con su design y el gate de David.
- **Cerrar por PID con su árbol** (`Process.Kill(entireProcessTree: true)`) también cierra los `msedgewebview2`; después de las cuatro corridas no quedó ningún proceso del Kiosk.
- **La base de demostración** se reinicia borrando `%LOCALAPPDATA%\INComedor\demo\` antes de cada corrida, como dice `AGENTS.md`. Un E2E del repo tendría que hacerlo sin tocar la base del usuario: una ruta de base por variable es otra costura a decidir en el design.

## Lo que no se midió

- **Una sesión sin elevar con sólo la variable documentada.** Según la documentación tendría que funcionar; acá no se pudo probar, porque la sesión corría elevada.
- **El runner `windows-latest` de GitHub**: si tiene el WebView2 Runtime, si corre elevado y si una ventana WPF abre sin escritorio interactivo. La costura hace que la elevación no importe; las otras dos preguntas son de la acción 3. La primera CI del change, el 30/10, las respondió: el E2E pasó en `windows-latest`.
- La pantalla de atención completa: sólo existe la de demostración.

## Limpieza

- `%LOCALAPPDATA%\INComedor\demo\` no existía antes del spike; se borró al final y no existe.
- Ningún proceso `Intelektron.INComedor.Kiosk` quedó vivo.
- El clon y sus salidas quedaron en la carpeta temporal de la sesión, que no se versiona. La carpeta de datos de WebView2 del Kiosk (`%LOCALAPPDATA%\Intelektron.INComedor.Kiosk.WebView2\`) es la misma que usa cualquier corrida del Kiosk y no se tocó.

## Código mínimo

En [spike-e2e-cdp-20261029/](spike-e2e-cdp-20261029/), fuera de la solución, como evidencia y no como código que compile:

- [Spike.E2E.Cdp.csproj](spike-e2e-cdp-20261029/Spike.E2E.Cdp.csproj) y [DemostracionPorCdpTests.cs](spike-e2e-cdp-20261029/DemostracionPorCdpTests.cs): el proyecto y la prueba.
- [costura-y-paquete.diff](spike-e2e-cdp-20261029/costura-y-paquete.diff): la costura de `MainWindow` y la versión de Playwright.
- [registros.txt](spike-e2e-cdp-20261029/registros.txt) y [primer-intento-sin-costura.txt](spike-e2e-cdp-20261029/primer-intento-sin-costura.txt): las salidas.
