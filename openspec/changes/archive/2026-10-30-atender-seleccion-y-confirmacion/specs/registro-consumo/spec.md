# Spec Delta

## MODIFIED Requirements

### Requirement: Base SQLite con esquema versionado

Al abrir la base, el sistema MUST activar las claves foráneas, aplicar en orden las migraciones conocidas (`0001` y `0002`) que le falten a una base vacía o de una versión anterior, cada una en su propia transacción, y rechazar, sin modificarla, una base cuya versión de esquema no conoce. Las migraciones MUST usar los nombres físicos del mapa de nomenclatura (D-C18) y hacer cumplir las unicidades del modelo: un consumo por intención, un comprobante por consumo, `(TicketId, CopyNumber)`, la clave de `QuotaUsage` y un permiso por persona y rol en cada snapshot.

#### Scenario: Base vacía
Origen: TK03.1, «la migración crea una base vacía válida», y AT51.
- **GIVEN** un archivo SQLite inexistente
- **WHEN** se abre la base
- **THEN** queda en la versión de esquema 2, con claves foráneas activas y las 21 tablas con los nombres `tbComedor…` del mapa, entre ellas `tbComedorPermisosOperadores`

#### Scenario: Base en la versión 1
Origen: AT51 («persistencia, adaptación y serialización») y RB06 («un consumo ya confirmado no se revierte»).
- **GIVEN** una base en la versión de esquema 1 con un consumo autorizado
- **WHEN** se abre
- **THEN** queda en la versión de esquema 2 con `tbComedorPermisosOperadores` vacía
- **AND** el consumo, su comprobante y su trabajo de impresión siguen iguales

#### Scenario: Versión de esquema desconocida
Origen: TK03.1, trabajo incluido 2.
- **GIVEN** una base con una versión de esquema mayor que la última migración conocida
- **WHEN** se abre
- **THEN** la apertura falla con un error de esquema desconocido y el archivo queda sin cambios

#### Scenario: Unicidad de consumo por intención
Origen: TK03.1, trabajo incluido 5, y RNF01.
- **GIVEN** una intención con su consumo
- **WHEN** se intenta insertar directamente otro consumo con el mismo `IntentId`
- **THEN** la base rechaza la inserción por la restricción de unicidad

#### Scenario: Unicidad de permiso por persona y rol
Origen: modelo de datos, `LocalOperatorGrant` («UQ `(SnapshotId, PersonId, Role)`»).
- **GIVEN** una persona con un permiso `OPERATOR` en el snapshot activo
- **WHEN** se intenta insertar directamente otro permiso `OPERATOR` para esa persona en el mismo snapshot
- **THEN** la base rechaza la inserción por la restricción de unicidad
