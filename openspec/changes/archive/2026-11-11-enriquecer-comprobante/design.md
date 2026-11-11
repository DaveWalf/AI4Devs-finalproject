## Context

El comprobante nace en `UnidadConfirmacion`, dentro de la transacción de RB06: su identificador es un `Guid.NewGuid()` y
sus datos de impresión, un JSON con la opción, la fecha de negocio, la cantidad y el cargo, sin persona ni código. La
impresora (simulada o no) recibe un `ComprobanteAImprimir` con esos datos y la leyenda de la copia («DUPLICADO · copia N»
desde la 1), y le suma el encabezado y el pie de `impresora.json`. El comprobante en pantalla lee los mismos datos.
Consumos muestra y busca el comprobante por los ocho primeros caracteres del identificador. La reimpresión deja
`TICKET_REPRINTED` en la bitácora de la base con la persona del operador y el trabajo de la copia. Los eventos que salen
al receptor son registros tipados, sin los datos de impresión.

## Goals / Non-Goals

**Goals:** el nombre del comensal, el puesto, una frase cálida, la numeración elegible y la reimpresión con el operador,
en papel y en pantalla, sin migración y sin el código de identificación.

**Non-Goals:** cambiar comprobantes ya guardados, editar las frases, un consumo habilitado por un operador.

## Decisions

### D1. Los datos nuevos del comprobante

Al confirmar, los datos de impresión suman `PersonName`, el nombre visible de la persona en la versión activa, y, con la
numeración correlativa, `TicketNumber`. Quedan guardados con el comprobante: una reimpresión los repite. Un comprobante
viejo, sin ellos, se imprime como hasta hoy. Ni el código ni la credencial entran nunca.

- **Descartada: leer el nombre al imprimir.** Una reimpresión de un consumo de ayer mostraría el nombre de hoy, y un
  comprobante es lo que fue.

### D2. La numeración

`UnidadConfirmacion` recibe, al crearse, de dónde leer el «desde» del correlativo (nada con el código de siempre). Con
correlativo, el número es `MAX(TicketNumber) + 1` de los comprobantes de la base, o el «desde» si es mayor, calculado en la
misma transacción `BEGIN IMMEDIATE` de RB06: dos confirmaciones no pueden tomar el mismo. El número que se muestra es
`TicketNumber` si existe, o los ocho caracteres de siempre. Consumos lo muestra en su columna y lo encuentra al buscar
por comprobante.

- **Descartada: una tabla o una columna nueva para el contador.** Pide una migración y sube el esquema, que rompería los
  resguardos de hoy; el máximo de los datos guardados alcanza y no se puede desincronizar.

### D3. Los datos del puesto

`puesto.json`, en la carpeta de datos, con el número (1 a 999, opcional), el nombre (hasta 30 caracteres, sin caracteres
de control, opcional), la numeración («código» o «correlativo») y el «desde» (1 a 999.999.999), con su almacén, su
validación al leer y al guardar y la escritura atómica de los demás. Un archivo ilegible da los valores por defecto: sin
número ni nombre y el código de siempre. No entra en el resguardo: es del equipo.

### D4. La frase cálida

50 frases fijas en `FrasesComprobante`, de hasta 60 caracteres, cada una una oración con su cierre, sin datos de nadie. La
de un comprobante se elige por su identificador (sus primeros cuatro bytes, módulo 50): la misma en el original, en cada
copia y en pantalla. Va al pie, antes del texto configurable, así el comprobante sigue terminando con el pie que se
eligió.

### D5. La reimpresión

La leyenda de la copia N pasa a «REIMPRESIÓN · copia N». El trabajo de una copia busca en la bitácora de la base el
`TICKET_REPRINTED` de su trabajo y, con su `PersonId`, el nombre visible del operador en la versión activa; la impresora
lo recibe en `Habilito` y lo imprime como «Habilitó: …». La copia 0, también la recuperada, no lleva leyenda ni operador.

### D6. El comprobante, en papel, en pantalla y en «Ver»

De arriba abajo: el encabezado, «Puesto N · Nombre» (lo que haya), la leyenda o «ORIGINAL», el número, el comensal, la
opción, la fecha de negocio, el cargo, «Habilitó: …» en una copia, la frase y el pie. La impresora simulada conserva las
líneas de hoy (el identificador completo, la copia y los datos) y suma las nuevas; el comprobante en pantalla muestra las
nuevas con la letra y el ancho de las de hoy. «Ver» de Consumos muestra los datos del comprobante (el número, el
comensal, la opción, la fecha, el cargo y la frase), no los de la impresora, como hasta hoy. La prueba de impresión lleva
el puesto y una frase, sin persona ni número. El pedido del motivo de una reimpresión dice el número de la fila.

### D7. La pestaña Impresora

Una sección «Datos del comprobante», después de los textos: «Número del puesto», «Nombre del puesto», «Número del
comprobante» con «El código de siempre» y «Correlativo desde» más su número, y «Guardar», que valida y deja
`CONFIGURATION_CHANGED` del ajuste `TICKET_DATA` con el antes, el después y el resultado. Lo guardado vale desde el próximo
comprobante. Con un operador, la pestaña Impresora sigue a la vista, como hoy.

### D8. Prolijidad

Los campos de la sección comparten el alto táctil, la letra y los bordes de los de los textos; «Guardar», el de los
textos. Medido por CDP a 1024 × 768, con capturas, en una E2E que también confirma un consumo y lee el comprobante en
pantalla.

## Risks / Trade-offs

- [El nombre del comensal en papel y en pantalla] → lo pidió David; nunca el código. El comprobante en pantalla dura
  hasta tocarlo o 10 s, como hoy.
- [El número correlativo de dos puestos] → cada puesto lleva su serie; el número y el nombre del puesto los distinguen.
- [Leer el operador de la bitácora] → es el registro auditado de la reimpresión, escrito en la misma transacción que el
  trabajo; si faltara, la copia sale sin «Habilitó:».
- [El costo del correlativo] → el mayor emitido recorre los comprobantes dentro del bloqueo de RB06: unos 95 ms cada
  100.000, medido el 11/11 con SQLite 3.50. Leer sólo el último pide un índice, una migración y otro esquema, que dejaría
  sin recuperar los resguardos de hoy: queda como deuda, para cuando un puesto acumule historia.
- [La frase de un comprobante viejo] → sale de la lista por posición: la lista no se reordena, no se edita y no cambia de
  largo, y una prueba fija la frase de un comprobante conocido.
- [Un archivo de datos tomado por otro programa] → el almacén devuelve lo último que leyó o guardó, así un comprobante no
  sale con el código de siempre en medio de la serie.

## Migration Plan

Sin migración. La reversión es revertir el PR.

## Open Questions

Ninguna.
