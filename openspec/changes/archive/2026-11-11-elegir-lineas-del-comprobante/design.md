# Design

## Context

- **Los textos.** `TextosComprobante` (encabezado y pie) se guarda en `impresora.json` con `AlmacenImpresora`, que valida
  al leer y al guardar y vuelve al valor por defecto si el archivo es ilegible o está fuera de forma. Se edita en la
  sección «Textos del comprobante» de la pestaña Impresora (`#encabezado-comprobante`, `#pie-comprobante`,
  `#guardar-textos`), y el guardado deja `CONFIGURATION_CHANGED` con `Setting` `TICKET_TEXTS`. El resguardo de la
  configuración (`resguardar-configuracion`) lleva y recupera `impresora.json`.
- **El papel.** `ImpresoraSimulada.Enviar` escribe el encabezado, el puesto, la leyenda (`comprobante.Leyenda ??
  "ORIGINAL"`), el número, «Comensal:», «Copia: N», y la marca «SIN VALIDEZ» o «Habilitó:», nunca las dos
  (`reimprimir-sin-codigo`). `TrabajosImpresion.Leyenda` da «REIMPRESIÓN · copia N» para toda copia.
- **La pantalla.** `ComprobanteEnPantalla` arma el comprobante que sale al confirmar y el de «Ver», con los mismos
  campos.
- **Los botones.** Configuración ya usa pares de botones con `aria-pressed` para elegir entre dos valores
  (`#correos-activos` y `#correos-apagados`, `#numeracion-codigo` y `#numeracion-correlativo`).

## Goals / Non-Goals

**Goals:**

- Que el puesto elija tres líneas del comprobante sin cambiar lo guardado (gate, respuestas 1, 2 y 4).
- Que una copia siga distinguible del original (RB08) y que lo hecho con la clave del día no pase por válido (gate,
  respuesta 3).

**Non-Goals:**

- Los de la proposal: «Ver», la numeración y quién reimprime no cambian.

## Decisions

### D1. Las elecciones viven en `impresora.json`, junto a los textos

`TextosComprobante` suma tres booleanos al final, `Comensal`, `AvisoReimpresion` y `Operador`, todos con valor por
defecto `true`. `AlmacenImpresora.Interpretar` acepta el archivo de hoy (sin ellos) y les da `true`. Con alguno
presente que no es booleano JSON, el archivo entero se toma como fuera de forma y vale el valor por defecto, como ya
pasa con un encabezado de 300 caracteres. `Serializar` escribe los cinco campos. El resguardo los lleva: lee el
archivo con `Leer` y lo vuelve a escribir con `Serializar`, y al recuperar lo interpreta con `Interpretar`, así que un
resguardo con una elección fuera de forma se rechaza entero (precisión de la verificación del 11/11/2026).

- *Descartada*: un archivo nuevo para las elecciones. Habría que sumarlo al resguardo y a su manifiesto, y David las
  pidió «en los textos del comprobante».
- *Descartada*: ignorar sólo el campo fuera de forma. Mezclaría lo leído con valores por defecto sin avisar; tomar el
  archivo entero como ilegible es la regla que ya tiene la pestaña.

### D2. Se guardan con el mismo «Guardar» de los textos

La sección «Textos del comprobante» suma tres filas debajo del pie: «Nombre del comensal», «Aviso de reimpresión» y
«Operador que habilitó», cada una con el par «Imprimir» y «No imprimir» (`aria-pressed`). `#guardar-textos` guarda los
textos y las tres elecciones juntos, y `TICKET_TEXTS` registra el antes y el después de los cinco campos.

Alineación: las tres filas son una grilla de dos columnas. A la izquierda va el rótulo, en la letra de los `label` del
encabezado y el pie. A la derecha va el par de botones, que comparten ancho, alto táctil, borde y tipografía con los
pares de Mails; el borde izquierdo de cada par cae en la misma vertical en las tres filas.

Ajuste al aplicar (11/11/2026), medido por CDP: los dos botones de Mails no comparten ancho entre sí (120 y 130 px, según
su texto y la negrita del presionado). Los seis de las líneas comparten uno, `min-width: 9em` (162 px a 18 px), que alcanza
a «No imprimir» presionado, y llevan el alto, la letra, el radio y el borde de cada estado de Mails.

- *Descartada*: casillas de verificación (`checkbox`). La pestaña no las usa, y su área de toque es más chica que la de
  los pares de botones que ya tiene.
- *Descartada*: un «Guardar» propio para las elecciones. Serían dos guardados para una misma sección y un mismo archivo.

### D3. El papel y la pantalla siguen las elecciones vigentes al enviar

`ImpresoraSimulada` y el comprobante que sale al confirmar leen las elecciones vigentes, como ya leen el encabezado
vigente (escenario «Una copia lleva los textos vigentes»):

- **Comensal** apagado: no escriben «Comensal:».
- **Aviso de reimpresión** apagado: una copia no lleva la línea de la leyenda. Tampoco lleva «ORIGINAL»: el `??
  "ORIGINAL"` pasa a valer sólo para la copia 0. La copia conserva su «Copia: N».
- **Operador** apagado: no escriben «Habilitó:».
- **La marca «SIN VALIDEZ»** se escribe siempre que la copia la tenga, antes de mirar la elección del operador.

«Ver» (COPIA EN PANTALLA) usa siempre las tres encendidas, como pidió David en la respuesta 4.

- *Descartada*: guardar las elecciones en el comprobante al confirmarlo. Cambiaría lo guardado (`comcmpDatosImpresion`)
  y el esquema; los textos ya son de la impresora y no del comprobante.
- *Descartada*: que la casilla del operador apague también la marca. David lo descartó: un técnico con la clave del día
  no debe poder sacar un comprobante que parezca válido.

## Risks / Trade-offs

- **El requisito «Recuperar o reimprimir sin tocar el consumo» queda en 500 caracteres**, el tope. Un cambio más va en
  un requisito nuevo.
- **El orden del archivado**: este change modifica ese requisito sobre el texto de `reimprimir-sin-codigo`, así que se
  archiva después de aquél. Si se archivara antes, el archivado de `reimprimir-sin-codigo` lo pisaría.
- **Un resguardo nuevo recuperado en un puesto con el código de hoy** conserva el encabezado y el pie e imprime las tres
  líneas, porque `Interpretar` de hoy ignora los campos que no conoce (lo leí en `TextosComprobante.cs`). Recuperarlo
  en un puesto ya actualizado conserva también las elecciones.
