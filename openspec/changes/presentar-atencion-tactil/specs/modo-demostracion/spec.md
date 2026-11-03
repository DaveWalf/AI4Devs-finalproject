# Spec Delta

## ADDED Requirements

### Requirement: Catálogo sintético de nueve opciones

Cuando `INCOMEDOR_CATALOGO` vale `nueve`, sin espacios alrededor ni distinción de mayúsculas, la siembra de atención SHALL crear ocho opciones en lugar de catorce, que con «Almuerzo de demostración» suman nueve, tres con importe cero, con las mismas personas, códigos y operadores sintéticos. Otro valor, o ninguno, MUST dejar las quince. Una base ya sembrada MUST NOT cambiar.

#### Scenario: Base nueva con el catálogo de nueve
Origen: decisión de David del 03/11/2026 («empezar a hacer pruebas con el ejecutable … referencia real de cómo quedará») y AT01 (persona sintética habilitada).
- **GIVEN** que no existe la base del puesto y `INCOMEDOR_CATALOGO` vale `nueve`
- **WHEN** el puesto arranca
- **THEN** el catálogo activo tiene nueve opciones, tres con importe cero
- **AND** el código `0001` abre la selección como con el catálogo de quince

#### Scenario: Un valor desconocido deja las quince
Origen: `AGENTS.md` (variables del puesto: un valor inválido se ignora).
- **GIVEN** que no existe la base del puesto y `INCOMEDOR_CATALOGO` vale `9`
- **WHEN** el puesto arranca
- **THEN** el catálogo activo tiene quince opciones

#### Scenario: Una base existente no cambia
Origen: «Un segundo arranque no duplica ni borra» de esta capability.
- **GIVEN** una base ya sembrada con quince opciones
- **WHEN** el puesto arranca con `INCOMEDOR_CATALOGO` en `nueve`
- **THEN** el catálogo activo sigue teniendo quince opciones
