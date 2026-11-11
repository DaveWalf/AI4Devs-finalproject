## ADDED Requirements

### Requirement: Un teclado en pantalla para editar Personas

Al editar Personas, el panel SHALL mostrar un teclado en pantalla con letras, la Ñ, números, espacio y borrar, que escribe en el campo elegido; en el límite y el código MUST entrar sólo dígitos. «Ocultar teclado» MUST esconderlo y «Mostrar teclado», mostrarlo otra vez, hasta cerrar Configuración. Sus teclas MUST medir al menos 56 px de alto y entrar en 1024 × 768.

#### Scenario: Escribir un nombre con el teclado
Origen: mejora 5 de David del 10/11/2026 («teclado en pantalla que se muestra u oculta»).
- **GIVEN** la pestaña Personas en edición
- **WHEN** se toca el nombre de una fila y se tocan las teclas de «ANA»
- **THEN** el nombre de esa fila termina en «ANA», sin un teclado físico

#### Scenario: Ocultar el teclado
Origen: mejora 5 de David del 10/11/2026 («que se muestra u oculta»).
- **GIVEN** la pestaña Personas en edición, con el teclado a la vista
- **WHEN** se toca «Ocultar teclado», se cancela y se vuelve a editar
- **THEN** el teclado no se muestra hasta tocar «Mostrar teclado» o cerrar Configuración

#### Scenario: Una letra en el límite
Origen: «Modificar los cupos y el nombre de una persona» (el límite es un número de 0 a 99), borde.
- **GIVEN** el campo del límite de una celda elegido
- **WHEN** se tocan «A» y «1»
- **THEN** el campo dice sólo el dígito agregado

### Requirement: Exportar Personas a CSV y a Excel

La tabla de Personas SHALL exportarse a un archivo CSV o Excel (`.xlsx`) en la carpeta de la exportación de Consumos, pidiéndola la primera vez, con la persona, el estado, lo usado hoy y el límite de cada cupo y las opciones habilitadas, sin ningún código. Cada exportación MUST quedar en la general como `PEOPLE_EXPORTED`, con el formato, las filas y el nombre del archivo, sin nombres ni códigos.

#### Scenario: Exportar Personas a CSV
Origen: mejora 5 de David del 10/11/2026 («y exportación»).
- **GIVEN** la base de la demostración y una carpeta de exportación ya elegida
- **WHEN** se toca «Exportar CSV» en Personas
- **THEN** la carpeta tiene un `.csv` con un encabezado y una fila por persona, sin ningún código, y la general tiene `PEOPLE_EXPORTED`

#### Scenario: Exportar Personas a Excel
Origen: mejora 5 de David del 10/11/2026 («y exportación»), borde.
- **GIVEN** la base de la demostración y una carpeta de exportación ya elegida
- **WHEN** se toca «Exportar Excel» en Personas
- **THEN** la carpeta tiene un `.xlsx` que se abre como libro con una hoja, y lo usado hoy y el límite son números

## MODIFIED Requirements

### Requirement: Personas habilitadas con sus opciones y cupos

«Personas habilitadas», en la pestaña Personas, SHALL mostrar las personas de la versión activa en una tabla, una fila por persona, con el nombre visible, si está activa o de baja, cada cupo de servicio y categoría del catálogo como lo usado hoy sobre su límite («1/2»), o «—» sin cupo, y las opciones que esos cupos habilitan. Tocar un cupo o «Editar» MUST permitir cambiar nombres y límites en la misma tabla. MUST NOT mostrar ningún código.

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
