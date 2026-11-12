## ADDED Requirements

### Requirement: Los operadores en Personas

La tabla de Personas SHALL mostrar, en la columna «Operador», después de «Estado», un ícono de escudo con el texto accesible «Operador» en quien tiene un permiso `OPERATOR` vigente, y ordenarla como las demás. En edición, tocar esa celda MUST marcarla o desmarcarla; guardar MUST dar el permiso desde el guardado y sin fin, o revocarlo en la versión nueva sin borrar su fila. Un operador MUST NOT cambiar la suya.

#### Scenario: Ver quién es operador
Origen: mejora 7 de David del 11/11/2026 («un ícono que claramente distinga a las personas que son operadores») y su respuesta 3 al gate.
- **GIVEN** la base de la demostración, con `9001` vigente, `9002` vencido y `9003` revocado
- **WHEN** se abre la pestaña Personas
- **THEN** sólo la fila de «Operador sintético 01» tiene el escudo en «Operador», con el texto accesible «Operador», y ninguna fila muestra un código

#### Scenario: Marcar a una persona como operador
Origen: mejora 7 de David («que se pueda editar allí») y su respuesta 1 al gate, con RB26.
- **GIVEN** Configuración abierta con la clave del día, en Personas
- **WHEN** se toca «Editar», se marca «Operador» en «Persona sintética 01» y se guarda
- **THEN** la versión nueva tiene su permiso `OPERATOR` desde el guardado y sin fin, y en reposo, con `0001`, el engranaje abre Configuración como operador
- **AND** la general tiene el `CONFIGURATION_CHANGED` de esa persona con el permiso antes «No» y después «Sí», sin su código

#### Scenario: Desmarcar a un operador
Origen: respuesta 1 de David al gate del 11/11/2026 (quitarlo revoca en la versión nueva) y RB26 («revocación efectiva al activar el snapshot»), borde.
- **GIVEN** Configuración abierta con la clave del día, en Personas
- **WHEN** se desmarca «Operador» en «Operador sintético 01» y se guarda
- **THEN** la versión nueva tiene su permiso revocado en el instante del guardado, la versión anterior lo conserva como estaba, y el engranaje con `9001` dice que el permiso está revocado

#### Scenario: Un operador marca a otro, no a sí mismo
Origen: respuesta 2 de David al gate del 11/11/2026 («Operador, salvo a sí mismo»), borde: el control y el guardado.
- **GIVEN** Configuración abierta con `9001`, en Personas con «Editar»
- **WHEN** marca a «Persona sintética 01», guarda, y después se pide guardar su propio permiso desmarcado
- **THEN** «Persona sintética 01» queda operador con `9001` como actor, la celda «Operador» de su propia fila no se puede tocar, y el pedido de cambiar la suya se deniega sin crear versión

#### Scenario: Una persona de baja
Origen: «Quitar una persona es darla de baja» y RB26, borde: un permiso para quien no puede identificarse.
- **GIVEN** Personas en edición, con una persona dada de baja
- **WHEN** se revisa su celda «Operador»
- **THEN** no se puede marcar

### Requirement: El permiso de operador al importar Personas

Al importar Personas, la columna «Operador» con «Sí» SHALL dar el permiso `OPERATOR` desde la aplicación y sin fin, y con «No», revocarlo en la versión nueva; vacía o ausente MUST NOT cambiarlo. Otro valor MUST ser un error de su fila, que frena todo, como también una fila que cambie el permiso de quien importa siendo operador. El resumen y `PEOPLE_IMPORTED` MUST decir cuántos permisos se dan y se quitan, sin nombres ni códigos.

#### Scenario: Dar y quitar por planilla
Origen: mejora 7 de David del 11/11/2026 («que esto además se refleje en los archivos de […] importación») y su respuesta 4 al gate.
- **GIVEN** un archivo con `0001` y «Operador» en «Sí», y `9001` con «No»
- **WHEN** se importa y se toca «Modificar y agregar»
- **THEN** la versión nueva tiene a `0001` con el permiso y a `9001` revocado, y el resumen y `PEOPLE_IMPORTED` dicen 1 permiso dado y 1 quitado

#### Scenario: Una planilla de antes
Origen: respuesta 4 de David al gate («vacía o ausente no cambia nada»), borde: las planillas que ya circulan.
- **GIVEN** un archivo con la planilla de antes, sin la columna «Operador», con `9001` y su REFRIGERIO
- **WHEN** se importa y se aplica
- **THEN** `9001` sigue con su permiso, y la columna faltante no es un error

#### Scenario: Un valor que no se entiende
Origen: `AGENTS.md` (OWASP: la entrada externa) y la respuesta 4 de David al gate, borde.
- **GIVEN** un archivo con «Operador» en «Tal vez» en una fila, y en otra la fórmula `=1+1`
- **WHEN** se importa
- **THEN** el resumen señala las dos filas con su motivo, sin mostrar lo escrito, y no deja aplicar nada

#### Scenario: Un operador que se quita a sí mismo por planilla
Origen: respuesta 2 de David al gate del 11/11/2026 («salvo a sí mismo»), borde: la planilla no esquiva la regla.
- **GIVEN** Configuración abierta con `9001`, y un archivo con `9001` en «No»
- **WHEN** se importa
- **THEN** el resumen señala esa fila porque no puede cambiar su propio permiso, y no deja aplicar nada

## MODIFIED Requirements

### Requirement: Exportar Personas a CSV y a Excel

La tabla de Personas SHALL exportarse a un archivo CSV o Excel (`.xlsx`) en la carpeta de la exportación de Consumos, pidiéndola la primera vez, con la persona, el estado, lo usado hoy y el límite de cada cupo, las opciones habilitadas y, al final, si es operador («Sí» o «No»), sin ningún código. Cada exportación MUST quedar en la general como `PEOPLE_EXPORTED`, con el formato, las filas y el nombre del archivo, sin nombres ni códigos.

#### Scenario: Exportar Personas a CSV
Origen: mejora 5 de David del 10/11/2026 («y exportación»).
- **GIVEN** la base de la demostración y una carpeta de exportación ya elegida
- **WHEN** se toca «Exportar CSV» en Personas
- **THEN** la carpeta tiene un `.csv` con un encabezado y una fila por persona, con la columna «Operador» en «Sí» para «Operador sintético 01», sin ningún código, y la general tiene `PEOPLE_EXPORTED`

#### Scenario: Exportar Personas a Excel
Origen: mejora 5 de David del 10/11/2026 («y exportación»), borde.
- **GIVEN** la base de la demostración y una carpeta de exportación ya elegida
- **WHEN** se toca «Exportar Excel» en Personas
- **THEN** la carpeta tiene un `.xlsx` que se abre como libro con una hoja, y lo usado hoy y el límite son números

#### Scenario: Una carpeta que no se puede escribir
Origen: AT37 («conservación del valor previo ante error»), borde.
- **GIVEN** una carpeta de exportación guardada que ya no se puede escribir
- **WHEN** se toca «Exportar CSV» en Personas
- **THEN** el pie avisa que no se pudo exportar, la carpeta sigue vacía y la general no tiene `PEOPLE_EXPORTED`

### Requirement: La planilla de ejemplo de Personas

Personas SHALL descargar, en la carpeta de la exportación, una planilla de ejemplo `.xlsx` con la hoja «Personas» (Nombre, Código, Operador con «Sí» o «No» y el límite de cada cupo del catálogo, con dos filas de ejemplo sintéticas, la segunda operador, y el código como texto) y la hoja «Instrucciones». MUST NOT contener ningún código de una persona del puesto.

#### Scenario: Descargar la planilla de ejemplo
Origen: mejora 14 de David del 10/11/2026 («descargar un Excel de ejemplo claro y agradable de completar»).
- **GIVEN** una carpeta de exportación ya elegida
- **WHEN** se toca «Planilla de ejemplo» en Personas
- **THEN** la carpeta tiene un `.xlsx` con las hojas «Personas» e «Instrucciones», y el encabezado de «Personas» es «Nombre», «Código», «Operador» y «<cupo>: límite» por cada cupo, e «Instrucciones» explica «Sí», «No» y la celda vacía de «Operador»
- **AND** la columna «Código» tiene formato de texto y ningún código de la base de la demostración

#### Scenario: Sin carpeta para la planilla
Origen: «Exportar Personas a CSV y a Excel» (sin carpeta, se avisa), borde.
- **GIVEN** ninguna carpeta de exportación guardada
- **WHEN** se toca «Planilla de ejemplo» y se cancela la elección
- **THEN** el pie avisa que no se eligió una carpeta y no se escribe nada
