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

- [x] 2.1 Agregar `Infrastructure.Sqlite.Tests/ContextoReconstruibleTests.cs` con los escenarios de «Lo mostrado queda en el registro» (la autorización con sus cargos, el rechazo al confirmar y la oferta con la etiqueta y el importe de cada ofrecida) y uno de `SESSION_OPENED` al aceptar una identificación, con la sesión, el propósito, la persona y el método, sin el código. Ver fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~ContextoReconstruible"`; pegar la salida
  Cinco casos: autorización con sus cargos (y el catálogo cambiado después, que no cambia lo registrado), rechazo al confirmar con `LOCAL_LIMIT_REACHED` sin cargos ni comprobante, oferta con la etiqueta y el precio de cada ofrecida, `SESSION_OPENED` sin el código, y uno más para la clase pública de 2.2 (`RegistroDelPuesto`, creada con `throw new NotImplementedException()`): tipo, severidad, componente y contexto con la secuencia siguiente. Rojo, 06/11 09:36:
  ```
  dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~ContextoReconstruible"
  con errores … La atención registra en su propia transacción corta …   System.NotImplementedException : The method or operation is not implemented.
  con errores … Autorización con sus cargos …        System.Collections.Generic.KeyNotFoundException : The given key 'MenuItemId' was not present in the dictionary.
  con errores … Oferta con lo mostrado …             System.Collections.Generic.KeyNotFoundException : The given key 'MenuLabel' was not present in the dictionary.
  con errores … Identificación aceptada …            System.ArgumentNullException : Value cannot be null. (Parameter 'json')   (no hay SESSION_OPENED)
  con errores … Rechazo al confirmar …               System.Collections.Generic.KeyNotFoundException : The given key 'MenuItemId' was not present in the dictionary.
  total: 5 · error: 5 · correcto: 0
  ```
- [x] 2.2 Ampliar los contextos de `OPTIONS_OFFERED`, `CONSUMPTION_AUTHORIZED` y `CONSUMPTION_DENIED` sin cambiar los campos que ya tienen (D10), registrar `SESSION_OPENED` en la transacción de la identificación y crear la clase pública con la que la atención registra en su propia transacción corta (D8). Verificar con el filtro de 2.1 en verde, `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completo en verde y `git diff --diff-filter=M --stat 1000578 -- tests/` vacío
  `OPTIONS_OFFERED` suma `MenuLabel` y `Price` (`Currency`, `Scale`, `EmployeeChargeMinor`) a cada ofrecida. `CONSUMPTION_AUTHORIZED` suma `MenuItemId`, `MenuVersion`, `MenuLabel`, `Price` y `Charges` (ámbito, categoría con su nombre, unidades y el límite de la regla aplicable, que `UnidadConfirmacion` ahora conserva de la evaluación). `CONSUMPTION_DENIED` suma la opción, su versión y su etiqueta. Ningún campo anterior cambia de nombre ni de valor. `IdentificacionPorCodigo.Identificar` escribe `SESSION_OPENED` (`IdentitySessionId`, `Purpose`, `PersonId`, `Method` `CODE`) en su transacción. `RegistroDelPuesto` registra con `RegistroBitacora` en su propia `BEGIN IMMEDIATE` corta.
  ```
  dotnet build src/Intelektron.INComedor.Infrastructure.Sqlite -warnaserror   0 Advertencia(s) · 0 Errores
  Infrastructure.Sqlite.Tests --filter ContextoReconstruible   total: 5 · error: 0 · correcto: 5
  Infrastructure.Sqlite.Tests completo                         total: 143 · error: 0 · correcto: 143   (138 y 5 nuevas)
  git diff --diff-filter=M --stat 1000578 -- tests/            (vacío)
  ```

## 3. La atención, Configuración y el puesto

- [x] 3.1 Agregar `Kiosk.Tests/BitacorasDelPuestoTests.cs`, con máquina, bUnit y la pasada del escritor sobre una carpeta temporal, para los escenarios de «Una atención se reconstruye con su archivo» (autorizada, cancelada, inactividad y cambio de página, leyendo sólo el archivo de interacciones), «El puesto y Configuración quedan en la general» (guardar el estilo, guardar sin poder escribir, el modo de la impresora y la versión desconocida) y «Configuración muestra el estado de las bitácoras» (al día y degradada). Ver fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~BitacorasDelPuesto"`; pegar la salida
  Once casos, cada uno con su carpeta de datos temporal (la base adentro, no la carpeta compartida de las demás pruebas) y la pasada llamada a mano: las cuatro atenciones, leyendo sólo el archivo de interacciones y filtrando por la sesión de la última `SESSION_OPENED`; guardar el estilo (76/4 → 81/4), guardar sin poder escribir (la carpeta del estilo es un archivo: `FAILED` y lo vigente 76/4), el modo de la impresora con «Cancelar», la versión desconocida, uno más de arranque y cierre (`KIOSK_STARTED` y `KIOSK_STOPPED` con sus cinco versiones), y Configuración al día y degradada. `ServicioAtencion` suma `Bitacoras`, todavía siempre null, y `LeerVersionAplicacion`, sólo para compilar. Rojo, 06/11 09:40:
  ```
  dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~BitacorasDelPuesto"
  con errores … Atención autorizada …          System.NullReferenceException : Object reference not set to an instance of an object.
  con errores … Atención cancelada …           System.NullReferenceException : …
  con errores … Inactividad …                  System.NullReferenceException : …
  con errores … Cambiar de página …            System.NullReferenceException : …
  con errores … Guardar el estilo …            System.NullReferenceException : …
  con errores … Guardar sin poder escribir …   System.NullReferenceException : …
  con errores … El modo de la impresora …      System.NullReferenceException : …
  con errores … Arranque y cierre …            System.IO.DirectoryNotFoundException : Could not find a part of the path '…\bitacoras'.
  con errores … Versión desconocida …          System.NullReferenceException : …
  con errores … Configuración al día …         System.NullReferenceException : …
  con errores … Configuración degradada …      System.NullReferenceException : …
  total: 11 · error: 11 · correcto: 0
  ```
- [x] 3.2 Implementar en `MaquinaAtencion` los registros de D8, en `ServicioAtencion` el escritor (arranca al terminar `Preparar`, cada 2 s, y `Dispose` espera la pasada en curso y hace la última) con `KIOSK_STARTED` y `KIOSK_STOPPED`, y en la pantalla y en Configuración los registros de D9 y el estado de cada bitácora. Verificar con el filtro de 3.1 en verde, `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` completo en verde y `git diff --diff-filter=M --stat 1000578 -- tests/` vacío
  `MaquinaAtencion`: `OPTION_SELECTED` (sesión, opción, versión, etiqueta y el cargo mostrado; tocar la misma opción no repite), `CONFIRMATION_REQUESTED` en el hilo de la confirmación y antes de llamarla, `SESSION_CANCELLED` con la opción elegida, `SESSION_TIMED_OUT` con el estado desde el que venció, y `SESSION_CLOSED` con el nombre de la constante de `MensajesAtencion` o el `ReasonCode`, la decisión y `ClosedBy` (`DONE`, `TIMEOUT` o `RESULT` para la sesión vencida y el no confirmado). Un error de SQLite al registrar deja el error de almacenamiento, como cualquier otra escritura. `CONSUMPTION_AUTHORIZED` y `CONSUMPTION_DENIED` suman además `IdentitySessionId`, para que la atención se lea entera «con la misma sesión». `ServicioAtencion`: al terminar `Preparar` arma `RegistroDelPuesto` para la máquina y para Configuración, registra `KIOSK_STARTED` (`RegistroDelPuesto.RegistrarPuesto`: aplicación, runtime, sistema operativo, `user_version` y versión de configuración; la que no se lee, nula) y crea el escritor sobre la carpeta de la base; `Dispose` espera la pasada en curso, registra `KIOSK_STOPPED` y hace la última. Configuración registra `CONFIGURATION_OPENED`, `CONFIGURATION_CANCELLED` y `CONFIGURATION_CHANGED` (estilo con antes, después, resultado y lo vigente; modo de la impresora con el anterior y el nuevo) con `ActorPersonId` nulo y `NO_AUTHENTICATION`, y muestra el estado de cada bitácora leído antes de registrar la apertura. `MainWindow` libera el servicio al cerrarse: hasta hoy nadie lo liberaba, porque el contenedor no libera una instancia registrada desde afuera.
  **Decisión de implementación, a la vista de David.** Las pasadas automáticas de D4 (al terminar `Preparar`, cada 2 s y la última al cerrar) se activan con `ServicioAtencion.BitacorasAutomaticas`, que `MainWindow` pone siempre en verdadero: el puesto y el E2E hacen exactamente D4. Sin la propiedad, el escritor existe y las pasadas las pide quien usa el servicio. El motivo: las pruebas existentes del Kiosk arman el servicio sobre `%TEMP%\incomedor-tests\<guid>.db`, no lo liberan y borran la base al terminar; un temporizador siempre encendido podía tener la base abierta en ese instante (`File.Delete` falla en Windows), con fallas intermitentes, y mezclaba en una sola carpeta las bitácoras de muchas bases. «Arranque y cierre» prueba el modo automático, sin pedir ninguna pasada; esa prueba nueva se ajustó a ese modo antes de su primer verde.
  ```
  dotnet build src/Intelektron.INComedor.Kiosk -warnaserror          0 Advertencia(s) · 0 Errores
  Kiosk.Tests --filter BitacorasDelPuesto                             total: 11 · error: 0 · correcto: 11
  Kiosk.Tests completo (INCOMEDOR_SQLSERVER=INMO-DW-AR)               total: 174 · error: 0 · correcto: 174   (163 y 11 nuevas)
  Infrastructure.Sqlite.Tests completo                                total: 143 · error: 0 · correcto: 143
  git diff --diff-filter=M --stat 1000578 -- tests/                   (vacío)
  ```

## 4. De punta a punta

- [x] 4.1 Agregar `E2E.Tests/BitacorasExternasE2ETests.cs` sin tocar los ayudantes existentes: el puesto compilado, una atención autorizada por CDP, el cierre ordenado del puesto y la lectura de los dos archivos de su carpeta (en interacciones, de `SESSION_OPENED` a `SESSION_CLOSED` con la misma sesión; en la general, `KIOSK_STARTED` y `KIOSK_STOPPED`; en ninguno, el código). Como el puesto ya escribe cuando esta prueba nace, verla fallar con una afirmación imposible (un `EventType` que no existe) y revertirla, y verificar con `dotnet test tests/Intelektron.INComedor.E2E.Tests` en verde; pegar las dos salidas
  La prueba usa `EscenarioE2E` tal cual: `0001`, «Almuerzo común», «Confirmar» y «Listo» por CDP; cierre ordenado con `Process.CloseMainWindow()` (como la X) y espera de la salida; las seis líneas de la sesión en orden; `KIOSK_STARTED` antes y `KIOSK_STOPPED` último en la general; el código buscado como valor de texto de cada línea, porque «0001» aparece dentro de los identificadores sintéticos de la semilla. **Hallazgo:** la primera corrida, con la afirmación imposible, falló antes de llegar a ella: el puesto no terminó en 30 s. El ciclo de bitácoras arrancaba en el hilo de la pantalla y `Dispose` lo esperaba en ese mismo hilo; los dos quedaban esperándose. Se corrigió en `ServicioAtencion`: el ciclo corre con `Task.Run` y `ConfigureAwait(false)`. Las pruebas del Kiosk no lo veían porque no tienen el contexto de la pantalla. Rojo (06/11 09:49, ya con la corrección) y verde:
  ```
  dotnet test tests/Intelektron.INComedor.E2E.Tests --filter "FullyQualifiedName~BitacorasExternasE2E"   (con Assert.Contains(general, EventType == "EVENTO_QUE_NO_EXISTE"))
  con errores bitacoras-externas · De punta a punta: … (5s 284ms)
    Assert.Contains() Failure: Filter not matched in collection
  total: 1 · error: 1 · correcto: 0

  dotnet test tests/Intelektron.INComedor.E2E.Tests   (afirmación revertida)
  total: 11 · error: 0 · correcto: 11 · omitido: 0 · duración: 1m 56s 392ms   (10 de antes y 1 nueva)
  ```
- [x] 4.2 Medir «Ingresar» y elegir una opción con el cronómetro de la verificación de A (dos corridas de 60 vueltas, quince y nueve opciones, SQLite real en disco) y comparar con sus medianas de 8,4 a 10,0 ms. Pegar mediana y p95; si una mediana sube más de 3 ms, anotarlo como riesgo abierto para el gate de la verificación
  El script de A no quedó guardado; se reescribió fuera del repo, en el scratchpad de la sesión, con su método: `MaquinaAtencion` sobre una base SQLite real en disco, el reloj del sistema, dentro del proceso y sin WebView2; dos corridas de 60 vueltas por caso, quince y nueve opciones, sin consumos y con el almuerzo consumido; por vuelta, «Ingresar» con `0001`, elegir una opción y «Cancelar» (sin medir); el primer toque de cada corrida va aparte (JIT: 7,6 a 52,5 ms en B). B corre con las bitácoras automáticas, como la ventana. Para separar causas, el mismo cronómetro corrió en la misma sesión contra el código de A (`1000578`, extraído con `git archive` al scratchpad) y contra B sin el escritor en segundo plano, en seis tandas alternadas (06/11 de 09:54 a 09:55, Release):
  ```
  «Ingresar», mediana por corrida (16 por variante)   rango          mediana de las 16   p95 por corrida
  A (1000578), hoy                                    8,6 a 13,3 ms  10,5 ms             9,9 a 17,4 ms
  B                                                   8,6 a 14,3 ms  11,8 ms             10,2 a 20,3 ms
  B sin escritor en segundo plano                     9,2 a 14,3 ms  12,5 ms             11,0 a 25,9 ms
  Elegir una opción (B; en A no escribía nada)        mediana 3,8 a 6,4 ms · p95 4,5 a 12,0 ms
  ```
  **Riesgo abierto, para el gate de la verificación.** Contra las medianas de A del 04/11 (8,4 a 10,0 ms), hay medianas de B que suben más de 3 ms (hasta 14,3 ms). La medición de la misma sesión le atribuye al change unos +1,3 ms en «Ingresar» (`SESSION_OPENED` en la transacción y el contexto más grande de `OPTIONS_OFFERED`) y de 4 a 6 ms en cada elección (`OPTION_SELECTED`, una transacción corta con su `fsync`). El escritor en segundo plano no lo encarece. El resto de la diferencia es del equipo de hoy: A medido ahora llega a 13,3 ms. Falta repetirlo en la All-in-One.

## 5. Documentos

- [ ] 5.1 Cambiar en `AGENTS.md` la línea del primer corte: las bitácoras mensuales salen de la segunda ola por este change, y se nombra lo que sigue ahí (generación al restaurar, saltos del reloj, evento de emergencia, diagnóstico y conservación). Verificar con `git grep -n "bitácoras mensuales" -- AGENTS.md`, que muestre la línea nueva
- [ ] 5.2 Actualizar `readme.md` (dónde están las bitácoras y un paso del recorrido de verificación que abre el archivo de interacciones), `docs/modelo-datos.md` y `docs/arquitectura.md` (la marca de entrega es el archivo, D2, con el enlace a este change) y las dos filas de `docs/aplicacion-del-curso.md` que dicen que las bitácoras son de la segunda ola. Verificar con `git grep -nE "bitácoras y diagnóstico, que (son|quedan para la) segunda ola" -- docs/aplicacion-del-curso.md` sin resultados (hoy da dos, las líneas 117 y 164)
- [ ] 5.3 Sumar la evidencia del change a `docs/aplicacion-del-curso.md`, con el nombre del change y sin enlace propio a su carpeta (D5 de `mostrar-aplicacion-del-curso`), y su entrada en `prompts.md` si el apply deja un ajuste humano nuevo. Verificar con el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso` (`enlaces rotos: 0`, con su control negativo)

## 6. Integración

- [ ] 6.1 Correr `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror` y `dotnet test INComedor.sln` completo contra la instancia de `INCOMEDOR_SQLSERVER`, y pegar los totales por proyecto; verificar además `git diff --diff-filter=M --stat 1000578 -- tests/` vacío

## Workflow follow-up

- Verificar con `/openspec-verify-change` y archivar en la misma rama antes del PR.
- Redactar el borrador del PR en `C:\AI\INComedor\ai4devs\gestion\PR-escribir-bitacoras-externas-dw.md`, con la forma de `PR-ofrecer-solo-opciones-disponibles-dw.md`.
