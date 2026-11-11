# Tasks

Rama local `feature/mejoras-2-3-dw`, en el worktree `C:\AI\INComedor\INComedor-AI4DEVS-mejoras`, desde `0af7359`. Antes
de aplicar se rebasa sobre la punta que deje el goal de `reimprimir-sin-codigo`. Gate respondido por David el 11/11/2026
(proposal). Cada grupo cierra con `git diff --diff-filter=M --name-only <base> -- tests/` pegado, que tiene que salir
vacío: este change no prevé tocar pruebas existentes. Cada tarea que toca una pantalla sigue la regla de prolijidad de
`config.yaml`.

## 1. Las pruebas existentes

- [ ] 1.1 Confirmar que ninguna prueba existente cambia, porque el corte es de CSS y el texto de las celdas sigue entero.
  Verifica: `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde después del grupo 3, y el
  `git diff --diff-filter=M` del encabezado vacío. Si alguna prueba existente se pone roja, se para y se le pide a
  David la aprobación de esa prueba.

## 2. El globo del dato completo

- [ ] 2.1 Pruebas nuevas en `Kiosk.Tests` (`GloboDeConsumosTests`), vistas fallar antes, una por escenario: tocar la
  persona abre `#globo-dato` con el nombre entero y otro toque lo cierra; el mouse medio segundo sobre la hora lo abre
  con «11/11/2026 13:51» y salir lo cierra (con `TimeProvider` falso); a los 5 s se cierra solo; con la tabla consultada
  por `0001`, el globo no contiene `0001`. Verifica: el filtro de la clase, en rojo antes y en verde después.
- [ ] 2.2 D4 en `PestanaConsumos.razor`: los eventos de las tres celdas, el globo único y su cierre. Verifica: 2.1 en
  verde.

## 3. Un renglón por consumo

- [ ] 3.1 La prueba E2E nueva de D5 (`ConsumosEnUnRenglonE2ETests`), vista fallar antes contra la hoja de estilos de
  hoy. Crea por la pestaña Personas una persona sintética de 60 caracteres y otra «Persona sintética 01», con opciones
  de 40 caracteres y «Almuerzo con gaseosa», un original fallido y los consumos del escenario. Mide por CDP a
  1024 × 768 el alto de cada fila (igual al de una celda de un renglón, con tolerancia de 1 px), la letra de la tabla
  (entre 14 y 16 px), que la persona y la opción terminan en «…», y los bordes izquierdos, el alto y la letra de «Ver»,
  «Reimprimir» y «Recuperar». Deja la captura `consumos-un-renglon-1024x768`. Verifica:
  `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter-class *ConsumosEnUnRenglon*`, en rojo antes y en verde
  después, corrida en primer plano y con las medidas pegadas de `registro.txt`.
- [ ] 3.2 D1, D2 y D3 en `wwwroot/css/puesto.css` (`.tabla-consumos`, `--letra-tabla-consumos` y los botones de la
  fila). Verifica: 3.1 en verde y `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde.
- [ ] 3.3 D2: correr 3.1 con 16, 15 y 14 px y dejar la mayor que cumple. Verifica: las tres medidas pegadas y el valor
  elegido en `--letra-tabla-consumos`.

## 4. Documentación y verificación completa

- [ ] 4.1 Volver a tomar `docs/capturas/15-consumos-del-dia.jpg` a 1024 × 768 con la captura de 3.1, como JPEG a la
  escala CSS, igual que las otras de `docs/capturas`. Verifica: la captura nueva mirada, con cada fila en un renglón.
- [ ] 4.2 La verificación completa: `dotnet build INComedor.sln -warnaserror`, `dotnet test INComedor.sln` y
  `openspec validate --all --strict`, sin errores. Verifica: las tres salidas pegadas, con el total de pruebas.
- [ ] 4.3 La evidencia: la fila nueva o actualizada en `docs/aplicacion-del-curso.md` (el pedido de David medido por CDP,
  con su gate respondido) y, si deja un workflow o un ajuste humano nuevo, su entrada en `prompts.md`. Verifica: el
  chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso`, con su control negativo, `enlaces rotos: 0`.

## Workflow follow-up

- `/openspec-verify-change ver-consumos-en-un-renglon` y su salida en la descripción del PR; el archivo del change en el
  mismo PR. Push, PR y merge a `final-project-dw` son de David.
