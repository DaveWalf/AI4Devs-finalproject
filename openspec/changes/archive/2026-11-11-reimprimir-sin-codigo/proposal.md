# Proposal

## Why

El 11/11/2026 a las 16:40 David pidió, textual: «La reimpresión de tickets no debe solicitar un código extra.» Hoy, si se
entró a Configuración con la clave del día, «Reimprimir» y «Recuperar» en Consumos piden el código de un operador con
`OPERATOR` vigente (`#codigo-operador`); sólo no lo piden si ese operador entró a Configuración con su código. Quien ya
pasó la clave del día tiene que tipear un segundo código para sacar una copia.

Origen: historia HU03.1 (recuperar o reimprimir un comprobante con permiso, must), sus escenarios HU03-A, HU03-B y HU03-C,
y AT54; ticket TK03.1. El change se aparta a sabiendas de RB26 de `docs/especificacion-sdd.md` («la prohibición de
reimprimir sin OPERATOR») y del escenario HU03-C para el acceso con la clave del día: `docs/` es antecedente, y la
desviación la decidió David en el gate.

## Gate respondido (David, 11/11/2026)

1. Con la clave del día, que no identifica a nadie, la copia **no lleva «Habilitó:»** y dice claramente
   «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD». Respuesta textual: «Si no hay identificación debe indicar claramente: "SIN
   VALIDEZ: PRUEBA DE FUNCIONALIDAD"». En la bitácora, el actor queda nulo con `NO_AUTHENTICATION` y `Access` `DAILY_KEY`,
   como en `CONSUMPTIONS_QUERIED`.
2. «Recuperar» (el original que falló) **tampoco pide el código**.
3. Se **sigue pidiendo el motivo** de la lista fija.
4. La mejora **entra en `final-project-dw` antes del Typeform** (vence el 12/11/2026).

Con el código de un operador, todo sigue como hoy: su sesión, su permiso comprobado en cada acción y su nombre en
«Habilitó:».

## What Changes

- **Sin pedido de código en Consumos**: «Reimprimir» y «Recuperar» usan el acceso con que se entró a Configuración. Se
  van el campo `#codigo-operador`, «Ingresar» y «Cancelar» del pedido, la sesión de 30 s propia de Consumos y su «Salir».
- **La copia con la clave del día**: lleva la leyenda REIMPRESIÓN y su número de copia, como toda copia, y en lugar de
  «Habilitó:» la línea «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD». La pestaña lo avisa antes de elegir el motivo.
- **Recuperar con la clave del día**: reenvía el mismo trabajo original, sin leyenda ni marca, porque es el comprobante
  del consumo que nunca salió (D3 del design).
- **La bitácora**: `TICKET_REPRINTED` y `TICKET_RECOVERED` con la clave del día llevan el actor nulo,
  `NO_AUTHENTICATION` y `Access` `DAILY_KEY`, sin `PersonId` ni `GrantId`; con un operador, como hoy.
- **Un permiso que cae durante Configuración**: la acción se deniega con su motivo, la pestaña lo muestra y no pide un
  código; la sesión la sigue cerrando Configuración.
- **BREAKING (spec)**: se quita el requisito «El pedido del código de un operador en Consumos» y los escenarios del código
  tipeado en Consumos. Los rechazos de RB25 y RB26 al tipear un código quedan donde ya estaban: en el engranaje
  («Acceso de operador a Configuración» de `acceso-puesto`).

## Non-goals

- La mejora 4 de David (elegir en los textos del comprobante si se imprimen el nombre del comensal, el aviso de
  reimpresión y el operador) va en su propio change.
- La mejora 5 (el operador que se identifica en reposo o en el combo llega a sus comidas y a su Configuración) va en su
  propio change: este change no toca la entrada por el engranaje ni la identificación.
- No cambian «Ver» (COPIA EN PANTALLA), los motivos de la lista fija, el contenido del comprobante original ni la
  numeración de las copias.
- No se cambia RB26 en `docs/especificacion-sdd.md`: la desviación del corte queda como decisión en `docs/decisiones.md`.
- Ningún cambio de esquema SQLite ni del contrato con el receptor.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `comprobante-local`: «Asistencia con permiso OPERATOR» pasa a llamarse «Quién habilita reimprimir o recuperar en
  Consumos» y deja de pedir un código; «Recuperar o reimprimir sin tocar el consumo» suma la copia y la recuperación con
  la clave del día; se quita «El pedido del código de un operador en Consumos».

`consulta-consumos` y `acceso-puesto` no cambian: la consulta ya era de quien entra a Configuración sin otro código, y la
entrada del operador por el engranaje sigue igual.

## Impact

- Código: `Asistencia.cs` (reimprimir y recuperar con el acceso de la clave del día, sin sesión privilegiada),
  `TrabajosImpresion.cs` (la marca de la copia, leída del `TICKET_REPRINTED` como hoy «Habilitó:»),
  `ImpresoraSimulada.cs` y `ComprobanteEnPantalla.cs` (la línea de la marca), `SesionOperador.cs` y
  `PestanaConsumos.razor` (sin pedido), `PantallaAtencion.razor` (pasa el acceso a Consumos) y `MensajesAtencion`.
- Pruebas existentes que el cambio exige tocar, con la aprobación de David: la lista está en el grupo 1 de `tasks.md`.
- Documentación: `readme.md` (la reimpresión en Consumos), `docs/decisiones.md` (la desviación de RB26) y
  `docs/aplicacion-del-curso.md` (la evidencia).
