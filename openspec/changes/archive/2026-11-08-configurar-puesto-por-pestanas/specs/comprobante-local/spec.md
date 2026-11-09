# Spec Delta

## MODIFIED Requirements

### Requirement: Asistencia con permiso OPERATOR

La pestaña «Comprobantes» de Configuración SHALL identificar al actor por su código, como la atención, y abrir una sesión de acción privilegiada sólo si tiene un permiso `OPERATOR` no revocado y vigente en `[ValidFromUtc, ValidToUtc)` en el snapshot activo (RB26). Si no, MUST denegar sin abrir sesión. Cada intento MUST auditarse con `RequestedRole` y su causa; `PersonId`, `GrantId` y `GrantedRole`, sólo si existen. La pantalla de reposo MUST NOT ofrecer «Asistencia».

#### Scenario: Operador con permiso vigente
Origen: AT54 («sólo el operador vigente crea copia numerada»).
- **GIVEN** una persona con un permiso `OPERATOR` vigente y sin revocar
- **WHEN** tipea su código en la pestaña «Comprobantes»
- **THEN** existe una sesión de identidad de propósito `PRIVILEGED_ACTION` y la pestaña muestra los comprobantes de la fecha de negocio
- **AND** la bitácora registra el acceso con `RequestedRole` `OPERATOR`, su `PersonId`, `GrantId` y `GrantedRole`

#### Scenario: Persona sin permiso
Origen: HU03-C («denegación sin generar trabajo ni alterar consumo»).
- **GIVEN** una persona sin ningún permiso de operador
- **WHEN** tipea su código en la pestaña «Comprobantes»
- **THEN** la pestaña informa que no tiene permiso y sigue pidiendo un código, sin sesión nueva
- **AND** la bitácora registra la denegación con `PERMISSION_MISSING` y su `PersonId`, sin `GrantId`

#### Scenario: Permiso vencido
Origen: HU03-I y AT54 («operador … vencido»).
- **GIVEN** una persona cuyo permiso `OPERATOR` tiene `ValidToUtc` igual al instante del intento
- **WHEN** tipea su código en la pestaña «Comprobantes»
- **THEN** se deniega con `PERMISSION_EXPIRED`, sin sesión, y la bitácora registra su `GrantId` y `GrantedRole`

#### Scenario: Permiso revocado
Origen: HU03-I y AT54 («operador … revocado»).
- **GIVEN** una persona cuyo permiso `OPERATOR` tiene fecha de revocación
- **WHEN** tipea su código en la pestaña «Comprobantes»
- **THEN** se deniega con `PERMISSION_REVOKED`, sin sesión

#### Scenario: Administrador sin OPERATOR
Origen: AT54 («administrador sin OPERATOR») y RB26 («roles sin herencia»).
- **GIVEN** una persona con un permiso `SYSTEM_ADMINISTRATOR` vigente y ningún permiso `OPERATOR`
- **WHEN** tipea su código en la pestaña «Comprobantes»
- **THEN** se deniega con `PERMISSION_MISSING`, sin sesión

#### Scenario: Autenticación fallida
Origen: AT54 («también autenticación fallida») y RB26 («PersonId sólo si se resolvió el actor»).
- **GIVEN** un código desconocido
- **WHEN** se lo tipea en la pestaña «Comprobantes»
- **THEN** se deniega con `IDENTITY_NOT_RESOLVED`, sin sesión, y la bitácora registra `RequestedRole` `OPERATOR` sin `PersonId` y sin el código

#### Scenario: La sesión de asistencia no habilita consumo
Origen: RB26 («La sesión privilegiada no habilita consumo y se cierra al terminar o expirar»).
- **GIVEN** un operador con la sesión de asistencia abierta
- **WHEN** se intenta confirmar una intención con esa sesión, y después el operador toca «Salir»
- **THEN** la confirmación responde que la sesión no es utilizable y no escribe nada
- **AND** al salir, la pestaña vuelve a pedir un código, sin privilegios ni el nombre del operador, y la bitácora registra el cierre de la sesión

#### Scenario: Reposo sin «Asistencia»
Origen: pedido de David del 06/11/2026 («1. Eliminar el botón de Asistencia») y su decisión 2/3 del mismo día.
- **GIVEN** el puesto en reposo
- **WHEN** se revisa la pantalla
- **THEN** tiene el campo del código e «Ingresar», y ningún botón «Asistencia»

#### Scenario: Cerrar Configuración termina la sesión
Origen: HU03-E («vuelve a reposo sin privilegios ni identidad de la persona anterior»), borde.
- **GIVEN** un operador con la sesión abierta en la pestaña «Comprobantes»
- **WHEN** se cierra Configuración con «Cancelar»
- **THEN** la sesión ya no es utilizable, y al volver a abrir Configuración la pestaña pide un código

#### Scenario: Treinta segundos sin tocar la pestaña
Origen: RB26 («se cierra al terminar o expirar») y HU03-E («cierra o expira sesión»), borde.
- **GIVEN** un operador con la sesión abierta cuyo último toque fue hace exactamente 30 s
- **WHEN** el puesto revisa la inactividad
- **THEN** la sesión termina y la pestaña vuelve a pedir un código, sin el nombre del operador

### Requirement: Recuperar o reimprimir sin tocar el consumo

Con la sesión de asistencia, el operador SHALL poder recuperar un trabajo original en `FAILED`, que se vuelve a enviar como el mismo trabajo, o reimprimir un comprobante con un motivo de una lista fija. Reimprimir crea un trabajo con el número de copia siguiente y la leyenda DUPLICADO. Cada acción MUST comprobar de nuevo el permiso y MUST NOT cambiar consumo, cupo ni importe (RB08).

#### Scenario: Reimpresión con motivo
Origen: AT54 («crea copia numerada … sin cambio de consumo») y RB08.
- **GIVEN** un comprobante cuyo trabajo original está en `SUBMITTED` y un operador con permiso vigente
- **WHEN** lo reimprime con el motivo «Papel ilegible»
- **THEN** existe un trabajo nuevo de copia 1 para el mismo comprobante y la impresora recibió el comprobante con la leyenda DUPLICADO y el número de copia
- **AND** el consumo, el uso de cupo y el importe son iguales antes y después, y la bitácora registra la acción, el motivo, el comprobante, la copia, el `PersonId` y el `GrantId`

#### Scenario: Segunda reimpresión
Origen: RB08 («copia posterior … queda marcada y auditada») y la unicidad `(TicketId, CopyNumber)`.
- **GIVEN** un comprobante que ya tiene la copia 1
- **WHEN** el operador lo reimprime otra vez
- **THEN** el trabajo nuevo es la copia 2

#### Scenario: Trabajo fallido por papel
Origen: HU03-A («conserva consumo/ticket; acción auditada según estado del trabajo») y AT06.
- **GIVEN** un comprobante cuyo trabajo original está en `FAILED` y la impresora ya con papel
- **WHEN** el operador lo recupera
- **THEN** el mismo trabajo de copia 0 se envía otra vez y queda en `SUBMITTED`
- **AND** no se crea ningún trabajo nuevo y la bitácora registra la recuperación

#### Scenario: Resultado incierto
Origen: HU03-B («aviso de incertidumbre, número de copia y leyenda DUPLICADO»).
- **GIVEN** un comprobante cuyo último trabajo está en `UNKNOWN`
- **WHEN** el operador elige reimprimirlo
- **THEN** la pantalla avisa que el papel pudo haber salido antes de pedir el motivo
- **AND** al confirmar, la copia nueva lleva su número y la leyenda DUPLICADO

#### Scenario: El permiso vence durante la sesión
Origen: RB26 («comprobar el permiso en cada acción») y AT54 («siguiente acción rechazada»).
- **GIVEN** un operador que abrió la sesión de asistencia con un permiso cuyo `ValidToUtc` llega antes de su acción
- **WHEN** intenta reimprimir
- **THEN** la acción se deniega con `PERMISSION_EXPIRED`, sin trabajo nuevo, y la pestaña termina la sesión y vuelve a pedir un código con el aviso de ese motivo

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

## ADDED Requirements

### Requirement: Prueba de impresión sin consumo

La pestaña «Impresora» SHALL ofrecer «Imprimir una prueba», que envía a la impresora del puesto un comprobante con la leyenda PRUEBA, sin persona, código ni importe, y dice si salió, si falló o si quedó incierto. La prueba MUST NOT crear intención, consumo, comprobante, trabajo de impresión ni evento de salida, MUST NOT mostrarse como el comprobante de la próxima atención y MUST quedar en la bitácora general con su resultado.

#### Scenario: Prueba con la impresora disponible
Origen: decisión de David del 03/11/2026 («realizar todas las operaciones que sean convenientes en cuanto a su prueba y puesta en funcionamiento»), RF41 y AT38.
- **GIVEN** la impresora simulada en «Disponible»
- **WHEN** se toca «Imprimir una prueba»
- **THEN** la impresora recibió un comprobante con la leyenda PRUEBA y la pestaña dice que salió
- **AND** la bitácora general tiene la prueba con su resultado y el actor nulo con `NO_AUTHENTICATION`

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
- **THEN** la copia lleva el encabezado nuevo, la leyenda DUPLICADO y los mismos datos del comprobante

#### Scenario: Un archivo de textos ilegible
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** un archivo de textos con un encabezado de 300 caracteres
- **WHEN** el puesto arranca y una persona confirma
- **THEN** el comprobante empieza con «INComedor · comprobante» y no tiene pie
