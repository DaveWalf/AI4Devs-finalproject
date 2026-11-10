## ADDED Requirements

### Requirement: Configuración por pestañas, con Consumos primero

Configuración SHALL organizarse en las pestañas Consumos, Personas, Opciones, Comprobantes, Identidad visual, Impresora, Pantalla, Red y General, en ese orden, una visible a la vez y con Consumos elegida al entrar, con la clave del día o con el código de un operador; con un operador, sólo Consumos, Personas e Impresora (`acceso-puesto`). Cada pestaña MUST medir al menos 56 px de alto, operable por toque y teclado. General MUST mostrar el modo, las bitácoras y «Cerrar el puesto».

#### Scenario: Abre en Consumos con todas las pestañas en orden
Origen: decisión de David del 03/11/2026 («en diferentes pestañas siguiendo el estilo del INWeb»), su pedido del 06/11/2026 (punto 2) y sus mejoras 4 («Consumos primero») y 8a («General al final») del 10/11/2026.
- **GIVEN** el puesto en mantenimiento y en reposo
- **WHEN** se toca el engranaje y se entra con la clave del día
- **THEN** Configuración muestra las pestañas en el orden Consumos, Personas, Opciones, Comprobantes, Identidad visual, Impresora, Pantalla, Red y General, con Consumos elegida
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

## MODIFIED Requirements

### Requirement: Lo editado se conserva hasta cerrar

Lo editado y no guardado en una pestaña SHALL conservarse al pasar a otra mientras Configuración siga abierta. «Cerrar» o Escape MUST cerrar Configuración y descartar lo no guardado de todas las pestañas, sin tocar lo ya guardado. Si la atención sale de reposo con Configuración abierta, MUST cerrarse igual.

#### Scenario: Pasar de pestaña no pierde lo editado
Origen: pedido de David del 06/11/2026 (pestañas para separar las secciones), borde: una pestaña no borra el trabajo de otra.
- **GIVEN** Configuración abierta, con la opacidad movida a 84 % en Identidad visual sin guardar
- **WHEN** se pasa a Impresora y se vuelve a Identidad visual
- **THEN** la opacidad sigue en 84 % y la vista previa también

#### Scenario: Cancelar descarta lo no guardado y conserva lo guardado
Origen: AT37 («conservación del valor previo ante error»), la decisión de David del 03/11/2026 (vista previa con guardar y cancelar) y su hallazgo del 10/11/2026 («un botón Cancelar cuyo título es incorrecto, debería ser Cerrar»).
- **GIVEN** un cupo cambiado y guardado en Personas, y la opacidad movida sin guardar en Identidad visual
- **WHEN** se toca «Cerrar» o se pulsa Escape
- **THEN** Configuración se cierra, la opacidad vuelve a la guardada y la versión con el cupo cambiado sigue activa
- **AND** el pie de Configuración no tiene ningún botón «Cancelar»

#### Scenario: La atención sale de reposo con Configuración abierta
Origen: AT16 («siguiente persona inicia limpia») y RB05, borde.
- **GIVEN** Configuración abierta con una edición sin guardar
- **WHEN** una persona se identifica detrás del diálogo
- **THEN** Configuración se cierra con lo no guardado descartado y no reaparece al volver a reposo

## REMOVED Requirements

### Requirement: Configuración por pestañas

**Reason**: El orden cambia (Consumos primero, General al final) y Configuración abre en Consumos; los títulos de sus escenarios nombraban General como la pestaña inicial y «nueve» pestañas, y OpenSpec no deja renombrar escenarios dentro de un requisito modificado.

**Migration**: La reemplaza «Configuración por pestañas, con Consumos primero», con la misma conducta salvo el orden y la pestaña inicial, y escenarios sin el número de pestañas.
