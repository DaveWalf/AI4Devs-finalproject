# Tasks

Rama local `feature/mejoras-2-3-dw`, en el worktree `C:\AI\INComedor\INComedor-AI4DEVS-mejoras`, desde `0af7359`. Antes
de aplicar se rebasa sobre la punta que deje el goal de `reimprimir-sin-codigo`. Gate respondido por David el 11/11/2026
(proposal). Cada grupo cierra con `git diff --diff-filter=M --name-only <base> -- tests/` pegado, que tiene que salir
vacío: este change no prevé tocar pruebas existentes. Las pruebas de base y receptor corren con
`INCOMEDOR_SQLSERVER=INMO-DW-AR`. Cada tarea que toca una pantalla sigue la regla de prolijidad de `config.yaml`.

## 1. Las pruebas existentes

- [ ] 1.1 Confirmar que ninguna prueba existente cambia: las de la cabecera buscan `.servicio`, que no cambia, y las de
  General comparan con `Contains`. Verifica: `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde después del
  grupo 3, y el `git diff --diff-filter=M` del encabezado vacío. Si alguna prueba existente se pone roja, se para y se
  le pide a David la aprobación de esa prueba.

## 2. Leer la versión

- [ ] 2.1 Pruebas nuevas en `Kiosk.Tests` (`VersionProgramaTests`), vistas fallar antes. Con `v1.0.1` y
  `1.0.0+7b80d455e506008a66194ddcfdf8412a306d71eb` da «v1.0.1 · 7b80d45». Sin versión, da «desarrollo · 7b80d45». Con
  80 caracteres y un salto de línea, da «desarrollo». Sin `+`, o con algo que no es hexadecimal, da «sin commit». Con
  `FechaCompilacionPuesto` `2026-11-11T21:30:00Z` y la zona de Buenos Aires, da «11/11/2026 18:30». Sin fecha, da la
  del archivo. Verifica: el filtro de la clase, en rojo antes y en verde después.
- [ ] 2.2 `VersionPrograma` (D2) y los dos `AssemblyMetadata` del `.csproj` (D1). Verifica: 2.1 en verde y
  `dotnet build INComedor.sln -warnaserror` sin errores.

## 3. La cabecera y General

- [ ] 3.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes, una por escenario de `presentacion-puesto` y de
  `configuracion-puesto`. La cabecera muestra `#version-programa` en reposo y en la selección, y sigue sin botones.
  General termina con «Versión del programa», con sus cuatro datos. La sección no contiene la carpeta de datos de la
  prueba ni `Environment.MachineName` ni `Environment.UserName`. Verifica: en rojo antes y en verde después.
- [ ] 3.2 D3 en `PantallaAtencion.razor` y `wwwroot/css/puesto.css`, y D4 en `PanelConfiguracion.razor`. Verifica: 3.1
  en verde y `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde.
- [ ] 3.3 Prolijidad. Por CDP, a 1024 × 768, se mide en reposo el alto de la cabecera (64 px, con tolerancia de 1 px),
  los centros horizontales de `.servicio` y de `#version-programa` (1 px), y el tamaño de letra de los dos. En General
  se miden los bordes izquierdos y la letra de los tres `h3` y de los rótulos del `dl`. Con eso se dejan las capturas
  `cabecera-version-1024x768` y `general-version-1024x768`. Va en el E2E nuevo de 4.1. Verifica: las medidas pegadas de
  `registro.txt`.

## 4. El paquete lleva su versión

- [ ] 4.1 La prueba E2E nueva de D5 (`VersionDelPaqueteE2ETests`), vista fallar antes. Sin
  `INCOMEDOR_VERSION_ESPERADA`, la cabecera dice «desarrollo · » más el commit corto de `git rev-parse --short HEAD`.
  Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter-class *VersionDelPaquete*`, en primer plano,
  en rojo antes y en verde después de 3.2.
- [ ] 4.2 D1 y D5 en `publicar-puesto.ps1`: la fecha tomada una vez, `-p:VersionPuesto` y `-p:FechaCompilacionPuesto`
  en el build y el publish, y `INCOMEDOR_VERSION_ESPERADA` con el tag para el E2E contra el EXE. Verifica:
  `pwsh .github/scripts/publicar-puesto.ps1 -Version v0.0.0-ensayo` termina sin error, el E2E contra el EXE lee
  «v0.0.0-ensayo» en la cabecera, y el zip queda en `artifacts/`. Pegar la salida.
- [ ] 4.3 El escenario «La cabecera no muestra el tag», con una corrida de control: `INCOMEDOR_VERSION_ESPERADA` en
  `v9.9.9` contra un EXE armado con `v0.0.0-ensayo`, con el E2E pedido a mano, que tiene que fallar. Verifica: la falla
  pegada, y que el script no deja zip cuando el E2E falla (el escenario «Prueba en rojo» de `distribucion-puesto`).

## 5. Documentación y verificación completa

- [ ] 5.1 `readme.md`: dónde se ven la versión y el commit (la cabecera y General), y que un build desde el código dice
  «desarrollo». Verifica: el chequeo de enlaces relativos con su control negativo, `enlaces rotos: 0`.
- [ ] 5.2 La verificación completa: `dotnet build INComedor.sln -warnaserror`, `dotnet test INComedor.sln` y
  `openspec validate --all --strict`, sin errores. Verifica: las tres salidas pegadas, con el total de pruebas.
- [ ] 5.3 La evidencia: la fila nueva o actualizada en `docs/aplicacion-del-curso.md` (el pedido de David, con el camino
  del tag medido contra MSB4181) y, si deja un workflow o un ajuste humano nuevo, su entrada en `prompts.md`. Verifica:
  el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso`, con su control negativo, `enlaces rotos: 0`.

## Workflow follow-up

- `/openspec-verify-change mostrar-version-del-programa` y su salida en la descripción del PR; el archivo del change en el
  mismo PR. Push, PR, tag y Release son de David.
