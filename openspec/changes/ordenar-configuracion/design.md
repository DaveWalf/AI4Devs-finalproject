# Design

## Context

Configuración (`PanelConfiguracion.razor`) arma sus pestañas a partir del `enum Pestana` (`:325`), en el orden en que
están declaradas, y abre en `Pestana.General` (`:354`), salvo con un operador, que abre en Consumos (`:533`). El pie tiene
un solo botón, `#cancelar-configuracion` «Cancelar» (`:318`), que cierra y descarta lo no guardado. El combo de la
simulación (`PantallaAtencion.razor:36-45`) y los filtros Persona, Servicio y Opción de Consumos
(`PestanaConsumos.razor:19-48`) son `<select>` nativos: su lista la dibuja el WebView2 como una ventana emergente propia,
fuera de la página, y con la ventana del puesto en un monitor que no es el principal aparece en el principal (hallazgo de
David del 10/11). En `puesto.css:121-150` el campo del código crece con «Ingresar» hasta los 592 px de la tarjeta, con
letra de 24 px y relleno de 16 px, y el combo mide 480 px con letra de 22 px y relleno de 12 px: sólo coincide el alto.

## Goals / Non-Goals

**Goals:**
- El orden de las pestañas que piden las mejoras 4 y 8a, abriendo en la primera.
- «Cerrar» con la conducta de hoy.
- Listas que se abren en la ventana del puesto, para el combo y los filtros de Consumos.
- El combo alineado con la fila del código, y la regla de prolijidad escrita para los changes que siguen.

**Non-Goals:**
- Sacar Comprobantes, buscar, reimprimir en Consumos, las 24 h y la exportación (change 1b).
- Las listas de `PanelOpciones`, que rehace el change de Opciones.
- Cambiar la máquina de la atención, la base o la bitácora.

## Decisions

### D1. El orden sale del `enum`, y la inicial es la primera visible

El `enum Pestana` se reordena: Consumos, Personas, Opciones, Comprobantes, Identidad, Impresora, Pantalla, Red y General.
La pestaña inicial pasa a ser la primera de `Visibles`, con la clave del día o con un operador, así que el caso especial
de `OnInitialized` sobra. `DelOperador` no cambia. Las flechas, Inicio y Fin ya recorren `Visibles` en orden.

- **Descartada: una lista de orden aparte del `enum`.** Dos fuentes para lo mismo; el `enum` ya es la lista cerrada.
- **Descartada: abrir en General aunque quede última.** La mejora 4 pide «Consumos primero», y abrir en la última
  obligaría a recorrer la fila para lo que más se consulta.

### D2. «Cerrar» con el mismo id

El texto pasa a «Cerrar» y el id queda `cancelar-configuracion`, con la misma conducta (cierra y descarta lo no
guardado). El comportamiento no exige el id nuevo, y conservarlo evita cambiar las pruebas que sólo lo tocan (decisión
13 del plan).

- **Descartada: `cerrar-configuracion`.** Cambiaría catorce pruebas sólo por el nombre.
- **Descartada: preguntar si se descarta lo no guardado.** Nadie lo pidió, y agrega un paso a cada cierre.

### D3. Una lista desplegable propia: `ListaDesplegable.razor`

Un componente con un botón que muestra el texto elegido (o el texto vacío, como «Elegí a una persona…» o «Todas») y, al
tocarlo, una lista `role="listbox"` con una fila `role="option"` por entrada, dibujada en la página debajo del botón y
con su mismo ancho, dentro de la ventana: si no entra hacia abajo, se abre hacia arriba, y con muchas entradas se
desplaza adentro. El botón lleva `aria-haspopup="listbox"`, `aria-expanded` y el id que tenía el `<select>`
(`simulacion-persona`, `consumos-persona`, `consumos-servicio`, `consumos-opcion`). El valor de cada fila es el mismo que
tenía la opción (la credencial, nunca un código; la posición en los filtros). Teclado: Enter o espacio abre; flechas,
Inicio y Fin mueven el foco; Enter elige; Escape cierra y devuelve el foco al botón. Un fondo transparente debajo de la
lista toma el toque afuera y la cierra. Elegir dispara el mismo `ValueChanged` que hoy dispara `@onchange`, así que
`ElegirEnSimulacion` y los filtros no cambian de conducta.

- **Descartada: mover la ventana emergente del `<select>`.** La dibuja el WebView2, no la página; no hay una API de la
  página que la ubique.
- **Descartada: `<datalist>` o una lista siempre abierta.** El primero también es nativo; la segunda ocupa el alto de la
  pantalla de identificación.

### D4. El combo con las medidas del campo

En la tarjeta de identificación, el combo ocupa el ancho de la fila (`width: 100%`), con el borde izquierdo del campo y
el derecho de «Ingresar»; su texto usa la letra de 24 px y el relleno de 16 px del campo, y el alto de 56 px. Las reglas
de `.atencion input` y del combo comparten las mismas variables de tamaño en `puesto.css`, para que no vuelvan a
separarse.

- **Descartada: el ancho del campo solo.** El borde derecho quedaría a mitad de la fila, que es la asimetría del hallazgo.

### D5. La regla de prolijidad en `openspec/config.yaml`

Se agrega, con el texto de la decisión 14, a `rules.design` y a `rules.tasks`, en un commit propio del grupo 1. La
verificación de este change es su primera aplicación: mide el combo, el campo e «Ingresar» por CDP a 1024 × 768.

- **Descartada: escribirla sólo en `AGENTS.md`.** Las reglas de `config.yaml` llegan a cada artefacto que arma OpenSpec,
  que es donde se decide y se mide.

### D6. Pruebas existentes que cambian

Sólo las que el comportamiento exige (decisión 13): las que leen General sin tocarla, porque ya no abre ahí; las que
listan el orden o recorren la fila con las flechas; las que eligen en un `<select>` (el combo y los filtros de Consumos),
que pasan a tocar el botón y la fila; y los `DisplayName` de los dos escenarios renombrados («Cerrar descarta…»). Ninguna
cambia lo que afirma. Las que sólo tocan `#cancelar-configuracion` no cambian (D2).

## Risks / Trade-offs

- **[Las pruebas que leen General]** → Son muchas en `Kiosk.Tests` (`AccesoPuestoTests`, `ModoSimulacionTests`,
  `AccesoOperadorTests`): el ayudante de entrada suma el toque de General donde la prueba lo lee, sin cambiar lo que
  afirma.
- **[Una lista propia pierde algo del control nativo]** → Se prueba con bUnit el teclado y el toque afuera, y con el E2E
  que la lista queda entera dentro de la ventana; el monitor alternativo lo prueba David, porque la CI tiene uno solo.
- **[La lista dentro del diálogo de Configuración]** → Se abre dentro del diálogo, que ya es `inert` para lo de atrás; su
  fondo transparente queda dentro del diálogo.

## Migration Plan

Sin migración: no cambian la base, los archivos de la carpeta de datos ni la bitácora.

## Open Questions

Ninguna: el gate está dado (decisión 13).
