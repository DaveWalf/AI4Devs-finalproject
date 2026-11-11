# Design

## Context

`PestanaConsumos.razor` arma la tabla `.tabla-consumos` con ocho columnas: hora, N.º, persona, opción, servicio, cargo,
estado y acciones («Ver», «Reimprimir» y, si el original falló, «Recuperar»). `wwwroot/css/puesto.css` le da letra de
16 px, celdas de 44 px de alto con 8 px de relleno lateral y `table-layout` automático, sin `white-space: nowrap` salvo
en las acciones. Así, el navegador reparte el ancho y parte las celdas largas: en la captura 15 del 11/11 se parten la
fecha y hora («11/11» y «13:51»), «Persona sintética 01», «Almuerzo con gaseosa» y «ARS 2.000,00».

A 1024 × 768, la caja de la tabla mide unos 926 px (de x = 49 a x = 975 en la captura). La hora sale con `Hora(fila)`:
`HH:mm` si el rango es de un solo día y no es «Últimas 24 h», y `dd/MM HH:mm` si no.

## Goals / Non-Goals

**Goals:**

- Que cada consumo ocupe un renglón a 1024 × 768 o más, con cualquier largo de nombre (gate, respuesta 1).
- Que lo que se corta se pueda leer entero sin salir de la tabla, al tocarlo o con el mouse.
- Que la letra quede entre 14 y 16 px, la mayor que entre (gate, respuesta 2).

**Non-Goals:**

- Los de la proposal: no cambian las columnas, los datos, el orden, la búsqueda ni la exportación, ni las acciones de
  la fila, que son de `reimprimir-sin-codigo`.

## Decisions

### D1. Anchos fijos y el resto para la persona y la opción

La tabla pasa a `table-layout: fixed`, con `white-space: nowrap` en todas las celdas. Las columnas cortas tienen ancho
fijo en `ch`, medido en la letra elegida: hora `11ch` («11/11 13:51»), N.º `9ch` (ocho caracteres del número), servicio
`11ch` («REFRIGERIO»), cargo `13ch` («ARS 2.000,00»), estado `10ch` y acciones el ancho de los tres botones. La persona y
la opción se reparten el resto por mitades, con `overflow: hidden` y `text-overflow: ellipsis`. El texto de la celda
sigue entero en el DOM: el corte es de CSS, así que la búsqueda, el orden, la exportación y las pruebas que leen las
celdas no cambian.

- *Descartada*: `table-layout` automático con `max-width` en la persona y la opción. El navegador ignora `max-width` en
  las celdas de una tabla automática, y el reparto vuelve a depender del contenido.
- *Descartada*: abreviar el servicio o el estado. Son palabras de la spec y de la exportación, y cambiarlas confunde la
  búsqueda por texto.

### D2. La letra: la mayor de 14 a 16 px que entra

La tabla define `--letra-tabla-consumos`. La tarea de medición prueba 16, 15 y 14 px con la fila de prueba del escenario
«Un renglón a 1024 × 768»: la persona y la opción tienen que mostrar al menos «Persona sintética» y «Almuerzo con», y la
fila tiene que llevar los tres botones. Se queda con la mayor que cumple. Los encabezados usan la misma letra que las
celdas, en negrita, como hoy.

- *Descartada*: fijar 14 px de entrada. David aceptó 14 como mínimo, no como meta, y 16 es lo que usa el resto de la
  pestaña.

### D3. Botones de la fila más compactos, con el mismo alto

«Ver», «Reimprimir» y «Recuperar» bajan a la letra de la tabla y a 8 px de relleno lateral, con 4 px entre ellos y los
44 px de alto táctil de hoy. Alineación: los tres comparten alto, borde, relleno, tipografía y línea de base; el ancho
de cada uno es el de su texto, y la columna de acciones reserva el de los tres aunque la fila no tenga «Recuperar», para
que los «Ver» de todas las filas queden en la misma vertical.

- *Descartada*: un solo botón que despliegue las acciones. Lo ofreció el gate y David eligió cortar con «…».

### D4. El globo del dato completo, sin JavaScript

Las celdas de hora, persona y opción reciben `@onpointerup` y `@onpointerenter` / `@onpointerleave`. Con un toque
(`PointerType` distinto de `mouse`) se abre o se cierra el globo de esa celda. Con el mouse, el globo se abre a los
500 ms de quedarse encima y se cierra al salir. Hay un solo globo a la vez (`#globo-dato`, `role="tooltip"`). Se dibuja
con `position: fixed` en las coordenadas del puntero (`ClientX`, `ClientY` de `PointerEventArgs`), 12 px más abajo, con
`max-width: 360px`. Como las tres columnas están en la mitad izquierda de la tabla, el globo abierto hacia la derecha
cabe en la ventana. Se cierra a los 5 s con el temporizador de la pestaña, al cambiar la consulta o al dejar la pestaña.
Muestra el dato entero: el nombre visible de la persona, la opción, o la fecha con el año (`dd/MM/yyyy HH:mm`). Nunca
muestra el código, que la fila no tiene.

Interpretación del gate, **para la revisión de David**: «la información completa» se lee como el dato entero de la
celda tocada, no como la fila entera. Si quiere la fila entera, cambia el contenido del globo, no el mecanismo.

- *Descartada*: el atributo `title`. El tooltip nativo del navegador no aparece al tocar en WebView2, y con el mouse
  tarda y se ve distinto al resto de la pantalla.
- *Descartada*: posicionar el globo dentro de la celda (`position: absolute`). `.caja-tabla` tiene `overflow: auto` y lo
  cortaría en las últimas filas.
- *Descartada*: medir la ventana con interop de JavaScript para orientar el globo. Las tres columnas quedan en la mitad
  izquierda, así que abrirlo hacia la derecha alcanza.

### D5. La medición en una prueba E2E nueva

`ConsumosEnUnRenglonE2ETests` mide por CDP a 1024 × 768: el alto de cada fila, la letra, que el `scrollWidth` de cada
celda no supere su `clientWidth` salvo en la persona y la opción (que cortan con «…»), y la alineación de los botones de
la fila. Deja la captura `consumos-un-renglon-1024x768`. Las personas largas del escenario se crean por la pestaña
Personas, como ya hace `AdministracionE2ETests`.

- *Descartada*: sumar la medición a `BuscarYExportarConsumosE2ETests`. Es modificar una prueba existente, que necesita
  la aprobación de David, y además ese E2E lo reescribe `reimprimir-sin-codigo`: tocarlo acá crearía un conflicto.

## Risks / Trade-offs

- **Un nombre cortado**: a 14 px y con los tres botones, la persona y la opción tienen unos 140 px cada una
  (estimación, a confirmar con la medición). Un nombre más largo se lee con el globo.
- **La columna de acciones reserva el ancho de «Recuperar»** aunque casi ninguna fila lo tenga; a cambio, los botones
  quedan alineados entre filas.
- **Conflicto con `reimprimir-sin-codigo`**: este change toca `PestanaConsumos.razor` en las celdas, y aquél en el
  bloque del operador, más arriba en el mismo archivo. Se rebasa después de aplicado aquél, y git los une sin solaparse.
