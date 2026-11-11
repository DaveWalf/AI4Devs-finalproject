## Context

Las opciones se editan en `PanelOpciones` y se guardan con `AdministracionLocal.GuardarOpciones`, que valida (nombre de 1 a
60, servicio existente, importe de 0 a 9.999.999,99, al menos una carga de 1 a 9 unidades, franjas HH:mm no vacías) y
crea una sola versión con un `CONFIGURATION_CHANGED` por opción; quitar una opción la saca del catálogo activo sin tocar
sus consumos. El 2b dejó `LectorPlanilla` (con las celdas numéricas marcadas), `EscritorPlanilla.EscribirLibro`,
`IElectorArchivo` y el cartel del resumen de una importación; el 3a, el catálogo a la vista en la pestaña Opciones.

## Goals / Non-Goals

**Goals:** sacar el catálogo a un `.xlsx` que se pueda volver a importar; una planilla base; importar reemplazando todo,
con un resumen antes de aplicar; todo o nada; la bitácora.

**Non-Goals:** otro modo que reemplazar; la presentación de cada opción (3a); CSV.

## Decisions

### D1. Las columnas

La hoja «Opciones» tiene «Nombre», «Servicio», «Importe», «Franjas» y una columna «<CATEGORÍA>: unidades» por cada
categoría de cupo del catálogo. El encabezado se reconoce como en Personas (sin distinguir mayúsculas, espacios de más ni
tildes); falta una de las cuatro fijas, o sobra o se repite una columna, es un error del archivo, nombrada por su letra.
Una columna de categoría que falta vale como cero unidades: el archivo reemplaza.

- **Descartada: una columna de cargas en texto («ALMUERZO × 1»).** Una columna por categoría se completa con un número y
  la valida Excel; el texto obligaría a escribir el nombre exacto de la categoría.

### D2. Quién es quién

Una fila se reconoce por su nombre, como en D1. La que coincide con una opción del catálogo la modifica (conserva su id y
su presentación); la que no, la agrega; la opción que no está en el archivo se quita. Un nombre repetido en el archivo es
error de las dos filas. Una opción igual en todo queda sin cambios y no entra en la versión.

- **Descartada: reconocer por un id en una columna oculta.** Sobrevive a un cambio de nombre, pero una planilla base o
  una hecha a mano no lo tienen, y una columna oculta se borra sin querer.

### D3. Los valores

El importe es un número de Excel o un texto con coma decimal («1.800,00»), en pesos, con hasta los decimales de la escala
del catálogo. Las unidades, enteros de 0 a 9 (cero o vacío: sin carga en esa categoría), y al menos una carga por opción.
Las franjas, «HH:mm a HH:mm» separadas por punto y coma, vacías para todo el día. Cada error es de su fila y su columna,
con el motivo de `GuardarOpciones` (`REQUIRED`, `UNKNOWN`, `OUT_OF_RANGE`, `INVALID_TIME`, `EMPTY_RANGE`, `REPEATED`) o
`NOT_DIGITS` para un número que no es número.

- **Descartada: aceptar el importe con punto decimal en texto.** «1.800» sería mil ochocientos o uno coma ocho; con la
  coma de es-AR no hay duda, y un número de Excel no la necesita.

### D4. El resumen y aplicar

`ImportacionOpciones.Revisar` devuelve los errores o las agregadas, modificadas, sin cambios y quitadas, con las
ediciones listas para `GuardarOpciones`. El cartel del resumen ofrece «Reemplazar» y «Cancelar»; con errores, los lista
fila por fila con «Cerrar». Aplicar guarda en una sola versión con el actor; si algo cambió desde el resumen, el guardado
lo rechaza como siempre. Sin nada que cambiar no crea versión. Un archivo sin opciones es un error: la atención quedaría
sin nada que ofrecer.

- **Descartada: aplicar sin resumen.** Reemplazar quita opciones; el resumen dice cuántas antes de tocar nada.

### D5. Exportar y la planilla base

«Exportar Excel» escribe `INComedor-opciones-AAAAMMDD-HHmmss.xlsx` en la carpeta de la exportación, con el catálogo en el
formato de D1 (el importe como número) y la hoja «Instrucciones»; «Planilla base», `INComedor-planilla-opciones-…`,
con las mismas columnas, dos filas de ejemplo con el primer servicio y la primera categoría, y las instrucciones.
Exportar queda en la general como `MENU_OPTIONS_EXPORTED`, con el archivo sin ruta y las filas.

- **Descartada: que la planilla base sea el catálogo exportado.** La mejora pide una planilla para empezar; el catálogo
  ya sale con «Exportar Excel».

### D6. La pantalla y la bitácora

Los tres botones van en el pie de Opciones, como los de Personas, con la pestaña elegida y sin la edición abierta;
«Importar Excel» no se ofrece con INWeb como dueño. Cada opción cambiada deja su `CONFIGURATION_CHANGED`; la importación,
`MENU_OPTIONS_IMPORTED` con `REPLACE`, la huella del archivo, la versión, los totales y cada opción con su id y lo que
pasó (`ADDED`, `UPDATED`, `UNCHANGED`, `REMOVED`), o `MENU_OPTIONS_IMPORT_REJECTED` con los errores por motivo.

- **Descartada: reusar `PEOPLE_IMPORTED`.** Otro evento deja buscar en la general qué se importó sin leer su contexto.

### D7. Prolijidad

Los tres botones del pie miden lo mismo, con el alto y la letra de «Cerrar»; los del resumen, el alto táctil. Medido por
CDP a 1024 × 768, con capturas.

- **Descartada: los botones en la pestaña, sobre el catálogo.** Le quitarían alto al catálogo; el pie ya tiene lugar.

## Risks / Trade-offs

- [Un cambio de nombre en la planilla quita una opción y agrega otra] → la nueva no tiene la presentación de la anterior,
  y el resumen lo cuenta como una quitada y una agregada antes de aplicar; las instrucciones lo dicen.
- [Un catálogo grande] → los topes de `LectorPlanilla`.

## Migration Plan

Sin migración. La reversión es revertir el PR.

## Open Questions

Ninguna.
