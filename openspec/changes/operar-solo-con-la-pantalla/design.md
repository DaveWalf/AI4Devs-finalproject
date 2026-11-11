## Context

El puesto ya tiene dos teclados en pantalla: el numérico de la clave del día (`DialogoClaveDelDia`) y el de letras de las
raciones (`TecladoEnPantalla`, en `PanelPersonas`), que escribe con una tecla que no saca el foco del campo. El resto de
los campos se tipea con un teclado físico. La pantalla y Configuración son Blazor; lo único que el puesto hace fuera de
Blazor está en `wwwroot/js/puesto.js`.

## Goals / Non-Goals

**Goals:** que cada campo de texto se pueda llenar sólo con la pantalla; que un técnico con teclado físico pueda ocultar
el teclado por la sesión; que los teclados que ya existen sigan como están.

**Non-Goals:** lo que el proposal deja en «Non-goals».

## Decisions

### D1. Un teclado general, abajo de la pantalla

`TecladoGeneral` usa `TecladoEnPantalla` con una quinta fila de símbolos (`@ . - _ : / \`), que Personas no muestra. Lo
dibuja la pantalla, abajo y del ancho de la pantalla, cuando un campo de texto tiene el foco y el teclado no está oculto.
**Descartada**: un teclado dentro de cada pestaña, que repetiría el componente diez veces y dejaría campos sin él.

### D2. Cuándo aparece

`puesto.js` escucha, desde que carga la página, `pointerdown`, `focusin` y `focusout` en el documento, y avisa a la
pantalla por un método estático de .NET (`TecladoGeneral.CampoTocado`) si el foco quedó en un campo de texto (un `input`
de texto, correo, número o búsqueda, habilitado y no de sólo lectura, fuera de un `data-teclado-propio`) **por un toque de
la pantalla táctil**; con un mouse o un teclado físico no aparece, porque quien los tiene no lo necesita. Al aparecer,
el campo se desplaza a la vista. La pantalla no llama a JS al dibujarse. **Descartada**: abrirlo con cualquier foco, que
movería la pantalla entre un clic de mouse y su efecto; y abrirlo con un botón al lado de cada campo, que suma un toque y
diez botones.

### D3. Cómo escribe

Cada tecla llama a `incomedor.teclear`, que escribe en el campo con el foco donde está el cursor (o al final, en los
tipos que no tienen selección, como el de correo), respeta su `maxlength` y dispara el evento `input`, así Blazor recibe
el valor como si se hubiera tipeado. Las teclas no sacan el foco del campo. **Descartada**: escribir en el estado de
cada componente desde C#, que obligaría a que el teclado conozca cada campo.

### D4. Ocultar por sesión

«Ocultar teclado» lo oculta hasta que se cierre Configuración; al abrirla de nuevo, el teclado está. En la pantalla de
reposo no hay «Ocultar teclado»: el comensal no tiene un teclado físico. **Descartada**: recordarlo en un archivo, que
dejaría un puesto sin teclado para el próximo que no tenga uno físico.

### D5. Lugar para el campo

Con el teclado a la vista, la pantalla suma una clase que acorta Configuración y la atención al alto que queda arriba
del teclado; el campo tocado queda a la vista con `scrollIntoView`. **Descartada**: un teclado que flota encima, que tapa
justo los campos de abajo.

### D6. Los teclados propios

La clave del día y la edición de Personas marcan su zona con `data-teclado-propio`: el general no aparece en sus campos.

## Risks / Trade-offs

- [Las E2E que llenan un campo y después miden o tocan] → el teclado acorta la pantalla: se miden ancho, alto y borde
  izquierdo, que no cambian, y Playwright desplaza hasta el botón; las que cambien se listan al aplicar.
- [Un tipo de campo sin selección] → `teclear` escribe al final.
