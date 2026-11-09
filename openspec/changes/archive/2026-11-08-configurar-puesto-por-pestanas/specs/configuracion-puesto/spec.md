# Spec Delta

## Purpose

Organizar Configuración en pestañas, una por función del puesto, con un guardado propio en cada una, sin perder lo editado al pasar de una a otra y sin que nada quede a medias al cerrarla.

## ADDED Requirements

### Requirement: Configuración por pestañas

Configuración SHALL organizarse en las pestañas General, Personas, Opciones, Consumos, Comprobantes, Identidad visual, Impresora, Pantalla y Red, con General elegida al abrir y una sola pestaña visible a la vez. Cada pestaña MUST ser un control de al menos 56 px de alto, operable por toque y por teclado, con su nombre visible y la marca de elegida. General MUST mostrar el modo, el estado de las bitácoras y «Cerrar el puesto».

#### Scenario: Abre en General con las nueve pestañas
Origen: decisión de David del 03/11/2026 («en diferentes pestañas siguiendo el estilo del INWeb») y su pedido del 06/11/2026 (punto 2).
- **GIVEN** el puesto en mantenimiento y en reposo
- **WHEN** se toca el engranaje
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

### Requirement: Lo editado se conserva hasta cerrar

Lo editado y no guardado en una pestaña SHALL conservarse al pasar a otra mientras Configuración siga abierta. «Cancelar» o Escape MUST cerrar Configuración y descartar lo no guardado de todas las pestañas, sin tocar lo ya guardado. Si la atención sale de reposo con Configuración abierta, MUST cerrarse igual.

#### Scenario: Pasar de pestaña no pierde lo editado
Origen: pedido de David del 06/11/2026 (pestañas para separar las secciones), borde: una pestaña no borra el trabajo de otra.
- **GIVEN** Configuración abierta, con la opacidad movida a 84 % en Identidad visual sin guardar
- **WHEN** se pasa a Impresora y se vuelve a Identidad visual
- **THEN** la opacidad sigue en 84 % y la vista previa también

#### Scenario: Cancelar descarta lo no guardado y conserva lo guardado
Origen: AT37 («conservación del valor previo ante error») y la decisión de David del 03/11/2026 (vista previa con guardar y cancelar).
- **GIVEN** un cupo cambiado y guardado en Personas, y la opacidad movida sin guardar en Identidad visual
- **WHEN** se toca «Cancelar» o se pulsa Escape
- **THEN** Configuración se cierra, la opacidad vuelve a la guardada y la versión con el cupo cambiado sigue activa

#### Scenario: La atención sale de reposo con Configuración abierta
Origen: AT16 («siguiente persona inicia limpia») y RB05, borde.
- **GIVEN** Configuración abierta con una edición sin guardar
- **WHEN** una persona se identifica detrás del diálogo
- **THEN** Configuración se cierra con lo no guardado descartado y no reaparece al volver a reposo

### Requirement: Cada pestaña guarda lo suyo

Cada «Guardar» SHALL guardar sólo lo de su pestaña y dejar Configuración abierta en esa pestaña con un aviso de lo guardado. Si no se puede guardar, MUST avisar el motivo y dejar vigente lo que había.

#### Scenario: Guardar el estilo deja Configuración abierta
Origen: pedido de David del 06/11/2026 (pestañas), borde: guardar una pestaña no cierra las demás.
- **GIVEN** Configuración abierta en Identidad visual, con la opacidad en 84 %
- **WHEN** se toca su «Guardar»
- **THEN** el estilo queda guardado con 84 %, Configuración sigue abierta en Identidad visual y dice que se guardó

#### Scenario: Guardar personas no guarda la vista previa del estilo
Origen: D12 de `administrar-personas-y-opciones` (un guardado no mezcla un archivo de estilo con una versión de la base), borde.
- **GIVEN** la opacidad movida sin guardar y un cupo cambiado en Personas
- **WHEN** se toca el «Guardar» de Personas y, después, «Cancelar»
- **THEN** existe la versión nueva con el cupo y el estilo guardado es el de antes

#### Scenario: Guardar sin poder escribir
Origen: AT37 («conservación del valor previo ante error») y «Estilo por defecto y validado» de `presentacion-puesto`.
- **GIVEN** una carpeta de datos donde no se puede escribir
- **WHEN** se toca el «Guardar» de Identidad visual
- **THEN** Configuración sigue abierta con el aviso de que no se pudo guardar y el estilo vigente es el de antes

### Requirement: Cada ajuste del equipo queda en la bitácora general

Cada guardado de la identidad visual, la impresora o la pantalla SHALL quedar en la bitácora general como `CONFIGURATION_CHANGED`, con el ajuste, el antes, el después y el resultado, el actor nulo con `NO_AUTHENTICATION` y cómo se entró a Configuración. Un logo MUST registrarse por su huella SHA-256, su tamaño y su formato, nunca por su contenido.

#### Scenario: El monitor guardado
Origen: RB21 («todo cambio/prueba tiene permiso, actor y resultado») y AT41.
- **GIVEN** Configuración abierta en mantenimiento, en Pantalla
- **WHEN** se elige otro monitor y se guarda
- **THEN** la general tiene `CONFIGURATION_CHANGED` con el monitor de antes y el de después, el resultado aplicado y `MAINTENANCE_MODE`

#### Scenario: Un logo guardado
Origen: RB21 y `AGENTS.md` (OWASP: la configuración que llega al puesto), borde: el archivo no entra a la bitácora.
- **GIVEN** un PNG válido de 40 KB elegido como logo de la empresa
- **WHEN** se guarda
- **THEN** la general tiene `CONFIGURATION_CHANGED` con el logo de la empresa, su SHA-256, 40 KB y PNG, sin el contenido del archivo
