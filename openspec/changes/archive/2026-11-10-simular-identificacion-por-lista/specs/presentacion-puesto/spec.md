## ADDED Requirements

### Requirement: Pantalla de identificación que invita a comer

En reposo, la pantalla de identificación SHALL titularse con una frase que invite a disfrutar la comida, «¡Hola! Identificate y disfrutá tu comida», en lugar de «Identificate para comenzar». Con el modo simulación activo, el combo MUST ir arriba del campo del código. «Ingresar» MUST ir a la derecha del campo, en la misma fila, y los dos MUST medir al menos 56 px CSS de alto.

#### Scenario: El título invita a disfrutar la comida
Origen: mejora 1 de David del 10/11/2026 («el cartel debe siempre invitar a disfrutar una comida, así que en vez de Identificate para comenzar, diseñá una frase más amigable»).
- **GIVEN** el puesto en reposo
- **WHEN** se muestra la pantalla de identificación
- **THEN** su título es «¡Hola! Identificate y disfrutá tu comida» y la pantalla no contiene «Identificate para comenzar»

#### Scenario: El combo arriba y «Ingresar» a la derecha del campo
Origen: mejora 1 de David del 10/11/2026 («arriba de la opción de ingreso manual. La opción manual debe tener el botón ingresar a su derecha») y la decisión de David del 03/11/2026 (UX táctil, controles de al menos 56 px).
- **GIVEN** el modo simulación activo y la ventana con un área de 1024 × 768
- **WHEN** se muestra la pantalla de identificación
- **THEN** el combo está entero por encima del campo del código, y «Ingresar» está a la derecha del campo, en su misma fila
- **AND** el combo, el campo e «Ingresar» miden al menos 56 px de alto

#### Scenario: Sin simulación, el campo e «Ingresar» siguen en una fila
Origen: mejora 1 de David del 10/11/2026 y decisión 6 de David del 09/11/2026 (con el modo apagado queda sólo el código tipeado), borde.
- **GIVEN** el puesto en mantenimiento con la simulación apagada
- **WHEN** se muestra la pantalla de identificación
- **THEN** no hay combo, y «Ingresar» está a la derecha del campo del código, en su misma fila
