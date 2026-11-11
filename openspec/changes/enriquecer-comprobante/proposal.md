# Proposal

## Why

El 10/11/2026 David pidió (mejora 9b), para la pestaña Impresora: «nombre del comensal, una de 50 frases cálidas al pie,
número y nombre del puesto, código de comprobante actual o correlativo desde un número, y REIMPRESIÓN o el operador que
habilitó», y que se le explique el algoritmo actual del código. Decidió que todo entra en la final, con el gate dado
(decisiones 10, 12 y 13 del plan `AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`). Es el change 5 del §6b.

Hoy el comprobante dice el encabezado, «ORIGINAL» o «DUPLICADO · copia N», el comprobante por los primeros ocho
caracteres de su identificador, la opción, la fecha de negocio, el cargo y el pie. **El algoritmo actual del código**: al
confirmar, `UnidadConfirmacion` crea el identificador del comprobante con `Guid.NewGuid()`, un GUID versión 4 (122 bits
aleatorios), y el comprobante muestra sus primeros ocho caracteres hexadecimales (32 bits). No es correlativo ni dice
nada del consumo, y la chance de que dos comprobantes muestren los mismos ocho caracteres es de una en unos 4.300
millones por par; el identificador completo, que es el que guarda la base, no se repite.

Origen: AT01 («ticket lógico asociado»), RB08 (la copia marcada y auditada), RB26 (el operador de la reimpresión); ticket
TK03.1.

## What Changes

- **El comprobante** suma el nombre del comensal, tomado al confirmar; el número y el nombre del puesto debajo del
  encabezado; y, al pie, después del texto configurable, una de 50 frases cálidas, siempre la misma para ese
  comprobante. Nunca el código de identificación.
- **El número del comprobante**, en Impresora: «el código de siempre» (los ocho caracteres de hoy) o «correlativo desde»
  un número, asignado al confirmar, dentro de la transacción de RB06, y guardado con el comprobante. Consumos lo muestra
  y lo encuentra.
- **La reimpresión** dice «REIMPRESIÓN · copia N» en lugar de «DUPLICADO · copia N», y «Habilitó:» con el nombre del
  operador que la pidió.
- **La pestaña Impresora** suma «Datos del comprobante»: el número y el nombre del puesto y la numeración, con su
  «Guardar».

## Non-goals

- Cambiar el comprobante guardado de un consumo ya confirmado: los datos nuevos valen desde el próximo.
- Editar las frases desde la pantalla.
- Un consumo habilitado por un operador fuera del cupo: no existe en este corte; «el operador que habilitó» es el de la
  reimpresión.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `comprobante-local`: el contenido del comprobante, su numeración y la reimpresión.

## Impact

- **Código**: `Infrastructure.Sqlite` (el nombre y el correlativo en los datos del comprobante al confirmar, la leyenda y
  el operador de una reimpresión, el número en Consumos) y `Kiosk` (las frases, el comprobante en la impresora simulada y
  en pantalla, los datos del puesto y la numeración en Impresora). Sin migración.
- **Pruebas**: nuevas en `Infrastructure.Sqlite.Tests`, `Kiosk.Tests` y una E2E a 1024 × 768. Pruebas existentes que
  cambian (decisión 13), porque David pidió lo contrario de lo que afirman: las que esperan «DUPLICADO · copia N»
  (`AsistenciaTests`, `TrabajosImpresionTests`, `PantallaAtencionTests`, `TextosComprobanteTests`), la que afirma que los
  datos de impresión no tienen ninguna propiedad de la persona (`TrabajosImpresionTests`, que sigue exigiendo que no
  tengan el código ni la credencial) y la que afirma que el comprobante en pantalla no tiene el nombre
  (`ComprobanteEnPantallaTests`); `PuntoFinalTests`, si se suman métodos a `MensajesAtencion`.
- **Documentos**: `readme.md` (el comprobante, con el algoritmo del código) y la fila que corresponda de
  `docs/aplicacion-del-curso.md`.

## Gate (decisión 13 del plan: dado, con límites)

Opciones recomendadas, aplicadas sin esperar:

1. El nombre del comensal va en el comprobante impreso y en el de pantalla, que es el mismo comprobante; se toma al
   confirmar y se guarda con él, así una reimpresión lo repite aunque la persona cambie de nombre. Los datos del
   comprobante no viajan al receptor.
2. La frase es una de 50 fijas, cálidas y sin datos de nadie, elegida por el comprobante: la misma en el original, en sus
   copias y en pantalla.
3. El número (1 a 999) y el nombre del puesto (hasta 30 caracteres) son del equipo: viven en `puesto.json` y no viajan en
   un resguardo. Sin ellos, el comprobante no los muestra.
4. La numeración también es del puesto, porque cada puesto lleva su serie en su base: vive en `puesto.json`. El
   correlativo es el mayor ya emitido más uno, nunca menor que el «desde» (1 a 999.999.999): bajar el «desde» no reusa
   números.
5. «Habilitó:» lleva el nombre visible del operador que la bitácora registró en la reimpresión; recuperar un original
   fallido no lleva leyenda, porque es el original.
6. La prueba de impresión lleva el puesto y una frase, sin persona ni número.

Talla: XL. Si una prueba existente falla por otra razón, se para (cláusula del plan).
