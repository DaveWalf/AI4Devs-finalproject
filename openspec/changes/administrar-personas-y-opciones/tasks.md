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

- [ ] 2.1 Agregar `Domain.Tests/FranjasHorariasTests.cs`: a las 10:59, 11:00 y 14:00 con la franja de 11:00 a 14:00; a la 01:30 y a las 02:00 con la de 22:00 a 02:00; una opción con dos franjas; sin franjas, a las 03:00; y el orden de la evaluación (regla cero antes que la franja, la franja antes que el cupo, y las reglas elegidas que siguen llegando con la franja fuera). Ver fallar con `dotnet test tests/Intelektron.INComedor.Domain.Tests --filter "FullyQualifiedName~FranjasHorarias"`; pegar la salida
- [ ] 2.2 Implementar la franja, `MotivoDenegacion.OutsideServiceHours` y la sobrecarga de `EvaluadorDisponibilidad.Evaluar` (D4, D6), sin cambiar la firma actual. Verificar con el filtro de 2.1 en verde, `dotnet test tests/Intelektron.INComedor.Domain.Tests` completo en verde y `git diff --stat 18cce75 -- tests/Intelektron.INComedor.Domain.Tests/EvaluadorDisponibilidadTests.cs` vacío; pegar las salidas
- [ ] 2.3 Agregar `Infrastructure.Sqlite.Tests/FranjaEnLaEvaluacionTests.cs` con los escenarios de «La franja horaria de la opción entra a la misma evaluación» (antes, dentro y en el fin; la franja que cierra entre la oferta y la confirmación; la regla cero antes que la franja; el cupo agotado a la vista fuera de horario) y «Fuera de horario, la oferta y la confirmación también coinciden», afirmando además la `Cause` en `OPTIONS_OFFERED` y en `CONSUMPTION_DENIED`, `ReasonCause` en el resultado y `SERVICE_NOT_ENABLED` en el evento de salida. Las franjas se insertan por SQL en la versión del fixture. Ver fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~FranjaEnLaEvaluacion"`; pegar la salida
- [ ] 2.4 Leer las franjas en `LecturaDecision.LeerOpcion`, calcular la hora local con la zona del puesto en la oferta y en la confirmación, y traducir el motivo a `SERVICE_NOT_ENABLED` con la causa `OUTSIDE_SERVICE_HOURS` (D5). Verificar con el filtro de 2.3 en verde, `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completo en verde y el `git diff` del párrafo inicial; pegar las salidas

## 3. El servicio de administración

- [ ] 3.1 Agregar `Infrastructure.Sqlite.Tests/AdministracionLocalTests.cs` con los escenarios de `administracion-local` que no necesitan pantalla, llamando al servicio: versión nueva que conserva la anterior; consumo que conserva su versión; guardado inválido; los tres de INWeb (sin pantalla, el de la versión activa afirma el estado de sólo lectura que devuelve el servicio); alta con su identificación por `IdentificacionPorCodigo`; código ya usado y revocado; datos fuera de forma; ceros iniciales; baja con consumos; baja con una sesión abierta; los cuatro de cupos; los cuatro de opciones (el de precio, con `UnidadConfirmacion` devolviendo `CatalogoCambiado`); los de franjas que no son de la evaluación; y los tres de la bitácora, con el escritor de B sobre una carpeta temporal y la búsqueda del código en los dos archivos. Crear `AdministracionLocal` con `throw new NotImplementedException()`. Ver fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~AdministracionLocal"`; pegar la salida
- [ ] 3.2 Implementar `AdministracionLocal`: lectura de la vista con su origen, guardado de personas y de opciones con la copia de la versión (D1), el bloqueo por origen (D3), la baja (D8), los cupos (D9), la validación (D11), los registros (D13) y los números y el hash de la versión (D14). Verificar con el filtro de 3.1 en verde, `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completo en verde y el `git diff` del párrafo inicial; pegar las salidas
- [ ] 3.3 Medir el guardado con la semilla de quince y 200 personas sintéticas, en una base SQLite real en disco, 30 guardados de un cupo, con un cronómetro en el scratchpad de la sesión, fuera del repo. Pegar mediana y p95; si la mediana pasa de 500 ms, anotarlo como riesgo abierto para el gate de la verificación

## 4. Los paneles

- [ ] 4.1 Agregar `Kiosk.Tests/PanelPersonasTests.cs` y `Kiosk.Tests/PanelOpcionesTests.cs`, con bUnit y la base de la demostración en una carpeta temporal, para los escenarios con pantalla: la persona del código 0001 con sus cupos y sus nueve opciones; ningún código en el panel; persona sin cupos; la versión activa de INWeb, sin «Guardar» y con el aviso; «Cancelar» y Escape sin versión nueva; el campo inválido señalado; la franja vista como «11:00 a 14:00»; y, en `MaquinaAtencion`, «Fuera de horario» sin opciones y el mensaje del rechazo por horario al confirmar. Ver fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~PanelPersonas|FullyQualifiedName~PanelOpciones|FullyQualifiedName~FueraDeHorario"`; pegar la salida
- [ ] 4.2 Implementar los dos botones del diálogo y los dos paneles (D10, D12), con controles de 56 px, el campo de código enmascarado de sólo dígitos, los mensajes nuevos de `MensajesAtencion` y el motivo sin opciones por la causa en `MaquinaAtencion`. Verificar con el filtro de 4.1 en verde, `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` completo en verde y el `git diff` del párrafo inicial; pegar las salidas
- [ ] 4.3 Agregar `E2E.Tests/AdministracionE2ETests.cs` sin tocar los ayudantes existentes: con el puesto compilado y por CDP, abrir Configuración y «Personas habilitadas», agregar «Persona sintética 05» con `0105` y ALMUERZO límite 1, guardar, identificarse con `0105` y ver sólo las opciones de ALMUERZO; después del cierre ordenado, la general tiene el `CONFIGURATION_CHANGED` de la persona y ninguna de las dos bitácoras contiene `0105`. Como la conducta ya existe cuando nace, verla fallar con una afirmación imposible y revertirla. Sacar las capturas 13 (personas) y 14 (opciones con una franja) a 1024 × 768 y verlas una por una. Verificar con `dotnet test tests/Intelektron.INComedor.E2E.Tests` en verde; pegar las dos salidas y lo visto en cada captura

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
