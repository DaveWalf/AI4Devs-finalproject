# Tasks

Rama `feature/ordenar-configuracion-dw`, cortada con `--no-track` de `origin/final-project-dw` en `86ae3b3`. Gate dado
por la decisión 13 del plan del cierre final. Cada grupo cierra con `git diff --stat 86ae3b3 -- tests/` y
`git diff --diff-filter=M --name-only 86ae3b3 -- tests/` pegados, para ver que sólo cambian las pruebas existentes de D6.
Las pruebas de base y receptor corren con `INCOMEDOR_SQLSERVER=INMO-DW-AR`.

## 1. La regla de prolijidad y el gate

- [ ] 1.1 Agregar a `openspec/config.yaml` la regla de prolijidad visual de D5, en `rules.design` y `rules.tasks`, y el
  registro del gate en `proposal.md`. Verifica: `openspec validate --all --strict` sin errores y
  `openspec instructions design --change ordenar-configuracion --json` que muestra la regla.

## 2. El orden de las pestañas y «Cerrar»

- [ ] 2.1 Pruebas nuevas, vistas fallar antes: `OrdenConfiguracionTests` en `Kiosk.Tests`: abre en Consumos con las nueve
  en el orden de D1; con la flecha derecha, Fin e Inicio; al tocar General, el modo, las bitácoras y «Cerrar el puesto»;
  el pie dice «Cerrar» y ningún botón dice «Cancelar», y «Cerrar» descarta la opacidad no guardada. Verifica:
  `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter-class *OrdenConfiguracionTests`, rojo antes y verde después.
- [ ] 2.2 Reordenar el `enum Pestana`, abrir en la primera visible y cambiar el texto del pie (D1 y D2). Verifica: 2.1 en
  verde.
- [ ] 2.3 Ajustar las pruebas existentes de D6 que leen General sin tocarla o listan el orden, y los `DisplayName` de
  los escenarios renombrados, cada una vista fallar antes del ajuste. Verifica: `dotnet test
  tests/Intelektron.INComedor.Kiosk.Tests` en verde y la lista de archivos de pruebas existentes cambiados, pegada.

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
