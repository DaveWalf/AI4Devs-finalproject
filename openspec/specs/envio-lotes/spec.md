# envio-lotes Specification

## Purpose
Definir cómo el puesto envía al receptor central los hechos de su outbox sin que el envío intervenga en la autorización: lotes durables que se reenvían idénticos, una cola que avanza sólo con un acuse durable (RB16) y una atención que sigue sin red.

## Requirements

### Requirement: Lote durable antes de enviar

El puesto SHALL guardar el lote, su cuerpo canónico y su hash en su base SQLite antes de enviarlo, en una transacción que lo asocia a sus eventos pendientes. Mientras un lote no tenga acuse, MUST reenviarse ese mismo lote, byte por byte, y sus eventos MUST NOT entrar en otro. Un lote lleva a lo sumo 100 eventos.

#### Scenario: Eventos pendientes
Origen: HU02-C, «envía lote con IDs estables».
- **GIVEN** un puesto con una autorización y un rechazo confirmados y sus dos eventos en `PENDING`
- **WHEN** prepara un lote
- **THEN** existe un lote guardado con esos dos eventos, su cuerpo y el SHA-256 de ese cuerpo
- **AND** el cuerpo cumple `SyncBatchRequest` del OpenAPI con `SchemaVersion` 1

#### Scenario: Caída después de guardar el lote y antes de enviarlo
Origen: AT27 y RB17; `SubmitSyncBatch`, «El cliente guarda BatchId y el lote antes de enviar».
- **GIVEN** un lote guardado que nunca llegó al receptor
- **WHEN** el puesto se reinicia con una conexión nueva y vuelve a preparar
- **THEN** obtiene el mismo `BatchId` y el mismo cuerpo, byte por byte
- **AND** no se crea otro lote

#### Scenario: Nada pendiente
Origen: sin AT propio; borde de HU02-C.
- **GIVEN** un puesto sin eventos en `PENDING` ni lotes sin acuse
- **WHEN** corre un ciclo de envío
- **THEN** no se guarda ningún lote y no se hace ningún pedido al receptor

#### Scenario: Más de 100 eventos pendientes
Origen: sin AT propio; el receptor rechaza lotes de más de 500 eventos.
- **GIVEN** 101 eventos en `PENDING`
- **WHEN** el puesto prepara un lote
- **THEN** el lote lleva los 100 primeros en el orden en que se registraron y el restante queda para el lote siguiente

### Requirement: La cola avanza sólo con un acuse durable

El puesto SHALL marcar sus eventos según el acuse sólo si la respuesta es `201` o `200` con un acuse `COMPLETED` del mismo `BatchId` y exactamente un resultado por evento del lote. `ACCEPTED` y `DUPLICATE` MUST marcar el evento como incorporado; `CONFLICT`, `REJECTED` y `NEEDS_REVIEW` MUST retenerlo con su estado, sin reenviarlo solo ni borrarlo.

#### Scenario: Acuse completo
Origen: AT27, «cola sólo avanza con resultado durable».
- **GIVEN** un lote con dos eventos
- **WHEN** el receptor responde `201` con `ACCEPTED/RECORDED` para los dos
- **THEN** los dos eventos quedan incorporados y el lote queda completo con su acuse guardado

#### Scenario: Un evento en conflicto o rechazado
Origen: `SubmitSyncBatch`, «El kiosco conserva conflictos y rechazos para diagnóstico y conciliación, sin purgar silenciosamente su outbox».
- **GIVEN** un lote con tres eventos
- **WHEN** el acuse trae `ACCEPTED` para uno, `CONFLICT` para otro y `REJECTED` para el tercero
- **THEN** el primero queda incorporado y los otros dos quedan retenidos con su estado
- **AND** el ciclo siguiente no los vuelve a enviar y siguen en la base

#### Scenario: Acuse incompleto
Origen: `CompletedBatchAck`, «Exactamente un resultado por EventId recibido; sin extraños ni omitidos».
- **GIVEN** un lote con dos eventos
- **WHEN** el receptor responde `201` con un solo resultado, o con un `EventId` que no está en el lote
- **THEN** ningún evento cambia de estado y el lote sigue sin acuse

#### Scenario: Respuestas que no acusan
Origen: AT27 y RB16.
- **GIVEN** un lote guardado
- **WHEN** el receptor responde `202`, `409`, `422`, `503` o la red falla
- **THEN** ningún evento cambia de estado, el lote sigue sin acuse y conserva su `BatchId`

### Requirement: Reenvío idéntico ante un acuse perdido

Cuando un envío termina sin respuesta, el puesto SHALL consultar el mismo `BatchId` antes de reenviar. Si el receptor responde `404`, MUST reenviar exactamente el lote original; si devuelve el acuse, MUST registrarlo como si hubiera llegado con el envío. MUST NOT cambiar el `BatchId` ni el cuerpo para esquivar un conflicto.

#### Scenario: El receptor guardó el lote y se perdió la respuesta
Origen: AT27 y HU02-D.
- **GIVEN** un lote que el receptor acusó y cuya respuesta no llegó al puesto
- **WHEN** corre el ciclo siguiente
- **THEN** el puesto consulta el mismo `BatchId`, registra el acuse original y no vuelve a enviar el lote
- **AND** el receptor tiene un solo consumo consolidado

#### Scenario: El receptor nunca recibió el lote
Origen: `SubmitSyncBatch`, «si aún no aparece, reenviar exactamente el lote original».
- **GIVEN** un lote cuyo envío falló antes de llegar al receptor
- **WHEN** corre el ciclo siguiente y la consulta responde `404`
- **THEN** el puesto reenvía el mismo cuerpo, byte por byte, con el mismo `Idempotency-Key`

#### Scenario: El receptor tiene otro cuerpo con esa clave
Origen: `Conflict` del OpenAPI, «No se cambia BatchId para ocultarlo».
- **GIVEN** un lote guardado
- **WHEN** el receptor responde `409 BATCH_PAYLOAD_CONFLICT`
- **THEN** el lote y sus eventos se conservan sin acuse, con el mismo `BatchId`, y el conflicto queda en la bitácora

### Requirement: Error de envío visible sin cortar la atención

Un error de envío SHALL quedar en la bitácora con su código y verse en el estado de la sincronización, y la atención MUST seguir autorizando sin red. Un `401` o un `403` MUST mostrarse como un error técnico de la credencial del equipo, distinto de una falta de red.

#### Scenario: Credencial de equipo vencida
Origen: HU02-G y AT13, «hechos locales conservados; en monopuesto la atención sigue».
- **GIVEN** un puesto con eventos pendientes y un certificado de equipo vencido
- **WHEN** corre un ciclo de envío y el receptor responde `401`
- **THEN** los eventos y el lote se conservan sin acuse
- **AND** el estado de la sincronización informa el error de la credencial del equipo, y una intención nueva se autoriza como antes

#### Scenario: Receptor inalcanzable
Origen: AT01 (monopuesto sin red) y HU02-C, «el puesto sigue atendiendo durante sincronización».
- **GIVEN** un receptor que no responde
- **WHEN** corre un ciclo de envío mientras una persona confirma
- **THEN** la confirmación termina con su decisión sin esperar al envío
- **AND** el estado de la sincronización informa los eventos pendientes

### Requirement: El envío no toca las decisiones

Preparar, enviar y registrar un acuse SHALL cambiar sólo los lotes, el estado de los eventos del outbox y la bitácora. MUST NOT modificar intenciones, decisiones, consumos, usos de cupo, comprobantes ni trabajos de impresión (RB16), y el lote MUST NOT contener el código de identificación.

#### Scenario: Un conflicto no revierte el consumo
Origen: RB16 y AT27, «consumo local intacto».
- **GIVEN** un consumo autorizado cuyo evento el receptor acusa como `CONFLICT`
- **WHEN** el puesto registra el acuse
- **THEN** las filas de intenciones, consumos, usos de cupo, comprobantes y trabajos de impresión quedan iguales, byte por byte

#### Scenario: El lote no lleva el código
Origen: `AGENTS.md`, «El código de identificación no se escribe en logs, tickets ni eventos».
- **GIVEN** una persona que se identificó con un código sintético conocido y confirmó
- **WHEN** el puesto prepara y envía el lote
- **THEN** ni el cuerpo del lote guardado ni el acuse contienen ese código
