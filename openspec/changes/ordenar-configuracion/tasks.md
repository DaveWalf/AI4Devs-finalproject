# Tasks

Rama `feature/ordenar-configuracion-dw`, cortada con `--no-track` de `origin/final-project-dw` en `86ae3b3`. Gate dado
por la decisión 13 del plan del cierre final. Cada grupo cierra con `git diff --stat 86ae3b3 -- tests/` y
`git diff --diff-filter=M --name-only 86ae3b3 -- tests/` pegados, para ver que sólo cambian las pruebas existentes de D6.
Las pruebas de base y receptor corren con `INCOMEDOR_SQLSERVER=INMO-DW-AR`.

## 1. La regla de prolijidad y el gate

- [x] 1.1 Agregar a `openspec/config.yaml` la regla de prolijidad visual de D5, en `rules.design` y `rules.tasks`, y el
  registro del gate en `proposal.md`. Verifica: `openspec validate --all --strict` sin errores y
  `openspec instructions design --change ordenar-configuracion --json` que muestra la regla.
  - Hecho el 10/11: `openspec validate --all --strict` → `Totals: 16 passed, 0 failed (16 items)`;
    `openspec instructions design … --json` → rules `['Cada decisión nombra la alternativa descartada y ', 'Prolijidad
    visual: los controles hermanos de una m']`; `instructions tasks` → la cuarta regla es «Prolijidad visual: toda tarea
    que toque ». El gate quedó escrito en «Decisión del gate» de `proposal.md`.

## 2. El orden de las pestañas y «Cerrar»

- [x] 2.1 Pruebas nuevas, vistas fallar antes: `OrdenConfiguracionTests` en `Kiosk.Tests`: abre en Consumos con las nueve
  en el orden de D1; con la flecha derecha, Fin e Inicio; al tocar General, el modo, las bitácoras y «Cerrar el puesto»;
  el pie dice «Cerrar» y ningún botón dice «Cancelar», y «Cerrar» descarta la opacidad no guardada. Verifica:
  `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter-class *OrdenConfiguracionTests`, rojo antes y verde después.
  - Antes de 2.2, cuatro casos: tres rojos (`Expected: "Consumos"` / `Actual: "General"`; `Expected: "true"` /
    `Actual: "false"` con Fin; `Expected: "Cerrar"` / `Actual: "Cancelar"`) y uno verde, el de General con su contenido,
    que ya pasaba y se reforzó con la afirmación de que General es la última pestaña. Después de 2.3 quedaron en la clase
    los dos que no repite la prueba existente ajustada: el teclado con Fin y «Cerrar»; el orden y la apertura en Consumos
    los afirma `PestanasConfiguracionTests.Abre_en_general_con_las_nueve_pestanas`.
- [x] 2.2 Reordenar el `enum Pestana`, abrir en la primera visible y cambiar el texto del pie (D1 y D2). Verifica: 2.1 en
  verde.
- [x] 2.3 Ajustar las pruebas existentes de D6 que leen General sin tocarla o listan el orden, y los `DisplayName` de
  los escenarios renombrados, cada una vista fallar antes del ajuste. Verifica: `dotnet test
  tests/Intelektron.INComedor.Kiosk.Tests` en verde y la lista de archivos de pruebas existentes cambiados, pegada.
  - `Kiosk.Tests` entero después de 2.2: `total: 387`, `error: 2`, `correcto: 385`. Las dos rojas, las únicas de D6 en
    este grupo: `PestanasConfiguracionTests.Abre_en_general_con_las_nueve_pestanas` (el orden viejo) y
    `Las_pestanas_con_el_teclado` (desde General). Las demás que leen General pasan, porque todas las pestañas quedan
    montadas. Ajustadas: el orden nuevo, Consumos elegida y visible, y el toque de General antes de leerla; el teclado
    desde Consumos, con el `DisplayName` «… e Inicio vuelve a Consumos …». Después: las dos clases, `total: 13`,
    `error: 0`. `git diff --diff-filter=M --name-only 86ae3b3 -- tests/` →
    `tests/Intelektron.INComedor.Kiosk.Tests/PestanasConfiguracionTests.cs` (7 inserciones y 5 borrados).

## 3. La lista desplegable propia y el combo alineado

- [ ] 3.1 Pruebas nuevas, vistas fallar antes: `ListaDesplegableTests` en `Kiosk.Tests`: abre con un toque, una fila
  `role="option"` por entrada, elegir cierra y aplica, Escape y el toque afuera cierran sin elegir, Enter, flechas,
  Inicio y Fin, y el foco vuelve al botón. Verifica: `dotnet test tests/Intelektron.INComedor.Kiosk.Tests
  --filter-class *ListaDesplegableTests`, rojo antes y verde después.
- [ ] 3.2 `ListaDesplegable.razor` y su CSS (D3), con filas de 56 px y la lista dentro de la ventana. Verifica: 3.1 en
  verde.
- [ ] 3.3 El combo de la simulación y los tres filtros de Consumos pasan a `ListaDesplegable`, con los mismos id y valores;
  la prueba nueva «Ningún select nativo» en la pantalla de identificación y en Consumos, vista fallar antes. Verifica:
  `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde.
- [ ] 3.4 El combo con las medidas del campo (D4). Prueba E2E nueva en `SimulacionE2ETests` o una clase nueva: a
  1024 × 768, bordes izquierdo y derecho del combo contra el campo e «Ingresar» (1 px de tolerancia), letra y relleno, y
  la lista abierta entera dentro de la ventana, debajo del combo, con filas de 56 px; captura. Vista fallar contra el EXE
  de `86ae3b3`. Verifica: la prueba en verde contra el puesto compilado de la rama, con las medidas pegadas.
- [ ] 3.5 Ajustar las pruebas existentes de D6 que eligen en un `<select>` (el combo y los filtros de Consumos), vistas
  fallar antes. Verifica: `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` y `dotnet test
  tests/Intelektron.INComedor.E2E.Tests` en verde, y la lista de archivos cambiados pegada.

## 4. El puesto compilado, documentos y verificación integral

- [ ] 4.1 Las pruebas E2E existentes del orden de las pestañas y del operador contra el puesto compilado de la rama.
  Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests` en verde.
- [ ] 4.2 `readme.md` donde nombra «Cancelar» de Configuración o el orden de las pestañas; la fila de la regla de
  prolijidad en `docs/aplicacion-del-curso.md` y el ajuste humano en `prompts.md` (§5). Verifica: el chequeo de enlaces de
  D8 de `mostrar-aplicacion-del-curso` con su control negativo, `enlaces rotos: 0`.
- [ ] 4.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror`, el
  puesto publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c Release` contra ese
  EXE, con los totales por proyecto pegados; `git diff --stat 86ae3b3 -- tests/` y la lista de pruebas existentes
  cambiadas, contra D6.
