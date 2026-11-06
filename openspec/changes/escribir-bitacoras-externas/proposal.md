# Proposal

## Why

David pidió el 04/11: «Todo lo que hagan tanto personas como administradores en el sistema, tiene que tener una bitácora separada de la base de datos que permita una completa reconstrucción de qué se hizo o qué ocurrió en cada interacción». Hoy el puesto registra cada hecho en `tbComedorBitacora`, dentro de SQLite y en la misma transacción que el hecho, pero ningún archivo sale de la base, y una atención no se puede reconstruir: no quedan la identificación aceptada, la selección, la cancelación, el vencimiento por inactividad ni lo que hace Configuración.

Es el segundo de cuatro changes (A a D) que David decidió llevar a la final, con corte el 11/11 a las 12:00. El diseño existe desde la entrega 1 (D-C13, D-C14, RF43 a RF46, AT41 a AT44); este change implementa su núcleo.

Origen: historia HU03.5 («Seguir atendiendo con una bitácora degradada y reconstruirla», escenario HU03-H) y ticket TK03.

## What Changes

- **Dos archivos mensuales por puesto, fuera de SQLite.** `INComedor-general-AAAAMM.log` e `INComedor-interacciones-AAAAMM.log`, en `bitacoras\<KioskId>\` de la carpeta de datos, en JSON Lines UTF-8. El mes de cada línea es el del hecho, no el de la escritura.
- **Un escritor fuera de la atención.** Lleva a los archivos lo que ya quedó en `tbComedorBitacora`, en orden de secuencia, con flush durable. La atención nunca espera al archivo.
- **Reanudar sin duplicar.** El archivo es su propia marca de entrega: tras una caída, el escritor sigue desde la última línea completa, aparta una línea parcial como evidencia de reparación y no repite un `AuditId`.
- **Falla de un archivo.** La atención sigue, el otro archivo sigue, se abre una incidencia y la recuperación escribe lo pendiente con sus tiempos originales y la marca `Recovered`. Configuración muestra el estado y los pendientes de cada bitácora.
- **Lo que falta para reconstruir.** Registros nuevos de la atención (identificación aceptada, selección, confirmación pedida, cancelación, inactividad y cierre con el mensaje mostrado), del puesto (arranque y cierre con sus versiones) y de Configuración (abrir, guardar con el antes y el después, cancelar, modo de la impresora). Los registros de la oferta y de la decisión suman la etiqueta, el importe y los cargos que se mostraron.
- **`AGENTS.md`** deja de nombrar las bitácoras mensuales en la segunda ola y nombra lo que queda afuera.

## Non-goals

Lo que el diseño de la entrega 1 pide y este change deja afuera, con su porqué, es el punto 1 del gate: generación nueva al restaurar una base, saltos del reloj, evento de emergencia sin SQLite, espacio disponible, diagnóstico de bitácoras, conservación y acceso, raíz configurable y bitácora del receptor. Tampoco cambia ninguna decisión, cupo, consumo ni evento de salida: los archivos son una proyección de lo que la base ya guarda.

## Capabilities

### New Capabilities

- `bitacoras-externas`: los dos archivos mensuales, su contrato de línea, el reparto por tipo de evento, la entrega desde la bitácora durable, la reanudación tras una caída, la falla y recuperación de un archivo, y los registros que permiten reconstruir una atención y lo hecho en Configuración.

### Modified Capabilities

Ninguna. Los requisitos vivos que nombran «la bitácora» (`atencion-puesto`, `comprobante-local`, `envio-lotes`, `registro-consumo`) siguen valiendo tal cual: este change agrega registros y campos, no cambia ni quita ninguno.

## Impact

- **Código**: `Infrastructure.Sqlite` (escritor de archivos, reparto, registros nuevos, contexto ampliado de la oferta y la decisión, severidad en `RegistroBitacora`), `Kiosk` (registros de la máquina de atención y de Configuración, arranque y cierre, estado de las bitácoras en Configuración).
- **Pruebas**: nuevas en `Infrastructure.Sqlite.Tests` (archivos reales en carpetas temporales), `Kiosk.Tests` y un paso del E2E. Ninguna existente cambia, según lo medido al proponer (D10).
- **Documentos**: `AGENTS.md` (alcance), `readme.md` (dónde están las bitácoras), `docs/modelo-datos.md` y `docs/arquitectura.md` (la marca de entrega, D2), `docs/aplicacion-del-curso.md` (las filas que dicen «segunda ola»).
- Sin migración, sin dependencias nuevas y sin cambios en la CI ni en el receptor.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David | La das en el gate | | |
| La IA (Claude Code, Opus 5.5), 06/11/2026 | **M** | 4 a 6 h | 5,2 a 7,8 h de capacidad |

**Por qué la IA da M y no L.** El registro durable ya existe y está en la transacción de cada hecho; lo nuevo es proyectarlo a dos archivos, con pruebas de caída y de falla sobre archivos reales, y sumar unos doce tipos de registro. Con el recorte del punto 1 queda afuera lo que necesita piezas que hoy no existen (restauración de bases, detector de reloj, pantalla de diagnóstico). Como referencia, A se estimó S (2 a 3 h) y se aplicó en 0,35 h de reloj.

## Para el gate de David

1. **El recorte** (lo decidís vos). B entero (RF43 a RF46 y AT41 a AT44) no entra antes del corte junto con C y D: C (personas y opciones con franjas) y D (Configuración por pestañas) son cada uno más grandes que B recortado. Propuesta: entra el núcleo (RF43 a RF45, RF46 salvo el evento de emergencia, AT41, AT43, AT44 y de AT42 el cambio de mes y la exportación tardía). Queda afuera, con su porqué:
   1. **Generación nueva al restaurar una base** (AT42, RB24): el puesto no tiene procedimiento de respaldo ni de restauración. Entra sólo la detección: si la base y el archivo no coinciden en una secuencia, el escritor para ese archivo con una incidencia y no pierde nada en silencio (D6).
   2. **Saltos del reloj** (AT42): su detector es de la segunda ola (D-C17 y `AGENTS.md`).
   3. **Evento de emergencia cuando SQLite tampoco escribe** (RF46): que no se confirme un consumo sin SQLite ya lo cumple RB06; la línea de emergencia directa al archivo queda afuera.
   4. **Espacio disponible, diagnóstico de bitácoras, permisos de lectura y exportación, ACL, retención e integridad verificable**: el documento los pone «antes del piloto», y van con la administración.
   5. **Raíz de bitácoras configurable**: va con las pestañas de Configuración de D; hasta entonces, la carpeta de datos.
   6. **Bitácora del receptor**: no tiene acciones de personas; sus casos ya quedan en su base.
2. **La marca de entrega es el archivo** (D2). Tomada: el escritor sigue desde la última línea completa de cada archivo, sin tabla nueva. Descartada: `AuditFileDelivery` y `LogDeliveryIncident` como tablas, que proponen `docs/modelo-datos.md` y `docs/arquitectura.md`: piden la migración 0004, una segunda escritura por línea, una ventana incierta entre el flush y la marca, y cambiar seis pruebas existentes que fijan la versión de esquema 3 y las 23 tablas. Si elegís las tablas, esas seis pruebas cambian y la talla sube.
3. **Configuración sin actor** (D9). Hasta que D traiga la clave, abrir y guardar Configuración queda con actor nulo y motivo `NO_AUTHENTICATION`, sin inventar a nadie. D tiene que completarlo.
4. **Qué es «cada interacción»** (D8). Tomada: identificación, oferta, cada elección de opción, confirmación pedida y su resultado, cancelación, inactividad, cierre con el mensaje mostrado, impresión y acciones de asistencia. Descartada: cada toque, incluidas las páginas y los toques sin efecto, que el diseño llama «pulsaciones indiscriminadas».
5. **Pruebas existentes.** Ninguna cambia (D10). Si alguna falla durante el apply, se para y se te pregunta, como en A.
6. **`AGENTS.md`** (tarea 6.1). La línea del primer corte pasa a decir que las bitácoras mensuales entran por este change y nombra lo que sigue en la segunda ola (el punto 1).
7. **Rama.** `feature/escribir-bitacoras-externas-dw`, apilada sobre `feature/ofrecer-solo-opciones-disponibles-dw` (`1000578`). Su PR apunta a esa rama hasta que la integres, y después a `final-project-dw`.
8. **Tallas.** La de la IA es M; la tuya la das acá.
