# Design

## Context

- **El permiso.** `tbComedorPermisosOperadores` tiene una fila por persona y rol en cada versión (`UNIQUE (comvcCodigo,
  comperCodigo, compopRol)`), con vigencia desde y hasta y fecha de revocación. `AdministracionLocal` copia la tabla
  entera a cada versión nueva. `EvaluadorPermisoOperador` da el permiso por vigente si no está revocado y el instante
  cae en `[desde, hasta)`.
- **Personas.** La tabla de `PanelPersonas.razor` tiene el nombre, el estado, los cupos, las opciones y las acciones; en
  edición, las celdas son campos. Un operador entra a Personas desde el engranaje y, desde
  `operador-desde-la-identificacion`, también desde la identificación. Guardar ya registra al operador como actor.
- **Los Excel.** `ExportacionPersonas` escribe «Persona», «Estado», lo usado y el límite de cada cupo, y las opciones; su
  prueba lee las celdas por posición. `ImportacionPersonas` lee el encabezado, rechaza una columna desconocida
  (`UNKNOWN_COLUMN`), escribe la planilla de ejemplo con sus «Instrucciones» y arma el resumen y `PEOPLE_IMPORTED`.

## Goals / Non-Goals

**Goals:**

- Ver y editar quién es operador en Personas, y llevarlo y traerlo en los Excel (gate, respuestas 1, 3 y 4).
- Que un operador no pueda cambiar su propio permiso, ni por la tabla ni por la planilla (gate, respuesta 2).

**Non-Goals:**

- Los de la proposal: no se editan `SYSTEM_ADMINISTRATOR` ni la vigencia.

## Decisions

### D1. Dar y quitar sobre la fila que ya copia la versión

Al guardar, por cada persona cuyo «Operador» cambió:

- **Dar**: si la versión nueva ya tiene su fila `OPERATOR` (revocada o vencida, como `9002` o `9003`), se actualiza a
  desde = el guardado, hasta y revocación nulos; si no la tiene, se inserta con un `compopCodigo` nuevo.
- **Quitar**: se pone la revocación en el instante del guardado.

Todo pasa en la misma transacción que la versión. Lo que la tabla muestra como marcado es lo que
`EvaluadorPermisoOperador` da por vigente en ese instante, así que `9002` (vencido) aparece sin marcar, y marcarlo lo
vuelve a dar.

- *Descartada*: borrar la fila al quitar. La bitácora y las versiones anteriores tienen que poder decir que la persona
  fue operador, y la revocación es como lo modela RB26.
- *Descartada*: insertar siempre una fila nueva. La restricción `UNIQUE` por persona y rol lo impide.

### D2. La regla del propio permiso, en el servicio y en la pantalla

`AdministracionLocal.Guardar` y la importación reciben el actor, como hoy. Si el actor es un operador y el cambio toca
su propio permiso, el guardado se deniega entero con `OWN_PERMISSION` y queda en la bitácora como rechazado, sin crear
versión; en la importación, esa fila es un error con el mismo motivo. En la pantalla, la celda de su propia fila está
deshabilitada, con el título «No podés cambiar tu propio permiso.». Con la clave del día, el actor es nulo y no hay
fila propia.

- *Descartada*: controlarlo sólo en la pantalla. La importación esquivaría la regla, y RB26 pide comprobar en cada acción.

### D3. La columna «Operador» y su celda

La columna «Operador» va después de «Estado», angosta, con el contenido centrado.

- **En lectura**, la celda de un operador muestra un escudo en SVG en línea (`role="img"`, `aria-label="Operador"`),
  del color de `--tinta`; la de quien no es operador, vacía.
- **En edición**, la celda es un botón con `aria-pressed`, con el escudo lleno o en contorno, de 44 × 44 px.
- **El orden**: la columna se ordena como las demás, con los operadores primero en el orden ascendente.

Alineación: el botón comparte el alto de 44 px de los campos de cupo de la fila y queda centrado en su columna; el
encabezado «Operador» usa la letra y el relleno de los demás encabezados ordenables.

- *Descartada*: un emoji como ícono. Su dibujo depende de la fuente del sistema y no toma el color del tema.

### D4. Los Excel

- **La exportación** suma «Operador» al final de cada fila, con «Sí» o «No». Va al final para que no se corran las
  columnas que ya leen quienes usan la exportación, ni la prueba que lee las celdas por posición.
- **La planilla de ejemplo** pone «Operador» después de «Código», donde se completa junto con la identidad de la
  persona. La primera fila de ejemplo lleva «No» y la segunda «Sí», e «Instrucciones» suma una oración sobre «Sí», «No»
  y la celda vacía.
- **La importación** reconoce el título «Operador» y acepta «Sí», «Si» y «No» sin distinguir mayúsculas. Una celda vacía
  o la columna ausente no cambian el permiso. Cualquier otro valor, también una fórmula, es un error `INVALID_VALUE` de
  su fila, y el resumen no muestra lo escrito. El resumen suma «N permisos de operador dados y M quitados.», y
  `PEOPLE_IMPORTED` suma `OperatorGranted` y `OperatorRevoked`.

OWASP: es una entrada externa. La celda se compara contra una lista cerrada de valores, nunca se interpreta, y el valor
rechazado no llega al resumen ni a la bitácora (A03). La exportación escribe sólo «Sí» o «No», así que no hay fórmula
que inyectar.

- *Descartada*: «Operador» después de «Estado» en la exportación. Correría las columnas de lo usado y del límite, y
  rompería a quien lee la exportación por posición.
- *Descartada*: aceptar «1», «0», «true» o «x». Cada sinónimo es una ambigüedad más, y la planilla de ejemplo dice qué
  escribir.

## Risks / Trade-offs

- **Un operador puede crear operadores** (gate, respuesta 2). Queda en la bitácora con su persona como actor.
- **Marcar a alguien lo habilita al instante.** Con `operador-desde-la-identificacion`, esa persona ve «Configuración» en
  su próxima identificación.
- **Una prueba existente cambia**: `ImportacionPersonasTests`, la de la planilla de ejemplo, porque su encabezado
  cambia. Necesita la aprobación de David (tarea 1.1).
