## ADDED Requirements

### Requirement: Un teclado en pantalla para cada campo de texto

Al tocar con el dedo un campo de texto de Configuración o el código de la pantalla de reposo, el puesto SHALL mostrar abajo un teclado en pantalla con números, letras con la Ñ, «Mayús», espacio, borrar y los símbolos `@ . - _ : / \`, que escribe en ese campo sin sacarle el foco y lo deja a la vista. Los campos con un teclado propio (la clave del día y Personas) MUST NOT mostrarlo.

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

### Requirement: Las ayudas desactivables por sesión de Configuración

El teclado en pantalla de Configuración SHALL ofrecer «Ocultar teclado»: oculto, MUST NOT volver a aparecer hasta que Configuración se cierre, y al abrirla otra vez MUST estar de nuevo. En la pantalla de reposo no se puede ocultar.

#### Scenario: Ocultar el teclado con un teclado físico
Origen: mejora 8b de David del 10/11/2026 («con las ayudas desactivables por sesión de Configuración»).
- **GIVEN** Configuración abierta y el teclado en pantalla a la vista en un campo
- **WHEN** se toca «Ocultar teclado» y después otro campo
- **THEN** el teclado no aparece; al cerrar Configuración, volver a abrirla y tocar un campo, aparece otra vez
