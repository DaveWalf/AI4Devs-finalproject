# INComedor — Épicas, historias y tickets

Autor: David Walfisch · Iniciales: DW · Versión documental: 1.1 · Fecha: 29/09/2026 · Estado: **diseñados; sin implementación ni pruebas ejecutadas**.

Las historias aplican la decisión vigente de **autorización y persistencia locales sobre SQLite, sin dependencia de INWeb online**. La alternativa de suspensión multipuesto limita nuevas confirmaciones según D-A02, manteniendo la decisión local. El MVP usará adaptadores de prueba declarados.

El trabajo se presenta en cuatro niveles, para que cada pieza tenga el tamaño que le corresponde:

| Nivel | Elementos | Tamaño y uso |
| --- | --- | --- |
| Épica de producto | HU01, HU02 y HU03 | Agrupan los 35 escenarios por objetivo. Ninguna cabe en una entrega; se descomponen en historias hijas. |
| Historia de usuario | HU01.1, HU02.1 y HU02.2 (principales) y el [backlog de historias hijas](#backlog-de-historias-hijas) | Una persona usuaria de la [SDD §1](especificacion-sdd.md#1-problema-objetivo-y-personas-usuarias) y de 1 a 4 escenarios; tallas preliminares S/M/L, pendientes de reestimación incremental en M0. |
| Épica técnica | TK01 backend, TK02 interfaz y TK03 datos | Paquetes de trabajo que el [plan académico](plan-academico.md) divide en las unidades U01–U09. |
| Ticket de tarea | TK03.1 datos, TK02.1 interfaz y TK01.1 backend | Tarea ejecutable de una unidad, con 3 a 4 AT propios, un responsable y dependencias que deben estar disponibles antes de su integración. |

Tallas: **S** de 2 a 4 h, **M** de 4 a 8 h y **L** de 8 a 16 h de trabajo asistido. Prioridad: **must** acredita la funcionalidad; **should** integra el compromiso de la entrega final, pero se difiere primero según la [regla de recorte](plan-academico.md#regla-de-recorte).

El caso habitual es un puesto autónomo. En multi puesto con auditoría, todos informan en línea a INWeb y reciben inmediatamente los consumos de sus pares para actualizar cupos locales; una carrera puede causar exceso y debe generar aviso auditable. La conducta al desconectarse es configurable por instalación: `CONTINUE_LOCAL` (valor inicial) o `SUSPEND_NEW_CONSUMPTIONS`; D-A02 cerrada el 29/09. Los datos de la demo son una propuesta sintética y el cliente permanece anónimo.

[Especificación SDD](especificacion-sdd.md) · [Alcance académico](../readme.md) · [Arquitectura](arquitectura.md) · [Modelo de datos](modelo-datos.md) · [OpenAPI de sincronización](../api/openapi.yaml).

## Historias principales

Son las tres historias que presenta el README. Se prevén iteraciones cortas; sus tallas son hipótesis pendientes de reestimación incremental en M0. Los tickets y sus colaboradores cubren el recorrido de cada historia.

### HU01.1 — Confirmar un servicio sin red y sin duplicarlo

**Épica:** HU01 · **Persona:** Comensal · **Prioridad:** must · **Talla:** M · **Tickets:** TK02.1 (interfaz), TK03.1 (datos) y TK01 (dominio, principal de AT01).

**Como** Comensal, **quiero** elegir un servicio del catálogo y confirmarlo en el puesto aunque no haya red, **para** recibir mi comprobante sin que un toque repetido, una cancelación o un cambio de precio alteren lo que consumí.

**Precondiciones:** base provisionada con el fixture sintético, sesión de identidad abierta por el adaptador de prueba (la identificación es HU01.2) y perfil monopuesto o `CONTINUE_LOCAL`.

| Escenario | Dado | Cuando | Entonces |
| --- | --- | --- | --- |
| HU01-A | Persona sintética habilitada, cupo local libre y red ausente | Confirma servicio | Una decisión autorizada, consumo/cupo/evento locales y ticket lógico |
| HU01-B | Catálogo de 15 opciones | Navega y cancela antes de confirmar | Navegación completa y cero consumo |
| HU01-D | Catálogo cambió entre mostrar y confirmar | Confirma valor anterior | Se informa el cambio y se solicita reconfirmación; no cambia el importe silenciosamente |
| HU01-G | Operación confirmándose | Se repite toque o llega otra lectura | Conserva persona/intención; no duplica consumo |

**Aceptación:** AT01, AT16, AT26 y AT47. **INVEST:** no depende del lector real (usa el doble), es negociable en su presentación, aporta el valor central del puesto, se estima como M, tiene cuatro escenarios y se verifica con esos AT.

**Terminado:** una persona sintética confirma un servicio con la red desconectada y recibe su comprobante; cancelar no deja consumo, un segundo toque no crea otro y un cambio de precio obliga a reconfirmar.

### HU02.1 — Recuperar el resultado después de una caída

**Épica:** HU02 · **Persona:** Soporte técnico · **Prioridad:** must · **Talla:** S · **Tickets:** TK03.1 (datos) y TK01 (contrato de replay/conflicto, principal de AT25).

**Como** Soporte técnico, **quiero** que el puesto recupere tras un reinicio el resultado de una confirmación ya registrada, **para** resolver la caída sin cobrar dos veces ni perder el comprobante.

**Precondiciones:** base provisionada y una confirmación ya guardada por la transacción local.

| Escenario | Dado | Cuando | Entonces |
| --- | --- | --- | --- |
| HU02-B | Consumo confirmado localmente | El proceso cae antes de mostrar el resultado | Recupera mismos IDs y un solo trabajo original; no vuelve a consumir cupo |
| HU02-L | Decisión local durable y posterior vencimiento de sesión, baja o cambio de snapshot | Reintenta misma intención/ámbito/payload | Recupera resultado original sin repetir validación de negocio ni crear consumo/ticket/trabajo; una intención nueva sí usa reglas actuales |

**Aceptación:** AT05, AT25 y AT35.

**Terminado:** después de cortar el proceso en cada punto de la confirmación, el puesto vuelve a mostrar el mismo resultado con los mismos identificadores, sin un segundo consumo, y rechaza reutilizar la intención con otro contenido.

### HU02.2 — Reenviar lotes y consolidarlos sin duplicar

**Épica:** HU02 · **Persona:** Soporte técnico · **Prioridad:** must · **Talla:** M · **Tickets:** TK01.1 (receptor, principal de AT27) y TK01/TK03 (cliente sincronizador y cola local de U05).

**Como** Soporte técnico, **quiero** que un lote reenviado después de perder el acuse se reconozca como el mismo, **para** recuperar los pendientes sin duplicar consumos en la consolidación ni ocultar conflictos.

**Precondiciones:** receptor académico con su base de prueba y eventos pendientes en el puesto.

| Escenario | Dado | Cuando | Entonces |
| --- | --- | --- | --- |
| HU02-C | Hay eventos pendientes | Vuelve la conexión | Envía lote con IDs estables; el puesto sigue atendiendo durante sincronización |
| HU02-D | El servidor recibió el lote pero se perdió el ACK | Se reenvía o consulta el mismo lote | Deduplicación central; no se crea otro consumo ni se pierde el evento local |
| HU02-F | Servidor detecta discrepancia de cupo entre puestos | Concilia los hechos | Conserva ambos, informa conflicto y pide revisión; no niega retroactivamente una comida |
| HU02-G | Credencial de sincronización vencida | Se intenta enviar lote | Error técnico visible y evento retenido; en monopuesto sigue atención; nueva operación multi puesto aplica CONTINUE_LOCAL o SUSPEND_NEW_CONSUMPTIONS persistido |

**Aceptación:** AT12, AT13, AT27 y AT50.

**Terminado:** el receptor devuelve el mismo acuse ante reenvío/consulta, detecta contenido incompatible y rechaza hechos ajenos; el cliente mantiene atención según D-A02, retiene eventos ante error y avanza su cola sólo por acuse durable. Se conserva el consumo local.

## Tickets de trabajo

Cada ticket es una tarea de una unidad del plan. Sus dependencias están disponibles al iniciarla y su definición de terminado cita sólo sus AT propios; la [matriz de aceptación](plan-academico.md#matriz-de-aceptación) lo nombra como responsable principal de esos AT. Responsable de los tres: DW, autor único del proyecto.

### TK03.1 — Datos: migración inicial y transacción de confirmación

**Tipo:** base de datos · **Épica técnica:** TK03 · **Unidad:** U02 · **Hito:** M1 · **Talla:** M · **Historias:** HU01.1, HU01.3 y HU02.1 · **Responsable:** DW · **Estado:** diseñado; preparación en M0 y dependencias de integración por completar.

**Objetivo:** crear la primera migración SQLite y la unidad de trabajo que guarda una decisión en una sola transacción (RB06), para que la confirmación sea todo o nada y se recupere sin duplicarse.

**Trabajo incluido:**

1. Migración numerada `0001` con las tablas de la transacción (intención, consumo, consumo conocido propio y sus cargas, uso de cupo, comprobante, trabajo de impresión, evento de salida y registro de bitácora) y las de configuración que sus claves referencian, con los nombres del [mapa](nomenclatura-integracion.md).
2. Apertura con `PRAGMA foreign_keys = ON`, comprobación de la versión de esquema y rechazo de una base con versión desconocida.
3. Búsqueda previa por `IntentId`: si existe una decisión con el mismo hash, devolverla; si el hash difiere, informar conflicto sin escribir.
4. Unidad de trabajo con `BEGIN IMMEDIATE`: reconsulta IntentId/hash bajo bloqueo, lee snapshot activo y cupo e invoca al dominio de TK01 dentro de esa misma transacción. Persiste la decisión resultante antes del commit: autorización con intención, consumo, cargas, contador, ticket, copia 0, evento y bitácora; rechazo con intención, decisión, evento y bitácora sin consumo/ticket. La lectura previa es orientativa, nunca una autorización lista para guardar.
5. Unicidades del modelo: consumo por intención, ticket por consumo, `(TicketId, CopyNumber)` y clave de `QuotaUsage`.

**Fuera de alcance:** implementar las reglas de habilitación y cupo (las aporta TK01); su invocación transaccional y la integración de AT03 sí están incluidas. Se excluyen pantallas, sincronización, feed de pares, escritura de archivos de bitácora y backup.

**Dependencias disponibles:** [diccionario](modelo-datos.md) y mapa de nombres, fixture sintético del [OpenAPI](../api/openapi.yaml) y contrato de intención cerrado en M0 (U01). Para cerrar AT03 se requiere el evaluador de dominio de TK01 integrado; TK03.1 conserva la responsabilidad de su evidencia.

**Archivos previstos:** `src/Intelektron.INComedor.Infrastructure.Sqlite/Migraciones/0001_transaccion_consumo.sql`, la unidad de trabajo en el mismo proyecto y sus pruebas en `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests`.

| AT propio | Qué demuestra |
| --- | --- |
| AT03 | Dos conexiones SQLite reales compiten por el último cupo: relectura y decisión bajo bloqueo producen una autorización y un rechazo; mismo IntentId concurrente no duplica decisión. |
| AT05 | Una caída después del commit recupera los mismos IDs, un solo consumo y un solo trabajo original. |
| AT35 | Repetir una intención ya decidida devuelve el resultado original aunque después cambien la sesión, el padrón o el catálogo. |

**Terminado:** la migración crea una base vacía válida y los tres AT pasan contra un archivo SQLite real, con el comando y su salida registrados. El ensayo de caída de proceso no acredita resistencia a la pérdida física del disco.

### TK02.1 — Interfaz: selección y confirmación con sus estados

**Tipo:** frontend · **Épica técnica:** TK02 · **Unidad:** U03 · **Hito:** M1 · **Talla:** M · **Historia:** HU01.1 · **Responsable:** DW · **Estado:** diseñado; preparación en M0 y dependencias de integración por completar.

**Objetivo:** construir en WPF + Blazor Hybrid las pantallas de catálogo, confirmación y resultado sobre el caso de uso local, con estados que impidan confirmar dos veces o cambiar de persona durante la confirmación.

**Trabajo incluido:**

1. Estados de la atención: reposo, selección, confirmando (botón ocupado), resultado y regreso a reposo; una lectura nueva durante la confirmación no cambia la persona.
2. Catálogo paginado de 15 opciones sintéticas, operable por toque y teclado; cancelar o esperar 30 s antes de confirmar vuelve a reposo sin consumo.
3. Un `IntentId` por confirmación: se genera al entrar en «confirmando» y un toque repetido reutiliza el mismo.
4. Reconfirmación cuando el caso de uso informa un cambio de precio o de versión del menú.
5. Mensajes de resultado según el [catálogo de estados](experiencia.md#estados-y-mensajes-propuestos): autorizado, rechazo por motivo y error de almacenamiento.

**Fuera de alcance:** identificación (el adaptador de prueba entrega la sesión), impresión física, administración y supervisión.

**Dependencias disponibles:** para construir componentes, caso de uso con doble, fixture y mensajes de M0; para cerrar los AT y la demostración, TK01 y U02/TK03.1 integrados con SQLite real. M0 solo no basta para el cierre integral.

**Archivos previstos:** componentes Razor en `src/Intelektron.INComedor.Kiosk/Atencion/` y sus pruebas de componentes en `tests/Intelektron.INComedor.Kiosk.Tests`.

| AT propio | Qué demuestra |
| --- | --- |
| AT16 | El catálogo de 15 opciones se recorre y, si vence el tiempo antes de confirmar, no hay consumo y la siguiente persona empieza limpia. |
| AT26 | Un doble toque o una lectura nueva durante la confirmación dejan una sola intención y la persona original. |
| AT47 | Un cambio de precio entre la selección y la confirmación exige reconfirmar antes de crear el consumo. |

**Terminado:** los tres AT pasan en pruebas de componentes y en una demostración grabada sobre el equipo declarado, con persistencia local real del consumo.

### TK01.1 — Backend: receptor idempotente de lotes

**Tipo:** backend · **Épica técnica:** TK01 · **Unidad:** U05 · **Hito:** M1 · **Talla:** M · **Historia:** HU02.2 · **Responsable:** DW · **Estado:** diseñado; preparación en M0 y dependencias de integración por completar.

**Objetivo:** implementar en el receptor académico ASP.NET Core las operaciones `POST /api/incomedor/v1/sync-batches` y `GET /api/incomedor/v1/sync-batches/{batchId}` del [OpenAPI](../api/openapi.yaml), sobre SQL Server de prueba, sin duplicar hechos ante reintentos.

**Trabajo incluido:**

1. Autenticar el equipo con el certificado de prueba y limitar cada consulta a sus propios lotes; un lote ajeno responde como desconocido.
2. Exigir `Idempotency-Key` igual a `BatchId` y calcular el hash RFC 8785 del cuerpo; la misma clave con otro cuerpo responde 409 sin alterar el original.
3. Guardar recibo del lote y eventos de entrada en una transacción, con un resultado por `EventId`: aceptado, duplicado ya proyectado, conflicto de contenido o rechazado por evento inválido.
4. Validar por aplicación que las cargas de cupo tengan la fecha de negocio del consumo; una diferencia se rechaza como `INVALID_EVENT` y se conserva la evidencia.
5. Registrar un caso de conciliación cuando dos terminales informan hechos incompatibles, sin modificar ni denegar el consumo.
6. `GET` devuelve el acuse original durable; mientras procesa, responde 202.

**Fuera de alcance:** `GET /configuration` (otra tarea de U05), cliente sincronizador del puesto, feed de pares (U07) y adaptador INWeb real.

**Dependencias disponibles:** OpenAPI 0.6.0-draft y certificados sintéticos de M0. El cierre integral de AT13/27 requiere cliente sincronizador y cola SQLite de U05, desarrollados por TK01/TK03; TK01.1 coordina la evidencia del receptor y del recorrido cliente-servidor.

**Archivos previstos:** controlador y servicio de ingesta en `src/Intelektron.INComedor.Sync.Api/`, migración de SQL Server en `src/Intelektron.INComedor.Integration.INWeb/` y pruebas de contrato en `tests/Intelektron.INComedor.Sync.Api.Tests`.

| AT propio | Qué demuestra |
| --- | --- |
| AT12 | Hechos incompatibles de dos terminales quedan conservados y abren revisión, sin negar la comida. |
| AT13 | Una credencial de equipo inválida o un lote ajeno se rechazan sin revelar datos y sin afectar los hechos locales. |
| AT27 | Reenviar o consultar un lote tras perder el acuse devuelve el mismo resultado y no cuenta dos veces. |
| AT50 | Un evento cuyas cargas tienen otra fecha de negocio se rechaza como inválido y se conserva para revisión. |

**Terminado:** pruebas de contrato del receptor contra SQL Server y ejemplos OpenAPI válidos; además AT13 y AT27 integran cliente y cola SQLite: error retiene eventos, ACK perdido permite reenvío y sólo el acuse durable avanza la cola. La prueba aislada del receptor no cierra HU02.2.

## Backlog de historias hijas

Descompone las tres épicas en historias de tamaño real. Cubre los 35 escenarios; la columna Unidad remite al [plan académico](plan-academico.md#unidades-de-trabajo-y-tallas).

| Historia | Épica | Persona | Necesidad resumida | Escenarios | Unidad | Talla | Prioridad |
| --- | --- | --- | --- | --- | --- | --- | --- |
| HU01.1 | HU01 | Comensal | Confirmar un servicio sin red y sin duplicarlo | A, B, D, G | U03, U02 | M | must |
| HU01.2 | HU01 | Comensal | Identificarse por 1:N o por código con 1:1, con credencial vigente | E, F, J | U03, U01 | M | must |
| HU01.3 | HU01 | Comensal | Recibir la decisión según cupo y reglas locales vigentes | C, H, I | U01, U02 | M | must |
| HU02.1 | HU02 | Soporte técnico | Recuperar el resultado tras una caída | B, L | U02 | S | must |
| HU02.2 | HU02 | Soporte técnico | Reenviar lotes y consolidarlos sin duplicar | C, D, F, G | U05 | M | must |
| HU02.3 | HU02 | Soporte técnico | Arrancar sin red y conservar la última configuración válida | A, E | U02, U05 | S | must |
| HU02.4 | HU02 | Administrador de personal | Que cada puesto cuente los consumos de los demás y se auditen los excesos | H, I, J, K | U07 | L | must |
| HU02.5 | HU02 | Administrador de personal | Seguir atendiendo con `CONTINUE_LOCAL` durante una pérdida de comunicación | M | U07 | S | must |
| HU02.6 | HU02 | Administrador de personal | Suspender nuevas confirmaciones y cambiar la política por snapshot | N, O, P | U07 | M | must |
| HU03.1 | HU03 | Operador | Recuperar o reimprimir un comprobante con permiso | A, B, C, I | U04 | M | must |
| HU03.2 | HU03 | Operador | Consultar el estado del puesto y cerrar la sesión | D, E | U08 | S | must |
| HU03.3 | HU03 | Administrador del Sistema | Configurar el equipo sin red, con acceso alternativo de mantenimiento | F | U08 | M | must |
| HU03.4 | HU03 | Administrador del Sistema | Diagnosticar los componentes: SQLite, API e impresora son must; el resto de la matriz, should | G | U08 | M | must y should |
| HU03.5 | HU03 | Administrador del Sistema | Seguir atendiendo con una bitácora degradada y reconstruirla | H | U06 | M | must |

## Épicas de producto

### Épica HU01 — Identificarse y consumir de forma autónoma

**Como** comensal habilitado, **quiero** identificarme, elegir y confirmar un servicio en el puesto, **para** obtener mi comprobante aunque INWeb o la red no estén disponibles.

**Valor:** continuidad desatendida con reglas y datos locales; evita que una elección accidental genere dos consumos.

**Precondiciones:** puesto provisionado con base íntegra, padrón/templates y reglas aplicables, almacenamiento operativo y adaptadores disponibles. La conectividad no es precondición en monopuesto. En multi puesto auditado con CONTINUE_LOCAL se mantiene atención ante pérdida de intercambio; con SUSPEND_NEW_CONSUMPTIONS se exige comunicación/feed recuperados para una nueva confirmación. Los escenarios de autonomía de HU01 y HU02-A/E se ejecutan en monopuesto o CONTINUE_LOCAL; HU02-N/O prueban la alternativa.

**Flujo principal:** identidad local → sesión local → catálogo → selección → confirmación → transacción SQLite → impresión → reposo. La transacción guarda intención, decisión, consumo, ObservedConsumption propio con sus ObservedQuotaCharge, contador QuotaUsage, ticket, PrintJob original, outbox y AuditRecord; sincronizar después no otorga el permiso. La impresión física ocurre después del commit.

El producto soporta 1:N sobre templates habilitados y código previo con verificación 1:1. Ambos devuelven el ID asociado para continuar. En el MVP, los dobles de prueba ejercitan contratos y estados sin atribuirse matching físico.

| Escenario | Dado | Cuando | Entonces |
| --- | --- | --- | --- |
| HU01-A | Persona sintética habilitada, cupo local libre y red ausente | Confirma servicio | Una decisión autorizada, consumo/cupo/evento locales y ticket lógico |
| HU01-B | Catálogo de 15 opciones | Navega y cancela antes de confirmar | Navegación completa y cero consumo |
| HU01-C | Último cupo local disponible | Dos confirmaciones locales concurren | Una autoriza, otra deniega; contador no supera límite |
| HU01-D | Catálogo cambió entre mostrar y confirmar | Confirma valor anterior | Se informa el cambio y se solicita reconfirmación; no cambia el importe silenciosamente |
| HU01-E | Código de A, huella de B | Se verifica 1:1 | Rechazo; no cambia a B ni autoriza con A |
| HU01-F | Conjunto local 1:N válido | Hay una coincidencia habilitada | ID correcto y evaluación local de derecho/cupo, sin INWeb |
| HU01-G | Operación confirmándose | Se repite toque o llega otra lectura | Conserva persona/intención; no duplica consumo |
| HU01-H | Regla local sin vencimiento y un período prolongado sin red | Persona válida confirma | Continúa según esa regla; no inventa caducidad de caché |
| HU01-I | Persona activa/credencial vigente; permiso general y regla vigente aplicable `Limit=0` | Confirma nueva intención | Deniega por regla explícita incluso si el permiso general tiene Priority mayor; al expirar la regla cero se evalúan las restantes |
| HU01-J | Código sintético vencido, revocado, todavía no vigente o desconocido | Se presenta en el puesto | Motivo propio (CREDENTIAL_EXPIRED, CREDENTIAL_REVOKED o IDENTITY_NOT_RESOLVED); no abre selección ni crea consumo; la decisión queda auditada sin el código en claro |

**Trazabilidad:** F01/F02; RF01–RF11/21/23/28/33–RF36; RB01–RB07/10/13–RB15/25; AT01–AT04/07/08/16/17/19–AT26/28/29/36/46–49/52/53.

En multi puesto, HU01-C sigue comprobando concurrencia dentro de una sola base. El cupo local también debe incorporar eventos ya recibidos de otras terminales; la carrera entre bases se cubre en HU02-H/I.

**Exclusiones:** reservas, pagos, préstamo de identidad y garantía global de cupo entre terminales aisladas sin política aprobada.

**Aceptación:** demostrar autonomía con conectividad efectivamente ausente y persistencia real del incremento. Los ensayos biométricos se reportan como contrato con doble o como hardware homologado, nunca mezclados.

### Épica HU02 — Recuperar y sincronizar consumos sin perder autonomía

**Como** Soporte técnico, **quiero** que el puesto conserve los consumos durante reinicios y desconexiones y los envíe cuando vuelva la red, **para** disponer de trazabilidad, preservar la atención según la política configurada y evitar duplicaciones.

Las historias hijas de multipuesto (HU02.4 a HU02.6) tienen como persona al Administrador de personal, que publica la política de la instalación y revisa los avisos de exceso.

**Valor:** separa la operación del comedor de la disponibilidad administrativa y evita que un ACK perdido cambie el resultado local.

**Precondiciones:** base previamente provisionada e íntegra; almacenamiento suficiente. La primera provisión puede recibirse como paquete local validado.

| Escenario | Dado | Cuando | Entonces |
| --- | --- | --- | --- |
| HU02-A | Base operativa instalada y red ausente | Arranca el equipo desde apagado | Puede identificar, autorizar y generar comprobante; cola queda pendiente |
| HU02-B | Consumo confirmado localmente | El proceso cae antes de mostrar el resultado | Recupera mismos IDs y un solo trabajo original; no vuelve a consumir cupo |
| HU02-C | Hay eventos pendientes | Vuelve la conexión | Envía lote con IDs estables; el puesto sigue atendiendo durante sincronización |
| HU02-D | El servidor recibió el lote pero se perdió el ACK | Se reenvía o consulta el mismo lote | Deduplicación central; no se crea otro consumo ni se pierde el evento local |
| HU02-E | Nueva configuración incompleta o incompatible | Se interrumpe la descarga | Conserva la última base válida y sus vigencias; no bloquea sólo por falta de red |
| HU02-F | Servidor detecta discrepancia de cupo entre puestos | Concilia los hechos | Conserva ambos, informa conflicto y pide revisión; no niega retroactivamente una comida |
| HU02-G | Credencial de sincronización vencida | Se intenta enviar lote | Error técnico visible y evento retenido; en monopuesto sigue atención; nueva operación multi puesto aplica CONTINUE_LOCAL o SUSPEND_NEW_CONSUMPTIONS persistido |
| HU02-H | A consumió y B recibió el evento | B intenta el mismo cupo | B cuenta el hecho de A y deniega localmente por límite |
| HU02-I | A y B confirman antes de recibir el evento del otro | INWeb consolida ambos | Conserva hechos y genera aviso de exceso con referencias, tiempos y puestos; no anula automáticamente |
| HU02-J | Evento propio llega como eco o el de un par se repite | Se incorpora al ledger | Se cuenta una vez, no genera ticket ni consumo nuevo y queda trazabilidad |
| HU02-K | B cae al incorporar el consumo de A | Recupera y continúa el feed | Ledger, contador y deduplicación quedan coherentes con una sola aplicación |
| HU02-L | Decisión local durable y posterior vencimiento de sesión, baja o cambio de snapshot | Reintenta misma intención/ámbito/payload | Recupera resultado original sin repetir validación de negocio ni crear consumo/ticket/trabajo; una intención nueva sí usa reglas actuales |
| HU02-M | MULTI_KIOSK_AUDITED y política inicial CONTINUE_LOCAL | Falla salida a INWeb o recepción del feed | Continúa según reglas/cupos locales; registra incidencia y vincula hechos para conciliación/aviso posterior |
| HU02-N | MULTI_KIOSK_AUDITED y SUSPEND_NEW_CONSUMPTIONS persistido | Falla comunicación o arranca sin ella | Nueva confirmación denegada por motivo técnico, sin consumo/ticket; replay, diagnóstico y comprobantes anteriores disponibles |
| HU02-O | Perfil suspendido; red vuelve pero feed tiene huecos | Se recuperan envío y recepción hasta cortes acordados | No reanuda por un ping; reanuda tras recuperación íntegra, con IDs originales y excesos auditables |
| HU02-P | Snapshot con cambio autorizado de política | Se activa completo, o falla/reinicia durante activación | Cambio atómico y auditable para futuras confirmaciones; conserva última política válida y hechos previos |

**Trazabilidad:** F02/F04; RF10/11/20–RF22/24–RF26/30/31/37–RF39; RB06/07/14–RB20; AT05/09/10/12/13/25/27/30–AT35/50/55; RNF01/04/05/06/11.

**Decisión confirmada D-A02:** configuración por instalación con continuidad local inicial y suspensión alternativa. HU02-F/H/I conservan propagación y avisos; los excesos detectados después de aislamiento también se auditan, sin exclusión global fuerte.

**Aceptación:** probar arranque frío y consumo offline, caída posterior al commit local y pérdida de ACK después de persistencia central. Un test de request que nunca salió del cliente no cubre la duplicación de sincronización.

### Épica HU03 — Supervisar el puesto y recuperar un comprobante

**Como** Operador, **quiero** ver el estado y recuperar un comprobante, **para** resolver una incidencia sin registrar otra comida ni perder la auditoría.

**Como** Administrador del Sistema, **quiero** configurar y diagnosticar el puesto sin red y ver cuándo una bitácora quedó degradada, **para** mantenerlo operativo sin modificar las reglas maestras de INWeb.

**Valor:** separa efecto económico, sincronización y papel físico.

**Precondiciones:** permiso local vigente del rol que exige la acción (RB26): `OPERATOR` para recuperar o reimprimir y `SYSTEM_ADMINISTRATOR` para configurar y diagnosticar. El actor se autentica sin red con su código y verificación 1:1; los permisos llegan con el snapshot activo ([SDD §7.4](especificacion-sdd.md#74-credenciales-y-control-de-acceso-local)). Recuperar un comprobante requiere además consumo confirmado y ticket existente.

Escenarios del Operador:

| Escenario | Dado | Cuando | Entonces |
| --- | --- | --- | --- |
| HU03-A | Trabajo fallido por papel | Operador solicita recuperación | Conserva consumo/ticket; acción auditada según estado del trabajo |
| HU03-B | Papel pudo salir, pero el resultado es incierto | Solicita nueva copia con motivo | Aviso de incertidumbre, número de copia y leyenda DUPLICADO |
| HU03-C | Persona sin permiso | Intenta reimprimir | Denegación sin generar trabajo ni alterar consumo |
| HU03-D | API caída, cola pendiente y periférico desconectado | Abre diagnóstico local | Distingue estados, versiones y última sincronización; sin biometría ni secretos |
| HU03-E | Finaliza asistencia | Cierra o expira sesión | Vuelve a reposo sin privilegios ni identidad de la persona anterior |
| HU03-I | Operador con permiso vencido o revocado por el snapshot activo | Solicita una reimpresión sin red | Denegación auditada con actor, rol y motivo; sin trabajo nuevo ni cambio de consumo |

Escenarios del Administrador del Sistema:

| Escenario | Dado | Cuando | Entonces |
| --- | --- | --- | --- |
| HU03-F | Administrador con permiso local y sin red | Valida y aplica parámetros del equipo | Revisión íntegra y auditada; no modifica reglas maestras |
| HU03-G | Componentes reales o simulados declarados | Ejecuta diagnóstico de la matriz | Resultado/versión/correlación por componente, sin consumo ni interferencia con sesión |
| HU03-H | Un archivo de bitácora no admite escritura; SQLite sí | Se registran interacciones y vuelve la escritura | Atención continúa con respaldo durable; reconstrucción conserva mes/IDs e intervalo degradado |

**Trazabilidad:** F03/F04; RF10/12/13/17/24/26/36/40/41/43–46; RB05/08/21/23/24/26; AT06/24/37–39/41–45/54; RNF08/11–14.

**Exclusiones:** asignación de derechos, cambios de precio, anulación/restauración de cupo y administración completa de INWeb.

**Aceptación:** comparar consumos e importes antes/después de recuperar o reimprimir; verificar actor, rol, motivo y copia. Una aceptación del spooler no se reporta como papel entregado sin evidencia física.

## Épicas técnicas

Agrupan el trabajo por capa. Sus tickets de tarea son TK01.1, TK02.1 y TK03.1; el resto de su trabajo se descompone en las unidades del plan. Cada terminado se expresa en lenguaje llano y remite a la [matriz de aceptación](plan-academico.md#matriz-de-aceptación), que asigna cada AT a un único responsable principal.

### TK01 — Backend: autorización local e ingesta idempotente

**Tipo:** backend/lógica de aplicación · **Estado:** diseñado · **Épicas de producto:** HU01/HU02/HU03 · **Unidades:** U01, U04, U05 y U07 · **Ticket de tarea:** TK01.1.

**Objetivo:** implementar el dominio/caso de uso que decide localmente y el receptor de prueba que consolida eventos sin duplicarlos. El backend local autoriza; el remoto recibe hechos.

**Trabajo incluido:**

1. Localizar primero `IntentId` y verificar ámbito/pertenencia y hash canónico del payload: decisión durable compatible se devuelve sin revalidar sesión/política/cupo; reutilización incompatible produce conflicto.
2. Sólo para una intención nueva, TK03.1 invoca al dominio dentro de BEGIN IMMEDIATE con snapshot y cupo releídos bajo bloqueo: validar sesión local, persona/credencial (RB25), catálogo, cantidad y cupo. Aplicar RB02: filtrar vigencia `[ValidFromUtc, ValidToUtc)` con extremos opcionales, denegación `Limit=0` prioritaria y mayor `Priority` entre reglas positivas; no llamar a INWeb para autorizar.
3. Proveer a TK03.1 el evaluador de dominio: se ejecuta con los datos leídos dentro de la transacción y devuelve la decisión que esa misma unidad persiste; no se entrega una autorización precalculada fuera del bloqueo.
4. Crear el cliente sincronizador de U05 con TK03 y el receptor académico de lotes TK01.1, con deduplicación por origen/ID y resultado durable por evento; el ticket de receptor coordina AT27 pero no implementa el cliente.
5. Registrar conflictos de consolidación sin mutar el consumo local ni convertir ACK en permiso.
6. Persistir AuditRecord con cada decisión/transición, incluyendo rechazo; registrar interacciones sin consumo y correlación segura, sin credenciales ni biometría. El archivo no condiciona la atención si SQLite respalda durablemente la auditoría (D-C14).
7. Disparar envío al commit conectado, consolidar y propagar hechos de pares con cursor recuperable; no esperar un lote periódico en el modo multi puesto normal.
8. Aplicar ledger/contador local por persona, ámbito, categoría y día; deduplicar eco propio y crear avisos de exceso identificables y no repetidos.
9. Aplicar D-A02 en nuevas confirmaciones: validar política/estado de comunicación locales, emitir motivo técnico de suspensión sin consumo y conservar replay previo. Persistir incidentes/transiciones con outbox; reanudar tras recuperar intercambio/feed, no por un ping.
10. Comprobar en cada acción privilegiada el permiso vigente del rol requerido (RB26) y auditar RequestedRole, resultado y causa, con actor y grant sólo cuando existen. Integrar la autenticación individual de mantenimiento de U08 sin conceder roles ni permiso de consumo.

**Dependencias:** fixture sintético de cupo/día, sesión local, política de reloj D-C17, catálogo y modelo. El contrato de señal de vida y corte del feed se especificará en M0 y es condición para iniciar U07; la propagación conectada y el aviso de exceso están definidos.

**Fuera de alcance:** SDK biométrico, enrolamiento real, liquidaciones, hardware y autorización central de consumos.

| Prueba prevista | Evidencia exigida |
| --- | --- |
| Monopuesto/CONTINUE_LOCAL con INWeb y red inaccesibles | Consumo completo local y outbox durable |
| Replay después de vencer sesión o recibir baja/cambio de snapshot | Misma decisión/IDs y trabajo original; intención nueva aplica reglas actuales |
| Misma clave/payload distinto | Conflicto sin cambio de decisión original |
| Persona activa, regla cero vigente y permiso de prioridad mayor | Denegación explícita; baja/revocación son motivos distintos; el fin exclusivo excluye la regla cero |
| Reglas positivas de igual prioridad y efectos contradictorios | Publicación inválida; no resolver arbitrariamente por orden de lectura |
| Credencial vencida, revocada o desconocida; operador sin permiso vigente | Motivos separados y denegación auditada, sin consumo ni trabajo |
| Lote repetido tras perder ACK | Receptor deduplica y conserva mismo resultado; no vuelve a contar |
| Evento de A recibido por B antes de confirmar | B lo incluye en su cupo y evita consumo excedido |
| Carrera A/B, eco propio y repetición | Aviso auditable por exceso; cada hecho contado una vez; sin impresión por importación |

**Terminado:** el puesto decide con sus reglas y cupos y recupera decisiones sin repetirlas. Los AT propios de TK01 tienen evidencia de dominio y SQLite real; los de TK01.1, de receptor y SQL Server de prueba. AT13 y AT27 también exigen cliente y cola SQLite integrados. Cada puesto cuenta los hechos pares y aplica la política configurada.

### TK02 — Interfaz: flujo autónomo y estados de operación

**Tipo:** frontend/interfaz · **Estado:** diseñado · **Épicas de producto:** HU01/HU03; estados de HU02 · **Unidades:** U03, U08 y U09 · **Ticket de tarea:** TK02.1.

**Objetivo:** construir la experiencia WPF/Blazor Hybrid sobre casos de uso locales, mostrando por separado identidad, selección, decisión, impresión y sincronización.

**Trabajo incluido:**

1. Modelar estados para impedir que una lectura nueva sustituya persona o intención durante confirmación (TK02.1).
2. Mostrar catálogo navegable, cargo y confirmación; cancelación/timeout anteriores al commit no producen consumo (TK02.1).
3. Incorporar recorridos 1:N y código previo + 1:1 mediante adaptadores de prueba rotulados.
4. Distinguir autorización local, rechazo local, error de almacenamiento, impresión fallida y sincronización pendiente.
5. Permitir diagnóstico y reimpresión local con permiso, motivo y cierre seguro de sesión; incorporar interfaz del Administrador del Sistema, configuración local, matriz de componentes, inventario de versiones y estados de las dos bitácoras (HU03-F–H).
6. Mantener atención monopuesto con red ausente; aplicar al modo multi puesto CONTINUE_LOCAL inicial o SUSPEND_NEW_CONSUMPTIONS configurado, sin alterar la autoridad local del commit.
7. Mostrar en supervisión avisos de exceso por carrera, origen de hechos y estado de difusión de pares; distinguirlos de rechazo local de una intención nueva.

**Dependencias:** casos de uso, fixtures, mensajes, timeout y permisos locales (RB26). La UI no modifica tablas ni decide cupos por su cuenta.

**Fuera de alcance:** administración empresarial completa, SDK dentro de componentes visuales, login obligatorio remoto y prueba de impresión física.

| Prueba prevista | Evidencia exigida |
| --- | --- |
| Arranque monopuesto/CONTINUE_LOCAL sin red y operación completa | Identidad/selección/consumo/comprobante accesibles |
| Impresión fallida y sincronización atrasada | Estados distintos; sólo las condiciones reales de la operación afectan continuidad |
| Escalas 100/125/150 % | Controles visibles, foco claro y UI sin bloqueos observables en equipo declarado |

**Terminado:** una persona puede identificarse, elegir, confirmar y retirar su comprobante sin red; el operador y el administrador ven estados separados y sólo actúan con su permiso; la demostración E2E conserva el consumo en SQLite. El simulador biométrico se informa como contrato, no como homologación.

### TK03 — Datos: base autónoma, provisión y recuperación

**Tipo:** datos/persistencia · **Estado:** diseñado · **Épicas de producto:** HU01/HU02/HU03 · **Unidades:** U02 y U06 · **Ticket de tarea:** TK03.1.

**Objetivo:** implementar SQLite como autoridad operativa local, conservar configuración y hechos, y permitir arranque/recuperación sin conectividad.

**Trabajo incluido:**

1. Crear esquema/migraciones de padrón, credenciales y permisos locales, catálogo/reglas versionadas, cupos, intención/decisión, consumo, ticket/trabajo, outbox, AuditRecord, AuditFileDelivery, LogDeliveryIncident y LocalSettingsRevision.
2. Incorporar paquete inicial validado y actualización de snapshots atómica; nunca activar datos parciales o inventar un padrón vacío como operativo.
3. Establecer unicidad de intención, consumo por intención, evento, ticket/copia y código de credencial por snapshot sin filtrar revocadas, con comprobación de titularidad histórica; claves externas y estados coherentes.
4. Guardar intención/decisión/consumo/ObservedConsumption propio/ObservedQuotaCharge/contador/ticket/PrintJob original/outbox/AuditRecord en una transacción única; recuperar el estado del trabajo existente después de una caída, sin crear otro original (TK03.1).
5. Conservar eventos hasta resolución durable; reintento/ACK perdido no permite borrarlos prematuramente.
6. Implementar backup consistente y restauración; proteger base y referencias biométricas conforme política explícita.
7. Incorporar ledger con origen/consumo, marca de deduplicación y cursor de recepción; aplicar cada hecho de par y su efecto de cupo en una transacción.
8. Implementar escritor de archivos general/interacciones UTF-8 por AAAAMM con flush, respaldo SQLite, detección de entregas inciertas, recuperación y retención sin purgar pendientes.
9. Persistir OperationMode, MultiKioskOfflinePolicy e incidentes/transiciones auditables; el reinicio conserva suspensión y referencias, sin volver al valor inicial.

**Dependencias:** [modelo de datos](modelo-datos.md), contratos de snapshot/lote, retención y protección de datos. El contenido de templates requiere almacenamiento/protección específicos; las muestras del MVP son sintéticas y no contienen biometría real.

**Fuera de alcance:** archivo SQLite compartido entre equipos, sincronización por carpeta y garantía global entre bases aisladas.

| Prueba prevista | Evidencia exigida |
| --- | --- |
| Provisión monopuesto/CONTINUE_LOCAL mediante paquete local y arranque sin red | Catálogo/padrón/reglas disponibles y operación completa |
| Rechazo o fallo de persistencia antes de confirmar | Decisión, outbox y AuditRecord quedan juntos o no queda ninguno; sin consumo, cargas ni ticket. Tras commit se recuperan los mismos IDs y la auditoría pendiente, sin nueva autorización |
| Caída después del commit y antes de iniciar el envío al spooler | Se retoma el trabajo original sin envío iniciado; si el envío pudo comenzar, se conserva incertidumbre y se aplica HU03-B |
| Snapshot incompleto/incompatible o con credenciales/permisos inválidos | Última versión válida intacta y atención según sus vigencias |
| Disco lleno | Error visible y hechos conservados; no informar éxito sin commit ni borrar pendientes |
| Backup/restauración con eventos ya enviados | Reenvío deduplicado y consumo/comprobante intactos |
| Regla sin vencimiento y prolongada desconexión | No caduca por antigüedad arbitraria de caché |
| Eco propio y mismo consumo en otro lote/feed | Un solo efecto de cupo; no duplicación por cambiar el sobre de transporte |
| Caída durante incorporación de un par | Ledger/contador/marca consistentes; cursor recuperable sin hechos omitidos |

**Terminado:** la base se crea por migraciones, una confirmación queda completa o ausente, la configuración sólo cambia al activar un snapshot íntegro, los hechos de pares se cuentan una vez y las dos bitácoras se reconstruyen tras un fallo del archivo. Los AT de los que TK03 o TK03.1 son responsables en la matriz se prueban con SQLite y archivos reales.

### Mantenimiento dentro de las unidades existentes

HU03.3 incorpora AT56: TK02 es principal de acceso y sesión, TK01 de autorización/auditoría y TK03 de persistencia protegida. U01 especifica el contrato y fixture sintético en M0; U08 lo implementa antes de la final. Los detalles abiertos se enumeran en [administración](administracion-y-bitacoras.md#mantenimiento-alternativo-y-pendientes-de-m0). No se agrega una funcionalidad independiente ni emisión para terceros.

## Cobertura y secuencia propuesta

| Función | Historias | Tickets de tarea y épicas técnicas | Aceptación crítica |
| --- | --- | --- | --- |
| F01 Identificación/selección | HU01.1, HU01.2 | TK02.1; TK02/TK01; adaptadores requieren tareas propias | AT01/07/16/19–AT24/26/52 |
| F02 Registro local | HU01.1, HU01.3, HU02.1 | TK03.1; TK01/TK03 | AT02–AT05/10/25/28/29/35/53 |
| F03 Comprobante | HU03.1 | TK01/TK02/TK03; adaptador requiere tarea propia | AT06/24/54 |
| F04 Supervisión/sincronización | HU02.2–HU02.6, HU03.2–HU03.5 | TK01.1; TK01/TK02/TK03 | AT09/10/12/13/27/30–AT34/55/56 |

Primero se cierran en M0 las reglas sintéticas y contratos; luego se construye la vertical autónoma con SQLite y adaptadores de prueba. La sincronización se incorpora conservando los mismos IDs. Los ensayos físicos de BioMini, serie e impresora quedan separados del MVP y requieren tareas y homologación específicas.

La conducta ante desconexión está definida por D-A02 y la del reloj por D-C17: continuar con hora de Windows y auditar saltos. Los parámetros técnicos de detección/recuperación se fijan en la preparación de cada componente. Las tallas, la fecha de M0 y la regla de recorte están en el [plan académico](plan-academico.md#prioridad-dentro-de-las-funcionalidades); son estimaciones de planificación, no compromisos ya medidos.

## Responsabilidad de la aceptación

Cada AT tiene un único responsable principal en la [matriz completa](plan-academico.md#matriz-de-aceptación): el ticket de tarea cuando existe, o la épica técnica. Las definiciones de terminado citan sólo los AT propios; la colaboración de otras capas figura como apoyo en la matriz y explicita las dependencias que condicionan el cierre integral.

AT46 se ejecuta con adaptadores sintéticos intercambiables y guiones controlados, en bases de prueba independientes. Los ensayos no comparan UUID aleatorios entre corridas como prueba de equivalencia: comparan el resultado y la estabilidad de los IDs dentro de cada corrida. AT38 usa la matriz completa del [contrato](administracion-y-bitacoras.md); las pruebas físicas siguen fuera de la evidencia del simulador. La homologación de comandos de impresora es trabajo adicional, no una promesa de compatibilidad universal. El conjunto incluye casos del MVP y casos de producto futuro claramente separados.

Backlog futuro RF42, fuera de las épicas del MVP: emisión por operador privilegiado para beneficiario interno/externo. Antes de construir se especificará la historia con D-C12/15, AT15/40, permisos locales, identidad de externos y versión del evento; siempre reglas del beneficiario, sin excepción de cupo. Su aceptación corresponde a una ampliación posterior del producto.

## Coherencia del ecosistema: trabajo y aceptación

RNF15 y AT51 incorporan el [perfil de nomenclatura](nomenclatura-integracion.md) al alcance de los tickets existentes, sin agregar historias ni funcionalidades.

- **TK03, responsable principal:** completar en M0 el diccionario físico y mappings de los 44 tipos de entidad, incluidos los campos que hoy sólo figuran en el diccionario; conservar tipos lógicos, PK/FK compuestas, unicidades y UUID. Diseñar y luego ejecutar migraciones y pruebas SQLite conforme al mapa.
- **TK01, apoyo y responsable del adaptador:** definir ámbitos de claves nativas y resolución estable de personas; mantener distintos `perCodigo`, `empCodigo` y UUID del módulo. Implementar DTO con serialización explícita y probar ida/vuelta contra los ejemplos versionados.
- **Evidencia de terminado:** AT51 ejecutado con el mismo código nativo en dos instancias y tipos distintos, identidad estable entre snapshots, persistencia con claves compuestas y JSON compatible. La primera entrega incluye el diseño y los mapas; no acredita estas pruebas de implementación.
