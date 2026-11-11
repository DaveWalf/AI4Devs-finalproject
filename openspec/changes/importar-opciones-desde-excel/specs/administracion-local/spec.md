## ADDED Requirements

### Requirement: Exportar Opciones y la planilla base

Opciones SHALL exportar el catálogo activo a un `.xlsx`, en la carpeta de la exportación, con las columnas que se importan (Nombre, Servicio, Importe, Franjas y las unidades de cada categoría de cupo), y descargar una planilla base con esas columnas, dos filas de ejemplo y una hoja «Instrucciones». Exportar MUST quedar en la general como `MENU_OPTIONS_EXPORTED`.

#### Scenario: Exportar el catálogo
Origen: mejora 15 de David del 10/11/2026 («Opciones exporta e importa Excel»).
- **GIVEN** el catálogo de la demostración y una carpeta de exportación elegida
- **WHEN** se toca «Exportar Excel» en Opciones
- **THEN** la carpeta tiene un `.xlsx` con una fila por opción y la general tiene `MENU_OPTIONS_EXPORTED` con sus filas

#### Scenario: Descargar la planilla base
Origen: mejora 15 de David del 10/11/2026 («con la descarga de una planilla base»).
- **GIVEN** una carpeta de exportación elegida
- **WHEN** se toca «Planilla base»
- **THEN** la carpeta tiene un `.xlsx` con las hojas «Opciones» e «Instrucciones», el encabezado de D1 y dos filas de ejemplo

#### Scenario: Sin carpeta
Origen: «Exportar Personas a CSV y a Excel» (sin carpeta, se avisa), borde.
- **GIVEN** ninguna carpeta de exportación guardada
- **WHEN** se toca «Exportar Excel» y se cancela la elección
- **THEN** el pie avisa que no se eligió una carpeta y no se escribe nada

### Requirement: Importar Opciones desde Excel

Opciones SHALL importar un `.xlsx` con las columnas de la planilla base y reemplazar con él el catálogo: una opción se reconoce por su nombre, la que coincide se modifica, la nueva se agrega y la que no está se quita sin tocar sus consumos. Antes de aplicar MUST mostrar el resumen con «Reemplazar» y «Cancelar»; una fila con error o un archivo sin opciones MUST frenar todo.

#### Scenario: Reemplazar el catálogo
Origen: mejora 15 de David del 10/11/2026 («lo importado reemplaza todas las opciones vigentes»).
- **GIVEN** el catálogo exportado, con el importe de una opción cambiado, una fila nueva y otra borrada
- **WHEN** se importa y se toca «Reemplazar»
- **THEN** la versión siguiente está activa con una opción modificada, una agregada y una quitada, y la quitada no se ofrece pero sus consumos siguen registrados

#### Scenario: Cancelar el resumen de Opciones
Origen: mejora 15 de David del 10/11/2026, borde.
- **GIVEN** el resumen de una importación sin errores
- **WHEN** se toca «Cancelar»
- **THEN** la versión activa no cambia y la general no tiene `MENU_OPTIONS_IMPORTED`

#### Scenario: Una fila de Opciones con error
Origen: «Opción inválida» de este requisito vivo (todo o nada), borde.
- **GIVEN** un archivo con una fila sin cargas, otra con un servicio desconocido y otra con la franja «11 a 14»
- **WHEN** se importa
- **THEN** el resumen señala las tres filas con su campo y su motivo, no deja aplicar y la versión activa no cambia

#### Scenario: Un archivo de Opciones vacío o que no es una planilla
Origen: `AGENTS.md` (OWASP: la entrada externa), borde.
- **GIVEN** un archivo dañado, uno sin la columna «Importe» y uno sin ninguna opción
- **WHEN** se importa cada uno
- **THEN** el resumen dice qué falta o qué está mal y no se aplica nada

### Requirement: La importación de Opciones queda en la bitácora

Cada importación aplicada SHALL dejar un `CONFIGURATION_CHANGED` por opción cambiada y un `MENU_OPTIONS_IMPORTED` con `REPLACE`, la huella SHA-256 del archivo, los totales y, por opción, su id y lo que pasó. Una rechazada MUST dejar `MENU_OPTIONS_IMPORT_REJECTED` con los errores por motivo.

#### Scenario: El resumen opción por opción
Origen: RB21 («todo cambio … tiene permiso, actor y resultado»).
- **GIVEN** una importación con una opción modificada, una agregada y una quitada
- **WHEN** se aplica
- **THEN** la general tiene `MENU_OPTIONS_IMPORTED` con 1 agregada, 1 modificada, 1 quitada y una entrada por opción con su id

#### Scenario: Una importación de Opciones rechazada
Origen: RB21, borde.
- **GIVEN** un archivo con una fila sin nombre
- **WHEN** se importa
- **THEN** la general tiene `MENU_OPTIONS_IMPORT_REJECTED` con un error `REQUIRED` y ningún `MENU_OPTIONS_IMPORTED`
