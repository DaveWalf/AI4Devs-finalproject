## ADDED Requirements

### Requirement: Modo simulación encendido por omisión y sólo en mantenimiento

El puesto SHALL tener un modo simulación, encendido por omisión, que se apaga y se vuelve a encender desde General sólo en mantenimiento y se conserva en su carpeta de datos. Sin ajuste guardado, o con uno ilegible, MUST arrancar encendido. En operativo MUST NOT estar activo, aunque lo guardado diga lo contrario, y pasar o arrancar en operativo MUST NOT cambiar lo guardado.

#### Scenario: Una carpeta nueva arranca con la simulación
Origen: mejora 1 de David del 10/11/2026 («la simulación de elegí persona, por defecto activa hasta que la desactive en la configuración»), que reemplaza en esto a la decisión 6 del 09/11/2026 de `IDSerialSim/AGENTS.md`.
- **GIVEN** una carpeta de datos sin ajuste de simulación
- **WHEN** el puesto arranca
- **THEN** General dice «Simulación: encendida» y la pantalla de identificación tiene el combo

#### Scenario: Apagarla se conserva al volver a abrir
Origen: mejora 1 de David del 10/11/2026 («hasta que la desactive en la configuración») y su decisión del 10/11/2026 a las 00:25 (el modo simulación «se conserva al reiniciar»).
- **GIVEN** el puesto en mantenimiento, con la simulación encendida y Configuración abierta en General
- **WHEN** se toca «Apagar la simulación», se cierra el puesto y se lo vuelve a abrir
- **THEN** arranca con la simulación apagada y sin combo en la pantalla de identificación

#### Scenario: En operativo nunca se ve
Origen: decisión de David del 10/11/2026 a las 10:36 («en operativo nunca se ve, pero al volver a mantenimiento reaparece»).
- **GIVEN** el puesto en mantenimiento con la simulación encendida
- **WHEN** se pasa a operativo con la clave del día y, después, se vuelve a mantenimiento
- **THEN** en operativo la pantalla de identificación no tiene combo y General no ofrece la simulación
- **AND** de vuelta en mantenimiento, General dice «Simulación: encendida» y el combo vuelve a la pantalla de identificación

#### Scenario: Operativo con la simulación guardada encendida
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto) y la decisión de David del 10/11/2026 a las 10:36 (en operativo nunca se ve), borde.
- **GIVEN** el modo guardado en operativo y el ajuste de simulación guardado encendido
- **WHEN** el puesto arranca
- **THEN** la pantalla de identificación no tiene combo y el ajuste de simulación sigue guardado encendido

#### Scenario: Un ajuste de simulación ilegible
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), como «Un modo guardado ilegible», que arranca en el modo por omisión, borde.
- **GIVEN** un ajuste de simulación con texto que no es un valor conocido
- **WHEN** el puesto arranca en mantenimiento
- **THEN** la simulación está encendida, como sin ajuste, y el puesto atiende normalmente

### Requirement: El modo simulación queda en la bitácora general

Cada apagado y encendido del modo simulación desde General SHALL quedar en la bitácora general como `CONFIGURATION_CHANGED`, con el ajuste, el antes, el después, el resultado, el actor nulo con `NO_AUTHENTICATION` y cómo se entró a Configuración. Pasar a operativo MUST NOT registrar un cambio del modo simulación, porque no lo cambia. Si no se puede guardar, el puesto MUST avisarlo en General, dejar la simulación como estaba y registrar el resultado fallido.

#### Scenario: Apagar la simulación
Origen: RB21 («todo cambio/prueba tiene permiso, actor y resultado») y AT41.
- **GIVEN** Configuración abierta en mantenimiento, en General, con la simulación encendida
- **WHEN** se toca «Apagar la simulación»
- **THEN** la general tiene `CONFIGURATION_CHANGED` con el modo simulación, antes encendido y después apagado, el resultado aplicado, el acceso con que se entró a Configuración, el actor nulo y `NO_AUTHENTICATION`

#### Scenario: Pasar a operativo no la registra
Origen: RB21 y la decisión de David del 10/11/2026 a las 10:36 (pasar a operativo no cambia lo guardado).
- **GIVEN** Configuración abierta en mantenimiento, con la simulación encendida
- **WHEN** se pasa a operativo con la clave del día
- **THEN** la general tiene el `CONFIGURATION_CHANGED` del modo del puesto con `DAILY_KEY` y ninguno del modo simulación

#### Scenario: La simulación no se puede guardar
Origen: AT37 («conservación del valor previo ante error»), borde.
- **GIVEN** una carpeta de datos donde no se puede escribir, con el puesto en mantenimiento y la simulación encendida
- **WHEN** se toca «Apagar la simulación»
- **THEN** General avisa que no se pudo guardar, la simulación sigue encendida y la pantalla de identificación conserva el combo
- **AND** la general tiene el `CONFIGURATION_CHANGED` del modo simulación con el resultado fallido
