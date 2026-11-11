# Proposal

## Why

El 10/11/2026 David pidió (mejora 7): «Opciones editables en el lugar; con un botón de personalización, color o imagen de
fondo y transparencias», y durante el goal del cierre (mejora 16, 18:33): «que el sistema debe poder configurar si mostrar
el precio de cada elemento o no, más allá de lo que esté configurado». Decidió que todo entra en la final, con el gate dado
(decisiones 10, 12 y 13 del plan `AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`). Es el change 3a del §6b; el 3b exporta e importa
Opciones en Excel (mejora 15).

Hoy la pestaña Opciones muestra sólo una línea («9 opciones en la versión 1 de la configuración») y «Editar»: para ver una
opción hay que abrir su edición. Todas las tarjetas de la atención se ven iguales, y el precio se muestra siempre que la
opción tenga importe.

Origen: historia HU01.1 (la selección y la confirmación) y HU01-D (no cambiar el importe en silencio), RF04 (catálogo
operable por toque), la decisión del 02/11/2026 sobre el cargo cero sin importe y D5 de `presentar-atencion-tactil` (la
presentación es local del puesto); ticket TK02.1.

## What Changes

- **Opciones en el lugar**: la pestaña muestra el catálogo con cada opción y sus parámetros (servicio, importe, cargas y
  franjas), una muestra de cómo se ve en la atención y dos botones por opción: «Editar», que abre la edición de siempre en
  el mismo lugar con el nombre de esa opción a mano, y «Personalizar».
- **Personalizar una opción**: un cartel con la muestra de la tarjeta, el fondo (un color de una paleta de ocho, una
  imagen PNG, WebP o SVG con las reglas de los logos, o ninguno), la transparencia (0, 25, 50 o 75 %) y «Mostrar el
  precio», con «Guardar» y «Cancelar».
- **La atención con la presentación**: cada tarjeta se dibuja con su fondo y su transparencia; una opción con el precio
  oculto se muestra como una sin cargo, en la tarjeta, en el cartel de la elección y en la reconfirmación por cambio de
  precio. El comprobante no cambia: el importe es su registro.
- **Presentación local**: se guarda en `presentacion-opciones.json` de la carpeta de datos, con las imágenes en
  `opciones\`, no en la configuración versionada: no crea versión y vale aunque INWeb administre los datos. Cada guardado
  queda en la general como un `CONFIGURATION_CHANGED` del ajuste `OPTION_STYLE`, con el antes, el después y el resultado,
  también si falla, y la huella de la imagen y no su contenido.

## Non-goals

- Exportar e importar Opciones en Excel: es el 3b.
- Cambiar el estilo de toda la pantalla (eso es la pestaña Identidad visual) o el tamaño y el orden de las tarjetas.
- Ocultar el importe del comprobante o de la bitácora.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `administracion-local`: las opciones a la vista en su pestaña y su personalización.
- `atencion-puesto`: la tarjeta de una opción con su presentación; el catálogo, el cartel de la elección y la
  reconfirmación, sin el cargo de una opción con el precio oculto.

## Impact

- **Código**: `Kiosk` (`PresentacionOpciones` y su almacén, el catálogo en la pestaña Opciones, el cartel de
  personalización, la tarjeta y el cartel de la elección en `PantallaAtencion`, `puesto.css`). Sin migración ni cambios
  en `Infrastructure.Sqlite`.
- **Pruebas**: nuevas en `Kiosk.Tests` y una E2E a 1024 × 768. Pruebas existentes que pueden cambiar (decisión 13):
  `PuntoFinalTests`, si se suman métodos a `MensajesAtencion`, y las que lean el resumen de la pestaña Opciones, si el
  catálogo a la vista las mueve; se listan en tasks.md con su porqué.
- **Documentos**: `readme.md` (Opciones) y la fila que corresponda de `docs/aplicacion-del-curso.md`.

## Gate (decisión 13 del plan: dado, con límites)

Opciones recomendadas, aplicadas sin esperar:

1. «Editable en el lugar» es el catálogo a la vista en su pestaña, con «Editar» por opción que abre la edición de siempre
   ahí mismo; la edición sigue guardando todo el catálogo en una versión, como hoy.
2. La personalización es por opción, en un cartel: fondo de color de una paleta de ocho tonos que se leen con la letra
   oscura, o imagen con las reglas de los logos (PNG, WebP o SVG, hasta su tamaño), o ninguno; transparencia en cuatro
   pasos táctiles (0, 25, 50 y 75 %), que dejan ver el fondo de la pantalla.
3. «Mostrar el precio» es por opción y empieza encendido; apagado, la opción se muestra como una sin cargo en la tarjeta y
   en el cartel, y el comprobante sigue diciendo el importe.
4. La presentación es local del puesto y no versionada, como el estilo visual (D5 de `presentar-atencion-tactil`): vale
   con INWeb como dueño («más allá de lo que esté configurado») y el resguardo del change 4 la va a incluir.
5. Personalizar se ofrece con la clave del día, como el resto de la pestaña Opciones; un operador no la ve.
6. (Agregada por la verificación.) Con el precio oculto, la reconfirmación por cambio de precio dice que la opción cambió
   sin mostrar el importe: la mejora 16 pide no mostrarlo «más allá de lo que esté configurado». La bitácora sigue
   registrando el cargo informado de la elección, porque es el registro.
7. (Agregada por la verificación.) La bitácora usa `CONFIGURATION_CHANGED` del ajuste `OPTION_STYLE`, como el estilo y los
   logos, con el guardado fallido incluido.

Talla: L. Si una prueba existente falla por otra razón, se para (cláusula del plan).
