# Tasks

Cambian sólo las ocho pruebas existentes del punto 4 del gate, y sólo en la versión de esquema y en las tablas (D2): al cerrar cada grupo, `git diff --diff-filter=M --stat 18cce75 -- tests/` muestra a lo sumo `AperturaBaseTests.cs`, `MigracionesTests.cs`, `MigracionLotesTests.cs`, `SemillaAtencionTests.cs`, `ServicioDemostracionTests.cs` y `BitacorasDelPuestoTests.cs`, con `18cce75` la punta de `feature/escribir-bitacoras-externas-dw` sobre la que se apila esta rama. Cada prueba nueva se ve fallar antes de implementar, con la salida pegada. Si falla otra prueba existente, se para y se le pregunta a David (punto 12 del gate). Las pruebas de SQLite usan un archivo temporal real; el reloj, un `TimeProvider` fijo en la zona `America/Argentina/Buenos_Aires`.

## 1. Versión de esquema 4

- [x] 1.1 Agregar `Infrastructure.Sqlite.Tests/MigracionFranjasTests.cs`: una base vacía queda en la versión 4 con las 25 tablas; una base de la versión 3 con un consumo autorizado pasa a la 4 y conserva el consumo y su evento; el `CHECK` rechaza una franja con inicio igual al fin y un origen que no es `LOCAL` ni `INWEB`. Ver fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~MigracionFranjas"`; pegar la salida
  Cuatro casos: base vacía con las 25 tablas, base armada con los recursos 0001 a 0003 y `user_version` 3 con un consumo autorizado y su evento `PENDING` (la foto de las dos filas, igual antes y después), franja de 12:00 a 12:00 y origen `CENTRAL` sobre el fixture sintético. Rojo, 06/11 20:13:
  ```
  dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~MigracionFranjas"
  con errores D2 · Base vacía: queda en la versión de esquema 4 con las 25 tablas, las dos nuevas incluidas (80ms)
    Assert.Equal() Failure: Values differ   Expected: 4   Actual: 3
  con errores D2 · D6 · Una franja con inicio igual al fin falla por CHECK (39ms)
    Assert.Equal() Failure: Values differ   Expected: 275   Actual: 1      (no such table)
  con errores D2 · D3 · El origen de una versión sólo admite LOCAL o INWEB (28ms)
    Assert.Equal() Failure: Values differ   Expected: 275   Actual: 1
  con errores D2 · AT51 · Base en la versión 3 con un consumo autorizado: pasa a la 4, … (94ms)
    Assert.Equal() Failure: Values differ   Expected: 4   Actual: 3
  total: 4 · error: 4 · correcto: 0
  ```
- [x] 1.2 Agregar `0004_franjas_y_origen.sql` y su entrada en `BaseComedor` (D2), y cambiar las ocho pruebas existentes del punto 4 sólo en el número de versión, la lista de tablas de `AperturaBaseTests` y las tablas que quita `SemillaAtencionTests`, con la aprobación del gate. Verificar con el filtro de 1.1 en verde, `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` y `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` completos en verde y `git diff --diff-filter=M --stat 18cce75 -- tests/` con sólo los seis archivos; pegar las tres salidas
  `0004_franjas_y_origen.sql`: `tbComedorFranjasOpcionesMenu` (clave `comvcCodigo` y `comfraCodigo`, FK a la opción de su versión, horas `HH:mm` de 00:00 a 23:59 por `GLOB` y `CHECK` de inicio distinto del fin, índice por opción) y `tbComedorOrigenesVersiones` (FK a la versión, `CHECK IN ('LOCAL', 'INWEB')`), embebida y cuarta en `BaseComedor.Migraciones`. Las ocho pruebas del punto 4 cambian sólo el número que afirman (y el de su `DisplayName`): `AperturaBaseTests` suma las dos tablas a su lista (25) y `SemillaAtencionTests` las quita al simular la versión 1. Los nombres de los métodos no cambian, para que sigan siendo los que lista el gate. 06/11 20:15 a 20:17:
  ```
  Infrastructure.Sqlite.Tests --filter MigracionFranjas   total: 4 · error: 0 · correcto: 4
  Infrastructure.Sqlite.Tests completo                    total: 147 · error: 0 · correcto: 147   (143 y 4 nuevas)
  Kiosk.Tests completo (INCOMEDOR_SQLSERVER=INMO-DW-AR)   total: 174 · error: 0 · correcto: 174
  git diff --diff-filter=M --stat 18cce75 -- tests/
   .../AperturaBaseTests.cs                                          | 6 ++++--
   .../MigracionLotesTests.cs                                        | 8 ++++----
   .../MigracionesTests.cs                                           | 4 ++--
   .../Intelektron.INComedor.Kiosk.Tests/BitacorasDelPuestoTests.cs  | 4 ++--
   tests/Intelektron.INComedor.Kiosk.Tests/SemillaAtencionTests.cs   | 6 +++---
   .../ServicioDemostracionTests.cs                                  | 4 ++--
   6 files changed, 17 insertions(+), 15 deletions(-)
  ```

## 2. La franja en la evaluación única

- [x] 2.1 Agregar `Domain.Tests/FranjasHorariasTests.cs`: a las 10:59, 11:00 y 14:00 con la franja de 11:00 a 14:00; a la 01:30 y a las 02:00 con la de 22:00 a 02:00; una opción con dos franjas; sin franjas, a las 03:00; y el orden de la evaluación (regla cero antes que la franja, la franja antes que el cupo, y las reglas elegidas que siguen llegando con la franja fuera). Ver fallar con `dotnet test tests/Intelektron.INComedor.Domain.Tests --filter "FullyQualifiedName~FranjasHorarias"`; pegar la salida
  Dieciséis casos (cuatro teorías y cuatro hechos): 10:59, 11:00, 13:59 y 14:00 con la de 11:00 a 14:00; 01:30, 22:00, 02:00 y 21:59 con la de 22:00 a 02:00; 12:00, 23:00 y 16:00 con las dos; sin franjas a las 03:00; regla cero antes que la franja; franja antes que el cupo agotado; las reglas elegidas que siguen llegando; y la franja de inicio igual al fin que no se construye. `FranjaHoraria` y la sobrecarga creadas con `throw new NotImplementedException()`, y `MotivoDenegacion.OutsideServiceHours` para compilar. Rojo, 06/11 20:16:
  ```
  dotnet test tests/Intelektron.INComedor.Domain.Tests --filter "FullyQualifiedName~FranjasHorarias"
  con errores D6 · Franja de 11:00 a 14:00: … (hora: "11:00", autorizada: True) (0ms)
    System.TypeInitializationException : The type initializer for '…FranjasHorariasTests' threw an exception.
    ---- System.NotImplementedException : The method or operation is not implemented.
  con errores D6 · Una franja con inicio igual al fin no se puede construir (2ms)
    ---- System.NotImplementedException : The method or operation is not implemented.
  (los dieciséis con la misma excepción)    total: 16 · error: 16 · correcto: 0
  ```
- [x] 2.2 Implementar la franja, `MotivoDenegacion.OutsideServiceHours` y la sobrecarga de `EvaluadorDisponibilidad.Evaluar` (D4, D6), sin cambiar la firma actual. Verificar con el filtro de 2.1 en verde, `dotnet test tests/Intelektron.INComedor.Domain.Tests` completo en verde y `git diff --stat 18cce75 -- tests/Intelektron.INComedor.Domain.Tests/EvaluadorDisponibilidadTests.cs` vacío; pegar las salidas
  `FranjaHoraria` (record con `Inicio` y `Fin` en `TimeOnly`; rechaza inicio igual al fin con `ArgumentException`; `Contiene` con inicio incluido y fin excluido, y el cruce de medianoche cuando el inicio es mayor). La sobrecarga de `Evaluar` recibe las franjas y la hora local: habilitación; si no hay motivo y la opción tiene franjas sin ninguna que contenga la hora, `OutsideServiceHours` sin cargas y con las reglas elegidas; si no, el cupo. La firma actual delega en la sobrecarga sin franjas («todo el día»).
  ```
  Domain.Tests --filter FranjasHorarias   total: 16 · error: 0 · correcto: 16
  Domain.Tests completo                   total: 91 · error: 0 · correcto: 91   (75 y 16 nuevas)
  git diff --stat 18cce75 -- tests/Intelektron.INComedor.Domain.Tests/EvaluadorDisponibilidadTests.cs   (vacío, exit 0)
  ```
- [x] 2.3 Agregar `Infrastructure.Sqlite.Tests/FranjaEnLaEvaluacionTests.cs` con los escenarios de «La franja horaria de la opción entra a la misma evaluación» (antes, dentro y en el fin; la franja que cierra entre la oferta y la confirmación; la regla cero antes que la franja; el cupo agotado a la vista fuera de horario) y «Fuera de horario, la oferta y la confirmación también coinciden», afirmando además la `Cause` en `OPTIONS_OFFERED` y en `CONSUMPTION_DENIED`, `ReasonCause` en el resultado y `SERVICE_NOT_ENABLED` en el evento de salida. Las franjas se insertan por SQL en la versión del fixture. Ver fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~FranjaEnLaEvaluacion"`; pegar la salida
  Siete casos sobre `ConfirmacionTestBase`, con el reloj llevado a la hora local del 19/10 (UTC-3): 10:59, 11:00 y 14:00; ofrecida a las 13:59 y confirmada a las 14:00; regla cero a las 16:00 (sin causa en el resultado, en la retenida ni en `CONSUMPTION_DENIED`); almuerzo consumido a las 12:00 y oferta a las 16:00 con las nueve de ALMUERZO fuera de horario y ALMUERZO 1 de 1; y la oferta y la confirmación en el mismo instante. Los fuera de horario afirman `Cause` en `Withheld` y en `CONSUMPTION_DENIED`, `ReasonCause` y `SERVICE_NOT_ENABLED` en el evento de salida, sin la causa. `OpcionRetenida.Causa` y `ResultadoConfirmacion.ReasonCause` se suman, todavía siempre nulas, para compilar. Rojo, 06/11 20:18:
  ```
  dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~FranjaEnLaEvaluacion"
  con errores … (hora: "10:59", autorizada: False)    Assert.Single() Failure: The collection did not contain any matching items
  con errores … (hora: "14:00", autorizada: False)    Assert.Single() Failure: The collection did not contain any matching items
  con errores … La franja cierra entre la oferta y la confirmación …   Assert.Equal() Failure   Expected: Denegada   Actual: Autorizada
  con errores … Fuera de horario, la oferta y la confirmación coinciden …   Assert.Single() Failure: … did not contain any matching items
  con errores … El cupo agotado sigue a la vista fuera de horario …   Assert.All() Failure: 9 out of 9 items … Expected: "OUTSIDE_SERVICE_HOURS"   Actual: null
  con errores … Una regla cero decide antes que la franja …   System.Collections.Generic.KeyNotFoundException : The given key 'Cause' was not present in the dictionary.
  total: 7 · error: 6 · correcto: 1   (el de las 11:00 ya autorizaba sin franjas)
  ```
- [x] 2.4 Leer las franjas en `LecturaDecision.LeerOpcion`, calcular la hora local con la zona del puesto en la oferta y en la confirmación, y traducir el motivo a `SERVICE_NOT_ENABLED` con la causa `OUTSIDE_SERVICE_HOURS` (D5). Verificar con el filtro de 2.3 en verde, `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completo en verde y el `git diff` del párrafo inicial; pegar las salidas
  `OpcionMenu` suma `Franjas`, que `LeerOpcion` lee de `tbComedorFranjasOpcionesMenu` en la versión activa (`LecturaDecision.LeerFranjas`); `HoraLocal` convierte el instante a la zona del puesto, la de la fecha de negocio. La oferta y la confirmación llaman a la sobrecarga con las franjas y esa hora. `CodigoDeMotivo` traduce `OutsideServiceHours` a `SERVICE_NOT_ENABLED` y `CausaDeMotivo` da `OUTSIDE_SERVICE_HOURS`, que van a `OpcionRetenida.Causa`, a `Cause` de cada retenida en `OPTIONS_OFFERED`, a `ResultadoConfirmacion.ReasonCause` (y así a su `ResultPayload` y a su replay) y a `Cause` de `CONSUMPTION_DENIED`; nula en los demás motivos. El evento de salida no cambia.
  ```
  Infrastructure.Sqlite.Tests --filter FranjaEnLaEvaluacion   total: 7 · error: 0 · correcto: 7
  Infrastructure.Sqlite.Tests completo                         total: 154 · error: 0 · correcto: 154   (147 y 7 nuevas)
  dotnet build INComedor.sln -warnaserror                      0 Advertencia(s) · 0 Errores
  git diff --diff-filter=M --stat 18cce75 -- tests/            los seis archivos de 1.2, sin cambios desde entonces
  ```

## 3. El servicio de administración

- [x] 3.1 Agregar `Infrastructure.Sqlite.Tests/AdministracionLocalTests.cs` con los escenarios de `administracion-local` que no necesitan pantalla, llamando al servicio: versión nueva que conserva la anterior; consumo que conserva su versión; guardado inválido; los tres de INWeb (sin pantalla, el de la versión activa afirma el estado de sólo lectura que devuelve el servicio); alta con su identificación por `IdentificacionPorCodigo`; código ya usado y revocado; datos fuera de forma; ceros iniciales; baja con consumos; baja con una sesión abierta; los cuatro de cupos; los cuatro de opciones (el de precio, con `UnidadConfirmacion` devolviendo `CatalogoCambiado`); los de franjas que no son de la evaluación; y los tres de la bitácora, con el escritor de B sobre una carpeta temporal y la búsqueda del código en los dos archivos. Crear `AdministracionLocal` con `throw new NotImplementedException()`. Ver fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~AdministracionLocal"`; pegar la salida
  Treinta casos sobre una carpeta de datos temporal con la base adentro, el fixture sintético (versión 1, código `000123`) y el reloj fijo el 06/11/2026 a las 12:00 locales. Los escenarios que el spec escribe sobre la base de la demostración (`0001`, «Almuerzo común» con 1.500,00, REFRIGERIO 2) se arman con su equivalente del fixture; el de «bajar por debajo de lo usado» sube primero REFRIGERIO a 2. Además de los pedidos: el par con reglas de vigencia o prioridad (se ve con el límite que decide y el aviso, y editarlo deja una sola regla), quitar un cupo con cambio de nombre, el guardado denegado en la bitácora y los números y el hash de D14. La búsqueda del código recorre cada valor de texto de las dos bitácoras salvo los UUID, que pueden contener esos dígitos, como en el E2E de B. Los modelos de la vista y de la edición van en `ModelosAdministracion.cs`. Rojo, 06/11 20:25:
  ```
  dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~AdministracionLocal"
  con errores administracion-local · Datos fuera de forma: el código 12a4, un nombre de 81 caracteres y … (57ms)
    Xunit.MicrosoftTestingPlatform.XunitException: System.NotImplementedException : The method or operation is not implemented.
      a las Intelektron.INComedor.Infrastructure.Sqlite.AdministracionLocal..ctor(BaseComedor baseComedor, TimeProvider reloj) en …\AdministracionLocal.cs:11
  (los treinta con la misma excepción)    total: 30 · error: 30 · correcto: 0
  ```
- [x] 3.2 Implementar `AdministracionLocal`: lectura de la vista con su origen, guardado de personas y de opciones con la copia de la versión (D1), el bloqueo por origen (D3), la baja (D8), los cupos (D9), la validación (D11), los registros (D13) y los números y el hash de la versión (D14). Verificar con el filtro de 3.1 en verde, `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completo en verde y el `git diff` del párrafo inicial; pegar las salidas
  `Leer` arma la vista de la versión activa en una transacción de lectura: origen (sin fila, `LOCAL`), servicios, categorías, opciones con cargas y franjas en el orden del catálogo, y personas con un cupo por servicio y categoría (el límite de la regla que hoy decide: la cero prevalece y si no la de mayor prioridad, entre las vigentes o, si no hay, todas) y las opciones cuyas cargas tienen todas un cupo mayor que cero. `GuardarPersonas` y `GuardarOpciones` abren un `BEGIN IMMEDIATE`; con origen `INWEB` registran `DENIED` y no escriben nada más; validan (nombres sin controles ni separadores de línea y con su largo, código de 1 a 20 dígitos ASCII y no usado en ninguna versión ni en la misma edición, límites de 0 a 99, importe de 0 a 999.999.999 unidades menores, cargas de 1 a 9 unidades sin repetir categoría, franjas `HH:mm` distintas) y, con algún campo, registran `REJECTED` con `INVALID_FIELD` o `DUPLICATE_CODE` y los campos sin valores; si no, crean la versión con el número siguiente y esquema 4, copian las diez tablas con listas de columnas explícitas, aplican la edición, suben padrón, políticas o catálogo según lo que cambió, guardan el SHA-256 del contenido en JSON canónico (`JsonCanonico`), pasan la anterior a `SUPERSEDED`, activan la nueva con origen `LOCAL` y dejan un `CONFIGURATION_CHANGED` por persona u opción cambiada, todo antes del commit. Una baja deja a la persona inactiva y revoca su credencial en el instante del guardado; un par cuyo límite no cambió conserva sus reglas; una opción sólo sube su versión por nombre, servicio, importe o cargas.
  ```
  Infrastructure.Sqlite.Tests --filter AdministracionLocal   total: 30 · error: 0 · correcto: 30
  Infrastructure.Sqlite.Tests completo                       total: 184 · error: 0 · correcto: 184   (154 y 30 nuevas)
  dotnet build INComedor.sln -warnaserror                    0 Advertencia(s) · 0 Errores
  git diff --diff-filter=M --stat 18cce75 -- tests/          los seis archivos de 1.2: 6 files changed, 17 insertions(+), 15 deletions(-)
  ```
- [x] 3.3 Medir el guardado con la semilla de quince y 200 personas sintéticas, en una base SQLite real en disco, 30 guardados de un cupo, con un cronómetro en el scratchpad de la sesión, fuera del repo. Pegar mediana y p95; si la mediana pasa de 500 ms, anotarlo como riesgo abierto para el gate de la verificación
  Una aplicación de consola en el scratchpad (`cronometro\Program.cs`), con referencia al proyecto de SQLite: siembra en una base nueva en `%TEMP%` los tres archivos SQL de la demostración, la parte común y el catálogo de quince, y 200 personas más con su código y sus dos reglas (209 personas, 15 opciones); un guardado de calentamiento aparte y 30 guardados del cupo de ALMUERZO de `0001`, alternando 1 y 2 para que cada uno cambie algo; el cronómetro mide sólo `GuardarPersonas`, no la lectura del panel. Release, dos corridas, 06/11 20:30:
  ```
  corrida 1   mediana 32,8 ms · p95 36,2 ms · mín 21,8 · máx 44,2 · primero (JIT) 134,8 ms · 32 versiones · 8.204 KiB
  corrida 2   mediana 30,8 ms · p95 35,8 ms · mín 21,7 · máx 44,6 · primero (JIT) 70,3 ms  · 32 versiones · 8.224 KiB
  ```
  La mediana queda muy debajo de 500 ms. **Para el gate de la verificación**, no por tiempo sino por tamaño: con 209 personas, cada versión agrega unos 250 KiB a la base (32 versiones, 8 MiB). Purgar versiones viejas es de la conservación que B dejó afuera (riesgo «Copiar la versión entera» del design).

## 4. Los paneles

- [x] 4.1 Agregar `Kiosk.Tests/PanelPersonasTests.cs` y `Kiosk.Tests/PanelOpcionesTests.cs`, con bUnit y la base de la demostración en una carpeta temporal, para los escenarios con pantalla: la persona del código 0001 con sus cupos y sus nueve opciones; ningún código en el panel; persona sin cupos; la versión activa de INWeb, sin «Guardar» y con el aviso; «Cancelar» y Escape sin versión nueva; el campo inválido señalado; la franja vista como «11:00 a 14:00»; y, en `MaquinaAtencion`, «Fuera de horario» sin opciones y el mensaje del rechazo por horario al confirmar. Ver fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~PanelPersonas|FullyQualifiedName~PanelOpciones|FullyQualifiedName~FueraDeHorario"`; pegar la salida
  Dieciocho casos sobre un `ServicioAtencion` real con el catálogo de nueve: en `PanelPersonasTests`, 0001 con ALMUERZO 1, REFRIGERIO 2 y nueve opciones; ningún código en el texto, los valores ni los atributos del panel; «Persona sintética 02» sin cupos; INWeb sin controles ni «Guardar» y con el aviso; «Cancelar» y Escape; el nombre vacío señalado; el campo de código (contraseña, numérico, sin autocompletado, `[0-9]*`) que no vuelve a dibujar lo tipeado; y el alta de 0105 desde el panel hasta su atención. En `PanelOpcionesTests`, los parámetros de cada opción, la franja vista como «11:00 a 14:00» con la versión de menú intacta, «Cancelar» y Escape, la franja vacía señalada, INWeb sólo para leer y el estilo que sigue editable con INWeb. En `FueraDeHorarioTests`, la máquina y la pantalla a las 16:00 con todas las opciones de 11:00 a 14:00, y el rechazo por horario al confirmar a las 14:00. Para que no aparezcan códigos dentro de identificadores, el panel no dibuja UUID: las filas y los `select` usan posiciones. Rojo, 06/11 20:34:
  ```
  dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~PanelPersonas|FullyQualifiedName~PanelOpciones|FullyQualifiedName~FueraDeHorario"
  con errores … La persona del código 0001 …          Bunit.ElementNotFoundException : No elements were found that matches the selector '#abrir-personas'
  con errores … Las opciones con todos sus parámetros … Bunit.ElementNotFoundException : … '#abrir-opciones'
  (los catorce de los paneles, igual)
  con errores … Fuera de horario: con todas las opciones de 11:00 a 14:00 …   Assert.Equal() Failure: Strings differ
  con errores … La franja cierra entre la oferta y la confirmación …          Assert.Equal() Failure: Strings differ
  total: 18 · error: 17 · correcto: 1
  ```
  El que pasa desde el principio es «Con INWeb, el estilo visual sigue editable»: el diálogo ya lo era, y la prueba cuida que los paneles nuevos no lo cambien.
- [x] 4.2 Implementar los dos botones del diálogo y los dos paneles (D10, D12), con controles de 56 px, el campo de código enmascarado de sólo dígitos, los mensajes nuevos de `MensajesAtencion` y el motivo sin opciones por la causa en `MaquinaAtencion`. Verificar con el filtro de 4.1 en verde, `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` completo en verde y el `git diff` del párrafo inicial; pegar las salidas
  `ServicioAtencion.Administracion` se arma en `Preparar`. `PanelConfiguracion` suma «Personas habilitadas» y «Opciones del comedor», que abren `PanelPersonas` o `PanelOpciones` en lugar del contenido del diálogo; Escape cierra el panel abierto como su «Cancelar» y, sin panel, cancela el diálogo; al guardar, el diálogo dice «Se guardó la versión N de la configuración». Los paneles leen la vista del servicio, arman la edición entera y señalan con `aria-invalid` los campos que devuelve el guardado («Revisá los campos marcados», o «Ese código ya existe. Usá otro» sin decir de quién); con INWeb, sólo texto, el aviso y «Cancelar». Un límite, unas unidades o un importe que no son número van como -1, para que el servicio los rechace y los registre. `MaquinaAtencion` muestra «No hay opciones disponibles en este horario» si alguna retenida tiene la causa de horario, y el rechazo al confirmar elige el mensaje por `ReasonCause` («Esta opción no está disponible en este horario»). Los estilos nuevos de `puesto.css` ensanchan el diálogo a 960 px, desplazan la lista en vertical y dan 56 px a cada control.
  ```
  Kiosk.Tests --filter (los tres)                        total: 18 · error: 0 · correcto: 18
  Kiosk.Tests completo (INCOMEDOR_SQLSERVER=INMO-DW-AR)  total: 192 · error: 0 · correcto: 192   (174 y 18 nuevas)
  dotnet build INComedor.sln -warnaserror                0 Advertencia(s) · 0 Errores
  git diff --diff-filter=M --stat 18cce75 -- tests/      los seis archivos de 1.2: 6 files changed, 17 insertions(+), 15 deletions(-)
  ```
- [x] 4.3 Agregar `E2E.Tests/AdministracionE2ETests.cs` sin tocar los ayudantes existentes: con el puesto compilado y por CDP, abrir Configuración y «Personas habilitadas», agregar «Persona sintética 05» con `0105` y ALMUERZO límite 1, guardar, identificarse con `0105` y ver sólo las opciones de ALMUERZO; después del cierre ordenado, la general tiene el `CONFIGURATION_CHANGED` de la persona y ninguna de las dos bitácoras contiene `0105`. Como la conducta ya existe cuando nace, verla fallar con una afirmación imposible y revertirla. Sacar las capturas 13 (personas) y 14 (opciones con una franja) a 1024 × 768 y verlas una por una. Verificar con `dotnet test tests/Intelektron.INComedor.E2E.Tests` en verde; pegar las dos salidas y lo visto en cada captura
  La prueba usa `EscenarioE2E` tal cual (catálogo de quince) y fija el área en 1024 × 768 con `Emulation.setDeviceMetricsOverride`. Agrega a 0105 desde el panel, lo ve en la lista (10 personas), se identifica y recibe las nueve de ALMUERZO, ninguna de REFRIGERIO; después carga a «Almuerzo común» la franja de 11:00 a 14:00 (versión 3); cierra en orden y lee las dos bitácoras: un `CONFIGURATION_CHANGED` de persona con su nombre, `APPLIED` y `CredentialChanged`, y ningún valor de texto con `0105` fuera de los UUID. Las capturas salen en JPEG a escala CSS, calidad 85, en la carpeta de resultados, y se copian a `docs/capturas/`. **Dos hallazgos del E2E, que bUnit no veía.** (1) La primera corrida, todavía con la afirmación imposible, falló antes de llegar a ella: «No pudimos identificarte». La base tenía `0105` bien guardado; el test tipeaba el código mientras el diálogo, que vuelve inerte la atención, todavía se estaba cerrando. Se corrigió en la prueba, que ahora espera a que el diálogo se haya ido. (2) Después, el guardado de la franja salió rechazado: el `input type="time"` del WebView2 entrega «11:00:00». Primero se agregó una prueba bUnit (`La_hora_con_segundos_se_lleva_a_horas_y_minutos`), que falló (`Assert.Empty() Failure: Collection was not empty`), y después `PanelOpciones` lleva una hora con segundos en cero a `HH:mm`. Rojo (06/11 20:42, ya con las dos correcciones) y verde:
  ```
  dotnet test tests/Intelektron.INComedor.E2E.Tests --filter "FullyQualifiedName~AdministracionE2E"   (con Assert.Equal("RESULTADO_QUE_NO_EXISTE", Result))
  con errores administracion-local · De punta a punta: agregar «Persona sintética 05» con 0105 … (3s 882ms)
    Assert.Equal() Failure: Strings differ   Expected: "RESULTADO_QUE_NO_EXISTE"   Actual: "APPLIED"
  total: 1 · error: 1 · correcto: 0

  dotnet test tests/Intelektron.INComedor.E2E.Tests   (afirmación revertida, 06/11 20:46 a 20:48)
  total: 12 · error: 0 · correcto: 12 · omitido: 0 · duración: 1m 57s 932ms   (11 de antes y 1 nueva)
  Kiosk.Tests completo   total: 193 · error: 0 · correcto: 193   (192 y la de la hora con segundos)
  dotnet build INComedor.sln -warnaserror   0 Advertencia(s) · 0 Errores
  git diff --diff-filter=M --stat 18cce75 -- tests/   los seis archivos de 1.2
  ```
  Capturas, miradas con `Read`. **13** `docs/capturas/13-personas-habilitadas.jpg` (1024 × 768, 91.830 bytes): el diálogo «Configuración» con «Personas habilitadas»; la lista desplazada hasta «Persona sintética 01», «Activa», ALMUERZO 1 y REFRIGERIO 2 con «Quitar cupo», «Elegí un cupo», «Agregar cupo» y «Quitar», y la línea «Habilita:» con las quince opciones; debajo asoma «Persona sintética 02» con «Sin cupos»; la barra de desplazamiento a la derecha y «Agregar persona», «Cancelar» y «Guardar» dentro de los 768. Ningún código. **14** `docs/capturas/14-opciones-con-franja.jpg` (1024 × 768, 84.217 bytes): «Opciones del comedor» con «Almuerzo común», «Versión 1», servicio ALMUERZO, importe 1500,00, la carga ALMUERZO × 1 y la franja 11:00 y 14:00 con el texto «11:00 a 14:00» y «Quitar franja»; debajo, «Almuerzo de demostración»; «Agregar opción», «Cancelar» y «Guardar» a la vista. Nada cortado salvo lo que la lista desplaza. Para mirar en el monitor: la fila de la carga no tiene rótulo propio (se lee por el `select` de la categoría y el número).

## 5. Documentos

- [ ] 5.1 Cambiar en `AGENTS.md` la línea de la segunda ola: la administración de personas, cupos y opciones sale de ahí por este change mientras no haya INWeb, y siguen ahí los permisos de operador, la importación de INWeb y lo que deja el recorte. Corregir en el contexto de `openspec/config.yaml` la línea «Fuera del corte», que todavía nombra las bitácoras (B) y la administración. Verificar con `git grep -n "administrar-personas-y-opciones" -- AGENTS.md` y `git grep -n "bitácoras" -- openspec/config.yaml`; pegar las salidas
- [ ] 5.2 Actualizar `readme.md` (cómo se editan personas y opciones, las franjas y el bloqueo con INWeb, y un paso del recorrido de verificación), `docs/administracion-y-bitacoras.md` (después de las líneas 34 a 38, la precisión de la decisión de David del 04/11 para el período sin INWeb) y `docs/modelo-datos.md` (las dos tablas, con el enlace a este change que el archivado tiene que actualizar). Verificar con `git grep -n "Puesto hasta que haya INWeb" -- docs/administracion-y-bitacoras.md` y `git grep -n "tbComedorFranjasOpcionesMenu" -- docs/modelo-datos.md`; pegar las salidas
- [ ] 5.3 Sumar la evidencia del change a `docs/aplicacion-del-curso.md`, con el nombre del change y sin enlace propio a su carpeta (D5 de `mostrar-aplicacion-del-curso`), y su entrada en `prompts.md` si el apply deja un ajuste humano nuevo. Verificar con el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso` (`enlaces rotos: 0`, con su control negativo); pegar la salida

## 6. Integración

- [ ] 6.1 Correr `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror` y `dotnet test INComedor.sln` completo contra la instancia de `INCOMEDOR_SQLSERVER`, y pegar los totales por proyecto; verificar además el `git diff` del párrafo inicial

## Workflow follow-up

- Probar en el monitor táctil de David con el acceso directo «Probar puesto INComedor»: su base de `C:\INComedor\prueba-monitor` pasa a la versión de esquema 4 al abrir, y no vuelve a abrir con un puesto anterior.
- Verificar con `/openspec-verify-change` y archivar en la misma rama antes del PR.
- Redactar el borrador del PR en `C:\AI\INComedor\ai4devs\gestion\PR-administrar-personas-y-opciones-dw.md`, con la forma de `PR-escribir-bitacoras-externas-dw.md`.
