# Administración, diagnóstico y bitácoras

Diseño técnico del MVP, 29/09/2026. Requisitos funcionales y decisiones de arquitectura. [SDD](especificacion-sdd.md) y
[decisiones](decisiones.md) conservan los identificadores de trazabilidad.

## Clasificación y alcance

| Tema | Tipo | Alcance | Trazabilidad |
|---|---|---|---|
| Interfaz del Administrador del Sistema | Funcional; autorización y configuración | F04 del MVP: interfaz y configuración local; servicios simulados declarados | RF40; RB21; AT37; D-C11 |
| Diagnóstico de componentes | Funcional de soporte; aislamiento y seguridad | F04: pruebas con adaptadores; homologación física posterior | RF41; RNF14; AT38/39 |
| Tickets para terceros internos y externos | Funcional de operación privilegiada y regla de negocio | Producto futuro, fuera del MVP | RF14/42; RB22; AT15/40; D-C12/15 |
| Bitácora general y de interacciones | Funcional de auditoría; persistencia, reconstrucción y protección | Transversal a F01–F04 del MVP; archivos reales cuando se implemente | RF43–46; RB23/24; RNF13; AT41–44; D-C13/14 |

## Interfaz y permisos de administración

El Administrador del Sistema configura el puesto mediante una interfaz local
autenticada, disponible sin INWeb en línea. Es un rol de la aplicación; no implica
ejecutarla con privilegios de administrador de Windows. Los permisos llegan en el
snapshot como roles `OPERATOR` y `SYSTEM_ADMINISTRATOR`, con vigencia y revocación;
el actor se autentica sin red con su código y verificación 1:1 o con usuario y contraseña individual local de mantenimiento, sin login remoto
([SDD §7.4](especificacion-sdd.md#74-credenciales-y-control-de-acceso-local), RB26).
Comensal, operador de recuperación, administrador y futuro emisor para terceros son
capacidades distintas; el caso de uso local comprueba el permiso en cada acción.

Configuración del equipo: selección del adaptador real o simulado, parámetros de
serie, impresora y SDK compatibles, destino de API, tiempos técnicos admitidos y
directorio de bitácoras. Mostrar valores efectivos, versión y procedencia; validar,
probar sin activar y aplicar la configuración local como una revisión íntegra.
Los secretos se administran por referencias protegidas y no se muestran en claro.
Toda modificación registra actor, revisión anterior/nueva, campos cambiados sin
secretos, hora y resultado. Una revisión inválida conserva la configuración anterior.

INWeb sigue siendo maestro de padrón, catálogo, precios, habilitaciones, cupos y
política multipuesto de la instalación. La pantalla permite inspeccionar la versión
efectiva e importar un paquete autorizado; no introduce una edición local paralela
de derechos ni de D-A02. Cambiar un endpoint tampoco cambia SiteId o KioskId sin un
procedimiento de provisión autorizado. No se permite SQL libre sobre la base operativa.

## Mantenimiento alternativo y pendientes de M0

D-C22 confirma una cuenta individual local para administrar cuando el lector no funciona o no está disponible, también sin INWeb. Usa la misma identidad de actor y los grants del snapshot; autenticar no concede permisos. OPERATOR y SYSTEM_ADMINISTRATOR no se heredan. La sesión privilegiada no puede convertirse en sesión de consumo, y administrador sin OPERATOR no reimprime. Acceder al mantenimiento no requiere diagnosticar previamente el lector ni tener una sesión biométrica abierta.

| Pendiente M0 | Responsable | Criterio de cierre antes de implementar |
|---|---|---|
| Identidad y provisión inicial | TK01/TK03; David designa custodio autorizado | Usuario individual vinculado a PersonId estable, cuenta sintética y procedimiento offline de alta; no cuenta compartida, contraseña universal ni edición local de roles maestros |
| Almacenamiento protegido | TK03 | Verificador con sal y derivación de contraseña; algoritmo, parámetros, permisos de almacenamiento y protección definidos; ningún secreto en snapshot, logs o exportaciones |
| Intentos y recuperación de bloqueo | TK01/TK03 | Umbral, espera/bloqueo, estado tras reinicio y recuperación autorizada especificados; no se eluden reiniciando el proceso |
| Sesión y cierre | TK02/TK01 | Expiración, cierre manual, aislamiento de atención y comprobación del rol por acción; revocar en snapshot impide la siguiente acción |
| Baja, restablecimiento y restauración | TK03/TK01; custodio por designar | Procedimiento auditable sin depender del lector averiado; restaurar backup no reactiva silenciosamente cuentas/grants deshabilitados; resolver historial antes de habilitar acceso |
| Auditoría y fixture | TK01/TK02 | Acción, resultado, causa y RequestedRole; PersonId y grant sólo cuando existen. AT56 con cuenta sintética, lector caído, red ausente y errores controlados |

Los valores de intentos y tiempos no están fijados todavía. La elección del acceso alternativo está confirmada; estos parámetros y procedimientos siguen diseñados pendientes de cierre en M0. El mantenimiento no habilita diagnósticos ampliados como must: permite acceder a administración y a SQLite/API/impresora aun sin lector. No acredita recuperación productiva ni protección ya implementada.

## Matriz de diagnóstico

Cada ejecución tiene DiagnosticRunId, actor, componente/adaptador, versión, modo
real/simulado, parámetros no sensibles, inicio/fin, duración, estado, resultado y
error. Las acciones con efectos muestran antes su destino y efecto; requieren
permiso y confirmación en la interfaz. No generan consumos ni cargos.

| Componente | Configuración y pruebas previstas | Evidencia y límite |
|---|---|---|
| INWeb por API | Endpoint, confianza/certificado por referencia; DNS, transporte, TLS, autenticación y operación de prueba en ámbito sintético | Estado y latencia por etapa, código HTTP y correlación; no registrar tokens/cuerpos personales. Probar conexión no activa snapshots ni consume pendientes |
| Pantalla táctil | Área de prueba de toque, coordenadas, focos, escalado y gestos soportados | Secuencia sintética visible, latencia y zonas sin respuesta; no alimentar selección de comida |
| BioMini por SDK | Enumeración, modelo, arquitectura, versión/licencia, apertura/cierre, captura y comparación 1:N/1:1 en contexto de prueba autorizado | Retorno del SDK, timeout y recuperación; no volcar huellas/templates ni enrolar silenciosamente; simulación no homologa hardware |
| Puerto serie | Puerto, velocidad, paridad, bits y timeouts; apertura/cierre y visor de eventos/tramas parciales, ruido y parser | Recepción y reconstrucción con tiempos, longitudes y campos técnicos; identificadores personales enmascarados; fixtures completos sintéticos disponibles |
| Impresora | Selección de dispositivo/controlador; consulta de estado, página sintética y consola de comandos completos del protocolo soportado | Comando completo de prueba, parámetros, bytes/escape según driver, envío, respuesta y error. Comandos concretos/modelo pendientes de homologación; no asumir ESC/POS universal ni éxito físico por spooler |
| SQLite local | Ruta efectiva, versión real de motor/proveedor, apertura, integridad, espacio y transacción de prueba aislada con rollback | Informe de salud sin editar consumos/ledger; pruebas invasivas en mantenimiento, fuera de una confirmación activa |
| Red | Interfaces, dirección/ruta, DNS y conectividad al destino configurado, separadas de autenticación API | Error y duración por capa; ping aislado no acredita API ni feed recuperados |
| Versiones | Aplicación/build, SO, runtime, WebView2, esquema DB, SQLite efectivo, adaptadores, DLL/SDK, driver y firmware si consultable, configuración activa | Inventario exportable; valor desconocido explícito, nunca versión inventada |
| Bitácoras | Rutas, mes, capacidad, permisos, última escritura, pendientes y recuperación de cada archivo | Prueba rotulada y trazada; no borrar archivos ni pendientes para que la prueba pase |

Una prueba que toma BioMini, serie o impresora reserva el dispositivo; si interfiere
con la atención, el puesto entra en mantenimiento explícito al quedar libre la sesión.
Cancelar o expirar libera recursos y permisos. La consola de impresora admite los
comandos completos del adaptador homologado, no una consola arbitraria del sistema.
Las pruebas nunca crean tickets que aparenten un consumo: el papel dice PRUEBA.
No hay exclusión global del servicio por ejecutar una consulta técnica inocua.

## Emisión futura para terceros

El autor confirmó que un operador con permiso específico podrá emitir para un
beneficiario interno o externo. Deben quedar separados ActorId (quien opera) y
BeneficiaryId/BeneficiaryType (para quién); no se presta la credencial del operador.
Registrar servicio/variante y cantidad (qué), beneficiario, operador, fecha/hora,
observaciones, reglas/configuración aplicadas, decisión, consumo y ticket/copia.
El formulario exige una observación, aunque sea «Sin observaciones adicionales».

**Decisión confirmada: se aplican las reglas y cupos del beneficiario, sin
excepciones de cupo.** El permiso de emitir para terceros no concede un servicio,
no cambia precio ni amplía cupo. Persona inactiva, sin derecho o con cupo agotado
recibe rechazo auditable. Los externos requieren una identidad estable y derechos
previamente provisionados; escribir un nombre libre no fabrica una habilitación.

La futura intención debe ligar actor, beneficiario, modalidad y observaciones a su
contenido idempotente. El replay conserva la decisión original; otra persona en el
mismo IntentId genera conflicto. El cargo corresponde al beneficiario y comparte
su contador habitual, también si luego usa autoservicio. Correcciones de hechos
siguen el circuito separado de compensaciones, nunca un bypass del emisor.

La versión API actual describe autoservicio y no transporta estos campos. Antes de
implementar esta ampliación se versionarán intención, consumo y evento sincronizado,
incluyendo actor/beneficiario y permisos. No se enviarán campos desconocidos al
contrato vigente. El alta y acreditación de externos quedan para esa etapa.

## Dos bitácoras externas mensuales

**Requisito confirmado:** archivos independientes de texto plano fuera de SQLite,
uno general y otro de interacciones, generados mes a mes con AAAAMM en el nombre.
Se propone UTF-8 con una estructura JSON por línea (JSON Lines), legible como texto,
en disco local por KioskId. No son un export manual ni una consulta de tablas.

Nombres propuestos: `INComedor-general-AAAAMM.log` e
`INComedor-interacciones-AAAAMM.log`. Por ejemplo, septiembre de 2026 usa
`INComedor-general-202609.log` e `INComedor-interacciones-202609.log`.
Directorio raíz configurable, subdirectorio por KioskId; al restaurar una base se
abre una nueva GenerationId para no colisionar con secuencias de otra generación.

- General: arranque/cierre, versiones, salud, cambios de configuración, acciones
  administrativas/diagnóstico, fallas, impresión técnica, DB/red/API/feed,
  sincronización, incidencias, recuperación y estado de las propias bitácoras.
- Interacciones: inicio/cierre de sesión, método y resultado de identificación,
  selección/cancelación/timeout, confirmación solicitada, decisión y motivos,
  consumo y cargos, solicitud/resultado de impresión, copia/reimpresión,
  mensajes de resultado y acciones privilegiadas sobre comprobantes; en el futuro,
  emisión para terceros con operador, beneficiario y observaciones.
- Un evento relevante para ambas aparece en las dos con el mismo AuditId.
  No se registran pulsaciones indiscriminadas, claves, códigos completos innecesarios,
  biometría ni cuerpos API sensibles. Una identidad aún no resuelta permanece nula,
  con motivo; no se inventa una persona para completar la traza.

Contrato de registro propuesto: SchemaVersion, AuditId UUID, KioskId, GenerationId,
Sequence durable, BootId, OccurredAtUtc, RecordedAtUtc, zona/offset y LogMonth
inmutables, duración monotónica cuando aplica, Stream, EventType, Severity,
Component/ComponentVersion, ConfigurationVersion, LocalSettingsRevision,
SessionId/InteractionId y referencias IntentId, DecisionId, ConsumptionId, TicketId,
PrintJobId, SyncEventId/BatchId, DiagnosticRunId e IncidentId cuando correspondan;
actor/sujeto, acción, resultado, motivo y datos mínimos de contexto. Los campos sin
relación con ese evento son nulos, no identidades ficticias. Sequence ordena el
registro por puesto/generación; no promete orden total entre equipos ni reloj exacto.

LogMonth se fija al capturar el evento según su hora y zona efectiva, no por la
fecha de reconstrucción. Recuperar en octubre un hecho de septiembre agrega al
archivo de septiembre conservando OccurredAtUtc y marcando Recovered=true y
ExportedAtUtc real. Nunca altera una línea histórica para corregirla; agrega un
evento de corrección/reconciliación. Un salto de reloj queda explícito; analizar
Sequence y tiempos originales evita ordenar sólo por el reloj o por la línea.

### Contexto mínimo para reconstruir sin consultar la base

Los IDs permiten correlacionar, pero por sí solos no describen una interacción.
Cada archivo debe conservar el contexto observado necesario, además de las referencias:

| Tipo de evento | Datos mínimos adicionales, cuando correspondan |
|---|---|
| Identificación/sesión | Método serie/1:N/1:1, identidad estable resuelta o nula, resultado y motivo, inicio/fin/timeout; nunca material biométrico ni credencial en claro |
| Selección/confirmación | Servicio y variante con etiqueta histórica, cantidad, importe exacto/moneda/escala mostrados, versión del catálogo y acción del usuario |
| Decisión/consumo | Resultado, código de motivo, reglas/versiones efectivas, BusinessDate, cargos por ámbito/categoría/unidades y límites aplicados; consumo y ticket vinculados |
| Impresión/reimpresión | Copia, actor/motivo, solicitud, estado de trabajo y respuesta observada; distinguir enviado, fallido e incierto de papel entregado |
| Mensaje/interacción UI | Código y versión del mensaje mostrado, parámetros no sensibles, estado origen/destino y acción/cancelación; no grabación indiscriminada de pantalla |
| Administración/diagnóstico | Actor, permiso ejercido, cambio de revisión, valores técnicos antes/después sin secretos, comando de prueba, destino, resultado/error y duración |
| Sincronización/incidencia | IDs de eventos/lote/feed, resultado de recepción, cursor, dirección fallida y apertura/recuperación; sin volcar cuerpos personales completos |

Una decisión denegada no incluye un consumo ficticio. Reintentar una operación
genera una traza del intento con nuevo AuditId y referencia a la decisión recuperada;
no vuelve a emitir un evento CONSUMPTION_RECORDED ni un registro de consumo nuevo. Los eventos distinguen REQUESTED,
COMPLETED, REJECTED, CANCELLED y UNKNOWN según la evidencia disponible.
Cambiar nombres o reglas del catálogo no modifica las etiquetas y valores históricos.
Estos campos se especificarán por tipo al implementar y se comprobarán con AT41.

## Continuidad con respaldo durable y reconstrucción

**D-C14, decisión del autor:** si falla el archivo por espacio, permisos o bloqueo,
continuar usando SQLite como respaldo durable y reconstruir luego, señalando el
intervalo degradado. Es una cola auxiliar; los dos archivos externos siguen siendo
entregables permanentes de la operación normal. No se sustituye el requisito por
una tabla ni por logs efímeros de consola.

Propuesta técnica: AuditRecord inmutable y AuditFileDelivery por destino. Las
transiciones de negocio guardan su AuditRecord en la misma transacción SQLite que
el hecho. Interacciones sin consumo y diagnóstico guardan su registro antes de
dar por finalizada la acción. Cada archivo tiene un escritor serializado que toma
pendientes, agrega línea completa, solicita flush durable y recién después marca
la entrega. Registrar antes y después una operación lenta distingue solicitud,
resultado e incertidumbre. Un evento ya persistido no vuelve a consumir cupo.

SQLite y el archivo no comparten transacción atómica. Una caída tras escribir y
antes de marcar puede dejar entrega incierta: al recuperar, validar el sufijo y
buscar AuditId en el destino antes de reenviar. Misma identidad/contenido no se
repite lógicamente; contenido distinto se conserva como conflicto. Una línea
truncada se conserva en evidencia de reparación y se restituye desde la cola,
sin ignorarla ni perder líneas válidas. El procedimiento debe probarse con archivos
reales; no se promete exactamente una escritura física bajo cualquier falla.

Detectar un fallo abre LogDeliveryIncident por archivo con hora, causa, primer
AuditId pendiente y estado; el otro archivo puede seguir. Recuperar cierra sólo
tras verificar entregas hasta un punto de corte capturado, conserva el intervalo
y agrega resumen recuperado a ambos archivos. La interfaz muestra degradación,
cantidad/antigüedad pendiente y espacio disponible. La falla del logger no produce
recursión ilimitada de nuevos errores del logger; se agrupa por incidencia y cambio
de estado. No borra pendientes ni cambia una política multipuesto para continuar.

Si SQLite tampoco puede persistir, **no se confirma un consumo nuevo**: falla la
precondición de durabilidad RB06. Diagnóstico informa la falla y, si el archivo
general sigue disponible, registra un evento de emergencia con UUID/BootId propio
para conciliación posterior. Si ambos soportes fallan no se promete reconstrucción
de eventos que nunca pudieron guardarse. Disco físico perdido exige backup; ningún
log local protege por sí solo contra ese desastre. Replay recupera el hecho previo
sin cambiar su decisión y marca cualquier imposibilidad de registrar el nuevo intento.

La reconstrucción precisa alcanza las acciones y estados observados por el sistema.
Ni la aceptación del spooler ni un ticket confirman entrega física de papel o comida.
Los estados desconocidos se conservan como tales; no se rellenan por inferencia.

## Conservación y acceso

Permisos de lectura/exportación separados de administración y emisión. Los archivos
son texto plano con ACL; eso no los hace inmutables ni prueba resistencia a manipulación.
Integridad verificable, copias, plazo de conservación, límites de espacio y protección
del disco se diseñarán antes del piloto. No purgar registros no exportados ni cerrar
un mes contra reconstrucciones pendientes. Una política de retención nunca borra
silenciosamente evidencia necesaria para recuperación. Observaciones no deben contener
secretos o datos personales ajenos al propósito de la operación.

## Anomalías del reloj

D-C17 mantiene la hora de Windows como referencia incluso ante un salto detectado. La supervisión muestra el aviso y permite consultar el intervalo y los consumos correlacionados; no crea una suspensión por reloj ni un piso temporal. Conserva ClockIncidentId en Context, observaciones antes/después, BootId, secuencia, zona/corte, actor de una corrección conocida y referencias a AuditId/IntentId/ConsumptionId. Si se desconoce quién o qué cambió el reloj, el origen se registra como desconocido.

La secuencia durable ordena registros del puesto sin depender de que los timestamps crezcan. LogMonth se fija al capturar y conserva su mes aun si el reloj se corrige después; una escritura tardía al archivo correspondiente mantiene marcas de recuperación. El cierre del aviso documenta la revisión y no recalcula automáticamente fechas, cupos o cargos ya confirmados. El detector, pruebas de saltos y límites tras reinicio están en [SDD §7.3](especificacion-sdd.md#73-política-del-reloj-y-fecha-de-negocio).
