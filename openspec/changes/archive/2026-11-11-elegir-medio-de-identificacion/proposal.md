## Why

El 10/11/2026 David pidió, como mejora 9a: «después de Red, el medio de identificación: simulador interno o un puerto
serie con terminadores y relleno de dígitos». Decidió que entra en la final con el envío simulado (decisión 11 del plan
`AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`), sobre la propuesta aparcada `identificar-por-puerto-serie` (`1f53512`) rehecha
sobre la punta, y sin IDSerialSim (decisión 16): un doble del lector en las pruebas y el lector real en su prueba. Es el
change 8 del §6b, con el gate dado (decisiones 12 y 13).

Hoy el puesto sólo identifica por el código tipeado en la pantalla de reposo; la lectura de tramas serie (RF23, AT07)
está en la segunda ola.

Origen: HU01.2 (escenario J, el código), F01, RF23 («reconstruir tramas serie parciales o múltiples; el ruido no genera
identidades falsas») y AT07 («trama partida, múltiples tramas, ruido y exceso de tamaño»); tickets TK01 y TK02.

## What Changes

- **La pestaña «Lector»**, después de Red y sólo con la clave del día, elige el medio de identificación: «Teclado» (como
  hoy), «Simulador interno» o «Puerto serie». Para el puerto: el puerto de los que el equipo tiene, la velocidad, los
  bits de datos, la paridad y los bits de parada; para los dos: el terminador (CR, LF, CR LF o TAB) y el relleno de
  dígitos (un código de sólo dígitos más corto se completa con ceros a la izquierda hasta N). Muestra el estado del
  lector y el resultado de la última lectura, sin el código.
- **Una lectura identifica a la persona** en la pantalla de reposo por el mismo camino que el código tipeado con
  «Ingresar»: mismas reglas de RB01 y RB25, mismos rechazos y mismos mensajes. El teclado en pantalla sigue.
- **Fuera de la identificación, la lectura se descarta**, sin cola: durante una atención, con un diálogo abierto o con el
  error de almacenamiento.
- **El armado de tramas**: el código en ASCII imprimible, terminado en el terminador elegido; las tramas partidas se
  arman, varias tramas en un bloque se separan, y el ruido, el exceso de largo o un resto de más de 2 segundos se
  descartan sin concatenar identidades (AT07).
- **El simulador interno**: con el puesto en mantenimiento y el medio en «Simulador interno», la pantalla de reposo
  muestra «Lector simulado», que manda lo tipeado y el terminador como bytes al mismo armado que el puerto real; así se
  prueba el lector sin hardware ni IDSerialSim. En operativo no se muestra.
- **Un puerto que falta, está ocupado o se desconecta** no traba la atención: el lector reintenta solo y la pestaña lo
  dice.
- **El código leído no queda en ningún registro**: la identificación por lector deja sus registros de siempre con el
  lector como origen, y los descartes y los cambios de estado van a la general sin el código.
- **`System.IO.Ports` `11.0.0`** entra como dependencia, sólo en el `Kiosk`.

## Non-goals

- La biometría 1:N y 1:1 y la homologación de un equipo real (protocolo, DTR y RTS, encabezados, CRC): segunda ola.
- IDSerialSim y los puertos virtuales (decisión 16).
- Identificar por el lector un operador en Consumos o en una acción privilegiada: sigue tipeando su código.
- Más de un lector a la vez.

## Capabilities

### New Capabilities

- `lector-identificacion`: el medio de identificación, el armado de tramas con su terminador y su relleno, cuándo una
  lectura identifica y cuándo se descarta, el simulador interno, el puerto que falta o se cae, la pestaña «Lector» y lo
  que queda registrado sin el código.

### Modified Capabilities

- `configuracion-puesto`: la pestaña Lector después de Red.

## Impact

- **Código**: `Kiosk` (una carpeta `Lector`: el armado, la configuración y su almacén, el puerto detrás de una
  interfaz con el real y el simulado, y el servicio que escucha; la pestaña; la entrada del lector en `MaquinaAtencion`
  y `PantallaAtencion`; el puerto real registrado por `MainWindow`) e `Infrastructure.Sqlite` (el origen de la lectura
  en el registro de la identificación, sin cambiar el tipeado). Sin migración.
- **Pruebas**: nuevas en `Kiosk.Tests` e `Infrastructure.Sqlite.Tests`, con el puerto simulado como doble, como permite
  `AGENTS.md`; ninguna prueba ni la CI abren un puerto COM. Pruebas existentes que cambian (decisión 13): las que fijan
  las diez pestañas (`PestanasConfiguracionTests`, `AccesoOperadorTests`, `AccionesEnConsumosTests`,
  `AccesoOperadorE2ETests`, `ConfiguracionPorPestanasE2ETests`, `ExportacionE2ETests` y `CorreoE2ETests`) y
  `PuntoFinalTests`, que clasifica los mensajes nuevos.
- **Dependencias**: `System.IO.Ports` `11.0.0` en `Directory.Packages.props` y en el `Kiosk`.
- **Documentos**: `AGENTS.md` (sale de la segunda ola la lectura de tramas serie), `readme.md` y
  `docs/aplicacion-del-curso.md`.

## Gate (decisión 13 del plan: dado, con límites)

Opciones recomendadas, aplicadas sin esperar:

1. La pestaña se llama «Lector» y su título «Medio de identificación»: con once pestañas, «Medio de identificación» no
   entra en la fila de 1024 × 768. Si «Lector» tampoco entra, el relleno lateral de las pestañas baja lo justo, sin
   tocar la letra ni el alto de 56 px, medido en la E2E.
2. Una lectura admite de 1 a 32 caracteres ASCII imprimibles sin espacios internos, con los de alrededor quitados; el
   relleno de dígitos (0 a 20, 0 sin relleno) sólo se aplica a una lectura de sólo dígitos.
3. Un resto de trama de más de 2 segundos se descarta antes del byte siguiente, para que un `00` colgado no se pegue a
   un `01` posterior.
4. La identificación por lector deja `SESSION_OPENED` o `IDENTIFICATION_REJECTED` de siempre con `Origin` (`SERIAL_PORT`
   o `SIMULATED_READER`), la clave que la simulación por lista ya usa;
   una lectura descartada va a la general como `READER_READING_DISCARDED`, a lo sumo una por motivo y por minuto, y los
   cambios de estado del puerto como `READER_STATE_CHANGED`. Ninguno lleva el código ni su largo.
5. El simulador interno se muestra sólo en mantenimiento, como el combo de la simulación por lista.
6. La configuración del lector es del equipo y no viaja en un resguardo; por defecto el medio es «Teclado», así un
   puesto sin lector se comporta como hoy.

Talla: XL. Si una prueba existente falla por otra razón, se para (cláusula del plan).
