# modo-demostracion Specification

## Purpose

Permitir que el puesto arranque sobre una base vacía con un conjunto fijo de datos sintéticos y que una pantalla confirme intenciones contra la base local con la confirmación atómica (RB06), para mostrar el flujo principal sin red y sin datos reales.

## Requirements

### Requirement: Base de demostración con datos sintéticos

Al arrancar en modo demostración, el puesto SHALL abrir su base SQLite local y, si no tiene ningún puesto configurado, cargar en una sola transacción un conjunto fijo de datos sintéticos: un puesto, una persona habilitada con límite 1 para ALMUERZO, una opción de menú y su regla. Una base que ya tiene un puesto configurado MUST NOT volver a sembrarse.

#### Scenario: Primer arranque sobre una base inexistente
Origen: AT51 (base vacía válida), TK03.1, y AT01 (persona sintética habilitada).
- **GIVEN** que no existe el archivo de la base del puesto
- **WHEN** el puesto arranca en modo demostración
- **THEN** la base queda en la versión de esquema 3, con un puesto, una persona habilitada, una opción de menú y una regla de límite 1, todos sintéticos
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

### Requirement: Confirmación desde la pantalla de demostración

Cada toque de «Confirmar» SHALL abrir una sesión de identidad de prueba sin credencial, generar un `IntentId` nuevo y confirmar la intención con la confirmación atómica (RB06) sobre la base local, sin red. La pantalla MUST mostrar la decisión, el motivo si la rechaza, y los identificadores de consumo y comprobante si la autoriza. MUST NOT mostrar ni registrar ningún código de identificación.

#### Scenario: Primera confirmación del día
Origen: AT01.
- **GIVEN** la base de demostración recién sembrada y ninguna conexión de red
- **WHEN** se toca «Confirmar»
- **THEN** la pantalla muestra la decisión AUTHORIZED con un identificador de consumo y uno de comprobante
- **AND** la base tiene exactamente un consumo, con esos identificadores

#### Scenario: Segunda confirmación con el cupo agotado
Origen: AT02.
- **GIVEN** que la persona sintética ya tiene autorizado su único ALMUERZO de la fecha de negocio
- **WHEN** se vuelve a tocar «Confirmar»
- **THEN** la pantalla muestra la decisión DENIED con el motivo `LOCAL_LIMIT_REACHED`
- **AND** la base sigue teniendo un solo consumo

#### Scenario: La sesión de prueba no lleva código
Origen: D6 de `fijar-primer-corte` y `AGENTS.md`.
- **GIVEN** una confirmación autorizada desde la pantalla
- **WHEN** se revisan la sesión de identidad creada y el texto de la pantalla
- **THEN** la sesión no tiene credencial asociada y ningún valor de credencial aparece en la pantalla

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
- **THEN** la base queda en la versión 3 con los datos de atención agregados
- **AND** sigue habiendo un solo puesto y el consumo anterior sigue registrado

### Requirement: Receptor y puesto de demostración conectados

El receptor arrancado con `--demostracion` SHALL generar, si faltan, una autoridad, un certificado de servidor y uno del puesto, todos sintéticos, en la carpeta de la demostración fuera del repo, y registrar el puesto de la demostración. Con esos certificados el puesto MUST enviar sus lotes a ese receptor; sin ellos MUST atender e informar que no hay receptor configurado.

#### Scenario: Primer arranque del receptor de demostración
Origen: AT13 (credencial de equipo) y `AGENTS.md`, «Never commit secrets».
- **GIVEN** que no existe la carpeta de certificados de la demostración
- **WHEN** el receptor arranca con `--demostracion`
- **THEN** la carpeta tiene la autoridad, el certificado del receptor y el del puesto, y el receptor tiene registrado al puesto de la demostración
- **AND** `git status --short` no muestra ningún archivo nuevo en el repo

#### Scenario: Puesto con receptor
Origen: HU02-C y AT27.
- **GIVEN** el receptor de demostración en marcha y un puesto con un consumo confirmado
- **WHEN** corre un ciclo de envío
- **THEN** el receptor tiene ese consumo consolidado y el estado de la sincronización del puesto dice que está al día

#### Scenario: Puesto sin certificados
Origen: AT01 (monopuesto sin red) y HU02-G, «en monopuesto sigue atención».
- **GIVEN** que no existe la carpeta de certificados de la demostración
- **WHEN** arranca la atención y una persona confirma
- **THEN** la intención se decide como antes y el estado de la sincronización informa que no hay receptor configurado
- **AND** el evento queda en `PENDING`
