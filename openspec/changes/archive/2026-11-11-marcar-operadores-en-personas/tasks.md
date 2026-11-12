# Tasks

Rama local `feature/mejoras-2-5-dw`, en el worktree `C:\AI\INComedor\INComedor-AI4DEVS-mejoras`, sobre `d6bbb24` (la
mejora 1 aplicada). Gate respondido por David el 11/11/2026 (proposal). Cada grupo cierra con
`git diff --diff-filter=M --name-only d6bbb24 -- tests/` pegado, que tiene que listar sólo lo aprobado en 1.1. Las
pruebas de base corren con `INCOMEDOR_SQLSERVER=INMO-DW-AR`. Cada tarea que toca una pantalla sigue la regla de
prolijidad de `config.yaml`. Se aplica después de `operador-desde-la-identificacion`, que también lee el permiso.

## 1. Las pruebas existentes

- [x] 1.1 David aprueba esta lista antes de que se toque una prueba existente (`AGENTS.md`). Verifica: su respuesta
  anotada en esta tarea, y en cada grupo el `git diff --diff-filter=M` sin archivos fuera de la lista.
  - `Infrastructure.Sqlite.Tests/ImportacionPersonasTests.cs`, la prueba «Descargar la planilla de ejemplo»: el
    encabezado esperado pasa de «Nombre», «Código» y los cupos a «Nombre», «Código», «Operador» y los cupos. El código
    sigue en la columna B, así que lo demás de la prueba no cambia.
  - Aprobada por David el 11/11/2026 a las 19:59 («Sí, se cambia»). Otra prueba existente necesita otra aprobación suya.
  - Sin cambios:
    - `ExportacionPersonasTests`, porque «Operador» va al final y las celdas que lee por posición no se corren;
    - `Kiosk.Tests/ImportarPersonasTests`, cuya planilla propia no tiene «Operador», que entonces no cambia nada;
    - `E2E.Tests/ImportarPersonasE2ETests`, porque su resumen sigue conteniendo «1 persona nueva, 1 modificada y 0 sin
      cambios.».
  - **Hallazgo al aplicar (11/11/2026, 20:50), pendiente de David:** la lista de arriba quedó corta. Con el delta aprobado
    aplicado tal cual, dos pruebas existentes más se ponen rojas, y ninguna se tocó (decisión 3 del plan del goal):
    - `Infrastructure.Sqlite.Tests/ExportacionPersonasTests.cs`, «Exportar Personas a CSV» (línea 59): exige que el
      encabezado termine en `"Opciones habilitadas"`, y el delta pone «Operador» al final. El cambio que haría falta:
      `Assert.EndsWith("\"Opciones habilitadas\";\"Operador\"", lineas[0], StringComparison.Ordinal);`.
    - `Kiosk.Tests/TablaPersonasTests.cs`, «Las columnas de cupo» (línea 48): compara la lista entera de encabezados, y
      el delta suma «Operador» después de «Estado». El cambio que haría falta:
      `Assert.Equal(["Nombre", "Estado", "Operador", "ALMUERZO", "REFRIGERIO", "Opciones habilitadas"], encabezados);`.
    Sin esa aprobación, la verificación completa (5.2 y las de los otros cuatro changes) no puede dar cero fallas.
  - **Aprobadas por David el 11/11/2026 a las 21:30** («Aprobar las dos», con las dos líneas a la vista), y aplicadas tal
    cual. La lista final de pruebas existentes modificadas por el change es esa y la de «Descargar la planilla de ejemplo».

## 2. La base: dar, quitar y la regla del propio permiso

- [x] 2.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests` (`OperadoresEnPersonasTests`), vistas fallar antes:
  - dar a `0001`: la fila nueva, vigente desde el guardado y sin fin;
  - dar a `9002`, vencido: la misma fila, actualizada;
  - quitar a `9001`: revocado en la versión nueva, y la anterior sin cambios;
  - el `CONFIGURATION_CHANGED` con el permiso antes y después, sin código;
  - con `9001` como actor, cambiar el propio permiso se deniega con `OWN_PERMISSION`, sin versión y con el rechazo en
    la bitácora, mientras que dárselo a otro se aplica.
  Hecho el 11/11/2026: `OperadoresEnPersonasTests`, 8 pruebas (las cinco de la tarea más la vista que dice quién es operador, guardar sin tocar el permiso y la persona de baja), 8 en rojo antes y 8 en verde después. El vigente y el vencido se suman al fixture sintético, como 9001 y 9002.

  Verifica: el filtro de la clase, en rojo antes y en verde después.
- [x] 2.2 D1 y D2 en `AdministracionLocal.cs`. Verifica: 2.1 en verde y `dotnet test
  tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` en verde, con el `git diff --diff-filter=M` del encabezado.
  Hecho: `PersonaAdministrada.Operador` (lo que el evaluador de RB26 da por vigente), `EdicionPersona.Operador` (null no lo toca), `CambiarPermiso` sobre la fila copiada o una nueva, `Operator` antes y después en el `CONFIGURATION_CHANGED` de la persona, y `OWN_PERMISSION` en `CodigosAdministracion`. 2.1 en verde; `Infrastructure.Sqlite.Tests` 328 de 329, con la roja de `ExportacionPersonasTests` del hallazgo de 1.1, que no es de este grupo; el `git diff --diff-filter=M` lista sólo `ImportacionPersonasTests.cs`.

## 3. Los Excel

- [x] 3.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests` (`OperadorEnLosExcelTests`), vistas fallar antes:
  - la exportación con «Operador» al final, en «Sí» o «No», en CSV y en Excel;
  - la planilla de ejemplo con «Operador» después de «Código», la segunda fila en «Sí» y la oración de «Instrucciones»;
  - la importación: «Sí» da y «No» quita; vacía o ausente no cambia; «Tal vez» y `=1+1` son errores `INVALID_VALUE`
    sin lo escrito; con `9001` como actor, su propia fila es un error `OWN_PERMISSION`; el resumen y `PEOPLE_IMPORTED`
    cuentan los permisos dados y quitados.
  Hecho: `OperadorEnLosExcelTests`, 10 pruebas (la exportación en CSV y en Excel, la planilla de ejemplo, «Sí» y «No» por planilla con los conteos de la revisión y del resultado, «si» y «SÍ», la celda vacía, la planilla de antes, «Tal vez» y `=1+1` con `INVALID_VALUE` sin lo escrito, y el operador que se quita a sí mismo con `OWN_PERMISSION`), 9 en rojo antes (la planilla de antes ya pasaba) y 10 en verde después.

  Verifica: el filtro de la clase, en rojo antes y en verde después.
- [x] 3.2 La reescritura aprobada en 1.1 y D4 en `ExportacionPersonas.cs` e `ImportacionPersonas.cs`. Verifica: 3.1 en
  verde, `ImportacionPersonasTests` en verde con su único cambio aprobado, y el `git diff --diff-filter=M`.
  Hecho: la reescritura aprobada en 1.1 (una línea de «Descargar la planilla de ejemplo»: «Operador» después de «Código»); «Operador» al final de la exportación; la planilla de ejemplo con «No» y «Sí» y su oración de «Instrucciones»; la importación con la columna opcional, la lista cerrada «Sí», «Si» y «No», `INVALID_VALUE` y `OWN_PERMISSION` (con el actor que `PanelConfiguracion` le pasa a `Revisar`); el resumen suma «N permisos de operador dados y M quitados.» cuando hay alguno, y `PEOPLE_IMPORTED`, `OperatorGranted` y `OperatorRevoked`. 3.1 en verde y `ImportacionPersonasTests` en verde con su único cambio aprobado; el `git diff --diff-filter=M` lista sólo ese archivo. `ExportacionPersonasTests` «Exportar Personas a CSV» queda en rojo por «Operador» al final, sin tocar: ver el hallazgo de 1.1.

## 4. La columna en Personas

- [x] 4.1 Pruebas nuevas en `Kiosk.Tests` (`OperadoresEnPersonasPantallaTests`), vistas fallar antes, una por escenario
  de «Los operadores en Personas»:
  - el escudo sólo en «Operador sintético 01», con su texto accesible;
  - marcar a «Persona sintética 01» y entrar con `0001` por el engranaje;
  - desmarcar a `9001` y el engranaje con su permiso revocado;
  - `9001` marca a otro, y su propia celda está deshabilitada;
  - la celda de una persona dada de baja, deshabilitada;
  - ordenar por «Operador».
  Hecho: `OperadoresEnPersonasPantallaTests`, 7 pruebas (las seis de la tarea más la importación en pantalla, con el resumen y `PEOPLE_IMPORTED`), 6 en rojo antes y 7 en verde después.

  Verifica: en rojo antes y en verde después.
- [x] 4.2 D3 en `PanelPersonas.razor`, `wwwroot/css/puesto.css` y `MensajesAtencion.cs`. Verifica: 4.1 en verde y
  `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde.
  Hecho: la columna «Operador» después de «Estado» (el escudo con `role="img"` y `aria-label="Operador"` en lectura; en edición, un botón de 44 × 44 px con `aria-pressed`, deshabilitado en la propia fila de un operador con el título `MensajesAtencion.PermisoPropio` y en una persona de baja), el orden con los operadores primero y el guardado que manda el permiso sólo si cambió. La oración de los permisos de una importación va dentro de `ResumenImportacion`, con dos parámetros opcionales, para no sumar un método de texto sin clasificar en `PuntoFinalTests`. 4.1 en verde; `Kiosk.Tests` 636 de 637, con la roja de `TablaPersonasTests` del hallazgo de 1.1, sin tocar.
- [x] 4.3 Prolijidad. Por CDP, a 1024 × 768, en Personas, en lectura y en edición, se miden:
  - el ancho de la columna «Operador» y el centro del escudo;
  - el alto del botón de la celda contra los campos de cupo de la misma fila (tolerancia de 1 px);
  - que se sigan viendo al menos diez filas.
  Hecho: `OperadoresEnPersonasE2ETests` en verde, en primer plano. Medidas de `registro.txt`: en lectura, la columna «Operador» de x 349,7 a 433,4 (83,7 px) con el escudo centrado en 391,5, 9 de 9 filas a la vista (la base de la demostración tiene 9 personas) y la tabla de 924 px en 924 px; en edición, el botón de 44,0 × 44,0 px contra los campos de cupo de 44,0 px, centrado en su columna y la tabla de 909 px en 909 px, sin barra horizontal. La primera medida dio 909,48 px en 909 (la barra aparecía por medio píxel): el encabezado «Operador» bajó su relleno y la prueba compara el ancho real de la tabla, sin tolerancia. Capturas `personas-operador-1024x768` y `personas-operador-edicion-1024x768`, miradas. La corrida completa de las 21:05 dio en rojo `RacionesEnPersonasE2ETests` («Con el teclado se ven 1 filas enteras»): con la columna nueva, lo usado («0/») saltaba arriba del campo del límite. La celda de cupo va en un renglón (`e93c629`) y el botón con `vertical-align: middle`: con el teclado entran 3 filas enteras, como antes, el botón queda a la altura de los cupos (418,5 px los dos) y la tabla sigue en 909 px; esta prueba lo afirma ahora con 1 px.

  Queda la captura `personas-operador-1024x768`, en una prueba E2E nueva, `OperadoresEnPersonasE2ETests`. Verifica:
  `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter-class *OperadoresEnPersonas*`, en primer plano, con las
  medidas pegadas de `registro.txt`.

## 5. Documentación y verificación completa

- [x] 5.1 `readme.md`: la columna «Operador» de Personas, la regla del propio permiso y la columna de los Excel.
  Verifica: `git grep -n "Operador»" -- readme.md` con las líneas nuevas.
  Hecho: `readme.md` describe la columna «Operador» de Personas, la regla del propio permiso y la columna de los Excel (exportación, planilla de ejemplo e importación). `git grep -n "Operador»" -- readme.md` da sus líneas (409, 414, 421, 422 y 427 al aplicarla; corridas después por la sección de la versión).
- [x] 5.2 La verificación completa: `dotnet build INComedor.sln -warnaserror`, `dotnet test INComedor.sln` y
  `openspec validate --all --strict`, sin errores. Verifica: las tres salidas pegadas, con el total de pruebas.
  Hecho con la corrida única del final (§6.6 del plan del goal), el 11/11/2026 de 21:46 a 21:52, sobre `afc4b87`, con `INCOMEDOR_SQLSERVER=INMO-DW-AR`: `dotnet build INComedor.sln -warnaserror` «0 Errores» (build exit 0); `dotnet test INComedor.sln` exit 0, «Resumen de la serie de pruebas: Correcta! · total: 1215 · error: 0 · correcto: 1215 · omitido: 0» (Contracts 23, Domain 91, Sync.Api 62, Infrastructure.Sqlite 329, Kiosk 661 y E2E 49 en 5 min 27 s); `openspec validate --all --strict` «Totals: 25 passed, 0 failed (25 items)». Las pruebas existentes modificadas son sólo las tres aprobadas por David (`git diff --diff-filter=M --name-only d6bbb24 -- tests/`).
- [x] 5.3 La evidencia: la fila nueva o actualizada en `docs/aplicacion-del-curso.md` (el pedido de David, con la regla
  del propio permiso y la revisión OWASP de la columna del Excel) y, si deja un workflow o un ajuste humano nuevo, su
  entrada en `prompts.md`. Verifica: el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso`, con su
  control negativo, `enlaces rotos: 0`.
  Hecho: la fila «OWASP Top 10 como criterio fijo del contexto» de `docs/aplicacion-del-curso.md` suma la lista cerrada de la columna «Operador», el `INVALID_VALUE` sin lo escrito y la regla del propio permiso en el servicio. No deja un workflow ni un ajuste humano nuevo para `prompts.md`. Chequeo de D8: `enlaces rotos: 0`; control negativo `enlaces rotos: 2`.

## Workflow follow-up

- `/openspec-verify-change marcar-operadores-en-personas` y su salida en la descripción del PR; el archivo del change en
  el mismo PR. Push, PR y merge a `final-project-dw` son de David.
