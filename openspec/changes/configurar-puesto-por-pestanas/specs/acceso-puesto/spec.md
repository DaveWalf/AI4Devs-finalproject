# Spec Delta

## Purpose

Decidir quién entra a Configuración y quién cierra el puesto: los modos de mantenimiento y operativo, la clave del día que pide el operativo, el cierre sin botón de cierre y el registro de cada acceso, sin que el modo cambie lo que la atención decide.

## ADDED Requirements

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

Cuando el modo la pide, el puesto SHALL mostrar el diálogo de la clave del día: un campo numérico enmascarado, «Aceptar», «Cancelar» y el aviso de que en desarrollo se acepta cualquier número y en la versión operativa la valida INWeb. En este corte MUST aceptar cualquier número de 1 a 12 dígitos; otro valor MUST rechazarse sin ejecutar la acción. Lo tipeado MUST NOT guardarse ni registrarse.

#### Scenario: Cualquier número en desarrollo
Origen: decisión de David del 03/11/2026 («Mientras el software no esté operativo, debe aparecer la ventana de la clave del día, aceptando cualquier número»).
- **GIVEN** el puesto en operativo y en reposo
- **WHEN** se toca el engranaje, se tipea `4711` y se toca «Aceptar»
- **THEN** se abre Configuración
- **AND** el diálogo de la clave mostraba el aviso de desarrollo

#### Scenario: Clave vacía o con letras
Origen: criterio 2 de la maqueta («pedir números no vacíos») y `AGENTS.md` (OWASP: el código tipeado), borde.
- **GIVEN** el diálogo de la clave del día abierto para entrar a Configuración
- **WHEN** se toca «Aceptar» con el campo vacío y, después, con `12a4`
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

### Requirement: Entrada a Configuración según el modo

En reposo, el engranaje SHALL abrir Configuración directamente en mantenimiento y, en operativo, después de aceptada la clave del día. Pasar a operativo desde la pestaña General MUST pedir la clave del día; volver a mantenimiento desde ahí MUST hacerse sin pedirla otra vez, porque ya se entró con ella.

#### Scenario: Mantenimiento entra directo
Origen: decisión de David del 03/11/2026 (mantenimiento «permite … entrar en el modo configuración directamente»).
- **GIVEN** el puesto en mantenimiento y en reposo
- **WHEN** se toca el engranaje
- **THEN** se abre Configuración sin pedir ninguna clave

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

### Requirement: Cierre sin botón de cierre

La barra de título de la ventana SHALL quedar sin botones. Escape en reposo sin diálogos abiertos, o «Cerrar el puesto» en la pestaña General, MUST cerrar el puesto en orden: directo en mantenimiento y, en operativo, después de la clave del día. En operativo, cualquier otro pedido de cierre de la ventana MUST convertirse en el pedido de la clave; el apagado de Windows MUST NOT frenarse.

#### Scenario: Ningún botón de cierre
Origen: decisión de David del 03/11/2026 («la pantalla principal no debe mostrar un botón de cierre»).
- **GIVEN** el puesto abierto, en cualquiera de los dos modos
- **WHEN** se revisa la ventana
- **THEN** está maximizada y su barra de título no tiene botones de cerrar, minimizar ni restaurar

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

Cada clave del día pedida con su resultado y su acción, cada entrada a Configuración, cada cambio de modo y cada cierre SHALL quedar en la bitácora general con el actor nulo y `NO_AUTHENTICATION`, porque la clave no identifica a nadie, y con cómo se entró: `MAINTENANCE_MODE` o `DAILY_KEY`. Ningún registro MUST contener lo tipeado.

#### Scenario: Entrada con la clave del día
Origen: RB21 («todo cambio/prueba tiene permiso, actor y resultado») y AT41.
- **GIVEN** el puesto en operativo
- **WHEN** se entra a Configuración con la clave del día
- **THEN** la general tiene la clave aceptada para abrir Configuración y `CONFIGURATION_OPENED` con `DAILY_KEY`, los dos con el actor nulo y `NO_AUTHENTICATION`

#### Scenario: Clave rechazada
Origen: AT56 («los demás casos se deniegan y auditan sin secretos»), borde.
- **GIVEN** el diálogo de la clave del día abierto para cerrar el puesto
- **WHEN** se toca «Aceptar» con `12a4`
- **THEN** la general tiene la clave rechazada por formato para cerrar el puesto, sin `12a4`

#### Scenario: Cambio de modo
Origen: RB21 y AT37 («cambio válido versionado/auditado»).
- **GIVEN** Configuración abierta en mantenimiento
- **WHEN** se pasa a operativo con la clave del día
- **THEN** la general tiene `CONFIGURATION_CHANGED` con el modo, antes mantenimiento y después operativo, el resultado aplicado y `DAILY_KEY`
