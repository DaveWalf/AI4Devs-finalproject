# Spec Delta

## REMOVED Requirements

### Requirement: Ningún acuse sin ingesta durable

**Reason**: La ingesta durable existe con este change: el receptor acusa un lote sólo después de guardar su recibo y el resultado de cada evento en una transacción (RB16), así que el `501` del esqueleto deja de tener objeto.
**Migration**: El envío y la consulta se rigen por «Lote idempotente por BatchId» y «Consulta del acuse original». El escenario «Cuerpo que no es JSON» pasa a «Errores que no reflejan la entrada» con `400 INVALID_REQUEST`. «Ruta no declarada» pasa a «Sólo las operaciones del contrato».

## ADDED Requirements

### Requirement: Equipo autenticado por certificado

El receptor SHALL atender un pedido de lotes sólo si llega con un certificado de cliente vigente, emitido por la autoridad que el receptor confía y registrado para un puesto. Sin certificado, vencido o de otra autoridad MUST responder `401 EQUIPMENT_AUTH_REQUIRED`; válido pero no registrado, `403 EQUIPMENT_FORBIDDEN`. En ninguno de esos casos MUST persistir nada.

#### Scenario: Pedido sin certificado
Origen: AT13, «credencial de sincronización inválida».
- **GIVEN** el receptor en marcha
- **WHEN** un cliente envía un lote sintético válido sin certificado de cliente
- **THEN** la respuesta es `401` con `application/problem+json` y `code` `EQUIPMENT_AUTH_REQUIRED`
- **AND** el receptor no guarda ningún recibo ni evento

#### Scenario: Certificado vencido
Origen: HU02-G, «credencial de sincronización vencida», y AT13.
- **GIVEN** un certificado emitido por la autoridad confiable, registrado para un puesto y vencido
- **WHEN** ese puesto envía un lote
- **THEN** la respuesta es `401` con `code` `EQUIPMENT_AUTH_REQUIRED` y no se guarda nada

#### Scenario: Certificado de otra autoridad
Origen: AT13; OWASP A07 (fallas de identificación y autenticación).
- **GIVEN** un certificado vigente con la misma identidad que uno registrado, pero emitido por otra autoridad
- **WHEN** se envía un lote con él
- **THEN** la respuesta es `401` con `code` `EQUIPMENT_AUTH_REQUIRED` y no se guarda nada

#### Scenario: Certificado válido de un equipo no registrado
Origen: AT13, «remoto deniega sin datos ajenos».
- **GIVEN** un certificado vigente de la autoridad confiable que no está registrado para ningún puesto
- **WHEN** se envía un lote con él
- **THEN** la respuesta es `403` con `code` `EQUIPMENT_FORBIDDEN` y no se guarda nada

### Requirement: Lote idempotente por BatchId

El receptor SHALL exigir `Idempotency-Key` igual a `BatchId` y calcular el SHA-256 del cuerpo canónico RFC 8785. Un lote nuevo MUST guardar su recibo y el resultado de cada evento en una sola transacción y responder `201` con el acuse. El mismo lote del mismo puesto MUST devolver `200` con el acuse original. La misma clave con otro cuerpo MUST responder `409 BATCH_PAYLOAD_CONFLICT` sin alterar nada.

#### Scenario: Lote nuevo
Origen: AT27 y HU02-C.
- **GIVEN** un puesto registrado y un lote sintético con un `CONSUMPTION_RECORDED` y un `CONSUMPTION_DENIED`
- **WHEN** el puesto lo envía
- **THEN** la respuesta es `201` con `Status` `COMPLETED` y exactamente un resultado por `EventId`, sin extraños ni omitidos
- **AND** el encabezado `X-Correlation-Id` es igual al `CorrelationId` del cuerpo

#### Scenario: Reenvío tras perder el acuse
Origen: AT27 y HU02-D.
- **GIVEN** un lote ya acusado
- **WHEN** el mismo puesto lo reenvía con el mismo cuerpo
- **THEN** la respuesta es `200` con el mismo acuse, byte por byte, incluidos `ServerAcknowledgedAtUtc` y `CorrelationId`
- **AND** la cantidad de recibos, eventos de entrada y consumos consolidados no cambia

#### Scenario: Mismo contenido con otro orden de propiedades y otros espacios
Origen: AT27; `SubmitSyncBatch`, «SHA-256 del body JSON canónico (RFC 8785)».
- **GIVEN** un lote ya acusado
- **WHEN** el mismo puesto envía el mismo lote con las propiedades en otro orden y con espacios entre los tokens
- **THEN** la respuesta es `200` con el acuse original

#### Scenario: Misma clave con otro cuerpo
Origen: TK01.1, trabajo incluido 2.
- **GIVEN** un lote ya acusado
- **WHEN** el mismo puesto envía el mismo `BatchId` con otro evento
- **THEN** la respuesta es `409` con `code` `BATCH_PAYLOAD_CONFLICT`
- **AND** el recibo, su acuse y los eventos del lote original quedan iguales

#### Scenario: Clave distinta del BatchId
Origen: TK01.1, trabajo incluido 2.
- **GIVEN** un puesto registrado
- **WHEN** envía un lote cuyo `Idempotency-Key` no es igual a su `BatchId`
- **THEN** la respuesta es `400` con `code` `IDEMPOTENCY_KEY_MISMATCH` y no se guarda nada

#### Scenario: El mismo lote dos veces a la vez
Origen: AT27; RB16, «la cola sólo avanza con resultado durable».
- **GIVEN** un lote nuevo
- **WHEN** el mismo puesto lo envía dos veces en paralelo
- **THEN** existe un solo recibo y un solo consumo consolidado
- **AND** las dos respuestas llevan el mismo acuse

### Requirement: Deduplicación y consolidación por evento

El receptor SHALL deduplicar cada evento por su `EventId` en todos los lotes y puestos, y consolidar cada `ConsumptionId` una sola vez. Un evento ya incorporado con el mismo contenido y del mismo puesto MUST resultar `DUPLICATE/ALREADY_RECORDED`. Otro contenido, otro puesto u otro evento con el mismo `ConsumptionId` MUST resultar `CONFLICT/EVENT_PAYLOAD_CONFLICT`, con un caso que conserve la evidencia nueva y sin tocar la original.

#### Scenario: Un consumo nuevo se consolida una vez
Origen: AT27 y RNF01.
- **GIVEN** un `CONSUMPTION_RECORDED` sintético que el receptor no conoce
- **WHEN** llega en un lote
- **THEN** su resultado es `ACCEPTED/RECORDED` y existe un consumo consolidado con ese `ConsumptionId`

#### Scenario: El mismo evento en otro lote
Origen: AT27 y HU02-D, «deduplicación central».
- **GIVEN** un `CONSUMPTION_RECORDED` ya consolidado
- **WHEN** el mismo puesto lo envía en otro lote con el mismo contenido
- **THEN** su resultado es `DUPLICATE/ALREADY_RECORDED`
- **AND** sigue habiendo un solo consumo consolidado con ese `ConsumptionId`

#### Scenario: El mismo EventId con otro contenido
Origen: `SubmitSyncBatch`, «otro contenido para EventId es CONFLICT y requiere revisión visible».
- **GIVEN** un `CONSUMPTION_RECORDED` ya consolidado
- **WHEN** llega el mismo `EventId` con otra `MenuVersion`
- **THEN** su resultado es `CONFLICT/EVENT_PAYLOAD_CONFLICT`
- **AND** el evento y el consumo consolidado originales quedan iguales, y existe un caso de conciliación con el contenido nuevo

#### Scenario: Un EventId que ya mandó otro puesto
Origen: HU02.2, terminado: «rechaza hechos ajenos», y AT13.
- **GIVEN** un evento ya incorporado desde un puesto
- **WHEN** otro puesto registrado envía un lote con el mismo `EventId` y el mismo contenido
- **THEN** su resultado es `CONFLICT/EVENT_PAYLOAD_CONFLICT`, no `DUPLICATE`, y se abre un caso

#### Scenario: Otro evento con un ConsumptionId ya consolidado
Origen: `SubmitSyncBatch`, «ConsumptionId conserva su unicidad de negocio durante la consolidación».
- **GIVEN** un consumo consolidado
- **WHEN** llega un `CONSUMPTION_RECORDED` con otro `EventId` y el mismo `ConsumptionId`
- **THEN** su resultado es `CONFLICT/EVENT_PAYLOAD_CONFLICT` y sigue habiendo un solo consumo consolidado

#### Scenario: Una denegación se incorpora sin consumo
Origen: `ConsumptionDenied` del OpenAPI, «sin ConsumptionId ni TicketId».
- **GIVEN** un `CONSUMPTION_DENIED` sintético
- **WHEN** llega en un lote
- **THEN** su resultado es `ACCEPTED/RECORDED` y no se crea ningún consumo consolidado

### Requirement: Ningún acuse reautoriza ni revierte

Ningún resultado del receptor SHALL autorizar, denegar ni revertir un consumo (RB16). Un conflicto o un rechazo MUST conservar el hecho y su evidencia para revisión, y MUST NOT borrar ni modificar un consumo consolidado.

#### Scenario: Un conflicto no niega la comida
Origen: AT27 y RB16, «conflictos se registran para revisión sin eliminar ni negar retroactivamente hechos locales».
- **GIVEN** un consumo consolidado y un caso abierto por un conflicto sobre su `EventId`
- **WHEN** se consulta lo guardado
- **THEN** el consumo consolidado sigue con su contenido original
- **AND** ningún resultado del acuse lleva un estado o un motivo de denegación de consumo

### Requirement: Validación por aplicación de cada evento

El receptor SHALL validar cada evento contra su esquema del contrato y, para `CONSUMPTION_RECORDED`, que cada carga tenga la `BusinessDate` del consumo (AT50). Un evento inválido con un `EventId` legible MUST resultar `REJECTED/INVALID_EVENT`, guardarse como evidencia y no consolidarse, sin impedir que se procesen los demás eventos del lote.

#### Scenario: Una carga con otra fecha de negocio
Origen: AT50.
- **GIVEN** un `CONSUMPTION_RECORDED` cuya segunda carga tiene una `BusinessDate` distinta de la del consumo
- **WHEN** llega en un lote junto con un evento válido
- **THEN** su resultado es `REJECTED/INVALID_EVENT`, el evento queda guardado y no se crea su consumo consolidado
- **AND** el otro evento del lote resulta `ACCEPTED/RECORDED`

#### Scenario: Todas las cargas con la misma fecha
Origen: AT50, «ejemplo coherente acepta igualdad».
- **GIVEN** un `CONSUMPTION_RECORDED` con dos cargas de la misma `BusinessDate` que el consumo
- **WHEN** llega en un lote
- **THEN** su resultado es `ACCEPTED/RECORDED`

#### Scenario: Una propiedad que el contrato no declara
Origen: OWASP A08 (integridad de datos); `additionalProperties: false` de los eventos del OpenAPI.
- **GIVEN** un `CONSUMPTION_RECORDED` con una propiedad extra
- **WHEN** llega en un lote
- **THEN** su resultado es `REJECTED/INVALID_EVENT` y no se consolida

#### Scenario: Reenvío de un evento rechazado
Origen: `EventAck`, «El cliente mantiene el hecho pendiente de corrección».
- **GIVEN** un evento ya rechazado
- **WHEN** el mismo puesto lo reenvía con el mismo contenido en otro lote
- **THEN** su resultado vuelve a ser `REJECTED/INVALID_EVENT` y sigue sin consolidarse

### Requirement: Eventos que esperan una referencia o una revisión

El receptor SHALL guardar un `PRINT_EVENT_RECORDED` cuyo consumo todavía no consolidó como `NEEDS_REVIEW/RELATED_EVENT_PENDING`, y MUST retomar su proyección cuando ese consumo llegue, sin reescribir el acuse original. Un `CONNECTIVITY_INCIDENT_RECORDED` válido MUST quedar `NEEDS_REVIEW/MULTI_KIOSK_POLICY_REVIEW`. Un reenvío de un evento en revisión MUST NOT resultar `DUPLICATE`.

#### Scenario: Evento de impresión de un consumo conocido
Origen: `PrintEventRecorded` del OpenAPI; HU03.1, sin AT propio en este ticket.
- **GIVEN** un consumo consolidado
- **WHEN** llega un `PRINT_EVENT_RECORDED` válido de ese consumo y del mismo puesto
- **THEN** su resultado es `ACCEPTED/RECORDED`

#### Scenario: Evento de impresión antes que su consumo
Origen: `SubmitSyncBatch`, «Un evento recibido antes de su referencia conserva NEEDS_REVIEW/RELATED_EVENT_PENDING, incluso al repetirse».
- **GIVEN** un `PRINT_EVENT_RECORDED` cuyo `ConsumptionId` el receptor no consolidó
- **WHEN** llega en un lote y después se reenvía en otro
- **THEN** las dos veces su resultado es `NEEDS_REVIEW/RELATED_EVENT_PENDING`

#### Scenario: Llega la referencia
Origen: `SubmitSyncBatch`, «El servidor retoma su proyección al llegar la referencia».
- **GIVEN** un `PRINT_EVENT_RECORDED` en `RELATED_EVENT_PENDING`
- **WHEN** llega en otro lote el `CONSUMPTION_RECORDED` de su consumo
- **THEN** el evento de impresión queda proyectado
- **AND** la consulta del primer lote devuelve su acuse original, con `NEEDS_REVIEW/RELATED_EVENT_PENDING`

#### Scenario: Incidencia de conectividad
Origen: D-C24 (incidencias y multipuesto en la segunda ola).
- **GIVEN** un `CONNECTIVITY_INCIDENT_RECORDED` válido
- **WHEN** llega en un lote
- **THEN** su resultado es `NEEDS_REVIEW/MULTI_KIOSK_POLICY_REVIEW` y queda guardado

### Requirement: Lote inválido sin efectos

El receptor SHALL responder `422 INVALID_BATCH` sin guardar nada cuando el lote no cumple el contrato como un todo: `SchemaVersion` distinta de 1, sin eventos, más de 500 eventos, un `EventId` repetido dentro del lote o un evento sin `EventId` legible.

#### Scenario: EventId repetido dentro del lote
Origen: `SyncBatchRequest`, «EventId únicos dentro del lote».
- **GIVEN** un puesto registrado
- **WHEN** envía un lote con dos eventos del mismo `EventId`
- **THEN** la respuesta es `422` con `code` `INVALID_BATCH` y no se guarda nada

#### Scenario: Versión de esquema desconocida
Origen: `SyncBatchRequest`, `SchemaVersion` `const: 1`.
- **GIVEN** un puesto registrado
- **WHEN** envía un lote con `SchemaVersion` 2
- **THEN** la respuesta es `422` con `code` `INVALID_BATCH` y no se guarda nada

#### Scenario: Demasiados eventos
Origen: OWASP A04 (diseño inseguro: consumo de recursos sin límite).
- **GIVEN** un puesto registrado
- **WHEN** envía un lote con 501 eventos
- **THEN** la respuesta es `422` con `code` `INVALID_BATCH` y no se guarda nada

### Requirement: Consulta del acuse original

`GET /api/incomedor/v1/sync-batches/{batchId}` SHALL devolver `200` con el acuse original de un lote del puesto autenticado. Un lote desconocido o de otro puesto MUST responder `404 BATCH_NOT_FOUND` con la misma forma, sin revelar que existe.

#### Scenario: Consulta tras perder el acuse
Origen: AT27, «se reenvía/consulta con mismos IDs».
- **GIVEN** un lote ya acusado
- **WHEN** el mismo puesto lo consulta
- **THEN** la respuesta es `200` con el acuse original, byte por byte

#### Scenario: Lote de otro puesto
Origen: AT13, «consulta de lote ajeno».
- **GIVEN** un lote acusado para un puesto
- **WHEN** otro puesto registrado lo consulta
- **THEN** la respuesta es `404` con `code` `BATCH_NOT_FOUND`
- **AND** salvo el `correlationId`, es igual a la de un `BatchId` que nunca existió

### Requirement: Errores que no reflejan la entrada

Toda respuesta de error del receptor SHALL ser `application/problem+json` con `type`, `title`, `status`, `code` y `correlationId`, igual al encabezado `X-Correlation-Id`. MUST NOT incluir contenido del pedido ni detalles internos. Un cuerpo que no es JSON, con propiedades repetidas, con números no enteros o de más de 1 MiB MUST responder `400 INVALID_REQUEST`.

#### Scenario: Cuerpo que no es JSON
Origen: sin AT propio; OWASP A03 (inyección) y A05 (configuración insegura): una entrada inválida no produce un error interno ni se refleja.
- **GIVEN** un puesto registrado
- **WHEN** envía un cuerpo que no es JSON y que contiene una cadena reconocible
- **THEN** la respuesta es `400` con `code` `INVALID_REQUEST`, no `500`
- **AND** la respuesta no contiene esa cadena

#### Scenario: Propiedad repetida
Origen: OWASP A08; RFC 8785 exige nombres de propiedad únicos.
- **GIVEN** un puesto registrado
- **WHEN** envía un lote cuyo objeto raíz repite `BatchId` con dos valores
- **THEN** la respuesta es `400` con `code` `INVALID_REQUEST` y no se guarda nada

#### Scenario: Cuerpo demasiado grande
Origen: OWASP A04.
- **GIVEN** un puesto registrado
- **WHEN** envía un cuerpo de más de 1 MiB
- **THEN** la respuesta es `400` con `code` `INVALID_REQUEST` y no se guarda nada

### Requirement: Sólo las operaciones del contrato

El receptor SHALL exponer sólo las operaciones de lotes del OpenAPI. Una ruta que el OpenAPI no declara MUST responder `404`.

#### Scenario: Ruta no declarada
Origen: sin AT propio; el receptor expone sólo las operaciones del OpenAPI.
- **GIVEN** el receptor en marcha
- **WHEN** un cliente pide una ruta que el OpenAPI no declara
- **THEN** la respuesta es `404`
