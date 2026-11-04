# presentacion-puesto Specification

## Purpose
Cómo se ve y se adapta el puesto en una pantalla táctil: el aspecto, la cabecera con los logos y el engranaje, la ventana y el estilo visual configurable, sin cambiar lo que la atención decide ni registra.

## Requirements

### Requirement: Aspecto táctil del puesto

Las pantallas del puesto SHALL mostrarse sobre el fondo ilustrado de cocina, con las opciones y las acciones como superficies translúcidas cuyo texto MUST quedar opaco. La fila entera de una opción MUST ser tocable, la opción elegida MUST indicarse también con el texto «Elegida» y no sólo con el color, y ninguna función MUST depender de pasar el puntero por encima. «Cancelar», «Confirmar» y cada opción MUST medir al menos 56 px CSS de alto.

#### Scenario: La opción elegida se marca con texto
Origen: decisión de David del 03/11/2026 (UX táctil, criterios 2 y 3) y TK02.1.
- **GIVEN** una persona en la selección, sin opción elegida
- **WHEN** toca una opción
- **THEN** esa opción figura como presionada y muestra el texto «Elegida»
- **AND** ninguna otra opción muestra «Elegida»

#### Scenario: Elegir otra opción mueve la marca
Origen: decisión de David del 03/11/2026 (UX táctil, criterio 3: «selección separada de confirmación»).
- **GIVEN** una persona con una opción elegida y sin confirmar
- **WHEN** toca otra opción
- **THEN** sólo la nueva muestra «Elegida» y no se escribió ninguna intención

#### Scenario: Nueve opciones y las acciones entran en 1024 × 768
Origen: decisión de David del 02/11/2026 (una misma pantalla, regla 4) y del 03/11/2026 (UX táctil, criterio 1).
- **GIVEN** el catálogo de nueve opciones y la ventana con un área de 1024 × 768
- **WHEN** se muestra la selección
- **THEN** las nueve opciones, «Cancelar» y «Confirmar» se ven sin desplazar la pantalla
- **AND** cada una mide al menos 56 px de alto

### Requirement: Cabecera con logos y un solo control

La cabecera SHALL mostrar el logo de la empresa, el nombre del servicio y el logo de la concesionaria. En reposo, el engranaje de Configuración MUST ser su único control, con el nombre accesible «Configuración» y un área de toque de al menos 56 px aunque el dibujo sea menor; durante una atención el engranaje MUST NOT mostrarse. El pie MUST mostrar la marca Intelektron, discreta, y el estado de sincronización.

#### Scenario: Un solo control en la cabecera
Origen: decisión de David del 03/11/2026 (perfil gráfico, regla 1).
- **GIVEN** el puesto en reposo
- **WHEN** se revisan los controles de la cabecera
- **THEN** hay un solo botón, con el nombre «Configuración»

#### Scenario: Sin engranaje durante una atención
Origen: AT16 («siguiente persona inicia limpia») y RB05: Configuración no se abre con una persona en curso.
- **GIVEN** una persona identificada en la selección
- **WHEN** se revisa la cabecera
- **THEN** no hay ningún botón «Configuración»

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

Al abrir, la ventana del puesto SHALL quedar maximizada y la pantalla MUST ocupar su área. Cuando el ancho no admite dos columnas con el texto entero y las áreas de toque mínimas, las opciones MUST ir en una columna con desplazamiento vertical, sin achicar el texto ni los botones.

#### Scenario: Abre maximizada
Origen: decisión de David del 03/11/2026 (pantalla y configuración, punto 2: «deberá permanecer maximizada»).
- **GIVEN** el puesto cerrado
- **WHEN** se abre
- **THEN** la ventana está maximizada

#### Scenario: Ventana angosta
Origen: decisión de David del 02/11/2026 (regla 4: la excepción queda explícita y no se consigue encogiendo los controles).
- **GIVEN** el catálogo de nueve opciones y un área de 600 px de ancho
- **WHEN** se muestra la selección
- **THEN** las nueve opciones van en una sola columna, cada una exactamente una vez, y la pantalla se desplaza en vertical
- **AND** el texto de las opciones tiene el mismo tamaño que con 1024 px de ancho

### Requirement: Estilo visual configurable

En reposo, el engranaje SHALL abrir Configuración con la sección «Estilo visual»: la opacidad de las superficies, de 72 % a 88 %, y el desenfoque del fondo detrás de ellas, de 0 a 12 px, que MUST previsualizarse al moverlos. «Guardar» MUST conservarlos en la carpeta de datos y aplicarlos al volver a abrir el puesto; «Cancelar» o Escape MUST cerrar Configuración con los valores de antes. En este corte se abre sin clave.

#### Scenario: Guardar y volver a abrir
Origen: pedido de David del 03/11/2026 («que en la etapa de configuración estén esos mismos controles … como parámetros»).
- **GIVEN** Configuración abierta con los valores por defecto
- **WHEN** se fija la opacidad en 84 % y el desenfoque en 8 px, se toca «Guardar», y el puesto se cierra y se vuelve a abrir
- **THEN** la pantalla usa 84 % y 8 px

#### Scenario: Cancelar descarta la vista previa
Origen: pedido de David del 03/11/2026 (vista previa con guardar y cancelar).
- **GIVEN** los valores guardados 76 % y 4 px y Configuración abierta
- **WHEN** se mueven a 88 % y 12 px y se toca «Cancelar», o se pulsa Escape
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
