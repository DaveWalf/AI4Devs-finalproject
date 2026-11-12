# Design

## Context

- **La cabecera** (`PantallaAtencion.razor`, `header.cabecera`) tiene el logo de la empresa, `span.servicio` con
  «INComedor · tu pausa del día» y el logo de la concesionaria. Mide 64 px de alto fijo, en `display: flex` con
  `align-items: center`. El nombre del servicio va en Georgia de 22 px.
- **La pestaña General** (`PanelConfiguracion.razor`, `#seccion-general`) muestra el modo, la simulación, sus botones,
  la sección «Bitácoras» y la sección «Resguardo de la configuración», cada sección con su `h3`. Un operador no la ve.
- **La versión del Kiosk** es `1.0.0` en todo build. El SDK agrega el commit a la versión informativa
  (`AssemblyInformationalVersion` `1.0.0+7b80d455e506…`) en Debug, en Release y en el EXE publicado: lo medí en los
  ensamblados de `bin/` del 11/11.
- **El empaquetado.** `publicar-puesto.ps1 -Version <tag>` valida el tag con `^[0-9A-Za-z][0-9A-Za-z.\-]{0,63}\z` y lo usa
  sólo para nombrar el zip. `release.yml` le pasa el tag por `INCOMEDOR_TAG` y no por `VERSION`, porque MSBuild toma las
  variables de entorno como propiedades y «v0.0.0-ensayo» como `Version` rompió el restore (MSB4181). El script corre
  el E2E contra el EXE publicado antes de escribir el zip.

## Goals / Non-Goals

**Goals:**

- Ver de un vistazo qué compilación corre en el puesto, con el tag del Release y el commit (gate, respuestas 3 y 5).
- Dar en General lo que soporte necesita (gate, respuesta 4).
- Que el tag llegue al EXE sin volver a romper el restore.

**Non-Goals:**

- Los de la proposal: la versión de ensamblado sigue en `1.0.0`, y el diagnóstico de versiones por componente queda en
  la segunda ola.

## Decisions

### D1. El tag y la fecha entran como metadatos con nombre propio

`Intelektron.INComedor.Kiosk.csproj` declara dos `AssemblyMetadata`: `VersionPuesto` con `$(VersionPuesto)` y
`FechaCompilacionPuesto` con `$(FechaCompilacionPuesto)`, éste en UTC ISO 8601. Los dos van vacíos si no llegan.
`publicar-puesto.ps1` se los pasa al build y al publish (`-p:VersionPuesto=$Version`
`-p:FechaCompilacionPuesto=<UTC>`), con la fecha tomada una sola vez al empezar. El tag ya pasó la validación del
script.

- *Descartada*: pasar el tag como `Version` o `InformationalVersion`. `Version` rompe el restore con un tag que empieza
  con «v» (MSB4181, tres corridas medidas), y reemplazar `InformationalVersion` le quita el commit que agrega el SDK.
- *Descartada*: estampar la fecha en todo build con `$([System.DateTime]::UtcNow)`. Cambia el atributo generado en cada
  compilación, así que el build incremental recompila siempre el Kiosk.

### D2. Una sola clase lee la versión, y valida lo que lee

`VersionPrograma` (en el Kiosk) se arma una vez desde el ensamblado del Kiosk:

- **la versión**: `VersionPuesto` si cumple la misma expresión del script; si no, «desarrollo»;
- **el commit**: lo que sigue al `+` de la versión informativa, si son de 7 a 40 caracteres hexadecimales; si no,
  «sin commit»;
- **la fecha**: `FechaCompilacionPuesto` si es una fecha ISO válida; si no, la fecha de escritura del archivo del
  ensamblado;
- **.NET**: `RuntimeInformation.FrameworkDescription`.

Expone el texto corto de la cabecera («v1.0.1 · 7b80d45») y los cuatro datos de General. Las pruebas la construyen
con valores sintéticos, sin depender del build.

- *Descartada*: leer la versión en cada componente Razor. Duplica la validación, y las pruebas tendrían que depender de
  cómo se compiló el ensamblado de prueba.
- *Descartada*: la fecha de última escritura como fecha de un paquete. Al descomprimir el zip, la fecha es la del zip, y
  se perdería la de compilación.

### D3. La cabecera: el título y la versión en una columna centrada

`span.servicio` pasa a ir dentro de un `div.titulo-cabecera` en columna, con el mismo `flex: 1` que hoy tiene el span.
Debajo va `span#version-programa`, en 12 px, con `color: var(--tinta-suave)` y `opacity: .8`. El texto y la clase de
`.servicio` no cambian, así que las pruebas que lo buscan siguen iguales. Los dos renglones entran en los 64 px: 22 px
de Georgia con su interlineado más los 12 px de la versión dan unos 44 px. Alineación: el título y la versión quedan
centrados en la misma vertical, la del espacio entre los dos logos, que no se mueven. La versión no es un control:
es texto sin `tabindex` ni eventos.

- *Descartada*: la versión dentro de `span.servicio`. Cambiaría el texto que comparan las pruebas de la cabecera.
- *Descartada*: la versión en el pie, junto a la marca Intelektron. David la pidió «debajo del título», y el pie ya
  tiene el estado de sincronización, la marca y el engranaje.

### D4. General: una sección más, la última

Al final de `#seccion-general`, después de «Resguardo de la configuración», va
`section#version-programa-general` con `h3` «Versión del programa» y una lista de definiciones (`dl`) con «Versión»,
«Commit», «Compilado» y «.NET». La fecha se muestra en la zona del puesto (el parámetro `Zona` que ya recibe el
panel), como `dd/MM/yyyy HH:mm`. Alineación: el `h3` tiene la misma letra y margen que «Bitácoras» y «Resguardo de la
configuración»; los rótulos del `dl` forman una columna y los valores otra, alineadas a la izquierda, en la letra del
resto de la pestaña. El commit completo usa `font-variant-numeric: tabular-nums` y `overflow-wrap: anywhere`, para que
no desborde.

- *Descartada*: un botón «Copiar» junto al commit. El puesto no tiene a dónde pegar, y sería un control más en una
  pestaña que no lo necesita.

### D5. La prueba del tag va en un E2E nuevo, que el script elige

`VersionDelPaqueteE2ETests` abre el EXE publicado y compara la cabecera con `INCOMEDOR_VERSION_ESPERADA`. El script
le pone el tag a esa variable, y sin ella la prueba compara contra «desarrollo». El nombre no contiene `VERSION` sola,
que MSBuild tomaría como propiedad: es `INCOMEDOR_VERSION_ESPERADA`, que no coincide con ninguna propiedad del SDK.
Un E2E de la solución, sin la variable, comprueba «desarrollo».

- *Descartada*: comprobar la versión leyendo el ensamblado con PowerShell en el script. Prueba el metadato, no lo que
  ve la persona en la pantalla, que es lo que pidió David.

## Risks / Trade-offs

- **Un release con un tag nuevo** es la primera corrida real del camino del tag, y vuelve a pasar por el restore donde
  falló MSB4181. Lo cubre el escenario «Un tag que rompía el restore», que se corre en local con
  `publicar-puesto.ps1 -Version v0.0.0-ensayo` antes de dar la tarea por hecha.
- **El commit es el de HEAD**: un build con cambios sin commitear muestra el commit anterior. Es lo que trae el SDK, y
  vale para un paquete, que se arma desde un tag.
- **La fecha de un build de desarrollo** es la del archivo, que cambia también al copiarlo.
