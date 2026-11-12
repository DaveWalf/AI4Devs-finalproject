# Tasks

Rama local `feature/mejoras-2-3-dw`, en el worktree `C:\AI\INComedor\INComedor-AI4DEVS-mejoras`, desde `0af7359`. Antes
de aplicar se rebasa sobre la punta que deje el goal de `reimprimir-sin-codigo`. Gate respondido por David el 11/11/2026
(proposal). Cada grupo cierra con `git diff --diff-filter=M --name-only <base> -- tests/` pegado, que tiene que salir
vacío: este change no prevé tocar pruebas existentes. Las pruebas de base y receptor corren con
`INCOMEDOR_SQLSERVER=INMO-DW-AR`. Cada tarea que toca una pantalla sigue la regla de prolijidad de `config.yaml`.

## 1. Las pruebas existentes

- [x] 1.1 Confirmar que ninguna prueba existente cambia: las de la cabecera buscan `.servicio`, que no cambia, y las de
  General comparan con `Contains`. Verifica: `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde después del
  grupo 3, y el `git diff --diff-filter=M` del encabezado vacío. Si alguna prueba existente se pone roja, se para y se
  le pide a David la aprobación de esa prueba.
  Hecho: `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde para las pruebas de la cabecera y de General (`CabeceraTests` y las de esta tanda, 22 de 22) y `git diff --diff-filter=M --name-only d6bbb24 -- tests/` sin archivos de este change (lista sólo la prueba aprobada de `marcar-operadores-en-personas`). El proyecto entero, en la verificación única del final.

## 2. Leer la versión

- [x] 2.1 Pruebas nuevas en `Kiosk.Tests` (`VersionProgramaTests`), vistas fallar antes. Con `v1.0.1` y
  `1.0.0+7b80d455e506008a66194ddcfdf8412a306d71eb` da «v1.0.1 · 7b80d45». Sin versión, da «desarrollo · 7b80d45». Con
  80 caracteres y un salto de línea, da «desarrollo». Sin `+`, o con algo que no es hexadecimal, da «sin commit». Con
  `FechaCompilacionPuesto` `2026-11-11T21:30:00Z` y la zona de Buenos Aires, da «11/11/2026 18:30». Sin fecha, da la
  del archivo. Verifica: el filtro de la clase, en rojo antes y en verde después.
  Hecho el 11/11/2026: `VersionProgramaTests`, 14 casos (los de la tarea más la fecha que no es ISO, el commit de 6 y de 42 caracteres y el Kiosk compilado para las pruebas), 14 en rojo antes y 14 en verde después.
- [x] 2.2 `VersionPrograma` (D2) y los dos `AssemblyMetadata` del `.csproj` (D1). Verifica: 2.1 en verde y
  `dotnet build INComedor.sln -warnaserror` sin errores.
  Hecho: `VersionPrograma` (D2), con la misma expresión del tag que el script y el commit de 7 a 40 hexadecimales, y los dos `AssemblyMetadata` del `.csproj` (D1), vacíos sin las propiedades. 2.1 en verde; el build `-warnaserror` en Release lo corrió `publicar-puesto.ps1` en 4.2 y la verificación completa de 5.2.

## 3. La cabecera y General

- [x] 3.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes, una por escenario de `presentacion-puesto` y de
  `configuracion-puesto`. La cabecera muestra `#version-programa` en reposo y en la selección, y sigue sin botones.
  General termina con «Versión del programa», con sus cuatro datos. La sección no contiene la carpeta de datos de la
  prueba ni `Environment.MachineName` ni `Environment.UserName`. Verifica: en rojo antes y en verde después.
  Hecho: `VersionEnPantallaTests`, 4 pruebas (la cabecera en reposo y en la selección, sin botones y sin `tabindex`; General con «Versión del programa» como última sección y sus cuatro datos; y la sección sin la carpeta de datos, el nombre del equipo, el del usuario ni una ruta), 4 en rojo antes y 4 en verde después.
- [x] 3.2 D3 en `PantallaAtencion.razor` y `wwwroot/css/puesto.css`, y D4 en `PanelConfiguracion.razor`. Verifica: 3.1
  en verde y `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde.
  Hecho: `div.titulo-cabecera` con `.servicio` y `#version-programa` (12 px, `--tinta-suave` y `opacity: .8`) en `PantallaAtencion.razor` y `puesto.css` (D3), y `#version-programa-general` con su `dl` al final de General en `PanelConfiguracion.razor` (D4). 3.1 en verde, y `CabeceraTests` sin cambios y en verde (22 de 22 con las de la versión).
- [x] 3.3 Prolijidad. Por CDP, a 1024 × 768, se mide en reposo el alto de la cabecera (64 px, con tolerancia de 1 px),
  los centros horizontales de `.servicio` y de `#version-programa` (1 px), y el tamaño de letra de los dos. En General
  se miden los bordes izquierdos y la letra de los tres `h3` y de los rótulos del `dl`. Con eso se dejan las capturas
  `cabecera-version-1024x768` y `general-version-1024x768`. Va en el E2E nuevo de 4.1. Verifica: las medidas pegadas de
  `registro.txt`.
  Hecho, dentro de `VersionDelPaqueteE2ETests` (4.1). Medidas de `registro.txt`: la cabecera de 64 px, el centro de `.servicio` y de `#version-programa` en 511,996 los dos, la letra 22 y 12 px, ningún botón; en General, los tres `h3` («Bitácoras», «Resguardo de la configuración» y «Versión del programa») en x 49,0 con 19 px, los cuatro rótulos del `dl` en x 49,0 con 17 px y los valores en x 149,2. Capturas `cabecera-version-1024x768` y `general-version-1024x768`, mirada la de General.

## 4. El paquete lleva su versión

- [x] 4.1 La prueba E2E nueva de D5 (`VersionDelPaqueteE2ETests`), vista fallar antes. Sin
  `INCOMEDOR_VERSION_ESPERADA`, la cabecera dice «desarrollo · » más el commit corto de `git rev-parse --short HEAD`.
  Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter-class *VersionDelPaquete*`, en primer plano,
  en rojo antes y en verde después de 3.2.
  Hecho: `VersionDelPaqueteE2ETests`, en rojo antes (con la cabecera de `bded680`, sin `#version-programa`: el localizador vence a los 30 s) y en verde después, en primer plano: «la cabecera dice «desarrollo · bded680»; se esperaba «desarrollo · bded680»».
- [x] 4.2 D1 y D5 en `publicar-puesto.ps1`: la fecha tomada una vez, `-p:VersionPuesto` y `-p:FechaCompilacionPuesto`
  en el build y el publish, y `INCOMEDOR_VERSION_ESPERADA` con el tag para el E2E contra el EXE. Verifica:
  `pwsh .github/scripts/publicar-puesto.ps1 -Version v0.0.0-ensayo` termina sin error, el E2E contra el EXE lee
  «v0.0.0-ensayo» en la cabecera, y el zip queda en `artifacts/`. Pegar la salida.
  Hecho: `publicar-puesto.ps1` toma la fecha una vez y pasa `-p:VersionPuesto` y `-p:FechaCompilacionPuesto` al build y a la publicación, y corre contra el EXE el flujo principal y `Version_del_paquete` con `INCOMEDOR_VERSION_ESPERADA` en el tag. `pwsh .github/scripts/publicar-puesto.ps1 -Version v0.0.0-ensayo` (21:00 a 21:02 del 11/11/2026), exit 0: el restore y el build `-warnaserror` en Release con «0 Errores», sin MSB4181; «publicación revisada: 485 entradas, ninguna con datos ni secretos»; E2E contra el EXE «total: 2, error: 0, correcto: 2», con «la cabecera dice «v0.0.0-ensayo · 5882ba4»; se esperaba «v0.0.0-ensayo · 5882ba4»»; «zip: …\artifacts\INComedor-Puesto-v0.0.0-ensayo-win-x64.zip · 77891478 bytes · sha256 e761cd1ceb8eefe7cb61560fd1d9f586456e9e9a82de161ef4a30d00958ef394».
- [x] 4.3 El escenario «La cabecera no muestra el tag», con una corrida de control: `INCOMEDOR_VERSION_ESPERADA` en
  `v9.9.9` contra un EXE armado con `v0.0.0-ensayo`, con el E2E pedido a mano, que tiene que fallar. Verifica: la falla
  pegada, y que el script no deja zip cuando el E2E falla (el escenario «Prueba en rojo» de `distribucion-puesto`).
  Hecho: con `INCOMEDOR_VERSION_ESPERADA=v9.9.9` contra el EXE armado con `v0.0.0-ensayo`, el E2E pedido a mano falla: «Expected: "v9.9.9 · 5882ba4" · Actual: "v0.0.0-ensayo · 5882ba4"», «total: 1, error: 1». Y el script no deja zip con el E2E en rojo: corrido con `INCOMEDOR_SQLSERVER=NO-EXISTE-E2E` (el E2E falla por la misma llamada de `Invocar`), salió con exit 1 en «El E2E contra el EXE publicado» y `artifacts/` quedó sin ningún `.zip`; el zip bueno de 4.2 se guardó antes y volvió después, con la misma suma.

## 5. Documentación y verificación completa

- [x] 5.1 `readme.md`: dónde se ven la versión y el commit (la cabecera y General), y que un build desde el código dice
  «desarrollo». Verifica: el chequeo de enlaces relativos con su control negativo, `enlaces rotos: 0`.
  Hecho: `readme.md` suma «La versión que corre» (la cabecera, General, el tag que llega al EXE como `VersionPuesto` y «desarrollo» desde el código o la CI) y la versión entre lo que muestra General. Chequeo de enlaces relativos de D8 sobre `readme.md`: `enlaces rotos: 0`; control negativo `enlaces rotos: 2`.
- [ ] 5.2 La verificación completa: `dotnet build INComedor.sln -warnaserror`, `dotnet test INComedor.sln` y
  `openspec validate --all --strict`, sin errores. Verifica: las tres salidas pegadas, con el total de pruebas.
  Se cierra con la corrida única del final (§6.6 del plan del goal): pendiente hasta después del quinto change.
- [x] 5.3 La evidencia: la fila nueva o actualizada en `docs/aplicacion-del-curso.md` (el pedido de David, con el camino
  del tag medido contra MSB4181) y, si deja un workflow o un ajuste humano nuevo, su entrada en `prompts.md`. Verifica:
  el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso`, con su control negativo, `enlaces rotos: 0`.
  Hecho: la fila «Construir una vez y promover el mismo artefacto» de `docs/aplicacion-del-curso.md` suma el camino del tag medido contra MSB4181. No deja un workflow ni un ajuste humano nuevo para `prompts.md`. Chequeo de D8: `enlaces rotos: 0`; control negativo `enlaces rotos: 2`.

## Workflow follow-up

- `/openspec-verify-change mostrar-version-del-programa` y su salida en la descripción del PR; el archivo del change en el
  mismo PR. Push, PR, tag y Release son de David.
