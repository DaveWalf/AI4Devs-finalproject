## MODIFIED Requirements

### Requirement: Identificación simulada por código

Al tipear un código y enviarlo con «Ingresar» o con Enter, el puesto SHALL resolverlo por tipo `SERIAL_CODE` y valor normalizado, sin espacios alrededor y con sus ceros iniciales (RB01), contra el snapshot activo (RB25). Un código vigente MUST abrir una sesión de identidad de consumo con esa credencial y mostrar la selección. En este corte el código solo abre la sesión, sin huella (identificación simulada de D-C24). El puesto MUST NOT mostrar el código después de enviarlo.

#### Scenario: Código vigente abre la selección
Origen: AT52, la parte sin huella, y AT01.
- **GIVEN** una persona activa con el código sintético `0001`, vigente y sin revocar
- **WHEN** tipea `0001` y lo envía
- **THEN** existe una sesión de identidad de propósito `CONSUMPTION` con esa persona y esa credencial
- **AND** la pantalla muestra el nombre visible de la persona y el catálogo, el campo del código queda vacío y no se escribió ninguna intención

#### Scenario: Ceros iniciales y espacios alrededor
Origen: RB01 («conservar ceros iniciales») y RB25 («valor normalizado»).
- **GIVEN** el código `0001` en el snapshot activo y ningún código `1`
- **WHEN** se envía `1`, y después se envía `0001` con espacios antes y después
- **THEN** `1` no se resuelve y `0001` con espacios abre la sesión de la persona de `0001`

#### Scenario: Enter envía el código
Origen: decisión 5 de David del 09/11/2026, en `IDSerialSim/AGENTS.md` («el ingreso manual del código confirmado con Enter»), y AT01.
- **GIVEN** la pantalla de identificación y `0001` vigente
- **WHEN** se tipea `0001` y se pulsa Enter en el campo, sin tocar «Ingresar»
- **THEN** existe una sesión de identidad de propósito `CONSUMPTION` con la persona de `0001`
- **AND** la pantalla muestra su nombre y el catálogo, y el campo del código queda vacío

#### Scenario: Enter rechaza igual que «Ingresar» y una sola vez
Origen: AT52 y RB25 (causa interna `UNKNOWN_CODE`), borde: una tecla no envía dos identificaciones.
- **GIVEN** la pantalla de identificación y ningún código `7777` en el snapshot activo
- **WHEN** se tipea `7777` y se pulsa Enter en el campo
- **THEN** la pantalla muestra el mensaje de `IDENTITY_NOT_RESOLVED`, sigue en reposo con el campo vacío y no se abrió ninguna sesión
- **AND** la bitácora tiene un solo registro de identificación rechazada nuevo, con la causa interna `UNKNOWN_CODE` y sin `7777`

## ADDED Requirements

### Requirement: Identificación eligiendo a la persona en modo simulación

Con el modo simulación activo (`acceso-puesto`), la pantalla de identificación SHALL mostrar, arriba del campo del código, un combo con el nombre visible de cada credencial de código del snapshot activo, en orden alfabético. Elegir una entrada MUST identificar a esa persona en el acto, sin «Ingresar», con las reglas, los rechazos y los mensajes del código tipeado (RB25). El combo MUST NOT mostrar ni llevar ningún código. Sin el modo activo, MUST NOT existir.

#### Scenario: Elegir a una persona abre la selección
Origen: decisión 5 de David del 09/11/2026 («un combo con todas las personas cargadas que identifica al elegir, sin otro paso») y AT01.
- **GIVEN** el modo simulación activo y la persona de `0001`, vigente y con cupo libre
- **WHEN** se elige su nombre en el combo
- **THEN** existe una sesión de identidad de propósito `CONSUMPTION` con esa persona y esa credencial, sin que se haya tocado «Ingresar»
- **AND** la pantalla muestra su nombre y el catálogo, y su registro de sesión abierta dice que la identificación vino de la lista

#### Scenario: Una credencial vencida elegida en el combo
Origen: AT52 («vencido: CREDENTIAL_EXPIRED») y la decisión 5 de David del 09/11/2026 (el combo identifica como el código).
- **GIVEN** el modo simulación activo y la persona de `0002`, cuya credencial está vencida
- **WHEN** se elige su nombre en el combo
- **THEN** la pantalla muestra el mensaje de `CREDENTIAL_EXPIRED` y sigue en reposo, con el combo sin ninguna persona elegida
- **AND** no existe ninguna sesión nueva, y el registro de identificación rechazada tiene `CREDENTIAL_EXPIRED` y dice que vino de la lista

#### Scenario: Sin el modo simulación no hay combo
Origen: decisión 6 de David del 09/11/2026 («con el modo apagado queda sólo el código tipeado»), borde.
- **GIVEN** el puesto en mantenimiento con la simulación apagada en General
- **WHEN** se muestra la pantalla de identificación
- **THEN** tiene el campo del código y «Ingresar», y ningún combo

#### Scenario: El combo no muestra ni lleva códigos
Origen: RB25, D10 de `administrar-personas-y-opciones` («el panel nunca muestra un código») y `AGENTS.md` (el código no se escribe en logs, tickets ni eventos), borde.
- **GIVEN** el modo simulación activo con las ocho personas sembradas, de `0001` a `0004` y de `9001` a `9004`
- **WHEN** se revisan el texto y el valor de cada entrada del combo
- **THEN** el texto de cada entrada es un nombre visible sin ninguno de los ocho códigos, y su valor identifica una credencial sin ser un código

#### Scenario: Una credencial que no está en la versión activa
Origen: RB25 (causa interna `UNKNOWN_CODE`) y `AGENTS.md` (OWASP: lo que llega desde la pantalla es entrada externa), borde.
- **GIVEN** el modo simulación activo
- **WHEN** llega desde la pantalla la elección de una credencial que no existe en el snapshot activo
- **THEN** el motivo es `IDENTITY_NOT_RESOLVED` con la causa interna `UNKNOWN_CODE`, sin sesión nueva y sin `PersonId` en el registro del rechazo

#### Scenario: La lista sigue a la versión activa
Origen: `AGENTS.md` («cada guardado es una versión local») y la decisión 5 de David del 09/11/2026 («todas las personas cargadas»), borde.
- **GIVEN** el modo simulación activo
- **WHEN** se agrega «Persona sintética 05» en Personas, se guarda y se cierra Configuración
- **THEN** el combo de la pantalla de identificación la incluye, en su lugar alfabético
