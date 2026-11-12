# Design

## Context

- **La cabecera** (`PantallaAtencion.razor:15-24`) tiene el logo de la empresa, `div.titulo-cabecera` con
  `span.servicio` («INComedor · tu pausa del día») y `span#version-programa`, y el logo de la concesionaria. Mide 64 px
  fijos (`puesto.css:57-75`); el título va en Georgia de 22 px y la versión en 12 px, atenuada, con cifras tabulares.
- **El pie** (`PantallaAtencion.razor:262-276`, `puesto.css:303-321`) es un `flex` de al menos 56 px: la
  `FranjaSincronizacion` a la izquierda y `div.pie-derecha` (la marca Intelektron y el engranaje, o su lugar reservado
  de 56 × 56 px durante una atención) empujado a la derecha con `margin-left: auto`. Es inerte con un diálogo o el
  cartel abiertos, como la cabecera.
- **La versión** sale de `VersionPrograma.DelPuesto().Cabecera` («v1.0.1 · 7b80d45», «desarrollo · …» o con «sin
  commit»), validada al leer; General usa sus otros campos y no cambia.
- **La letra del Kiosk.** El cuerpo usa «Segoe UI», `system-ui`, sans-serif (`puesto.css:33`); los títulos de la
  atención y de los diálogos, Georgia. No hay ninguna `@font-face`, ni política de contenido en `index.html`: el
  WebView2 sirve `wwwroot` desde el propio paquete.
- **INWeb** escribe «IN-Web» en `.header .header-titulo` con `font-size: 26px` y `font-weight: 300`, heredando
  `font-family: Heebo, sans-serif` del `body`. Su `wwwroot/vendor/google/css2.css` declara Heebo 300, 400 y 700 con el
  mismo archivo por subconjunto (una fuente variable); el latino es `NGS6v5_NC0k9P9H2TbE.woff2`, de 30.240 bytes, con
  el rango `U+0000-00FF` (las tildes y «·»), `U+2000-206F` y otros.
- **Pruebas que fijan la versión en la cabecera**: `VersionEnPantallaTests.cs:14-40` (bUnit) y
  `VersionDelPaqueteE2ETests.cs:29-61` (por CDP, también contra el EXE de `publicar-puesto.ps1`).

## Goals / Non-Goals

**Goals:**

- La versión en el centro del pie, con «versión » adelante, sin mover nada de lo que se toca (gate, puntos 1 a 3).
- El título con la letra de INWeb, sin depender de la red (gate, puntos 4 y 5).

**Non-Goals:**

- Los de la proposal: el resto de la interfaz sigue en su letra, y General y el empaquetado no cambian.

## Decisions

### D1. El pie en tres columnas

`.pie` pasa de `flex` a `grid` con `grid-template-columns: 1fr auto 1fr`: la franja en la primera, alineada a la
izquierda; `span#version-programa` en la del centro; y `div.pie-derecha` en la tercera, con `justify-self: end`. Las
dos columnas de los costados miden lo mismo, así que la del medio queda centrada en la ventana aunque cambie el texto
de la sincronización, y el engranaje sigue en el borde derecho, con su área de 56 px.

Descartada: dejar el `flex` y meter la versión entre la franja y la marca con `margin: auto`. Queda centrada en el
espacio libre, no en la ventana, y se corre cada vez que la franja cambia de texto, contra las «zonas de acción
estables».

Prolijidad: la versión queda alineada en la vertical con la franja y la marca (`align-items: center` de la grilla) y
no tiene hermanos de su tipo. Con un texto de sincronización largo, la primera columna corta con `min-width: 0` y la
franja conserva su propio recorte; la versión nunca la tapa porque es otra columna.

### D2. El texto es «versión » más la línea de hoy

`PantallaAtencion.razor` escribe `versión @VersionDelPrograma.Cabecera` dentro de `span#version-programa`, que
conserva el `id` (lo lee el E2E que `publicar-puesto.ps1` corre contra el EXE). Así la validación del tag, «desarrollo» y
«sin commit» siguen en un solo lugar, `VersionPrograma`, sin otra propiedad. Es un rótulo, no una oración: sin punto
final. La letra sigue en 12 px, atenuada y con cifras tabulares (gate, punto 2).

Descartada: una propiedad nueva `VersionPrograma.Pie`. Duplica la regla del formato para agregar una palabra.

### D3. La cabecera vuelve a tres piezas

`div.titulo-cabecera` desaparece y `span.servicio` vuelve a ser hijo directo de la cabecera, con `flex: 1` y
`text-align: center`, como antes de `mostrar-version-del-programa`. Las reglas `.titulo-cabecera` y
`.cabecera #version-programa` de `puesto.css` se quitan, y `.pie #version-programa` toma la letra.

Descartada: dejar el contenedor vacío. No aporta nada y una prueba que busque la versión en la cabecera tendría que
distinguir un contenedor sin hijos.

### D4. Heebo desde un archivo del Kiosk

Se copia `NGS6v5_NC0k9P9H2TbE.woff2` de Intelektron.web.NET a
`src/Intelektron.INComedor.Kiosk/wwwroot/fonts/heebo-latin.woff2`, y el texto de la OFL 1.1 de Heebo, de
`google/fonts` (`ofl/heebo/OFL.txt`), a `wwwroot/fonts/OFL-Heebo.txt`. `puesto.css` declara una sola `@font-face`
(`font-family: "Heebo"`, `src: url("../fonts/heebo-latin.woff2") format("woff2")`, `font-weight: 100 900`,
`font-display: block`, el `unicode-range` latino de INWeb) y `.cabecera .servicio` pasa a
`font-family: Heebo, "Segoe UI", system-ui, sans-serif; font-weight: 300`, a 22 px (gate, punto 4).

`font-display: block` evita que el título se dibuje primero en otra letra y salte: el archivo es local y llega en
milisegundos; si no está, el navegador usa la siguiente de la pila (escenario «La fuente que falta»).

Descartadas: el `<link>` a `fonts.googleapis.com`, que INWeb tiene comentado y que rompería el puesto sin red; copiar
los quince bloques de `css2.css` con los subconjuntos hebreo y de símbolos, que el título no usa.

### D5. Cómo se prueba la letra

Por CDP, `CSS.getPlatformFontsForNode` sobre el nodo de texto del título devuelve la familia que el navegador usó de
verdad (no la pedida): tiene que ser «Heebo» para todos los glifos, y `getComputedStyle(...).fontWeight` tiene que dar
`300`. El E2E lo mide a 1024 × 768 junto con la cabecera de 64 px. En bUnit se comprueba sólo la estructura (la
cabecera sin versión y el pie con «versión »), porque bUnit no dibuja.

Descartada: comparar `getComputedStyle().fontFamily`, que devuelve la pila pedida aunque la fuente no haya cargado.

### D6. Las tres pruebas existentes, con aprobación

Se cambian, con la aprobación de David en la tarea 1.1 y nada más que lo que este change pide:

- `VersionEnPantallaTests.La_version_en_reposo`: busca `footer.pie #version-programa`, compara con
  `"versión " + VersionPrograma.DelPuesto().Cabecera`, comprueba que empieza con «versión desarrollo · », que la
  cabecera no tiene `#version-programa` ni botones y que la versión no tiene `tabindex`. Se quita la afirmación de que
  comparte el padre con `.servicio`.
- `VersionEnPantallaTests.La_version_durante_una_atencion`: la misma comparación en `footer.pie`.
- `VersionDelPaqueteE2ETests.Version_del_paquete_en_la_cabecera`: espera `versión {esperada} · {commit}`, mide el
  centro de la versión contra el centro de la ventana y su letra contra la del pie, comprueba que no se solapa con la
  franja ni con la marca y que la cabecera sigue en 64 px sin botones. Su `DisplayName` cambia a «El tag en el pie del
  EXE…»; el método y la parte de General no cambian.

## Risks / Trade-offs

- **La licencia**: la fuente entra al repo público. La OFL 1.1 lo permite siempre que viaje con su licencia y no se
  venda sola; por eso el `OFL-Heebo.txt` va en la misma carpeta y en el zip del paquete (el `publish` copia `wwwroot`).
- **El ancho del título**: Heebo Light a 22 px es algo más angosto que Georgia; el título cabe entre los logos a
  1024 × 768 con más margen que hoy. La captura del E2E lo muestra.
- **La línea del tiempo**: el change entra la noche anterior a la entrega; suma una corrida entera de la solución
  (~5 min en local con el E2E) y la del paquete contra el EXE, si se arma.
