## MODIFIED Requirements

### Requirement: Configuración por pestañas, con Consumos primero

Configuración SHALL organizarse en las pestañas Consumos, Personas, Opciones, Identidad visual, Impresora, Mails, Pantalla, Red, Lector, Exportación y General, en ese orden, una visible a la vez y con Consumos elegida al entrar, con la clave del día o con el código de un operador; con un operador, sólo Consumos, Personas e Impresora (`acceso-puesto`). Cada pestaña MUST medir al menos 56 px de alto, operable por toque y teclado. General MUST mostrar el modo, las bitácoras y «Cerrar el puesto».

#### Scenario: Abre en Consumos con todas las pestañas en orden
Origen: decisión de David del 03/11/2026 («en diferentes pestañas siguiendo el estilo del INWeb»), su pedido del 06/11/2026 (punto 2) y sus mejoras 4 («Consumos primero»), 6 («sin Comprobantes»), 8a («General al final») y 11 («Exportación de consumos, antes de la última»), 10 («pestaña de mails después de Impresora») y 9a («después de Red, el medio de identificación») del 10/11/2026.
- **GIVEN** el puesto en mantenimiento y en reposo
- **WHEN** se toca el engranaje y se entra con la clave del día
- **THEN** Configuración muestra las pestañas en el orden Consumos, Personas, Opciones, Identidad visual, Impresora, Mails, Pantalla, Red, Lector, Exportación y General, con Consumos elegida, y ninguna pestaña Comprobantes
- **AND** al tocar General, la última, muestra el modo, el estado de las dos bitácoras y «Cerrar el puesto»

#### Scenario: Elegir otra pestaña
Origen: pedido de David del 06/11/2026 («utilizar pestañas para separar las diferentes secciones»).
- **GIVEN** Configuración abierta en Consumos
- **WHEN** se toca «Impresora»
- **THEN** sólo se ve el contenido de Impresora, y «Impresora» queda marcada como elegida

#### Scenario: Las pestañas con el teclado
Origen: decisión de David del 03/11/2026 (UX táctil, criterio 5: «conservar teclado y foco visible»), borde: los extremos de la fila.
- **GIVEN** el foco en la pestaña Consumos
- **WHEN** se pulsa la flecha derecha, después Fin y después Inicio
- **THEN** quedan elegidas Personas, General y otra vez Consumos, con el foco en la elegida

#### Scenario: Las pestañas entran en 1024 × 768
Origen: decisión de David del 03/11/2026 (UX táctil, criterio 1: objetivos amplios sin reducirlos para forzar el ajuste), borde.
- **GIVEN** un área de 1024 × 768
- **WHEN** se abre Configuración
- **THEN** todas las pestañas se ven en una fila, cada una de al menos 56 px de alto, sin desplazar la pantalla

#### Scenario: Las tres pestañas de un operador
Origen: mejora 13 de David del 10/11/2026 («sólo debe habilitarle las secciones de Consumos, Personas e Impresora»), borde: las flechas no llegan a una pestaña oculta.
- **GIVEN** Configuración abierta con el código de un operador, con el foco en Consumos
- **WHEN** se pulsa la flecha derecha dos veces y, después, Inicio
- **THEN** quedan elegidas Personas, Impresora y otra vez Consumos, y ninguna otra pestaña existe en la pantalla
