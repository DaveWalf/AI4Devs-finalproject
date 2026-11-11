## ADDED Requirements

### Requirement: Las líneas que imprime el comprobante

La pestaña Impresora SHALL dejar elegir «Imprimir» o «No imprimir» para el nombre del comensal, el aviso de reimpresión y el operador que habilitó una copia; las tres empiezan en «Imprimir», se guardan con los textos y quedan en `TICKET_TEXTS`. MUST valer desde el próximo comprobante, en el papel y en el que sale en pantalla al confirmar. La marca «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD» MUST NOT depender de ellas. Un archivo sin las tres MUST imprimirlas.

#### Scenario: Elegir qué imprime
Origen: mejora 4 de David del 11/11/2026 y su respuesta 1 al gate («Impresora, encendidas»).
- **GIVEN** la pestaña Impresora con las tres elecciones en «Imprimir»
- **WHEN** se elige «No imprimir» en «Nombre del comensal» y se guarda
- **THEN** la pestaña avisa lo guardado, la general tiene `CONFIGURATION_CHANGED` de `TICKET_TEXTS` con las elecciones antes y después, y al volver a abrir Configuración la elección sigue

#### Scenario: Un archivo de textos de antes
Origen: D1 de `enriquecer-comprobante` (lo guardado antes no cambia) y `AGENTS.md` (la configuración que llega al puesto), borde.
- **GIVEN** un `impresora.json` con encabezado y pie, sin las tres elecciones
- **WHEN** el puesto arranca y una persona confirma
- **THEN** el comprobante lleva el nombre del comensal, y la pestaña Impresora muestra las tres en «Imprimir»

#### Scenario: Una elección fuera de forma
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** un `impresora.json` con «comensal» igual al texto «no»
- **WHEN** el puesto arranca
- **THEN** usa los textos y las elecciones por defecto, como con un archivo ilegible

## MODIFIED Requirements

### Requirement: El contenido del comprobante

El comprobante SHALL llevar el nombre visible del comensal tomado al confirmar, si Impresora lo imprime, el número y el nombre del puesto si están guardados, su número y, al pie antes del texto configurable, una de 50 frases cálidas elegida por el comprobante. Una copia MUST repetir el número y la frase del original, y el nombre si se imprime. MUST NOT llevar el código de identificación, en papel ni en pantalla.

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

#### Scenario: Un comprobante sin el comensal
Origen: mejora 4 de David del 11/11/2026 («la opción de imprimir o no el nombre del comensal») y su respuesta 4 al gate, borde: «Ver» lo sigue mostrando.
- **GIVEN** «Nombre del comensal» en «No imprimir» en Impresora
- **WHEN** «Persona sintética 01» confirma, sale el comprobante y, después, se lo ve con «Ver» en Consumos
- **THEN** el papel y el comprobante en pantalla no tienen línea de comensal, y «Ver» muestra «Persona sintética 01»

### Requirement: Recuperar o reimprimir sin tocar el consumo

Desde la fila de Consumos SHALL poder recuperarse un trabajo original en `FAILED`, que se vuelve a enviar como el mismo trabajo, o reimprimirse el comprobante con un motivo de una lista fija. Reimprimir crea un trabajo con la copia siguiente, la leyenda REIMPRESIÓN y «Habilitó:» con el operador, si Impresora los imprime, o, con la clave del día, siempre «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD». Las acciones MUST NOT cambiar consumo, cupo ni importe (RB08) y MUST quedar en la bitácora con su actor.

#### Scenario: Reimpresión con motivo
Origen: AT54 («crea copia numerada … sin cambio de consumo»), RB08 y la mejora 9b de David del 10/11/2026 («REIMPRESIÓN o el operador que habilitó»).
- **GIVEN** un comprobante cuyo trabajo original está en `SUBMITTED`, y Configuración abierta con el código de un operador con permiso vigente
- **WHEN** lo reimprime desde su fila con el motivo «Papel ilegible»
- **THEN** existe un trabajo nuevo de copia 1 para el mismo comprobante y la impresora recibió el comprobante con la leyenda REIMPRESIÓN, el número de copia y «Habilitó:» con el nombre del operador
- **AND** el consumo, el uso de cupo y el importe son iguales antes y después, y la bitácora registra la acción, el motivo, el comprobante, la copia, el `PersonId` y el `GrantId`

#### Scenario: Reimpresión con la clave del día
Origen: AT54 y RB08, con la respuesta 1 de David al gate del 11/11/2026 («Si no hay identificación debe indicar claramente: "SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD"»).
- **GIVEN** un comprobante cuyo trabajo original está en `SUBMITTED`, y Configuración abierta con la clave del día
- **WHEN** se lo reimprime desde su fila con el motivo «Papel ilegible»
- **THEN** existe un trabajo nuevo de copia 1 y la impresora recibió el comprobante con la leyenda REIMPRESIÓN, el número de copia y la línea «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD», sin «Habilitó:»
- **AND** el consumo, el uso de cupo y el importe son iguales antes y después, y `TICKET_REPRINTED` tiene el motivo, el comprobante, la copia, el actor nulo con `NO_AUTHENTICATION` y `Access` `DAILY_KEY`, sin `PersonId` ni `GrantId`

#### Scenario: El aviso de una copia sin validez
Origen: respuesta 1 de David al gate del 11/11/2026 («debe indicar claramente») y AT54, borde: se avisa antes de imprimir.
- **GIVEN** Configuración abierta con la clave del día
- **WHEN** se toca «Reimprimir» en una fila de Consumos
- **THEN** junto a los motivos, la pestaña avisa que la copia sale marcada «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD» porque se entró con la clave del día, y la impresora todavía no recibió nada

#### Scenario: Segunda reimpresión
Origen: RB08 («copia posterior … queda marcada y auditada») y la unicidad `(TicketId, CopyNumber)`.
- **GIVEN** un comprobante que ya tiene la copia 1
- **WHEN** se lo reimprime otra vez
- **THEN** el trabajo nuevo es la copia 2

#### Scenario: Trabajo fallido por papel
Origen: HU03-A («conserva consumo/ticket; acción auditada según estado del trabajo») y AT06.
- **GIVEN** un comprobante cuyo trabajo original está en `FAILED` y la impresora ya con papel
- **WHEN** se lo recupera desde su fila
- **THEN** el mismo trabajo de copia 0 se envía otra vez, sin leyenda ni operador, y queda en `SUBMITTED`
- **AND** no se crea ningún trabajo nuevo y la bitácora registra la recuperación

#### Scenario: Recuperar con la clave del día
Origen: HU03-A y AT06, con la respuesta 2 de David al gate del 11/11/2026 («Recuperar» tampoco pide el código), borde: el original no lleva la marca.
- **GIVEN** Configuración abierta con la clave del día, un comprobante cuyo trabajo original está en `FAILED` y la impresora ya con papel
- **WHEN** se toca «Recuperar» en su fila
- **THEN** sin pedir un código, el mismo trabajo de copia 0 se envía otra vez sin leyenda, sin «Habilitó:» y sin «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD», y queda en `SUBMITTED`
- **AND** `TICKET_RECOVERED` tiene el actor nulo con `NO_AUTHENTICATION` y `Access` `DAILY_KEY`

#### Scenario: Resultado incierto
Origen: HU03-B («aviso de incertidumbre, número de copia y leyenda DUPLICADO»), con la leyenda de la mejora 9b.
- **GIVEN** un comprobante cuyo último trabajo está en `UNKNOWN`
- **WHEN** se elige reimprimirlo
- **THEN** la pantalla avisa que el papel pudo haber salido antes de pedir el motivo
- **AND** al confirmar, la copia nueva lleva su número y la leyenda REIMPRESIÓN

#### Scenario: El permiso vence durante la sesión
Origen: RB26 («comprobar el permiso en cada acción») y AT54 («siguiente acción rechazada»), borde.
- **GIVEN** Configuración abierta con el código de un operador cuyo permiso tiene un `ValidToUtc` que llega antes de su acción
- **WHEN** intenta reimprimir
- **THEN** la acción se deniega con `PERMISSION_EXPIRED`, sin trabajo nuevo, y la pestaña muestra el aviso de ese motivo sin pedir un código
- **AND** la acción siguiente desde Consumos también se deniega, sin trabajo nuevo

## REMOVED Requirements

#### Scenario: Una copia sin aviso ni operador
Origen: mejora 4 de David del 11/11/2026 («si se imprimirá un aviso visual en caso de tratarse de la reimpresión del ticket, y qué operador lo hizo») y su respuesta 2 al gate, con RB08: la copia sigue numerada.
- **GIVEN** «Aviso de reimpresión» y «Operador que habilitó» en «No imprimir», y Configuración abierta con el código de un operador con permiso vigente
- **WHEN** reimprime un comprobante con el motivo «Papel ilegible»
- **THEN** la copia dice «Copia: 1» y no lleva REIMPRESIÓN, «ORIGINAL» ni «Habilitó:»

#### Scenario: La marca de la clave del día no se apaga
Origen: respuesta 3 de David al gate del 11/11/2026 («un técnico que esté configurando […] no debe poder generar tickets válidos»), borde.
- **GIVEN** «Aviso de reimpresión» y «Operador que habilitó» en «No imprimir», y Configuración abierta con la clave del día
- **WHEN** se reimprime un comprobante
- **THEN** la copia lleva «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD» y «Copia: 1»
