# Design

## Context

Ver `proposal.md` («Why») para el pedido de David. El estado de `7d772c3` que este change toca, en `PantallaAtencion.razor`:

- En `Seleccion` y `Confirmando`, la pantalla tiene el bloque `.titulo` («Elegí tu servicio de hoy», `#saludo` y `#uso-del-dia`), el aviso, la lista de opciones, las páginas y, al pie, la barra `.barra` con `#eleccion` o la indicación, `.acciones` con `#cancelar` y `#confirmar`, y `#ocupado` mientras confirma.
- Tocar una opción llama a `Maquina.Elegir(id)`, que la marca: `aria-pressed`, la clase `elegida` y el texto «Elegida».
- `TeclaRaiz` sólo atiende Escape: en reposo cierra o pide la clave según el modo, y durante una atención no hace nada (`acceso-puesto`, «Escape durante una atención»).
- `HayDialogo` vuelve inertes la cabecera y el pie cuando hay un diálogo de Configuración o de la clave.
- La reconfirmación (RB10), sin opciones y el resultado son estados propios de la máquina, cada uno con su tarjeta.

## Goals / Non-Goals

**Goals:**

- Que «Confirmar» aparezca recién con una opción elegida, junto a lo elegido.
- Que la lista de opciones gane el alto de la barra del pie.
- Que la máquina de la atención no cambie: el cartel es estado de la pantalla.

**Non-Goals:**

- La reconfirmación dentro del cartel, cantidades o varias opciones por confirmación.

## Decisions

### D1. El cartel es un diálogo de la pantalla

Con la selección, la pantalla agrega `_cartelAbierto`. Tocar una opción llama a `Maquina.Elegir(id)` y abre el cartel. El cartel se dibuja sólo si `_cartelAbierto`, hay `Maquina.Elegida` y el estado es `Seleccion` o `Confirmando`; al salir de esos estados, `_cartelAbierto` vuelve a falso, así «Volver» de la reconfirmación, un rechazo o el resultado no lo dejan abierto.

El cartel es una `<section id="cartel-eleccion" role="dialog" aria-modal="true" aria-labelledby="titulo-cartel">` sobre un fondo que cubre la pantalla, con:
- el título «¿Confirmás tu elección?»;
- `<p id="eleccion">` con el nombre de la opción en grande y, sólo si hay cargo, «Cargo informado: …»;
- «Elegir otra» (`#elegir-otra`) y «Confirmar» (`#confirmar`, principal), de al menos 56 px;
- mientras confirma, «Confirmando…» deshabilitado con `aria-busy`, «Elegir otra» deshabilitado y `#ocupado`, como hoy.

`#confirmar`, `#eleccion` y `#ocupado` conservan sus `id`, así las pruebas que eligen y confirman siguen pasando.

- **Descartada: el cartel como estado nuevo de `MaquinaAtencion`.** Cambia la máquina y sus pruebas por algo que no decide nada: la confirmación y sus guardas siguen iguales.
- **Descartada: un `<dialog>` nativo con `showModal()`.** Pide interop de JavaScript para abrir y cerrar, y en bUnit no se puede probar. Un `role="dialog"` con el fondo inerte da lo mismo.

### D2. «Cancelar» pasa al bloque del título

Sale la barra `.barra` del pie de la selección. «Cancelar» (`#cancelar`, el mismo texto y la misma acción) va en `.titulo`, a la derecha del saludo, dentro de `<main>`, y mide al menos 56 px. Mientras confirma queda deshabilitado, como hoy. La cabecera sigue sin controles (`presentacion-puesto`).

- **Descartada: «Cancelar» dentro del cartel.** Para irse habría que elegir algo primero.
- **Descartada: «Cancelar» en la cabecera.** Rompe «la cabecera no tiene controles» y la prueba `CabeceraTests.Sin_engranaje_durante_la_seleccion`.

### D3. Foco, inercia y Escape

Al abrirse el cartel, el foco va a «Confirmar» (`ElementReference.FocusAsync` en `OnAfterRenderAsync`), y el título, la lista y las páginas quedan `inert`, igual que la cabecera y el pie con `HayDialogo`. Escape con el cartel abierto es «Elegir otra»: cierra el cartel y deja a la persona y la marca. Mientras confirma, Escape no hace nada. Sin cartel, Escape durante una atención sigue sin hacer nada.

- **Descartada: Escape no hace nada con el cartel.** Un diálogo modal que no se cierra con Escape contradice la convención que ya siguen Configuración y la clave.

### D4. El tamaño en 1024 × 768 y en 600 px

Sin la barra, la lista gana su alto. El cartel es fijo y centrado, de hasta 560 px de ancho y con el 16 px de margen en la ventana angosta, así que «Confirmar» se ve sin desplazar en los dos anchos. La distribución del catálogo no cambia.

- **Descartada: agrandar las opciones para llenar el lugar ganado.** Cambia las medidas que prueban «Distribución del catálogo por cantidad» y el pedido es lugar, no tamaño.

## Risks / Trade-offs

- [Una prueba E2E de geometría, `PresentacionTactilTests.Nueve_opciones_en_1024_por_768_y_una_columna_en_600`, espera que a 600 px la página se desplace, y sin la barra quizá no haga falta] → Si falla, se para y se le pregunta a David antes de tocarla.
- [El foco por `FocusAsync` no se puede ver en bUnit] → La prueba de componentes mira el `inert` y el orden; el foco se prueba en el E2E.
- [Dos changes abiertos tocan `atencion-puesto` y `presentacion-puesto`: la ampliación del 1 y éste] → Están apilados y se archivan en orden; éste se valida y se archiva después de que la spec viva tenga los del 1.
- [El corte del 11/11 a las 12:00] → Recorte del punto 5 del gate.

## Migration Plan

Sin migración. Para volver atrás alcanza con revertir el merge.
