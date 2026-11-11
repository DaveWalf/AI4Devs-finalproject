# avisos-correo Specification

## Purpose
Los avisos por correo del puesto: los de gestión a los operadores, el estado técnico y la señal de vida diaria al Administrador Principal, con el asunto «Cliente · Puesto · Situación» y nunca un código de identificación, y su envío por una cola con un servidor simulado. Nació con el change `avisar-por-correo` (mejoras 10, 10b y 12 de David del 10/11/2026); el envío real por SMTP queda pendiente.

## Requirements

### Requirement: La configuración de los correos

La pestaña Mails SHALL ofrecer activarlos, el cliente, la casilla de origen (remitente, servidor, puerto, cifrado STARTTLS o TLS y segundo factor como método), hasta cinco casillas de operador, la del Administrador Principal y la hora de la señal de vida, con «Guardar», que MUST rechazar lo que no tiene forma con su campo señalado y registrar `CONFIGURATION_CHANGED` de `MAIL` sin direcciones. MUST NOT guardar ninguna clave ni token.

#### Scenario: Guardar los correos
Origen: mejora 10 de David del 10/11/2026 («destinatarios y casilla de origen con 2FA y cifrado») y decisión 11 del plan (envíos simulados).
- **GIVEN** la pestaña Mails
- **WHEN** se activan con el cliente «Cliente Demo», un remitente, el servidor `smtp.ejemplo.local` con STARTTLS y contraseña de aplicación, dos casillas de operador y la del Administrador Principal, y se toca «Guardar»
- **THEN** la pestaña avisa lo guardado, `correo.json` no tiene ninguna clave y la general tiene `CONFIGURATION_CHANGED` de `MAIL` con el antes y el después, sin ninguna dirección

#### Scenario: Casillas fuera de forma
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** la pestaña Mails
- **WHEN** se intenta guardar una casilla de operador sin arroba, un puerto 0 y la hora 25:00
- **THEN** cada campo se señala y la configuración no cambia

### Requirement: Los avisos por correo

Con los correos activos, el puesto SHALL avisar a las casillas de operador cuando una persona con una credencial conocida no puede comer al marcar, con su nombre visible y el motivo, y cuando un comprobante no se imprime; y a la del Administrador Principal cuando falla la exportación automática o se degrada una bitácora. El asunto MUST ser «Cliente · Puesto · Situación». Ningún correo MUST llevar un código ni repetirse antes de 15 minutos.

#### Scenario: Una persona sin consumo
Origen: mejora 12 de David del 10/11/2026 («problemas de una persona al marcar»).
- **GIVEN** los correos activos con el cliente «Cliente Demo», el puesto 3 «Comedor Norte» y una casilla de operador
- **WHEN** una persona con la credencial vencida marca su código
- **THEN** la casilla de operador recibe un correo con el asunto «Cliente Demo · Puesto 3 Comedor Norte · Persona sin consumo», el nombre de la persona y el motivo, sin su código

#### Scenario: La impresora sin papel
Origen: mejora 12 de David del 10/11/2026 («la impresora por cualquier motivo, incluida la falta de papel»).
- **GIVEN** los correos activos y la impresora simulada sin papel
- **WHEN** se confirman dos consumos seguidos
- **THEN** las casillas de operador reciben un solo correo de «Impresora» que dice que no tiene papel

#### Scenario: La exportación que falla
Origen: mejora 12 de David del 10/11/2026 («el estado técnico al Administrador Principal»).
- **GIVEN** los correos activos y la exportación automática al SFTP simulado caído
- **WHEN** corre una pasada
- **THEN** la casilla del Administrador Principal recibe un correo de «Exportación» con el destino y el motivo, y las de operador no

### Requirement: La señal de vida diaria

Con los correos activos, el puesto SHALL enviar una vez por día, a la hora elegida o en la primera pasada después, a la casilla del Administrador Principal, el resumen del día anterior: consumos, rechazos, comprobantes sin imprimir y advertencias o errores de la bitácora general, sin nombres. MUST NOT repetirla el mismo día aunque el puesto se reinicie.

#### Scenario: La señal de las 08:00
Origen: mejora 12 de David del 10/11/2026 («una señal de vida diaria con el resumen y las fallas»).
- **GIVEN** los correos activos con la señal a las 08:00 y dos consumos y un rechazo el día anterior
- **WHEN** el reloj pasa las 08:00 y después el puesto se reinicia
- **THEN** la casilla del Administrador Principal recibe una sola «Señal de vida» con 2 consumos y 1 rechazo

### Requirement: El envío simulado y la cola

Cada correo SHALL pasar por una cola en la carpeta de datos y salir como un `.eml` en `correo-simulado\`. Un correo que no sale MUST quedar en la cola y reintentarse cada minuto, en orden, hasta 500 pendientes. El servidor simulado MUST ofrecer «Disponible» y «Caído», y «Enviar una prueba» un correo a todas las casillas. La bitácora general MUST registrar cada encolado y envío sin direcciones. La conexión real queda pendiente.

#### Scenario: El servidor de correo cae y vuelve
Origen: mejora 10b de David del 10/11/2026 («envío de prueba en mails») y decisión 11 del plan.
- **GIVEN** los correos activos y el servidor simulado «Caído»
- **WHEN** se toca «Enviar una prueba», el servidor vuelve a «Disponible» y pasa un minuto
- **THEN** primero la prueba queda en la cola y la pestaña lo dice; después `correo-simulado\` tiene un `.eml` de «Prueba» y la cola está vacía
