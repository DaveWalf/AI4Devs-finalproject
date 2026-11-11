# comprobante-local Specification

## Purpose
Llevar el comprobante de un consumo ya confirmado hasta la impresora sin tocar el consumo (RB17), dejar un estado durable que distinga lo enviado de lo incierto, y permitir que un operador con permiso `OPERATOR` lo recupere o lo reimprima como copia auditada (RB08, RB26).

## Requirements

### Requirement: Envío del comprobante después del commit

Después de una autorización, el puesto SHALL enviar a la impresora el trabajo original, fuera de la transacción de RB06. Antes de enviarlo MUST dejarlo en `UNKNOWN` de forma durable y, con la respuesta, pasarlo a `SUBMITTED` o `FAILED`. Cada cambio de estado MUST quedar en la bitácora. Un fallo MUST NOT tocar el consumo, el cupo ni el comprobante (RB05, RB17).

#### Scenario: Impresora disponible
Origen: AT01 («ticket lógico asociado») y RB17; el nombre, por la mejora 9b de David del 10/11/2026.
- **GIVEN** una autorización recién confirmada y la impresora disponible
- **WHEN** el puesto envía el trabajo original
- **THEN** el trabajo de copia 0 queda en `SUBMITTED`, la impresora recibió el comprobante sin la leyenda REIMPRESIÓN, con el nombre del comensal y sin ningún código de identificación
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

Con la sesión de asistencia, el operador SHALL poder recuperar desde la fila de Consumos un trabajo original en `FAILED`, que se vuelve a enviar como el mismo trabajo, o reimprimir el comprobante con un motivo de una lista fija. Reimprimir crea un trabajo con el número de copia siguiente, la leyenda REIMPRESIÓN y el nombre del operador que la habilitó. Cada acción MUST comprobar de nuevo el permiso y MUST NOT cambiar consumo, cupo ni importe (RB08).

#### Scenario: Reimpresión con motivo
Origen: AT54 («crea copia numerada … sin cambio de consumo»), RB08 y la mejora 9b de David del 10/11/2026 («REIMPRESIÓN o el operador que habilitó»).
- **GIVEN** un comprobante cuyo trabajo original está en `SUBMITTED` y un operador con permiso vigente
- **WHEN** lo reimprime desde su fila con el motivo «Papel ilegible»
- **THEN** existe un trabajo nuevo de copia 1 para el mismo comprobante y la impresora recibió el comprobante con la leyenda REIMPRESIÓN, el número de copia y el nombre del operador
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
- **THEN** el mismo trabajo de copia 0 se envía otra vez, sin leyenda ni operador, y queda en `SUBMITTED`
- **AND** no se crea ningún trabajo nuevo y la bitácora registra la recuperación

#### Scenario: Resultado incierto
Origen: HU03-B («aviso de incertidumbre, número de copia y leyenda DUPLICADO»), con la leyenda de la mejora 9b.
- **GIVEN** un comprobante cuyo último trabajo está en `UNKNOWN`
- **WHEN** el operador elige reimprimirlo
- **THEN** la pantalla avisa que el papel pudo haber salido antes de pedir el motivo
- **AND** al confirmar, la copia nueva lleva su número y la leyenda REIMPRESIÓN

#### Scenario: El permiso vence durante la sesión
Origen: RB26 («comprobar el permiso en cada acción») y AT54 («siguiente acción rechazada»).
- **GIVEN** un operador que abrió la sesión de asistencia con un permiso cuyo `ValidToUtc` llega antes de su acción
- **WHEN** intenta reimprimir
- **THEN** la acción se deniega con `PERMISSION_EXPIRED`, sin trabajo nuevo, y Consumos termina la sesión y vuelve a pedir un código con el aviso de ese motivo

### Requirement: Comprobante en pantalla con la impresora simulada

Con la impresora simulada, cuando una confirmación se autoriza y el comprobante sale, el resultado SHALL mostrar una impresora de la que sale el comprobante, con los datos que la impresora simulada escribe en su archivo, el nombre del comensal incluido, y sin el código. Tocar el comprobante MUST equivaler a «Listo». Si el comprobante no salió, MUST NOT mostrarse ninguno. Con movimiento reducido, MUST aparecer sin animación.

#### Scenario: Autorización con la impresora disponible
Origen: decisión de David del 03/11/2026 (impresora simulada, pasos 1 a 4), AT06 y la mejora 9b del 10/11/2026 (el nombre del comensal).
- **GIVEN** la impresora simulada en «Disponible» y una persona con una opción elegida
- **WHEN** confirma y la confirmación se autoriza
- **THEN** la pantalla muestra «Consumo registrado. Retirá tu comprobante», la impresora y el comprobante con la opción, el cargo y el nombre de la persona, sin su código
- **AND** al tocar el comprobante el puesto vuelve a reposo sin el nombre ni la elección

#### Scenario: Sin papel no sale comprobante
Origen: AT06 («consumo preservado, trabajo recuperable») y «Sin papel» de `docs/experiencia.md`.
- **GIVEN** la impresora simulada en «Sin papel»
- **WHEN** una confirmación se autoriza
- **THEN** la pantalla muestra «Tu consumo está registrado. El comprobante queda pendiente» y ningún comprobante

#### Scenario: Un rechazo no muestra la impresora
Origen: AT02 visto desde el puesto: un rechazo no crea comprobante.
- **GIVEN** una persona que ya usó su cupo de ALMUERZO
- **WHEN** confirma otra opción de ALMUERZO
- **THEN** la pantalla muestra el mensaje de `LOCAL_LIMIT_REACHED` y no muestra la impresora

#### Scenario: Diez segundos sin tocar el comprobante
Origen: TK02.1, trabajo incluido 1 («resultado y regreso a reposo»), y HU03-E.
- **GIVEN** el comprobante en pantalla
- **WHEN** pasan 10 s sin que nadie lo toque
- **THEN** el puesto vuelve a reposo sin el nombre ni la elección

#### Scenario: Movimiento reducido
Origen: decisión de David del 03/11/2026 (UX táctil, criterio 6: «alternativa sin movimiento cuando el dispositivo la solicite»).
- **GIVEN** el sistema pide movimiento reducido
- **WHEN** sale el comprobante
- **THEN** aparece completo desde el primer cuadro, sin animación

#### Scenario: Con otra impresora queda «Listo»
Origen: D11 de `atender-seleccion-y-confirmacion` (la simulación es sólo de la demostración).
- **GIVEN** un puesto cuya impresora no es la simulada
- **WHEN** una confirmación se autoriza
- **THEN** el resultado muestra el mensaje y el botón «Listo», sin impresora en pantalla

### Requirement: Modo de la impresora simulada desde Configuración

Con la impresora simulada, la pestaña «Impresora» de Configuración SHALL ofrecer la sección «Impresora simulada» con los modos «Disponible», «Sin papel» e «Incierta»; el modo elegido MUST aplicar desde el próximo envío. Las pantallas de atención MUST NOT mostrar esos modos. Con una impresora que no es la simulada, la sección MUST NOT aparecer.

#### Scenario: Elegir «Sin papel» desde Configuración
Origen: AT06, con el modo elegido fuera de la pantalla de atención.
- **GIVEN** la impresora simulada en «Disponible»
- **WHEN** en la pestaña «Impresora» se elige «Sin papel», se vuelve a reposo y una persona confirma
- **THEN** el consumo queda registrado y el comprobante, pendiente

#### Scenario: Con otra impresora no hay sección
Origen: D11 de `atender-seleccion-y-confirmacion` (la simulación es sólo de la demostración).
- **GIVEN** un puesto cuya impresora no es la simulada
- **WHEN** se abre Configuración
- **THEN** no hay sección «Impresora simulada»

### Requirement: Prueba de impresión sin consumo

La pestaña «Impresora» SHALL ofrecer «Imprimir una prueba», que envía a la impresora del puesto un comprobante con la leyenda PRUEBA, el puesto y una frase, sin persona, código, número ni importe, y dice si salió, si falló o si quedó incierto. La prueba MUST NOT crear intención, consumo, comprobante, trabajo de impresión ni evento de salida, MUST NOT mostrarse como el comprobante de la próxima atención y MUST quedar en la bitácora general con su resultado.

#### Scenario: Prueba con la impresora disponible
Origen: decisión de David del 03/11/2026 («realizar todas las operaciones que sean convenientes en cuanto a su prueba y puesta en funcionamiento»), RF41 y AT38.
- **GIVEN** la impresora simulada en «Disponible»
- **WHEN** se toca «Imprimir una prueba»
- **THEN** la impresora recibió un comprobante con la leyenda PRUEBA y la pestaña dice que salió
- **AND** la bitácora general tiene la prueba con su resultado y el actor nulo con `NO_AUTHENTICATION`

#### Scenario: La prueba con el puesto y una frase
Origen: mejora 9b de David del 10/11/2026 y D6 de este change: la prueba muestra cómo sale el comprobante.
- **GIVEN** el puesto 3 «Comedor Norte» guardado en Impresora
- **WHEN** se toca «Imprimir una prueba»
- **THEN** la prueba lleva «Puesto 3 · Comedor Norte» y una de las 50 frases, sin comensal ni número

#### Scenario: Prueba sin papel
Origen: AT38 («errores inyectados») y AT06, borde.
- **GIVEN** la impresora simulada en «Sin papel»
- **WHEN** se toca «Imprimir una prueba»
- **THEN** la pestaña dice que la impresora no tiene papel

#### Scenario: La prueba no toca la base
Origen: AT38 («sin consumo») y RNF14 («sin modificar efectos económicos»), borde.
- **GIVEN** la base con sus intenciones, consumos, comprobantes, trabajos y eventos de salida
- **WHEN** se imprime una prueba
- **THEN** ninguna de esas tablas tiene una fila nueva

#### Scenario: La prueba no es el comprobante de la próxima atención
Origen: D7 de `presentar-atencion-tactil` (el comprobante en pantalla es el del consumo), borde.
- **GIVEN** una prueba impresa con la impresora simulada
- **WHEN** después una persona confirma con «Sin papel»
- **THEN** la pantalla dice que el comprobante queda pendiente y no muestra el comprobante de la prueba

### Requirement: Textos del comprobante

La pestaña «Impresora» SHALL permitir cambiar el encabezado del comprobante, de 1 a 40 caracteres, y su pie, de 0 a 60, sin caracteres de control, y conservarlos en la carpeta de datos. MUST aplicarse desde el próximo comprobante que salga, en la impresora y en pantalla, sin cambiar los datos guardados de ningún comprobante. Sin textos guardados, o con un archivo ilegible o fuera de forma, MUST usarse «INComedor · comprobante» y ningún pie.

#### Scenario: Cambiar el encabezado y el pie
Origen: decisión de David del 03/11/2026 («adaptar las características del ticket y los textos que sean configurables»).
- **GIVEN** la impresora simulada y los textos por defecto
- **WHEN** se guarda el encabezado «Comedor de planta» y el pie «Gracias por tu visita», y una persona confirma
- **THEN** el comprobante en pantalla y el de la impresora empiezan con «Comedor de planta» y terminan con «Gracias por tu visita»

#### Scenario: Un texto fuera de forma
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** la pestaña «Impresora»
- **WHEN** se intenta guardar un encabezado vacío y, después, un pie con un salto de línea
- **THEN** los dos se rechazan con su campo señalado y los textos vigentes no cambian

#### Scenario: Una copia lleva los textos vigentes
Origen: RB08 («reimprimir no modifica consumo/cupo/importe»), borde: los textos son de la impresora, no del comprobante guardado.
- **GIVEN** un comprobante impreso con los textos por defecto y, después, un encabezado nuevo guardado
- **WHEN** un operador lo reimprime
- **THEN** la copia lleva el encabezado nuevo, la leyenda REIMPRESIÓN y los mismos datos del comprobante

#### Scenario: Un archivo de textos ilegible
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** un archivo de textos con un encabezado de 300 caracteres
- **WHEN** el puesto arranca y una persona confirma
- **THEN** el comprobante empieza con «INComedor · comprobante» y no tiene pie

### Requirement: Ver el comprobante sin imprimirlo

«Ver» en una fila de Consumos SHALL mostrar el comprobante en pantalla, con los datos del comprobante que lleva el impreso (no los de la impresora: encabezado, puesto y pie) y la marca «COPIA EN PANTALLA», sin pedir un operador. MUST NOT crear un trabajo de impresión ni una copia, ni cambiar consumo, cupo o importe, y MUST NOT mostrar el código de identificación.

#### Scenario: Ver un comprobante
Origen: mejora 6 de David del 10/11/2026 («con reimpresión virtual o real»).
- **GIVEN** un consumo en la tabla de Consumos, con su comprobante impreso
- **WHEN** se toca «Ver» en su fila
- **THEN** la pantalla muestra el comprobante con su número, el comensal, la opción, la fecha de negocio, el cargo y la marca «COPIA EN PANTALLA»
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

### Requirement: El contenido del comprobante

El comprobante SHALL llevar el nombre visible del comensal tomado al confirmar, el número y el nombre del puesto si están guardados, su número y, al pie antes del texto configurable, una de 50 frases cálidas elegida por el comprobante. Una copia MUST repetir el nombre, el número y la frase del original. MUST NOT llevar el código de identificación, en papel ni en pantalla.

#### Scenario: Un comprobante con el puesto y la frase
Origen: mejora 9b de David del 10/11/2026 («nombre del comensal, una de 50 frases cálidas al pie, número y nombre del puesto»).
- **GIVEN** el puesto 3 «Comedor Norte» guardado en Impresora
- **WHEN** una persona confirma y sale el comprobante
- **THEN** el comprobante en papel y en pantalla dice «Puesto 3 · Comedor Norte», el nombre de la persona y una de las 50 frases, y no su código

#### Scenario: La copia repite al comensal y la frase
Origen: RB08 («reimprimir no modifica consumo/cupo/importe»), borde: el comprobante es lo que fue.
- **GIVEN** un comprobante impreso y, después, la persona con otro nombre
- **WHEN** un operador lo reimprime
- **THEN** la copia lleva el nombre y la frase del original

#### Scenario: Un comprobante de antes
Origen: D1 de este change, borde: los comprobantes guardados no cambian.
- **GIVEN** un comprobante guardado antes de este cambio, sin nombre en sus datos
- **WHEN** se lo reimprime o se lo ve en pantalla
- **THEN** sale sin línea de comensal y con los ocho caracteres de siempre como número

### Requirement: La numeración del comprobante

Impresora SHALL ofrecer dos numeraciones: «El código de siempre», los primeros ocho caracteres del identificador del comprobante, o «Correlativo desde» un número de 1 a 999.999.999. Con correlativo, cada confirmación MUST tomar, en su transacción, el mayor número ya emitido más uno, nunca menor que el «desde», y guardarlo con el comprobante. Un número MUST NOT repetirse. Consumos MUST mostrar el número y encontrarlo al buscar.

#### Scenario: Correlativo desde mil
Origen: mejora 9b de David del 10/11/2026 («código de comprobante actual o correlativo desde un número»).
- **GIVEN** la numeración «Correlativo desde» 1000 y ningún comprobante correlativo
- **WHEN** se confirman dos consumos
- **THEN** sus comprobantes son el 1000 y el 1001, y Consumos encuentra el 1001 al buscarlo

#### Scenario: Bajar el «desde» no reusa números
Origen: RB06 («todo o nada») y D2 de este change, borde.
- **GIVEN** comprobantes correlativos hasta el 1001 y, después, el «desde» cambiado a 5
- **WHEN** se confirma otro consumo
- **THEN** su comprobante es el 1002

#### Scenario: El código de siempre
Origen: mejora 9b de David del 10/11/2026 («explicar el algoritmo actual del código»).
- **GIVEN** la numeración «El código de siempre»
- **WHEN** se confirma un consumo
- **THEN** su número son los primeros ocho caracteres de su identificador, como hasta hoy

### Requirement: Los datos del comprobante en Impresora

La pestaña Impresora SHALL tener «Datos del comprobante»: el número del puesto (1 a 999, opcional), su nombre (hasta 30 caracteres, sin caracteres de control, opcional) y la numeración, con su «Guardar», en la carpeta de datos del equipo. Lo guardado MUST valer desde el próximo comprobante; lo que no tiene forma MUST rechazarse con su campo señalado. Cada guardado MUST quedar como `CONFIGURATION_CHANGED` de `TICKET_DATA`.

#### Scenario: Guardar los datos del comprobante
Origen: mejora 9b de David del 10/11/2026 («en Impresora»).
- **GIVEN** la pestaña Impresora
- **WHEN** se guarda el puesto 3 «Comedor Norte» con «Correlativo desde» 1000
- **THEN** la pestaña avisa lo guardado y la general tiene `CONFIGURATION_CHANGED` de `TICKET_DATA` con el antes y el después

#### Scenario: Datos fuera de forma
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** la pestaña Impresora
- **WHEN** se intenta guardar el puesto 0, un nombre con un salto de línea o un «desde» de 0
- **THEN** cada campo se señala y lo guardado no cambia

#### Scenario: Un archivo de datos ilegible
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** un archivo de datos del puesto con un número de puesto de 5000
- **WHEN** una persona confirma
- **THEN** el comprobante sale sin puesto y con el código de siempre
