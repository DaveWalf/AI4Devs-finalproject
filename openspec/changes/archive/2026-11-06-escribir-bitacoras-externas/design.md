# Design

## Context

Ver `proposal.md` (Why). Lo que el código ya tiene, medido el 06/11/2026 sobre `1000578`:

- `tbComedorBitacora` (migración 0001) es el `AuditRecord` del diseño: `AuditId`, puesto, generación, `Sequence` única por puesto y generación, `BootId`, tiempos, zona, desplazamiento, `LogMonth` fijado al registrar, tipo, severidad, componente, versión de esquema y contexto JSON. La escribe `RegistroBitacora.Insertar` dentro de la transacción de quien la pide, que siempre es `BEGIN IMMEDIATE` (`BaseComedor.IniciarTransaccionEscritura`).
- Tipos que hoy se registran: `IDENTIFICATION_REJECTED`, `OPTIONS_OFFERED`, `CONSUMPTION_AUTHORIZED`, `CONSUMPTION_DENIED`, `PRINT_JOB_UPDATED`, `ASSISTANCE_GRANTED`, `ASSISTANCE_DENIED`, `TICKET_RECOVERED`, `TICKET_REPRINTED`, `PRIVILEGED_ACTION_DENIED`, `SYNC_ACK_INVALID`, `SYNC_BATCH_ACKNOWLEDGED` y `SYNC_BATCH_FAILED`. La severidad es siempre `INFO` y la generación, una constante.
- No se registran: la identificación aceptada, la selección, la cancelación, la inactividad, el cierre de la atención, el arranque y el cierre del puesto, ni nada de Configuración (el estilo se guarda en `estilo-visual.json` y el modo de la impresora vive en memoria).
- Los contextos de la decisión llevan identificadores pero no la etiqueta, el importe ni los cargos, que `UnidadConfirmacion` sí tiene a mano (`opcion.Nombre`, `opcion.Moneda`, `opcion.Escala`, `decision.Cargas`).

## Goals / Non-Goals

**Goals:**

- Que el archivo de interacciones de un mes alcance para contar cada atención sin abrir la base (AT41).
- Que ninguna falla de un archivo cambie el resultado de una confirmación (D-C14, RB23).
- Que una caída o una falla no dejen líneas duplicadas ni perdidas, con pruebas sobre archivos reales (AT43, AT44).

**Non-Goals:**

- Lo que el punto 1 del gate deja afuera.
- Integridad criptográfica de los archivos: son texto con ACL, y el diseño de la entrega 1 ya dice que eso no los hace inmutables.
- La pantalla de demostración anterior (`-- --demostracion`) no arranca el escritor: lo que registre se escribe la próxima vez que arranque la atención, porque el archivo es su propia marca (D2).

## Decisions

### D1. Los archivos son una proyección de la bitácora durable

Cada registro se guarda primero en `tbComedorBitacora`, en la transacción de su hecho cuando lo hay, y un escritor lo copia después a sus archivos. Es el patrón del outbox de lotes, aplicado a archivos.

- **Descartada:** escribir el archivo dentro de la transacción o antes del commit. SQLite y un archivo no comparten transacción: una falla de disco bloquearía la confirmación, que D-C14 y RB23 prohíben, y una caída entre los dos dejaría una línea de un hecho que nunca se confirmó.

### D2. La marca de entrega es el propio archivo

El escritor no guarda en la base qué entregó. Para cada destino lee la última línea completa de cada archivo mensual de esa carpeta y toma la mayor `Sequence`: lo pendiente es todo registro de ese destino con una `Sequence` mayor. La incidencia de un archivo se guarda como registros `LOG_DELIVERY_FAILED` y `LOG_DELIVERY_RECOVERED` en `tbComedorBitacora`, con la hora, la causa, el primer `AuditId` pendiente, el corte y la cantidad entregada; si hay una abierta se deduce del último de esos registros para ese destino.

Funciona porque cada `Sequence` se asigna dentro de un `BEGIN IMMEDIATE`: las escrituras van de a una, así que un lector nunca ve la 42 confirmada antes que la 41.

- **Descartada:** las tablas `AuditFileDelivery` y `LogDeliveryIncident` de `docs/modelo-datos.md` y `docs/arquitectura.md`. Piden la migración 0004 y una segunda escritura por línea, y dejan entre el flush y la marca una ventana incierta que igual obliga a buscar el `AuditId` en el archivo. Además cambian seis pruebas existentes que fijan la versión de esquema 3: `AperturaBaseTests.Base_vacia_queda_en_version_3_con_las_23_tablas`, `MigracionesTests.Base_en_version_1_se_actualiza_sin_perder_el_consumo`, `MigracionLotesTests.Base_en_version_2_con_un_evento_pendiente_queda_en_version_3`, `MigracionLotesTests.Base_en_version_1_queda_en_version_3_con_las_tablas_de_lotes_vacias`, `SemillaAtencionTests.Atencion_sobre_una_base_de_la_demostracion_en_version_1` y `ServicioDemostracionTests.Primer_arranque_siembra_los_datos_sinteticos`. Y si alguien borra un archivo, una marca en la base diría «entregado» sobre algo que ya no está; con la marca en el archivo, el escritor lo vuelve a escribir entero.

### D3. JSON Lines y un reparto fijo por tipo

Una línea es el objeto del contrato de la spec, serializado con `System.Text.Json` sin indentar, en UTF-8 sin BOM y terminado en `\n`. Las referencias (`IntentId`, `TicketId`, `IdentitySessionId`…) van dentro de `Context`, como ya están guardadas. El destino sale de una tabla fija de tipo a destino; un tipo que no está en la tabla va a la general.

| Destino | Tipos |
|---|---|
| Interacciones | `SESSION_OPENED`, `IDENTIFICATION_REJECTED`, `OPTIONS_OFFERED`, `OPTION_SELECTED`, `CONFIRMATION_REQUESTED`, `CONSUMPTION_AUTHORIZED`, `CONSUMPTION_DENIED`, `SESSION_CANCELLED`, `SESSION_TIMED_OUT`, `SESSION_CLOSED`, `ASSISTANCE_GRANTED`, `ASSISTANCE_DENIED`, `TICKET_RECOVERED`, `TICKET_REPRINTED`, `PRIVILEGED_ACTION_DENIED` |
| Las dos | `PRINT_JOB_UPDATED`, `LOG_DELIVERY_RECOVERED` |
| General | `KIOSK_STARTED`, `KIOSK_STOPPED`, `CONFIGURATION_OPENED`, `CONFIGURATION_CHANGED`, `CONFIGURATION_CANCELLED`, `SYNC_*`, `LOG_DELIVERY_FAILED`, `LOG_SEQUENCE_MISMATCH`, `LOG_LINE_REPAIRED` y todo tipo desconocido |

- **Descartada:** sacar las referencias del `Context` al primer nivel de la línea, como enumera el contrato de la entrega 1. Obliga a conocer cada tipo para armar la línea, y los registros ya guardados no las tienen afuera; adentro del `Context` siguen correlacionando igual.
- **Descartada:** un solo archivo con una columna de destino. D-C13 pide dos archivos independientes, y uno degradado no tiene que arrastrar al otro.

### D4. Un escritor serializado, fuera de la atención

Un único escritor por puesto, con un semáforo para que nunca corran dos pasadas a la vez, procesa los dos destinos en cada pasada: al terminar `Preparar`, cada 2 s y una última vez al cerrar el puesto. Por destino abre el archivo del mes de cada línea en modo agregar (`FileShare.Read`), escribe hasta 500 líneas y llama a `Flush(flushToDisk: true)`. Las pruebas llaman a la pasada directamente, sin temporizador.

Las pasadas automáticas se activan con `ServicioAtencion.BitacorasAutomaticas`, que la ventana pone siempre en verdadero. Sin esa propiedad el escritor existe y las pasadas las pide quien usa el servicio, porque las pruebas existentes del Kiosk arman el servicio sobre `%TEMP%\incomedor-tests\<guid>.db`, no lo liberan y borran la base al terminar: un temporizador siempre encendido podía tener la base abierta en ese instante (`File.Delete` falla en Windows) y mezclaba en una carpeta las bitácoras de muchas bases. El ciclo corre con `Task.Run` y `ConfigureAwait(false)`, fuera del hilo de la pantalla, porque `Dispose` lo espera desde ese hilo y, si el ciclo arrancara ahí, los dos quedarían esperándose (lo que corrigió `0c67056` y vio el E2E).

- **Descartada:** escribir después de cada commit en el hilo de la atención. Suma la escritura y el flush a cada toque, y una falla del archivo llegaría a la pantalla.
- **Descartada:** un escritor con su propio ciclo por destino. Duplica el temporizador y el cierre sin ganar nada: la independencia entre destinos la da que la falla de uno no corta la pasada del otro.

### D5. `Recovered` y `ExportedAtUtc`

`ExportedAtUtc` es la hora de la escritura. `Recovered` es verdadero si el destino tenía una incidencia abierta al empezar la pasada o si el registro se guardó en otro arranque (otro `BootId`), es decir, si llega tarde por una falla o por una caída.

- **Descartada:** un umbral de demora entre `RecordedAtUtc` y `ExportedAtUtc`. Depende del reloj y del intervalo de 2 s, y marcaría como recuperado un registro que sólo esperó su turno.

### D6. Reparar una línea parcial y detectar un desajuste

Si el archivo no termina en `\n`, los bytes desde el último `\n` se agregan a `<archivo>.reparacion.log` con un salto de línea, el archivo se corta en ese punto (`SetLength`) y la general recibe `LOG_LINE_REPAIRED` con el archivo y la cantidad de bytes. El registro cortado se vuelve a escribir desde la base, porque su `Sequence` queda por encima de la marca.

Antes de escribir, el escritor compara el `AuditId` de la última línea con el registro de la base que tiene esa `Sequence` en la misma generación. Si no existe o no coincide, la base no es la que escribió ese archivo (por ejemplo, una restaurada): ese destino no se escribe hasta el próximo arranque y la general recibe `LOG_SEQUENCE_MISMATCH`, una sola vez por arranque y destino.

- **Descartada:** renombrar o reescribir el archivo. Destruye la evidencia que la bitácora tiene que conservar.
- **Descartada:** seguir escribiendo. Con una base restaurada, cada registro nuevo con una `Sequence` ya usada quedaría por debajo de la marca y se perdería en silencio.

### D7. Incidencias agrupadas y sin recursión

Una `IOException` o una `UnauthorizedAccessException` al escribir un destino abre una incidencia sólo si no hay una abierta para ese destino: una falla repetida no agrega registros. Al volver la escritura, el escritor entrega lo pendiente hasta la `Sequence` más alta que había al empezar la pasada, y recién entonces registra `LOG_DELIVERY_RECOVERED`, que va a los dos archivos. Los registros del propio escritor pasan por la misma cola que los demás, así que una general caída guarda su incidencia como pendiente y la escribe al recuperarse.

- **Descartada:** registrar cada intento fallido. Es la recursión ilimitada que el diseño prohíbe: cada fallo agregaría un registro que tampoco se puede escribir.

### D8. Qué es una interacción

La atención registra lo que cambia su estado, no cada toque:

| Registro | Quién lo escribe | Contexto |
|---|---|---|
| `SESSION_OPENED` | la identificación, en la transacción que abre la sesión | sesión, propósito, persona, método `CODE` |
| `OPTION_SELECTED` | la máquina de atención, antes de marcar la opción | sesión, opción, versión, etiqueta, `DisplayedCharge` (el importe como se mostró, en texto) |
| `CONFIRMATION_REQUESTED` | la máquina, antes de llamar a la confirmación | sesión, `IntentId`, opción, versión |
| `SESSION_CANCELLED` | la máquina, al tocar «Cancelar» | sesión, opción elegida o nula |
| `SESSION_TIMED_OUT` | la máquina, al vencer los 30 s | sesión, estado desde el que venció |
| `SESSION_CLOSED` | la máquina, al volver a reposo desde un resultado | sesión, código del mensaje mostrado, `DecisionId` si hubo, `ClosedBy` (`DONE`, `TIMEOUT` o `RESULT`) |

La máquina escribe con una clase nueva de `Infrastructure.Sqlite` que abre su propia transacción corta y usa `RegistroBitacora`. El código del mensaje es el nombre de la constante de `MensajesAtencion` o, para un rechazo, su `ReasonCode`.

- **Descartada:** registrar cada toque (páginas, «Volver», toques sin efecto). El diseño de bitácoras lo excluye y no agrega nada que la reconstrucción necesite.
- **Descartada:** escribir `CONFIRMATION_REQUESTED` dentro de la confirmación. Cambiaría las cuentas de la transacción de RB06 que afirman tres pruebas existentes (`DecisionNuevaTests` y `CaidasYConcurrenciaTests`), y un pedido que se cae antes del commit tiene que quedar registrado igual.

### D9. Configuración y el puesto, con actor nulo

`ServicioAtencion` registra `KIOSK_STARTED` al terminar `Preparar` (versión informativa del ensamblado, `Environment.Version`, `RuntimeInformation.OSDescription`, `user_version` y versión de configuración; la que no se pueda leer, nula) y `KIOSK_STOPPED` al cerrar, antes de la última pasada del escritor. La pantalla registra `CONFIGURATION_OPENED`, `CONFIGURATION_CANCELLED` y `CONFIGURATION_CHANGED` con el ajuste (`VISUAL_STYLE` o `PRINTER_SIMULATION_MODE`), el antes, el después y el resultado (`APPLIED` o `FAILED`). Sin clave, `ActorPersonId` va nulo y `ActorReason` dice `NO_AUTHENTICATION`.

- **Descartada:** un actor genérico como «Administrador». Es la identidad ficticia que el diseño prohíbe; cuando D traiga la clave, el actor será el `PersonId` autenticado.

### D10. El contexto crece, no cambia

`OPTIONS_OFFERED` suma la etiqueta y el importe de cada ofrecida; `CONSUMPTION_AUTHORIZED` suma `IdentitySessionId`, la opción, su versión, su etiqueta, el precio (moneda, escala, importe en unidades menores) y los cargos (categoría, nombre, unidades, límite); `CONSUMPTION_DENIED` suma `IdentitySessionId`, la opción, su versión, su etiqueta y el mismo precio, porque la spec pide lo mostrado para toda decisión. El `IdentitySessionId` permite leer la atención entera «con la misma sesión». Ningún campo existente cambia de nombre ni de valor.

Pruebas existentes, medido al proponer: las que leen el contexto (`ConsultaDisponibilidadTests`, `IdentificacionPorCodigoTests`, `AsistenciaTests`, `TrabajosImpresionTests`, `ColaSincronizacionTests`) toman propiedades sueltas y ninguna compara el JSON entero; las que cuentan filas de `tbComedorBitacora` después de confirmar crean la sesión por SQL directo (`FixtureSintetico.NuevaSesion`), así que `SESSION_OPENED` no les suma una fila. Ninguna existente cambia.

- **Descartada:** una versión 2 del contexto con otra forma. Obligaría al lector a conocer dos formas sin ganar nada: agregar campos no rompe a quien lee los que ya conocía.

### D11. Dónde quedan los archivos

En `<carpeta de datos>\bitacoras\<KioskId>\`, al lado de la base y de `comprobantes\`, con la ruta resuelta con `Path.GetFullPath` y comprobada dentro de la carpeta de datos (OWASP A01). El `KioskId` sale de la configuración del puesto.

- **Descartada:** `%ProgramData%` o una raíz configurable aparte. La primera pide permisos de instalación que la demostración no tiene; la segunda va con las pestañas de Configuración de D.

### D12. Severidad

`RegistroBitacora.Insertar` suma un parámetro de severidad con `INFO` por defecto, de modo que las llamadas existentes no cambian. `LOG_DELIVERY_FAILED` y `LOG_SEQUENCE_MISMATCH` van con `ERROR`; `LOG_DELIVERY_RECOVERED` y `LOG_LINE_REPAIRED`, con `WARNING`.

- **Descartada:** deducir la severidad del tipo en el escritor. La severidad es del hecho, se guarda con él y no tiene que depender de quién lo lea.

## Risks / Trade-offs

- [Cada elección de opción suma una escritura corta] → A midió una escritura de este tamaño en 1 a 1,5 ms por toque en este equipo; la tarea 4.2 volvió a medir «Ingresar» y elegir con el cronómetro de A, en la misma sesión: unos +1,3 ms en «Ingresar» y de 4 a 6 ms por elección (`OPTION_SELECTED`, una transacción corta con su `fsync`). Falta la medida en la All-in-One.
- [La marca depende de leer el final de cada archivo] → se lee al arrancar y después de un error; entre pasadas la marca queda en memoria. Con una línea por registro, son unos pocos kilobytes por archivo.
- [Un antivirus o un indexador toma el archivo un momento] → es una `IOException` como cualquier otra: incidencia, reintento cada 2 s y recuperación sin duplicar.
- [Una base restaurada detiene un archivo] → se ve en Configuración y en la general; resolverlo con una generación nueva es parte del recorte (punto 1.1 del gate).
- [El primer arranque con el change escribe todo lo que la base ya tenía] → es correcto: son registros reales, salen con `Recovered` verdadero porque son de otro arranque, y la prueba de David en `C:\INComedor\prueba-monitor` los va a mostrar así.
- [Las pruebas del Kiosk que arman `ServicioAtencion` sobre una carpeta temporal la borran mientras el escritor corre] → las pasadas automáticas van detrás de `ServicioAtencion.BitacorasAutomaticas`, que sólo la ventana enciende, así que esas pruebas no tienen un temporizador que mantenga la base abierta; el ciclo corre con `Task.Run`, fuera del hilo de la pantalla, porque `Dispose` lo espera desde ese hilo. El escritor igual abre y cierra cada archivo dentro de la pasada. Si falla una prueba existente por esto, se para y se pregunta (punto 5 del gate).
- [El reloj hacia atrás deja líneas de un mes anterior al final del archivo de ese mes] → es la exportación tardía que pide AT42; la `Sequence` sigue ordenando.

## Migration Plan

Sin migración de base. Al primer arranque, el escritor encuentra la carpeta vacía y escribe todos los registros de `tbComedorBitacora` en sus archivos mensuales. Volver atrás es quitar el escritor: la base no cambió de forma, y borrar la carpeta `bitacoras\` sólo provoca que la próxima versión con el escritor la reescriba entera.
