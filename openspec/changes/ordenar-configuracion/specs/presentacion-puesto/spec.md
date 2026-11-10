## MODIFIED Requirements

### Requirement: Estilo visual configurable

La pestaña «Identidad visual» de Configuración SHALL ofrecer la sección «Estilo visual»: la opacidad de las superficies, de 72 % a 88 %, y el desenfoque del fondo detrás de ellas, de 0 a 12 px, que MUST previsualizarse al moverlos. Su «Guardar» MUST conservarlos en la carpeta de datos y aplicarlos al volver a abrir el puesto; cerrar Configuración sin guardarlos, con «Cerrar» o Escape, MUST volver a los valores de antes. A Configuración se entra según el modo del puesto.

#### Scenario: Guardar y volver a abrir
Origen: pedido de David del 03/11/2026 («que en la etapa de configuración estén esos mismos controles … como parámetros»).
- **GIVEN** Configuración abierta con los valores por defecto
- **WHEN** se fija la opacidad en 84 % y el desenfoque en 8 px, se toca «Guardar», y el puesto se cierra y se vuelve a abrir
- **THEN** la pantalla usa 84 % y 8 px

#### Scenario: Cancelar descarta la vista previa
Origen: pedido de David del 03/11/2026 (vista previa con guardar y cancelar) y su hallazgo del 10/11/2026 («debería ser Cerrar»).
- **GIVEN** los valores guardados 76 % y 4 px y Configuración abierta
- **WHEN** se mueven a 88 % y 12 px y se toca «Cerrar», o se pulsa Escape
- **THEN** la pantalla vuelve a 76 % y 4 px y el archivo guardado no cambió

### Requirement: Pantalla de identificación que invita a comer

En reposo, la pantalla de identificación SHALL titularse con una frase que invite a disfrutar la comida, «¡Hola! Identificate y disfrutá tu comida», en lugar de «Identificate para comenzar». Con el modo simulación activo, el combo MUST ir arriba del campo del código y ocupar el mismo ancho que la fila del campo con «Ingresar», con sus bordes izquierdo y derecho alineados con los de esa fila, la misma letra y el mismo relleno que el campo. «Ingresar» MUST ir a la derecha del campo, en la misma fila, y el combo, el campo e «Ingresar» MUST medir al menos 56 px CSS de alto.

#### Scenario: El título invita a disfrutar la comida
Origen: mejora 1 de David del 10/11/2026 («el cartel debe siempre invitar a disfrutar una comida, así que en vez de Identificate para comenzar, diseñá una frase más amigable»).
- **GIVEN** el puesto en reposo
- **WHEN** se muestra la pantalla de identificación
- **THEN** su título es «¡Hola! Identificate y disfrutá tu comida» y la pantalla no contiene «Identificate para comenzar»

#### Scenario: El combo arriba y «Ingresar» a la derecha del campo
Origen: mejora 1 de David del 10/11/2026 («arriba de la opción de ingreso manual. La opción manual debe tener el botón ingresar a su derecha») y la decisión de David del 03/11/2026 (UX táctil, controles de al menos 56 px).
- **GIVEN** el modo simulación activo y la ventana con un área de 1024 × 768
- **WHEN** se muestra la pantalla de identificación
- **THEN** el combo está entero por encima del campo del código, y «Ingresar» está a la derecha del campo, en su misma fila
- **AND** el combo, el campo e «Ingresar» miden al menos 56 px de alto

#### Scenario: El combo alineado con la fila del código
Origen: hallazgo de David del 10/11/2026 («El combo de Elegí una persona tiene un tamaño diferente al de ingresar el código que está debajo, y no entiendo esa asimetría») y la regla de prolijidad visual (decisión 14 del plan del cierre final), borde: la tolerancia de 1 px.
- **GIVEN** el modo simulación activo y la ventana con un área de 1024 × 768
- **WHEN** se miden el combo, el campo del código e «Ingresar»
- **THEN** el borde izquierdo del combo coincide con el del campo y su borde derecho con el de «Ingresar», con 1 px de tolerancia
- **AND** el texto del combo y el del campo tienen el mismo tamaño de letra y el mismo relleno izquierdo

#### Scenario: Sin simulación, el campo e «Ingresar» siguen en una fila
Origen: mejora 1 de David del 10/11/2026 y decisión 6 de David del 09/11/2026 (con el modo apagado queda sólo el código tipeado), borde.
- **GIVEN** el puesto en mantenimiento con la simulación apagada
- **WHEN** se muestra la pantalla de identificación
- **THEN** no hay combo, y «Ingresar» está a la derecha del campo del código, en su misma fila

## ADDED Requirements

### Requirement: Listas desplegables dentro de la ventana del puesto

El combo de la simulación y los filtros Persona, Servicio y Opción de Consumos SHALL ser listas dibujadas por la página, dentro de la ventana del puesto, nunca un `<select>` nativo. Tocar el control MUST abrir su lista debajo, con filas de al menos 56 px; elegir una fila MUST cerrarla y aplicar la elección; Escape o un toque afuera MUST cerrarla sin cambiar nada. Las flechas, Inicio, Fin y Enter MUST operarla con el teclado.

#### Scenario: La lista se abre en el monitor del puesto
Origen: hallazgo de David del 10/11/2026 («Cuando tengo elegido el monitor alternativo, la lista desplegable de "Elegí a una persona" se muestra en el monitor principal»).
- **GIVEN** el modo simulación activo y la ventana con un área de 1024 × 768
- **WHEN** se toca el combo
- **THEN** su lista es parte de la página, entera dentro de la ventana, debajo del combo, con una fila de al menos 56 px por persona

#### Scenario: Elegir en la lista identifica a la persona
Origen: decisión 5 de David del 09/11/2026 («un combo con todas las personas cargadas que identifica al elegir, sin otro paso») y AT01.
- **GIVEN** el modo simulación activo y la lista del combo abierta
- **WHEN** se toca el nombre de la persona de `0001`
- **THEN** la lista se cierra y la pantalla muestra su nombre y el catálogo, sin que se haya tocado «Ingresar»

#### Scenario: Escape o un toque afuera cierran sin elegir
Origen: decisión de David del 03/11/2026 (UX táctil, criterio 5: «conservar teclado y foco visible»), borde.
- **GIVEN** la lista del combo abierta
- **WHEN** se pulsa Escape, o se toca fuera de la lista
- **THEN** la lista se cierra, el puesto sigue en reposo y no hay ninguna identificación nueva

#### Scenario: Con el teclado
Origen: decisión de David del 03/11/2026 (UX táctil, criterio 5), borde.
- **GIVEN** el foco en el filtro Persona de Consumos
- **WHEN** se pulsa Enter, la flecha abajo dos veces y Enter otra vez
- **THEN** el filtro queda en la segunda persona de la lista y la lista se cierra con el foco en el filtro

#### Scenario: Ningún select nativo
Origen: hallazgo de David del 10/11/2026 (la lista nativa se abre en otro monitor), borde.
- **GIVEN** el modo simulación activo y Configuración abierta en Consumos
- **WHEN** se buscan elementos `select` en la pantalla de identificación y en la pestaña Consumos
- **THEN** no hay ninguno
