# Tasks

Ninguna prueba existente cambia (D10): `git diff --diff-filter=M --stat 1000578 -- tests/` tiene que salir vacío al terminar cada grupo, con `1000578` la punta de `feature/ofrecer-solo-opciones-disponibles-dw` sobre la que se apila esta rama. Cada prueba nueva se ve fallar antes de implementar, con la salida pegada. Si una prueba existente falla, se para y se le pregunta a David (punto 5 del gate). Las pruebas de archivos usan una carpeta temporal real por caso; para que un archivo no admita escritura se le pone el atributo de sólo lectura, y para que una carpeta no la admita se crea un archivo con su nombre.

## 1. El escritor de archivos

- [x] 1.1 Agregar `Infrastructure.Sqlite.Tests/BitacorasExternasTests.cs` con los escenarios de «Dos archivos mensuales por puesto», «Cada línea se entiende sola», «Cada registro va a su bitácora» y «La atención no espera al archivo» (el de orden; el del archivo bloqueado va en 1.5), llamando a la pasada del escritor sin temporizador y con un reloj fijo. Crear el escritor con `throw new NotImplementedException()` y ver fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~BitacorasExternas"`; pegar la salida
  `BitacorasExternasTests.cs` con ocho casos, cada uno con su carpeta de datos temporal real (la base adentro), el fixture sintético, el reloj fijo en el 06/11/2026 a las 12:00 locales y `Entregar()` llamado sin temporizador: nombres del mes (sin BOM, una línea JSON por renglón, nada fuera de `bitacoras\<KioskId>\`), hecho del 30/11 a las 23:59 locales entregado el 01/12, los 18 campos del contrato en su orden con el `AuditId` y la `Sequence` de la base, ningún código (uno reconocible rechazado y después autorizado), identidad no resuelta, impresión en las dos, tipo sin destino conocido y orden sin huecos. `EscritorBitacoras` con sus firmas y `throw new NotImplementedException()`. Rojo, 06/11 09:26:
  ```
  dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~BitacorasExternas"
  con errores bitacoras-externas · Impresión en las dos: … (38ms)
  Xunit.MicrosoftTestingPlatform.XunitException: System.NotImplementedException : The method or operation is not implemented.
    a las Intelektron.INComedor.Infrastructure.Sqlite.EscritorBitacoras..ctor(…) en …\EscritorBitacoras.cs:20
  (los ocho con la misma excepción)    total: 8 · error: 8 · correcto: 0
  ```
- [x] 1.2 Implementar la severidad de `RegistroBitacora` (D12), el reparto (D3), la línea con `ExportedAtUtc` y `Recovered` (D5), la carpeta contenida en la de datos (D11) y la pasada con la marca leída del archivo (D2, D4). Verificar con el filtro de 1.1 en verde y `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completo en verde
  `RegistroBitacora.Insertar` suma `severidad` con `INFO` por defecto (`SeveridadBitacora`), y su constructor de un argumento usa `ArranqueDelProceso`, un `BootId` por proceso en lugar de uno por instancia, para que «otro arranque» de D5 signifique otro proceso. `EscritorBitacoras`: lee el `KioskId` del puesto, resuelve `bitacoras\<KioskId>` con `Path.GetFullPath` y comprueba que quede dentro de la carpeta de datos; reparto fijo de D3 como condición `IN`/`NOT IN` parametrizada; marca = mayor `Sequence` de la última línea completa de cada archivo del destino, leída desde el final de a 4 KiB; corte = la mayor secuencia al empezar; tandas de 500, agrupadas por `LogMonth`, en modo agregar con `FileShare.Read` y `Flush(flushToDisk: true)`; línea con `Utf8JsonWriter` en el orden del contrato, `Recovered` verdadero si el `BootId` es de otro arranque; un semáforo impide dos pasadas a la vez. `BaseComedor.AbrirConexionExistente` abre sin crear el archivo, para que el escritor nunca deje una base vacía donde alguien borró la suya.
  ```
  dotnet build src/Intelektron.INComedor.Infrastructure.Sqlite -warnaserror   Compilación correcta · 0 Advertencia(s) · 0 Errores
  Infrastructure.Sqlite.Tests --filter BitacorasExternas   correcto [+8/x0/?0]     total: 8 · error: 0
  Infrastructure.Sqlite.Tests completo                     correcto [+129/x0/?0]   total: 129 · error: 0   (121 de antes y 8 nuevas)
  ```
- [x] 1.3 Agregar a `BitacorasExternasTests` los escenarios de «Reanudar tras una caída sin duplicar» (caída después de escribir, línea parcial, base y archivo que no coinciden), armando cada archivo a mano antes de la pasada. Ver fallar con el filtro de 1.1; pegar la salida
  Tres casos con el archivo armado a mano (una línea con `AuditId`, `Sequence` y tipo por registro de la base): caída con las líneas 1 a 40 y 45 en la base; línea parcial con 10 completas más 25 bytes de la 11, 12 en la base, y además `LOG_LINE_REPAIRED` en la general con el archivo y los bytes; desajuste con la `Sequence` 12 de otro `AuditId`, 15 en la base y dos pasadas, que deja una sola `LOG_SEQUENCE_MISMATCH`. Rojo, 06/11 09:29, los dos que dependen de 1.4:
  ```
  dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~BitacorasExternas"
  con errores bitacoras-externas · Base y archivo no coinciden: … (152ms)
    Assert.Equal() Failure: Collections differ
  con errores bitacoras-externas · Línea parcial: … (121ms)
    System.IO.FileNotFoundException : Could not find file '…\INComedor-interacciones-202611.log.reparacion.log'.
  total: 11 · error: 2 · correcto: 9
  ```
  La de caída pasa desde el principio: la marca leída del archivo es de 1.2 (D2). Para no dejarla sin verla fallar, corrió contra un mutante con la marca en 0, y falló por las líneas repetidas; el mutante se revirtió en el acto:
  ```
  dotnet test … --filter "FullyQualifiedName~Caida_despues"   (marca forzada a 0)
  con errores bitacoras-externas · Caída después de escribir y antes de seguir: … (333ms)
    Assert.Equal() Failure: Collections differ    total: 1 · error: 1 · correcto: 0
  ```
- [x] 1.4 Implementar la reparación y la detección del desajuste (D6). Verificar con el filtro de 1.1 en verde
  Al leer la marca, cada archivo del destino que no termina en `\n` pasa los bytes desde el último salto a `<archivo>.reparacion.log` (agregados, con su salto y flush), se corta con `SetLength` y deja `LOG_LINE_REPAIRED` (`WARNING`, con `File`, `Bytes` y `RepairFile`). Después se compara el `AuditId` de la última línea completa con el registro de la base de esa `Sequence` y generación: si falta, no coincide o la línea no se puede leer, el destino queda bloqueado para este arranque y la general recibe una sola `LOG_SEQUENCE_MISMATCH` (`ERROR`, con `File`, `Sequence` y los dos `AuditId`). Los registros propios pasan por la misma cola: si una vuelta registró algo, la pasada vuelve a recorrer los destinos, hasta tres vueltas.
  ```
  dotnet build src/Intelektron.INComedor.Infrastructure.Sqlite -warnaserror   Compilación correcta · 0 Advertencia(s) · 0 Errores
  Infrastructure.Sqlite.Tests --filter BitacorasExternas   correcto    total: 11 · error: 0 · correcto: 11
  ```
- [x] 1.5 Agregar los escenarios de «Una falla de archivo no detiene la atención» y «Recuperar un archivo», más el del archivo bloqueado durante una confirmación de «La atención no espera al archivo» y una falla repetida tres pasadas que deja una sola incidencia. Ver fallar con el filtro de 1.1; pegar la salida
  Seis casos: archivo bloqueado durante una confirmación (interacciones vacía y de sólo lectura); falla la general (tres pasadas, una sola `LOG_DELIVERY_FAILED` con `Stream`, `OpenedAtUtc`, `Cause` y `FirstPendingAuditId`, `ERROR`, y el estado degradado con 3 pendientes desde las 12:00); falla repetida con la segunda y la tercera pasada en un arranque nuevo del escritor; fallan las dos (un archivo llamado `bitacoras`, dos personas con sus comprobantes); recuperación de la general (tres pendientes de las 12:01 entregados a las 12:05 con `Recovered`, `LOG_DELIVERY_RECOVERED` con apertura, cierre y `DeliveredCount` 3 en los dos archivos, los consumos iguales) y pendiente de octubre recuperado en noviembre, a continuación en el archivo de 202610. Rojo, 06/11 09:33:
  ```
  dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~BitacorasExternas"
  con errores … Falla repetida …                      System.UnauthorizedAccessException : Access to the path '…\INComedor-general-202611.log' is denied.
  con errores … Archivo bloqueado durante una confirmación …   System.UnauthorizedAccessException : … INComedor-interacciones-202611.log' is denied.
  con errores … Pendiente de un mes anterior …        System.UnauthorizedAccessException : … INComedor-interacciones-202610.log' is denied.
  con errores … Recuperación de la general …          System.UnauthorizedAccessException : … INComedor-general-202611.log' is denied.
  con errores … Fallan las dos …                      System.IO.IOException : Cannot create '…\bitacoras' because a file or directory with the same name already exists.
  con errores … Falla la general …                    System.UnauthorizedAccessException : … INComedor-general-202611.log' is denied.
  total: 17 · error: 6 · correcto: 11
  ```
- [x] 1.6 Implementar las incidencias agrupadas y la recuperación hasta el corte (D7). Verificar con el filtro de 1.1 en verde, `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completo en verde y `git diff --diff-filter=M --stat 1000578 -- tests/` vacío
  Una `IOException` o una `UnauthorizedAccessException` de un destino abre una incidencia sólo si no hay otra: `LOG_DELIVERY_FAILED` (`ERROR`, con `Stream`, `OpenedAtUtc`, `Cause` y `FirstPendingAuditId`), y la marca se vuelve a leer del archivo. La incidencia abierta se deduce al construir el escritor del último `LOG_DELIVERY_FAILED` o `LOG_DELIVERY_RECOVERED` de ese `Stream`, así que un arranque nuevo no abre otra. Con una incidencia abierta al empezar, lo entregado sale con `Recovered`; al llegar al corte se registra `LOG_DELIVERY_RECOVERED` (`WARNING`, con la apertura, el cierre, `DeliveredCount` y el primer pendiente), que la vuelta siguiente lleva a los dos archivos. El estado de cada destino dice al día, o degradado con sus pendientes y la hora de la incidencia o del desajuste. Corrección de paso: la tanda se escribe en tramos consecutivos de un mismo mes y en orden de `Sequence`, no agrupada por mes, para que una falla en el segundo archivo no deje una marca que saltee registros.
  ```
  dotnet build src/Intelektron.INComedor.Infrastructure.Sqlite -warnaserror   0 Advertencia(s) · 0 Errores
  Infrastructure.Sqlite.Tests --filter BitacorasExternas   total: 17 · error: 0 · correcto: 17
  Infrastructure.Sqlite.Tests completo                     total: 138 · error: 0 · correcto: 138   (121 de antes y 17 nuevas)
  git diff --diff-filter=M --stat 1000578 -- tests/        (vacío)
  ```

## 2. Lo que falta para reconstruir

- [ ] 2.1 Agregar `Infrastructure.Sqlite.Tests/ContextoReconstruibleTests.cs` con los escenarios de «Lo mostrado queda en el registro» (la autorización con sus cargos, el rechazo al confirmar y la oferta con la etiqueta y el importe de cada ofrecida) y uno de `SESSION_OPENED` al aceptar una identificación, con la sesión, el propósito, la persona y el método, sin el código. Ver fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~ContextoReconstruible"`; pegar la salida
- [ ] 2.2 Ampliar los contextos de `OPTIONS_OFFERED`, `CONSUMPTION_AUTHORIZED` y `CONSUMPTION_DENIED` sin cambiar los campos que ya tienen (D10), registrar `SESSION_OPENED` en la transacción de la identificación y crear la clase pública con la que la atención registra en su propia transacción corta (D8). Verificar con el filtro de 2.1 en verde, `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completo en verde y `git diff --diff-filter=M --stat 1000578 -- tests/` vacío

## 3. La atención, Configuración y el puesto

- [ ] 3.1 Agregar `Kiosk.Tests/BitacorasDelPuestoTests.cs`, con máquina, bUnit y la pasada del escritor sobre una carpeta temporal, para los escenarios de «Una atención se reconstruye con su archivo» (autorizada, cancelada, inactividad y cambio de página, leyendo sólo el archivo de interacciones), «El puesto y Configuración quedan en la general» (guardar el estilo, guardar sin poder escribir, el modo de la impresora y la versión desconocida) y «Configuración muestra el estado de las bitácoras» (al día y degradada). Ver fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~BitacorasDelPuesto"`; pegar la salida
- [ ] 3.2 Implementar en `MaquinaAtencion` los registros de D8, en `ServicioAtencion` el escritor (arranca al terminar `Preparar`, cada 2 s, y `Dispose` espera la pasada en curso y hace la última) con `KIOSK_STARTED` y `KIOSK_STOPPED`, y en la pantalla y en Configuración los registros de D9 y el estado de cada bitácora. Verificar con el filtro de 3.1 en verde, `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` completo en verde y `git diff --diff-filter=M --stat 1000578 -- tests/` vacío

## 4. De punta a punta

- [ ] 4.1 Agregar `E2E.Tests/BitacorasExternasE2ETests.cs` sin tocar los ayudantes existentes: el puesto compilado, una atención autorizada por CDP, el cierre ordenado del puesto y la lectura de los dos archivos de su carpeta (en interacciones, de `SESSION_OPENED` a `SESSION_CLOSED` con la misma sesión; en la general, `KIOSK_STARTED` y `KIOSK_STOPPED`; en ninguno, el código). Como el puesto ya escribe cuando esta prueba nace, verla fallar con una afirmación imposible (un `EventType` que no existe) y revertirla, y verificar con `dotnet test tests/Intelektron.INComedor.E2E.Tests` en verde; pegar las dos salidas
- [ ] 4.2 Medir «Ingresar» y elegir una opción con el cronómetro de la verificación de A (dos corridas de 60 vueltas, quince y nueve opciones, SQLite real en disco) y comparar con sus medianas de 8,4 a 10,0 ms. Pegar mediana y p95; si una mediana sube más de 3 ms, anotarlo como riesgo abierto para el gate de la verificación

## 5. Documentos

- [ ] 5.1 Cambiar en `AGENTS.md` la línea del primer corte: las bitácoras mensuales salen de la segunda ola por este change, y se nombra lo que sigue ahí (generación al restaurar, saltos del reloj, evento de emergencia, diagnóstico y conservación). Verificar con `git grep -n "bitácoras mensuales" -- AGENTS.md`, que muestre la línea nueva
- [ ] 5.2 Actualizar `readme.md` (dónde están las bitácoras y un paso del recorrido de verificación que abre el archivo de interacciones), `docs/modelo-datos.md` y `docs/arquitectura.md` (la marca de entrega es el archivo, D2, con el enlace a este change) y las dos filas de `docs/aplicacion-del-curso.md` que dicen que las bitácoras son de la segunda ola. Verificar con `git grep -nE "bitácoras y diagnóstico, que (son|quedan para la) segunda ola" -- docs/aplicacion-del-curso.md` sin resultados (hoy da dos, las líneas 117 y 164)
- [ ] 5.3 Sumar la evidencia del change a `docs/aplicacion-del-curso.md`, con el nombre del change y sin enlace propio a su carpeta (D5 de `mostrar-aplicacion-del-curso`), y su entrada en `prompts.md` si el apply deja un ajuste humano nuevo. Verificar con el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso` (`enlaces rotos: 0`, con su control negativo)

## 6. Integración

- [ ] 6.1 Correr `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror` y `dotnet test INComedor.sln` completo contra la instancia de `INCOMEDOR_SQLSERVER`, y pegar los totales por proyecto; verificar además `git diff --diff-filter=M --stat 1000578 -- tests/` vacío

## Workflow follow-up

- Verificar con `/openspec-verify-change` y archivar en la misma rama antes del PR.
- Redactar el borrador del PR en `C:\AI\INComedor\ai4devs\gestion\PR-escribir-bitacoras-externas-dw.md`, con la forma de `PR-ofrecer-solo-opciones-disponibles-dw.md`.
