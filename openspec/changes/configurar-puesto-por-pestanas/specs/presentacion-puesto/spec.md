# Spec Delta

## MODIFIED Requirements

### Requirement: Estilo visual configurable

La pestaña «Identidad visual» de Configuración SHALL ofrecer la sección «Estilo visual»: la opacidad de las superficies, de 72 % a 88 %, y el desenfoque del fondo detrás de ellas, de 0 a 12 px, que MUST previsualizarse al moverlos. Su «Guardar» MUST conservarlos en la carpeta de datos y aplicarlos al volver a abrir el puesto; cerrar Configuración sin guardarlos, con «Cancelar» o Escape, MUST volver a los valores de antes. A Configuración se entra según el modo del puesto.

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

## ADDED Requirements

### Requirement: Monitor asignado y recordado

La pestaña Pantalla SHALL listar los monitores conectados con su resolución y cuál es el principal, y permitir elegir el del puesto. Al guardar, la ventana MUST pasar maximizada a ese monitor, y cada vez que el puesto abra MUST volver a él. Si el monitor guardado no está conectado, o lo guardado no se puede leer, MUST abrir maximizada en el principal y la pestaña MUST avisarlo.

#### Scenario: Elegir el segundo monitor
Origen: decisión de David del 03/11/2026 (pantalla y configuración, punto 2: «permanecer maximizada en el monitor a la cual se destine»).
- **GIVEN** dos monitores conectados y el puesto en el principal
- **WHEN** en Pantalla se elige el otro y se guarda
- **THEN** la ventana queda maximizada en el monitor elegido

#### Scenario: Volver a abrir en el monitor guardado
Origen: decisión de David del 03/11/2026 (punto 2: «recordando la configuración del monitor asignado en caso de ser más de uno»).
- **GIVEN** el segundo monitor guardado como el del puesto
- **WHEN** el puesto se cierra y se vuelve a abrir
- **THEN** abre maximizado en el segundo monitor

#### Scenario: El monitor guardado no está
Origen: nota de pantalla y configuración («queda por diseñar qué ocurre si el monitor asignado se desconecta»), borde.
- **GIVEN** un monitor guardado que ya no está conectado
- **WHEN** el puesto abre
- **THEN** abre maximizado en el principal y la pestaña Pantalla dice que el monitor guardado no está conectado

#### Scenario: Un solo monitor
Origen: decisión de David del 03/11/2026 (punto 2, «en caso de ser más de uno»), borde.
- **GIVEN** un solo monitor conectado
- **WHEN** se abre la pestaña Pantalla
- **THEN** lista ese monitor como el principal y elegido, con su resolución

### Requirement: Sin rebote al arrastrar

Arrastrar el dedo en cualquier pantalla del puesto SHALL NOT mover la página más allá de sus bordes ni hacerla rebotar. Las zonas que se desplazan, como la selección en la ventana angosta y las tablas de Configuración, MUST seguir desplazándose dentro de sus límites.

#### Scenario: La página no rebota
Origen: hallazgo de David del 04/11/2026 en su monitor táctil («arrastrar el dedo hace rebotar la pantalla») y los criterios táctiles del 03/11/2026 («zonas de acción estables»).
- **GIVEN** el puesto en reposo, en la atención y con Configuración abierta
- **WHEN** se revisa cómo responde la página a un arrastre más allá de su borde
- **THEN** la página y su contenedor principal no admiten desplazarse más allá del borde ni encadenar el desplazamiento

#### Scenario: La ventana angosta sigue desplazándose
Origen: «Ventana angosta» de este mismo spec, borde: quitar el rebote no quita el desplazamiento.
- **GIVEN** el catálogo de nueve opciones y un área de 600 px de ancho
- **WHEN** se desplaza la selección
- **THEN** se llega a la última opción y a «Confirmar»

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
