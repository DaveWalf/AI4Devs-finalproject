# Proposal

## Why

El 11/11/2026, alrededor de las 23:15, mientras se preparaba la punta de la entrega final, David pidió dos cosas,
textuales: «mover la versión, de debajo del título INComedor en la barra de arriba, al centro de la barra que está
abajo, indicando previamente "versión ..."» y «fijate cuál es la tipografía que utiliza el INWeb para representar su
propio nombre en su interfaz, y aplicá la misma tipografía a todo ese título». Decidió que entren como un change en
`feature/mejoras-2-5-dw`, antes del PR de la final (respuesta a la pregunta de las 23:16, «Change en esta rama»).

Hoy la versión («v1.0.1 · 7b80d45» o «desarrollo · 7b80d45») va debajo de «INComedor · tu pausa del día», en la
cabecera, por `mostrar-version-del-programa` (archivado el 11/11/2026). El pie tiene la franja de sincronización a la
izquierda y la marca Intelektron con el engranaje a la derecha; el centro está libre. El título usa Georgia, una serif
que no es la letra de la familia de productos.

INWeb escribe su nombre, «IN-Web», en la barra superior con **Heebo**, peso 300 (Light), a 26 px
(`Views/Shared/_Layout.cshtml:97-98` y `wwwroot/css/site.css:1301-1302` y `:1389-1398` de Intelektron.web.NET), desde un
`.woff2` local que su `wwwroot/vendor/google/css2.css` declara para los pesos 300, 400 y 700 (una sola fuente variable
de 30 KB para el subconjunto latino). Heebo es una fuente de Google Fonts publicada bajo la SIL Open Font License 1.1.

Origen: historia HU03.2 (consultar el estado del puesto), escenario HU03-D («Distingue estados, versiones y última
sincronización»), y los criterios táctiles de David del 03/11/2026 («zonas de acción estables»); ticket TK02.1
(interfaz).

## Gate (para David)

Recomendada primero en cada punto.

1. **El texto del pie.** (a, recomendada) «versión v1.0.1 · 7b80d45» y «versión desarrollo · 7b80d45»: la versión
   queda igual a la del Release, del zip y de General, así lo que se ve coincide con lo que se busca. (b) «versión
   1.0.1 · 7b80d45», sin la «v» del tag, que se lee mejor después de «versión».
2. **La letra de la versión.** (a, recomendada) Como hoy: 12 px, atenuada, con cifras tabulares, porque la mejora 3
   pidió «menor tamaño y más atenuada». (b) La del pie, 14 px, sin atenuar.
3. **Dónde es «el centro».** (a, recomendada) El centro de la ventana: el pie pasa a tres columnas (la franja, la
   versión y la marca con el engranaje), y la versión queda en el mismo lugar en reposo y durante una atención. (b) El
   centro del espacio libre entre la franja y la marca, que se corre cuando cambia el texto de la sincronización.
4. **El título en Heebo.** (a, recomendada) Todo «INComedor · tu pausa del día» en Heebo Light (300), a los 22 px de
   hoy, para no cambiar el alto de la cabecera ni el espacio entre los logos. (b) Heebo Light a 26 px, como INWeb.
5. **La fuente en el repo.** (a, recomendada) Copiar el `.woff2` latino de INWeb al Kiosk (`wwwroot/fonts/`), con el
   texto de la licencia OFL 1.1 de Heebo al lado, bajado de `google/fonts` en GitHub, porque el repo es público y la
   OFL pide redistribuir la fuente con su licencia. (b) Sin el archivo de la licencia (no recomendado).
6. **Las tres pruebas existentes que cambian**, a aprobar antes de tocarlas (tarea 1.1):
   - `tests/Intelektron.INComedor.Kiosk.Tests/VersionEnPantallaTests.cs:14-28`, «Un build de desarrollo: en reposo,
     debajo de «INComedor · tu pausa del día», la cabecera dice…»: pasa a buscar la versión en el pie, con «versión »
     adelante, y a comprobar que la cabecera ya no la tiene.
   - `VersionEnPantallaTests.cs:30-40`, «Un paquete publicado: con una persona en la selección, la cabecera sigue
     mostrando la versión»: pasa a comprobarlo en el pie.
   - `tests/Intelektron.INComedor.E2E.Tests/VersionDelPaqueteE2ETests.cs:29-61`, «El tag en la cabecera del EXE»:
     pasa a leer el texto en el pie, con «versión » adelante, y a medir que la versión quede centrada en la ventana,
     dentro del pie, más chica que el título; la parte de General no cambia. Es la prueba que `publicar-puesto.ps1`
     corre contra el EXE.

## What Changes

- **La versión, al centro del pie**: «versión » y la misma línea de hoy, en reposo y durante una atención, como texto
  y no como control, sin cambiar el alto del pie ni el lugar del engranaje.
- **La cabecera sin la versión**: vuelve a tener sólo el logo de la empresa, el título y el logo de la concesionaria,
  en 64 px.
- **El título en Heebo**: «INComedor · tu pausa del día» con la letra con que INWeb escribe su nombre, desde un archivo
  local del Kiosk, sin pedir nada a internet.
- La sección «Versión del programa» de General no cambia.

## Non-goals

- No cambia la letra del resto de la interfaz: sólo el título de la cabecera.
- No cambia la versión que llega al EXE, ni el zip, ni `release.yml`, ni lo que muestra General.
- No se agrega ningún control al pie: la versión no se toca.
- La mejora 6 (el consumo excepcional) sigue fuera de la final.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `presentacion-puesto`: se retira «La versión del programa bajo el título» y se suman «La versión del programa en el
  pie» y «El título con la letra de INWeb».

## Impact

- Código: `src/Intelektron.INComedor.Kiosk/Atencion/PantallaAtencion.razor` (la versión sale de la cabecera y entra al
  pie), `src/Intelektron.INComedor.Kiosk/wwwroot/css/puesto.css` (el pie en tres columnas, la versión y el
  `@font-face` de Heebo) y un archivo nuevo, `src/Intelektron.INComedor.Kiosk/wwwroot/fonts/heebo-latin.woff2`, con
  su licencia.
- Pruebas existentes que el cambio exige tocar: las tres del punto 6 del gate. Pruebas nuevas: la cabecera sin versión,
  el texto del pie con «versión » y la letra del título por CDP.
- Documentación: `readme.md` (dónde se ve la versión) y `docs/aplicacion-del-curso.md` (la evidencia).
