## Context

La pestaña Opciones de `PanelConfiguracion` muestra `#resumen-opciones` y `#abrir-opciones` («Editar», o «Ver» con INWeb
como dueño), que abre `PanelOpciones` (`#panel-opciones`) con una tarjeta `article.opcion-admin` por opción. La atención
arma su catálogo con `MensajesAtencion.Opcion` (nombre y cargo ya formateado; vacío con importe cero) y lo dibuja en
`PantallaAtencion` como botones `.opcion`, con el cartel de la elección que repite el cargo. El estilo visual de la
pantalla (`EstiloVisual`, `AlmacenEstiloVisual`) y los logos (`LogosLocales`, con `InputFile` y `Validar`) ya son
presentación local, en archivos de la carpeta de datos, con escritura atómica.

## Goals / Non-Goals

**Goals:** ver el catálogo en su pestaña y editarlo desde ahí; un fondo de color o de imagen, una transparencia y el
precio visible u oculto por opción; que la atención lo muestre.

**Non-Goals:** Excel (3b); el estilo de toda la pantalla; el comprobante y la bitácora sin importe.

## Decisions

### D1. El catálogo a la vista

La pestaña Opciones muestra, debajo de su resumen, `#catalogo-opciones`: una tarjeta `article.opcion-catalogo` por opción,
en el orden del panel, con su nombre, servicio, importe («Sin cargo» con cero), cargas y franjas («Todo el día» sin
franjas), una muestra de la tarjeta de la atención y dos botones: «Editar» abre `PanelOpciones` en el mismo lugar, como
`#abrir-opciones`, con el foco en el nombre de esa opción; «Personalizar» abre el cartel de D2. Con la edición abierta,
el catálogo no se dibuja: la edición ocupa su lugar y, al cerrarla, vuelve leído de nuevo.

- **Descartada: editar cada campo en la tarjeta de lectura.** Duplicaría la validación de `PanelOpciones` y la edición
  guardaría una versión por campo; abrir la edición de siempre en el lugar conserva una versión por guardado.
- **Descartada: usar `#panel-opciones` en modo lectura.** Las pruebas existentes cuentan con que no exista hasta tocar
  «Editar»; un contenedor propio no las toca.

### D2. El cartel de personalización

`#personalizar-opcion`, un cartel como el de «Ver» en Consumos, con la muestra de la tarjeta, «Fondo» (ocho colores de una
paleta fija y «Sin fondo»), «Imagen» (`InputFile` con PNG, WebP o SVG, validado con `LogosLocales.Validar`, y «Quitar
imagen»), «Transparencia» (0, 25, 50 y 75 %) y «Mostrar el precio» (`aria-pressed`). Elegir un color quita la imagen y
elegir una imagen quita el color. «Guardar» escribe y deja el aviso «Se guardó la presentación de «X».»; «Cancelar» y
Escape no escriben nada.

La paleta: `#F4E3C1`, `#EBCB9F`, `#DDB089`, `#E6B8B7`, `#D8C7E8`, `#BFD7EA`, `#C8D5B9`, `#A9C5A0`, tonos claros que se
leen con la tinta oscura del puesto.

- **Descartada: un selector de color libre.** Un color oscuro deja el nombre ilegible y no hay forma de avisarlo en el
  momento; la paleta lo evita.
- **Descartada: una barra deslizante para la transparencia.** En una pantalla táctil, cuatro botones de 56 px se aciertan;
  una barra pide precisión.

### D3. Dónde se guarda

`PresentacionOpciones` (por opción: color, imagen, transparencia y precio visible) y `AlmacenPresentacionOpciones`, que
lee y escribe `presentacion-opciones.json` en la carpeta de datos con escritura atómica, como `AlmacenEstiloVisual`, y
guarda cada imagen en `opciones\<SHA-256>.<extensión>`. Al leer, descarta lo que no valide (un color fuera de la paleta,
una transparencia que no sea 0, 25, 50 o 75, un nombre de imagen que no sea una huella con su extensión o que no exista),
como un archivo roto del estilo visual (OWASP A05): esa opción se ve sin personalizar. La clave es el id de la opción, que
no cambia entre versiones; una opción quitada deja una entrada que nadie lee.

- **Descartada: columnas nuevas en `tbComedorOpcionesMenu`.** Pedirían una migración, una versión por cambio de color y
  quedarían bloqueadas con INWeb como dueño; la mejora 16 pide justamente «más allá de lo que esté configurado».

### D4. La atención

Cada `.opcion` lleva sus variables (`--fondo-opcion`, `--opacidad-fondo`, `--imagen-opcion`) en su `style` y la clase
`personalizada` o `con-imagen`; el fondo se pinta en un `::before` con esa opacidad (1 menos la transparencia), así el
texto queda opaco. Con imagen, el nombre y el cargo llevan un respaldo claro para leerse sobre cualquier foto. Una opción
con el precio oculto no dibuja `.cargo` ni «Cargo informado» en el cartel, como una sin cargo; el comprobante y la
bitácora no cambian.

- **Descartada: ocultar el precio en `MaquinaAtencion`.** La máquina decide y registra con el cargo; ocultarlo es
  presentación y queda en la pantalla.

### D5. La bitácora

Cada guardado deja `OPTION_STYLE_CHANGED` en la general con `OptionId`, `Color`, `ImageSha256` (no la imagen),
`Transparency`, `PriceVisible`, cómo se entró y el actor nulo con `NO_AUTHENTICATION`, como los demás ajustes de la
clave del día.

- **Descartada: un `CONFIGURATION_CHANGED`.** Ése registra versiones de la configuración; la presentación no crea
  versión.

### D6. Prolijidad

Las tarjetas del catálogo comparten ancho y sus botones el alto táctil y la letra de «Cerrar»; en el cartel, los ocho
colores y «Sin fondo» miden lo mismo, y los cuatro pasos de transparencia también. Medido por CDP a 1024 × 768, con
capturas de la pestaña, del cartel y de la atención personalizada.

- **Descartada: medir sólo con bUnit.** bUnit no dibuja: el ancho y el alto sólo los da el navegador.

## Risks / Trade-offs

- [Una imagen pesada en cada tarjeta] → las reglas de los logos limitan el tamaño, y la página tiene a lo sumo diez
  tarjetas a la vez.
- [El archivo de presentación borrado] → todas las opciones se ven como hoy; no impide atender.

## Migration Plan

Sin migración. La reversión es revertir el PR; el archivo de presentación que quede no lo lee nadie.

## Open Questions

Ninguna.
