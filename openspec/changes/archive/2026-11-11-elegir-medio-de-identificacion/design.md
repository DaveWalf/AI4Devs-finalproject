## Context

La propuesta aparcada `identificar-por-puerto-serie` (`1f53512`) diseñó el lector por puerto serie sobre `07a2329`. Desde
entonces Configuración ordenó sus pestañas, sumó Exportación y Mails, y David descartó IDSerialSim (decisión 16) y pidió
el medio de identificación con un simulador interno, terminadores y relleno de dígitos (mejora 9a). Este diseño rehace
aquella propuesta sobre la punta: conserva su armado de tramas, su puerto detrás de una interfaz y su registro sin el
código, y suma el simulador interno, el terminador elegible y el relleno.

Lo que el código ya tiene: la identificación tipeada (`MaquinaAtencion.Identificar`, sólo en reposo), los diálogos de
la pantalla (`HayDialogo`), el modo simulación por lista sólo en mantenimiento, y el molde de un dispositivo del equipo
registrado sólo por la ventana (`EjecutorNetsh`).

## Goals / Non-Goals

**Goals:** que una lectura identifique por el mismo camino que el código tipeado; que nada del lector se pruebe con un
puerto real; que con el medio «Teclado» el puesto quede exactamente como hoy.

**Non-Goals:** lo que el proposal deja en «Non-goals».

## Decisions

### D1. Una carpeta `Lector` en el Kiosk

`Kiosk/Lector/` tiene el armado de tramas (puro), la configuración con su almacén, `IPuertoSerie` con el puerto del
sistema y el simulado, y el servicio `LectorIdentificacion`, que abre el puerto, reintenta, arma las lecturas y publica
el estado y la última lectura. **Descartada**: ponerlo en `Infrastructure.Sqlite`, que es la base y no conoce
dispositivos.

### D2. El puerto detrás de una interfaz

`IPuertoSerie` abre con una configuración, entrega bloques de bytes por un evento, avisa si se cae y se cierra.
`PuertoSerieDelSistema` usa `System.IO.Ports.SerialPort` sin control de flujo, con DTR activo, leyendo bytes crudos, y
lo registra sólo `MainWindow`, como `EjecutorNetsh`. `PuertoSimulado` es el simulador interno y el doble de las pruebas:
lo que se le escribe llega como un bloque de bytes, por el mismo evento. **Descartada**: un simulador aparte que no pase
por el armado, que dejaría sin probar justo lo que el lector real necesita.

### D3. El medio de identificación

`lector.json` de la carpeta de datos guarda el medio («Teclado», «Simulador interno» o «Puerto serie»), el puerto
(`COM1` a `COM999`), la velocidad (una de 1200, 2400, 4800, 9600, 19200, 38400, 57600 o 115200), los bits de datos (7 u 8), la paridad (ninguna, par o impar), los
bits de parada (1 o 2), el terminador (CR, LF, CR LF o TAB) y el relleno (0 a 20). Se valida al leer y al guardar; fuera
de forma, lo de fábrica: «Teclado», 9600 8N1, CR LF y sin relleno. Con «Puerto serie», el puerto es obligatorio.
**Descartada**: guardarlo en la base, que lo haría viajar en un resguardo a otro puesto con otro hardware.

### D4. El armado de tramas

Sobre bytes: se acumula hasta el terminador elegido (con CR LF, también basta un CR o un LF solo, y la línea vacía entre
los dos no es una lectura). Una lectura válida tiene de 1 a 32 bytes de `0x21` a `0x7E` después de quitar los espacios de
alrededor (y de un CR o LF que sobra: con el terminador CR, un lector que manda CR LF deja un LF al comienzo de la trama
siguiente, que no se acumula); con un byte de control, fuera de ASCII o con espacios internos se descarta entera
(`INVALID_CHARACTERS`), una de más de 32 se descarta como `TOO_LONG`, y al pasar de 96 bytes sin terminador deja de
acumular hasta el próximo, para no crecer sin tope. Si pasan más de 2 segundos sin
bytes en medio de una trama, el resto se descarta antes del byte siguiente (`STALE_FRAGMENT`). El relleno completa con
ceros a la izquierda una lectura de sólo dígitos más corta que N. **Descartada**: no vencer los restos, que reproduce
la concatenación de identidades que AT07 prohíbe.

### D5. La lectura llega en otro hilo y se decide en el de la pantalla

El servicio arma con un `lock` y publica cada lectura por un evento; `PantallaAtencion` se suscribe al iniciar, se
desuscribe en `Dispose` y la pasa con `InvokeAsync` al hilo de la pantalla. Con un diálogo abierto la descarta
(`DIALOG_OPEN`); si no, la entrega a `MaquinaAtencion.IdentificarPorLector`, que descarta fuera de reposo (`NOT_IDLE`) o
con el error de almacenamiento (`STORAGE_ERROR`) sin poner ningún aviso, y en reposo identifica como el código tipeado.
**Descartada**: una cola de lecturas, que identificaría a alguien que ya se fue.

### D6. El simulador interno

Con el medio «Simulador interno» y el puesto en mantenimiento, la pantalla de reposo muestra «Lector simulado»: un campo
y «Pasar por el lector», que escribe lo tipeado y el terminador en el `PuertoSimulado`. La lectura recorre el mismo
armado, el mismo relleno y la misma decisión que una del puerto real, con un byte por carácter (una ñ llega como 0xF1 y
se descarta, como la descartaría un lector). En operativo no se muestra. Su fila comparte la grilla de la del código:
los dos campos en la primera columna y los dos botones en la segunda, del mismo ancho, con el rótulo encima ocupando
las dos (la regla de prolijidad). **Descartada**: un
campo del simulador dentro de Configuración, que con el diálogo abierto descartaría cada lectura.

### D7. Lo que queda registrado

`IdentificacionPorCodigo.Identificar` suma un parámetro con el origen, que va en la clave `Origin` que el combo de la
simulación ya usa: `SERIAL_PORT` o `SIMULATED_READER` en `SESSION_OPENED` e `IDENTIFICATION_REJECTED`; el código tipeado
sigue sin `Origin`. **Descartada**: una clave nueva `Reader`, que dejaría dos claves para decir lo mismo. Las lecturas descartadas van a la general como
`READER_READING_DISCARDED` con el motivo y la cantidad, a lo sumo una por motivo y por minuto; los cambios de estado del
puerto, como `READER_STATE_CHANGED`; el guardado de la pestaña, como `CONFIGURATION_CHANGED` de `READER`. Ninguno lleva
el código ni su largo. **Descartada**: un registro por cada lectura descartada, que un lector con ruido convertiría en
miles de filas.

### D8. La pestaña «Lector»

Después de Red y sólo con la clave del día, con el título «Medio de identificación» y la columna de rótulos de
Impresora: el medio, el puerto con «Actualizar», la velocidad, los bits, la paridad, la parada, el terminador, el
relleno, «Guardar», el estado del lector y la última lectura (la hora, si identificó, se rechazó o se descartó, y el
motivo, sin el código). Con once pestañas la fila tiene que seguir entrando en 1024 × 768: si no entra, baja el relleno
lateral de las pestañas lo justo, sin tocar la letra ni el alto. **Descartada**: «Medio de identificación» como nombre de
la pestaña, que no entra; y el lector como una sección de Red, que mezcla la red con un dispositivo.

### D9. Reintentos del puerto

Un puerto que falta (`PORT_NOT_FOUND`), ocupado (`PORT_BUSY`) o que se cae (`PORT_LOST`) se reintenta cada 5 segundos
con un reloj del servicio; la atención sigue por el teclado. Un lector USB que se desenchufa no levanta ningún evento de
`SerialPort`, así que mientras escucha el servicio pregunta cada 5 segundos si el puerto sigue abierto y en la lista del
equipo; si no, lo da por caído. Cerrar un puerto que ya no está puede fallar: se suelta igual. Un equipo sin puertos
serie (`PlatformNotSupportedException`) queda como «sin acceso a puertos serie». **Descartada**: pedir que se toque «Guardar» otra vez, que
dejaría un puesto sin lector hasta que alguien entre a Configuración.

## Risks / Trade-offs

- [La undécima pestaña no entra en una fila] → D8 baja el relleno lateral, medido en la E2E.
- [Una lectura y un toque en el mismo instante] → los dos pasan por el hilo de la pantalla, en orden.
- [El paquete nuevo en la publicación autocontenida] → la E2E contra el EXE toca «Actualizar», que carga
  `System.IO.Ports` y lee los puertos del registro de Windows sin abrir ninguno.
- [Un equipo real con otro formato] → fuera de este corte; el contrato queda escrito.

## Migration Plan

Sin migración. `lector.json` no existe en un puesto actual: arranca con «Teclado».
