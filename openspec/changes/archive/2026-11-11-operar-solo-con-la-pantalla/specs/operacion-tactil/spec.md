## ADDED Requirements

### Requirement: Un teclado en pantalla para cada campo de texto

Al tocar con el dedo un campo de texto de Configuración o el código de la pantalla de reposo, el puesto SHALL mostrar abajo un teclado en pantalla con números, letras con la Ñ, «Mayús», espacio, borrar y los símbolos `@ . - _ : / \`, que escribe en ese campo como un teclado físico, sin sacarle el foco, y lo deja a la vista. Los campos con un teclado propio (la clave del día y Personas) MUST NOT mostrarlo.

#### Scenario: Una casilla sólo con la pantalla
Origen: mejora 8b de David del 10/11/2026 («todo operable sólo con la pantalla»).
- **GIVEN** la pestaña Mails abierta
- **WHEN** se toca el campo del remitente y se tocan las teclas `p`, `@`, `e`, `.`, `c`, `o` y `m`
- **THEN** el campo dice `p@e.com`, como si se hubiera tipeado, y sigue con el foco

#### Scenario: Con un mouse no aparece
Origen: D2 de este change, borde: quien tiene un mouse o un teclado físico no necesita el teclado en pantalla.
- **GIVEN** la pestaña Mails abierta
- **WHEN** se hace clic con el mouse en el campo del remitente
- **THEN** no aparece el teclado en pantalla y el campo se escribe con el teclado físico, como siempre

#### Scenario: El código en la pantalla de reposo
Origen: mejora 8b de David del 10/11/2026, borde: el comensal no tiene teclado físico.
- **GIVEN** el puesto en reposo
- **WHEN** se toca el campo del código, se tocan `0`, `0`, `0`, `1` y después «Ingresar»
- **THEN** el puesto identifica a la persona de `0001`

#### Scenario: Lo escrito con las teclas se guarda
Origen: verificación de este change, borde: Opciones lee cada campo al salir de él, no en cada tecla.
- **GIVEN** las opciones del comedor en edición
- **WHEN** se toca el nombre de una opción, se le suma un `2` con las teclas y se toca «Guardar»
- **THEN** se guarda el nombre con el `2`, que se lee al abrir las opciones otra vez

#### Scenario: Un campo numérico recibe sólo dígitos
Origen: verificación de este change, borde: un campo numérico se vacía con un valor que no es un número.
- **GIVEN** las unidades de una carga en `1`
- **WHEN** se tocan `A`, `.` y `2`
- **THEN** el campo dice `12`

#### Scenario: Los teclados propios siguen
Origen: mejora 8b de David del 10/11/2026 y gate de este change (opción 5), borde: la clave del día y Personas ya tienen su teclado.
- **GIVEN** la clave del día pedida, o las personas en edición
- **WHEN** se toca con el dedo su campo
- **THEN** el teclado general no aparece

#### Scenario: El código leído al salir del reposo
Origen: verificación de este change, borde: el campo del código sale de la pantalla sin que se toque otra cosa.
- **GIVEN** el teclado en pantalla a la vista en el código de la pantalla de reposo
- **WHEN** una lectura identifica a la persona
- **THEN** el teclado se va con el reposo y, al volver, aparece recién con otro toque

### Requirement: Las ayudas desactivables por sesión de Configuración

El teclado en pantalla de Configuración SHALL ofrecer «Ocultar teclado»: oculto, MUST NOT volver a aparecer hasta que Configuración se cierre, y al abrirla otra vez MUST estar de nuevo. En la pantalla de reposo no se puede ocultar.

#### Scenario: Ocultar el teclado con un teclado físico
Origen: mejora 8b de David del 10/11/2026 («con las ayudas desactivables por sesión de Configuración»).
- **GIVEN** Configuración abierta y el teclado en pantalla a la vista en un campo
- **WHEN** se toca «Ocultar teclado» y después otro campo
- **THEN** el teclado no aparece; al cerrar Configuración, volver a abrirla y tocar un campo, aparece otra vez

#### Scenario: En reposo no hay «Ocultar teclado»
Origen: mejora 8b de David del 10/11/2026 y gate de este change (opción 3), borde: el comensal no tiene un teclado físico.
- **GIVEN** el puesto en reposo
- **WHEN** se toca el campo del código
- **THEN** el teclado aparece sin «Ocultar teclado»
