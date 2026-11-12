## ADDED Requirements

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

## REMOVED Requirements

### Requirement: La versión del programa bajo el título

**Reason**: David pidió el 11/11/2026 mover la versión de debajo del título, en la cabecera, al centro del pie.

**Migration**: Lo reemplaza «La versión del programa en el pie», que conserva el texto entero («desarrollo», «sin commit» y la validación del tag), la letra más chica y atenuada, y que no sea un control; la cabecera vuelve a tener sólo los logos y el título.
