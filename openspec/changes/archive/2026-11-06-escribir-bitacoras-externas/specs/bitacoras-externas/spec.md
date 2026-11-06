# Spec Delta

## Purpose

Que todo lo que hacen las personas, los operadores y quien administra el puesto quede, además de en SQLite, en dos archivos de texto mensuales fuera de la base, con lo necesario para reconstruir cada interacción sin consultar la base, y que una falla de esos archivos no detenga la atención ni pierda registros.

## ADDED Requirements

### Requirement: Dos archivos mensuales por puesto

El puesto SHALL escribir dos bitácoras de texto, `INComedor-general-AAAAMM.log` e `INComedor-interacciones-AAAAMM.log`, en la carpeta `bitacoras\<KioskId>\` de su carpeta de datos, en UTF-8 sin BOM y con un registro JSON por línea terminado en salto de línea. El AAAAMM de cada registro MUST ser el mes de su hecho en la zona del puesto, fijado al registrarlo, y no el de la escritura.

#### Scenario: Nombres del mes
Origen: RF45, D-C13 y AT42 (nombres AAAAMM).
- **GIVEN** un puesto con zona `America/Argentina/Buenos_Aires` que autoriza un consumo el 06/11/2026 a las 12:00 locales
- **WHEN** el escritor entrega sus registros
- **THEN** existen `INComedor-general-202611.log` e `INComedor-interacciones-202611.log` en `bitacoras\<KioskId>\`, cada línea se lee como un objeto JSON y ninguna carpeta fuera de esa contiene bitácoras

#### Scenario: Hecho del último minuto del mes
Origen: AT42, borde: el mes es el del hecho en la zona del puesto, no el UTC.
- **GIVEN** un consumo autorizado el 30/11/2026 a las 23:59 locales, que en UTC ya es diciembre
- **WHEN** el escritor lo entrega el 01/12
- **THEN** su línea está en `INComedor-interacciones-202611.log` y no en el archivo de 202612

### Requirement: Cada línea se entiende sola

Cada línea SHALL llevar `SchemaVersion`, `AuditId`, `KioskId`, `GenerationId`, `Sequence`, `BootId`, `OccurredAtUtc`, `RecordedAtUtc`, `TimeZone`, `Offset`, `LogMonth`, `Stream`, `EventType`, `Severity`, `Component`, `ExportedAtUtc`, `Recovered` y `Context`. Un dato que no aplica MUST ir nulo o ausente, nunca inventado. Ninguna línea MUST contener el código de identificación, una clave ni material biométrico (RB25).

#### Scenario: Campos de una autorización
Origen: AT41 (mismos IDs en la base y en el archivo) y RB24.
- **GIVEN** un consumo autorizado
- **WHEN** se lee su línea en la bitácora de interacciones
- **THEN** tiene todos los campos del contrato, `EventType` `CONSUMPTION_AUTHORIZED`, el mismo `AuditId` y la misma `Sequence` que su registro en la base, y en `Context` el `IntentId`, el `DecisionId`, el `ConsumptionId` y el `TicketId` del resultado

#### Scenario: Ningún código en los archivos
Origen: AT41 («sin biometría ni secretos») y RB25.
- **GIVEN** una sesión con un código sintético reconocible que se rechaza una vez por desconocido y después se autoriza
- **WHEN** el escritor entrega los registros
- **THEN** ninguno de los dos archivos contiene ese código en ninguna línea

#### Scenario: Identidad no resuelta
Origen: AT41, borde: no se inventa una persona para completar la traza.
- **GIVEN** un código desconocido
- **WHEN** se lee la línea de su rechazo
- **THEN** su `Context` no tiene `PersonId` y tiene el motivo `IDENTITY_NOT_RESOLVED`

### Requirement: Cada registro va a su bitácora

Los registros de la atención, la asistencia y los comprobantes SHALL ir a la bitácora de interacciones; los del puesto, Configuración, la sincronización y las propias bitácoras, a la general; los de impresión y el cierre de una incidencia de bitácora, a las dos, con el mismo `AuditId`. Un tipo de registro sin destino conocido MUST ir a la general: nada se descarta por no tener destino.

#### Scenario: Impresión en las dos
Origen: AT41 y el diseño de bitácoras («un evento relevante para ambas aparece en las dos con el mismo AuditId»).
- **GIVEN** un trabajo de impresión que pasa de `UNKNOWN` a `SUBMITTED`
- **WHEN** el escritor entrega su registro
- **THEN** la misma línea, con el mismo `AuditId`, está en la general y en la de interacciones, y en cada una con su `Stream`

#### Scenario: Tipo sin destino conocido
Origen: RB23, borde: un tipo nuevo sin reparto no se pierde.
- **GIVEN** un registro de un tipo que no figura en el reparto
- **WHEN** el escritor lo entrega
- **THEN** está en la bitácora general y no en la de interacciones

### Requirement: La atención no espera al archivo

Cada registro SHALL quedar primero en la base, en la misma transacción que su hecho cuando lo hay (RB06, RB23). Un escritor fuera de la atención MUST llevarlo después a sus archivos, en orden de `Sequence`, con un flush durable antes de seguir. Una confirmación MUST NOT esperar al archivo ni fallar por él.

#### Scenario: Orden de las líneas
Origen: RB24 (la secuencia ordena el registro del puesto) y AT41.
- **GIVEN** una identificación, una oferta, una confirmación autorizada y su impresión
- **WHEN** el escritor las entrega
- **THEN** en cada archivo las líneas están en orden creciente de `Sequence` y sin huecos entre los registros de ese destino

#### Scenario: Archivo bloqueado durante una confirmación
Origen: AT43 y D-C14.
- **GIVEN** la bitácora de interacciones sin permiso de escritura
- **WHEN** una persona confirma un almuerzo
- **THEN** la confirmación termina autorizada como sin el bloqueo, con su comprobante, y su registro queda en la base pendiente de entrega

### Requirement: Reanudar tras una caída sin duplicar

Al arrancar, el escritor SHALL seguir desde la última línea completa de cada archivo y MUST NOT repetir un `AuditId` ya escrito. Una línea final incompleta MUST pasar entera a `<archivo>.reparacion.log` y salir del archivo, y su registro se vuelve a escribir desde la base. Si la línea del archivo y el registro de la base con esa `Sequence` difieren en `AuditId`, MUST NOT escribir ese archivo y abre una incidencia.

#### Scenario: Caída después de escribir y antes de seguir
Origen: AT44 (caída después del append y del flush).
- **GIVEN** un archivo cuyas últimas líneas ya tienen los registros hasta la `Sequence` 40 y un proceso que se cayó antes de escribir la 41
- **WHEN** el puesto vuelve a arrancar y el escritor entrega
- **THEN** el archivo tiene una sola línea por `AuditId`, de la 1 a la última de la base, sin repetir ninguna

#### Scenario: Línea parcial
Origen: AT44 (línea parcial y evidencia de reparación).
- **GIVEN** un archivo cuya última línea quedó cortada a la mitad y sin salto de línea
- **WHEN** el escritor entrega
- **THEN** los bytes cortados están en `<archivo>.reparacion.log`, el archivo termina en una línea completa de ese mismo registro y ninguna línea válida anterior se perdió

#### Scenario: Base y archivo no coinciden
Origen: RB24, borde: una base restaurada no pisa en silencio un archivo más nuevo.
- **GIVEN** un archivo cuya última línea tiene la `Sequence` 12 con un `AuditId` distinto del registro 12 de la base
- **WHEN** el escritor entrega
- **THEN** ese archivo no cambia, la base conserva todos sus registros y la bitácora general recibe una incidencia `LOG_SEQUENCE_MISMATCH` con el archivo y la secuencia

### Requirement: Una falla de archivo no detiene la atención

Si un archivo no se puede escribir, la atención SHALL seguir autorizando, el otro archivo MUST seguir recibiendo sus registros, y el puesto MUST abrir una sola incidencia por archivo con la hora, la causa y el primer `AuditId` pendiente, aunque la falla se repita. MUST NOT borrar pendientes para seguir.

#### Scenario: Falla la general
Origen: AT43 (fallar la escritura de cada archivo).
- **GIVEN** la bitácora general sin permiso de escritura y la de interacciones disponible
- **WHEN** una persona se identifica, confirma y el escritor corre tres veces
- **THEN** la atención termina autorizada, la de interacciones tiene sus líneas, la base tiene una sola incidencia `LOG_DELIVERY_FAILED` abierta para la general y sus registros pendientes

#### Scenario: Fallan las dos
Origen: AT43 (fallar ambos archivos con SQLite operativo) y HU03-H.
- **GIVEN** la carpeta de bitácoras sin permiso de escritura
- **WHEN** dos personas consumen
- **THEN** los dos consumos quedan autorizados con sus comprobantes y la base tiene una incidencia abierta por cada archivo y todos los registros pendientes

### Requirement: Recuperar un archivo

Cuando un archivo vuelve a admitir escritura, el escritor SHALL entregar lo pendiente en orden, con sus tiempos y su mes originales, `Recovered` verdadero y el `ExportedAtUtc` real, y recién entonces cerrar la incidencia con el intervalo degradado, que MUST quedar en los dos archivos.

#### Scenario: Recuperación de la general
Origen: AT43 (reconstruir hasta el corte sin duplicar consumos) y HU03-H.
- **GIVEN** la incidencia abierta de la general con tres registros pendientes
- **WHEN** vuelve el permiso de escritura y el escritor corre
- **THEN** la general tiene los tres con `Recovered` verdadero y su `OccurredAtUtc` original, una incidencia `LOG_DELIVERY_RECOVERED` con la apertura, el cierre y la cantidad entregada está en los dos archivos, y la base tiene los mismos consumos que antes

#### Scenario: Pendiente de un mes anterior
Origen: AT42 (exportación tardía del mes anterior).
- **GIVEN** registros de octubre pendientes por una falla que se recupera en noviembre
- **WHEN** el escritor los entrega
- **THEN** van al archivo de 202610, a continuación de lo que ya tenía, y no al de 202611

### Requirement: Configuración muestra el estado de las bitácoras

Configuración SHALL mostrar, para cada bitácora, si está al día o degradada, cuántos registros tiene pendientes y desde qué hora. MUST NOT mostrar datos de personas.

#### Scenario: Al día
Origen: HU03.5 (ver cuándo una bitácora quedó degradada).
- **GIVEN** las dos bitácoras sin pendientes
- **WHEN** se abre Configuración
- **THEN** dice que las dos están al día

#### Scenario: Degradada
Origen: AT43 (cola e intervalo degradado visibles).
- **GIVEN** la general sin permiso de escritura desde las 12:01 con cuatro registros pendientes
- **WHEN** se abre Configuración
- **THEN** dice que la general está degradada con 4 pendientes desde las 12:01 y que la de interacciones está al día

### Requirement: Una atención se reconstruye con su archivo

La bitácora de interacciones SHALL permitir reconstruir cada atención sin consultar la base: identificación aceptada o rechazada, oferta, cada opción elegida, confirmación pedida y su resultado, cancelación, inactividad y cierre con el código del mensaje mostrado, todos con la misma sesión. MUST NOT registrar las páginas ni los toques sin efecto.

#### Scenario: Atención autorizada
Origen: AT41 (sesión autorizada).
- **GIVEN** una persona que se identifica, elige un almuerzo, confirma y toca «Listo»
- **WHEN** se lee sólo su archivo de interacciones
- **THEN** sus líneas dicen, en orden y con la misma sesión, `SESSION_OPENED`, `OPTIONS_OFFERED`, `OPTION_SELECTED` con la etiqueta y el importe del almuerzo, `CONFIRMATION_REQUESTED`, `CONSUMPTION_AUTHORIZED` y `SESSION_CLOSED` con el código del mensaje de éxito

#### Scenario: Atención cancelada
Origen: AT41 (sesión cancelada).
- **GIVEN** una persona que se identifica, elige un almuerzo y toca «Cancelar»
- **WHEN** se lee su archivo de interacciones
- **THEN** termina en `SESSION_CANCELLED` con la opción que tenía elegida, sin `CONFIRMATION_REQUESTED` ni consumo

#### Scenario: Inactividad
Origen: AT41, borde: nadie toca nada.
- **GIVEN** una persona que se identifica y no toca nada más
- **WHEN** pasan 30 s
- **THEN** su atención termina en `SESSION_TIMED_OUT`, sin consumo

#### Scenario: Cambiar de página
Origen: el diseño de bitácoras («no se registran pulsaciones indiscriminadas»).
- **GIVEN** una persona con quince opciones ofrecidas
- **WHEN** pasa a la página siguiente y vuelve
- **THEN** no hay ninguna línea nueva por esos toques

### Requirement: Lo mostrado queda en el registro

El registro de una oferta SHALL llevar la etiqueta y el importe de cada opción ofrecida. El de una decisión MUST llevar la opción con su etiqueta, versión, importe, moneda y escala mostrados, la fecha de negocio y, si autoriza, los cargos por categoría con su límite. Cambiar después el catálogo MUST NOT cambiar lo registrado.

#### Scenario: Autorización con sus cargos
Origen: AT41 y el diseño de bitácoras («contexto mínimo para reconstruir sin consultar la base»).
- **GIVEN** una persona con límite 1 en ALMUERZO que confirma «Almuerzo común»
- **WHEN** se lee la línea de su autorización
- **THEN** su `Context` tiene «Almuerzo común», su versión, su importe, su moneda y su escala, la fecha de negocio y el cargo de ALMUERZO de 1 unidad con límite 1

#### Scenario: Rechazo al confirmar
Origen: AT41 (sesión rechazada).
- **GIVEN** una opción ofrecida que se agota antes de confirmar
- **WHEN** se lee la línea de su rechazo
- **THEN** su `Context` tiene la opción con su etiqueta, el motivo `LOCAL_LIMIT_REACHED` y la fecha de negocio, sin cargos ni comprobante

### Requirement: El puesto y Configuración quedan en la general

La bitácora general SHALL registrar el arranque y el cierre ordenado del puesto, con la versión de la aplicación, del runtime, del sistema operativo y del esquema, y la versión de configuración; abrir, guardar y cancelar Configuración; y cada cambio de un ajuste con su valor anterior y el nuevo. Sin actor autenticado, el actor MUST ir nulo con el motivo `NO_AUTHENTICATION`.

#### Scenario: Guardar el estilo
Origen: pedido de David del 04/11/2026 («tanto personas como administradores») y el diseño de administración («actor, revisión anterior y nueva, campos cambiados sin secretos, hora y resultado»).
- **GIVEN** Configuración con opacidad 76 y desenfoque 4
- **WHEN** se lleva la opacidad a 81 y se toca «Guardar»
- **THEN** la general tiene `CONFIGURATION_CHANGED` con el ajuste del estilo, antes 76 y 4, después 81 y 4, el resultado y el actor nulo con `NO_AUTHENTICATION`

#### Scenario: Guardar sin poder escribir
Origen: borde: un guardado fallido también se reconstruye.
- **GIVEN** una carpeta de datos donde no se puede escribir el estilo
- **WHEN** se toca «Guardar»
- **THEN** la general tiene `CONFIGURATION_CHANGED` con el resultado fallido y el valor que quedó vigente

#### Scenario: Versión desconocida
Origen: el diseño de diagnóstico («valor desconocido explícito, nunca versión inventada»).
- **GIVEN** un arranque en el que una versión no se puede leer
- **WHEN** se lee la línea `KIOSK_STARTED`
- **THEN** esa versión figura nula y las demás con su valor
