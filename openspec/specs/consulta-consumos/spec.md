# consulta-consumos Specification

## Purpose
Mostrarle a quien entra a Configuración los consumos registrados en el puesto, con los del día por defecto, filtros y orden por columna, sin códigos, sin cambiar ningún hecho y dejando cada consulta en la bitácora.

## Requirements

### Requirement: Los consumos del día por defecto

La pestaña Consumos SHALL mostrar en una tabla los consumos registrados en el puesto en la fecha de negocio actual, con la hora local, la persona, la opción, el servicio, el cargo y el estado del comprobante, del más reciente al más antiguo. Persona y opción MUST mostrarse como eran en la versión del consumo. MUST NOT mostrar códigos de identificación ni intentos denegados.

#### Scenario: Los consumos de hoy
Origen: pedido de David del 06/11/2026 («Por defecto, que muestre los consumos del día»), RF17 y AT45.
- **GIVEN** tres consumos registrados hoy, de `0001` y de `0002`, y Configuración abierta
- **WHEN** se abre la pestaña Consumos
- **THEN** la tabla muestra los tres, del más reciente al más antiguo, con su hora, persona, opción, servicio, cargo y estado del comprobante

#### Scenario: Un consumo de otra fecha de negocio
Origen: RB04 (la fecha de negocio decide el día) y AT45, borde.
- **GIVEN** un consumo registrado ayer y ninguno hoy
- **WHEN** se abre la pestaña Consumos
- **THEN** la tabla está vacía y dice «No hay consumos con estos filtros»

#### Scenario: Un rechazo no es un consumo
Origen: AT45 («estados y referencias separados») y RF17 (consumos y rechazos por separado), borde.
- **GIVEN** un consumo autorizado y una confirmación denegada por `LOCAL_LIMIT_REACHED`, los dos de hoy
- **WHEN** se abre la pestaña Consumos
- **THEN** la tabla muestra sólo el consumo autorizado

#### Scenario: Una persona renombrada después de consumir
Origen: AT09 («sin borrar consumos previos») y RF04 («sin alterar consumos históricos»), borde.
- **GIVEN** un consumo de hoy de «Persona sintética 01» y, después, una versión de configuración que la renombra
- **WHEN** se abre la pestaña Consumos
- **THEN** el consumo figura con «Persona sintética 01»

#### Scenario: Ningún código en la tabla
Origen: RB25 («no registrar el código en claro») y `AGENTS.md`.
- **GIVEN** consumos de hoy de `0001` y de `0002`
- **WHEN** se abre la pestaña Consumos
- **THEN** ni el texto ni los atributos de la pestaña contienen `0001` ni `0002`

### Requirement: Filtros de la consulta

La pestaña SHALL filtrar por un rango de fechas de negocio, por persona, por servicio y por opción, combinables. El rango MUST tener fechas válidas, el inicio no posterior al fin y hasta 366 días; si no, MUST rechazarse sin consultar. La tabla MUST mostrar a lo sumo 500 consumos y avisar cuando hay más.

#### Scenario: Una persona en una semana
Origen: pedido de David del 06/11/2026 («pudiendo filtrar por fecha, persona, tipo de consumo, etc.»).
- **GIVEN** consumos de `0001` y de `0002` en los últimos siete días
- **WHEN** se eligen esas siete fechas y «Persona sintética 01»
- **THEN** la tabla muestra sólo los de «Persona sintética 01» en esas fechas

#### Scenario: Un tipo de consumo
Origen: pedido de David del 06/11/2026 («tipo de consumo»).
- **GIVEN** almuerzos y refrigerios registrados hoy
- **WHEN** se elige el servicio REFRIGERIO
- **THEN** la tabla muestra sólo los refrigerios

#### Scenario: Una persona dada de baja
Origen: AT09 y D8 de `administrar-personas-y-opciones` (la baja no borra la persona), borde.
- **GIVEN** un consumo de hoy de una persona que después se dio de baja
- **WHEN** se la elige en el filtro de persona
- **THEN** la persona figura en el filtro como de baja y la tabla muestra su consumo

#### Scenario: Un rango invertido
Origen: `AGENTS.md` (OWASP: lo que se tipea en el puesto), borde.
- **GIVEN** la pestaña Consumos abierta
- **WHEN** se pide del 06/11/2026 al 01/11/2026
- **THEN** la pestaña dice que el inicio no puede ser posterior al fin y no cambia la tabla

#### Scenario: Más de 500 consumos
Origen: AT45 (el supervisor consulta los consumos), borde: una consulta sin tope no se puede recorrer en 1024 × 768.
- **GIVEN** 520 consumos en el rango elegido
- **WHEN** se consulta
- **THEN** la tabla muestra 500 y dice que hay más y que conviene acotar los filtros

### Requirement: Orden por columna

Tocar el encabezado de una columna SHALL ordenar los consumos filtrados por ese campo, ascendente y, con otro toque, descendente, con el orden vigente indicado en el encabezado. El orden MUST aplicarse a todos los consumos que cumplen los filtros antes del tope de 500, no sólo a los mostrados.

#### Scenario: Ordenar por persona
Origen: pedido de David del 06/11/2026 («Con ordenamiento por campos»).
- **GIVEN** la tabla con los consumos de hoy
- **WHEN** se toca el encabezado «Persona»
- **THEN** las filas quedan ordenadas por persona de la A a la Z, y el encabezado indica el orden ascendente

#### Scenario: Otro toque invierte el orden
Origen: pedido de David del 06/11/2026 («Con ordenamiento por campos»), borde.
- **GIVEN** la tabla ordenada por cargo ascendente
- **WHEN** se vuelve a tocar «Cargo»
- **THEN** las filas quedan del cargo mayor al menor y el encabezado indica el orden descendente

#### Scenario: El orden decide qué entra en el tope
Origen: pedido de David del 06/11/2026 («Con ordenamiento por campos»), borde del tope de 500 filas.
- **GIVEN** 520 consumos en el rango, el de mayor cargo entre los 20 más antiguos
- **WHEN** se ordena por cargo descendente
- **THEN** la primera fila es la de mayor cargo

### Requirement: La consulta es de quien entra a Configuración y queda registrada

La pestaña SHALL mostrarse a quien entró a Configuración, en mantenimiento o con la clave del día, sin pedir el código de un operador; «Comprobantes» MUST seguir pidiéndolo. Cada consulta MUST quedar en la bitácora general con el actor nulo, `NO_AUTHENTICATION` y cómo se entró (`Access`), los filtros, el orden y la cantidad de filas, sin nombres ni códigos.

#### Scenario: En mantenimiento, sin ningún código
Origen: decisión de David del 06/11/2026 (gate de D, punto 3: «Consumos lo ve cualquiera que entre a Configuración»).
- **GIVEN** el puesto en mantenimiento, con consumos de hoy
- **WHEN** se abre Configuración y se toca la pestaña Consumos
- **THEN** la tabla muestra los consumos de hoy sin pedir ningún código

#### Scenario: En operativo, con la clave del día
Origen: decisión de David del 06/11/2026 (gate de D, punto 3) y «Entrada a Configuración según el modo» de `acceso-puesto`, borde.
- **GIVEN** el puesto en operativo, con consumos de hoy
- **WHEN** se entra a Configuración con la clave del día y se toca la pestaña Consumos
- **THEN** la tabla muestra los consumos de hoy sin pedir otro código

#### Scenario: Comprobantes sigue pidiendo un operador
Origen: decisión de David del 06/11/2026 (2/3: «Comprobantes» con `OPERATOR`; gate de D, punto 3), borde: ver los consumos no abre la reimpresión.
- **GIVEN** Configuración abierta con la tabla de Consumos a la vista
- **WHEN** se pasa a la pestaña Comprobantes
- **THEN** pide el código de un operador con `OPERATOR` y no muestra comprobantes hasta aceptarlo

#### Scenario: La consulta en la bitácora
Origen: RB21 («todo cambio/prueba tiene permiso, actor y resultado») y AT41.
- **GIVEN** Configuración abierta en mantenimiento
- **WHEN** se filtra por REFRIGERIO y la tabla muestra dos consumos
- **THEN** la general tiene `CONSUMPTIONS_QUERIED` con el actor nulo, `NO_AUTHENTICATION`, `MAINTENANCE_MODE`, el servicio filtrado y 2 filas, sin nombres de personas ni códigos
