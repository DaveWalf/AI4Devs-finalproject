## ADDED Requirements

### Requirement: Cada consumo en un renglón

A 1024 × 768 o más, cada consumo de la tabla SHALL ocupar un solo renglón, sin partir ninguna celda; la persona y la opción que no entran en su columna MUST cortarse con «…», con letra de entre 14 y 16 px. Tocar la fecha y hora, la persona o la opción, o dejar el mouse encima medio segundo, MUST mostrar en un globo ese dato entero, la fecha con el año, sin el código; MUST cerrarse con otro toque, al salir el mouse o a los 5 s.

#### Scenario: Un renglón a 1024 × 768
Origen: mejora 2 de David del 11/11/2026 («ver toda la información en un mismo renglón») y AT45, con la captura 15 de ese día.
- **GIVEN** la pantalla de 1024 × 768 y tres consumos de las últimas 24 horas, uno de «Persona sintética 01» con «Almuerzo con gaseosa» por ARS 2.000,00 y un original fallido con «Recuperar»
- **WHEN** se abre la pestaña Consumos
- **THEN** cada fila mide lo mismo que una celda de un solo renglón, con «Ver», «Reimprimir» y, en la fallida, «Recuperar» en esa misma fila, y la letra de la tabla mide entre 14 y 16 px

#### Scenario: Un nombre que no entra
Origen: respuesta 1 de David al gate del 11/11/2026 («cortar con "…"») y AT45, borde: un nombre arbitrariamente largo.
- **GIVEN** un consumo de una persona de 60 caracteres, con una opción de 40
- **WHEN** se abre la pestaña Consumos a 1024 × 768
- **THEN** la fila sigue en un renglón, la persona y la opción terminan en «…», y el texto de la celda sigue entero para la búsqueda, el orden y la exportación

#### Scenario: El dato completo al tocarlo
Origen: respuesta 1 de David al gate del 11/11/2026 («un tooltip con la información completa al tocar cualquiera de esos datos»).
- **GIVEN** la fila del consumo de la persona de 60 caracteres
- **WHEN** se toca su persona
- **THEN** un globo muestra el nombre entero, y otro toque lo cierra

#### Scenario: El dato completo con el mouse
Origen: respuesta 1 de David al gate del 11/11/2026 («ante la permanencia de un eventual mouse sobre ellos»), borde.
- **GIVEN** la tabla con un consumo del 11/11/2026 a las 13:51
- **WHEN** el mouse queda medio segundo sobre su fecha y hora, y después sale
- **THEN** el globo muestra «11/11/2026 13:51» mientras el mouse está encima, y se cierra al salir

#### Scenario: El globo no queda abierto
Origen: D8 de `configurar-puesto-por-pestanas` (lo que nadie atiende se cierra solo), borde.
- **GIVEN** el globo abierto con la opción de un consumo
- **WHEN** pasan 5 s sin un toque
- **THEN** el globo se cierra y la tabla sigue como estaba

#### Scenario: El globo no muestra el código
Origen: RB25 («no registrar el código en claro») y `AGENTS.md`, borde.
- **GIVEN** la tabla consultada con el código `0001`
- **WHEN** se toca la persona de una fila
- **THEN** el globo muestra el nombre y no contiene `0001`
