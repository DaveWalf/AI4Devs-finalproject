# Tasks

Rama local `feature/mejoras-2-5-dw` (antes `feature/mejoras-2-3-dw`), en el worktree
`C:\AI\INComedor\INComedor-AI4DEVS-mejoras`, rebasada sobre `d6bbb24`, la punta que dejó el goal de `reimprimir-sin-codigo`;
`<base>` es `d6bbb24`. Gate respondido por David el 11/11/2026
(proposal). Cada grupo cierra con `git diff --diff-filter=M --name-only <base> -- tests/` pegado, que tiene que salir
vacío: este change no prevé tocar pruebas existentes. Cada tarea que toca una pantalla sigue la regla de prolijidad de
`config.yaml`.

## 1. Las pruebas existentes

- [x] 1.1 Confirmar que ninguna prueba existente cambia, porque el corte es de CSS y el texto de las celdas sigue entero.
  Verifica: `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde después del grupo 3, y el
  `git diff --diff-filter=M` del encabezado vacío. Si alguna prueba existente se pone roja, se para y se le pide a
  David la aprobación de esa prueba.
  Hecho el 11/11/2026: `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` 609 de 609 después del grupo 3, y `git diff --diff-filter=M --name-only d6bbb24 -- tests/` vacío.

## 2. El globo del dato completo

- [x] 2.1 Pruebas nuevas en `Kiosk.Tests` (`GloboDeConsumosTests`), vistas fallar antes, una por escenario: tocar la
  persona abre `#globo-dato` con el nombre entero y otro toque lo cierra; el mouse medio segundo sobre la hora lo abre
  con «11/11/2026 13:51» y salir lo cierra (con `TimeProvider` falso); a los 5 s se cierra solo; con la tabla consultada
  por `0001`, el globo no contiene `0001`. Verifica: el filtro de la clase, en rojo antes y en verde después.
  Hecho: `GloboDeConsumosTests`, 6 pruebas (las cuatro del escenario más «el mouse que sale antes no abre» y «cambiar la consulta cierra»), 6 en rojo antes (sin manejadores de puntero) y 6 en verde después. El reloj de la prueba avanza la fecha al 11/11/2026 16:51 UTC para leer «11/11/2026 13:51».
- [x] 2.2 D4 en `PestanaConsumos.razor`: los eventos de las tres celdas, el globo único y su cierre. Verifica: 2.1 en
  verde.
  Hecho: los tres manejadores por celda, un solo globo `#globo-dato`, la espera de 500 ms y el cierre a los 5 s con el `TimeProvider` de la pestaña (`Reloj`, `TimeProvider.System` en la ventana), y el cierre al consultar y al dejar la pestaña. Ajuste del design (D4): el globo se ancla a su celda con `anchor-name`. 2.1 en verde.

## 3. Un renglón por consumo

- [x] 3.1 La prueba E2E nueva de D5 (`ConsumosEnUnRenglonE2ETests`), vista fallar antes contra la hoja de estilos de
  hoy. Crea por la pestaña Personas una persona sintética de 60 caracteres y otra «Persona sintética 01», con opciones
  de 40 caracteres y «Almuerzo con gaseosa», un original fallido y los consumos del escenario. Mide por CDP a
  1024 × 768 el alto de cada fila (igual al de una celda de un renglón, con tolerancia de 1 px), la letra de la tabla
  (entre 14 y 16 px), que la persona y la opción terminan en «…», y los bordes izquierdos, el alto y la letra de «Ver»,
  «Reimprimir» y «Recuperar». Deja la captura `consumos-un-renglon-1024x768`. Verifica:
  `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter-class *ConsumosEnUnRenglon*`, en rojo antes y en verde
  después, corrida en primer plano y con las medidas pegadas de `registro.txt`.
  Hecho: `ConsumosEnUnRenglonE2ETests`, rojo contra la hoja de hoy (filas de 87 px, la persona en 4 renglones) y verde después de 3.2, en primer plano. Medidas de `registro.txt` a 14 px: tres filas de 45 a 45,5 px, todas las celdas en 1 renglón, persona y opción con `ellipsis` (la persona de 60 caracteres desborda 255 px y la opción de 40, 151 px), «Ver» de x 756,8 en las tres filas, «Ver», «Reimprimir» y «Recuperar» de 44 px y 14 px de letra, «Recuperar» termina en 968,4 con la celda en 974; tabla de 924 px en una caja de 924 px; el globo del mouse empieza en x 218,99 como su celda y arriba en 425 con la celda abajo en 421. Capturas `consumos-un-renglon-1024x768` y `consumos-globo-1024x768`. La persona larga se crea por Personas con límite 2 de ALMUERZO, y las opciones se renombran por Opciones, porque el catálogo de quince no trae «Almuerzo con gaseosa».
- [x] 3.2 D1, D2 y D3 en `wwwroot/css/puesto.css` (`.tabla-consumos`, `--letra-tabla-consumos` y los botones de la
  fila). Verifica: 3.1 en verde y `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde.
  Hecho: `table-layout: fixed`, `white-space: nowrap`, el corte con `ellipsis`, los anchos en `ch` y las acciones en `em` (ajuste al aplicar en D1: relleno de 4 px), `--letra-tabla-consumos` y los botones de la fila con 8 px de relleno y 4 px entre ellos. 3.1 en verde y Kiosk.Tests 609 de 609.
- [x] 3.3 D2: correr 3.1 con 16, 15 y 14 px y dejar la mayor que cumple. Verifica: las tres medidas pegadas y el valor
  elegido en `--letra-tabla-consumos`.
  Hecho, con el criterio de D2 medido como «Persona sintética…» y «Almuerzo con…» con su elipsis: 16 px, persona y opción a la vista 83 px para 130,1 y 109,5 px pedidos (rojo); 15 px, 102 px para 122 y 102,7 (rojo); 14 px, 122 px para 113,8 y 95,8 (verde). Queda `--letra-tabla-consumos: 14px`.

## 4. Documentación y verificación completa

- [x] 4.1 Volver a tomar `docs/capturas/15-consumos-del-dia.jpg` a 1024 × 768 con la captura de 3.1, como JPEG a la
  escala CSS, igual que las otras de `docs/capturas`. Verifica: la captura nueva mirada, con cada fila en un renglón.
  Hecho: `docs/capturas/15-consumos-del-dia.jpg` rehecha por el mismo E2E como JPEG a la escala CSS (1024 × 768), mirada: tres filas en un renglón, la persona y la opción largas con «…», ningún código. El texto alternativo del `readme.md` la describe.
- [x] 4.2 La verificación completa: `dotnet build INComedor.sln -warnaserror`, `dotnet test INComedor.sln` y
  `openspec validate --all --strict`, sin errores. Verifica: las tres salidas pegadas, con el total de pruebas.
  Hecho con la corrida única del final (§6.6 del plan del goal), el 11/11/2026 de 21:46 a 21:52, sobre `afc4b87`, con `INCOMEDOR_SQLSERVER=INMO-DW-AR`: `dotnet build INComedor.sln -warnaserror` «0 Errores» (build exit 0); `dotnet test INComedor.sln` exit 0, «Resumen de la serie de pruebas: Correcta! · total: 1215 · error: 0 · correcto: 1215 · omitido: 0» (Contracts 23, Domain 91, Sync.Api 62, Infrastructure.Sqlite 329, Kiosk 661 y E2E 49 en 5 min 27 s); `openspec validate --all --strict` «Totals: 25 passed, 0 failed (25 items)». Las pruebas existentes modificadas son sólo las tres aprobadas por David (`git diff --diff-filter=M --name-only d6bbb24 -- tests/`).
- [x] 4.3 La evidencia: la fila nueva o actualizada en `docs/aplicacion-del-curso.md` (el pedido de David medido por CDP,
  con su gate respondido) y, si deja un workflow o un ajuste humano nuevo, su entrada en `prompts.md`. Verifica: el
  chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso`, con su control negativo, `enlaces rotos: 0`.
  Hecho: la fila «Contexto y reglas por artefacto en `openspec/config.yaml`» de `docs/aplicacion-del-curso.md` suma el pedido medido por CDP con su gate. No deja un workflow ni un ajuste humano nuevo para `prompts.md`. Chequeo de D8 sobre `docs/aplicacion-del-curso.md`, `readme.md` y `prompts.md`: `enlaces rotos: 0`; control negativo `ROTO`, `ANCLA` y `enlaces rotos: 2`.

## Workflow follow-up

- `/openspec-verify-change ver-consumos-en-un-renglon` y su salida en la descripción del PR; el archivo del change en el
  mismo PR. Push, PR y merge a `final-project-dw` son de David.
