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

La pantalla dibuja `TecladoEnPantalla` con una quinta fila de símbolos (`@ . - _ : / \`), que Personas no muestra, abajo
y del ancho de la pantalla, cuando un campo de texto tiene el foco y el teclado no está oculto; `TecladoGeneral` es sólo
el aviso estático que llega de `puesto.js`. Las teclas toman el estilo de los botones del puesto (la letra, el borde y el
radio de 12 px) y cada fila ocupa el ancho entero, así todas tienen los mismos bordes izquierdo y derecho y las teclas
son del mismo ancho dentro de cada fila. En Configuración, «Ocultar teclado» ocupa tres teclas, apartada de los símbolos
y con el borde del acento, y la quinta fila suma las diez columnas de las de arriba; en reposo, sus siete símbolos son
más anchos. **Descartada**: un teclado dentro de cada pestaña, que repetiría el componente diez veces y dejaría campos sin él.

### D2. Cuándo aparece

`puesto.js` escucha, desde que carga la página, `pointerdown`, `focusin` y `focusout` en el documento, y avisa a la
pantalla por un método estático de .NET (`TecladoGeneral.CampoTocado`) si el foco quedó en un campo de texto (un `input`
de texto, correo, número, búsqueda, teléfono o URL, habilitado y no de sólo lectura, fuera de un `data-teclado-propio`)
**por un toque de la pantalla táctil**; con un mouse o un teclado físico no aparece, porque quien los tiene no lo
necesita, y manda el último puntero. Un foco que llega hasta 1,5 s después de un toque cuenta como tocado: «Editar» en
Opciones, que enfoca el nombre, abre el teclado, y se asume. Al aparecer, el campo se desplaza a la vista y lleva
`inputmode="none"` hasta perder el foco, para que el teclado táctil de Windows no se abra encima. La pantalla lo
muestra sólo en reposo o con Configuración abierta y lo olvida al salir del reposo: si el campo sale de la pantalla sin
que se avise el foco (una lectura del lector), el teclado se va con él, y un toque fuera del teclado sin un campo con el
foco también lo cierra. Cada foco por un toque avisa otra vez, aunque el teclado ya estuviera abierto. La pantalla no
llama a JS al dibujarse. **Descartada**: abrirlo con cualquier foco, que
movería la pantalla entre un clic de mouse y su efecto; y abrirlo con un botón al lado de cada campo, que suma un toque y
diez botones.

### D3. Cómo escribe

Cada tecla llama a `incomedor.teclear`, que escribe en el campo con el foco donde está el cursor (o al final, en los
tipos que no tienen selección, como el de correo), respeta su `maxlength` y dispara el evento `input`, así Blazor recibe
el valor como si se hubiera tipeado; además marca el campo y, al salir de él, dispara `change`, como un teclado físico,
así los campos que Blazor lee con `onchange` (los de Opciones) reciben el valor antes del toque en «Guardar». En un campo
numérico acepta sólo dígitos: otra tecla vaciaría el valor al sanearlo. Ni las teclas ni un toque en el teclado fuera
de ellas sacan el foco del campo. **Descartada**: escribir en el estado de
cada componente desde C#, que obligaría a que el teclado conozca cada campo.

### D4. Ocultar por sesión

«Ocultar teclado» lo oculta hasta que se cierre Configuración; al abrirla de nuevo, el teclado está. En la pantalla de
reposo no hay «Ocultar teclado»: el comensal no tiene un teclado físico. **Descartada**: recordarlo en un archivo, que
dejaría un puesto sin teclado para el próximo que no tenga uno físico.

### D5. Lugar para el campo

Con el teclado a la vista, la pantalla suma una clase que acorta Configuración y la atención al alto que queda arriba
del teclado; el campo tocado queda a la vista con `scrollIntoView`, a los 150 ms y otra vez a los 400 ms, cuando la
pantalla ya se acortó, con un margen de 16 px. Configuración conserva sus bordes izquierdo y derecho y su lugar: sólo
cambia su alto, así nada de lo que está alineado se corre de costado. **Descartada**: un teclado que flota encima, que tapa
justo los campos de abajo.

### D6. Los teclados propios

La clave del día y la edición de Personas marcan su zona con `data-teclado-propio`: el general no aparece en sus campos.
**Descartada**: reemplazar los teclados propios por el general, que perdería el teclado numérico grande de la clave y el
de Personas, que elige el campo que edita.

## Risks / Trade-offs

- [Las E2E que llenan un campo y después miden o tocan] → el teclado acorta la pantalla: se miden ancho, alto y borde
  izquierdo, que no cambian, y Playwright desplaza hasta el botón; las que cambien se listan al aplicar.
- [Un tipo de campo sin selección] → `teclear` escribe al final.
- [El teclado táctil de Windows encima del general] → `inputmode="none"` mientras el general está abierto; se confirma en
  el AiO, en la prueba de David.
- [«Ocultar teclado» sin vuelta atrás en la sesión] → apartada de los símbolos y con el borde del acento. Una tecla
  «Mostrar teclado» cambiaría el MUST NOT del requisito, y queda para David; Personas conserva sus propios «Ocultar» y
  «Mostrar teclado».
- [Cada tecla redibuja la pantalla y Configuración] → sin medir en el AiO; se mira en la prueba de David.
