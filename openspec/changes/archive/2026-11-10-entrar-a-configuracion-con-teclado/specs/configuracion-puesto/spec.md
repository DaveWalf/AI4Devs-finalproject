## MODIFIED Requirements

### Requirement: Configuración por pestañas

Configuración SHALL organizarse en las pestañas General, Personas, Opciones, Consumos, Comprobantes, Identidad visual, Impresora, Pantalla y Red, una visible a la vez y con General elegida al entrar con la clave del día; con el código de un operador, sólo Consumos, Personas e Impresora (`acceso-puesto`). Cada pestaña MUST medir al menos 56 px de alto, operable por toque y teclado, con su nombre y la marca de elegida. General MUST mostrar el modo, las bitácoras y «Cerrar el puesto».

#### Scenario: Abre en General con las nueve pestañas
Origen: decisión de David del 03/11/2026 («en diferentes pestañas siguiendo el estilo del INWeb») y su pedido del 06/11/2026 (punto 2).
- **GIVEN** el puesto en mantenimiento y en reposo
- **WHEN** se toca el engranaje y se entra con la clave del día
- **THEN** Configuración muestra las nueve pestañas, en ese orden, con General elegida
- **AND** General muestra el modo, el estado de las dos bitácoras y «Cerrar el puesto»

#### Scenario: Elegir otra pestaña
Origen: pedido de David del 06/11/2026 («utilizar pestañas para separar las diferentes secciones»).
- **GIVEN** Configuración abierta en General
- **WHEN** se toca «Impresora»
- **THEN** sólo se ve el contenido de Impresora, y «Impresora» queda marcada como elegida

#### Scenario: Las pestañas con el teclado
Origen: decisión de David del 03/11/2026 (UX táctil, criterio 5: «conservar teclado y foco visible»), borde.
- **GIVEN** el foco en la pestaña General
- **WHEN** se pulsa la flecha derecha y, después, Inicio
- **THEN** primero queda elegida Personas y después otra vez General, con el foco en la elegida

#### Scenario: Las pestañas entran en 1024 × 768
Origen: decisión de David del 03/11/2026 (UX táctil, criterio 1: objetivos amplios sin reducirlos para forzar el ajuste), borde.
- **GIVEN** un área de 1024 × 768
- **WHEN** se abre Configuración
- **THEN** las nueve pestañas se ven en una fila, cada una de al menos 56 px de alto, sin desplazar la pantalla

#### Scenario: Las tres pestañas de un operador
Origen: mejora 13 de David del 10/11/2026 («sólo debe habilitarle las secciones de Consumos, Personas e Impresora»), borde: las flechas no llegan a una pestaña oculta.
- **GIVEN** Configuración abierta con el código de un operador, con el foco en Consumos
- **WHEN** se pulsa la flecha derecha dos veces y, después, Inicio
- **THEN** quedan elegidas Personas, Impresora y otra vez Consumos, y ninguna otra pestaña existe en la pantalla
