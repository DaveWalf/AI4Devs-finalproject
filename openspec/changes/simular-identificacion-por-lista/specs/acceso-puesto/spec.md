## ADDED Requirements

### Requirement: Modo simulación sólo en mantenimiento

El puesto SHALL tener un modo simulación, apagado por omisión, que se enciende y se apaga desde General sólo en mantenimiento y se conserva en su carpeta de datos para el próximo arranque. Sin ajuste guardado, o con uno ilegible o desconocido, MUST arrancar apagado. Pasar a operativo MUST apagarlo y guardarlo apagado. En operativo MUST NOT estar activo, aunque lo guardado diga lo contrario.

#### Scenario: Una carpeta nueva arranca sin simulación
Origen: decisión 6 de David del 09/11/2026, en `IDSerialSim/AGENTS.md` («un modo simulación del puesto, apagado por omisión»).
- **GIVEN** una carpeta de datos sin ajuste de simulación
- **WHEN** el puesto arranca
- **THEN** General dice «Simulación: apagada» y la pantalla de identificación no tiene combo

#### Scenario: Encenderla se conserva al volver a abrir
Origen: decisión de David del 10/11/2026 (el modo simulación «se conserva al reiniciar»).
- **GIVEN** el puesto en mantenimiento, con Configuración abierta en General
- **WHEN** se toca «Encender la simulación», se cierra el puesto y se lo vuelve a abrir
- **THEN** arranca con la simulación encendida y el combo en la pantalla de identificación

#### Scenario: Pasar a operativo la apaga
Origen: decisión de David del 10/11/2026 («pasar a operativo lo apaga, así un puesto operativo nunca muestra la lista»).
- **GIVEN** el puesto en mantenimiento con la simulación encendida
- **WHEN** se pasa a operativo con la clave del día y, después, se vuelve a mantenimiento
- **THEN** en operativo la pantalla de identificación no tiene combo y General no ofrece la simulación
- **AND** de vuelta en mantenimiento, General dice «Simulación: apagada»

#### Scenario: Operativo con la simulación guardada encendida
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto) y la decisión de David del 10/11/2026 (un puesto operativo nunca muestra la lista), borde.
- **GIVEN** el modo guardado en operativo y el ajuste de simulación guardado encendido
- **WHEN** el puesto arranca
- **THEN** la pantalla de identificación no tiene combo y el ajuste de simulación queda guardado apagado

#### Scenario: Un ajuste de simulación ilegible
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), como «Un modo guardado ilegible», borde.
- **GIVEN** un ajuste de simulación con texto que no es un valor conocido
- **WHEN** el puesto arranca en mantenimiento
- **THEN** la simulación está apagada y el puesto atiende normalmente

### Requirement: El modo simulación queda en la bitácora general

Cada encendido y apagado del modo simulación SHALL quedar en la bitácora general como `CONFIGURATION_CHANGED`, con el ajuste, el antes, el después, el resultado, el actor nulo con `NO_AUTHENTICATION` y cómo se entró a Configuración. Si no se puede guardar, el puesto MUST avisarlo en General, dejar la simulación como estaba y registrar el resultado fallido.

#### Scenario: Encender la simulación
Origen: RB21 («todo cambio/prueba tiene permiso, actor y resultado») y AT41.
- **GIVEN** Configuración abierta en mantenimiento, en General
- **WHEN** se toca «Encender la simulación»
- **THEN** la general tiene `CONFIGURATION_CHANGED` con el modo simulación, antes apagado y después encendido, el resultado aplicado, `MAINTENANCE_MODE`, el actor nulo y `NO_AUTHENTICATION`

#### Scenario: Apagada al pasar a operativo
Origen: RB21 y la decisión de David del 10/11/2026 («pasar a operativo lo apaga»).
- **GIVEN** Configuración abierta en mantenimiento, con la simulación encendida
- **WHEN** se pasa a operativo con la clave del día
- **THEN** la general tiene el `CONFIGURATION_CHANGED` del modo del puesto y, después, el del modo simulación, de encendido a apagado, los dos con `DAILY_KEY`

#### Scenario: La simulación no se puede guardar
Origen: AT37 («conservación del valor previo ante error»), borde.
- **GIVEN** una carpeta de datos donde no se puede escribir, con el puesto en mantenimiento
- **WHEN** se toca «Encender la simulación»
- **THEN** General avisa que no se pudo guardar, la simulación sigue apagada y la pantalla de identificación no tiene combo
- **AND** la general tiene el `CONFIGURATION_CHANGED` del modo simulación con el resultado fallido
