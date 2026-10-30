# Spec Delta

## Purpose

Llevar el comprobante de un consumo ya confirmado hasta la impresora sin tocar el consumo (RB17), dejar un estado durable que distinga lo enviado de lo incierto, y permitir que un operador con permiso `OPERATOR` lo recupere o lo reimprima como copia auditada (RB08, RB26).

## ADDED Requirements

### Requirement: Envío del comprobante después del commit

Después de una autorización, el puesto SHALL enviar a la impresora el trabajo original, fuera de la transacción de RB06. Antes de enviarlo MUST dejarlo en `UNKNOWN` de forma durable y, con la respuesta, pasarlo a `SUBMITTED` o `FAILED`. Cada cambio de estado MUST quedar en la bitácora. Un fallo MUST NOT tocar el consumo, el cupo ni el comprobante (RB05, RB17).

#### Scenario: Impresora disponible
Origen: AT01 («ticket lógico asociado») y RB17.
- **GIVEN** una autorización recién confirmada y la impresora disponible
- **WHEN** el puesto envía el trabajo original
- **THEN** el trabajo de copia 0 queda en `SUBMITTED`, la impresora recibió el comprobante sin la leyenda DUPLICADO y sin ningún código de identificación
- **AND** la bitácora tiene un registro de ese cambio de estado

#### Scenario: Impresora sin papel
Origen: AT06 («consumo preservado, trabajo recuperable») y «Sin papel» de `docs/experiencia.md`.
- **GIVEN** una autorización recién confirmada y la impresora sin papel
- **WHEN** el puesto envía el trabajo original
- **THEN** el trabajo queda en `FAILED` con su código de error y la pantalla muestra «Tu consumo está registrado. El comprobante queda pendiente»
- **AND** el consumo, el uso de cupo y el comprobante son los mismos que antes del envío, y sigue habiendo un solo trabajo

#### Scenario: Respuesta incierta de la impresora
Origen: HU03-B («papel pudo salir, pero el resultado es incierto») y RB08 («Spooler no prueba papel entregado»).
- **GIVEN** una autorización recién confirmada y una impresora que no puede confirmar si imprimió
- **WHEN** el puesto envía el trabajo original
- **THEN** el trabajo queda en `UNKNOWN`

#### Scenario: Caída durante el envío
Origen: RB17 y la SDD («Si se desconoce si salió papel, un operador decide una copia marcada; no se promete una única impresión física»).
- **GIVEN** un trabajo original que quedó en `UNKNOWN` porque el proceso cayó antes de la respuesta de la impresora
- **WHEN** el puesto vuelve a arrancar
- **THEN** el trabajo sigue en `UNKNOWN` y la impresora no lo recibe otra vez

#### Scenario: Arranque con un trabajo en cola
Origen: AT05 («un solo trabajo original») y RB17 («recuperar retoma el trabajo existente sin reconstruir ni crear otro original»).
- **GIVEN** un consumo confirmado cuyo trabajo original quedó en `QUEUED` porque el proceso cayó después del commit
- **WHEN** el puesto vuelve a arrancar
- **THEN** envía ese mismo trabajo una sola vez y queda en `SUBMITTED`, sin crear otro

### Requirement: Asistencia con permiso OPERATOR

Desde reposo, «Asistencia» SHALL identificar al actor por su código, como la atención, y abrir una sesión de acción privilegiada sólo si tiene un permiso `OPERATOR` no revocado y vigente en `[ValidFromUtc, ValidToUtc)` en el snapshot activo (RB26). Si no, MUST denegar sin abrir sesión. Cada intento MUST auditarse con `RequestedRole` y su causa; `PersonId`, `GrantId` y `GrantedRole`, sólo si existen.

#### Scenario: Operador con permiso vigente
Origen: AT54 («sólo el operador vigente crea copia numerada»).
- **GIVEN** una persona con un permiso `OPERATOR` vigente y sin revocar
- **WHEN** entra por «Asistencia» con su código
- **THEN** existe una sesión de identidad de propósito `PRIVILEGED_ACTION` y la pantalla muestra los comprobantes de la fecha de negocio
- **AND** la bitácora registra el acceso con `RequestedRole` `OPERATOR`, su `PersonId`, `GrantId` y `GrantedRole`

#### Scenario: Persona sin permiso
Origen: HU03-C («denegación sin generar trabajo ni alterar consumo»).
- **GIVEN** una persona sin ningún permiso de operador
- **WHEN** entra por «Asistencia» con su código
- **THEN** la pantalla informa que no tiene permiso y vuelve a reposo, sin sesión nueva
- **AND** la bitácora registra la denegación con `PERMISSION_MISSING` y su `PersonId`, sin `GrantId`

#### Scenario: Permiso vencido
Origen: HU03-I y AT54 («operador … vencido»).
- **GIVEN** una persona cuyo permiso `OPERATOR` tiene `ValidToUtc` igual al instante del intento
- **WHEN** entra por «Asistencia»
- **THEN** se deniega con `PERMISSION_EXPIRED`, sin sesión, y la bitácora registra su `GrantId` y `GrantedRole`

#### Scenario: Permiso revocado
Origen: HU03-I y AT54 («operador … revocado»).
- **GIVEN** una persona cuyo permiso `OPERATOR` tiene fecha de revocación
- **WHEN** entra por «Asistencia»
- **THEN** se deniega con `PERMISSION_REVOKED`, sin sesión

#### Scenario: Administrador sin OPERATOR
Origen: AT54 («administrador sin OPERATOR») y RB26 («roles sin herencia»).
- **GIVEN** una persona con un permiso `SYSTEM_ADMINISTRATOR` vigente y ningún permiso `OPERATOR`
- **WHEN** entra por «Asistencia»
- **THEN** se deniega con `PERMISSION_MISSING`, sin sesión

#### Scenario: Autenticación fallida
Origen: AT54 («también autenticación fallida») y RB26 («PersonId sólo si se resolvió el actor»).
- **GIVEN** un código desconocido
- **WHEN** se usa para entrar por «Asistencia»
- **THEN** se deniega con `IDENTITY_NOT_RESOLVED`, sin sesión, y la bitácora registra `RequestedRole` `OPERATOR` sin `PersonId` y sin el código

#### Scenario: La sesión de asistencia no habilita consumo
Origen: RB26 («La sesión privilegiada no habilita consumo y se cierra al terminar o expirar»).
- **GIVEN** un operador con la sesión de asistencia abierta
- **WHEN** se intenta confirmar una intención con esa sesión, y después el operador toca «Salir»
- **THEN** la confirmación responde que la sesión no es utilizable y no escribe nada
- **AND** al salir el puesto vuelve a reposo sin privilegios ni el nombre del operador

### Requirement: Recuperar o reimprimir sin tocar el consumo

Con la sesión de asistencia, el operador SHALL poder recuperar un trabajo original en `FAILED`, que se vuelve a enviar como el mismo trabajo, o reimprimir un comprobante con un motivo de una lista fija. Reimprimir crea un trabajo con el número de copia siguiente y la leyenda DUPLICADO. Cada acción MUST comprobar de nuevo el permiso y MUST NOT cambiar consumo, cupo ni importe (RB08).

#### Scenario: Reimpresión con motivo
Origen: AT54 («crea copia numerada … sin cambio de consumo») y RB08.
- **GIVEN** un comprobante cuyo trabajo original está en `SUBMITTED` y un operador con permiso vigente
- **WHEN** lo reimprime con el motivo «Papel ilegible»
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
- **WHEN** el operador lo recupera
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
- **THEN** la acción se deniega con `PERMISSION_EXPIRED`, sin trabajo nuevo, y el puesto vuelve a reposo
