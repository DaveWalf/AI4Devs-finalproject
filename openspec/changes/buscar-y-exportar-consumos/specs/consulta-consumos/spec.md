## ADDED Requirements

### Requirement: Las últimas 24 horas por defecto

La pestaña Consumos SHALL mostrar en una tabla los consumos confirmados en las 24 horas anteriores a abrirla, por la hora de confirmación, con la fecha y la hora locales, el número de comprobante, la persona, la opción, el servicio, el cargo y el estado del comprobante, del más reciente al más antiguo. Persona y opción MUST mostrarse como eran en la versión del consumo. MUST NOT mostrar códigos de identificación ni intentos denegados.

#### Scenario: Los consumos de las últimas 24 horas
Origen: mejora 4 de David del 10/11/2026 («Consumos primero, con las últimas 24 h»), RF17 y AT45.
- **GIVEN** tres consumos confirmados en las últimas 24 horas, uno de ellos ayer, de otra fecha de negocio
- **WHEN** se abre la pestaña Consumos
- **THEN** la tabla muestra los tres, del más reciente al más antiguo, con su fecha y hora, número de comprobante, persona, opción, servicio, cargo y estado del comprobante

#### Scenario: Un consumo de hace más de 24 horas
Origen: mejora 4 de David del 10/11/2026, borde: el límite de las 24 horas.
- **GIVEN** un consumo confirmado hace 24 horas y un minuto, y ninguno después
- **WHEN** se abre la pestaña Consumos
- **THEN** la tabla está vacía y dice «No hay consumos con estos filtros»

#### Scenario: Un rechazo no es un consumo
Origen: AT45 («estados y referencias separados») y RF17 (consumos y rechazos por separado), borde.
- **GIVEN** un consumo autorizado y una confirmación denegada por `LOCAL_LIMIT_REACHED`, los dos en las últimas 24 horas
- **WHEN** se abre la pestaña Consumos
- **THEN** la tabla muestra sólo el consumo autorizado

#### Scenario: Una persona renombrada después de consumir
Origen: AT09 («sin borrar consumos previos») y RF04 («sin alterar consumos históricos»), borde.
- **GIVEN** un consumo de «Persona sintética 01» en las últimas 24 horas y, después, una versión de configuración que la renombra
- **WHEN** se abre la pestaña Consumos
- **THEN** el consumo figura con «Persona sintética 01»

#### Scenario: Ningún código en la tabla
Origen: RB25 («no registrar el código en claro») y `AGENTS.md`.
- **GIVEN** consumos de `0001` y de `0002` en las últimas 24 horas
- **WHEN** se abre la pestaña Consumos
- **THEN** ni el texto ni los atributos de la pestaña contienen `0001` ni `0002`

### Requirement: Buscar por texto y por código

La pestaña SHALL buscar un texto en la persona, la opción, el servicio y el número de comprobante, sin distinguir acentos ni mayúsculas, y filtrar por el código de una persona, combinables con los demás filtros. El código MUST NOT mostrarse después de consultar ni registrarse, y uno que no es de nadie MUST avisarse sin consultar.

#### Scenario: Un nombre sin acentos
Origen: mejora 6 de David del 10/11/2026 («búsqueda en Consumos por persona, código, comprobante y tipo, por fecha o alfanumérico»).
- **GIVEN** consumos de «Persona sintética 01» y de «Persona sintética 03» en las últimas 24 horas
- **WHEN** se busca «sintetica 03»
- **THEN** la tabla muestra sólo los de «Persona sintética 03»

#### Scenario: Un número de comprobante
Origen: mejora 6 de David del 10/11/2026 («comprobante»).
- **GIVEN** dos consumos en las últimas 24 horas
- **WHEN** se busca el número de comprobante de uno de ellos
- **THEN** la tabla muestra sólo ése

#### Scenario: El código de una persona
Origen: mejora 6 de David del 10/11/2026 («código») y RB25, borde: el código no queda a la vista.
- **GIVEN** consumos de `0001` y de `0003` en las últimas 24 horas
- **WHEN** se tipea `0003` en el campo del código y se consulta
- **THEN** la tabla muestra sólo los de «Persona sintética 03», el campo del código queda vacío y la pestaña no contiene `0003`

#### Scenario: Un código que no es de nadie
Origen: RB25 (código desconocido) y `AGENTS.md` (OWASP: lo que se tipea en el puesto), borde.
- **GIVEN** la pestaña Consumos con su tabla
- **WHEN** se consulta con el código `7777`, que no es de nadie
- **THEN** la pestaña avisa que ese código no es de ninguna persona, la tabla no cambia y la pestaña no contiene `7777`

#### Scenario: Comodines en el texto
Origen: `AGENTS.md` (OWASP A03: entrada externa en una consulta), borde.
- **GIVEN** consumos en las últimas 24 horas
- **WHEN** se busca «%»
- **THEN** la tabla está vacía, porque ningún nombre contiene ese carácter, y no muestra todos los consumos

### Requirement: La consulta es de quien entra a Configuración, y queda registrada

La pestaña SHALL mostrarse a quien entró a Configuración con la clave del día o con el código de un operador, sin pedir otro código. Cada consulta MUST quedar en la bitácora general como `CONSUMPTIONS_QUERIED`, con el actor (nulo con `NO_AUTHENTICATION` si se entró con la clave), cómo se entró (`Access`), los filtros, el orden y la cantidad de filas, sin nombres ni códigos; del texto buscado y del código MUST quedar sólo que se usaron.

#### Scenario: En mantenimiento, con la clave del día
Origen: decisión de David del 06/11/2026 (gate de D, punto 3: «Consumos lo ve cualquiera que entre a Configuración») y su mejora 3 del 10/11/2026 (la clave también en mantenimiento).
- **GIVEN** el puesto en mantenimiento, con consumos en las últimas 24 horas
- **WHEN** se entra a Configuración con la clave del día
- **THEN** se abre en Consumos con la tabla de esos consumos, sin pedir otro código

#### Scenario: En operativo, con la clave del día
Origen: decisión de David del 06/11/2026 (gate de D, punto 3) y «Entrada a Configuración según el modo» de `acceso-puesto`, borde.
- **GIVEN** el puesto en operativo, con consumos en las últimas 24 horas
- **WHEN** se entra a Configuración con la clave del día
- **THEN** se abre en Consumos con la tabla de esos consumos, sin pedir otro código

#### Scenario: Un operador ve los consumos
Origen: mejora 13 de David del 10/11/2026 (Consumos entre las secciones del operador) y RB21.
- **GIVEN** el puesto con consumos en las últimas 24 horas
- **WHEN** se entra a Configuración con `9001`
- **THEN** se abre en Consumos con la tabla de esos consumos
- **AND** la general tiene `CONSUMPTIONS_QUERIED` con `OPERATOR` y la persona de `9001` como actor, sin nombres de personas ni códigos

#### Scenario: La consulta en la bitácora
Origen: RB21 («todo cambio/prueba tiene permiso, actor y resultado») y AT41.
- **GIVEN** Configuración abierta en mantenimiento, a la que se entró con la clave del día
- **WHEN** se filtra por REFRIGERIO y la tabla muestra dos consumos
- **THEN** la general tiene `CONSUMPTIONS_QUERIED` con el actor nulo, `NO_AUTHENTICATION`, `DAILY_KEY`, el servicio filtrado y 2 filas, sin nombres de personas ni códigos

#### Scenario: El texto y el código en la bitácora
Origen: RB25 y `AGENTS.md` («el código de identificación no se escribe en logs»), borde.
- **GIVEN** la pestaña Consumos abierta
- **WHEN** se consulta con el texto «sintetica» y el código `0001`
- **THEN** el `CONSUMPTIONS_QUERIED` dice que se usaron un texto y un código, y no contiene «sintetica» ni `0001`

### Requirement: Exportar la consulta a CSV y a Excel

La pestaña SHALL exportar los consumos de los filtros aplicados, con el orden vigente y hasta 10 000, a un archivo CSV o Excel (`.xlsx`) en una carpeta que se elige la primera vez y se recuerda en la carpeta de datos. Los archivos MUST tener las columnas de la tabla y ningún código, y cada exportación MUST quedar en la general como `CONSUMPTIONS_EXPORTED`. Sin carpeta o sin poder escribir, MUST avisar sin dejar un archivo a medias.

#### Scenario: Exportar a CSV
Origen: mejora 4 de David del 10/11/2026 («exportación CSV y Excel a una ubicación elegida y recordada»).
- **GIVEN** dos consumos en la tabla y una carpeta de exportación ya elegida
- **WHEN** se toca «Exportar CSV»
- **THEN** la carpeta tiene un archivo `.csv` con un encabezado y dos filas, con las columnas de la tabla, y la pestaña dice su nombre
- **AND** la general tiene `CONSUMPTIONS_EXPORTED` con el formato, 2 filas y el nombre del archivo, sin nombres de personas ni códigos

#### Scenario: Exportar a Excel
Origen: mejora 4 de David del 10/11/2026 («Excel»).
- **GIVEN** dos consumos en la tabla y una carpeta de exportación ya elegida
- **WHEN** se toca «Exportar Excel»
- **THEN** la carpeta tiene un archivo `.xlsx` que se abre como libro con una hoja, un encabezado y dos filas, y el cargo como número

#### Scenario: La primera vez se elige la carpeta y después se recuerda
Origen: mejora 4 de David del 10/11/2026 («a una ubicación elegida y recordada»).
- **GIVEN** ninguna carpeta de exportación guardada
- **WHEN** se toca «Exportar CSV» y se elige una carpeta, y después se cierra el puesto, se vuelve a abrir y se exporta otra vez
- **THEN** los dos archivos quedan en esa carpeta, y la segunda vez no se pide elegirla

#### Scenario: Sin poder escribir
Origen: AT37 («conservación del valor previo ante error»), borde.
- **GIVEN** una carpeta de exportación guardada que ya no se puede escribir
- **WHEN** se toca «Exportar CSV»
- **THEN** la pestaña avisa que no se pudo exportar, no queda ningún archivo en esa carpeta y la general no tiene `CONSUMPTIONS_EXPORTED`

#### Scenario: Un nombre que empieza como una fórmula
Origen: `AGENTS.md` (OWASP: inyección de fórmulas en una planilla exportada), borde.
- **GIVEN** un consumo de una persona llamada «=1+1»
- **WHEN** se exporta a CSV
- **THEN** la celda de esa persona empieza con un apóstrofo, y la planilla no la toma como fórmula

## MODIFIED Requirements

### Requirement: Filtros de la consulta

La pestaña SHALL filtrar por un rango de fechas de negocio, por persona, por servicio y por opción, combinables, y «Últimas 24 h» MUST volver al rango por defecto. El rango MUST tener fechas válidas, el inicio no posterior al fin y hasta 366 días; si no, MUST rechazarse sin consultar. La tabla MUST mostrar a lo sumo 500 consumos y avisar cuando hay más.

#### Scenario: Una persona en una semana
Origen: pedido de David del 06/11/2026 («pudiendo filtrar por fecha, persona, tipo de consumo, etc.»).
- **GIVEN** consumos de `0001` y de `0002` en los últimos siete días
- **WHEN** se eligen esas siete fechas y «Persona sintética 01»
- **THEN** la tabla muestra sólo los de «Persona sintética 01» en esas fechas

#### Scenario: Un tipo de consumo
Origen: pedido de David del 06/11/2026 («tipo de consumo»).
- **GIVEN** almuerzos y refrigerios en las últimas 24 horas
- **WHEN** se elige el servicio REFRIGERIO
- **THEN** la tabla muestra sólo los refrigerios

#### Scenario: Una persona dada de baja
Origen: AT09 y D8 de `administrar-personas-y-opciones` (la baja no borra la persona), borde.
- **GIVEN** un consumo en las últimas 24 horas de una persona que después se dio de baja
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

#### Scenario: Volver a las últimas 24 horas
Origen: mejora 4 de David del 10/11/2026 («con las últimas 24 h»), borde: después de elegir fechas.
- **GIVEN** la tabla con los consumos de una semana elegida
- **WHEN** se toca «Últimas 24 h»
- **THEN** la tabla vuelve a los consumos de las últimas 24 horas

## REMOVED Requirements

### Requirement: Los consumos del día por defecto

**Reason**: Desde la mejora 4 de David del 10/11/2026, la pestaña abre con las últimas 24 horas por la hora de confirmación, no con la fecha de negocio actual, y muestra la fecha y el número de comprobante; los escenarios de este requisito afirmaban el día de negocio.

**Migration**: La reemplaza «Las últimas 24 horas por defecto». El rango de fechas de negocio sigue en «Filtros de la consulta».

### Requirement: La consulta es de quien entra con la clave o un operador, y queda registrada

**Reason**: La pestaña Comprobantes se va (mejora 6 de David del 10/11/2026), y este requisito y su escenario «Comprobantes sigue pidiendo un operador» la nombraban.

**Migration**: La reemplaza «La consulta es de quien entra a Configuración, y queda registrada»; el código de operador para reimprimir está en «Asistencia con permiso OPERATOR» de `comprobante-local`.
