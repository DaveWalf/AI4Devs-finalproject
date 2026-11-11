# Tasks

Rama local `feature/mejoras-2-5-dw`, en el worktree `C:\AI\INComedor\INComedor-AI4DEVS-mejoras`, sobre `d6bbb24` (la
mejora 1 aplicada). Gate respondido por David el 11/11/2026 (proposal). Cada grupo cierra con
`git diff --diff-filter=M --name-only d6bbb24 -- tests/` pegado, que tiene que listar sólo lo aprobado en 1.1. Las
pruebas de base corren con `INCOMEDOR_SQLSERVER=INMO-DW-AR`. Cada tarea que toca una pantalla sigue la regla de
prolijidad de `config.yaml`. Se aplica después de `operador-desde-la-identificacion`, que también lee el permiso.

## 1. Las pruebas existentes

- [ ] 1.1 David aprueba esta lista antes de que se toque una prueba existente (`AGENTS.md`). Verifica: su respuesta
  anotada en esta tarea, y en cada grupo el `git diff --diff-filter=M` sin archivos fuera de la lista.
  - `Infrastructure.Sqlite.Tests/ImportacionPersonasTests.cs`, la prueba «Descargar la planilla de ejemplo»: el
    encabezado esperado pasa de «Nombre», «Código» y los cupos a «Nombre», «Código», «Operador» y los cupos. El código
    sigue en la columna B, así que lo demás de la prueba no cambia.
  - Sin cambios:
    - `ExportacionPersonasTests`, porque «Operador» va al final y las celdas que lee por posición no se corren;
    - `Kiosk.Tests/ImportarPersonasTests`, cuya planilla propia no tiene «Operador», que entonces no cambia nada;
    - `E2E.Tests/ImportarPersonasE2ETests`, porque su resumen sigue conteniendo «1 persona nueva, 1 modificada y 0 sin
      cambios.».

## 2. La base: dar, quitar y la regla del propio permiso

- [ ] 2.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests` (`OperadoresEnPersonasTests`), vistas fallar antes:
  - dar a `0001`: la fila nueva, vigente desde el guardado y sin fin;
  - dar a `9002`, vencido: la misma fila, actualizada;
  - quitar a `9001`: revocado en la versión nueva, y la anterior sin cambios;
  - el `CONFIGURATION_CHANGED` con el permiso antes y después, sin código;
  - con `9001` como actor, cambiar el propio permiso se deniega con `OWN_PERMISSION`, sin versión y con el rechazo en
    la bitácora, mientras que dárselo a otro se aplica.

  Verifica: el filtro de la clase, en rojo antes y en verde después.
- [ ] 2.2 D1 y D2 en `AdministracionLocal.cs`. Verifica: 2.1 en verde y `dotnet test
  tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` en verde, con el `git diff --diff-filter=M` del encabezado.

## 3. Los Excel

- [ ] 3.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests` (`OperadorEnLosExcelTests`), vistas fallar antes:
  - la exportación con «Operador» al final, en «Sí» o «No», en CSV y en Excel;
  - la planilla de ejemplo con «Operador» después de «Código», la segunda fila en «Sí» y la oración de «Instrucciones»;
  - la importación: «Sí» da y «No» quita; vacía o ausente no cambia; «Tal vez» y `=1+1` son errores `INVALID_VALUE`
    sin lo escrito; con `9001` como actor, su propia fila es un error `OWN_PERMISSION`; el resumen y `PEOPLE_IMPORTED`
    cuentan los permisos dados y quitados.

  Verifica: el filtro de la clase, en rojo antes y en verde después.
- [ ] 3.2 La reescritura aprobada en 1.1 y D4 en `ExportacionPersonas.cs` e `ImportacionPersonas.cs`. Verifica: 3.1 en
  verde, `ImportacionPersonasTests` en verde con su único cambio aprobado, y el `git diff --diff-filter=M`.

## 4. La columna en Personas

- [ ] 4.1 Pruebas nuevas en `Kiosk.Tests` (`OperadoresEnPersonasPantallaTests`), vistas fallar antes, una por escenario
  de «Los operadores en Personas»:
  - el escudo sólo en «Operador sintético 01», con su texto accesible;
  - marcar a «Persona sintética 01» y entrar con `0001` por el engranaje;
  - desmarcar a `9001` y el engranaje con su permiso revocado;
  - `9001` marca a otro, y su propia celda está deshabilitada;
  - la celda de una persona dada de baja, deshabilitada;
  - ordenar por «Operador».

  Verifica: en rojo antes y en verde después.
- [ ] 4.2 D3 en `PanelPersonas.razor`, `wwwroot/css/puesto.css` y `MensajesAtencion.cs`. Verifica: 4.1 en verde y
  `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde.
- [ ] 4.3 Prolijidad. Por CDP, a 1024 × 768, en Personas, en lectura y en edición, se miden:
  - el ancho de la columna «Operador» y el centro del escudo;
  - el alto del botón de la celda contra los campos de cupo de la misma fila (tolerancia de 1 px);
  - que se sigan viendo al menos diez filas.

  Queda la captura `personas-operador-1024x768`, en una prueba E2E nueva, `OperadoresEnPersonasE2ETests`. Verifica:
  `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter-class *OperadoresEnPersonas*`, en primer plano, con las
  medidas pegadas de `registro.txt`.

## 5. Documentación y verificación completa

- [ ] 5.1 `readme.md`: la columna «Operador» de Personas, la regla del propio permiso y la columna de los Excel.
  Verifica: `git grep -n "Operador»" -- readme.md` con las líneas nuevas.
- [ ] 5.2 La verificación completa: `dotnet build INComedor.sln -warnaserror`, `dotnet test INComedor.sln` y
  `openspec validate --all --strict`, sin errores. Verifica: las tres salidas pegadas, con el total de pruebas.
- [ ] 5.3 La evidencia: la fila nueva o actualizada en `docs/aplicacion-del-curso.md` (el pedido de David, con la regla
  del propio permiso y la revisión OWASP de la columna del Excel) y, si deja un workflow o un ajuste humano nuevo, su
  entrada en `prompts.md`. Verifica: el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso`, con su
  control negativo, `enlaces rotos: 0`.

## Workflow follow-up

- `/openspec-verify-change marcar-operadores-en-personas` y su salida en la descripción del PR; el archivo del change en
  el mismo PR. Push, PR y merge a `final-project-dw` son de David.
