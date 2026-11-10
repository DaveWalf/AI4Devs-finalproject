## MODIFIED Requirements

### Requirement: Aspecto táctil del puesto

Las pantallas del puesto SHALL mostrarse sobre el fondo ilustrado de cocina, con las opciones y las acciones como superficies translúcidas cuyo texto MUST quedar opaco. La fila entera de una opción MUST ser tocable, la opción elegida MUST indicarse también con el texto «Elegida» y no sólo con el color, y ninguna función MUST depender de pasar el puntero por encima. «Cancelar», «Confirmar», «Elegir otra» y cada opción MUST medir al menos 56 px CSS de alto.

#### Scenario: La opción elegida se marca con texto
Origen: decisión de David del 03/11/2026 (UX táctil, criterios 2 y 3) y TK02.1.
- **GIVEN** una persona en la selección, sin opción elegida
- **WHEN** toca una opción
- **THEN** esa opción figura como presionada y muestra el texto «Elegida»
- **AND** ninguna otra opción muestra «Elegida»

#### Scenario: Elegir otra opción mueve la marca
Origen: decisión de David del 03/11/2026 (UX táctil, criterio 3: «selección separada de confirmación») y mejora 2 de David del 10/11/2026 («volver a seleccionar otra opción»).
- **GIVEN** una persona con una opción elegida, el cartel cerrado con «Elegir otra» y sin confirmar
- **WHEN** toca otra opción
- **THEN** sólo la nueva muestra «Elegida», el cartel se abre con ella y no se escribió ninguna intención

#### Scenario: Nueve opciones y las acciones entran en 1024 × 768
Origen: decisión de David del 02/11/2026 (una misma pantalla, regla 4), del 03/11/2026 (UX táctil, criterio 1) y mejora 2 de David del 10/11/2026 («así tenemos más lugar para selecciones»).
- **GIVEN** el catálogo de nueve opciones y la ventana con un área de 1024 × 768
- **WHEN** se muestra la selección y, después, se toca una opción
- **THEN** las nueve opciones y «Cancelar» se ven sin desplazar la pantalla, y con el cartel abierto se ven «Confirmar» y «Elegir otra»
- **AND** cada una mide al menos 56 px de alto

### Requirement: Cabecera con logos y el engranaje en el pie

La cabecera SHALL mostrar el logo de la empresa, el nombre del servicio y el logo de la concesionaria, y MUST NOT tener controles. El pie MUST mostrar el estado de sincronización y, a la derecha, la marca Intelektron, discreta. En reposo, el engranaje de Configuración MUST ir en el pie, en el borde derecho y al lado de la marca, con el nombre accesible «Configuración» y un área de toque de al menos 56 px; durante una atención MUST NOT mostrarse.

#### Scenario: El engranaje en el borde inferior derecho
Origen: pedido de David del 04/11/2026 («que el botón de configuración esté en el borde inferior derecho, al lado del logo de Intelektron»).
- **GIVEN** el puesto en reposo, con un área de 1024 × 768
- **WHEN** se revisan la cabecera y el pie
- **THEN** la cabecera no tiene botones y el pie tiene uno solo, con el nombre «Configuración»
- **AND** ese botón queda a la derecha de la marca Intelektron, con su borde derecho y su borde inferior a no más de 16 px de los de la ventana, y mide al menos 56 × 56 px

#### Scenario: Sin engranaje durante una atención
Origen: AT16 («siguiente persona inicia limpia») y RB05: Configuración no se abre con una persona en curso.
- **GIVEN** una persona identificada en la selección
- **WHEN** se revisan la cabecera y el pie
- **THEN** no hay ningún botón «Configuración», y la cabecera sigue sin botones aunque «Cancelar» esté en la selección

#### Scenario: El pie no cambia de alto al salir de reposo
Origen: «zonas de acción estables» de los criterios táctiles de David del 03/11/2026.
- **GIVEN** el puesto en reposo, con el catálogo de nueve opciones y un área de 1024 × 768
- **WHEN** una persona se identifica y se muestra la selección
- **THEN** el pie mide lo mismo que en reposo
- **AND** las nueve opciones y «Cancelar» se siguen viendo sin desplazar la pantalla

### Requirement: Sin rebote al arrastrar

Arrastrar el dedo en cualquier pantalla del puesto SHALL NOT mover la página más allá de sus bordes ni hacerla rebotar: la página MUST impedirlo con su estilo y el navegador del puesto MUST arrancar con el rebote elástico apagado. Las zonas que se desplazan, como la selección en la ventana angosta y las tablas de Configuración, MUST seguir desplazándose dentro de sus límites.

#### Scenario: La página no rebota
Origen: hallazgo de David del 04/11/2026 en su monitor táctil («arrastrar el dedo hace rebotar la pantalla») y los criterios táctiles del 03/11/2026 («zonas de acción estables»).
- **GIVEN** el puesto en reposo, en la atención y con Configuración abierta
- **WHEN** se revisa cómo responde la página a un arrastre más allá de su borde
- **THEN** la página y su contenedor principal no admiten desplazarse más allá del borde ni encadenar el desplazamiento

#### Scenario: El navegador arranca sin rebote elástico
Origen: decisión de David del 06/11/2026 (gate de D, punto 9: «Rebote: CSS y el argumento de Chromium desde ya»).
- **GIVEN** el puesto sin el puerto de depuración de las pruebas y, otra vez, con él
- **WHEN** se arman los argumentos de su navegador
- **THEN** los dos llevan `--disable-features=ElasticOverscroll`, y el segundo además el puerto de depuración
- **AND** el proceso del navegador del puesto abierto arrancó con ese argumento

#### Scenario: La ventana angosta sigue desplazándose
Origen: «Ventana angosta» de este mismo spec, borde: quitar el rebote no quita el desplazamiento, y mejora 2 de David del 10/11/2026 (el cartel de la elección).
- **GIVEN** el catálogo de nueve opciones y un área de 600 px de ancho
- **WHEN** se desplaza la selección y se toca la última opción
- **THEN** se llega a la última opción, y el cartel muestra «Confirmar» dentro de la ventana sin desplazar más
