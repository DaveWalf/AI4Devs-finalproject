## MODIFIED Requirements

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

## ADDED Requirements

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

## REMOVED Requirements

### Requirement: Entrada a Configuración según el modo
**Reason**: El 10/11/2026 David pidió que el engranaje pida la clave del día también en mantenimiento (mejora 3) y que un operador pueda entrar con su código (mejora 13), así que mantenimiento deja de entrar directo. Lo reemplaza «Entrada a Configuración con la clave o un operador», que conserva «Operativo pide la clave», «Pasar a operativo pide la clave» y «Volver a mantenimiento».
**Migration**: El escenario «Mantenimiento entra directo» pasa a «Mantenimiento también pide la clave». Quien entraba directo en mantenimiento entra ahora con cualquier número de 1 a 12 dígitos, o con el código de un operador.
