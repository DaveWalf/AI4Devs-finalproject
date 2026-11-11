## ADDED Requirements

### Requirement: Guardar un resguardo de la configuración

General SHALL guardar, en la carpeta de la exportación, un `.zip` con la versión activa de la configuración, el estilo visual, los logos, los textos del comprobante y la presentación de las opciones, más un manifiesto con el formato, el esquema y la huella de cada parte. MUST NOT incluir certificados, consumos ni lo que es del equipo. Queda en la general como `CONFIGURATION_BACKUP_CREATED`.

#### Scenario: Guardar el resguardo
Origen: mejora 8a de David del 10/11/2026 («resguardo … de toda la configuración»).
- **GIVEN** la configuración de la demostración con un logo propio y una carpeta de exportación elegida
- **WHEN** se toca «Guardar resguardo» en General
- **THEN** la carpeta tiene un `.zip` con el manifiesto, la configuración y el logo, sin certificados, y la general tiene `CONFIGURATION_BACKUP_CREATED` con su huella

#### Scenario: Sin carpeta para el resguardo
Origen: «Exportar Personas a CSV y a Excel» (sin carpeta, se avisa), borde.
- **GIVEN** ninguna carpeta de exportación guardada
- **WHEN** se toca «Guardar resguardo» y se cancela la elección
- **THEN** General avisa que no se eligió una carpeta y no se escribe nada

### Requirement: Recuperar un resguardo

General SHALL recuperar un resguardo: lo valida entero, muestra el resumen de qué se recupera con «Recuperar» y «Cancelar», y al confirmar crea una versión nueva con su configuración y reemplaza los ajustes locales que trae. Un resguardo de otro formato o esquema, con una parte alterada o desconocida, o con un código que en este puesto es de otra persona, MUST rechazarse sin cambiar nada.

#### Scenario: Recuperar en otro puesto
Origen: mejora 8a de David del 10/11/2026 («recuperación … entre puestos y versiones, y el resumen de qué se recupera»).
- **GIVEN** un resguardo guardado en un puesto con una persona agregada, un estilo y un logo propios
- **WHEN** otro puesto con el mismo esquema lo recupera y se toca «Recuperar»
- **THEN** el resumen decía cuántas personas, opciones y permisos y qué ajustes traía, y el otro puesto tiene una versión nueva con esa persona, ese estilo y ese logo

#### Scenario: Volver a la configuración de ayer
Origen: mejora 8a de David del 10/11/2026 («entre … versiones»), borde.
- **GIVEN** un resguardo guardado y, después, una opción quitada
- **WHEN** se recupera ese resguardo
- **THEN** la opción vuelve a ofrecerse en una versión nueva, y la anterior queda en la base

#### Scenario: Un resguardo alterado o de otro esquema
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto) y AT37 («conservación del valor previo ante error»), borde.
- **GIVEN** un resguardo con una parte cambiada después de guardarlo, otro con una parte que no es del resguardo y otro de otro esquema
- **WHEN** se intenta recuperar cada uno
- **THEN** el cartel dice el motivo, la versión activa no cambia y la general tiene `CONFIGURATION_RESTORE_REJECTED`

#### Scenario: Un código que es de otra persona
Origen: RB25 («el código no se reasigna»), borde.
- **GIVEN** un resguardo con una persona cuyo código, en este puesto, es de otra persona
- **WHEN** se intenta recuperar
- **THEN** se rechaza por el código, sin mostrarlo, y no cambia nada

#### Scenario: Cancelar la recuperación
Origen: AT37, borde.
- **GIVEN** el resumen de un resguardo válido
- **WHEN** se toca «Cancelar»
- **THEN** la versión activa y los ajustes locales no cambian y la general no tiene `CONFIGURATION_RESTORED`

### Requirement: El resguardo queda en la bitácora

Guardar SHALL dejar `CONFIGURATION_BACKUP_CREATED` con el archivo y su huella; recuperar, `CONFIGURATION_RESTORED` con la huella, la versión de origen, la nueva y las partes recuperadas; un rechazo, `CONFIGURATION_RESTORE_REJECTED` con el motivo. Ninguno MUST llevar códigos, nombres ni contenido.

#### Scenario: La recuperación en la bitácora
Origen: RB21 («todo cambio … tiene permiso, actor y resultado»).
- **GIVEN** un resguardo recuperado
- **WHEN** se lee la general
- **THEN** tiene `CONFIGURATION_RESTORED` con la huella del archivo, la versión de origen y la nueva, y ningún código ni nombre de persona
