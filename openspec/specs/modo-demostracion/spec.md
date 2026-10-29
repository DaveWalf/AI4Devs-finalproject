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
- **THEN** la base queda en la versión de esquema 1, con un puesto, una persona habilitada, una opción de menú y una regla de límite 1, todos sintéticos
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
