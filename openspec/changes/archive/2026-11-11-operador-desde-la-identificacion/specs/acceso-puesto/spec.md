## MODIFIED Requirements

### Requirement: Cada acceso queda en la bitácora general

Cada clave del día pedida con su resultado y su acción, cada entrada a Configuración, cada cambio de modo y cada cierre SHALL quedar en la bitácora general con cómo se entró o se cerró: `MAINTENANCE_MODE`, `DAILY_KEY`, `OPERATOR` (el engranaje) u `OPERATOR_IDENTIFICATION` (la identificación en reposo). Con la clave o en mantenimiento, el actor MUST ser nulo con `NO_AUTHENTICATION`; con un operador, MUST ser su persona. Ningún registro MUST contener lo tipeado.

#### Scenario: Entrada con la clave del día
Origen: RB21 («todo cambio/prueba tiene permiso, actor y resultado») y AT41.
- **GIVEN** el puesto en mantenimiento y, otra vez, en operativo
- **WHEN** se entra a Configuración con la clave del día
- **THEN** cada vez la general tiene la clave aceptada para abrir Configuración y `CONFIGURATION_OPENED` con `DAILY_KEY`, los dos con el actor nulo y `NO_AUTHENTICATION`

#### Scenario: Entrada de un operador
Origen: RB21, RB26 y la mejora 13 de David del 10/11/2026 («si el que está logueado es un usuario operador»).
- **GIVEN** el puesto en reposo
- **WHEN** se entra a Configuración con `9001`
- **THEN** la general tiene `CONFIGURATION_OPENED` con `OPERATOR` y la persona de `9001` como actor
- **AND** ningún registro de las dos bitácoras contiene `9001`

#### Scenario: Clave rechazada
Origen: AT56 («los demás casos se deniegan y auditan sin secretos»), borde.
- **GIVEN** el diálogo de la clave del día abierto para cerrar el puesto
- **WHEN** se toca «ENTER» con `12a4`
- **THEN** la general tiene la clave rechazada por formato para cerrar el puesto, sin `12a4`

#### Scenario: Cambio de modo
Origen: RB21 y AT37 («cambio válido versionado/auditado»).
- **GIVEN** Configuración abierta en mantenimiento, a la que se entró con la clave del día
- **WHEN** se pasa a operativo con la clave del día
- **THEN** la general tiene `CONFIGURATION_CHANGED` con el modo, antes mantenimiento y después operativo, el resultado aplicado y `DAILY_KEY`

#### Scenario: Entrada de un operador desde la identificación
Origen: mejora 5 de David del 11/11/2026 y RB21 («todo cambio/prueba tiene permiso, actor y resultado»), borde: la general distingue los dos caminos.
- **GIVEN** el puesto en reposo
- **WHEN** se tipea `9001` en el campo del código, se toca «Ingresar» y después «Configuración»
- **THEN** la general tiene `CONFIGURATION_OPENED` con `OPERATOR_IDENTIFICATION` y la persona de `9001` como actor, y ningún registro contiene `9001`
