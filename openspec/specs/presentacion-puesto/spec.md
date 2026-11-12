# presentacion-puesto Specification

## Purpose
Cómo se ve y se adapta el puesto en una pantalla táctil: el aspecto, la cabecera con los logos, el engranaje en el pie, la ventana y el estilo visual configurable, sin cambiar lo que la atención decide ni registra.

## Requirements

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

### Requirement: Logos locales de la empresa y la concesionaria

El puesto SHALL mostrar como logos los archivos `marcas\empresa` y `marcas\concesionaria` de su carpeta de datos, en PNG, WebP o SVG de hasta 2 MB, con su transparencia y sus proporciones. Sin archivo, o con uno que no cumple, MUST mostrar el logo de ejemplo y seguir atendiendo. MUST NOT buscar logos en la red, y un SVG MUST mostrarse como imagen, nunca insertado en la página.

#### Scenario: Logo local de la empresa
Origen: decisión de David del 03/11/2026 (perfil gráfico, reglas 3 y 4) y `AGENTS.md` («cliente anonimizado»: el logo real no está en el repo).
- **GIVEN** un `marcas\empresa.png` válido de 40 KB en la carpeta de datos, y ningún archivo de la concesionaria
- **WHEN** el puesto arranca
- **THEN** la cabecera muestra esa imagen como logo de la empresa
- **AND** el logo de la concesionaria es el de ejemplo

#### Scenario: Archivo de más de 2 MB
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto).
- **GIVEN** un `marcas\concesionaria.webp` de 2 MB más un byte
- **WHEN** el puesto arranca
- **THEN** la cabecera muestra el logo de ejemplo de la concesionaria y la atención funciona

#### Scenario: Contenido que no corresponde a la extensión
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto).
- **GIVEN** un `marcas\empresa.png` cuyo contenido es texto
- **WHEN** el puesto arranca
- **THEN** la cabecera muestra el logo de ejemplo de la empresa

#### Scenario: Un SVG con un script se muestra como imagen
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto).
- **GIVEN** un `marcas\empresa.svg` que contiene un elemento `script`
- **WHEN** el puesto arranca
- **THEN** el logo se muestra como el contenido de una imagen y la página no contiene ese elemento

### Requirement: Ventana maximizada que se adapta

Al abrir, la ventana del puesto SHALL quedar maximizada a pantalla completa, sin bordes ni barra de título y tapando la barra de tareas, en los dos modos, y la pantalla MUST ocupar su área. Cuando el ancho no admite dos columnas con el texto entero y las áreas de toque mínimas, las opciones MUST ir en una columna con desplazamiento vertical, sin achicar el texto ni los botones.

#### Scenario: Abre maximizada
Origen: decisión de David del 03/11/2026 (pantalla y configuración, punto 2: «deberá permanecer maximizada»).
- **GIVEN** el puesto cerrado
- **WHEN** se abre
- **THEN** la ventana está maximizada

#### Scenario: Pantalla completa sin bordes
Origen: decisión de David del 06/11/2026 (gate de D, punto 7: «pantalla completa sin bordes, en los dos modos»), que gana sobre «deberá permanecer maximizada» del 03/11/2026.
- **GIVEN** el puesto cerrado, en mantenimiento y, otra vez, en operativo
- **WHEN** se abre
- **THEN** la ventana no tiene bordes ni barra de título y su rectángulo es el del monitor entero, con la barra de tareas tapada

#### Scenario: Ventana angosta
Origen: decisión de David del 02/11/2026 (regla 4: la excepción queda explícita y no se consigue encogiendo los controles).
- **GIVEN** el catálogo de nueve opciones y un área de 600 px de ancho
- **WHEN** se muestra la selección
- **THEN** las nueve opciones van en una sola columna, cada una exactamente una vez, y la pantalla se desplaza en vertical
- **AND** el texto de las opciones tiene el mismo tamaño que con 1024 px de ancho

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

### Requirement: Estilo por defecto y validado

Sin valores guardados, o con un archivo de estilo ilegible o con algún valor fuera de rango, el puesto SHALL usar 76 % de opacidad y 4 px de desenfoque y MUST seguir atendiendo. Con cualquier valor del rango, el texto MUST quedar opaco.

#### Scenario: Sin estilo guardado
Origen: decisión de David del 03/11/2026 («aplicar 76 % de opacidad y 4 px de desenfoque»).
- **GIVEN** una carpeta de datos sin archivo de estilo
- **WHEN** el puesto arranca
- **THEN** usa 76 % y 4 px, y el texto de las opciones es opaco

#### Scenario: Archivo fuera de rango
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto).
- **GIVEN** un archivo de estilo con opacidad 150 y desenfoque -3
- **WHEN** el puesto arranca
- **THEN** usa 76 % y 4 px y atiende normalmente

### Requirement: Cabecera con logos y el engranaje en el pie

La cabecera SHALL mostrar el logo de la empresa, el nombre del servicio, «INComedor · tu pausa del día», y el logo de la concesionaria, y MUST NOT tener controles. El pie MUST mostrar el estado de sincronización y, a la derecha, la marca Intelektron, discreta. En reposo, el engranaje de Configuración MUST ir en el pie, en el borde derecho y al lado de la marca, con el nombre accesible «Configuración» y un área de toque de al menos 56 px; durante una atención MUST NOT mostrarse.

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

#### Scenario: El nombre del servicio en la cabecera
Origen: pedido de David del 10/11/2026 («La frase que dice "Comedor - tu pausa del día", debe hacer referencia a "INComedor - tu pausa del día"»), borde: en reposo y durante una atención.
- **GIVEN** el puesto en reposo
- **WHEN** se revisa la cabecera, y otra vez con una persona en la selección
- **THEN** el nombre del servicio dice «INComedor · tu pausa del día» las dos veces, y ninguna dice «Comedor · tu pausa del día» sin el «IN»

### Requirement: Monitor asignado y recordado

La pestaña Pantalla SHALL listar los monitores conectados con su resolución y cuál es el principal, y permitir elegir el del puesto. Al guardar, la ventana MUST pasar a pantalla completa en ese monitor, y cada vez que el puesto abra MUST volver a él. Si el monitor guardado no está conectado, o lo guardado no se puede leer, MUST abrir a pantalla completa en el principal y la pestaña MUST avisarlo.

#### Scenario: Elegir el segundo monitor
Origen: decisión de David del 03/11/2026 (pantalla y configuración, punto 2: «permanecer maximizada en el monitor a la cual se destine»).
- **GIVEN** dos monitores conectados y el puesto en el principal
- **WHEN** en Pantalla se elige el otro y se guarda
- **THEN** la ventana queda a pantalla completa en el monitor elegido

#### Scenario: Volver a abrir en el monitor guardado
Origen: decisión de David del 03/11/2026 (punto 2: «recordando la configuración del monitor asignado en caso de ser más de uno»).
- **GIVEN** el segundo monitor guardado como el del puesto
- **WHEN** el puesto se cierra y se vuelve a abrir
- **THEN** abre a pantalla completa en el segundo monitor

#### Scenario: El monitor guardado no está
Origen: nota de pantalla y configuración («queda por diseñar qué ocurre si el monitor asignado se desconecta»), borde.
- **GIVEN** un monitor guardado que ya no está conectado
- **WHEN** el puesto abre
- **THEN** abre a pantalla completa en el principal y la pestaña Pantalla dice que el monitor guardado no está conectado

#### Scenario: Un solo monitor
Origen: decisión de David del 03/11/2026 (punto 2, «en caso de ser más de uno»), borde.
- **GIVEN** un solo monitor conectado
- **WHEN** se abre la pestaña Pantalla
- **THEN** lista ese monitor como el principal y elegido, con su resolución

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

### Requirement: Ícono propio del puesto

El ejecutable y la ventana del puesto SHALL tener el ícono de INComedor, hecho con la paleta del puesto, en el archivo, el título y la barra de tareas. El ícono MUST NOT usar marcas de clientes.

#### Scenario: La ventana con su ícono
Origen: candidato del 04/11/2026 en el arnés («el ejecutable no tiene ícono propio») y la decisión de David del 06/11/2026 (3/3: «Todo lo que fui pidiendo hasta ahora»).
- **GIVEN** el puesto compilado
- **WHEN** abre
- **THEN** la ventana tiene un ícono propio y el ejecutable lleva ese mismo ícono

#### Scenario: Sin marcas de clientes
Origen: `AGENTS.md` («cliente anonimizado») y la presentación académica con cliente anonimizado del 27/09/2026, borde.
- **GIVEN** el ícono del repo
- **WHEN** se revisa su procedencia
- **THEN** está hecho con la paleta del puesto y su procedencia dice que no contiene marcas de clientes

### Requirement: Logos desde Configuración

La pestaña «Identidad visual» SHALL permitir elegir, para la empresa y para la concesionaria, un archivo PNG, WebP o SVG de hasta 2 MB, verlo sobre una cuadrícula antes de guardarlo, guardarlo como el logo de la carpeta de datos y volver al de ejemplo. Un archivo que no cumple las reglas de «Logos locales de la empresa y la concesionaria» MUST rechazarse con su motivo sin tocar el logo vigente.

#### Scenario: Elegir y guardar el logo de la empresa
Origen: decisión de David del 03/11/2026 (perfil gráfico, regla 3: «permitir reemplazar los logos de empresa y concesionaria desde Configuración»).
- **GIVEN** la pestaña Identidad visual y un PNG válido de 40 KB
- **WHEN** se lo elige como logo de la empresa, se ve sobre la cuadrícula y se guarda
- **THEN** la cabecera muestra ese logo, y al volver a abrir el puesto también

#### Scenario: Archivo de más de 2 MB
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** un WebP de 2 MB más un byte
- **WHEN** se lo elige como logo de la concesionaria
- **THEN** la pestaña dice que pasa de 2 MB y el logo vigente no cambia

#### Scenario: Contenido que no corresponde a la extensión
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** un archivo `.png` cuyo contenido es texto
- **WHEN** se lo elige como logo de la empresa
- **THEN** la pestaña dice que no es una imagen válida y no ofrece guardarlo

#### Scenario: Un SVG con un script en la vista previa
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** un SVG que contiene un elemento `script`
- **WHEN** se lo elige como logo de la empresa
- **THEN** la vista previa lo muestra como el contenido de una imagen y la página no contiene ese elemento

#### Scenario: Volver al de ejemplo
Origen: maqueta del 03/11/2026 («restaurar los ejemplos»), borde.
- **GIVEN** un logo de la empresa guardado
- **WHEN** se toca «Volver al de ejemplo» y se guarda
- **THEN** la cabecera muestra el logo de ejemplo y la carpeta de datos ya no tiene el logo de la empresa

### Requirement: Pantalla de identificación que invita a comer

En reposo, la pantalla de identificación SHALL titularse con una frase que invite a disfrutar la comida, «¡Hola! Identificate y disfrutá tu comida», en lugar de «Identificate para comenzar». Con el modo simulación activo, el combo MUST ir arriba del campo del código. «Ingresar» MUST ir a la derecha del campo, en la misma fila, y el combo, el campo e «Ingresar» MUST medir al menos 56 px CSS de alto.

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

#### Scenario: Sin simulación, el campo e «Ingresar» siguen en una fila
Origen: mejora 1 de David del 10/11/2026 y decisión 6 de David del 09/11/2026 (con el modo apagado queda sólo el código tipeado), borde.
- **GIVEN** el puesto en mantenimiento con la simulación apagada
- **WHEN** se muestra la pantalla de identificación
- **THEN** no hay combo, y «Ingresar» está a la derecha del campo del código, en su misma fila

### Requirement: Combo alineado con la fila del código

Con el modo simulación activo, el combo de la pantalla de identificación SHALL ocupar el mismo ancho que la fila del campo del código con «Ingresar», con sus bordes izquierdo y derecho alineados con los de esa fila, y MUST tener la misma letra y el mismo relleno que el campo.

#### Scenario: El combo alineado con la fila del código
Origen: hallazgo de David del 10/11/2026 («El combo de Elegí una persona tiene un tamaño diferente al de ingresar el código que está debajo, y no entiendo esa asimetría») y la regla de prolijidad visual (decisión 14 del plan del cierre final), borde: la tolerancia de 1 px.
- **GIVEN** el modo simulación activo y la ventana con un área de 1024 × 768
- **WHEN** se miden el combo, el campo del código e «Ingresar»
- **THEN** el borde izquierdo del combo coincide con el del campo y su borde derecho con el de «Ingresar», con 1 px de tolerancia
- **AND** el texto del combo y el del campo tienen el mismo tamaño de letra y el mismo relleno izquierdo

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

### Requirement: Las oraciones de la pantalla terminan en punto

Toda oración que la pantalla del puesto muestra (un aviso, un mensaje, una explicación o una indicación) SHALL terminar en punto, o en el signo que la cierra: «?» o «!», o «:» antes de lo que presenta. Un título, un rótulo, un botón, una opción o un estado MUST NOT llevar punto final.

#### Scenario: Un aviso de la atención
Origen: pedido de David del 10/11/2026 («no dejar oraciones sin el punto final donde corresponda en la interfaz»).
- **GIVEN** una credencial vencida
- **WHEN** se ingresa su código
- **THEN** la pantalla dice «Esta credencial está vencida. Solicitá asistencia», con el punto final después de «asistencia»

#### Scenario: Un título y un estado no llevan punto
Origen: pedido de David del 10/11/2026 («donde corresponda»), borde.
- **GIVEN** el puesto en reposo y la pestaña Red sin red
- **WHEN** se muestran
- **THEN** el título «¡Hola! Identificate y disfrutá tu comida» y el estado «Sin conexión de red» no terminan en punto

#### Scenario: Un mensaje nuevo sin punto
Origen: `openspec/config.yaml` (la regla de puntuación de la interfaz), borde.
- **GIVEN** los mensajes del puesto
- **WHEN** se agrega uno que es una oración sin el punto final
- **THEN** la prueba que los recorre falla y lo nombra

### Requirement: La versión del programa en el pie

En el centro del pie, el puesto SHALL mostrar la versión del paquete y el commit corto, como «v1.0.1 · 7b80d45», en letra más chica que la del pie y atenuada, en reposo y durante una atención. Sin versión de paquete, o con una fuera de forma, MUST decir «desarrollo» en su lugar, y sin commit, «sin commit». La línea MUST NOT ser un control, cambiar el alto del pie ni mover el engranaje, y la cabecera MUST NOT mostrarla.

#### Scenario: Un paquete publicado
Origen: pedido de David del 11/11/2026 («al centro de la barra que está abajo») y su respuesta 1 al gate («comenzá con v...»), con la mejora 3 del mismo día y HU03-D.
- **GIVEN** el EXE armado con el tag `v1.0.1` sobre el commit `7b80d45…`
- **WHEN** se revisa el pie en reposo, y otra vez con una persona en la selección
- **THEN** en el centro del pie dice «v1.0.1 · 7b80d45» las dos veces, y la cabecera no muestra ninguna versión

#### Scenario: Un build de desarrollo
Origen: respuesta 5 de David al gate de `mostrar-version-del-programa` («desarrollo» y el commit), borde: sin tag.
- **GIVEN** el puesto compilado desde el código, sin versión de paquete, sobre el commit `7b80d45…`
- **WHEN** se revisa el pie
- **THEN** dice «desarrollo · 7b80d45»

#### Scenario: Una versión fuera de forma
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto) y la validación del tag de `publicar-puesto.ps1`, borde.
- **GIVEN** un EXE cuyo metadato de versión tiene 80 caracteres y un salto de línea
- **WHEN** se revisa el pie
- **THEN** dice «desarrollo» y el commit corto, sin ningún carácter del metadato

#### Scenario: El pie y la cabecera no cambian de lugar
Origen: «zonas de acción estables» de los criterios táctiles de David del 03/11/2026 y el requisito «Cabecera con logos y el engranaje en el pie», borde: el texto de la sincronización cambia.
- **GIVEN** el puesto en reposo, a 1024 × 768
- **WHEN** se miden el pie y la cabecera, y otra vez con la sincronización mostrando otro estado
- **THEN** la cabecera mide 64 px y el pie lo mismo que antes, la versión queda centrada en la ventana con 1 px de tolerancia, más chica que la letra del pie, sin solaparse con la franja ni con la marca, y el engranaje sigue en el borde inferior derecho

### Requirement: El título con la letra de INWeb

El nombre del servicio de la cabecera, «INComedor · tu pausa del día», SHALL escribirse entero en Heebo Light (peso 300), la letra con que INWeb escribe su nombre, desde un archivo de fuente del propio puesto, con su licencia al lado. El puesto MUST NOT pedir la fuente a internet, y si el archivo falta, el título MUST seguir viéndose con una letra sans-serif del sistema, sin cambiar el alto de la cabecera.

#### Scenario: El título en Heebo
Origen: pedido de David del 11/11/2026 («aplicá la misma tipografía a todo ese título»), con el nombre de INWeb en `_Layout.cshtml:97-98` y `site.css:1389-1398` de Intelektron.web.NET.
- **GIVEN** el puesto en reposo, a 1024 × 768
- **WHEN** se mide el título de la cabecera por CDP
- **THEN** la letra efectiva del título entero es Heebo, de peso 300, y la cabecera mide 64 px

#### Scenario: Sin red
Origen: invariante de `AGENTS.md` («El puesto autoriza y registra sin red»), borde: ningún pedido a internet.
- **GIVEN** el puesto abierto sin acceso a internet
- **WHEN** se dibuja la cabecera
- **THEN** el título sale en Heebo desde el archivo del puesto, y ninguna regla de estilo pide una fuente a un dominio externo

#### Scenario: La fuente que falta
Origen: «zonas de acción estables» de los criterios táctiles de David del 03/11/2026, borde: el archivo de la fuente no está.
- **GIVEN** el puesto sin el archivo de la fuente
- **WHEN** se dibuja la cabecera
- **THEN** el título se ve entero en una sans-serif del sistema y la cabecera sigue midiendo 64 px
