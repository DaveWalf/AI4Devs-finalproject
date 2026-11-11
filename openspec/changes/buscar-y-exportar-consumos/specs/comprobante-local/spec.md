## ADDED Requirements

### Requirement: Ver el comprobante sin imprimirlo

«Ver» en una fila de Consumos SHALL mostrar el comprobante en pantalla, con los mismos datos que el impreso y la marca «COPIA EN PANTALLA», sin pedir un operador. MUST NOT crear un trabajo de impresión ni una copia, ni cambiar consumo, cupo o importe, y MUST NOT mostrar el nombre de la persona ni el código.

#### Scenario: Ver un comprobante
Origen: mejora 6 de David del 10/11/2026 («con reimpresión virtual o real»).
- **GIVEN** un consumo en la tabla de Consumos, con su comprobante impreso
- **WHEN** se toca «Ver» en su fila
- **THEN** la pantalla muestra el comprobante con su número, la opción, la fecha de negocio, el cargo y la marca «COPIA EN PANTALLA»
- **AND** al cerrarlo, la pestaña Consumos sigue como estaba

#### Scenario: Ver no imprime ni crea copia
Origen: RB08 («copia posterior … queda marcada y auditada») y AT54, borde.
- **GIVEN** un comprobante sin copias
- **WHEN** se lo ve en pantalla dos veces
- **THEN** sigue sin copias y sin trabajos de impresión nuevos, y la impresora no recibió nada

### Requirement: El pedido del código de un operador en Consumos

«Reimprimir» o «Recuperar» sin una sesión de operador SHALL pedir el código diciendo qué acción y qué comprobante esperan. El pedido MUST cerrarse sin hacer nada con «Cancelar», también después de una denegación, al dejar la pestaña o a los 30 s sin un toque. La sesión del operador que entró a Configuración con su código MUST NOT cerrarse con «Salir» ni con los 30 s de Consumos: la cierra Configuración al cerrarse.

#### Scenario: El pedido dice qué va a hacer
Origen: la verificación de este change (un código tipeado minutos después hacía una acción que no se veía), borde de AT54.
- **GIVEN** Configuración abierta con la clave del día y un consumo con su comprobante
- **WHEN** se toca «Reimprimir» en su fila
- **THEN** la pestaña pide el código de un operador para reimprimir el comprobante con su número, y la impresora no recibe nada

#### Scenario: Un pedido que nadie atiende
Origen: D8 de configurar-puesto-por-pestanas (30 s sin un toque), borde.
- **GIVEN** «Recuperar» pidió el código de un operador para un original fallido
- **WHEN** pasan 30 s sin un toque
- **THEN** el pedido se cierra sin recuperar nada y la impresora no recibe ningún trabajo

#### Scenario: Cancelar después de una denegación
Origen: HU03-C («denegación sin generar trabajo ni alterar consumo»), borde.
- **GIVEN** el pedido de código con el aviso de un permiso vencido
- **WHEN** se toca «Cancelar»
- **THEN** el pedido y el aviso se cierran, y la tabla de Consumos sigue como estaba

#### Scenario: Salir no cierra la sesión de quien entró con su código
Origen: D5 de este change (la sesión adoptada), borde.
- **GIVEN** Configuración abierta con el código de un operador vigente, que pidió reimprimir desde Consumos
- **WHEN** toca «Salir» en Consumos y después cierra Configuración
- **THEN** la bitácora registra el cierre de su sesión una sola vez, con `CONFIGURATION_CLOSED`

## MODIFIED Requirements

### Requirement: Asistencia con permiso OPERATOR

Reimprimir o recuperar en Consumos SHALL pedir el código de un operador, salvo con la sesión del que entró a Configuración con el suyo. El código MUST identificar al actor y abrir una sesión privilegiada sólo con un permiso `OPERATOR` no revocado y vigente en `[ValidFromUtc, ValidToUtc)` en el snapshot activo (RB26); si no, MUST denegarla. Cada intento MUST auditarse con `RequestedRole`, su causa y, si existen, `PersonId`, `GrantId` y `GrantedRole`. Reposo MUST NOT ofrecer «Asistencia».

#### Scenario: Operador con permiso vigente
Origen: AT54 («sólo el operador vigente crea copia numerada») y la mejora 6 de David del 10/11/2026 (la reimpresión en Consumos).
- **GIVEN** una persona con un permiso `OPERATOR` vigente y sin revocar, y Configuración abierta con la clave del día
- **WHEN** toca «Reimprimir» en una fila de Consumos y tipea su código
- **THEN** existe una sesión de identidad de propósito `PRIVILEGED_ACTION`, la pestaña muestra su nombre y pide el motivo de la reimpresión
- **AND** la bitácora registra el acceso con `RequestedRole` `OPERATOR`, su `PersonId`, `GrantId` y `GrantedRole`

#### Scenario: Persona sin permiso
Origen: HU03-C («denegación sin generar trabajo ni alterar consumo»).
- **GIVEN** una persona sin ningún permiso de operador
- **WHEN** tipea su código para reimprimir en Consumos
- **THEN** la pestaña informa que no tiene permiso y sigue pidiendo un código, sin sesión nueva
- **AND** la bitácora registra la denegación con `PERMISSION_MISSING` y su `PersonId`, sin `GrantId`

#### Scenario: Permiso vencido
Origen: HU03-I y AT54 («operador … vencido»).
- **GIVEN** una persona cuyo permiso `OPERATOR` tiene `ValidToUtc` igual al instante del intento
- **WHEN** tipea su código para reimprimir en Consumos
- **THEN** se deniega con `PERMISSION_EXPIRED`, sin sesión, y la bitácora registra su `GrantId` y `GrantedRole`

#### Scenario: Permiso revocado
Origen: HU03-I y AT54 («operador … revocado»).
- **GIVEN** una persona cuyo permiso `OPERATOR` tiene fecha de revocación
- **WHEN** tipea su código para reimprimir en Consumos
- **THEN** se deniega con `PERMISSION_REVOKED`, sin sesión

#### Scenario: Administrador sin OPERATOR
Origen: AT54 («administrador sin OPERATOR») y RB26 («roles sin herencia»).
- **GIVEN** una persona con un permiso `SYSTEM_ADMINISTRATOR` vigente y ningún permiso `OPERATOR`
- **WHEN** tipea su código para reimprimir en Consumos
- **THEN** se deniega con `PERMISSION_MISSING`, sin sesión

#### Scenario: Autenticación fallida
Origen: AT54 («también autenticación fallida») y RB26 («PersonId sólo si se resolvió el actor»).
- **GIVEN** un código desconocido
- **WHEN** se lo tipea para reimprimir en Consumos
- **THEN** se deniega con `IDENTITY_NOT_RESOLVED`, sin sesión, y la bitácora registra `RequestedRole` `OPERATOR` sin `PersonId` y sin el código

#### Scenario: La sesión de asistencia no habilita consumo
Origen: RB26 («La sesión privilegiada no habilita consumo y se cierra al terminar o expirar»).
- **GIVEN** un operador con la sesión de asistencia abierta en Consumos
- **WHEN** se intenta confirmar una intención con esa sesión, y después el operador toca «Salir»
- **THEN** la confirmación responde que la sesión no es utilizable y no escribe nada
- **AND** al salir, Consumos vuelve a pedir un código para reimprimir, sin el nombre del operador, y la bitácora registra el cierre de la sesión

#### Scenario: Reposo sin «Asistencia»
Origen: pedido de David del 06/11/2026 («1. Eliminar el botón de Asistencia») y su decisión 2/3 del mismo día.
- **GIVEN** el puesto en reposo
- **WHEN** se revisa la pantalla
- **THEN** tiene el campo del código e «Ingresar», y ningún botón «Asistencia»

#### Scenario: Cerrar Configuración termina la sesión
Origen: HU03-E («vuelve a reposo sin privilegios ni identidad de la persona anterior»), borde.
- **GIVEN** un operador con la sesión abierta en Consumos
- **WHEN** se cierra Configuración con «Cerrar»
- **THEN** la sesión ya no es utilizable, y al volver a abrir Configuración, reimprimir pide un código

#### Scenario: Treinta segundos sin tocar la pestaña
Origen: RB26 («se cierra al terminar o expirar») y HU03-E («cierra o expira sesión»), borde.
- **GIVEN** un operador con la sesión abierta en Consumos cuyo último toque fue hace exactamente 30 s
- **WHEN** el puesto revisa la inactividad
- **THEN** la sesión termina y reimprimir vuelve a pedir un código, sin el nombre del operador

#### Scenario: Un operador que entró con su código
Origen: mejora 13 de David del 10/11/2026 (el operador entra con su código) y RB26, borde: no se le pide dos veces.
- **GIVEN** Configuración abierta con `9001`, cuyo permiso `OPERATOR` está vigente
- **WHEN** toca «Reimprimir» en una fila de Consumos
- **THEN** la pestaña pide el motivo sin pedir otro código, y la reimpresión queda en la bitácora con la persona de `9001`

### Requirement: Recuperar o reimprimir sin tocar el consumo

Con la sesión de asistencia, el operador SHALL poder recuperar desde la fila de Consumos un trabajo original en `FAILED`, que se vuelve a enviar como el mismo trabajo, o reimprimir el comprobante con un motivo de una lista fija. Reimprimir crea un trabajo con el número de copia siguiente y la leyenda DUPLICADO. Cada acción MUST comprobar de nuevo el permiso y MUST NOT cambiar consumo, cupo ni importe (RB08).

#### Scenario: Reimpresión con motivo
Origen: AT54 («crea copia numerada … sin cambio de consumo») y RB08.
- **GIVEN** un comprobante cuyo trabajo original está en `SUBMITTED` y un operador con permiso vigente
- **WHEN** lo reimprime desde su fila con el motivo «Papel ilegible»
- **THEN** existe un trabajo nuevo de copia 1 para el mismo comprobante y la impresora recibió el comprobante con la leyenda DUPLICADO y el número de copia
- **AND** el consumo, el uso de cupo y el importe son iguales antes y después, y la bitácora registra la acción, el motivo, el comprobante, la copia, el `PersonId` y el `GrantId`

#### Scenario: Segunda reimpresión
Origen: RB08 («copia posterior … queda marcada y auditada») y la unicidad `(TicketId, CopyNumber)`.
- **GIVEN** un comprobante que ya tiene la copia 1
- **WHEN** el operador lo reimprime otra vez
- **THEN** el trabajo nuevo es la copia 2

#### Scenario: Trabajo fallido por papel
Origen: HU03-A («conserva consumo/ticket; acción auditada según estado del trabajo») y AT06.
- **GIVEN** un comprobante cuyo trabajo original está en `FAILED` y la impresora ya con papel
- **WHEN** el operador lo recupera desde su fila
- **THEN** el mismo trabajo de copia 0 se envía otra vez y queda en `SUBMITTED`
- **AND** no se crea ningún trabajo nuevo y la bitácora registra la recuperación

#### Scenario: Resultado incierto
Origen: HU03-B («aviso de incertidumbre, número de copia y leyenda DUPLICADO»).
- **GIVEN** un comprobante cuyo último trabajo está en `UNKNOWN`
- **WHEN** el operador elige reimprimirlo
- **THEN** la pantalla avisa que el papel pudo haber salido antes de pedir el motivo
- **AND** al confirmar, la copia nueva lleva su número y la leyenda DUPLICADO

#### Scenario: El permiso vence durante la sesión
Origen: RB26 («comprobar el permiso en cada acción») y AT54 («siguiente acción rechazada»).
- **GIVEN** un operador que abrió la sesión de asistencia con un permiso cuyo `ValidToUtc` llega antes de su acción
- **WHEN** intenta reimprimir
- **THEN** la acción se deniega con `PERMISSION_EXPIRED`, sin trabajo nuevo, y Consumos termina la sesión y vuelve a pedir un código con el aviso de ese motivo
