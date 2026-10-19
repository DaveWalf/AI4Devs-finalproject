# Spec Delta

## Purpose

Registrar en la base SQLite del puesto, sin red, la decisión sobre cada intención de consumo como un hecho todo o nada (RB06), y devolver ese mismo resultado ante un reintento o una caída sin crear un segundo consumo (RB07 y RB17).

## ADDED Requirements

### Requirement: Confirmación atómica de una intención nueva

El sistema SHALL decidir una intención nueva y persistir su decisión en una única transacción SQLite (RB06). Una autorización MUST guardar juntos intención y decisión, consumo, consumo conocido propio con sus cargas, uso de cupo, comprobante, trabajo de impresión con copia 0, evento de outbox y registro de bitácora. Un rechazo MUST guardar intención y decisión, evento y registro, sin consumo, cargas, comprobante ni trabajo.

#### Scenario: Autorización con cupo libre y sin red
Origen: AT01.
- **GIVEN** una persona sintética con una regla por persona vigente de límite 1 para ALMUERZO, cupo sin usar en su fecha de negocio y ninguna conexión de red
- **WHEN** confirma una intención nueva por una opción de menú vigente
- **THEN** la decisión es AUTHORIZED
- **AND** existen exactamente una intención, un consumo, un consumo conocido OWN con sus cargas, un uso de cupo con `Used` 1, un comprobante, un trabajo de impresión con copia 0, un evento `CONSUMPTION_RECORDED` y un registro de bitácora, todos con los identificadores que devuelve el resultado

#### Scenario: Rechazo por cupo agotado
Origen: AT02.
- **GIVEN** una persona cuyo uso de cupo ya alcanzó el límite de su regla en esa fecha de negocio
- **WHEN** confirma una intención nueva
- **THEN** la decisión es DENIED con motivo `LOCAL_LIMIT_REACHED`
- **AND** se guardan la intención con su decisión, un evento `CONSUMPTION_DENIED` y un registro de bitácora
- **AND** no se crean consumo, cargas, comprobante ni trabajo de impresión, y el uso de cupo no cambia

#### Scenario: Caída antes del commit no deja rastro
Origen: AT05, en el punto anterior al commit (HU02.1, «después de cortar el proceso en cada punto de la confirmación»).
- **GIVEN** una intención nueva cuya transacción ya escribió todas sus filas
- **WHEN** el proceso cae antes del commit
- **THEN** la base no contiene ninguna fila de esa intención en ninguna tabla
- **AND** confirmar después la misma intención la decide como nueva y produce un único consumo

### Requirement: Decisión calculada bajo el bloqueo

El sistema MUST tomar el bloqueo de escritura al iniciar la transacción y releer bajo ese bloqueo la intención, el snapshot activo, la opción de menú y el uso de cupo antes de decidir. La disponibilidad que se haya mostrado antes del bloqueo es orientativa y nunca habilita el commit (RB06).

#### Scenario: Dos conexiones compiten por el último cupo
Origen: AT03 (HU01.3-C).
- **GIVEN** una persona con una sola unidad de cupo libre y dos conexiones SQLite reales sobre el mismo archivo que leyeron disponibilidad antes de bloquear
- **WHEN** ambas confirman a la vez dos intenciones distintas
- **THEN** una decisión es AUTHORIZED y la otra DENIED con `LOCAL_LIMIT_REACHED`
- **AND** el uso de cupo queda en 1 y existe un solo consumo

#### Scenario: Versión de menú que dejó de ser la vigente
Origen: AT47.
- **GIVEN** una intención cuya versión de menú no coincide con la versión de esa opción en el snapshot activo
- **WHEN** se confirma
- **THEN** el resultado informa que el catálogo cambió y pide reconfirmación
- **AND** no se escribe ninguna fila, ni siquiera la intención

### Requirement: Replay idempotente por IntentId

Antes de decidir, el sistema MUST buscar el `IntentId` y comparar el hash del contenido de la intención. Mismo `IntentId` y mismo contenido MUST devolver el resultado original guardado, sin revalidar sesión, padrón, catálogo ni cupo y sin escribir nada. Mismo `IntentId` con otro contenido MUST responder conflicto sin escribir nada y sin alterar la decisión original (RB07).

#### Scenario: Doble toque con el mismo IntentId
Origen: AT26 (HU01-G).
- **GIVEN** una intención ya autorizada
- **WHEN** llega otra confirmación con el mismo `IntentId` y el mismo contenido
- **THEN** el resultado es el original, con los mismos identificadores de decisión, consumo y comprobante
- **AND** la cantidad de filas de cada tabla no cambia

#### Scenario: Mismo IntentId concurrente en dos conexiones
Origen: AT03, segunda parte.
- **GIVEN** dos conexiones SQLite reales sobre el mismo archivo
- **WHEN** ambas confirman a la vez el mismo `IntentId` con el mismo contenido
- **THEN** ambas devuelven la misma decisión durable y existe un solo consumo

#### Scenario: Replay después de vencer la sesión o cambiar el padrón o el catálogo
Origen: AT35 (HU02-L).
- **GIVEN** una intención ya autorizada y, después, la sesión de identidad vencida, la persona dada de baja en el padrón local y la opción de menú con otra versión
- **WHEN** se repite la misma intención con el mismo contenido
- **THEN** devuelve la decisión y los identificadores originales sin crear consumo, comprobante ni trabajo de impresión
- **AND** una intención distinta con esa sesión vencida se evalúa con el estado actual: no se confirma y no escribe ninguna fila

#### Scenario: Otra intención con una sesión ya usada
Origen: AT26, «otra lectura durante confirmación» (HU01-G).
- **GIVEN** una sesión de identidad con la que ya se autorizó una intención
- **WHEN** llega una intención con otro `IntentId` y la misma sesión
- **THEN** el resultado informa que la sesión no es utilizable
- **AND** no se escribe ninguna fila y sigue habiendo un solo consumo

#### Scenario: Mismo IntentId con otro contenido
Origen: AT25.
- **GIVEN** una intención ya decidida
- **WHEN** llega el mismo `IntentId` con otra opción de menú
- **THEN** el resultado es conflicto
- **AND** no se escribe ninguna fila y la decisión original queda igual, byte por byte

### Requirement: Recuperación tras una caída después del commit

Una vez confirmado el commit, el sistema MUST poder recuperar el resultado de una intención después de reiniciar, con los mismos identificadores, un solo consumo y un solo trabajo de impresión original; recuperar MUST retomar ese trabajo existente sin crear otro (RB17).

#### Scenario: Caída entre el commit y el resultado
Origen: AT05 (HU02-B).
- **GIVEN** una intención cuya transacción hizo commit
- **WHEN** el proceso cae antes de devolver el resultado y, al reabrir la base con una conexión nueva, se repite la intención
- **THEN** el resultado tiene los mismos `DecisionId`, `ConsumptionId` y `TicketId` que quedaron guardados
- **AND** existen un consumo, un comprobante y un solo trabajo de impresión, con copia 0 y todavía pendiente

#### Scenario: Recuperar no reconstruye el trabajo original
Origen: AT05 y RB17.
- **GIVEN** una intención recuperada después de una caída
- **WHEN** se la recupera una segunda vez
- **THEN** sigue habiendo un único trabajo de impresión para su comprobante, con el mismo identificador

### Requirement: Cupo por persona, ámbito, categoría y fecha de negocio

El sistema SHALL decidir el cupo de una intención nueva con la clave `PersonId` + `QuotaScopeId` + `QuotaBucketId` + `BusinessDate` (RB03): autoriza sólo si, para cada carga de la opción de menú, el uso más las unidades no supera el límite de la única regla por persona vigente para ese servicio, ámbito y categoría. Sin regla vigente MUST denegar con `SERVICE_NOT_ENABLED`.

#### Scenario: Variantes que comparten categoría cuentan juntas
Origen: AT02 (RB03, «Variantes pueden compartir categoría»).
- **GIVEN** una persona con límite 1 en ALMUERZO que ya consumió la variante común
- **WHEN** confirma una intención nueva por la variante opcional del mismo ALMUERZO
- **THEN** la decisión es DENIED con `LOCAL_LIMIT_REACHED`

#### Scenario: Otra categoría no consume el mismo cupo
Origen: AT02.
- **GIVEN** una persona que agotó ALMUERZO y tiene cupo libre en REFRIGERIO
- **WHEN** confirma una intención nueva por una opción de REFRIGERIO
- **THEN** la decisión es AUTHORIZED

#### Scenario: Persona sin regla aplicable
Origen: sin AT propio en el corte; RB02, «Sin regla aplicable se deniega», con la persona sin derecho del fixture.
- **GIVEN** una persona sin ninguna regla por persona vigente para ese servicio, ámbito y categoría
- **WHEN** confirma una intención nueva
- **THEN** la decisión es DENIED con `SERVICE_NOT_ENABLED` y no se crea consumo

### Requirement: Fecha de negocio con la zona y el corte configurados

El sistema SHALL calcular la fecha de negocio de una intención nueva con la hora de Windows observada, la zona horaria y la hora de inicio del día configuradas en el puesto (RB04). El consumo y todas sus cargas de cupo MUST llevar esa misma fecha de negocio.

#### Scenario: Confirmaciones a ambos lados del corte
Origen: AT04.
- **GIVEN** un puesto con zona `America/Argentina/Buenos_Aires`, corte 00:00 y límite 1 en ALMUERZO
- **WHEN** una persona confirma a las 02:59:59Z y otra intención suya a las 03:00:00Z del mismo día UTC
- **THEN** las dos decisiones son AUTHORIZED, con fechas de negocio de días consecutivos y usos de cupo separados

#### Scenario: Las cargas llevan la fecha del consumo
Origen: AT50 (invariante 8 del modelo de datos).
- **GIVEN** una opción de menú que carga dos categorías
- **WHEN** se autoriza una intención por esa opción
- **THEN** cada carga de cupo conocida y cada `AppliedQuotaCharges` del evento llevan exactamente la `BusinessDate` del consumo

### Requirement: Base SQLite con esquema versionado

Al abrir la base, el sistema MUST activar las claves foráneas, aplicar la migración `0001` a una base vacía y rechazar, sin modificarla, una base cuya versión de esquema no conoce. La migración MUST usar los nombres físicos del mapa de nomenclatura (D-C18) y hacer cumplir las unicidades del modelo: un consumo por intención, un comprobante por consumo, `(TicketId, CopyNumber)` y la clave de `QuotaUsage`.

#### Scenario: Base vacía
Origen: TK03.1, «la migración crea una base vacía válida», y AT51.
- **GIVEN** un archivo SQLite inexistente
- **WHEN** se abre la base
- **THEN** queda en la versión de esquema 1, con claves foráneas activas y las tablas con los nombres `tbComedor…` del mapa

#### Scenario: Versión de esquema desconocida
Origen: TK03.1, trabajo incluido 2.
- **GIVEN** una base con una versión de esquema mayor que la última migración conocida
- **WHEN** se abre
- **THEN** la apertura falla con un error de esquema desconocido y el archivo queda sin cambios

#### Scenario: Unicidad de consumo por intención
Origen: TK03.1, trabajo incluido 5, y RNF01.
- **GIVEN** una intención con su consumo
- **WHEN** se intenta insertar directamente otro consumo con el mismo `IntentId`
- **THEN** la base rechaza la inserción por la restricción de unicidad

### Requirement: Código de identificación fuera de los hechos registrados

El sistema MUST NOT escribir el código de identificación ni ningún valor de credencial en la intención, el resultado, el evento de outbox, el comprobante ni el registro de bitácora.

#### Scenario: Una autorización no registra el código
Origen: D6 de `fijar-primer-corte` y `AGENTS.md`.
- **GIVEN** una persona con un código sintético conocido y una sesión abierta con él
- **WHEN** se autoriza una intención
- **THEN** ninguna columna de intenciones, eventos de outbox, comprobantes ni bitácora contiene ese código

#### Scenario: Un rechazo tampoco lo registra
Origen: `ConsumptionDenied` del OpenAPI, «No contiene credencial».
- **GIVEN** la misma persona con su cupo agotado
- **WHEN** se rechaza una intención nueva
- **THEN** ni el evento `CONSUMPTION_DENIED` ni su registro de bitácora contienen ese código
