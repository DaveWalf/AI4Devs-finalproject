## ADDED Requirements

### Requirement: Quién habilita reimprimir o recuperar en Consumos

«Reimprimir» y «Recuperar» en Consumos SHALL usar el acceso con que se entró a Configuración, sin pedir otro código. Con el código de un operador, MUST usar su sesión privilegiada y comprobar su permiso `OPERATOR` en cada acción (RB26); con la clave del día, MUST hacerse sin sesión y con el actor nulo. La sesión del operador MUST NOT habilitar consumo y MUST cerrarse al cerrar Configuración. Reposo MUST NOT ofrecer «Asistencia».

#### Scenario: Con la clave del día no se pide un código
Origen: mejora 1 de David del 11/11/2026 («La reimpresión de tickets no debe solicitar un código extra») y AT54.
- **GIVEN** Configuración abierta con la clave del día y un consumo con su comprobante
- **WHEN** se toca «Reimprimir» en su fila
- **THEN** la pestaña pide el motivo de la reimpresión sin pedir un código, y no tiene ningún campo para tipearlo

#### Scenario: Un operador que entró con su código
Origen: mejora 13 de David del 10/11/2026 (el operador entra con su código) y RB26, borde: no se le pide dos veces.
- **GIVEN** Configuración abierta con `9001`, cuyo permiso `OPERATOR` está vigente
- **WHEN** toca «Reimprimir» en una fila de Consumos
- **THEN** la pestaña pide el motivo sin pedir otro código, y la reimpresión queda en la bitácora con la persona de `9001`

#### Scenario: La sesión del operador no habilita consumo
Origen: RB26 («La sesión privilegiada no habilita consumo y se cierra al terminar o expirar»), borde.
- **GIVEN** Configuración abierta con `9001`
- **WHEN** se intenta confirmar una intención con la sesión de esa entrada
- **THEN** la confirmación responde que la sesión no es utilizable y no escribe nada

#### Scenario: Cerrar Configuración termina la sesión
Origen: HU03-E («vuelve a reposo sin privilegios ni identidad de la persona anterior») y D5 de `buscar-y-exportar-consumos`, borde.
- **GIVEN** Configuración abierta con `9001`, que reimprimió un comprobante desde Consumos
- **WHEN** se cierra Configuración con «Cerrar»
- **THEN** la sesión ya no es utilizable y la bitácora registra su cierre una sola vez, con `CONFIGURATION_CLOSED`

#### Scenario: La sesión del operador vence durante Configuración
Origen: decisión de David del 11/11/2026 sobre la advertencia de la verificación (opción a), borde: Consumos ya no tiene dónde pedir el código.
- **GIVEN** Configuración abierta con `9001` y su sesión vencida, a los 2 minutos de entrar
- **WHEN** toca «Reimprimir» en una fila de Consumos y elige un motivo
- **THEN** la acción se deniega sin trabajo nuevo, y la pestaña avisa «La sesión del operador terminó. Cerrá Configuración y volvé a entrar con tu código.» sin pedir un código

#### Scenario: Reposo sin «Asistencia»
Origen: pedido de David del 06/11/2026 («1. Eliminar el botón de Asistencia») y su decisión 2/3 del mismo día.
- **GIVEN** el puesto en reposo
- **WHEN** se revisa la pantalla
- **THEN** tiene el campo del código e «Ingresar», y ningún botón «Asistencia»

## MODIFIED Requirements

### Requirement: Recuperar o reimprimir sin tocar el consumo

Desde la fila de Consumos SHALL poder recuperarse un trabajo original en `FAILED`, que se vuelve a enviar como el mismo trabajo, o reimprimirse el comprobante con un motivo de una lista fija. Reimprimir crea un trabajo con la copia siguiente, la leyenda REIMPRESIÓN y «Habilitó:» con el operador o, con la clave del día, «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD». Las acciones MUST NOT cambiar consumo, cupo ni importe (RB08) y MUST quedar en la bitácora con su actor.

#### Scenario: Reimpresión con motivo
Origen: AT54 («crea copia numerada … sin cambio de consumo»), RB08 y la mejora 9b de David del 10/11/2026 («REIMPRESIÓN o el operador que habilitó»).
- **GIVEN** un comprobante cuyo trabajo original está en `SUBMITTED`, y Configuración abierta con el código de un operador con permiso vigente
- **WHEN** lo reimprime desde su fila con el motivo «Papel ilegible»
- **THEN** existe un trabajo nuevo de copia 1 para el mismo comprobante y la impresora recibió el comprobante con la leyenda REIMPRESIÓN, el número de copia y «Habilitó:» con el nombre del operador
- **AND** el consumo, el uso de cupo y el importe son iguales antes y después, y la bitácora registra la acción, el motivo, el comprobante, la copia, el `PersonId` y el `GrantId`

#### Scenario: Reimpresión con la clave del día
Origen: AT54 y RB08, con la respuesta 1 de David al gate del 11/11/2026 («Si no hay identificación debe indicar claramente: "SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD"»).
- **GIVEN** un comprobante cuyo trabajo original está en `SUBMITTED`, y Configuración abierta con la clave del día
- **WHEN** se lo reimprime desde su fila con el motivo «Papel ilegible»
- **THEN** existe un trabajo nuevo de copia 1 y la impresora recibió el comprobante con la leyenda REIMPRESIÓN, el número de copia y la línea «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD», sin «Habilitó:»
- **AND** el consumo, el uso de cupo y el importe son iguales antes y después, y `TICKET_REPRINTED` tiene el motivo, el comprobante, la copia, el actor nulo con `NO_AUTHENTICATION` y `Access` `DAILY_KEY`, sin `PersonId` ni `GrantId`

#### Scenario: El aviso de una copia sin validez
Origen: respuesta 1 de David al gate del 11/11/2026 («debe indicar claramente») y AT54, borde: se avisa antes de imprimir.
- **GIVEN** Configuración abierta con la clave del día
- **WHEN** se toca «Reimprimir» en una fila de Consumos
- **THEN** junto a los motivos, la pestaña avisa que la copia sale marcada «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD» porque se entró con la clave del día, y la impresora todavía no recibió nada

#### Scenario: Segunda reimpresión
Origen: RB08 («copia posterior … queda marcada y auditada») y la unicidad `(TicketId, CopyNumber)`.
- **GIVEN** un comprobante que ya tiene la copia 1
- **WHEN** se lo reimprime otra vez
- **THEN** el trabajo nuevo es la copia 2

#### Scenario: Trabajo fallido por papel
Origen: HU03-A («conserva consumo/ticket; acción auditada según estado del trabajo») y AT06.
- **GIVEN** un comprobante cuyo trabajo original está en `FAILED` y la impresora ya con papel
- **WHEN** se lo recupera desde su fila
- **THEN** el mismo trabajo de copia 0 se envía otra vez, sin leyenda ni operador, y queda en `SUBMITTED`
- **AND** no se crea ningún trabajo nuevo y la bitácora registra la recuperación

#### Scenario: Recuperar con la clave del día
Origen: HU03-A y AT06, con la respuesta 2 de David al gate del 11/11/2026 («Recuperar» tampoco pide el código), borde: el original no lleva la marca.
- **GIVEN** Configuración abierta con la clave del día, un comprobante cuyo trabajo original está en `FAILED` y la impresora ya con papel
- **WHEN** se toca «Recuperar» en su fila
- **THEN** sin pedir un código, el mismo trabajo de copia 0 se envía otra vez sin leyenda, sin «Habilitó:» y sin «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD», y queda en `SUBMITTED`
- **AND** `TICKET_RECOVERED` tiene el actor nulo con `NO_AUTHENTICATION` y `Access` `DAILY_KEY`

#### Scenario: Resultado incierto
Origen: HU03-B («aviso de incertidumbre, número de copia y leyenda DUPLICADO»), con la leyenda de la mejora 9b.
- **GIVEN** un comprobante cuyo último trabajo está en `UNKNOWN`
- **WHEN** se elige reimprimirlo
- **THEN** la pantalla avisa que el papel pudo haber salido antes de pedir el motivo
- **AND** al confirmar, la copia nueva lleva su número y la leyenda REIMPRESIÓN

#### Scenario: El permiso vence durante la sesión
Origen: RB26 («comprobar el permiso en cada acción») y AT54 («siguiente acción rechazada»), borde.
- **GIVEN** Configuración abierta con el código de un operador cuyo permiso tiene un `ValidToUtc` que llega antes de su acción
- **WHEN** intenta reimprimir
- **THEN** la acción se deniega con `PERMISSION_EXPIRED`, sin trabajo nuevo, y la pestaña muestra el aviso de ese motivo sin pedir un código
- **AND** la acción siguiente desde Consumos también se deniega, sin trabajo nuevo

## REMOVED Requirements

### Requirement: Asistencia con permiso OPERATOR

**Reason**: Pedía el código de un operador en Consumos a quien entró con la clave del día; la mejora 1 de David del 11/11/2026 lo saca. Los rechazos de RB25 y RB26 al tipear un código (sin permiso, vencido, revocado, administrador sin `OPERATOR`, código desconocido) ya los cubre el engranaje en «Acceso de operador a Configuración» de `acceso-puesto`.

**Migration**: Lo reemplaza «Quién habilita reimprimir o recuperar en Consumos», que conserva el operador que entró con su código, la sesión que no habilita consumo, su cierre con Configuración y el reposo sin «Asistencia». Se quitan la sesión de 30 s propia de Consumos y su «Salir».

### Requirement: El pedido del código de un operador en Consumos

**Reason**: Consumos deja de pedir un código: «Reimprimir» y «Recuperar» usan el acceso con que se entró a Configuración (mejora 1 de David del 11/11/2026 y su respuesta 2 al gate).

**Migration**: No hay pedido que cancelar ni que expire. El cierre de la sesión del operador con Configuración pasa al escenario «Cerrar Configuración termina la sesión» de «Quién habilita reimprimir o recuperar en Consumos».
