# INComedor — Especificación del MVP y base SDD

Autor: David Walfisch · Iniciales: DW · Versión documental: 1.1 · Fecha: 29/09/2026 · Estado: **diseño técnico — entrega 1**.

INComedor opera de manera autónoma sobre su propia base SQLite. Identifica, autoriza y registra consumos localmente. INWeb publica configuración y consolida hechos. La instalación puede elegir continuidad local o suspensión de nuevas confirmaciones durante una pérdida de intercambio en el perfil multipuesto auditado; la decisión de consumo permanece local.

Las cuatro funcionalidades del MVP son obligatorias (D-C16). El diseño utiliza adaptadores de prueba y datos sintéticos. La implementación, las pruebas operativas y la homologación física corresponden a las etapas posteriores. RF, RB, AT y RNF identifican requisitos, reglas, aceptación y condiciones de calidad de forma estable.

[Portada](../readme.md) · [Arquitectura](arquitectura.md) · [Datos](modelo-datos.md) · [Historias y tickets](historias-y-tickets.md) · [Selección OpenAPI](../api/openapi.yaml).

## 1. Problema, objetivo y personas usuarias

En un comedor organizacional, una persona necesita identificarse, conocer los servicios que tiene habilitados y registrar su elección. El puesto debe continuar funcionando aunque INWeb o la red no estén disponibles bajo el perfil habitual y la política inicial; el perfil multipuesto auditado puede configurarse para suspender nuevas confirmaciones. Debe evitar duplicaciones accidentales, conservar consumos y comprobantes, y sincronizar después los hechos para administración y conciliación.

INComedor será un puesto de autoservicio sobre una All in One táctil Windows 11, con identidad por código y lector BioMini Suprema. Mantendrá su propia base SQLite con el último padrón, templates y reglas sincronizados. La autorización y el consumo son operaciones locales. INWeb actúa como fuente administrativa y destino de sincronización; su disponibilidad no es requisito para atender salvo la suspensión multipuesto expresamente configurada.

| Persona usuaria | Necesidad | Responsabilidad |
| --- | --- | --- |
| Comensal | Identificarse, elegir y recibir una respuesta clara | No administra derechos, precios ni cupos |
| Operador | Resolver incidencias de impresión y del puesto | Acciones privilegiadas con permiso y motivo |
| Administrador de personal | Mantener padrón, catálogo y habilitaciones | Publica configuración para sincronización |
| Administrador del Sistema | Configurar el puesto y probar componentes desde una interfaz local | Permisos específicos, cambios y diagnóstico auditados |
| Soporte técnico | Diagnosticar y recuperar pendientes | Usa datos técnicos sin biometría ni secretos |

En el puesto, Operador y Administrador del Sistema actúan con los roles locales `OPERATOR` y `SYSTEM_ADMINISTRATOR` (§7.4). Soporte técnico usa el rol de Administrador del Sistema cuando interviene en el equipo. Las historias y épicas usan sólo estas cinco personas.

## 2. Producto integral y recorte académico confirmado

El producto integral incluye periféricos reales, administración central, interfaces empresariales, conciliación y operación sostenida. La primera entrega del curso es documental. El MVP académico utilizará **adaptadores de prueba**, personas sintéticas y una base local real cuando se implemente; no se presentará un simulador como integración física terminada.

| Función | Resultado propuesto para las etapas de desarrollo | Frontera |
| --- | --- | --- |
| F01. Identificación y selección | Sesión local de identidad, catálogo habilitado y elección | Contratos para 1:N y código previo + 1:1; adaptadores de prueba en el MVP |
| F02. Autorización y registro local | Intención, decisión, consumo, ObservedConsumption propio con sus ObservedQuotaCharge, contador QuotaUsage, ticket, PrintJob original, outbox y AuditRecord en una transacción SQLite (RB06) | INWeb y la red no intervienen como autorización |
| F03. Comprobante y recuperación | Ticket lógico, trabajo durable y copia auditada | Adaptador de impresora de prueba; homologación física posterior |
| F04. Supervisión y sincronización | Arranque offline, administración local, diagnóstico, colas, recuperación y envío eventual | Adaptadores de prueba; bitácoras externas transversales. ACK no concede ni revoca consumo |

Quedan fuera del recorte académico inicial la liquidación de haberes, administración empresarial completa, pagos, reservas anticipadas, inventario, enrolamiento masivo, invitados automáticos, emisión privilegiada para terceros y canje físico con lector de tickets. El alcance de producto conserva esas dependencias o extensiones según su especificación posterior.

La mayoría de las instalaciones tendrá **un solo puesto autónomo**. Cuando varios puestos requieran auditoría compartida, todos deben informar sus consumos a INWeb en línea y recibir de inmediato los registros de los demás, actualizando su base local. Se conserva la decisión local: existe una ventana de carrera que puede permitir doble consumo antes de recibir el evento del otro puesto. Esa situación debe generar un aviso para auditoría posterior; no se promete exclusión global fuerte.

El comportamiento de una terminal desconectada en ese escenario multi puesto es configurable por instalación: `CONTINUE_LOCAL` (valor inicial) o `SUSPEND_NEW_CONSUMPTIONS`. Se registra la incidencia y se concilian los hechos al reconectar, con aviso ante exceso. Esa cuestión no modifica la autonomía del escenario habitual de un solo puesto. El caso de datos sintéticos de la demo es una propuesta de prueba, no una parametrización comercial aprobada. El cliente permanece anónimo en este paquete.

## 3. Decisiones vigentes y abiertas

| Tema | Criterio de esta versión | Estado |
| --- | --- | --- |
| Autonomía | Identificación, autorización, persistencia y recuperación locales; operación desatendida sin INWeb online | Confirmado por el autor |
| Datos operativos | Último padrón, templates, catálogo y reglas válidamente provisionados/sincronizados | Confirmado por el autor |
| Primera provisión | El puesto necesita una base operativa inicial; puede recibirse como paquete local validado | Condición técnica; mecanismo a diseñar |
| Vigencias | Se respetan fechas de vigencia/revocaciones conocidas; no se agrega una caducidad arbitraria de caché que bloquee por desconexión | Confirmado para este diseño |
| Biometría | BioMini Suprema, templates de personas habilitadas, 1:N y código previo + 1:1, ID asociado para resto del circuito | Ambos flujos definidos por el autor |
| SDK/modelo | Modelo exacto, licencia, arquitectura, formato y compatibilidad de templates | Homologación pendiente |
| MVP | Las cuatro funciones son obligatorias; adaptadores de prueba y datos sintéticos | D-C04 y D-C16 |
| Puesto único | Caso habitual autónomo, sin dependencia de INWeb online | Confirmado por el autor |
| Multi puesto con auditoría | Todos informan a INWeb en línea; consumos de pares actualizan inmediatamente el registro local; carrera residual genera aviso auditable | Confirmado por el autor; sin exclusión global fuerte |
| Desconexión multi puesto | Configurable por instalación: CONTINUE_LOCAL inicial; alternativa SUSPEND_NEW_CONSUMPTIONS. Última configuración válida persistida y aplicada también al reiniciar | D-A02 y D-C10 |
| Día y reloj | Fixture: zona America/Argentina/Buenos_Aires y corte 00:00. Ante salto anómalo, seguir hora de Windows y auditar; se acepta riesgo de fecha/cupo incorrectos | D-C17; parámetros productivos por instalación |
| Catálogo | Categorías/equivalencias, cantidades, precios y permisos concretos | Fixtures sintéticos para la demo; valores productivos pendientes |
| Precedencia de reglas | Entre aplicables vigentes, `Limit=0` deniega; positivas se ordenan por `Priority`; empate contradictorio impide publicación | Decisión técnica propuesta de esta base, no dato comercial aprobado |
| Reimpresión | Permiso, motivo y copia numerada; conserva el consumo | Propuesta operativa a validar |
| Credenciales y permisos locales | Snapshot atómico, códigos no reasignables, revocación recibida sin programación futura; dos roles sin herencia; autenticación biométrica o cuenta local de mantenimiento (§7.4) | ADR12; D-C20–D-C23 confirmadas |
| Prioridad dentro de F01–F04 | Must acredita cada funcionalidad; should se difiere primero según la regla de recorte del plan | D-C19 |

Una falta de red no cambia por sí misma la vigencia de una política. Una política con vencimiento explícito sí debe evaluarse según ese vencimiento. El puesto no puede conocer una baja ocurrida mientras está aislado: debe informar la versión y fecha de última sincronización y aplicar el cambio al recibirlo, conservando los hechos previos.

## 4. Requisitos funcionales

**Núcleo** identifica las cuatro funciones académicas; **Contrato** exige definir el comportamiento y permite adaptadores de prueba; **Producto** conserva alcance integral sin prometerlo en el MVP. Todos los requisitos están especificados, no implementados.

| ID | Requisito y criterio verificable | Recorte |
| --- | --- | --- |
| RF01 | Resolver localmente un código a una sola persona activa del padrón disponible; desconocidos, bajas conocidas o ambigüedades no habilitan selección | Núcleo |
| RF02 | Capturar/comparar huella con BioMini Suprema en 1:N y código previo + 1:1; resultado, rechazo, cancelación o error explícitos | Contrato; doble de prueba en MVP |
| RF03 | Mostrar servicios habilitados y validar localmente persona, credencial y derecho al confirmar una intención nueva; el replay recupera su decisión durable | Núcleo |
| RF04 | Publicar/recibir catálogo versionado; navegar 15 opciones sintéticas sin alterar consumos históricos | Núcleo |
| RF05 | Seleccionar, cancelar o expirar antes de confirmar no consume cupo | Núcleo |
| RF06 | Aplicar cupo transaccional local por persona, categoría, ámbito y día; no prometer control global entre bases aisladas | Núcleo |
| RF07 | Calcular fecha de negocio con hora de Windows y zona/corte configurados; registrar saltos detectados conservando tiempos y secuencia. Continuar según reglas y política de comunicación; cambiar de día no borra historial | Núcleo |
| RF08 | Informar límite local alcanzado sin nuevo consumo ni impresión automática | Núcleo |
| RF09 | Confirmar localmente consumo con versiones, importes y sellos temporales; sincronizar no recalcula su historia | Núcleo |
| RF10 | Guardar ticket lógico y trabajo original en la misma transacción que autoriza el consumo; impresión física sólo después del commit | Núcleo |
| RF11 | Buscar primero la intención; mismo ámbito/payload devuelven decisión durable existente aunque haya vencido la sesión o cambiado la regla. Sólo la nueva intención revalida; sin duplicar efecto | Núcleo |
| RF12 | Separar impresión y consumo; error o falta de papel no anulan ni repiten el consumo | Núcleo |
| RF13 | Reimprimir con permiso/motivo, manteniendo consumo/ticket y numerando una copia auditada | Núcleo |
| RF14 | Síntesis de RF42: emisión por operador para beneficiarios internos/externos con identidad y reglas propias; sin excepciones de cupo | Producto futuro; detalle único en RF42 |
| RF15 | Corregir por compensación conservando el original; restitución de cupo explícita | Producto |
| RF16 | Administrar y publicar catálogo, reglas, equivalencias y habilitaciones para los puestos | Producto; fixtures publicados en MVP |
| RF17 | Consultar consumos, rechazos, impresión y sincronización por separado | Núcleo operativo; reportes completos, Producto |
| RF18 | Conciliar datos de terminales con administración y exponer diferencias sin borrar hechos | Producto; conflictos básicos de sincronización en Núcleo |
| RF19 | Generar novedades para interfaces externas homologadas con lote y acuse deduplicados | Producto |
| RF20 | Aplicar snapshots completos de padrón/reglas/catálogo; descarga parcial conserva la versión operativa anterior | Núcleo |
| RF21 | En monopuesto iniciar y atender sin red con base provisionada. En multipuesto auditado aplicar la política por instalación persistida: CONTINUE_LOCAL inicial o SUSPEND_NEW_CONSUMPTIONS; registrar incidencia, conservar hechos y recuperar intercambio/feed antes de salir de la suspensión | Núcleo |
| RF22 | Persistir intención, resultado y evento de salida; recuperar tras reinicio con idénticos IDs | Núcleo |
| RF23 | Reconstruir tramas serie parciales/múltiples; ruido no genera identidades falsas | Contrato/parser con datos sintéticos |
| RF24 | Informar estado de periféricos, almacenamiento, versión operativa y cola/última sincronización | Núcleo |
| RF25 | Identificar terminal en la sincronización; una credencial remota inválida no se confunde con rechazo del consumo local | Núcleo |
| RF26 | Vincular intención, consumo, impresión, eventos y lote mediante correlación auditable | Núcleo |
| RF27 | Instalar/actualizar preservando base operativa y pendientes; recuperación compatible | Producto |
| RF28 | Usar adaptadores intercambiables para identidad, periféricos y sincronización sin cambiar reglas locales | Núcleo |
| RF29 | Enrolar sólo con autorización y asociación validada; enrolamiento no registra consumo | Producto |
| RF30 | Sincronizar hechos aceptados localmente con deduplicación y resultados individuales; conflictos quedan visibles y conservan el hecho | Núcleo |
| RF31 | Incorporar altas/modificaciones/bajas al recibir una actualización completa, con vigencia y trazabilidad | Contrato; fixtures en MVP |
| RF32 | Ampliar invitados/políticas mediante nueva especificación y aceptación | Producto |
| RF33 | Incluir en matching únicamente templates de personas habilitadas según el conjunto local vigente | Contrato biométrico |
| RF34 | Mantener template → identidad interna; ambos flujos biométricos entregan el ID asociado para el circuito local | Contrato biométrico |
| RF35 | Versionar templates, validar compatibilidad/asociaciones y aplicar actualizaciones completas; conservar último conjunto operativo válido durante una descarga fallida | Contrato biométrico |
| RF36 | Proteger templates; no guardar imágenes crudas por defecto ni biometría en logs, tickets, eventos de consumo o repositorio académico | Núcleo y contrato biométrico |
| RF37 | En multi puesto conectado, aplicar consumos de pares al ledger y contador locales por persona/ámbito/categoría/día, de forma atómica y deduplicada | Núcleo de sincronización |
| RF38 | Detectar exceso causado por carrera entre puestos y generar aviso auditable con referencias a ambos hechos, sin borrarlos ni negar retroactivamente la atención | Núcleo de auditoría |
| RF39 | Publicar inmediatamente el consumo confirmado localmente cuando hay conexión y propagarlo desde INWeb a los demás puestos; el envío usa outbox durable | Núcleo de sincronización |
| RF40 | Configurar el equipo desde una interfaz del Administrador del Sistema; validar y activar revisiones locales íntegras, con permisos y auditoría, sin sobrescribir reglas maestras INWeb | Núcleo F04 |
| RF41 | Diagnosticar API INWeb, táctil, BioMini/SDK, eventos serie, impresora con comandos completos soportados, SQLite, red y versiones; registrar ejecución/resultado y distinguir simulación de hardware | Núcleo F04 con adaptadores; homologación física, Producto |
| RF42 | Operador privilegiado emite para terceros internos/externos registrando qué, para quién, operador, cuándo y observaciones; usa derechos/cupos del beneficiario sin bypass | Producto futuro; fuera del MVP |
| RF43 | Generar bitácora general externa a SQLite con eventos del sistema, configuración, diagnóstico, fallas, sincronización y recuperación | Núcleo transversal |
| RF44 | Generar bitácora independiente de interacciones para reconstruir sesiones, decisiones, consumos, rechazos y comprobantes con correlación y estados observados | Núcleo transversal |
| RF45 | Escribir ambas bitácoras en archivos de texto plano independientes por mes, con AAAAMM en el nombre; conservar tiempos originales al recuperar registros | Núcleo transversal |
| RF46 | Si falla un archivo, continuar con respaldo durable SQLite; reconstruir y señalar el intervalo degradado, sin duplicar hechos. Si no puede persistir tampoco en SQLite, no confirmar consumos nuevos | Núcleo transversal |

RF33–RF36 explicitan la aclaración biométrica posterior; RF37–RF39, la decisión multi puesto. La pertenencia al conjunto de templates no sustituye la evaluación local del derecho al servicio ni del cupo.

## 5. Reglas de negocio e invariantes

| ID | Regla verificable |
| --- | --- |
| RB01 | `PersonId` es estable. Código, legajo y credencial son referencias con tipo/origen/vigencia; conservar ceros iniciales |
| RB02 | Evaluar primero persona activa y credencial vigente, con motivos separados. Filtrar reglas por sujeto/grupo, servicio, ámbito, categoría y vigencia: `ValidFromUtc` inclusiva, `ValidToUtc` exclusiva; extremos ausentes dejan intervalo abierto. Entre aplicables, `Limit=0` es denegación explícita y prevalece sobre cualquier permiso, aunque tenga mayor `Priority`. Entre reglas positivas gana mayor `Priority`; empate contradictorio impide publicar. Sin regla aplicable se deniega |
| RB03 | Cupo por `PersonId` + `QuotaScopeId` + `QuotaBucketId` + `BusinessDate`, evaluado sobre el ledger local con hechos propios y de pares ya recibidos. Variantes pueden compartir categoría. No hay exclusión global fuerte durante la propagación |
| RB04 | Calcular `BusinessDate` con la hora de Windows y zona/corte configurados. Ante salto detectado continuar con esa hora, auditar anomalía y vincular hechos afectados; se acepta posible imputación a otra fecha y cupo incorrecto. Sin piso temporal ni suspensión nueva por reloj. Sincronizar no reescribe hechos/fechas; las fechas de sus cargos de cupo coinciden con la del consumo |
| RB05 | Identificar, elegir y consumir son hechos distintos. Sólo el commit local de consumo afecta cupo y habilita comprobante; papel emitido no acredita retiro físico |
| RB06 | Para una intención nueva, la unidad SQLite inicia BEGIN IMMEDIATE, comprueba de nuevo IntentId/hash y lee snapshot activo y cupo; invoca al dominio y persiste su decisión antes de liberar la misma transacción. Una autorización guarda intención, decisión, consumo, ObservedConsumption propio con sus ObservedQuotaCharge, contador QuotaUsage, ticket, PrintJob original, outbox y AuditRecord. Un rechazo conserva intención/decisión, outbox y AuditRecord, sin consumo, cargas ni ticket/trabajo. La disponibilidad previa es orientativa; una decisión calculada antes del bloqueo no habilita el commit |
| RB07 | Primero localizar IntentId y verificar ámbito/pertenencia y hash del payload. Si existe decisión durable compatible, devolverla sin revalidar sesión, baja, catálogo o cupo actuales; payload distinto produce conflicto. Sólo una intención nueva valida reglas y cupo. Reinicio conserva IDs |
| RB08 | Reimprimir no modifica consumo/cupo/importe. Spooler no prueba papel entregado; copia posterior a incertidumbre queda marcada y auditada |
| RB09 | Original inmutable; anulación/restitución mediante compensación explícita. Ni error de impresión ni conflicto de sincronización anulan automáticamente una comida |
| RB10 | Precio de comensal y costo de prestador son diferentes. La regla local versionada resuelve precio/moneda/escala; ausente no significa cero; cambios requieren reconfirmación antes del commit |
| RB11 | Toda excepción conserva identidad, responsable y origen; no usa credenciales prestadas ni duplica hechos por otra vía |
| RB12 | Consumo y asistencia son hechos distintos. Proyecciones y lotes deduplican por origen/ID; el paralelo tiene una sola fuente económica definida |
| RB13 | En 1:N comparar sólo conjunto habilitado; en 1:1 sólo templates de la identidad del código. Sin coincidencia, ambiguo o asociación inconsistente no autoriza |
| RB14 | Matching devuelve identidad, no permiso. Sesión local breve vinculada a la operación; el dominio valida derecho/cupo para una intención nueva. Recuperar una decisión ya durable no exige repetir matching ni renovar sesión. Un `PersonId` arbitrario no sustituye el contrato de identificación |
| RB15 | Monopuesto conserva autonomía. En multipuesto auditado rige la última política configurada por instalación: CONTINUE_LOCAL inicial permite nuevos consumos con reglas/cupos conocidos e incidencia; SUSPEND_NEW_CONSUMPTIONS impide nuevas confirmaciones cuando falla envío a INWeb o recepción del feed. Replay y comprobantes previos se recuperan antes de aplicar ese bloqueo. La ausencia de red no agrega caducidad de la configuración ni anula hechos |
| RB16 | ACK central significa recepción/deduplicación del evento, no permiso para consumir. Conflictos se registran para revisión sin eliminar ni negar retroactivamente hechos locales |
| RB17 | Reportar éxito sólo después del commit único de RB06. Ticket/PrintJob original y consumo quedan presentes juntos o ausentes juntos. El worker físico trabaja después; recuperar retoma el trabajo existente sin reconstruir ni crear otro original |
| RB18 | Aplicar evento de par, registrar su deduplicación y actualizar ledger/contadores en una transacción local. La identidad del hecho incluye origen y consumo; recibir el eco propio o el mismo hecho en otro lote no vuelve a consumir cupo |
| RB19 | Un consumo de otro puesto cuenta en la categoría/ámbito/fecha correspondientes aunque no se haya originado en esta terminal. Importarlo no imprime un ticket nuevo ni crea otro hecho económico |
| RB20 | Si dos confirmaciones locales superan el cupo compartido durante una carrera, conservarlas y crear aviso de auditoría con IDs, origen, versiones y tiempos. El aviso se deduplica; no se convierte en anulación automática |
| RB21 | La interfaz administrativa configura el equipo; padrón, precios, cupos y política multipuesto mantienen la autoridad de INWeb. Todo cambio/prueba tiene permiso, actor y resultado |
| RB22 | Emitir para terceros no otorga derecho ni aumenta cupo: se evalúa al beneficiario, con identidad estable interna/externa y operador separado; observaciones obligatorias, sin excepción de cupo |
| RB23 | Registrar auditoría durable con la transición de negocio; exportarla a archivos independientes. Un fallo del archivo no impide consumo si SQLite conserva hecho y auditoría; no hay atomicidad DB/archivo |
| RB24 | Cada evento conserva identidad, secuencia, tiempos originales y destino mensual; reconstruir no crea otro consumo ni reescribe historia, y explicita períodos degradados e incertidumbre |
| RB25 | Resolver por tipo y valor normalizado contra el snapshot activo validado. Un solo registro por tipo/valor, incluso si está revocado; no se reasigna a otra persona, tampoco entre snapshots (D-C20). Si tiene RevokedAtUtc, da CREDENTIAL_REVOKED desde la activación, sin esperar al reloj local. Vencida da CREDENTIAL_EXPIRED; desconocida o todavía no vigente da IDENTITY_NOT_RESOLVED, con causa interna UNKNOWN_CODE o NOT_YET_VALID. Los inválidos no abren selección ni consumo; el válido sólo selecciona candidato y exige huella 1:1 para crear sesión. No registrar el código en claro |
| RB26 | Toda acción privilegiada local exige autenticación por código y huella 1:1 o cuenta individual local de mantenimiento (D-C22), más permiso del rol requerido vigente en el snapshot activo. Roles sin herencia; comprobar el permiso en cada acción. La sesión privilegiada no habilita consumo y se cierra al terminar o expirar. Revocación efectiva al activar el snapshot que la comunica. Auditar acción, resultado, causa y RequestedRole; PersonId sólo si se resolvió el actor, GrantId y GrantedRole sólo si existe un permiso evaluado |

La representación de denegación mediante `Limit=0`, su precedencia y los límites de vigencia de RB02 son decisiones técnicas propuestas para esta base. No afirman que exista una restricción comercial concreta. Una regla cero vencida no participa en precedencia; una persona inactiva o credencial revocada conserva su motivo propio y no se clasifica como regla cero. RB25 y RB26 concretan la vigencia de credenciales que exigen RB01 y RB02 y los permisos locales que suponen RB08 y RB21.

## 6. Biometría: dos recorridos obligatorios del producto

BioMini Suprema es el dispositivo definido. Modelo exacto, SDK/licencia, arquitectura de DLL, formato/algoritmo y compatibilidad de templates permanecen pendientes de homologación. La versión de hardware y software se registrará en la homologación.

### 6.1 Identificación 1:N

1. Cargar el conjunto local válido de templates de personas habilitadas y sus asociaciones.
2. Capturar con control de calidad, cancelación y timeout del SDK homologado.
3. Comparar contra ese conjunto; una única coincidencia aceptable devuelve el ID asociado. Cero coincidencias, ambigüedad o mapping inconsistente no crean sesión válida.
4. Crear la sesión local y continuar selección, autorización, consumo y comprobante sin consultar INWeb.

### 6.2 Código previo y verificación 1:1

1. Resolver localmente el código recibido a una sola identidad vigente, mediante una credencial vigente y no revocada (RB25).
2. Seleccionar exclusivamente el template o conjunto de templates de esa identidad.
3. Capturar y verificar. Una huella de otra persona produce rechazo; no sustituye la identidad ni dispara búsqueda global 1:N.
4. Con verificación válida, entregar el ID asociado al mismo circuito local de selección/consumo.

Ambos recorridos permanecen en el producto. El MVP usa dobles de prueba para sus contratos y estados. La aceptación del dispositivo requiere después demostrar ambos recorridos con lector, SDK y templates autorizados, medir calidad/latencia y comprobar reconexión.

### 6.3 Vigencia, protección y recuperación

Cada referencia de template tendrá origen, ID, persona, versión de algoritmo/conjunto, vigencia y revocación conocida. El contenido biométrico se mantiene protegido y separado del registro de consumo; no se incluye en eventos de sincronización de consumos ni en ejemplos académicos.

Una descarga incompleta o incompatible no reemplaza el conjunto válido. La terminal sigue operando con la última base operativa y sus reglas explícitas; si no existe una primera provisión válida no puede inventar padrón ni templates. Su instalación debe permitir incorporar un paquete local validado sin dependencia obligatoria de red.

El broker informa captura, comparación, cancelación, rechazo, desconexión y error recuperable. Retirar USB no permite reutilizar una huella previa. Deben definirse alternativa de identificación, custodia/retención, permisos y eliminación de templates antes de usar datos reales. Los umbrales de matching y detección de presentación se fijan con evidencia del equipo/SDK.

## 7. Operación local, comprobante y sincronización

Flujo visible: reposo → identidad local → selección → confirmación → transacción local → decisión → comprobante/asistencia → reposo. Cancelar antes de confirmar no registra consumo. Una falla incierta de proceso se resuelve consultando la intención local, no enviando un pedido de autorización a INWeb.

Para una intención nueva, la autorización confirma en una sola transacción SQLite intención, decisión, consumo, ObservedConsumption propio con sus ObservedQuotaCharge, contador QuotaUsage, ticket, PrintJob original, outbox y AuditRecord. El worker de impresión procesa ese trabajo sólo después del commit. Una caída durante la transacción deja todos esos efectos confirmados o ninguno; una caída posterior recupera el estado del trabajo ya existente. No hay una etapa posterior que deba crear el trabajo original de un consumo confirmado. Si se desconoce si salió papel, un operador decide una copia marcada; no se promete una única impresión física.

El replay busca primero `IntentId` y verifica pertenencia/ámbito y payload. Una decisión durable compatible se devuelve tal como fue registrada, aun si posteriormente venció la sesión, se recibió una baja o cambió el catálogo/snapshot. Es recuperación de un hecho, no una autorización nueva. Sólo si no existe esa decisión se procede según el estado de la intención y se evalúan sesión, política y cupo para una nueva decisión; no se altera el original para acomodarlo a reglas posteriores.

El sincronizador recibe configuración y envía lotes de hechos cuando hay conectividad. Los eventos tienen IDs estables, origen y versión. El servidor deduplica y devuelve resultado/acuse. Reintentar el mismo lote o consultar su estado no crea otro consumo. Un rechazo técnico de lote deja el pendiente local recuperable; un conflicto de negocio conserva el consumo y abre revisión.

En el modo multi puesto con auditoría, cada commit conectado activa el envío inmediato de su evento durable. INWeb consolida y distribuye los hechos a los demás puestos sin una espera de lote periódico como condición de la operación normal. El receptor incorpora cada hecho al ledger local y recalcula/actualiza los cupos correspondientes en la misma transacción que su marca de deduplicación. El transporte deberá definir cursor/versión y recuperación tras interrupción; si la selección de tres endpoints no expresa toda la difusión, se documentará el contrato complementario antes de implementar.

El cupo que ve una terminal incorpora consumos de otros puestos ya recibidos. La deduplicación debe reconocer un consumo aunque aparezca como eco propio, en un lote repetido o durante recuperación de un snapshot/feed, evitando contarlo dos veces. La propagación reduce la ventana de carrera, pero no la elimina. Su latencia se medirá por separado del commit local; “inmediato” significa disparo al commit y distribución sin espera deliberada, no tiempo cero garantizado.

La [selección OpenAPI](../api/openapi.yaml) documenta exactamente tres operaciones: obtener configuración, enviar lote de sincronización y consultar lote. Ninguna de ellas autoriza al comensal. Las autorizaciones remotas protegen ese intercambio; una credencial de sincronización vencida se informa como incidencia técnica y no convierte en denegados consumos locales ya confirmados. Para nuevas operaciones multipuesto se aplica la política local persistida, igual que ante pérdida de red o interrupción del feed.

### 7.1 Política configurable de comunicación — D-A02 cerrada

La instalación se identifica mediante SiteId en la base académica. El publicador autorizado incluye `OperationMode` y `MultiKioskOfflinePolicy` en cada snapshot completo. La misma política se administra para los puestos de esa instalación; su recepción no es simultánea y cada puesto informa la versión efectiva. Se inicializa explícitamente como `CONTINUE_LOCAL`; un reinicio, descarga parcial o error no restaura ese valor sobre una política ya configurada. El cambio queda versionado y auditable. No es un control que pueda cambiar el comensal.

El detector supervisa las dos direcciones: publicación autenticada a INWeb y recepción del feed de pares. Una conexión TCP abierta no demuestra recepción vigente; se requieren señal de vida y continuidad de secuencia incluso sin nuevos consumos. Corte, error de autenticación, timeout de intercambio o hueco del feed producen estado degradado e incidencia local durable. El umbral técnico de detección se homologará antes de implementar; no es una caducidad de las reglas. Entre la pérdida física y su detección sigue existiendo una ventana de carrera.

- `CONTINUE_LOCAL`: atiende desatendidamente con último padrón/reglas y cupos conocidos; enlaza los consumos nuevos con la incidencia y conserva sus eventos en outbox. No promete cupo global actualizado durante el aislamiento.
- `SUSPEND_NEW_CONSUMPTIONS`: al confirmar una intención nueva revalida el estado de comunicación junto a la política activa. Si está degradado o aún no se verificó tras un arranque, registra decisión técnica `MULTI_KIOSK_COMMUNICATION_REQUIRED`, sin consumo, cargo de cupo, ticket ni PrintJob. Conserva diagnóstico, reimpresión autorizada y replay de decisiones previas. Después de recuperarse, una nueva confirmación usa otra intención; el replay de una denegación durable no se transforma en autorización.
- En ambos casos, restaurar la red inicia envío deduplicado de pendientes y recuperación del feed por cursor. El perfil suspendido reanuda cuando el intercambio está autenticado, los pendientes hasta un punto de corte tienen recepción central durable y el ledger ha recuperado el feed hasta el punto de corte acordado, sin huecos. No basta el primer ping, ni se espera la resolución humana de avisos de negocio. Una nueva pérdida mantiene o reabre la incidencia. La consolidación conserva todos los hechos y genera avisos por exceso, también si provienen de operación aislada.

Incidencia, cambio de política y transición de recuperación quedan auditables por equipo, versión, tiempos y cursor. Un commit ya terminado no se deshace al detectar la pérdida ni al elegir suspensión. El cambio de política sólo gobierna confirmaciones futuras.

### 7.2 Administración, terceros y bitácoras

El [contrato detallado](administracion-y-bitacoras.md) clasifica RF40–RF46, matriz
de diagnóstico, permisos, nombres/estructura mensual, respaldo durable y recuperación.
RF42 es futuro; configuración/diagnóstico con adaptadores y archivos reales pertenecen
al MVP por implementar. D-C14 confirma continuidad con respaldo SQLite ante fallo
del archivo; D-C15 confirma ausencia de excepciones de cupo al emitir para terceros.
Los AT de autonomía presuponen persistencia operativa; una falla del archivo con
respaldo SQLite no introduce una dependencia de INWeb ni modifica D-A02.

### 7.3 Política del reloj y fecha de negocio

El puesto toma la hora de Windows y calcula el día con la zona y el corte configurados. Ante un retroceso o adelanto anómalo detectado, mantiene esa referencia y registra la anomalía (D-C17). Las reglas vigentes y el cupo se evalúan contra la fecha así calculada: la continuidad puede implicar una imputación a otro día, una autorización adicional o un rechazo que deba revisarse. La aceptación de este límite es parte del alcance; no se garantiza fecha verdadera durante aislamiento.

El diseño del detector compara avances del reloj civil UTC con una medición monotónica durante el mismo arranque y registra el cambio de fecha de negocio asociado. En el fixture se prueban saltos de una hora y de un día en ambas direcciones. El umbral técnico se configura y calibra antes de implementar; pequeñas correcciones normales no se clasifican automáticamente como un incidente. Cada arranque recibe un nuevo BootId; se conserva la última observación durable con su BootId anterior: un retroceso observable se marca, mientras que un adelanto sin referencia confiable queda como incertidumbre, no como hora verificada.

La auditoría conserva ClockIncidentId en el contexto, observaciones antes/después, BootId, secuencia, zona/corte y referencia a decisiones/consumos del intervalo. El supervisor muestra aviso y estado de revisión; el cierre registra la acción y la referencia utilizada, sin reescribir el historial. Los consumos mantienen su BusinessDate y los archivos su LogMonth original. Cualquier corrección económica sigue el flujo futuro de compensaciones; no se ejecuta automáticamente al sincronizar la hora.

Una anomalía horaria por sí sola no suspende confirmaciones ni impone un piso temporal. Continúan aplicándose las precondiciones independientes: persistencia durable y política multipuesto configurada. Replay devuelve el resultado original. Los cargos de un consumo comparten BusinessDate; el receptor rechaza como INVALID_EVENT un evento que incumple esa igualdad, conserva evidencia para revisión y no anula el hecho local.

### 7.4 Credenciales y control de acceso local

**Credenciales.** Cada código de identificación es una fila `PersonCredential` del snapshot: persona, tipo, valor normalizado (texto que conserva ceros iniciales), vigencia `[ValidFromUtc, ValidToUtc)` y `RevokedAtUtc` opcional. Viaja en `GET /configuration` junto al padrón y se activa con el mismo snapshot atómico, de modo que persona y credencial nunca quedan en versiones distintas. El código es un identificador, no un secreto: la verificación 1:1 aporta la prueba biométrica. No se escribe en logs, tickets ni eventos de consumo.

Al presentarse un código, el puesto aplica RB25. Si la credencial es válida, selecciona un candidato con su `CredentialId`; sólo una verificación 1:1 satisfactoria crea la sesión de identidad; si no, registra una intención denegada sin sesión con el motivo propio y vuelve a reposo. Al confirmar una intención nueva se vuelve a comprobar la credencial de la sesión contra el snapshot activo. El replay de una decisión ya durable no se reevalúa (RB07). Una revocación central rige desde la activación del siguiente snapshot: es la misma demora conocida que tienen las bajas.

**Validación temporal e histórica.** Publicación y staging rechazan RevokedAtUtc posterior a PublishedAtUtc del sobre, en credenciales y permisos. Un campo de revocación presente expresa una revocación ya conocida, no una agenda; una corrección del reloj no la deshace. ValidFromUtc es inclusivo y ValidToUtc exclusivo. Esta regla no cambia la hora de Windows ni la continuidad de D-C17. Para un CredentialId conocido, persona, tipo y valor se mantienen estables. El retiro de una fila no libera el código para otra persona. La publicación requiere un registro durable de titularidad y el puesto contrasta su historial conocido; M0 define el ámbito y cómo se provisiona evidencia histórica completa en un puesto nuevo/restaurado. Un snapshot aislado no prueba la ausencia de asignaciones anteriores. La renovación para la misma persona no es reasignación: no debe crear dos filas para el mismo código en el snapshot; su procedimiento queda en M0.

**Control de acceso local.** Los permisos son filas `LocalOperatorGrant` del snapshot: persona, rol, vigencia y revocación. El MVP define dos roles:

| Acción local | `OPERATOR` | `SYSTEM_ADMINISTRATOR` |
| --- | --- | --- |
| Recuperar un comprobante y reimprimir una copia con motivo | Sí | No |
| Consultar estados, pendientes y bitácoras del puesto | Sí | Sí |
| Validar y aplicar parámetros locales del equipo | No | Sí |
| Ejecutar diagnósticos de componentes | No | Sí |
| Exportar bitácoras | No | Sí |

Una persona puede tener ambos roles con dos permisos. El actor se autentica sin red con su código y la verificación 1:1 de su huella o mediante una cuenta individual local de mantenimiento; la sesión privilegiada tiene propósito propio y no sirve para consumir. El caso de uso comprueba el permiso en cada acción, no sólo al mostrar el botón (RB26). Revocar es publicar un snapshot con `RevokedAtUtc` o sin el permiso; al activarse, la acción siguiente se deniega aunque haya una sesión abierta. Cada intento, aceptado o denegado, genera un AuditRecord con acción, resultado, causa y RequestedRole. Si el actor fue resuelto se registra PersonId; si existe un permiso evaluado, GrantId y GrantedRole. La ausencia de permiso se registra como tal, sin UUID ficticio ni reutilizar el grant de otro rol. En autenticación fallida pueden faltar actor y grant. Estos campos forman parte de Context; el detalle físico se fija en M0.

**Mantenimiento alternativo.** La cuenta usa usuario y contraseña locales individuales, independientes del lector y de INWeb. Autentica al actor; no concede roles ni crea una sesión de comensal. Se conserva la autorización por acción de RB26, incluyendo la prohibición de reimprimir sin OPERATOR. El MVP usa una cuenta sintética. Provisión inicial, asociación con PersonId, verificador protegido, límites de intentos, expiración, recuperación y restauración quedan como pendientes verificables de M0 en [administración](administracion-y-bitacoras.md#mantenimiento-alternativo-y-pendientes-de-m0). No se incorpora una contraseña al snapshot ni un bypass automático.

Límites aceptados: sin red, una revocación todavía no recibida no se conoce; un rol de soporte propio, la revocación inmediata de emergencia y el enrolamiento del operador quedan para el producto. La firma del paquete local de provisión se especificará en M0 junto con el contrato de provisión inicial.

## 8. Escenarios de aceptación trazables

Todos están **diseñados, sin ejecutar**. Las pruebas de contrato biométrico con adaptadores no acreditan hardware. “Producto” identifica alcance posterior al MVP.

| ID | Preparación y acción | Resultado esperado | Requisitos/reglas |
| --- | --- | --- | --- |
| AT01 | Monopuesto/CONTINUE_LOCAL: persona sintética habilitada confirma con INWeb inaccesible | Una decisión, consumo, cupo y outbox locales; ticket lógico asociado | RF01/03/09/21; RB02/05/06 |
| AT02 | Cupo local agotado; nueva intención | Rechazo por límite; sin consumo/ticket adicional | RF06/08; RB03 |
| AT03 | Dos conexiones SQLite reales confirman simultáneamente el último cupo; ambas pueden haber mostrado disponibilidad antes del bloqueo | BEGIN IMMEDIATE protege relectura, decisión de dominio y persistencia: una autorización, un rechazo y contador válido; además, mismo IntentId concurrente devuelve una sola decisión durable | RF06/11; RB03/06/07 |
| AT04 | Confirmaciones a ambos lados del corte configurado | Fechas de negocio diferentes e historial íntegro | RF07; RB04 |
| AT05 | Caída después del commit local y antes de mostrar resultado | Mismos IDs al recuperar; un efecto y un trabajo original | RF11/22; RB07/17 |
| AT06 | Impresora falla después de autorizar | Consumo preservado, trabajo recuperable y copia auditada cuando corresponda | RF10/12/13; RB08 |
| AT07 | Trama partida, múltiples tramas, ruido y exceso de tamaño | Eventos válidos o rechazo seguro; sin concatenación de identidades | RF23; RB01 |
| AT08 | El adaptador biométrico de prueba simula desconexión durante captura y reconexión | Cancelación/liberación y sesión nueva sin datos residuales; ensayo físico posterior | RF02/36 |
| AT09 | Descarga parcial de configuración y posterior actualización completa con baja | Mantiene anterior ante descarga fallida; aplica baja al activar nueva versión, sin borrar consumos previos | RF16/20/31; RB02/15 |
| AT10 | Monopuesto/CONTINUE_LOCAL: arranque frío sin red con base previamente provisionada | Identifica, selecciona, autoriza e imprime mediante adaptadores disponibles; eventos quedan pendientes | RF21/22; RB15/17 |
| AT11 | Corrección de consumo ya consolidado | Compensación vinculada y original intacto; Producto | RF15/19; RB09/12 |
| AT12 | Servidor detecta diferencia entre hechos de dos terminales | Conserva ambos hechos y registra conflicto; no deniega retroactivamente la comida | RF18/30; RB16 |
| AT13 | Credencial de sincronización inválida o consulta de lote ajeno | Remoto deniega sin datos ajenos; hechos locales conservados; en monopuesto la atención sigue según reglas disponibles | RF25/26; RNF08 |
| AT14 | Migración falla con pendientes | Recuperación compatible sin perder intención/consumo/evento; Producto | RF27; RNF10 |
| AT15 | Emisión futura para un tercero interno o externo autorizado | Operador y beneficiario separados, servicio/fecha/observaciones; cargo al beneficiario sin exceder sus reglas; Producto | RF14/42; RB11/22 |
| AT16 | Catálogo de 15 opciones y timeout antes de confirmar | Navegación usable; cero consumo; siguiente persona inicia limpia | RF04/05 |
| AT17 | Persona consume fuera de un horario sólo informativo | No se agrega restricción horaria ajena a la regla configurada | RF03/06; RB02 |
| AT18 | Anulación con restitución explícita y nuevo consumo | Original/compensación intactos y contador neto coherente; Producto | RF15; RB09 |
| AT19 | 1:N con coincidencia habilitada y templates revocados excluidos | ID correcto según conjunto local; continuidad sin INWeb | RF02/33/34; RB13/14 |
| AT20 | En 1:1, código de A y huella de B | Rechazo; no cambia a B ni autoriza con A | RF02/34; RB13 |
| AT21 | En 1:1, código y huella verifican A | ID de A; mismo circuito local de consumo utilizado en 1:N | RF02/34; RB14 |
| AT22 | Matching sin coincidencia, ambiguo o mapping inválido | Sin sesión válida; mensaje seguro sin datos de terceros | RF33/34; RB13 |
| AT23 | Conjunto nuevo incompatible o incompleto | No sustituye al válido; estado técnico visible; sigue el conjunto anterior según sus vigencias | RF35; RB15 |
| AT24 | Inspección de trazas/ticket/eventos después de ambos flujos | Sin imágenes/templates/secretos ni códigos completos innecesarios | RF26/36; RNF08 |
| AT25 | Misma intención con payload distinto | Conflicto local; decisión original inmutable | RF11; RB07 |
| AT26 | Doble toque y otra lectura durante confirmación | Una intención y persona original; sin segundo consumo | RF05/11; RB05/07 |
| AT27 | Se pierde ACK de lote y se reenvía/consulta con mismos IDs | Servidor deduplica; consumo local intacto; cola sólo avanza con resultado durable | RF22/30; RB16 |
| AT28 | Monopuesto/CONTINUE_LOCAL: la base tiene reglas sin vencimiento; permanece desconectada un período prolongado | Continúa según última configuración; no se introduce vencimiento por antigüedad de caché | RF21; RB15 |
| AT29 | Una regla tiene fecha de fin explícita ya alcanzada | Se evalúa esa vigencia como regla de negocio, sin confundirla con falta de red | RF03/07; RB02/04/15 |
| AT30 | Multipuesto auditado: pérdida de envío o feed, por separado, con cada una de las dos políticas; repetir tras reinicio | CONTINUE_LOCAL inicial conserva atención e incidencia; SUSPEND_NEW_CONSUMPTIONS deniega sólo nuevas confirmaciones con motivo técnico. Recupera intercambio y feed antes de reanudar; replay y hechos anteriores intactos, excesos auditables | RF06/20/21/24/30/37–39; RB03/07/15/16/18/20 |
| AT31 | A confirma y su evento llega a B antes de que B confirme el mismo cupo | B aplica el consumo de A a su ledger/contador y rechaza por cupo agotado sin consultar permiso remoto | RF37/39; RB03/18/19 |
| AT32 | A y B confirman durante la ventana de propagación | Ambos hechos se conservan; consolidación detecta exceso y produce un aviso auditable que los vincula | RF38/39; RB20 |
| AT33 | Un hecho llega repetido, como eco propio y dentro de otro lote | Ledger/contador lo aplican una sola vez; no se imprime ni crea consumo adicional | RF37; RB18/19 |
| AT34 | B cae durante recepción del evento de A y luego recupera el feed | Hecho y contador se aplican completos una vez; cursor/acuse no avanzan dejando el cupo sin actualizar | RF22/37; RB18 |
| AT35 | Consumo local confirmado; después vence la sesión o llega baja/cambio de catálogo. Se reintenta misma intención/ámbito/payload | Devuelve decisión/IDs originales sin revalidar para cambiar su resultado; sin nuevo consumo, ticket o PrintJob. Una intención distinta sí evalúa el estado actual | RF03/10/11; RB06/07/14/17 |
| AT36 | Persona activa y credencial vigente; permiso general positivo de Priority mayor y regla aplicable `Limit=0` con inicio/fin definidos | Dentro de `[ValidFromUtc, ValidToUtc)` deniega por regla explícita, no por baja. En el límite final la regla cero ya no aplica y se evalúa el permiso restante; extremos ausentes no agregan un vencimiento | RF03/20; RB02 |
| AT37 | Administrador autorizado modifica parámetros del equipo offline; operador sin permiso intenta lo mismo; revisión inválida | Cambio válido versionado/auditado, denegación del no autorizado y conservación del valor previo ante error; reglas maestras no se editan | RF40; RB21 |
| AT38 | Ejecutar la matriz de diagnóstico con adaptadores sintéticos y errores inyectados | Estado/versión/resultado por componente, sin consumo ni secretos; comandos completos de impresora de prueba y correlación | RF41/43; RNF14 |
| AT39 | Prueba BioMini/serie/impresora con sesión activa y luego cancelación/timeout | Reserva segura o espera a mantenimiento, sin robar dispositivo ni mezclar identidad; libera recursos y audita | RF41; RB21; RNF14 |
| AT40 | Emisor para terceros prueba beneficiario interno/externo sin derecho o sin cupo; repite intención con otro beneficiario | Rechazo sin consumo ni bypass, conflicto de payload al cambiar beneficiario; autorizado carga al beneficiario; Producto futuro | RF14/42; RB22/07 |
| AT41 | Sesión autorizada, rechazada, cancelada y recuperación de impresión | Archivos general/interacciones distintos permiten reconstruir acciones/resultados con mismos IDs; incertidumbre física explícita, sin biometría/secretos | RF43/44; RB23/24; RNF13 |
| AT42 | Cambio de mes, reinicio/salto de reloj y exportación tardía del mes anterior | Nombres AAAAMM y destino original, secuencias/tiempos conservados, recuperación marcada y sin perder historial | RF45/46; RB24 |
| AT43 | Fallar escritura de cada archivo y ambos, con SQLite operativo; recuperar luego | Continúa atención y respaldo durable; cola e intervalo degradado visibles; reconstruye ambos destinos hasta corte sin duplicar consumos | RF46; RB23/24 |
| AT44 | Caída antes/después de append/flush y antes de marcar entrega; línea parcial; luego fallan archivo y DB | Recuperación por AuditId, evidencia de reparación y un hecho lógico; sin éxito de consumo nuevo si no hay persistencia, sin garantía falsa sobre eventos no guardados | RF43–46; RB06/23/24; RNF13 |
| AT45 | Supervisor consulta consumo confirmado, rechazo, impresión fallida y sincronización pendiente | Estados y referencias separados; un fallo de impresión o sincronización no convierte un consumo en denegado | RF17/24/26; RB05/08/16 |
| AT46 | Dos adaptadores sintéticos compatibles reciben el mismo guion, con IDs de prueba fijados, en bases independientes e idénticas | Sustituir adaptador conserva reglas y resultado semántico; cada ejecución conserva sus IDs frente a reintentos, sin modificar el dominio | RF28; RNF09 |
| AT47 | Cambia precio o versión de menú entre selección y confirmación | Informa nuevo valor y exige reconfirmación antes de crear consumo/ticket; consumos anteriores conservan su snapshot monetario | RF04/09; RB10 |
| AT48 | Reloj retrocede una hora o cruza al día anterior, con persistencia y política de comunicación habilitantes | Sigue con hora de Windows y reglas de esa fecha; audita salto/consumos vinculados sin piso ni bloqueo por reloj; conserva hechos previos y advierte riesgo de cupo | RF07/26/43/44; RB04/24; D-C17 |
| AT49 | Reloj avanza al día siguiente y luego se corrige; repetir con reinicio | Evalúa cada nueva intención según hora Windows vigente, conserva fechas ya confirmadas, registra observaciones/BootId e incertidumbre; no asegura fecha real sin referencia | RF07/26/43/44; RB04/24; D-C17 |
| AT50 | Un evento declara fecha de cargo de cupo distinta de BusinessDate del consumo | Validación de aplicación devuelve REJECTED/INVALID_EVENT, conserva evidencia y hecho local; ejemplo coherente acepta igualdad. El esquema estructural por sí solo no acredita esta regla | RF30/37; RB04/16/18 |
| AT51 | Persistencia y adaptador aplican el mapa de nombres; dos instancias o tipos de entidad comparten código nativo y una persona reaparece en otro snapshot | Correspondencias sin colisiones; referencias externas no se confunden; UUID de la misma persona y operaciones se conservan; PK/FK compuestas y tipos lógicos mantienen su significado; serialización y deserialización conservan nombres, tipos, enums y ejemplos del contrato | RNF15; D-C18; ADR11 |
| AT52 | Código vigente con huella correcta/incorrecta y códigos vencido, revocado, futuro y desconocido; límites exactos ValidFromUtc/ValidToUtc | Sólo vigente + huella correcta abre selección; código solo no crea sesión. Vencido: CREDENTIAL_EXPIRED; revocado: CREDENTIAL_REVOKED; futuro/desconocido: IDENTITY_NOT_RESOLVED con causa interna distinta. Inválidos sin selección ni consumo; sin código en logs | RF01/03; RB01/02/25 |
| AT53 | Persona con sesión abierta; se activa un snapshot que revoca su credencial antes de confirmar; luego se repite una intención ya decidida | La intención nueva se deniega con CREDENTIAL_REVOKED; el replay devuelve el resultado original; ninguna credencial ni consumo previo se borra | RF03/11/20; RB02/07/25 |
| AT54 | Sin red, reimpresión con motivo por operador vigente, vencido, revocado y administrador sin OPERATOR; también autenticación fallida y revocación durante sesión | Sólo el operador vigente crea copia numerada. Rechazos sin trabajo ni cambio de consumo. Auditar RequestedRole y causa; actor si conocido, GrantId/GrantedRole sólo si existe permiso evaluado; siguiente acción rechazada tras activar revocación | RF13/40; RB08/21/26; RNF08 |
| AT55 | Snapshot con código duplicado incluso revocado, reasignación entre personas tras retirar la fila anterior, CredentialId cambiado de titular, revocación futura respecto de PublishedAtUtc, referencia inexistente o rol desconocido | Rechazo en staging; snapshot activo intacto y motivo visible. Orden de filas irrelevante; historial no se elimina al retirar una credencial. Puesto nuevo requiere provisión histórica definida en M0 | RF20/31; RB15/25/26 |
| AT56 | Sin red y con lector inutilizable, acceso de mantenimiento por cuenta individual; contraseña inválida, límite de intentos, sesión expirada, permiso insuficiente y restauración | Cuenta válida y rol vigente permiten administración; los demás casos se deniegan y auditan sin secretos. No consumo ni reimpresión sin OPERATOR. Parámetros, provisión/recuperación y fixture de cuenta se fijan en M0 antes de ejecutar | RF40/41; RB21/26; RNF08/14 |

AT03 comprueba concurrencia dentro de la autoridad local. AT31–AT34 comprueban propagación y carrera entre puestos conectados. AT30 tiene criterio definido para ambas políticas de D-A02; permanece diseñado, sin ejecución de software. AT01/10/28 prueban autonomía monopuesto o CONTINUE_LOCAL, y AT30 agrega la alternativa de suspensión. AT52–AT56 cubren credenciales, permisos y mantenimiento local (§7.4). Ambas políticas de AT30 son must para la final. Diagnóstico ampliado y escalas 125/150 % conservan la prioridad should del plan.

## 9. Requisitos no funcionales y evidencia prevista

Las cifras son objetivos propuestos, sujetos a ambiente y volumen declarados; no son métricas obtenidas ni SLA.

| ID | Objetivo propuesto | Validación prevista |
| --- | --- | --- |
| RNF01 | Cero duplicados por retry de intención/evento | Invariantes SQLite y deduplicación de lote; AT03/05/25/27 |
| RNF02 | P95 autorización local menor de 1 s en equipo declarado | Medir dominio/persistencia aparte de captura, matching e impresión |
| RNF03 | Respuesta visual al toque menor de 150 ms | Instrumentación y equipo táctil; sin bloqueo UI |
| RNF04 | Recuperar proceso/cola en menos de 2 min | Caída en cada transición y arranque sin red |
| RNF05 | Ningún evento confirmado perdido ante caída del proceso | Reinicio y backup/restauración; pérdida física de disco se evalúa aparte |
| RNF06 | Capacidad inicial para siete días del volumen acordado, sin borrar pendientes | Dimensionamiento, alarma y prueba de espacio bajo; no es máximo permitido de desconexión |
| RNF07 | Propagar actualización dentro de 2 min con conectividad y sincronización disponibles | Medición desde publicación; objetivo sujeto al protocolo elegido |
| RNF08 | Permisos por equipo/rol/objeto y biometría ausente de trazas | Acceso cruzado y AT13/24; protección de base/template definida aparte |
| RNF09 | Reglas comprobables sin SDK/UI; contratos versionados | Unitarias y dobles de adaptadores; hardware por separado |
| RNF10 | Instalación con paquete inicial y recuperación compatible | Windows limpio, provisión local y actualización con pendientes |
| RNF11 | Trazabilidad intención → decisión → consumo → ticket → evento → lote | Caso de soporte reproducible con IDs técnicos |
| RNF12 | Táctil/teclado y alternativa a huella | Foco, contraste, mensajes, timeout y ergonomía real |
| RNF13 | Auditoría durable, correlacionada y recuperable en dos archivos externos mensuales; no pérdida por caída de proceso de registros confirmados en SQLite | Fallos reales de archivo/proceso, reconstrucción AT41–44; pérdida física requiere backup y se evalúa aparte |
| RNF14 | Diagnóstico con permisos y aislamiento de periféricos/sesión, sin modificar efectos económicos | AT37–39, inventario y simulación rotulada; pruebas físicas posteriores |
| RNF15 | Coherencia con el ecosistema INWeb mediante el [mapa lógico/físico/C#](nomenclatura-integracion.md), identidades tipadas y DTO explícitos que preservan el contrato JSON | AT51; revisión del diccionario y mappings en M0; pruebas de persistencia, identidad y serialización durante implementación |

El plan combina unitarias de dominio, integración SQLite, contrato de sincronización, E2E del flujo autónomo y ensayos físicos posteriores. Los resultados reales se agregarán cuando exista implementación; no se presentan comandos de arranque o pruebas inexistentes.

## 10. Condiciones de implementación y alcance productivo

Las decisiones de alcance, desconexión y reloj están definidas en D-C16, D-A02 y D-C17. El [plan académico](plan-academico.md) ordena la preparación técnica, la vertical funcional y el MVP completo con entregables y criterios de salida. Credenciales y permisos locales viajan en el snapshot de `GET /configuration` (§7.4). Los contratos complementarios de provisión inicial y difusión de pares se especificarán en M0, antes de implementar sus componentes; no se presupone que estén resueltos por las tres operaciones OpenAPI seleccionadas.

La integración productiva requiere homologar dispositivos/SDK/templates, protocolo serie e impresora, protección, permisos, retención, recuperación y responsables. La operación autónoma presupone datos provisionados y almacenamiento operativo; una base inexistente o corrupta requiere recuperación.

El ciclo SDD es requisito → regla → escenario → historia/ticket → implementación → evidencia → revisión. Un cambio de alcance actualiza contratos y trazabilidad antes de construirlo. El estado de esta entrega es documental.
