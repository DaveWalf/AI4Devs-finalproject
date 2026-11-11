# Design

## Context

`ConsultaConsumos` (`Infrastructure.Sqlite/ConsultaConsumos.cs`) filtra por un rango de fechas de negocio
(`FiltroConsumos.Desde` y `Hasta`, `DateOnly`) y por persona, servicio y opción, ordena por una lista cerrada de columnas y
corta en 500. `PestanaConsumos.razor` abre con `FiltroConsumos.DelDia(Consulta.Hoy())`. La reimpresión y la recuperación
están en `PestanaComprobantes.razor`, sobre `SesionOperador` y `Asistencia` (`Reimprimir`, `Recuperar`, `Entrar`), con
la lista de `Asistencia.ComprobantesDelDia()`. `Asistencia.Reimprimir` sirve para cualquier comprobante, sin restricción
de día. Un operador que entra a Configuración con su código ya abre una sesión de asistencia (`PantallaAtencion.razor`,
`ProbarCodigo`, `_sesionDelOperador`). El número que muestra el comprobante son los primeros ocho caracteres de su
`TicketId` (`ComprobanteEnPantalla.Desde`).

## Goals / Non-Goals

**Goals:** las últimas 24 horas por defecto; buscar por texto y por código; el número de comprobante en la tabla; Ver,
Reimprimir y Recuperar en cada fila; sin la pestaña Comprobantes; exportar a CSV y XLSX a una carpeta recordada.

**Non-Goals:** la exportación automática o por SFTP (change de Exportación); cambiar RB26, los motivos o el comprobante
impreso; mostrar o exportar códigos.

## Decisions

### D1. Las 24 horas, por instante de confirmación

`FiltroConsumos` suma al final dos parámetros opcionales, `DesdeUtc` y `HastaUtc`. Con ellos, la consulta filtra por
`comconFechaConfirmacionLocalUtc` en `[DesdeUtc, HastaUtc]` en lugar de la fecha de negocio; sin ellos, como hoy. La
pestaña abre con `HastaUtc` = ahora y `DesdeUtc` = ahora − 24 h, y la columna Hora muestra siempre la fecha
(`dd/MM HH:mm`), porque las 24 horas cruzan dos días. «Últimas 24 h» vuelve a ese rango con el instante nuevo. Elegir
fechas pasa al rango de fechas de negocio, con sus reglas de hoy (366 días, sin invertir).

- **Descartada: hoy y ayer por fecha de negocio.** Muestra hasta 48 horas y no responde «las últimas 24 h».

### D2. El texto, normalizado en SQLite

La conexión de la consulta registra una función `normalizar(texto)` (`SqliteConnection.CreateFunction`) que quita
acentos (descomposición NFD sin marcas) y pasa a minúsculas. El filtro es
`normalizar(persona || ' ' || opción || ' ' || servicio || ' ' || substr(comprobante, 1, 8)) LIKE $patron ESCAPE '\'`, con
el texto normalizado y sus `%`, `_` y `\` escapados, así que un comodín tipeado es un carácter más (OWASP A03). Va en
SQL, antes del orden y del tope, como los demás filtros.

- **Descartada: filtrar en memoria.** Rompería el tope de 500 y el orden sobre todos los que cumplen.

### D3. El código, resuelto sin abrir sesión

Un campo propio, `type="text"` enmascarado con `-webkit-text-security`, `inputmode="numeric"`, `autocomplete="off"` y
`spellcheck="false"`, que se vacía al consultar. El código se resuelve contra las credenciales `SERIAL_CODE` de cualquier
versión, normalizado como al identificar (sólo `Trim()`), porque un consumo viejo puede ser de una credencial que ya no
está en la versión activa; no abre una sesión ni registra un rechazo de atención, y filtra por su `PersonId`. Uno que no
es de nadie avisa sin consultar. La bitácora dice sólo `PersonCode: true`, y `FiltroConsumos` no escribe el código en su
`ToString`. Mientras el filtro aplicado lleva un código, la pestaña dice que la tabla está filtrada por el código de una
persona, sin mostrarlo, porque ordenar y exportar lo siguen usando; una consulta sin código lo quita.

- **Descartada: buscar el código dentro del texto.** Lo dejaría a la vista en el campo y en la bitácora.
- **Descartada: `type="password"`.** Consumos no pide autenticación, y un campo de contraseña lo sugeriría; las pruebas de
  la pestaña afirman que no hay ninguno. El enmascarado por CSS oculta los dígitos igual en la pantalla del puesto.

### D4. Ver sin operador; Reimprimir y Recuperar con operador

Cada fila con comprobante tiene «Ver», «Reimprimir» y, si el original está en `FAILED`, «Recuperar». «Ver» muestra el
comprobante en un diálogo dentro de Configuración, con `ComprobanteEnPantalla` y la marca «COPIA EN PANTALLA», sin
llamar a la impresora ni a `Asistencia`: muestra lo que la fila ya muestra. «Reimprimir» y «Recuperar» siguen las reglas
de hoy (RB26): con la sesión del operador, el motivo para reimprimir y el aviso de incertidumbre; sin ella, piden el
código.

- **Descartada: las tres con operador.** «Ver» no crea copia ni cambia nada, y pedir un código para mirar frena al
  supervisor sin proteger nada.

### D5. La sesión del operador

`SesionOperador` suma `Adoptar(sesionId, nombre)`: si se entró a Configuración con el código de un operador, la pantalla
le pasa esa sesión, y Consumos no vuelve a pedir el código. Si no, la entrada del código va en la misma pestaña, arriba de
la tabla, como en Comprobantes, y vale para las filas hasta «Salir», cerrar Configuración o 30 s sin un toque. Si la
sesión adoptada vence (dos minutos), la acción se deniega con su motivo y Consumos pide el código.

La sesión adoptada es prestada: «Salir» y los 30 s la olvidan en Consumos sin cerrarla, porque es la del acceso a
Configuración, que la cierra una sola vez al cerrarse (`CONFIGURATION_CLOSED`). La acción que espera un código vive en
`SesionOperador` (`Pendiente`): el pedido dice qué va a hacer y con qué comprobante, y se cierra sin hacer nada con
«Cancelar» (que también descarta el aviso de una denegación), al dejar la pestaña o a los 30 s sin un toque, para que un
código tipeado minutos después no haga una acción que nadie ve.

- **Descartada: pedir el código siempre.** El operador acaba de tipearlo para entrar.

### D6. La exportación

`ExportacionConsumos` (en `Infrastructure.Sqlite`) lee los consumos del filtro aplicado, con el orden vigente y hasta
10 000, y escribe a un archivo temporal en la carpeta destino que después renombra, así que un error no deja un archivo
a medias. CSV en UTF-8 con BOM, separado por `;` y con comillas, que Excel abre en español; una celda que empieza con `=`,
`+`, `-` o `@` lleva un apóstrofo delante (OWASP, inyección de fórmulas). XLSX mínimo, escrito con
`System.IO.Compression`: el libro, una hoja con cadenas en línea y el cargo como número. Nombre
`INComedor-consumos-AAAAMMDD-HHmmss.csv` o `.xlsx`; si ya existe (un doble toque en el mismo segundo), `-2` a `-9`
antes de la extensión. El temporal lleva un GUID en el nombre y se borra en un `finally` si no se renombró. La carpeta la elige `IElectorCarpeta`, que la ventana registra con el
`OpenFolderDialog` de WPF y las pruebas reemplazan por un doble; se recuerda en `exportacion.json` de la carpeta de datos,
y «Cambiar carpeta» la vuelve a pedir. `CONSUMPTIONS_EXPORTED` va a la general con el formato, las filas, el nombre del
archivo sin la ruta completa y los filtros como la consulta (`From`, `To`, `LastHours`, `TextSearched`, `PersonCode`).

Las columnas son las de la tabla, con la fecha completa en la de la hora («Fecha y hora», `AAAA-MM-DD HH:mm` del puesto) y
el cargo partido en dos: el número, con coma decimal en el CSV y como número en el libro, y «Moneda» al lado, porque una
celda numérica no lleva el símbolo. Si se cancela la elección de la carpeta al exportar, la pestaña avisa que no se eligió
ninguna y no exporta ni guarda nada; al cambiarla, queda la anterior. Si la carpeta elegida no se puede guardar, se usa
hasta cerrar Configuración, con un aviso. La carpeta guardada que ya no existe no se vuelve a crear: es «sin poder
escribir». El libro descarta sólo los caracteres que XML no admite, recorridos por `Rune` para no perder los de fuera
del plano básico.

- **Descartada: una biblioteca para el `.xlsx`.** Suma una dependencia y su auditoría para una hoja de seis columnas.
- **Descartada: una carpeta fija.** La mejora pide una ubicación elegida.

### D7. Comprobantes sale

El `enum Pestana` pierde Comprobantes, `PestanaComprobantes.razor` se borra y su conducta pasa a `PestanaConsumos.razor`.
Las pruebas de `PestanaComprobantesTests` pasan a Consumos con lo mismo que afirman.

### D8. Prolijidad

La fila de búsqueda (el texto, el código, «Últimas 24 h» y «Consultar») y las acciones de cada fila siguen la regla de
`config.yaml`: los hermanos de una fila comparten alto (56 px en los filtros, 44 px en las acciones de la tabla) y bordes,
medidos por CDP a 1024 × 768 con captura. «Últimas 24 h» va con las fechas, que es su grupo, y «Consultar» cierra los
filtros, así que a 1024 × 768 son dos renglones sin un control solo; los botones de la pestaña usan la letra de 16 px de
sus campos (los de los filtros, la exportación y la fila del código del operador); las acciones de cada fila de la tabla
usan 15 px, la letra compacta de sus filas de 44 px. Los tres de la exportación miden lo mismo, el ancho del más largo.

- **Descartada: «Últimas 24 h» y «Consultar» al final de los filtros.** Con los dos campos nuevos, «Consultar» quedaba
  solo en un tercer renglón, y con 18 px de letra al lado de campos de 16 px: lo mostró la primera medición de la E2E.

Las oraciones nuevas de este change terminan en punto (decisión 15 del plan del cierre, regla de `config.yaml`); las que
ya estaban, también las de esta pestaña, las puntúa el change `puntuar-la-interfaz`.

## Risks / Trade-offs

- **[La tabla pierde ancho con la columna nueva y las acciones]** → El número de comprobante va con ocho caracteres y las
  acciones como botones cortos; la E2E mide que la tabla entra en 1024 × 768 sin desplazamiento horizontal.
- **[El `OpenFolderDialog` no se puede probar en la CI]** → La interfaz se prueba con un doble; el diálogo real lo prueba
  David en su monitor.
- **[La sesión adoptada vence a los dos minutos]** → Se deniega con su motivo y se pide el código, como en la sesión de
  la pestaña; la sugerencia abierta del change 3 sobre ese plazo sigue abierta.

## Migration Plan

Sin migración de base. `exportacion.json` es un archivo nuevo de la carpeta de datos, opcional.

## Open Questions

Ninguna: el gate está dado (decisión 13).
