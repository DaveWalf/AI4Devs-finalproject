## 1. La base: la persona de un rechazo y el resumen del día

- [x] 1.1 `ResultadoIdentificacionCodigo` devuelve la persona y su nombre visible cuando la credencial existe pero no está
  vigente, sin abrir la sesión ni cambiar el registro (D9); un código desconocido sigue sin persona. Pruebas nuevas vistas
  fallar primero. Verifica: `dotnet test --project tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` en verde, sin
  cambiar `IdentificacionPorCodigoTests` ni `IdentificacionPorCredencialTests`.
- [x] 1.2 `ResumenDelDia`: consumos autorizados, rechazos (identificación rechazada y consumo denegado) y eventos de
  advertencia o error de la bitácora general entre dos instantes (D5). Pruebas nuevas vistas fallar primero. Verifica: lo
  mismo que 1.1. Hecho: `AvisosDesdeLaBaseTests` (3), dos vistas fallar contra el esqueleto (la persona nula y
  `NotImplementedException`); Sqlite 296 de 296, sin cambiar ninguna prueba existente.

## 2. Los correos en el puesto

- [x] 2.1 `ConfiguracionCorreo` con su validación y `AlmacenConfiguracionCorreo` (`correo.json`), validado al leer y al
  guardar, sin ninguna clave (D1); `MensajeCorreo` y el asunto «Cliente · Puesto N Nombre · Situación» (D2). Pruebas
  nuevas vistas fallar primero.
- [x] 2.2 La cola (`correo-pendiente\`, un archivo por correo, tope de 500), `CorreoSimulado` (`.eml` en
  `correo-simulado\`, modo «Caído») y el envío de la cola en orden, con `NOTIFICATION_QUEUED`, `NOTIFICATION_SENT` y
  `NOTIFICATION_FAILED` (D3 y D8). Pruebas nuevas vistas fallar primero.
- [x] 2.3 `AvisosCorreo`: los destinatarios por situación, la ventana de 15 minutos y la bitácora que avisa al
  degradarse (D4); la señal de vida con `correo-estado.json` (D5); el reloj de cada minuto, que no cierra el puesto ante
  una falla (D6). Pruebas nuevas vistas fallar primero. Verifica: `dotnet test --project
  tests/Intelektron.INComedor.Kiosk.Tests` en verde.

## 3. Los avisos desde la atención

- [x] 3.1 La máquina de atención avisa una persona sin consumo (rechazo con persona, consumo denegado y sin opciones) y
  un comprobante sin imprimir; la exportación automática avisa su falla; el servicio avisa una bitácora que se degrada
  (D4). Pruebas nuevas vistas fallar primero. Verifica: Kiosk en verde. Hecho (2.1 a 3.1): `AvisosCorreoTests` (10), las diez
  vistas fallar contra un esqueleto con `NotImplementedException`; `ResultadoEnvio` suma el código de la impresora con un
  valor por defecto, sin cambiar a nadie que lo construye; Kiosk 553 de 553 y Sqlite 296, sin cambiar ninguna prueba
  existente. Al aplicar: la primera pasada después de la hora manda la señal del día anterior aunque sea la del primer
  arranque (D5), así que las pruebas de los otros avisos la ponen a las 23:59.

## 4. La pestaña Mails

- [x] 4.1 La pestaña después de Impresora, sólo con la clave del día: los campos, «Guardar» con `CONFIGURATION_CHANGED`
  de `MAIL`, «Enviar una prueba», el servidor simulado con `SIMULATED_MAIL` y los pendientes (D7). Cambian, por la
  decisión 13, las pruebas que fijan las nueve pestañas: `PestanasConfiguracionTests`, `AccesoOperadorTests` y
  `AccionesEnConsumosTests`; y `PuntoFinalTests` clasifica los mensajes nuevos. Pruebas nuevas vistas fallar primero.
  Verifica: Kiosk en verde. Hecho: `CorreoEnPantallaTests` (5), cuatro vistas fallar con la pestaña vacía (la del operador
  pasa también sin la sección); las tres existentes pasan de nueve a diez pestañas y `PuntoFinalTests` clasifica
  `PruebaCorreoEnviada` y `EstadoColaCorreo`; Kiosk 558 de 558.

## 5. La pantalla de verdad y los documentos

- [x] 5.1 E2E nueva a 1024 × 768: la fila de diez pestañas entra; la pestaña Mails medida contra Impresora; guardar los
  correos con el servidor caído, «Enviar una prueba», ver el pendiente, levantar el servidor y ver el `.eml`, con
  capturas miradas (D7). Cambian, por la decisión 13, `AccesoOperadorE2ETests`, `ConfiguracionPorPestanasE2ETests` y
  `ExportacionE2ETests`, que cuentan nueve. Verifica: la E2E en verde. Hecho: `CorreoE2ETests` en verde;
  las diez pestañas en una fila hasta x 950,2; los once campos desde x 273,5 con 686,5 × 56 px y letra de 18 px, como
  `#numero-puesto` de Impresora; los botones de 56 px; con el servidor caído, «Cola: 1 pendiente», y levantado, dos `.eml`
  de «Prueba»; tres capturas miradas. Las tres existentes, en verde con diez. Atención para el change 8: quedan ~25 px en la
  fila de pestañas.
- [x] 5.2 `readme.md` (los avisos, la señal de vida y el envío real como pendiente) y la fila que corresponda de
  `docs/aplicacion-del-curso.md`. Verifica: el chequeo de enlaces con su control negativo, `enlaces rotos: 0`. Hecho: 0, con el control en 2.
- [ ] 5.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror
  --no-incremental`, el puesto publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c
  Release` contra ese EXE, con los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
