## ADDED Requirements

### Requirement: El medio de identificación

La pestaña Lector SHALL elegir el medio: «Teclado», «Simulador interno» o «Puerto serie» (puerto, velocidad, bits de datos, paridad y parada), y para los dos últimos el terminador (CR, LF, CR LF o TAB) y el relleno de dígitos (0 a 20). «Guardar» MUST rechazar lo que no tiene forma con su campo señalado y registrar `CONFIGURATION_CHANGED` de `READER`. Por defecto, «Teclado»: el puesto se comporta como sin lector.

#### Scenario: Elegir el puerto serie
Origen: mejora 9a de David del 10/11/2026 («un puerto serie con terminadores y relleno de dígitos»).
- **GIVEN** la pestaña Lector
- **WHEN** se elige «Puerto serie» en `COM3` a 9600 8N1 con el terminador CR y el relleno 4, y se toca «Guardar»
- **THEN** la pestaña avisa lo guardado, `lector.json` lo tiene y la general registra `CONFIGURATION_CHANGED` de `READER`

#### Scenario: Un puerto fuera de forma
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** la pestaña Lector con «Puerto serie»
- **WHEN** se intenta guardar sin puerto y con el relleno 25
- **THEN** los dos campos se señalan y la configuración no cambia

### Requirement: Una lectura identifica como el código tipeado

En la pantalla de reposo y sin un diálogo abierto, una lectura completa SHALL identificar a la persona por el mismo camino que el código tipeado con «Ingresar»: mismas reglas, rechazos y mensajes. Una lectura de sólo dígitos más corta que el relleno MUST completarse con ceros a la izquierda.

#### Scenario: La tarjeta de 0001
Origen: mejora 9a de David del 10/11/2026 y HU01.2 (escenario J).
- **GIVEN** el medio «Puerto serie» con el terminador CR y el relleno 4, y el puesto en reposo
- **WHEN** el lector manda `1` y CR
- **THEN** el puesto identifica a la persona de `0001` y ofrece sus opciones, como si se hubiera tipeado `0001`

#### Scenario: Una credencial vencida por el lector
Origen: RB25, borde.
- **GIVEN** el medio «Puerto serie» y el puesto en reposo
- **WHEN** el lector manda `0002` con su terminador
- **THEN** la pantalla dice «Esta credencial está vencida», como con el código tipeado

### Requirement: El armado de las tramas

El lector SHALL armar las lecturas sobre bytes hasta el terminador elegido: una trama partida se arma, varias en un bloque se separan, y una con un byte de control, fuera de ASCII, con espacios internos o de más de 32 caracteres se descarta entera. Un resto sin terminar de más de 2 segundos MUST descartarse antes del byte siguiente, sin concatenar identidades.

#### Scenario: Trama partida y dos tramas juntas
Origen: AT07 («trama partida, múltiples tramas»).
- **GIVEN** el terminador CR LF
- **WHEN** llegan `00`, después `01` y CR LF, y después `0003` CR LF `0004` CR LF en un solo bloque
- **THEN** salen tres lecturas, `0001`, `0003` y `0004`, en ese orden

#### Scenario: Un resto viejo no se pega
Origen: AT07 («ruido y exceso de tamaño»), borde: sin concatenación de identidades.
- **GIVEN** el terminador CR
- **WHEN** llega `00`, pasan 3 segundos y llega `01` y CR
- **THEN** la única lectura es `01`, y `00` queda descartado como un resto viejo

### Requirement: Fuera de la identificación la lectura se descarta

Durante una atención, con un diálogo abierto o con el error de almacenamiento, una lectura SHALL descartarse sin identificar a nadie, sin aviso en la pantalla y sin quedar en cola. La general MUST registrar `READER_READING_DISCARDED` con el motivo, a lo sumo una vez por motivo y por minuto, sin el código ni su largo.

#### Scenario: Una lectura durante la selección
Origen: decisión de David del 09/11/2026 («en cualquier otra pantalla se descarta, sin cola»).
- **GIVEN** una persona eligiendo su opción
- **WHEN** el lector manda otro código
- **THEN** la selección sigue igual, nadie más queda identificado y la general registra `READER_READING_DISCARDED` con `NOT_IDLE`

### Requirement: El simulador interno

Con el medio «Simulador interno» y el puesto en mantenimiento, la pantalla de reposo SHALL mostrar «Lector simulado», que manda lo tipeado y el terminador como bytes al mismo armado que el puerto real. En operativo MUST NOT mostrarse.

#### Scenario: Pasar un código por el lector simulado
Origen: mejora 9a de David del 10/11/2026 («simulador interno») y decisión 16 del plan (sin IDSerialSim).
- **GIVEN** el medio «Simulador interno» con el relleno 4 y el puesto en mantenimiento y en reposo
- **WHEN** se tipea `1` en «Lector simulado» y se toca «Pasar por el lector»
- **THEN** el puesto identifica a la persona de `0001`, y la última lectura de la pestaña dice que identificó, sin el código

### Requirement: El puerto que falta o se cae

Un puerto que no existe, está ocupado o se desconecta SHALL dejar la atención por el teclado como siempre; el lector MUST reintentar solo cada 5 segundos, la pestaña decir su estado y la general registrar `READER_STATE_CHANGED` en cada cambio, sin el código.

#### Scenario: El puerto vuelve
Origen: RF23, borde: un lector USB que se desenchufa.
- **GIVEN** el medio «Puerto serie» en un puerto que se cae
- **WHEN** pasan 5 segundos y el puerto vuelve
- **THEN** el estado pasa de «se cayó» a «escuchando» y la general tiene los dos cambios
