# Spec Delta

## MODIFIED Requirements

### Requirement: Base de demostración con datos sintéticos

Al arrancar en modo demostración, el puesto SHALL abrir su base SQLite local y, si no tiene ningún puesto configurado, cargar en una sola transacción un conjunto fijo de datos sintéticos: un puesto, una persona habilitada con límite 1 para ALMUERZO, una opción de menú y su regla. Una base que ya tiene un puesto configurado MUST NOT volver a sembrarse.

#### Scenario: Primer arranque sobre una base inexistente
Origen: AT51 (base vacía válida), TK03.1, y AT01 (persona sintética habilitada).
- **GIVEN** que no existe el archivo de la base del puesto
- **WHEN** el puesto arranca en modo demostración
- **THEN** la base queda en la versión de esquema 2, con un puesto, una persona habilitada, una opción de menú y una regla de límite 1, todos sintéticos
- **AND** no contiene ningún consumo

#### Scenario: Un segundo arranque no duplica ni borra
Origen: sin AT propio; RB06, «un consumo ya confirmado no se revierte».
- **GIVEN** una base sembrada en la que ya se autorizó un consumo
- **WHEN** el puesto vuelve a arrancar
- **THEN** sigue habiendo un solo puesto, una sola persona y una sola opción
- **AND** el consumo autorizado sigue registrado

#### Scenario: Base de una versión de esquema desconocida
Origen: TK03.1, trabajo incluido 2, visto desde el puesto.
- **GIVEN** una base con una versión de esquema mayor que la última conocida
- **WHEN** el puesto arranca
- **THEN** la pantalla informa un error de almacenamiento y no ofrece confirmar
- **AND** el archivo queda sin cambios

## ADDED Requirements

### Requirement: Conjunto sintético de atención

Al arrancar la atención, el puesto SHALL sembrar primero la base de demostración si hace falta y agregar después, en una sola transacción y sólo si todavía no están, los datos sintéticos de atención sobre ese mismo snapshot: opciones hasta sumar 15, personas con un código sintético en cada estado de credencial y personas con permisos de operador en cada estado. MUST NOT borrar ni duplicar nada.

#### Scenario: Primer arranque de la atención
Origen: AT16 («catálogo de 15 opciones»), AT52 (códigos vigente, vencido, revocado, futuro y desconocido) y AT54 (operador vigente, vencido, revocado y administrador sin OPERATOR).
- **GIVEN** que no existe el archivo de la base del puesto
- **WHEN** arranca la atención
- **THEN** la base tiene 15 opciones de menú, un código vigente, uno vencido, uno revocado y uno todavía no vigente, y permisos `OPERATOR` vigente, vencido y revocado, y uno `SYSTEM_ADMINISTRATOR` sin `OPERATOR`
- **AND** no contiene ningún consumo

#### Scenario: Un segundo arranque de la atención no duplica
Origen: sin AT propio; RB06, «un consumo ya confirmado no se revierte».
- **GIVEN** una base en la que la atención ya arrancó y se autorizó un consumo
- **WHEN** la atención vuelve a arrancar
- **THEN** la cantidad de filas de opciones, personas, credenciales y permisos no cambia y el consumo sigue registrado

#### Scenario: Atención sobre una base de la demostración en uso
Origen: RB06, «un consumo ya confirmado no se revierte», y la semilla de la demostración sin cambios.
- **GIVEN** una base sembrada por la demostración, en la versión de esquema 1, con un consumo autorizado
- **WHEN** arranca la atención
- **THEN** la base queda en la versión 2 con los datos de atención agregados
- **AND** sigue habiendo un solo puesto y el consumo anterior sigue registrado
