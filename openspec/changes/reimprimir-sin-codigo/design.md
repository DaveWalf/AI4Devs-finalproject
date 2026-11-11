# Design

## Context

Hoy, en `PestanaConsumos.razor`, «Reimprimir» y «Recuperar» pasan por `SesionOperador`. Sin sesión, la acción queda
pendiente y la pestaña muestra el pedido (`#pedido-operador`, `#codigo-operador`, «Ingresar», «Cancelar»). El código va a
`Asistencia.Entrar`, que abre una sesión `PRIVILEGED_ACTION` con persona y deja `ASSISTANCE_GRANTED` o `ASSISTANCE_DENIED`.
Si el operador entró a Configuración por el engranaje, `PantallaAtencion.razor` le pasa su sesión a la pestaña con
`SesionOperador.Adoptar`, y no se le pide nada.

`Asistencia.Reimprimir` y `Asistencia.Recuperar` exigen una sesión. `Actuar` busca su persona, vuelve a evaluar el
permiso `OPERATOR` (RB26) y registra `TICKET_REPRINTED` o `TICKET_RECOVERED` con `PersonId`, `GrantId` y
`GrantedRole`. El «Habilitó:» de la copia no se guarda en el trabajo: `TrabajosImpresion.LeerEnCola` lo lee del
`TICKET_REPRINTED` del mismo trabajo (D5 de `enriquecer-comprobante`), y lo escriben `ImpresoraSimulada` y
`ComprobanteEnPantalla`.

Con la clave del día no hay persona: Configuración registra sus acciones con `ActorPersonId` nulo, `NO_AUTHENTICATION`
y `Access` `DAILY_KEY`, igual que `CONSUMPTIONS_QUERIED`. La entrada del operador por el engranaje y sus rechazos
(RB25 y RB26) están en «Acceso de operador a Configuración» de `acceso-puesto`, y este change no los toca.

## Goals / Non-Goals

**Goals:**

- Que Consumos no pida un código en ningún caso, con las cuatro respuestas del gate (proposal).
- Que la copia hecha con la clave del día diga en el papel «SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD» y que la bitácora
  diga cómo se habilitó.
- Que RB08 siga intacto: ninguna acción cambia consumo, cupo ni importe.

**Non-Goals:**

- Los de la proposal: la mejora 4 (qué líneas imprime el comprobante), la mejora 5 (el operador desde reposo o el
  combo), ningún cambio de esquema ni del contrato con el receptor.

## Decisions

### D1. Con la clave del día, la acción va sin sesión, en su propio método

`Asistencia` suma `ReimprimirConClave(ticketId, motivo)` y `RecuperarConClave(trabajoId)`. Cada uno hace lo mismo que
su par con sesión, en la misma transacción `BEGIN IMMEDIATE`, pero no busca una persona ni evalúa un permiso. Su evento
lleva `ActorPersonId` nulo, `ActorReason` `NO_AUTHENTICATION` y `Access` `DAILY_KEY`, sin `GrantId`. Los métodos con
sesión, los del operador, no cambian, y para no duplicar el cuerpo se extrae una función común de lo que hoy hace
`Actuar` después de comprobar el permiso.

- *Descartada*: abrir una sesión `PRIVILEGED_ACTION` sin persona para la clave del día. La tabla de sesiones exige
  `comperCodigo`, y llenarlo con una persona inventada es el «UUID ficticio» que RB26 prohíbe.
- *Descartada*: un parámetro `Guid? sesion`, donde el nulo quiera decir «clave del día». Un nulo que se cuela por error
  pasaría a ser un acceso sin permiso. Un método con nombre propio lo deja a la vista en quien lo llama y en las pruebas.

Quién decide qué método se llama: `SesionOperador` sabe con qué acceso se entró. `PantallaAtencion.razor` le dice
`Adoptar(sesion, nombre)` (operador, como hoy) o `ConClaveDelDia()` (nuevo) al abrir Configuración, y `Salir` lo olvida
al cerrarla. Sin ninguno de los dos, la pestaña no ofrece «Reimprimir» ni «Recuperar». Así, un método «con clave» no se
alcanza fuera de una Configuración abierta con la clave (OWASP A01, control de acceso).

### D2. La marca de la copia se lee de la bitácora, como «Habilitó:»

`LeerEnCola` ya lee el `TICKET_REPRINTED` del trabajo para sacar el nombre. Se suma
`json_extract(bit.combitContexto, '$.Access') = 'DAILY_KEY'`, que llega como `SinValidez` a `ComprobanteAImprimir`.
`ImpresoraSimulada` y `ComprobanteEnPantalla` escriben `SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD` en el lugar donde iría
«Habilitó:». Una copia lleva una de las dos líneas, nunca las dos.

- *Descartada*: una columna nueva en `tbComedorTrabajosImpresion`. Es una migración de esquema a un día del cierre, para
  un dato que ya está, en la misma transacción, en la bitácora de la base.
- *Descartada*: escribir «Habilitó: SIN VALIDEZ…». David pidió que, sin identificación, el papel lo diga claramente;
  ponerlo bajo «Habilitó:» lo haría pasar por el nombre de una persona.

### D3. Recuperar con la clave del día no marca el original

El trabajo de copia 0 que falló por papel es el comprobante del consumo, que nunca salió. Se reenvía como hoy, sin
leyenda, sin «Habilitó:» y sin la marca. Sólo `TICKET_RECOVERED` dice que se hizo con la clave del día.

- *Descartada*: marcarlo «SIN VALIDEZ». El consumo es válido y ése es su único comprobante: marcarlo dejaría al
  comensal con un papel que dice que no vale. David aprobó D3 y D4 como están el 11/11/2026 a
  las 17:42 («apruebo la lista 1.1, D3 y D4 como están»).

### D4. La copia sin validez sigue numerada

Toma el número de copia siguiente, como toda copia: la unicidad `(TicketId, CopyNumber)` y el número dicen cuántos
papeles salieron de ese comprobante.

- *Descartada*: no numerarla. La copia 0 es el original, y una copia sin número rompería la cuenta de papeles que
  RB08 audita.

### D5. Consumos sin pedido ni «Salir»

Se van de `PestanaConsumos.razor` el bloque del pedido (`#pedido-operador`, `#codigo-operador`, `#entrar-operador`,
`#cancelar-operador`) y «Salir». `#aviso-comprobantes` queda, fuera del pedido, para la denegación de D6. Con un
operador, la fila dice «Operador: <nombre>» como hoy, sin botón. Con la clave del día no hay fila de operador. `SesionOperador` pierde `Pedir`,
`TomarPendiente`, `DescartarPendiente`, `Entrar` y `RevisarInactividad`, y `PantallaAtencion.razor` deja de llamarlo
por inactividad: la sesión del operador la cierra Configuración (`CONFIGURATION_CLOSED`), como ya decía D5 de
`buscar-y-exportar-consumos`.

Alineación: el aviso nuevo de D7 va en la sección del motivo, con la clase `aviso` y el mismo ancho que
`#aviso-incertidumbre`, encima de la fila de motivos. Los botones de motivo y «Cancelar» no cambian, siguen con el mismo
alto táctil, relleno y tipografía, y la fila «Operador: <nombre>» pierde su único botón, sin hermanos que alinear.

- *Descartada*: dejar «Salir» para el operador. Hoy sólo olvida la sesión adoptada y lleva al pedido de código, que ya
  no existe: después de tocarlo, la pestaña quedaría sin forma de reimprimir.

### D6. Un permiso que cae durante Configuración

Si `Asistencia.Reimprimir` o `Recuperar` deniegan con `PERMISSION_*` o `SESSION_NOT_USABLE`, `SesionOperador` muestra el
aviso del motivo (`#aviso-comprobantes`), no abre ningún pedido y deja la sesión adoptada como está: cada acción
siguiente vuelve a comprobar el permiso (RB26) y vuelve a denegarse. Configuración sigue abierta, y su cierre cierra la
sesión.

- *Descartada*: cerrar Configuración sola. El cierre de la sesión es de Configuración (D5 de
  `buscar-y-exportar-consumos`), y cerrarla en medio de una consulta sorprendería a quien la usa.
- *Descartada*: pasar a la clave del día. El operador no la tipeó, y pasaría a imprimir sin que nadie la haya dado.

La sesión del operador vence a los 2 minutos (`VigenciaSesion` de `IdentificacionPorCodigo`). Antes de este change, al
vencer, la pestaña volvía a pedir el código; ahora `SESSION_NOT_USABLE` en Consumos muestra
`MensajesAtencion.SesionOperadorVencida`: «La sesión del operador terminó. Cerrá Configuración y volvé a entrar con tu
código.», porque el «Identificate de nuevo» de la atención no dice dónde. Lo decidió David el 11/11/2026 sobre la
advertencia de la verificación (opción a).

- *Descartada*: renovar la sesión mientras Configuración esté abierta. Lo resolvería, pero cambia la vigencia fija de las
  sesiones privilegiadas a un día del cierre.

### D7. El aviso antes de una copia sin validez

Con la clave del día, la sección del motivo muestra `#aviso-sin-validez` con
`MensajesAtencion.CopiaSinValidez`: «La copia va a salir marcada SIN VALIDEZ: PRUEBA DE FUNCIONALIDAD porque se entró
con la clave del día.» Es una oración, así que termina en punto, y la prueba de `PuntoFinalTests` la incluye.

- *Descartada*: avisarlo sólo en el papel. La respuesta 1 pide «indicar claramente», y quien reimprime tiene que saberlo
  antes de gastar el papel.

## Risks / Trade-offs

- **Desviación de RB26 y de HU03-C**: con la clave del día se reimprime sin `OPERATOR`. La mitiga la marca en el papel y
  el `Access` `DAILY_KEY` en la bitácora, y la desviación queda como decisión del corte en `docs/decisiones.md`.
- **Pruebas existentes**: alrededor de veinte métodos en ocho archivos de pruebas tipean un código en Consumos o lo
  esperan. Cambiarlos necesita la aprobación de David (`AGENTS.md`), y la lista va en el grupo 1 de `tasks.md`.
- **Plazo**: entra antes del Typeform del 12/11. Reabre la punta verificada `7b80d45`: hacen falta la CI, la solución en
  Release contra el EXE nuevo y ver si el video muestra el pedido de código (`docs/guion-video.md`).
- **La clave del día de desarrollo acepta cualquier número de 1 a 12 dígitos**: quien la conozca reimprime. El papel
  queda marcado, así que una copia así no sirve como comprobante.
