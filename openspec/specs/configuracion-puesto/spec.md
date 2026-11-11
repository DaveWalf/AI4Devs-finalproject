# configuracion-puesto Specification

## Purpose
Organizar Configuración en pestañas, una por función del puesto, con un guardado propio en cada una, sin perder lo editado al pasar de una a otra y sin que nada quede a medias al cerrarla.

## Requirements

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

### Requirement: Configuración por pestañas, con Consumos primero

Configuración SHALL organizarse en las pestañas Consumos, Personas, Opciones, Identidad visual, Impresora, Mails, Pantalla, Red, Exportación y General, en ese orden, una visible a la vez y con Consumos elegida al entrar, con la clave del día o con el código de un operador; con un operador, sólo Consumos, Personas e Impresora (`acceso-puesto`). Cada pestaña MUST medir al menos 56 px de alto, operable por toque y teclado. General MUST mostrar el modo, las bitácoras y «Cerrar el puesto».

#### Scenario: Abre en Consumos con todas las pestañas en orden
Origen: decisión de David del 03/11/2026 («en diferentes pestañas siguiendo el estilo del INWeb»), su pedido del 06/11/2026 (punto 2) y sus mejoras 4 («Consumos primero»), 6 («sin Comprobantes»), 8a («General al final») y 11 («Exportación de consumos, antes de la última») y 10 («pestaña de mails después de Impresora») del 10/11/2026.
- **GIVEN** el puesto en mantenimiento y en reposo
- **WHEN** se toca el engranaje y se entra con la clave del día
- **THEN** Configuración muestra las pestañas en el orden Consumos, Personas, Opciones, Identidad visual, Impresora, Mails, Pantalla, Red, Exportación y General, con Consumos elegida, y ninguna pestaña Comprobantes
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

### Requirement: Guardar un resguardo de la configuración

General SHALL guardar, en la carpeta de la exportación, un `.zip` con la versión activa de la configuración, el estilo visual, los logos, los textos del comprobante y la presentación de las opciones, más un manifiesto con el formato, el esquema y la huella de cada parte. MUST NOT incluir certificados, consumos ni lo que es del equipo. Queda en la general como `CONFIGURATION_BACKUP_CREATED`.

#### Scenario: Guardar el resguardo
Origen: mejora 8a de David del 10/11/2026 («resguardo … de toda la configuración»).
- **GIVEN** la configuración de la demostración con un logo propio y una carpeta de exportación elegida
- **WHEN** se toca «Guardar resguardo» en General
- **THEN** la carpeta tiene un `.zip` con el manifiesto, la configuración y el logo, sin certificados, y la general tiene `CONFIGURATION_BACKUP_CREATED` con su huella

#### Scenario: Sin carpeta para el resguardo
Origen: «Exportar Personas a CSV y a Excel» (sin carpeta, se avisa), borde.
- **GIVEN** ninguna carpeta de exportación guardada
- **WHEN** se toca «Guardar resguardo» y se cancela la elección
- **THEN** General avisa que no se eligió una carpeta y no se escribe nada

### Requirement: Recuperar un resguardo

General SHALL recuperar un resguardo: lo valida entero, muestra el resumen de qué se recupera con «Recuperar» y «Cancelar», y al confirmar crea una versión nueva con su configuración y reemplaza los ajustes locales que trae. Un resguardo de otro formato o esquema, con una parte alterada o desconocida, o con un código que en este puesto es de otra persona, MUST rechazarse sin cambiar nada.

#### Scenario: Recuperar en otro puesto
Origen: mejora 8a de David del 10/11/2026 («recuperación … entre puestos y versiones, y el resumen de qué se recupera»).
- **GIVEN** un resguardo guardado en un puesto con una persona agregada, un estilo y un logo propios
- **WHEN** otro puesto con el mismo esquema lo recupera y se toca «Recuperar»
- **THEN** el resumen decía cuántas personas, opciones y permisos y qué ajustes traía, y el otro puesto tiene una versión nueva con esa persona, ese estilo y ese logo

#### Scenario: Volver a la configuración de ayer
Origen: mejora 8a de David del 10/11/2026 («entre … versiones»), borde.
- **GIVEN** un resguardo guardado y, después, una opción quitada
- **WHEN** se recupera ese resguardo
- **THEN** la opción vuelve a ofrecerse en una versión nueva, y la anterior queda en la base

#### Scenario: Un resguardo alterado o de otro esquema
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto) y AT37 («conservación del valor previo ante error»), borde.
- **GIVEN** un resguardo con una parte cambiada después de guardarlo, otro con una parte que no es del resguardo y otro de otro esquema
- **WHEN** se intenta recuperar cada uno
- **THEN** el cartel dice el motivo, la versión activa no cambia y la general tiene `CONFIGURATION_RESTORE_REJECTED`

#### Scenario: Un código que es de otra persona
Origen: RB25 («el código no se reasigna»), borde.
- **GIVEN** un resguardo con una persona cuyo código, en este puesto, es de otra persona
- **WHEN** se intenta recuperar
- **THEN** se rechaza por el código, sin mostrarlo, y no cambia nada

#### Scenario: Cancelar la recuperación
Origen: AT37, borde.
- **GIVEN** el resumen de un resguardo válido
- **WHEN** se toca «Cancelar»
- **THEN** la versión activa y los ajustes locales no cambian y la general no tiene `CONFIGURATION_RESTORED`

### Requirement: El resguardo queda en la bitácora

Guardar SHALL dejar `CONFIGURATION_BACKUP_CREATED` con el archivo y su huella; recuperar, `CONFIGURATION_RESTORED` con la huella, la versión de origen, la nueva y las partes recuperadas; un rechazo, `CONFIGURATION_RESTORE_REJECTED` con el motivo. Ninguno MUST llevar códigos, nombres ni contenido.

#### Scenario: La recuperación en la bitácora
Origen: RB21 («todo cambio … tiene permiso, actor y resultado»).
- **GIVEN** un resguardo recuperado
- **WHEN** se lee la general
- **THEN** tiene `CONFIGURATION_RESTORED` con la huella del archivo, la versión de origen y la nueva, y ningún código ni nombre de persona
