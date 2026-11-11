## ADDED Requirements

### Requirement: La planilla de ejemplo de Personas

Personas SHALL descargar, en la carpeta de la exportación, una planilla de ejemplo `.xlsx` con la hoja «Personas» (Nombre, Código y el límite de cada cupo del catálogo, con dos filas de ejemplo sintéticas y el código como texto) y la hoja «Instrucciones». MUST NOT contener ningún código de una persona del puesto.

#### Scenario: Descargar la planilla de ejemplo
Origen: mejora 14 de David del 10/11/2026 («descargar un Excel de ejemplo claro y agradable de completar»).
- **GIVEN** una carpeta de exportación ya elegida
- **WHEN** se toca «Planilla de ejemplo» en Personas
- **THEN** la carpeta tiene un `.xlsx` con las hojas «Personas» e «Instrucciones», y el encabezado de «Personas» es «Nombre», «Código» y «<cupo>: límite» por cada cupo
- **AND** la columna «Código» tiene formato de texto y ningún código de la base de la demostración

#### Scenario: Sin carpeta para la planilla
Origen: «Exportar Personas a CSV y a Excel» (sin carpeta, se avisa), borde.
- **GIVEN** ninguna carpeta de exportación guardada
- **WHEN** se toca «Planilla de ejemplo» y se cancela la elección
- **THEN** el pie avisa que no se eligió una carpeta y no se escribe nada

### Requirement: Importar Personas desde Excel

Personas SHALL importar un `.xlsx` con las columnas de la planilla de ejemplo: cada fila se reconoce por su código; uno sin revocar de una persona activa la modifica, aunque esté vencido, y uno nuevo la agrega. Antes de aplicar MUST mostrar el resumen; «Modificar y agregar» deja a las demás como estaban y «Reemplazar» da de baja a las activas que no están, salvo quien tenga un permiso de operador o de administrador. Una fila con error MUST frenar todo. Lo usado hoy no cambia.

#### Scenario: Modificar y agregar
Origen: mejora 14 de David del 10/11/2026 («modifique y agregue»).
- **GIVEN** un archivo con 0001 y REFRIGERIO 1, y una persona nueva con el código 0105
- **WHEN** se importa y se toca «Modificar y agregar»
- **THEN** la versión 2 está activa, 0001 tiene REFRIGERIO 1, «Persona sintética 05» está agregada y las demás personas siguen como estaban

#### Scenario: Reemplazar conserva a los operadores
Origen: mejora 14 de David del 10/11/2026 («reemplace lo actual») y RB26 (los operadores atienden la asistencia), borde.
- **GIVEN** un archivo sólo con 0001
- **WHEN** se importa y se toca «Reemplazar»
- **THEN** las personas activas sin permiso que no están en el archivo quedan de baja, y los operadores y el administrador siguen activos

#### Scenario: Los consumos de hoy siguen contando
Origen: mejora 14 de David del 10/11/2026 («aclarar si los consumos habilitados afectan los ya registrados … en la misma jornada») y RB03.
- **GIVEN** 0001 con un almuerzo consumido hoy
- **WHEN** se importa un archivo que le deja ALMUERZO 1
- **THEN** su fila dice «1/1» y su oferta ya no incluye almuerzos hoy

#### Scenario: Una fila con error no aplica nada
Origen: AT37 («conservación del valor previo ante error»), borde.
- **GIVEN** un archivo con una fila sin código y otra con el código de una persona dada de baja
- **WHEN** se importa
- **THEN** el resumen señala las dos filas con su motivo, no deja aplicar y la versión activa no cambia

#### Scenario: Un código que Excel guardó como número
Origen: verificación del change («Reemplazar» no se deshace y un código sin sus ceros cuenta como nuevo), borde.
- **GIVEN** un archivo con una fila de la planilla de ejemplo y un código guardado como número
- **WHEN** se importa
- **THEN** el resumen avisa las dos cosas antes de aplicar, y «Reemplazar» dice que la baja revoca los códigos sin vuelta atrás
- **AND** un código de más de 15 dígitos guardado como número es un error de su fila

#### Scenario: Cancelar el resumen
Origen: mejora 14 de David del 10/11/2026 («según se configure»), borde.
- **GIVEN** el resumen de una importación sin errores
- **WHEN** se toca «Cancelar»
- **THEN** la versión activa no cambia y la general no tiene `PEOPLE_IMPORTED`

#### Scenario: Un archivo que no es una planilla
Origen: `AGENTS.md` (OWASP: la entrada externa), borde.
- **GIVEN** un archivo `.xlsx` dañado, sin la columna «Código», con una columna desconocida o sin ninguna persona
- **WHEN** se importa
- **THEN** el resumen lista qué falta o qué sobra, sin mostrar el título de una columna que sobra, y no deja aplicar nada

### Requirement: La importación queda en la bitácora

Cada importación aplicada SHALL dejar un `CONFIGURATION_CHANGED` por persona cambiada, como un guardado a mano, y un `PEOPLE_IMPORTED` en la general con el modo, la huella SHA-256 del archivo, los totales y, por persona, su `PersonId` y lo que pasó. Una rechazada MUST dejar `PEOPLE_IMPORT_REJECTED` con los errores por motivo. Ninguno MUST contener nombres ni códigos.

#### Scenario: El resumen persona por persona
Origen: mejora 14 de David del 10/11/2026 («bitácora de lo decidido y del resumen persona por persona»).
- **GIVEN** una importación de 0001 modificada y 0105 agregada con «Modificar y agregar»
- **WHEN** se aplica
- **THEN** la general tiene `PEOPLE_IMPORTED` con `MERGE`, 1 agregada, 1 modificada, y una entrada por persona con su `PersonId`, sin nombres ni códigos

#### Scenario: Una importación rechazada
Origen: RB21 («todo cambio … tiene permiso, actor y resultado»), borde.
- **GIVEN** un archivo con una fila sin código
- **WHEN** se importa
- **THEN** la general tiene `PEOPLE_IMPORT_REJECTED` con un error `REQUIRED` y ningún `PEOPLE_IMPORTED`
