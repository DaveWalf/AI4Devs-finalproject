# Proposal

## Why

El 11/11/2026 a las 16:40 David pidió, textual: «En los textos del comprobante debe agregarse la opción de imprimir o no el
nombre del comensal. También si se imprimirá un aviso visual en caso de tratarse de la reimpresión del ticket, y qué
operador lo hizo.» Hoy el comprobante lleva siempre el nombre del comensal, una copia lleva siempre la leyenda
«REIMPRESIÓN · copia N», y «Habilitó:» con el operador o, desde `reimprimir-sin-codigo`, la marca «SIN VALIDEZ: PRUEBA DE
FUNCIONALIDAD» si se hizo con la clave del día.

Origen: historias HU03.1 (recuperar o reimprimir un comprobante con permiso) y HU01.1 (el comprobante del consumo), AT54
y RB08; ticket TK02.1 (interfaz).

## Gate respondido (David, 11/11/2026)

1. **Dónde y por defecto**: en la pestaña Impresora, junto al encabezado y el pie, y encendidas por defecto, para que un
   puesto que no las toca imprima como hoy.
2. **El aviso de reimpresión apagado** saca la leyenda REIMPRESIÓN, pero la copia sigue diciendo «Copia: N», como pide
   RB08.
3. **La marca «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD» no se apaga.** Textual: «Hay que distinguir operador, que es de la
   empresa dueña y quienes pagan (en ese caso SÍ tiene validez, y no lleva ese mensaje), de un técnico que esté
   configurando, y que no debe poder generar tickets válidos. En ese caso, si entró sólo con clave del día, no hay
   nombre, y debe aparecer ese texto.» La casilla del operador apaga sólo «Habilitó:».
4. **El nombre del comensal apagado** deja de salir en el papel y en el comprobante que sale en pantalla al confirmar;
   «Ver» en Consumos lo sigue mostrando.
5. La mejora entra en `final-project-dw` antes del Typeform del 12/11/2026 (decisión de las 17:55).

El delta lo aprobó David el 11/11/2026 a las 18:50 («aprobados los deltas»).

## What Changes

- **Tres elecciones en Impresora**: «Nombre del comensal», «Aviso de reimpresión» y «Operador que habilitó», cada una
  con «Imprimir» y «No imprimir», guardadas con el encabezado y el pie en `impresora.json` y registradas como hoy los
  textos (`TICKET_TEXTS`).
- **El papel y el comprobante en pantalla** siguen esas elecciones desde el próximo que sale; un archivo de antes, sin
  ellas, imprime las tres.
- **Una copia sin aviso** no lleva REIMPRESIÓN y tampoco «ORIGINAL»: lleva su «Copia: N».
- **La marca de la clave del día** sale siempre, sin importar las elecciones.

## Non-goals

- No cambian lo guardado de ningún comprobante, la numeración ni «Ver» (COPIA EN PANTALLA).
- No cambia quién puede reimprimir: eso es de `reimprimir-sin-codigo`.
- Quedan fuera las otras mejoras del 11/11: la tabla en un renglón, la versión del programa y el operador desde reposo
  van en sus changes, y el consumo excepcional del operador (la mejora 6) quedó abierto.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `comprobante-local`:
  - se suma «Las líneas que imprime el comprobante»;
  - «El contenido del comprobante» deja el nombre del comensal sujeto a esa elección;
  - «Recuperar o reimprimir sin tocar el consumo» deja la leyenda y «Habilitó:» sujetos a ella, con la marca siempre.

  Este último requisito lo modifica también `reimprimir-sin-codigo`, todavía sin archivar. El bloque de este change
  parte del de aquél, con todos sus escenarios, así que se archiva **después** de `reimprimir-sin-codigo`.

## Impact

- Código:
  - `TextosComprobante.cs` (`AlmacenImpresora`: las tres elecciones, leídas con valor por defecto);
  - `PanelConfiguracion.razor` (la sección de textos);
  - `PantallaAtencion.razor` (el guardado y el `TICKET_TEXTS`);
  - `ImpresoraSimulada.cs` y `ComprobanteEnPantalla.cs` (qué líneas se escriben).
- Pruebas existentes que el cambio exige tocar: ninguna prevista. Las que comparan el papel o el comprobante en pantalla
  corren con las tres elecciones encendidas, que es el valor por defecto. Se confirma con `git diff --diff-filter=M` en
  cada grupo de `tasks.md`.
- Documentación: `readme.md` (los textos del comprobante) y `docs/aplicacion-del-curso.md` (la evidencia).
