## ADDED Requirements

### Requirement: La consulta es de quien entra con la clave o un operador, y queda registrada

La pestaña SHALL mostrarse a quien entró a Configuración con la clave del día o con el código de un operador, sin pedir otro código; «Comprobantes» MUST seguir pidiéndolo. Cada consulta MUST quedar en la bitácora general con el actor (nulo con `NO_AUTHENTICATION` si se entró con la clave), cómo se entró (`Access`), los filtros, el orden y la cantidad de filas, sin nombres ni códigos.

#### Scenario: En mantenimiento, con la clave del día
Origen: decisión de David del 06/11/2026 (gate de D, punto 3: «Consumos lo ve cualquiera que entre a Configuración») y su mejora 3 del 10/11/2026 (la clave también en mantenimiento).
- **GIVEN** el puesto en mantenimiento, con consumos de hoy
- **WHEN** se entra a Configuración con la clave del día y se toca la pestaña Consumos
- **THEN** la tabla muestra los consumos de hoy sin pedir otro código

#### Scenario: En operativo, con la clave del día
Origen: decisión de David del 06/11/2026 (gate de D, punto 3) y «Entrada a Configuración según el modo» de `acceso-puesto`, borde.
- **GIVEN** el puesto en operativo, con consumos de hoy
- **WHEN** se entra a Configuración con la clave del día y se toca la pestaña Consumos
- **THEN** la tabla muestra los consumos de hoy sin pedir otro código

#### Scenario: Un operador ve los consumos
Origen: mejora 13 de David del 10/11/2026 (Consumos entre las secciones del operador) y RB21.
- **GIVEN** el puesto con consumos de hoy
- **WHEN** se entra a Configuración con `9001`
- **THEN** se abre en Consumos con la tabla de los consumos de hoy
- **AND** la general tiene `CONSUMPTIONS_QUERIED` con `OPERATOR` y la persona de `9001` como actor, sin nombres de personas ni códigos

#### Scenario: Comprobantes sigue pidiendo un operador
Origen: decisión de David del 06/11/2026 (2/3: «Comprobantes» con `OPERATOR`; gate de D, punto 3), borde: ver los consumos no abre la reimpresión.
- **GIVEN** Configuración abierta con la clave del día y la tabla de Consumos a la vista
- **WHEN** se pasa a la pestaña Comprobantes
- **THEN** pide el código de un operador con `OPERATOR` y no muestra comprobantes hasta aceptarlo

#### Scenario: La consulta en la bitácora
Origen: RB21 («todo cambio/prueba tiene permiso, actor y resultado») y AT41.
- **GIVEN** Configuración abierta en mantenimiento, a la que se entró con la clave del día
- **WHEN** se filtra por REFRIGERIO y la tabla muestra dos consumos
- **THEN** la general tiene `CONSUMPTIONS_QUERIED` con el actor nulo, `NO_AUTHENTICATION`, `DAILY_KEY`, el servicio filtrado y 2 filas, sin nombres de personas ni códigos

## REMOVED Requirements

### Requirement: La consulta es de quien entra a Configuración y queda registrada
**Reason**: Desde el 10/11/2026 (mejoras 3 y 13 de David) nadie entra a Configuración sin la clave del día o el código de un operador, y un operador queda registrado como actor. Lo reemplaza «La consulta es de quien entra con la clave o un operador, y queda registrada», que conserva «En operativo, con la clave del día», «Comprobantes sigue pidiendo un operador» y «La consulta en la bitácora».
**Migration**: El escenario «En mantenimiento, sin ningún código» pasa a «En mantenimiento, con la clave del día», y «La consulta en la bitácora» registra `DAILY_KEY` en lugar de `MAINTENANCE_MODE`.
