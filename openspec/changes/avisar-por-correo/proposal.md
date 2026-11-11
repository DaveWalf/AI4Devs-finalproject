## Why

El 10/11/2026 David pidió tres mejoras de correo. La 10: «pestaña de mails después de Impresora: destinatarios y casilla
de origen con 2FA y cifrado». La 10b: «impresión y envío de prueba en Impresora y en mails» (la impresión de prueba ya
existe en Impresora). La 12: «en mails, casillas de operador para las advertencias de gestión (problemas de una persona al
marcar, un operador que habilita otro consumo, la impresora por cualquier motivo, incluida la falta de papel) y una del
Administrador Principal para el estado técnico y una señal de vida diaria con el resumen y las fallas; el asunto, siempre
Cliente, luego puesto, luego tipo de situación». Decidió que todo entra en la final con los envíos simulados (decisión 11
del plan `AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`) y el gate dado (decisiones 12 y 13). Es el change 7 del §6b.

Hoy el puesto no avisa nada fuera de su pantalla: una impresora sin papel, una persona rechazada o una exportación que
falla sólo quedan en la bitácora, y nadie se entera hasta que alguien mira.

## What Changes

- **La pestaña Mails**, después de Impresora y sólo con la clave del día: «Activos» o «Apagados»; el nombre del cliente
  para el asunto; la casilla de origen (remitente, servidor, puerto, cifrado y segundo factor como método, sin guardar
  ninguna clave); hasta cinco casillas de operador y la del Administrador Principal; la hora de la señal de vida;
  «Enviar una prueba», «Guardar», el servidor simulado «Disponible» o «Caído» y la cola de pendientes.
- **Los avisos de gestión**, a las casillas de operador: una persona que no pudo comer al marcar (credencial vencida,
  revocada, sin habilitación, fuera de horario o sin cupo, con su nombre visible y nunca su código) y la impresora que no
  imprimió un comprobante (sin papel, una falla o un resultado incierto).
- **El estado técnico**, a la casilla del Administrador Principal: una exportación automática que falla y una bitácora
  que se degrada; y **la señal de vida**, una vez por día a la hora elegida, con el resumen del día anterior (consumos,
  rechazos y advertencias de la bitácora).
- **El asunto** siempre es «Cliente · Puesto N Nombre · Situación».
- **El envío simulado**: cada correo se escribe como un `.eml` en `correo-simulado\` de la carpeta de datos. Uno que no
  sale queda en la cola (`correo-pendiente\`) y se reintenta cada minuto; la conexión real queda como pendiente escrita.
- **La bitácora general** registra el ajuste (`CONFIGURATION_CHANGED` de `MAIL` y de `SIMULATED_MAIL`) y cada envío
  (`NOTIFICATION_SENT`, `NOTIFICATION_QUEUED`), sin direcciones, nombres ni códigos.

## Non-goals

- Enviar por un servidor real, guardar o pedir la clave o el token del segundo factor: pendiente escrita, como pide la
  decisión 11.
- El aviso de «un operador que habilita otro consumo»: en este corte un operador no habilita consumos fuera del cupo; el
  aviso queda listado para cuando exista.
- Correos con datos de personas más allá del nombre visible en un aviso de gestión.
- «Enviar una prueba» en Impresora: la prueba de envío vive en Mails, junto a la casilla que prueba; Impresora conserva
  «Imprimir una prueba».

## Capabilities

### New Capabilities

- `avisos-correo`: la configuración de los correos, los avisos de gestión y técnicos, la señal de vida y el envío
  simulado con su cola.

### Modified Capabilities

- `configuracion-puesto`: la pestaña Mails después de Impresora.

## Impact

- **Código**: `Infrastructure.Sqlite` (la persona de una identificación rechazada en el resultado, sin tocar la bitácora;
  el resumen del día) y `Kiosk` (la configuración, la cola, el correo simulado, los avisos y su reloj, la pestaña). Sin
  migración.
- **Pruebas**: nuevas en `Infrastructure.Sqlite.Tests`, `Kiosk.Tests` y una E2E a 1024 × 768. Pruebas existentes que
  cambian (decisión 13): las que fijan las nueve pestañas y su orden, porque David pidió una más después de Impresora
  (`PestanasConfiguracionTests`, `AccesoOperadorTests`, `AccionesEnConsumosTests`, `AccesoOperadorE2ETests`,
  `ConfiguracionPorPestanasE2ETests` y `ExportacionE2ETests`), y `PuntoFinalTests`, que clasifica los métodos nuevos de
  `MensajesAtencion`.
- **Documentos**: `readme.md` (los avisos y el envío real como pendiente) y la fila que corresponda de
  `docs/aplicacion-del-curso.md`.

## Gate (decisión 13 del plan: dado, con límites)

Opciones recomendadas, aplicadas sin esperar:

1. El segundo factor y el cifrado se configuran como métodos (contraseña de aplicación u OAuth 2.0; STARTTLS o TLS
   directo), sin guardar ninguna clave: el envío es simulado y el secreto real irá al almacén de credenciales de Windows.
2. Los avisos de gestión van a los operadores con el nombre visible de la persona y el motivo, nunca su código; los
   técnicos y la señal de vida, al Administrador Principal.
3. Un mismo aviso (la misma situación con el mismo motivo) no se repite más de una vez cada 15 minutos; una bitácora
   avisa cuando se degrada, no mientras sigue degradada.
4. La señal de vida sale una vez por día, a la hora elegida (08:00 por defecto), con el resumen del día anterior.
5. Un correo que no sale queda en la cola de la carpeta de datos y se reintenta cada minuto, hasta 500 pendientes.
6. La configuración y la cola son del equipo y no viajan en un resguardo.

Talla: XL. Si una prueba existente falla por otra razón, se para (cláusula del plan).
