## Context

El puesto ya registra en la bitácora general lo que pasa: identificaciones rechazadas, consumos denegados, el estado de
cada trabajo de impresión, las fallas de la exportación automática y el estado de las dos bitácoras. Nada de eso sale del
puesto. La decisión 11 del plan pide los correos con el envío simulado y la integración real como pendiente escrita, como
el SFTP del change `exportar-consumos-automaticamente`, cuyo modo «Caído» y cuya escritura atómica se reusan.

## Goals / Non-Goals

**Goals:** configurar los correos en una pestaña; avisar a los operadores y al Administrador Principal según la
situación; la señal de vida diaria; un envío simulado que se puede ver fallar y recuperarse.

**Non-Goals:** el envío real por SMTP, la clave o el token del segundo factor, y el aviso de un operador que habilita
otro consumo, que todavía no existe.

## Decisions

### D1. La configuración

`ConfiguracionCorreo` en `correo.json` de la carpeta de datos: activos o apagados, el cliente (1 a 60 caracteres), el
remitente, el servidor (las reglas del servidor SFTP), el puerto (1 a 65.535, 587 por defecto), el cifrado (`STARTTLS` o
`TLS`), el segundo factor (`APP_PASSWORD` u `OAUTH2`), hasta cinco casillas de operador, la del Administrador Principal y
la hora de la señal de vida (`HH:mm`, 08:00 por defecto). Cada casilla tiene la forma de una dirección (una arroba, un
dominio con punto, hasta 254 caracteres, sin espacios). Se valida al leer y al guardar, como la exportación; fuera de
forma, lo de fábrica: apagados. Ninguna clave ni token.

- **Descartada: guardar la clave cifrada con DPAPI ya.** Sin envío real no hay cómo probarla; el pendiente lo deja para
  el almacén de credenciales de Windows.

### D2. El correo y su asunto

`MensajeCorreo` lleva destinatarios, asunto y cuerpo en texto. El asunto siempre es «Cliente · Puesto N Nombre ·
Situación», con el número y el nombre de `puesto.json` («Puesto» solo si no hay número). Las situaciones son «Persona sin
consumo», «Impresora», «Exportación», «Bitácora», «Señal de vida» y «Prueba». Ningún cuerpo lleva un código de
identificación ni una credencial.

### D3. El envío simulado y la cola

`CorreoSimulado` escribe cada correo como un `.eml` (RFC 5322, UTF-8) en `correo-simulado\` con la escritura atómica de
la exportación, y tiene un modo «Caído» que falla como un servidor que no responde. Cada correo entra primero a la cola,
un archivo por correo en `correo-pendiente\`, y sale de ahí: si el envío falla, queda y se reintenta cada minuto, en
orden. La cola guarda hasta 500; uno más descarta el más viejo y lo registra.

### D4. Los avisos

`AvisosCorreo` arma cada aviso y lo encola si los correos están activos y la situación tiene destinatarios:

- **A los operadores**: una persona que no pudo comer al marcar (identificación rechazada con una credencial conocida,
  consumo denegado o sin opciones), con su nombre visible y el motivo en palabras; y un comprobante que no se imprimió
  (sin papel, una falla o un resultado incierto).
- **Al Administrador Principal**: una exportación automática que falla, con el destino y el motivo; y una bitácora que
  se degrada.

Un mismo aviso (situación y motivo) no se repite antes de 15 minutos; una bitácora avisa al pasar a degradada y no
mientras sigue así. Un código desconocido no avisa: no hay persona, y un tecleo errado no es un problema de gestión.

### D5. La señal de vida

Una vez por día, a la hora elegida o en la primera pasada después, al Administrador Principal: el resumen del día
anterior, contado de la bitácora general de la base (consumos autorizados, rechazos de identificación y consumos
denegados, y eventos de advertencia o error), sin nombres. `correo-estado.json` guarda la fecha de la última señal, así
un reinicio no la repite.

### D6. El reloj

Un reloj cada minuto, fuera del hilo de la pantalla y de a una pasada por vez: envía la cola y, si toca, la señal de vida.
Una falla queda registrada y no cierra el puesto, como la exportación.

### D7. La pestaña

«Mails», después de Impresora y sólo con la clave del día, con la columna de rótulos de Impresora: los campos de D1,
«Enviar una prueba» (un correo de prueba a todas las casillas, que pasa por la cola), «Guardar» (que valida y registra
`CONFIGURATION_CHANGED` de `MAIL` con el antes y el después, sin direcciones), el servidor simulado «Disponible» o
«Caído» (`SIMULATED_MAIL`) y los pendientes de la cola. La fila de diez pestañas tiene que seguir entrando en 1024 × 768.

### D8. La bitácora

`NOTIFICATION_QUEUED` (situación y cantidad de destinatarios), `NOTIFICATION_SENT` (situación) y `NOTIFICATION_FAILED`
(situación y motivo), sin direcciones, nombres ni códigos.

### D9. La persona de una identificación rechazada

`ResultadoIdentificacionCodigo` devuelve la persona y su nombre visible también cuando la credencial existe pero no está
vigente, sin abrir la sesión ni cambiar lo que se registra. La pantalla no la muestra: sólo el aviso la usa.

## Risks / Trade-offs

- [Datos personales fuera del puesto] → sólo el nombre visible, como la exportación de Consumos, y sólo a las casillas
  que el cliente configura.
- [Una cola que crece sin servidor] → tope de 500 con descarte del más viejo registrado.
- [Un aviso por cada consumo sin papel] → la ventana de 15 minutos.
