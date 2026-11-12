# Plan de pruebas y aceptación

Plan documental: ninguna prueba del producto se informa ejecutada. Las etapas
posteriores registrarán comando, versiones, ambiente, datos y resultado.

## Escenario sintético del MVP

Un puesto para autonomía y dos para multipuesto; zona America/Argentina/Buenos_Aires; corte diario 00:00; tres personas
ficticias (habilitada, inactiva, sin derecho). Un cupo ALMUERZO por día compartido
por variantes común/opcional y un cupo REFRIGERIO independiente. Catálogo de quince
opciones. Valores monetarios ficticios, sin copiar datos ni huellas empresariales.
La persona habilitada tiene un código vigente, uno revocado y uno vencido; la persona
sin derecho a comedor es operadora y tiene revocado su permiso de administración.
Para AT30 se agregan dos puestos sintéticos del mismo SiteId y ensayos separados
con CONTINUE_LOCAL y SUSPEND_NEW_CONSUMPTIONS; se corta cada dirección del
intercambio de forma independiente. Estas reglas son fixtures académicos, no aprobación comercial ni límites fijos del producto.

## Pruebas previstas

| Nivel | Caso | Resultado esperado |
|---|---|---|
| Unitarias | Persona/derecho/servicio/categoría, día y dinero | Reglas deterministas con reloj inyectable. |
| Unitarias | Adaptador 1:N y código+1:1 | Identidad estable, rechazo y autorización separados; no mide biometría física. |
| Unitarias | UI, lectura, cancelación | Ninguna sesión hereda identidad o selección anterior. |
| SQLite real | Dos intenciones por último cupo del mismo puesto | Sólo una autorización. |
| SQLite real | Doble toque/retry idéntico | Mismo consumo y ticket; contador sin incremento adicional. |
| SQLite real | Replay tras vencer sesión, baja o cambio de configuración | Devuelve decisión original sin reautorizar ni crear otro consumo. |
| SQLite real | Caídas alrededor de commit e impresión | Recuperación durable sin pérdida ni doble cargo. |
| Integración | Arranque monopuesto o CONTINUE_LOCAL con INWeb apagado | Identifica, autoriza e imprime con base provisionada. |
| Integración | Snapshot parcial o inconsistente | Conserva versión anterior íntegra. |
| Integración | Baja conocida/no recibida | Aplica la conocida; explicita el límite temporal de la no recibida. |
| Contrato | Lote, respuesta perdida y consulta/reenvío | Un hecho central por EventId; resultado estable. |
| Contrato | Mismo EventId, contenido distinto | Conflicto visible; original conservado. |
| Contrato | Equipo envía hechos de otro | Rechazo por ámbito; sin falsificar origen. |
| Integración | Consumo recibido de otro puesto antes de nueva elección | Ledger/cupo actualizado; impide segundo consumo según regla. |
| Integración | Carrera entre dos puestos conectados | Hechos conservados y aviso de exceso para auditoría. |
| Integración | Eco propio, repetición y reinicio durante recepción de pares | Sin dobleconteo, reimpresión ni nueva publicación del mismo hecho. |
| Contrato | Configuración inicial y cambio de política | Publica OperationMode y MultiKioskOfflinePolicy explícitos; inicial CONTINUE_LOCAL. Rechaza política ausente/desconocida y conserva snapshot previo. |
| Integración | Multipuesto CONTINUE_LOCAL: corte de salida, feed o ambos | Sigue atendiendo con cupos conocidos; incidencia durable, consumo vinculado y outbox; excesos detectados al conciliar generan aviso. |
| Integración | Multipuesto SUSPEND_NEW_CONSUMPTIONS: corte de salida, feed o ambos | Intención nueva recibe MULTI_KIOSK_COMMUNICATION_REQUIRED; sin consumo, cupo, ticket ni PrintJob. |
| Integración | Reinicio sin red con suspensión previamente configurada | Conserva política, incidencia e IDs; nuevas confirmaciones suspendidas, diagnóstico y comprobantes existentes disponibles. |
| Integración | Replay de decisión anterior mientras hay suspensión | Devuelve decisión original sin otro consumo ni reevaluación; reintentar una denegación durable no la autoriza. |
| Integración | Red restaurada con feed incompleto o pendientes sin recepción | Suspensión continúa hasta cortes de recepción/feed recuperados; no reanuda por un ping. CONTINUE_LOCAL conserva atención. |
| Integración | Cambio de política mediante snapshot completo y caída durante activación | Cambio atómico auditable para futuras confirmaciones; reinicio o snapshot incompleto no restablecen el valor inicial. |
| Contrato | Apertura/cierre de incidencia repetidos o fuera de orden | Un incidente central, sin cupo ni impresión; cierre antes de apertura queda pendiente de referencia, luego se proyecta sin duplicar. |
| E2E | Monopuesto/CONTINUE_LOCAL: identificación, selección, commit local, ticket y sincronización | Circuito completo con servidor apagado y luego recuperado. Ejecutado desde el 30/10 en [`FlujoPrincipalTests`](../tests/Intelektron.INComedor.E2E.Tests/FlujoPrincipalTests.cs), sobre los EXE del puesto y del receptor de demostración, por CDP: consumo y comprobante sin red, cupo, pendientes y el consumo consolidado una sola vez al volver el receptor (AT01, AT02 y AT27). |
| Hardware posterior | Serie, BioMini 1:N/1:1 e impresora reales | Modelos/SDK/templates, desconexión y recuperación homologados. |
| Recuperación | Restaurar base con lote ya recibido | IDs conservados; reenvío sin duplicación central. |
| UX | Quince opciones, teclado y escalado | Recorrido usable y sin mezcla de personas. |

Un simulador no demuestra calidad biométrica, liveness ni impresión física.
Un proveedor en memoria no reemplaza SQLite/SQL Server reales para concurrencia
y durabilidad. Validar OpenAPI no demuestra que exista un servidor implementado.

Para la entrega final se prevén unitarias, integración y al menos un E2E con
evidencia reproducible. Latencia y recuperación se medirán antes de declarar
objetivos cumplidos; no se presentan como SLA.

## Administración, diagnóstico y archivos externos — ampliación

| Aceptación | Ensayo previsto | Evidencia exigida |
|---|---|---|
| AT37 | Roles, configuración offline, revisión inválida y reinicio | Revisión activa conservada, cambio auditado y acceso denegado fuera de permiso; no modifica reglas maestras |
| AT38/39 | Cada fila de la matriz de diagnóstico; sesión ocupada, timeout/cancelación y error | DiagnosticRunId, comando completo de prueba de impresora, resultado técnico, adaptador/versión; sin consumos ni mezcla de sesiones; permisos y liberación |
| AT41 | Identificación 1:N y 1:1, rechazo, selección, cancelación, commit, retry, reimpresión y mensajes | Dos archivos reales correlacionados por AuditId/InteractionId y referencias del hecho; reconstrucción independiente de consultas DB; sin secretos/biometría |
| AT42 | Mes 09→10, exportación retrasada, cambio de zona/reloj y reinicio | Archivos AAAAMM independientes, mes original inmutable y secuencias/timestamps; generación nueva sólo al restaurar, no al reiniciar |
| AT43 | Permiso denegado/bloqueo/cuota de filesystem de logs, uno y ambos destinos; SQLite en soporte operativo | Continúa operación, cola durable crece controladamente e incidencia visible; recuperación hasta corte con mismos IDs y cero consumos extra |
| AT44 | Matar proceso alrededor de append/flush/marca, línea truncada y conflicto de AuditId | Reparación conserva evidencia; deduplicación lógica, contenido conflictivo visible, sin huecos silenciosos ni éxito falso |
| AT44 | Agotar también almacenamiento SQLite o dañarlo | Ningún nuevo consumo reportado como confirmado; previos conservados cuando el soporte lo permita; diagnóstico de emergencia y límite de evidencia declarado |
| AT15/40 futuro | Beneficiario interno/externo válido, sin derecho, cupo agotado y cambio de beneficiario en retry | Actor separado, observación, cargo correcto y rechazo sin bypass; contrato versionado antes de desarrollar |

RF43–46 se aceptan abriendo ambos archivos de texto independientes y reconstruyendo una secuencia completa, además de comprobar su respaldo SQLite. Verificar append tardío al mes previo, flags/fechas de recuperación y
exportación, incidentes cerrados tras corte y ausencia de purga de pendientes.
Un salto de reloj no se corrige borrando historia. Comparar estado económico
antes/después del diagnóstico, exportación y recuperación: esos procesos no consumen.
Estas pruebas siguen diseñadas; la validación documental no acredita ejecución.

## Coherencia, reloj y condiciones de cierre

| Aceptación | Ensayo previsto | Evidencia exigida |
|---|---|---|
| AT45 | Consultar estados de consumo, rechazo, impresión y sincronización | Estados y correlación independientes en la supervisión |
| AT46 | Cambiar adaptadores sintéticos usando guion equivalente y bases independientes | Resultado semántico y estabilidad de IDs por ejecución; dominio sin cambios |
| AT47 | Publicar otra versión de menú/precio mientras hay selección abierta | Reconfirmación explícita; cero consumo previo a ella; snapshot histórico preservado |
| AT48 | Atrasar reloj una hora y un día con TimeProvider inyectable | Continúa usando hora Windows simulada; audita salto y hechos, sin piso temporal; cupo de la fecha calculada |
| AT49 | Adelantar un día, corregir después y reiniciar | Fechas previas inmutables, secuencia/BootId, aviso e incertidumbre; ninguna garantía de hora real offline |
| AT50 | Variar sólo la fecha de uno de los cargos de un evento | Rechazo por regla de aplicación INVALID_EVENT; positivo con todas las fechas iguales; no confundir validación de esquema y negocio |
| AT51 | Coherencia lógico/físico/C# e identidad | Revisar mapa y diccionario; probar persistencia de PK/FK compuestas; resolver igual código nativo en distintas instancias y tipos sin colisiones; conservar UUID entre snapshots y reintentos; roundtrip DTO con nombres/tipos/enums JSON y hashes de ejemplos vigentes. Diseñar en M0 y ejecutar al implementar; la comprobación estática del mapa no sustituye estas pruebas. |
| AT52 | Código vigente solo y con huella correcta/incorrecta; vencido, revocado, futuro y desconocido; límites exactos de vigencia | Sólo vigente + huella correcta abre selección. Futuro/desconocido comparten motivo externo con causas internas distintas; inválidos sin selección/consumo y sin código en logs |
| AT53 | Activar un snapshot que revoca la credencial de una sesión abierta y repetir una intención ya decidida | Intención nueva denegada con CREDENTIAL_REVOKED; replay con el resultado original |
| AT54 | Operador vigente, vencido, revocado, administrador sin OPERATOR, autenticación fallida y revocación durante sesión | Sólo operador vigente reimprime; RequestedRole siempre, actor/grant sólo cuando existen; ninguna referencia ficticia ni trabajo en denegaciones |
| AT55 | Duplicados incluso revocados, reasignación tras retirar fila, cambio de titular de CredentialId, revocación futura, referencias o rol inválidos; permutar filas | Rechazo en staging; snapshot activo intacto. Historial de titularidad conservado y provisión histórica de equipo nuevo definida en M0 |

AT48/49 se prueban con base durable y en monopuesto o CONTINUE_LOCAL; luego se combinan con SUSPEND_NEW_CONSUMPTIONS para demostrar que D-C17 no elude la suspensión de D-A02. Replay conserva la decisión original en todos los casos.

Los ejemplos de configuración y lotes de OpenAPI representan el mismo fixture; sus referencias deben existir en la versión aplicable y sus hashes se comprueban sobre el contenido completo. La matriz de responsabilidad y el criterio de salida por hito están en [plan académico](plan-academico.md). Los resultados de producto se registrarán al ejecutar los ensayos durante la implementación.

## Casos complementarios de la corrección documental

| AT | Caso diseñado | Evidencia requerida al implementar |
|---|---|---|
| AT03 | Dos conexiones por último cupo y mismo IntentId concurrente, con lectura orientativa previa | Una decisión por intención; lectura, dominio y escritura protegidos por la misma transacción SQLite real |
| AT56 | Mantenimiento con lector inutilizable y sin red; cuenta inválida/bloqueada, sesión expirada, permiso ausente y restauración | Acceso individual autorizado y auditado; sin consumo ni reimpresión sin OPERATOR. Fixture y parámetros se cierran en M0 |

Las simulaciones de reglas sobre JSON sólo verifican la especificación y sus ejemplos. No son ejecución de INComedor ni prueba de concurrencia, hashing de contraseñas, bloqueo persistente o recuperación. Ambos modos y el cambio de política de AT30 son must; AT38/39 son must para SQLite/API/impresora, con diagnóstico restante y escalas 125/150 % diferibles según el plan.
