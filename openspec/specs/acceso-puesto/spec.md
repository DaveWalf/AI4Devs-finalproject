# acceso-puesto Specification

## Purpose
Decidir quién entra a Configuración y quién cierra el puesto: los modos de mantenimiento y operativo, la clave del día que pide el operativo, el cierre sin botón de cierre y el registro de cada acceso, sin que el modo cambie lo que la atención decide.

## Requirements

### Requirement: Dos modos, con mantenimiento por defecto

El puesto SHALL tener dos modos, mantenimiento y operativo, y conservar el elegido en su carpeta de datos para el próximo arranque. Sin modo guardado, o con uno ilegible o desconocido, MUST arrancar en mantenimiento. Los dos modos MUST atender igual: el modo sólo decide la entrada a Configuración y el cierre.

#### Scenario: Una carpeta nueva arranca en mantenimiento
Origen: decisión de David del 03/11/2026 («el software debe tener 2 modos») y el criterio 1 de la maqueta («iniciar … en mantenimiento»).
- **GIVEN** una carpeta de datos sin modo guardado
- **WHEN** el puesto arranca
- **THEN** está en mantenimiento y la pestaña General de Configuración dice «Modo: mantenimiento»

#### Scenario: El modo operativo se conserva al volver a abrir
Origen: decisión de David del 03/11/2026 («operativo, que para activarlo requerirá la clave del día»).
- **GIVEN** el puesto en operativo
- **WHEN** se cierra con la clave del día y se vuelve a abrir
- **THEN** arranca en operativo

#### Scenario: Un modo guardado ilegible
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** un archivo de modo con texto que no es un modo conocido
- **WHEN** el puesto arranca
- **THEN** está en mantenimiento y atiende normalmente

#### Scenario: En operativo la atención no pide clave
Origen: AT01 y la decisión de David del 03/11/2026 (los modos deciden el acceso a Configuración y el cierre), borde.
- **GIVEN** el puesto en operativo y `0001` con cupo libre
- **WHEN** `0001` se identifica, elige una opción y confirma
- **THEN** la decisión es AUTHORIZED, con un solo `IntentId`, sin que se pida ninguna clave

### Requirement: Clave del día de desarrollo

Cuando el puesto la pide, SHALL mostrar el diálogo de la clave del día: un campo enmascarado, un teclado numérico en pantalla con la disposición del teclado extendido y «ENTER», «Cancelar» y el aviso de desarrollo; el teclado físico MUST tipear también. En este corte MUST aceptar cualquier número de 1 a 12 dígitos; otro valor MUST rechazarse sin ejecutar la acción. Lo tipeado MUST NOT guardarse ni registrarse.

#### Scenario: Cualquier número en desarrollo
Origen: decisión de David del 03/11/2026 («debe aparecer la ventana de la clave del día, aceptando cualquier número») y su mejora 3 del 10/11/2026 («aceptando cualquier cosa, hasta tanto tenga esa funcionalidad implementada … al dar ENTER, aclarar que todavía se encuentra sin validar ese número e ingresar»).
- **GIVEN** el puesto en reposo, en mantenimiento y, otra vez, en operativo
- **WHEN** se toca el engranaje, se tocan 4, 7, 1 y 1 en el teclado en pantalla y se toca «ENTER»
- **THEN** se abre Configuración con el aviso de que la clave del día no se validó
- **AND** el diálogo de la clave mostraba el aviso de desarrollo

#### Scenario: El teclado en pantalla
Origen: mejora 3 de David del 10/11/2026 («un miniteclado tipo calculadora con la disposición del teclado extendido, y el botón ENTER») y su decisión del 03/11/2026 (UX táctil, controles de al menos 56 px).
- **GIVEN** el diálogo de la clave del día abierto
- **WHEN** se revisa el teclado y se tocan 4 y 7
- **THEN** tiene los diez dígitos en el orden del teclado numérico extendido, con 7, 8 y 9 arriba y el 0 ancho abajo, «Limpiar» y «ENTER» altos a la derecha y «Borrar» al lado del 0, cada tecla de al menos 56 px
- **AND** el campo muestra dos puntos, nunca los dígitos

#### Scenario: El teclado físico también tipea
Origen: mejora 3 de David del 10/11/2026 («para poder tipear en pantalla») y TK02.1 («operable por toque y teclado»), borde: un teclado conectado sigue sirviendo.
- **GIVEN** el diálogo de la clave del día abierto para entrar a Configuración
- **WHEN** se tipea `4711` con el teclado físico y se pulsa Enter
- **THEN** se abre Configuración como con el teclado en pantalla
- **AND** el campo pide no abrir el teclado táctil del sistema

#### Scenario: Clave vacía o con letras
Origen: criterio 2 de la maqueta («pedir números no vacíos») y `AGENTS.md` (OWASP: el código tipeado), borde.
- **GIVEN** el diálogo de la clave del día abierto para entrar a Configuración
- **WHEN** se toca «ENTER» con el campo vacío y, después, se tipea `12a4` con el teclado físico y se toca «ENTER»
- **THEN** las dos veces el diálogo dice que la clave no es válida y Configuración sigue cerrada

#### Scenario: Cancelar no ejecuta la acción
Origen: criterio 2 de la maqueta («cancelar o Escape dentro del diálogo conserva el estado y no ejecuta la acción pendiente»).
- **GIVEN** el diálogo de la clave del día abierto para cerrar el puesto
- **WHEN** se toca «Cancelar» o se pulsa Escape
- **THEN** el diálogo se cierra, el puesto sigue abierto en reposo y no existe ningún `KIOSK_STOPPED` nuevo

#### Scenario: La clave no queda en ningún lado
Origen: criterio 2 de la maqueta («no guardar la entrada»), RB21 y AT56 («se deniegan y auditan sin secretos»).
- **GIVEN** el puesto en operativo
- **WHEN** se entra a Configuración con la clave `4711` y se la cierra
- **THEN** ningún valor de texto de la base, de las dos bitácoras ni de los archivos de la carpeta de datos contiene `4711`, fuera de los UUID, que pueden contener esos dígitos

### Requirement: Cierre sin botón de cierre

La ventana SHALL ocupar la pantalla completa, sin bordes ni barra de título, en los dos modos, y no ofrecer botones de cerrar, minimizar ni restaurar. Escape en reposo sin diálogos abiertos, o «Cerrar el puesto» en la pestaña General, MUST cerrar el puesto en orden: directo en mantenimiento y, en operativo, después de la clave del día. En operativo, cualquier otro pedido de cierre de la ventana MUST convertirse en el pedido de la clave; el apagado de Windows MUST NOT frenarse.

#### Scenario: Ningún botón de cierre
Origen: decisión de David del 03/11/2026 («la pantalla principal no debe mostrar un botón de cierre») y la del 06/11/2026 (gate de D, punto 7: «pantalla completa sin bordes, en los dos modos»).
- **GIVEN** el puesto abierto, en cualquiera de los dos modos
- **WHEN** se revisa la ventana
- **THEN** está maximizada a pantalla completa, sin bordes ni barra de título, cubre el monitor entero con la barra de tareas tapada y no tiene botones de cerrar, minimizar ni restaurar

#### Scenario: Escape en mantenimiento
Origen: tabla de modos de la decisión de David del 03/11/2026 («Cerrar por Escape: directo, según la excepción de mantenimiento»).
- **GIVEN** el puesto en mantenimiento y en reposo, sin diálogos abiertos
- **WHEN** se pulsa Escape
- **THEN** el puesto se cierra en orden y la bitácora general termina con `KIOSK_STOPPED`

#### Scenario: Escape en operativo
Origen: decisión de David del 03/11/2026 («ante la presión de la tecla escape debe consultar la clave del día … lo cual habilita su cierre»).
- **GIVEN** el puesto en operativo y en reposo
- **WHEN** se pulsa Escape
- **THEN** se muestra el diálogo de la clave del día y el puesto se cierra en orden recién al aceptarla

#### Scenario: Otro pedido de cierre en operativo
Origen: decisión de David del 03/11/2026 (el cierre en operativo exige la clave), borde: Alt+F4 o un pedido de cierre de otro programa.
- **GIVEN** el puesto en operativo
- **WHEN** la ventana recibe un pedido de cierre que no vino de la clave
- **THEN** sigue abierta y muestra el diálogo de la clave del día

#### Scenario: Escape durante una atención
Origen: AT16 y RB05 (una atención no se interrumpe por fuera de sus acciones), borde.
- **GIVEN** el puesto en mantenimiento y una persona en la selección
- **WHEN** se pulsa Escape
- **THEN** el puesto sigue abierto con la misma persona y la misma elección

### Requirement: Cada acceso queda en la bitácora general

Cada clave del día pedida con su resultado y su acción, cada entrada a Configuración, cada cambio de modo y cada cierre SHALL quedar en la bitácora general con cómo se entró o se cerró: `MAINTENANCE_MODE`, `DAILY_KEY` u `OPERATOR`. Con la clave o en mantenimiento, el actor MUST ser nulo con `NO_AUTHENTICATION`; con un operador, MUST ser su persona. Ningún registro MUST contener lo tipeado.

#### Scenario: Entrada con la clave del día
Origen: RB21 («todo cambio/prueba tiene permiso, actor y resultado») y AT41.
- **GIVEN** el puesto en mantenimiento y, otra vez, en operativo
- **WHEN** se entra a Configuración con la clave del día
- **THEN** cada vez la general tiene la clave aceptada para abrir Configuración y `CONFIGURATION_OPENED` con `DAILY_KEY`, los dos con el actor nulo y `NO_AUTHENTICATION`

#### Scenario: Entrada de un operador
Origen: RB21, RB26 y la mejora 13 de David del 10/11/2026 («si el que está logueado es un usuario operador»).
- **GIVEN** el puesto en reposo
- **WHEN** se entra a Configuración con `9001`
- **THEN** la general tiene `CONFIGURATION_OPENED` con `OPERATOR` y la persona de `9001` como actor
- **AND** ningún registro de las dos bitácoras contiene `9001`

#### Scenario: Clave rechazada
Origen: AT56 («los demás casos se deniegan y auditan sin secretos»), borde.
- **GIVEN** el diálogo de la clave del día abierto para cerrar el puesto
- **WHEN** se toca «ENTER» con `12a4`
- **THEN** la general tiene la clave rechazada por formato para cerrar el puesto, sin `12a4`

#### Scenario: Cambio de modo
Origen: RB21 y AT37 («cambio válido versionado/auditado»).
- **GIVEN** Configuración abierta en mantenimiento, a la que se entró con la clave del día
- **WHEN** se pasa a operativo con la clave del día
- **THEN** la general tiene `CONFIGURATION_CHANGED` con el modo, antes mantenimiento y después operativo, el resultado aplicado y `DAILY_KEY`

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

### Requirement: Entrada a Configuración con la clave o un operador

En reposo, el engranaje SHALL pedir, en los dos modos, la clave del día o el código de un operador, y abrir Configuración recién al aceptarlos. Pasar a operativo desde la pestaña General MUST pedir la clave del día; volver a mantenimiento desde ahí MUST hacerse sin pedirla otra vez, porque ya se entró con ella.

#### Scenario: Mantenimiento también pide la clave
Origen: mejora 3 de David del 10/11/2026 («cuando toco el menú configuración, por ahora me debe pedir la clave del día»), que reemplaza «entrar en el modo configuración directamente» de su decisión del 03/11/2026.
- **GIVEN** el puesto en mantenimiento y en reposo
- **WHEN** se toca el engranaje
- **THEN** se muestra el diálogo de la clave del día y Configuración sigue cerrada hasta aceptarla

#### Scenario: Operativo pide la clave
Origen: decisión de David del 03/11/2026 (operativo «exigirá clave del día tanto para cerrar como para entrar en configuración»).
- **GIVEN** el puesto en operativo y en reposo
- **WHEN** se toca el engranaje
- **THEN** se muestra el diálogo de la clave del día y Configuración sigue cerrada hasta aceptarla

#### Scenario: Pasar a operativo pide la clave
Origen: tabla de modos de la decisión de David del 03/11/2026 («Activar operativo: exige clave del día»), con el borde de cancelar.
- **GIVEN** Configuración abierta en mantenimiento, en la pestaña General
- **WHEN** se toca «Pasar a operativo» y se cancela la clave, y después se vuelve a tocar y se acepta
- **THEN** después de cancelar sigue en mantenimiento, y después de aceptar dice «Modo: operativo»

#### Scenario: Volver a mantenimiento
Origen: la transición de operativo a mantenimiento que la nota de modos dejó por precisar, borde.
- **GIVEN** Configuración abierta en operativo, a la que se entró con la clave del día
- **WHEN** en la pestaña General se toca «Pasar a mantenimiento»
- **THEN** dice «Modo: mantenimiento» sin pedir otra clave, y el próximo arranque es en mantenimiento

### Requirement: Acceso de operador a Configuración

Con el código de un operador con `OPERATOR` vigente (RB26), el diálogo de la clave SHALL abrir Configuración sólo con Consumos, Personas e Impresora. Un código del snapshot activo MUST tratarse como el de un operador y nunca como clave del día, con los rechazos de RB25 y RB26, y MUST NOT cerrar el puesto ni pasar a operativo. Lo guardado en esa sesión MUST registrar al operador como actor.

#### Scenario: Un operador entra con su código
Origen: mejora 13 de David del 10/11/2026 («sólo debe habilitarle las secciones de Consumos, Personas e Impresora por ahora») y RB26.
- **GIVEN** el puesto en reposo, en mantenimiento y, otra vez, en operativo
- **WHEN** se toca el engranaje, se tipea `9001` y se toca «ENTER»
- **THEN** Configuración muestra sólo las pestañas Consumos, Personas e Impresora, con Consumos elegida, y ni General ni «Cerrar el puesto»
- **AND** no muestra el aviso de la clave sin validar

#### Scenario: Un permiso de operador vencido
Origen: RB26 y la tabla de códigos sintéticos del `readme.md` (`9002`, «operador con el permiso vencido»), borde.
- **GIVEN** el diálogo de la clave abierto para entrar a Configuración
- **WHEN** se tipea `9002` y se toca «ENTER»
- **THEN** el diálogo dice «Tu permiso de operador está vencido» y Configuración sigue cerrada
- **AND** `9002` no se aceptó como clave del día

#### Scenario: El código de una persona sin OPERATOR
Origen: RB26 y la tabla de códigos sintéticos del `readme.md` (`9004`, «administrador sin `OPERATOR`»), borde.
- **GIVEN** el diálogo de la clave abierto para entrar a Configuración
- **WHEN** se tipea `9004` y se toca «ENTER», y después `0001`
- **THEN** las dos veces el diálogo muestra el motivo del rechazo y Configuración sigue cerrada

#### Scenario: Un operador no cierra el puesto
Origen: mejora 13 de David del 10/11/2026 («sin permitirle alterar ninguna otra configuración significativa ni cerrar al programa»), borde.
- **GIVEN** el puesto en operativo y en reposo
- **WHEN** se pulsa Escape, se tipea `9001` y se toca «ENTER»
- **THEN** el diálogo dice que el código de un operador no cierra el puesto, el puesto sigue abierto y no existe ningún `KIOSK_CLOSE_REQUESTED` nuevo

#### Scenario: Lo guardado por un operador
Origen: RB21 («todo cambio/prueba tiene permiso, actor y resultado») y la mejora 5 de David del 10/11/2026 («qué operador hizo qué cambios a quién y cuándo»), borde.
- **GIVEN** Configuración abierta con `9001`, en la pestaña Personas
- **WHEN** se agrega «Persona sintética 05» y se guarda
- **THEN** el `CONFIGURATION_CHANGED` de las personas tiene como actor a la persona de `9001`, sin ningún código
