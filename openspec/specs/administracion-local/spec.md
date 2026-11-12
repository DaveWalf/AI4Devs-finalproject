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

«Personas habilitadas», en la pestaña Personas, SHALL mostrar las personas de la versión activa en una tabla, una fila por persona, con el nombre visible, si está activa o de baja, cada cupo de servicio y categoría del catálogo como lo usado hoy sobre su límite («1/2»), o «—» sin cupo, y la cantidad de opciones que esos cupos habilitan. Tocar un cupo o «Editar» MUST permitir cambiar nombres y límites en la misma tabla. MUST NOT mostrar ningún código.

#### Scenario: La persona del código 0001
Origen: pedido de David del 04/11/2026 («qué opciones y cupos tiene»), su pedido del 06/11/2026 («que cada columna represente sus diferentes elementos de configuración»), la mejora 5 del 10/11/2026 («raciones por tipo en x/n») y AT02.
- **GIVEN** la base de la demostración con el catálogo de nueve, sin consumos en el día
- **WHEN** se abre la pestaña Personas
- **THEN** la fila de «Persona sintética 01» dice «Activa», «0/1» en la columna ALMUERZO, «0/2» en la de REFRIGERIO y 9 opciones habilitadas

#### Scenario: Ningún código en el panel
Origen: RB25 («no registrar el código en claro») y `AGENTS.md`.
- **GIVEN** la base de la demostración, con los códigos sintéticos de 0001 a 0004 y de 9001 a 9004
- **WHEN** se abre la pestaña Personas y se toca «Editar»
- **THEN** el texto, los valores de los campos y los atributos de la tabla no contienen ninguno de esos códigos

#### Scenario: Persona sin cupos
Origen: RB02 (`SERVICE_NOT_ENABLED`), borde.
- **GIVEN** una persona activa sin ninguna regla
- **WHEN** se abre la pestaña Personas
- **THEN** su fila tiene «—» en cada columna de cupo y 0 opciones habilitadas

#### Scenario: Muchas personas en 1024 × 768
Origen: pedido de David del 06/11/2026 («utilizar un modo tabla donde entren muchas personas a la vez»).
- **GIVEN** la semilla de quince opciones con 200 personas sintéticas más y un área de 1024 × 768
- **WHEN** se abre la pestaña Personas
- **THEN** se ven al menos diez filas a la vez sin desplazar la pantalla, cada una de al menos 44 px de alto
- **AND** al desplazar la tabla, su encabezado sigue a la vista

#### Scenario: Cambiar un límite en su columna
Origen: pedido de David del 06/11/2026 («Que la posibilidad de consumos esté en esas columnas»).
- **GIVEN** la pestaña Personas con «Editar» tocado
- **WHEN** se cambia la celda REFRIGERIO de «Persona sintética 01» de 2 a 1 y se guarda
- **THEN** la versión 2 está activa y la fila dice «0/1» en REFRIGERIO

#### Scenario: Vaciar una celda quita el cupo
Origen: «Modificar los cupos y el nombre de una persona» (quitar el cupo deja al par sin regla), borde.
- **GIVEN** la pestaña Personas con «Editar» tocado
- **WHEN** se vacía la celda REFRIGERIO de «Persona sintética 01» y se guarda
- **THEN** la fila dice «—» en REFRIGERIO y sus opciones habilitadas son sólo las de ALMUERZO

#### Scenario: Lo usado hoy en cada cupo
Origen: mejora 5 de David del 10/11/2026 («raciones por tipo en x/n») y RB03 (el cupo se cuenta por persona y fecha de negocio).
- **GIVEN** `0001` con un almuerzo consumido hoy y otro consumido ayer
- **WHEN** se abre la pestaña Personas
- **THEN** su fila dice «1/1» en ALMUERZO

#### Scenario: Tocar un cupo lo edita
Origen: mejora 5 de David del 10/11/2026 («editables al tocar»).
- **GIVEN** la pestaña Personas en lectura
- **WHEN** se toca la celda REFRIGERIO de «Persona sintética 01»
- **THEN** la tabla queda en edición con el límite de esa celda elegido, y guardar crea una versión nueva

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

Cada guardado SHALL dejar, en la misma transacción que su versión, un `CONFIGURATION_CHANGED` por persona u opción cambiada, con el ajuste, el antes y el después, los cupos cambiados de una persona con su límite antes y después, el resultado, las versiones y el actor: la persona del operador que entró con su código, o nulo con `NO_AUTHENTICATION`. Un guardado rechazado o denegado MUST registrarse con su motivo. Ningún registro MUST contener un código.

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

#### Scenario: Un cupo cambiado por un operador
Origen: mejora 5 de David del 10/11/2026 («bitácora de qué operador cambió qué a quién y cuándo») y RB21.
- **GIVEN** Configuración abierta con el código `9001`
- **WHEN** se lleva REFRIGERIO de «Persona sintética 01» de 2 a 1 y se guarda
- **THEN** la general tiene `CONFIGURATION_CHANGED` con su `PersonId`, `QuotaChanges` con REFRIGERIO de 2 a 1, y como actor la persona de `9001`

### Requirement: Personas ordenables por columna

Tocar el encabezado de una columna de la tabla de personas, salvo las de acciones y código, SHALL ordenar las filas por ese campo, ascendente y, con otro toque, descendente, con el orden vigente indicado en el encabezado. El nombre MUST ordenarse alfabéticamente en español y una celda sin cupo MUST ir al final. Ordenar MUST NOT cambiar lo que se guarda ni a qué persona se le señala un campo inválido.

#### Scenario: Ordenar por un cupo
Origen: pedido de David del 06/11/2026 («Que se pueda ordenar por cualquiera de los campos que lo ameriten»).
- **GIVEN** la pestaña Personas de la base de la demostración
- **WHEN** se toca el encabezado REFRIGERIO dos veces
- **THEN** las filas quedan del límite mayor al menor, con las personas sin cupo de REFRIGERIO al final, y el encabezado indica el orden descendente

#### Scenario: Nombres con tilde
Origen: pedido de David del 06/11/2026 (ordenar «por cualquiera de los campos»), borde: el orden en español.
- **GIVEN** las personas «Ana», «Álvaro» y «Beatriz»
- **WHEN** se ordena por nombre
- **THEN** quedan «Álvaro», «Ana» y «Beatriz»

#### Scenario: Ordenar no mueve la marca de un campo inválido
Origen: D11 de `administrar-personas-y-opciones` (el servicio señala cada campo rechazado), borde.
- **GIVEN** una persona nueva sin nombre, agregada con «Editar», y un guardado que la señaló como inválida
- **WHEN** se ordena por nombre
- **THEN** la marca de campo inválido sigue en la fila de esa persona nueva y no en otra

### Requirement: Un teclado en pantalla para editar Personas

Al editar Personas, el panel SHALL mostrar un teclado en pantalla con letras, la Ñ, números, espacio y borrar, que escribe en el campo elegido; en el límite y el código MUST entrar sólo dígitos. «Ocultar teclado» MUST esconderlo y «Mostrar teclado», mostrarlo otra vez, hasta cerrar Configuración. Sus teclas MUST medir al menos 56 px de alto y entrar en 1024 × 768.

#### Scenario: Escribir un nombre con el teclado
Origen: mejora 5 de David del 10/11/2026 («teclado en pantalla que se muestra u oculta»).
- **GIVEN** la pestaña Personas en edición
- **WHEN** se toca el nombre de una fila, «Mayús» y las teclas de «ANA»
- **THEN** el nombre de esa fila termina en «ANA», sin un teclado físico

#### Scenario: Ocultar el teclado
Origen: mejora 5 de David del 10/11/2026 («que se muestra u oculta»).
- **GIVEN** la pestaña Personas en edición, con el teclado a la vista
- **WHEN** se toca «Ocultar teclado», se cancela, se pasa a Consumos y se vuelve a editar Personas
- **THEN** el teclado no se muestra hasta tocar «Mostrar teclado» o cerrar Configuración

#### Scenario: El código de una persona nueva con el teclado
Origen: D10 de administrar-personas-y-opciones («lo tipeado no vuelve a dibujarse»), borde.
- **GIVEN** una persona nueva en edición, con su código elegido
- **WHEN** se tocan «0», «1», «0» y «5» y se guarda
- **THEN** la versión 2 está activa con ese código, y la página no contiene «0105»

#### Scenario: Una letra en el límite
Origen: «Modificar los cupos y el nombre de una persona» (el límite es un número de 0 a 99), borde.
- **GIVEN** el campo del límite de una celda elegido, con 2
- **WHEN** se tocan borrar, «A» y «1»
- **THEN** el campo dice «1»: la letra no entró

### Requirement: Exportar Personas a CSV y a Excel

La tabla de Personas SHALL exportarse a un archivo CSV o Excel (`.xlsx`) en la carpeta de la exportación de Consumos, pidiéndola la primera vez, con la persona, el estado, lo usado hoy y el límite de cada cupo, las opciones habilitadas y, al final, si es operador («Sí» o «No»), sin ningún código. Cada exportación MUST quedar en la general como `PEOPLE_EXPORTED`, con el formato, las filas y el nombre del archivo, sin nombres ni códigos.

#### Scenario: Exportar Personas a CSV
Origen: mejora 5 de David del 10/11/2026 («y exportación»).
- **GIVEN** la base de la demostración y una carpeta de exportación ya elegida
- **WHEN** se toca «Exportar CSV» en Personas
- **THEN** la carpeta tiene un `.csv` con un encabezado y una fila por persona, con la columna «Operador» en «Sí» para «Operador sintético 01», sin ningún código, y la general tiene `PEOPLE_EXPORTED`

#### Scenario: Exportar Personas a Excel
Origen: mejora 5 de David del 10/11/2026 («y exportación»), borde.
- **GIVEN** la base de la demostración y una carpeta de exportación ya elegida
- **WHEN** se toca «Exportar Excel» en Personas
- **THEN** la carpeta tiene un `.xlsx` que se abre como libro con una hoja, y lo usado hoy y el límite son números

#### Scenario: Una carpeta que no se puede escribir
Origen: AT37 («conservación del valor previo ante error»), borde.
- **GIVEN** una carpeta de exportación guardada que ya no se puede escribir
- **WHEN** se toca «Exportar CSV» en Personas
- **THEN** el pie avisa que no se pudo exportar, la carpeta sigue vacía y la general no tiene `PEOPLE_EXPORTED`

### Requirement: La planilla de ejemplo de Personas

Personas SHALL descargar, en la carpeta de la exportación, una planilla de ejemplo `.xlsx` con la hoja «Personas» (Nombre, Código, Operador con «Sí» o «No» y el límite de cada cupo del catálogo, con dos filas de ejemplo sintéticas, la segunda operador, y el código como texto) y la hoja «Instrucciones». MUST NOT contener ningún código de una persona del puesto.

#### Scenario: Descargar la planilla de ejemplo
Origen: mejora 14 de David del 10/11/2026 («descargar un Excel de ejemplo claro y agradable de completar»).
- **GIVEN** una carpeta de exportación ya elegida
- **WHEN** se toca «Planilla de ejemplo» en Personas
- **THEN** la carpeta tiene un `.xlsx` con las hojas «Personas» e «Instrucciones», y el encabezado de «Personas» es «Nombre», «Código», «Operador» y «<cupo>: límite» por cada cupo, e «Instrucciones» explica «Sí», «No» y la celda vacía de «Operador»
- **AND** la columna «Código» tiene formato de texto y ningún código de la base de la demostración

#### Scenario: Sin carpeta para la planilla
Origen: «Exportar Personas a CSV y a Excel» (sin carpeta, se avisa), borde.
- **GIVEN** ninguna carpeta de exportación guardada
- **WHEN** se toca «Planilla de ejemplo» y se cancela la elección
- **THEN** el pie avisa que no se eligió una carpeta y no se escribe nada

### Requirement: Importar Personas desde Excel

Personas SHALL importar un `.xlsx` con las columnas de la planilla de ejemplo: cada fila se reconoce por su código; uno sin revocar de una persona activa la modifica, aunque esté vencido, y uno nuevo la agrega. Antes de aplicar MUST mostrar el resumen; «Modificar y agregar» deja a las demás como estaban y «Reemplazar» da de baja a las activas que no están, salvo quien tenga un permiso de operador o de administrador. Una fila con error MUST frenar todo. Lo usado hoy no cambia.

#### Scenario: Modificar y agregar
Origen: mejora 14 de David del 10/11/2026 («modifique y agregue»).
- **GIVEN** un archivo con 0001 y REFRIGERIO 1, y una persona nueva con el código 0105
- **WHEN** se importa y se toca «Modificar y agregar»
- **THEN** la versión 2 está activa, 0001 tiene REFRIGERIO 1, «Persona sintética 05» está agregada y las demás personas siguen como estaban

#### Scenario: Reemplazar conserva a los operadores
Origen: mejora 14 de David del 10/11/2026 («reemplace lo actual») y RB26 (los operadores atienden la asistencia), borde.
- **GIVEN** un archivo sólo con 0001
- **WHEN** se importa y se toca «Reemplazar»
- **THEN** las personas activas sin permiso que no están en el archivo quedan de baja, y los operadores y el administrador siguen activos

#### Scenario: Los consumos de hoy siguen contando
Origen: mejora 14 de David del 10/11/2026 («aclarar si los consumos habilitados afectan los ya registrados … en la misma jornada») y RB03.
- **GIVEN** 0001 con un almuerzo consumido hoy
- **WHEN** se importa un archivo que le deja ALMUERZO 1
- **THEN** su fila dice «1/1» y su oferta ya no incluye almuerzos hoy

#### Scenario: Una fila con error no aplica nada
Origen: AT37 («conservación del valor previo ante error»), borde.
- **GIVEN** un archivo con una fila sin código y otra con el código de una persona dada de baja
- **WHEN** se importa
- **THEN** el resumen señala las dos filas con su motivo, no deja aplicar y la versión activa no cambia

#### Scenario: Un código que Excel guardó como número
Origen: verificación del change («Reemplazar» no se deshace y un código sin sus ceros cuenta como nuevo), borde.
- **GIVEN** un archivo con una fila de la planilla de ejemplo y un código guardado como número
- **WHEN** se importa
- **THEN** el resumen avisa las dos cosas antes de aplicar, y «Reemplazar» dice que la baja revoca los códigos sin vuelta atrás
- **AND** un código de más de 15 dígitos guardado como número es un error de su fila

#### Scenario: Cancelar el resumen
Origen: mejora 14 de David del 10/11/2026 («según se configure»), borde.
- **GIVEN** el resumen de una importación sin errores
- **WHEN** se toca «Cancelar»
- **THEN** la versión activa no cambia y la general no tiene `PEOPLE_IMPORTED`

#### Scenario: Un archivo que no es una planilla
Origen: `AGENTS.md` (OWASP: la entrada externa), borde.
- **GIVEN** un archivo `.xlsx` dañado, sin la columna «Código», con una columna desconocida o sin ninguna persona
- **WHEN** se importa
- **THEN** el resumen lista qué falta o qué sobra, sin mostrar el título de una columna que sobra, y no deja aplicar nada

### Requirement: La importación queda en la bitácora

Cada importación aplicada SHALL dejar un `CONFIGURATION_CHANGED` por persona cambiada, como un guardado a mano, y un `PEOPLE_IMPORTED` en la general con el modo, la huella SHA-256 del archivo, los totales y, por persona, su `PersonId` y lo que pasó. Una rechazada MUST dejar `PEOPLE_IMPORT_REJECTED` con los errores por motivo. Ninguno MUST contener nombres ni códigos.

#### Scenario: El resumen persona por persona
Origen: mejora 14 de David del 10/11/2026 («bitácora de lo decidido y del resumen persona por persona»).
- **GIVEN** una importación de 0001 modificada y 0105 agregada con «Modificar y agregar»
- **WHEN** se aplica
- **THEN** la general tiene `PEOPLE_IMPORTED` con `MERGE`, 1 agregada, 1 modificada, y una entrada por persona con su `PersonId`, sin nombres ni códigos

#### Scenario: Una importación rechazada
Origen: RB21 («todo cambio … tiene permiso, actor y resultado»), borde.
- **GIVEN** un archivo con una fila sin código
- **WHEN** se importa
- **THEN** la general tiene `PEOPLE_IMPORT_REJECTED` con un error `REQUIRED` y ningún `PEOPLE_IMPORTED`

### Requirement: Las opciones a la vista en su pestaña

La pestaña Opciones SHALL mostrar el catálogo activo, cada opción con su nombre, servicio, importe, cargas, franjas y una muestra de su tarjeta en la atención, con «Editar» y «Personalizar». «Editar» MUST abrir la edición de siempre en el mismo lugar, con el nombre de esa opción a mano; con INWeb como dueño, sólo para leer.

#### Scenario: El catálogo a la vista
Origen: mejora 7 de David del 10/11/2026 («Opciones editables en el lugar»).
- **GIVEN** el catálogo de la demostración
- **WHEN** se elige la pestaña Opciones
- **THEN** se ven todas sus opciones con su servicio, su importe y sus cargas, sin abrir la edición

#### Scenario: Editar desde una opción
Origen: mejora 7 de David del 10/11/2026, borde.
- **GIVEN** el catálogo a la vista
- **WHEN** se toca «Editar» en «Almuerzo opcional»
- **THEN** la edición se abre en el lugar del catálogo con el nombre de «Almuerzo opcional» enfocado, y «Guardar» crea una sola versión como siempre

### Requirement: Personalizar una opción

«Personalizar» SHALL abrir un cartel con la muestra de la tarjeta, un fondo de color de una paleta de ocho o una imagen PNG, WebP o SVG con las reglas de los logos, la transparencia (0, 25, 50 o 75 %) y «Mostrar el precio». Lo guardado MUST quedar en el puesto, sin crear versión ni depender del dueño de los datos, y en la general como un ajuste del equipo, también si falla.

#### Scenario: Un fondo de color con transparencia
Origen: mejora 7 de David del 10/11/2026 («color o imagen de fondo y transparencias»).
- **GIVEN** el catálogo a la vista
- **WHEN** se personaliza «Almuerzo opcional» con el tercer color, 25 % de transparencia, y se guarda
- **THEN** la versión activa no cambia, la general tiene `CONFIGURATION_CHANGED` del ajuste `OPTION_STYLE` con el antes, ese color y 25 después y el resultado, y al reabrir Configuración la muestra conserva el fondo

#### Scenario: Una imagen de fondo
Origen: mejora 7 de David del 10/11/2026, y las reglas de los logos de `configurar-puesto-por-pestanas`.
- **GIVEN** el cartel de personalización
- **WHEN** se elige una imagen PNG válida y se guarda
- **THEN** la muestra la usa de fondo y la general registra su huella SHA-256, no su contenido
- **AND** un archivo que no es una imagen válida se rechaza con el aviso de los logos y no se guarda nada

#### Scenario: Ocultar el precio
Origen: mejora 16 de David del 11/11/2026 («configurar si mostrar el precio de cada elemento o no, más allá de lo que esté configurado»).
- **GIVEN** «Almuerzo opcional» con un importe de 1.800,00
- **WHEN** se apaga «Mostrar el precio» y se guarda
- **THEN** su muestra no tiene importe y la general registra `PriceVisible` en falso después

#### Scenario: Guardar sin poder escribir
Origen: el estilo visual de `configuracion-puesto` («un guardado fallido también se reconstruye»), borde.
- **GIVEN** una carpeta de datos donde no se puede escribir la presentación
- **WHEN** se elige un color y se toca «Guardar»
- **THEN** el cartel sigue abierto con el aviso de que no se pudo guardar, nada cambia y la general tiene el resultado fallido con lo vigente

#### Scenario: Con INWeb como dueño
Origen: mejora 16 de David del 11/11/2026 («más allá de lo que esté configurado»), borde.
- **GIVEN** la configuración activa administrada por INWeb
- **WHEN** se personaliza una opción
- **THEN** su «Editar» dice «Ver» y la presentación se guarda igual

#### Scenario: Cancelar la personalización
Origen: mejora 7 de David del 10/11/2026, borde.
- **GIVEN** el cartel con un color elegido y sin guardar
- **WHEN** se toca «Cancelar», o Escape, que cierra Configuración entera
- **THEN** la muestra vuelve a como estaba y no se escribe nada

#### Scenario: Un archivo de presentación roto
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto) y el estilo visual de `presentar-atencion-tactil`, borde.
- **GIVEN** un `presentacion-opciones.json` con un color fuera de la paleta y una imagen que no existe
- **WHEN** el puesto lo lee
- **THEN** esas opciones se ven sin personalizar y la atención sigue

### Requirement: Exportar Opciones y la planilla base

Opciones SHALL exportar el catálogo activo a un `.xlsx`, en la carpeta de la exportación, con las columnas que se importan (Nombre, Servicio, Importe, Franjas y las unidades de cada categoría de cupo), y descargar una planilla base con esas columnas, dos filas de ejemplo y una hoja «Instrucciones». Exportar MUST quedar en la general como `MENU_OPTIONS_EXPORTED`.

#### Scenario: Exportar el catálogo
Origen: mejora 15 de David del 10/11/2026 («Opciones exporta e importa Excel»).
- **GIVEN** el catálogo de la demostración y una carpeta de exportación elegida
- **WHEN** se toca «Exportar Excel» en Opciones
- **THEN** la carpeta tiene un `.xlsx` con una fila por opción y la general tiene `MENU_OPTIONS_EXPORTED` con sus filas

#### Scenario: Descargar la planilla base
Origen: mejora 15 de David del 10/11/2026 («con la descarga de una planilla base»).
- **GIVEN** una carpeta de exportación elegida
- **WHEN** se toca «Planilla base»
- **THEN** la carpeta tiene un `.xlsx` con las hojas «Opciones» e «Instrucciones», el encabezado de D1 y dos filas de ejemplo

#### Scenario: Sin carpeta
Origen: «Exportar Personas a CSV y a Excel» (sin carpeta, se avisa), borde.
- **GIVEN** ninguna carpeta de exportación guardada
- **WHEN** se toca «Exportar Excel» y se cancela la elección
- **THEN** el pie avisa que no se eligió una carpeta y no se escribe nada

### Requirement: Importar Opciones desde Excel

Opciones SHALL importar un `.xlsx` con las columnas de la planilla base y reemplazar con él el catálogo: una opción se reconoce por su nombre, la que coincide se modifica, la nueva se agrega y la que no está se quita, nombrada en el resumen, sin tocar sus consumos. Antes de aplicar MUST mostrar el resumen con «Reemplazar» y «Cancelar»; una fila con error o un archivo sin opciones MUST frenar todo.

#### Scenario: Reemplazar el catálogo
Origen: mejora 15 de David del 10/11/2026 («lo importado reemplaza todas las opciones vigentes») y AT47 («consumos anteriores conservan su snapshot monetario»).
- **GIVEN** el catálogo exportado, con el importe de una opción cambiado, una fila nueva y otra borrada
- **WHEN** se importa y se toca «Reemplazar»
- **THEN** la versión siguiente está activa con una opción modificada, una agregada y una quitada, y la quitada no se ofrece pero sus consumos siguen registrados

#### Scenario: Cancelar el resumen de Opciones
Origen: mejora 15 de David del 10/11/2026 y AT37 («conservación del valor previo»), borde.
- **GIVEN** el resumen de una importación sin errores
- **WHEN** se toca «Cancelar»
- **THEN** la versión activa no cambia y la general no tiene `MENU_OPTIONS_IMPORTED`

#### Scenario: Una fila de Opciones con error
Origen: AT37 («conservación del valor previo ante error») y «Opción inválida» de este requisito vivo, borde.
- **GIVEN** un archivo con una fila sin cargas, otra con un servicio desconocido y otra con la franja «11 a 14»
- **WHEN** se importa
- **THEN** el resumen señala las tres filas con su motivo y los rangos de Opciones, no deja aplicar y la versión activa no cambia

#### Scenario: Un importe escrito de otra forma
Origen: `AGENTS.md` (OWASP: la entrada externa), borde.
- **GIVEN** un archivo con un importe «1800.50» en texto y otro de 28 dígitos
- **WHEN** se importa
- **THEN** el primero es un importe que no es un número y el segundo, uno fuera de rango, sin leer otro importe ni frenar la pantalla

#### Scenario: Un archivo de Opciones vacío o que no es una planilla
Origen: `AGENTS.md` (OWASP: la entrada externa), borde.
- **GIVEN** un archivo dañado, uno sin la columna «Importe» y uno sin ninguna opción
- **WHEN** se importa cada uno
- **THEN** el resumen dice qué falta o qué está mal y no se aplica nada

### Requirement: La importación de Opciones queda en la bitácora

Cada importación aplicada SHALL dejar un `CONFIGURATION_CHANGED` por opción cambiada y un `MENU_OPTIONS_IMPORTED` con `REPLACE`, la huella SHA-256 del archivo, los totales y, por opción, su id y lo que pasó. Una rechazada al revisarla MUST dejar `MENU_OPTIONS_IMPORT_REJECTED` con los errores por motivo; un rechazo al aplicar queda como el `CONFIGURATION_CHANGED` rechazado de un guardado a mano.

#### Scenario: El resumen opción por opción
Origen: RB21 («todo cambio … tiene permiso, actor y resultado»).
- **GIVEN** una importación con una opción modificada, una agregada y una quitada
- **WHEN** se aplica
- **THEN** la general tiene `MENU_OPTIONS_IMPORTED` con 1 agregada, 1 modificada, 1 quitada y una entrada por opción con su id

#### Scenario: Una importación de Opciones rechazada
Origen: RB21, borde.
- **GIVEN** un archivo con una fila sin nombre
- **WHEN** se importa
- **THEN** la general tiene `MENU_OPTIONS_IMPORT_REJECTED` con un error `REQUIRED` y ningún `MENU_OPTIONS_IMPORTED`

### Requirement: Los operadores en Personas

La tabla de Personas SHALL mostrar, en la columna «Operador», después de «Estado», un ícono de escudo con el texto accesible «Operador» en quien tiene un permiso `OPERATOR` vigente, y ordenarla como las demás. En edición, tocar esa celda MUST marcarla o desmarcarla; guardar MUST dar el permiso desde el guardado y sin fin, o revocarlo en la versión nueva sin borrar su fila. Un operador MUST NOT cambiar la suya.

#### Scenario: Ver quién es operador
Origen: mejora 7 de David del 11/11/2026 («un ícono que claramente distinga a las personas que son operadores») y su respuesta 3 al gate.
- **GIVEN** la base de la demostración, con `9001` vigente, `9002` vencido y `9003` revocado
- **WHEN** se abre la pestaña Personas
- **THEN** sólo la fila de «Operador sintético 01» tiene el escudo en «Operador», con el texto accesible «Operador», y ninguna fila muestra un código

#### Scenario: Marcar a una persona como operador
Origen: mejora 7 de David («que se pueda editar allí») y su respuesta 1 al gate, con RB26.
- **GIVEN** Configuración abierta con la clave del día, en Personas
- **WHEN** se toca «Editar», se marca «Operador» en «Persona sintética 01» y se guarda
- **THEN** la versión nueva tiene su permiso `OPERATOR` desde el guardado y sin fin, y en reposo, con `0001`, el engranaje abre Configuración como operador
- **AND** la general tiene el `CONFIGURATION_CHANGED` de esa persona con el permiso antes «No» y después «Sí», sin su código

#### Scenario: Desmarcar a un operador
Origen: respuesta 1 de David al gate del 11/11/2026 (quitarlo revoca en la versión nueva) y RB26 («revocación efectiva al activar el snapshot»), borde.
- **GIVEN** Configuración abierta con la clave del día, en Personas
- **WHEN** se desmarca «Operador» en «Operador sintético 01» y se guarda
- **THEN** la versión nueva tiene su permiso revocado en el instante del guardado, la versión anterior lo conserva como estaba, y el engranaje con `9001` dice que el permiso está revocado

#### Scenario: Un operador marca a otro, no a sí mismo
Origen: respuesta 2 de David al gate del 11/11/2026 («Operador, salvo a sí mismo»), borde: el control y el guardado.
- **GIVEN** Configuración abierta con `9001`, en Personas con «Editar»
- **WHEN** marca a «Persona sintética 01», guarda, y después se pide guardar su propio permiso desmarcado
- **THEN** «Persona sintética 01» queda operador con `9001` como actor, la celda «Operador» de su propia fila no se puede tocar, y el pedido de cambiar la suya se deniega sin crear versión

#### Scenario: Una persona de baja
Origen: «Quitar una persona es darla de baja» y RB26, borde: un permiso para quien no puede identificarse.
- **GIVEN** Personas en edición, con una persona dada de baja
- **WHEN** se revisa su celda «Operador»
- **THEN** no se puede marcar

### Requirement: El permiso de operador al importar Personas

Al importar Personas, la columna «Operador» con «Sí» SHALL dar el permiso `OPERATOR` desde la aplicación y sin fin, y con «No», revocarlo en la versión nueva; vacía o ausente MUST NOT cambiarlo. Otro valor MUST ser un error de su fila, que frena todo, como también una fila que cambie el permiso de quien importa siendo operador. El resumen y `PEOPLE_IMPORTED` MUST decir cuántos permisos se dan y se quitan, sin nombres ni códigos.

#### Scenario: Dar y quitar por planilla
Origen: mejora 7 de David del 11/11/2026 («que esto además se refleje en los archivos de […] importación») y su respuesta 4 al gate.
- **GIVEN** un archivo con `0001` y «Operador» en «Sí», y `9001` con «No»
- **WHEN** se importa y se toca «Modificar y agregar»
- **THEN** la versión nueva tiene a `0001` con el permiso y a `9001` revocado, y el resumen y `PEOPLE_IMPORTED` dicen 1 permiso dado y 1 quitado

#### Scenario: Una planilla de antes
Origen: respuesta 4 de David al gate («vacía o ausente no cambia nada»), borde: las planillas que ya circulan.
- **GIVEN** un archivo con la planilla de antes, sin la columna «Operador», con `9001` y su REFRIGERIO
- **WHEN** se importa y se aplica
- **THEN** `9001` sigue con su permiso, y la columna faltante no es un error

#### Scenario: Un valor que no se entiende
Origen: `AGENTS.md` (OWASP: la entrada externa) y la respuesta 4 de David al gate, borde.
- **GIVEN** un archivo con «Operador» en «Tal vez» en una fila, y en otra la fórmula `=1+1`
- **WHEN** se importa
- **THEN** el resumen señala las dos filas con su motivo, sin mostrar lo escrito, y no deja aplicar nada

#### Scenario: Un operador que se quita a sí mismo por planilla
Origen: respuesta 2 de David al gate del 11/11/2026 («salvo a sí mismo»), borde: la planilla no esquiva la regla.
- **GIVEN** Configuración abierta con `9001`, y un archivo con `9001` en «No»
- **WHEN** se importa
- **THEN** el resumen señala esa fila porque no puede cambiar su propio permiso, y no deja aplicar nada
