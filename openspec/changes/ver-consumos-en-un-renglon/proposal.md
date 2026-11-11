# Proposal

## Why

El 11/11/2026 a las 16:40 David pidió, textual: «La ventana de consumos debe facilitar ver toda la información en un mismo
renglón: ver cómo modificar los tamaños para permitirlo en pantalla.» Hoy, a 1024 × 768, la tabla de Consumos parte en
dos renglones la fecha y la hora, la persona, la opción y el cargo (captura `docs/capturas/15-consumos-del-dia.jpg`).
Las columnas y los botones piden unos 1.180 px a 16 px de letra, y la tabla tiene unos 924 px útiles.

Origen: historia HU03.2 (consultar el estado del puesto), AT45 («estados y referencias separados») y RF17; ticket
TK02.1 (interfaz).

## Gate respondido (David, 11/11/2026)

1. **Qué cede**: ninguna celda se parte; la persona y la opción se cortan con «…», la fecha y la hora quedan como
   «11/11 13:51» y los botones van más compactos. Además, textual: «un tooltip con la información completa al tocar
   cualquiera de esos datos o ante la permanencia de un eventual mouse sobre ellos».
2. **La letra de la tabla** puede bajar **hasta 14 px**, si la medición lo pide.
3. La mejora entra en `final-project-dw` antes del Typeform del 12/11/2026 (decisión de las 17:55).

## What Changes

- **Un renglón por consumo**: ninguna celda de la tabla se parte en dos renglones, a 1024 × 768 o más.
- **«…» en la persona y la opción**: el texto que no entra en su columna se corta con «…»; los datos guardados, la
  búsqueda, el orden y la exportación no cambian.
- **Un globo con el dato completo**: tocar la fecha y hora, la persona o la opción de una fila, o dejar el mouse encima
  medio segundo, muestra el texto entero de ese dato (la fecha con el año). Se cierra con otro toque, al salir el mouse
  o a los 5 s.
- **Letra de 14 a 16 px**: la tabla usa la mayor de ese rango con la que el renglón de prueba entra, medida por CDP.
- **Botones de la fila más compactos**, con el alto táctil de 44 px.

## Non-goals

- No cambian las columnas ni su orden, los filtros, la búsqueda, el orden por columna ni la exportación.
- No cambian «Ver», «Reimprimir» ni «Recuperar»: lo que hacen es de `reimprimir-sin-codigo`, que este change no toca.
- No cambia el ancho del diálogo de Configuración ni el resto de sus pestañas.
- Las otras mejoras del 11/11 (la versión del programa, las líneas del comprobante, el operador desde reposo y el consumo
  excepcional) van en sus propios changes.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `consulta-consumos`: se suma el requisito «Cada consumo en un renglón», con el corte con «…» y el globo del dato
  completo. Los requisitos existentes de la tabla no cambian, porque los datos que muestra siguen siendo los mismos.

## Impact

- Código: `PestanaConsumos.razor` (las celdas con el globo) y `wwwroot/css/puesto.css` (`.tabla-consumos`: letra, ancho
  de columnas, `white-space: nowrap`, el corte con «…» y los botones de la fila).
- Pruebas existentes que el cambio exige tocar: ninguna prevista. Las de la tabla leen el texto de las celdas, que sigue
  entero en el DOM porque el corte es de CSS. La medición por CDP va en una prueba E2E nueva. La lista se confirma con
  `git diff --diff-filter=M` en cada grupo de `tasks.md`.
- Documentación: la captura `docs/capturas/15-consumos-del-dia.jpg` vuelve a tomarse, y `docs/aplicacion-del-curso.md`
  lleva la evidencia.
