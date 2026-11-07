# Spec Delta

## MODIFIED Requirements

### Requirement: Personas habilitadas con sus opciones y cupos

«Personas habilitadas», en la pestaña Personas, SHALL mostrar las personas de la versión activa en una tabla, una fila por persona, con columnas para el nombre visible, si está activa o de baja, cada cupo de servicio y categoría que usa el catálogo, con su límite o «—» sin cupo, y la cantidad de opciones que esos cupos habilitan. «Editar» MUST permitir cambiar nombres y límites en la misma tabla. MUST NOT mostrar ningún código de identificación.

#### Scenario: La persona del código 0001
Origen: pedido de David del 04/11/2026 («qué opciones y cupos tiene»), su pedido del 06/11/2026 («que cada columna represente sus diferentes elementos de configuración») y AT02.
- **GIVEN** la base de la demostración con el catálogo de nueve
- **WHEN** se abre la pestaña Personas
- **THEN** la fila de «Persona sintética 01» dice «Activa», 1 en la columna ALMUERZO, 2 en la de REFRIGERIO y 9 opciones habilitadas

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
- **THEN** la versión 2 está activa y la fila dice 1 en REFRIGERIO

#### Scenario: Vaciar una celda quita el cupo
Origen: «Modificar los cupos y el nombre de una persona» (quitar el cupo deja al par sin regla), borde.
- **GIVEN** la pestaña Personas con «Editar» tocado
- **WHEN** se vacía la celda REFRIGERIO de «Persona sintética 01» y se guarda
- **THEN** la fila dice «—» en REFRIGERIO y sus opciones habilitadas son sólo las de ALMUERZO

## ADDED Requirements

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
