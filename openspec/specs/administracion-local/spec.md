# administracion-local Specification

## Purpose
Editar en el puesto, mientras no haya INWeb conectado, las personas habilitadas con sus códigos y cupos y las opciones del comedor con sus parámetros y franjas, con una versión local por guardado que conserva las anteriores, el bloqueo cuando la versión activa es de INWeb y cada edición en la bitácora.

## Requirements

### Requirement: Cada guardado es una versión local nueva

«Guardar» en «Personas habilitadas» o en «Opciones del comedor» SHALL crear una versión de configuración nueva y completa, con el número siguiente, y activarla en la misma transacción; las versiones anteriores MUST conservarse sin cambios. Un guardado con algún dato inválido MUST NOT crear versión ni cambiar la activa. «Cancelar» MUST cerrar el panel sin crear versión.

#### Scenario: Guardar un cambio crea la versión 2 y conserva la 1
Origen: decisión de David del 04/11/2026 (1/3: «cada guardado es una versión local nueva»), RF40 y AT37 («cambio válido versionado/auditado»).
- **GIVEN** la base de la demostración con la versión de configuración 1 activa
- **WHEN** en «Personas habilitadas» se cambia un cupo y se toca «Guardar»
- **THEN** la versión 2 está activa con el cambio, y la versión 1 sigue en la base con todas sus filas como estaban

#### Scenario: Un consumo anterior conserva su versión
Origen: AT09 («sin borrar consumos previos») y RF04 («sin alterar consumos históricos»).
- **GIVEN** un consumo de «Almuerzo común» registrado con la versión 1 y su importe de 1.500,00
- **WHEN** se guarda la opción con un importe de 1.800,00
- **THEN** el consumo sigue registrado con la versión 1, su opción de esa versión y su importe de 1.500,00

#### Scenario: Un guardado inválido no cambia nada
Origen: AT37 («conservación del valor previo ante error»).
- **GIVEN** la versión 1 activa y el panel de personas con una persona nueva sin nombre
- **WHEN** se toca «Guardar»
- **THEN** el panel señala el campo inválido, la versión activa sigue siendo la 1 y no existe ninguna versión 2

#### Scenario: Cancelar no crea versión
Origen: AT37, borde: una edición sin guardar no es una revisión.
- **GIVEN** el panel de opciones con un importe cambiado y sin guardar
- **WHEN** se toca «Cancelar»
- **THEN** el panel se cierra, la versión activa es la misma y el catálogo ofrecido no cambió

### Requirement: Con INWeb como dueño la edición se bloquea

Si la versión activa tiene origen INWeb, los paneles «Personas habilitadas» y «Opciones del comedor» SHALL mostrar sus datos sin controles de edición y con el aviso de que los administra INWeb, y todo guardado MUST denegarse sin crear versión. Una versión creada en el puesto MUST tener origen local. El estilo visual y el modo de la impresora MUST seguir editables.

#### Scenario: Versión activa de INWeb
Origen: decisión de David del 04/11/2026 (1/3: «al conectarse INWeb … esa edición se bloquea») y RB21.
- **GIVEN** una versión activa con origen INWeb
- **WHEN** se abre «Personas habilitadas»
- **THEN** el panel lista las personas, no tiene «Guardar» ni controles de edición y muestra «Estos datos los administra INWeb»

#### Scenario: Guardado pedido con INWeb activo
Origen: AT37 («reglas maestras no se editan»), borde: el bloqueo no depende sólo de la pantalla.
- **GIVEN** una versión activa con origen INWeb
- **WHEN** se pide guardar un cambio de cupo
- **THEN** el guardado se deniega, no existe ninguna versión nueva y la versión activa sigue siendo la de INWeb

#### Scenario: Versión local
Origen: decisión de David del 04/11/2026 (1/3: «mientras no haya INWeb conectado, Configuración edita»).
- **GIVEN** la base de la demostración, cuya versión no tiene origen registrado
- **WHEN** se guarda un cambio de cupo
- **THEN** el guardado se aplica y la versión nueva tiene origen local

### Requirement: Personas habilitadas con sus opciones y cupos

«Personas habilitadas» SHALL listar las personas de la versión activa con su nombre visible, si están activas o de baja, sus cupos por servicio y categoría con su límite, y las opciones del catálogo que esos cupos habilitan. MUST NOT mostrar ningún código de identificación.

#### Scenario: La persona del código 0001
Origen: pedido de David del 04/11/2026 («qué opciones y cupos tiene») y AT02.
- **GIVEN** la base de la demostración con el catálogo de nueve
- **WHEN** se abre «Personas habilitadas»
- **THEN** «Persona sintética 01» figura activa, con ALMUERZO límite 1 y REFRIGERIO límite 2, y con las nueve opciones habilitadas

#### Scenario: Ningún código en el panel
Origen: RB25 («no registrar el código en claro») y `AGENTS.md`.
- **GIVEN** la base de la demostración, con los códigos sintéticos de 0001 a 0004 y de 9001 a 9004
- **WHEN** se abre «Personas habilitadas» y se abre la edición de cada persona
- **THEN** el texto y los valores de los campos del panel no contienen ninguno de esos códigos

#### Scenario: Persona sin cupos
Origen: RB02 (`SERVICE_NOT_ENABLED`), borde.
- **GIVEN** una persona activa sin ninguna regla
- **WHEN** se abre «Personas habilitadas»
- **THEN** esa persona figura con «Sin cupos» y ninguna opción habilitada

### Requirement: Agregar una persona

Agregar una persona SHALL pedir su nombre visible, de 1 a 80 caracteres sin caracteres de control, su código, de 1 a 20 dígitos y conservado tal cual con sus ceros iniciales (RB01), y sus cupos. El código MUST NOT existir en ninguna versión de configuración, ni siquiera revocado ni de otra persona (RB25). Un dato inválido MUST rechazar el guardado entero.

#### Scenario: Alta y primera atención
Origen: pedido de David del 04/11/2026 («agregar … personas») y AT01.
- **GIVEN** la base de la demostración
- **WHEN** se agrega «Persona sintética 05» con el código `0105` y ALMUERZO límite 1, se guarda y, en reposo, se tipea `0105`
- **THEN** se abre la sesión de «Persona sintética 05» y se le ofrecen sólo las opciones de ALMUERZO

#### Scenario: Código ya usado, aunque esté revocado
Origen: RB25 y D-C20 («no se reasigna a otra persona, tampoco entre snapshots») y AT55 («reasignación entre personas»).
- **GIVEN** el código `0003`, revocado, de «Persona sintética 03»
- **WHEN** se agrega otra persona con el código `0003` y se toca «Guardar»
- **THEN** el panel dice que ese código ya existe, sin mostrar a quién pertenece, y no se crea ninguna versión

#### Scenario: Datos fuera de forma
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto).
- **GIVEN** el panel de personas
- **WHEN** se intenta guardar una persona con el código `12a4`, otra con un nombre de 81 caracteres y otra con un nombre que tiene un salto de línea
- **THEN** cada una se rechaza con su campo señalado y no se crea ninguna versión

#### Scenario: Ceros iniciales
Origen: RB01 («conservar ceros iniciales»).
- **GIVEN** una persona agregada con el código `0105`
- **WHEN** en reposo se tipea `105`
- **THEN** el código no se resuelve, y `0105` sí

### Requirement: Quitar una persona es darla de baja

Quitar una persona SHALL dejarla, en la versión nueva, inactiva y con su credencial revocada en el instante del guardado, sin borrar su fila, sus consumos ni sus versiones anteriores. Su código MUST NOT poder asignarse a otra persona.

#### Scenario: Baja de una persona con consumos
Origen: AT09 («aplica baja al activar nueva versión, sin borrar consumos previos») y pedido de David del 04/11/2026 («eliminar personas»).
- **GIVEN** `0001` con un consumo registrado en el día
- **WHEN** se la quita, se guarda y, en reposo, se tipea `0001`
- **THEN** la pantalla muestra el mensaje de `CREDENTIAL_REVOKED` y no abre sesión
- **AND** su consumo sigue registrado y el panel la muestra de baja

#### Scenario: Baja con una sesión abierta
Origen: AT53 («se activa un snapshot que revoca su credencial antes de confirmar»).
- **GIVEN** `0001` identificada, con una sesión abierta en la versión 1 y una opción elegida
- **WHEN** se activa una versión que la da de baja y, después, `0001` confirma
- **THEN** la decisión es DENIED con `PERSON_INACTIVE` y no existe ningún consumo nuevo

### Requirement: Modificar los cupos y el nombre de una persona

Cada cupo de una persona SHALL editarse como un límite diario por servicio y categoría, de 0 a 99, donde 0 es una denegación explícita (RB02). Guardar MUST dejar para ese par una sola regla con ese límite, prioridad 1, sin vigencia y con el ámbito del puesto; quitar el cupo MUST dejar al par sin regla. El nombre visible MUST validarse como al agregar.

#### Scenario: Subir el límite con un almuerzo ya usado
Origen: pedido de David del 04/11/2026 («modificar … cupos») y RB03 (el cupo se cuenta por persona y fecha de negocio).
- **GIVEN** `0001` con ALMUERZO límite 1 y un almuerzo ya consumido en la fecha de negocio
- **WHEN** se lleva ALMUERZO a 2, se guarda y `0001` se identifica
- **THEN** la oferta vuelve a incluir las opciones de ALMUERZO y no muestra «Hoy ya usaste:» para ALMUERZO

#### Scenario: Límite cero
Origen: RB02 («`Limit=0` es denegación explícita») y AT36.
- **GIVEN** `0001` con REFRIGERIO límite 2
- **WHEN** se lleva REFRIGERIO a 0, se guarda y `0001` se identifica
- **THEN** las opciones de REFRIGERIO quedan retenidas con `RULE_DENIED` y no se muestra «Hoy ya usaste:» para REFRIGERIO

#### Scenario: Bajar el límite por debajo de lo usado
Origen: AT09 («sin borrar consumos previos»), borde.
- **GIVEN** `0001` con REFRIGERIO límite 2 y dos refrigerios consumidos en el día
- **WHEN** se lleva REFRIGERIO a 1 y se guarda
- **THEN** los dos consumos siguen registrados y la oferta muestra «Hoy ya usaste: refrigerio 2 de 1»

#### Scenario: Límite fuera de rango
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto).
- **GIVEN** el panel de personas
- **WHEN** se intenta guardar un límite de -1 y, después, uno de 100
- **THEN** los dos se rechazan con el campo señalado y no se crea ninguna versión

### Requirement: Opciones del comedor con todos sus parámetros

«Opciones del comedor» SHALL listar y permitir agregar, quitar y modificar opciones: nombre de 1 a 60 caracteres sin caracteres de control, servicio entre los existentes, importe al empleado en pesos de 0 a 9.999.999,99 y al menos una carga con su categoría y de 1 a 9 unidades. Cambiar nombre, servicio, importe o cargas MUST crear una versión nueva de la opción; quitarla MUST sacarla del catálogo activo sin tocar sus consumos.

#### Scenario: Cambio de precio con la opción elegida
Origen: AT47 («informa nuevo valor y exige reconfirmación») y RB10.
- **GIVEN** una persona que eligió «Almuerzo común» con un cargo de 1.500,00
- **WHEN** se lleva su importe a 1.800,00, se guarda y la persona toca «Confirmar»
- **THEN** la opción tiene la versión de menú siguiente y la pantalla muestra 1.800,00 y pide reconfirmar

#### Scenario: Agregar una opción
Origen: pedido de David del 04/11/2026 («las opciones con todos sus parámetros») y RF04.
- **GIVEN** el catálogo de nueve
- **WHEN** se agrega «Fruta» del servicio REFRIGERIO, sin cargo, con una carga de REFRIGERIO, se guarda y `0001` se identifica
- **THEN** la oferta tiene diez opciones, «Fruta» entre ellas y sin importe

#### Scenario: Quitar una opción con consumos
Origen: AT47, borde («consumos anteriores conservan su snapshot monetario»).
- **GIVEN** un consumo de «Yogurt» registrado en el día
- **WHEN** se quita «Yogurt» y se guarda
- **THEN** «Yogurt» no se ofrece a nadie y el consumo sigue registrado con su opción y su importe

#### Scenario: Opción inválida
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto) y el CHECK de importe no negativo.
- **GIVEN** el panel de opciones
- **WHEN** se intenta guardar una opción con el importe -1, otra sin nombre y otra sin cargas
- **THEN** cada una se rechaza con su campo señalado y no se crea ninguna versión

### Requirement: Franjas horarias de una opción

Cada opción SHALL admitir cero o más franjas, cada una con hora de inicio incluida y hora de fin excluida, en horas y minutos de la hora local del puesto. Si el inicio es posterior al fin, la franja cruza la medianoche. Una franja con inicio igual al fin MUST rechazarse. Una opción sin franjas MUST estar disponible todo el día. Cambiar las franjas MUST NOT cambiar la versión de la opción.

#### Scenario: Cargar la franja del almuerzo
Origen: pedido de David del 04/11/2026 («franjas horarias en las cuales están habilitados») y el horario observado de almuerzo, de 11:00 a 14:00.
- **GIVEN** «Almuerzo común» sin franjas
- **WHEN** se le agrega la franja de 11:00 a 14:00 y se guarda
- **THEN** la opción figura con «11:00 a 14:00» y conserva su versión de menú

#### Scenario: Franja que cruza la medianoche
Origen: el turno de 14:00 a 23:00 de los requisitos confirmados del cliente, borde: una franja nocturna no se parte a mano.
- **GIVEN** una opción con la franja de 22:00 a 02:00
- **WHEN** una persona con cupo se identifica a la 01:30 y, otra vez, a las 02:00
- **THEN** a la 01:30 la opción se ofrece y a las 02:00 se retiene

#### Scenario: Franja vacía
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** el panel de opciones
- **WHEN** se intenta guardar una franja de 12:00 a 12:00
- **THEN** se rechaza con la franja señalada y no se crea ninguna versión

### Requirement: Cada guardado queda en la bitácora general

Cada guardado SHALL dejar, en la misma transacción que su versión, un registro `CONFIGURATION_CHANGED` por persona u opción cambiada, con el ajuste, el antes y el después, el resultado, la versión anterior y la nueva, y el actor nulo con `NO_AUTHENTICATION`. Un guardado rechazado o denegado MUST registrarse con su resultado y su motivo. Ningún registro MUST contener un código.

#### Scenario: Alta de una persona
Origen: pedido de David del 04/11/2026 («tanto personas como administradores») y RB21 («todo cambio … tiene permiso, actor y resultado»).
- **GIVEN** la base de la demostración en la versión 1
- **WHEN** se agrega una persona con el código `0105` y se guarda
- **THEN** la bitácora general tiene `CONFIGURATION_CHANGED` con el ajuste de persona, el antes nulo, el después con su nombre, su estado y sus cupos, la marca de credencial nueva, el resultado aplicado y las versiones 1 y 2
- **AND** ninguna línea de las dos bitácoras contiene `0105`

#### Scenario: Cambio de importe
Origen: AT41 (reconstruir sin consultar la base) y el diseño de administración («revisión anterior y nueva, campos cambiados»).
- **GIVEN** «Almuerzo común» con un importe de 1.500,00
- **WHEN** se lleva a 1.800,00 y se guarda
- **THEN** la general tiene `CONFIGURATION_CHANGED` con el ajuste de opción, el importe antes 150000 y después 180000 en unidades menores, y su versión de menú antes y después

#### Scenario: Guardado rechazado
Origen: AT37 («conservación del valor previo ante error»), borde: un rechazo también se reconstruye.
- **GIVEN** una persona nueva con un código ya usado
- **WHEN** se toca «Guardar»
- **THEN** la general tiene `CONFIGURATION_CHANGED` con el resultado rechazado, el motivo de código duplicado y la versión activa sin cambio, sin el código
