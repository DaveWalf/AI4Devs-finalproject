# Tasks

Se aplica con `opus` · `xhigh`, supervisado. Es un plan diseñado con su verificador, pero tiene seguridad en alcance (mTLS y la primera entrada externa del sistema), y el apéndice de Claude Code manda para eso la línea de seguridad. Los comandos corren desde la raíz del repo y su salida se pega en la tarea. Al cerrar cada grupo hay un commit por rutas, en español, con `git commit -F <archivo>`.

Las pruebas existentes no se tocan, salvo las siete que aprobó David en el gate: las cuatro de D9 y las tres que borra D14. En el apply David aprobó además la línea 85 de `SemillaAtencionTests.cs`, el ayudante `LlevarAVersion1` (D9). `git diff --stat -- tests/` sólo puede mostrar los archivos nuevos, esos cuatro archivos y `ReceptorEsqueletoTests.cs`. Si otra tarea necesitara cambiar una prueba existente, se para y se le pregunta a David. Los chequeos de enlaces relativos y del mapa son los de D8 en `openspec/changes/archive/2026-10-29-mostrar-aplicacion-del-curso/design.md`.

## 1. Precondiciones y JSON canónico

- [x] 1.1 Comprobar que `atender-seleccion-y-confirmacion` ya está archivado, porque el delta de `modo-demostracion` modifica un requirement que ese change agrega. Verificar con `openspec list` que no figura entre los changes activos y con `openspec validate --all --strict` que no queda el aviso «Archive would refuse this delta». Si sigue activo, parar y avisarle a David: el archivado es suyo
- [x] 1.2 Crear `tests/Intelektron.INComedor.Contracts.Tests` (xUnit v3, en la solución) con `JsonCanonicoTests.cs`. Cubrir:
  - el orden de propiedades por unidades UTF-16 y el escape de cadenas, con los ejemplos del RFC 8785;
  - enteros en los extremos de ±(2^53 − 1);
  - el rechazo de un número decimal, de un exponente y de una propiedad repetida;
  - que el mismo objeto con otro orden y otros espacios da los mismos bytes.

  Verificar con `dotnet test tests/Intelektron.INComedor.Contracts.Tests` que no compilan o fallan antes de implementar, y pegar la salida
- [x] 1.3 Implementar `JsonCanonico` y `JsonCanonicoException` en `src/Intelektron.INComedor.Contracts` (D4), y los records del lote y del acuse con los nombres del OpenAPI (`SyncBatchRequest`, `CompletedBatchAck`, `EventAck`). Verificar con el mismo comando que pasan, y con `dotnet test INComedor.sln` que las 183 anteriores siguen en verde

  Salida de 1.1: `atender-seleccion-y-confirmacion` archivado por la IA con autorización de David en el gate (`e7e1362`). `openspec list`: sólo `recibir-lotes-idempotente` (0/28) e `imponer-invariantes-en-codigo` (17/19). `openspec validate --all --strict`: `Totals: 8 passed, 0 failed (8 items)`, sin «Archive would refuse this delta». El archivado dejó «Base SQLite con esquema versionado» en 565 caracteres, por encima de los 500 de `--strict`; por decisión de David se partió en dos, en la spec viva (`e7e1362`) y en el delta de este change (`b572b8c`), con el texto tal cual.

  Salida de 1.2 (antes de implementar): `dotnet test tests/Intelektron.INComedor.Contracts.Tests` no compiló: `error CS0246: El nombre del tipo o del espacio de nombres 'JsonCanonicoException' no se encontró` y `error CS0103: El nombre 'JsonCanonico' no existe en el contexto actual`; exit 1. Los ejemplos del RFC llevan sus secuencias de escape con un marcador (`%uXXXX` para el escape JSON y `%cXXXX` para el carácter), para que el fuente no dependa de cómo una herramienta escribe una barra-u. Nota: `dotnet sln add` informó «Se ha agregado» sin tocar el archivo, porque intentó sumar también la referencia a `Contracts`, que ya estaba; se agregó con `--include-references false` y se comprobó con `git diff --stat -- INComedor.sln`.

  Salida de 1.3: el mismo comando da `total: 23, error: 0, correcto: 23` (10 pruebas, 3 de ellas `Theory` con 4, 7, 2 y 4 casos). `dotnet build INComedor.sln -warnaserror`: `0 Advertencia(s) 0 Errores`. `dotnet test INComedor.sln`: `total: 206, error: 0, correcto: 206` (las 183 anteriores: Domain 68, Sqlite 73, Kiosk 38 y Sync.Api 4, más las 23 de Contracts).

## 2. Migración `0003` y versión de esquema 3

- [x] 2.1 Crear `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/MigracionLotesTests.cs` con los escenarios nuevos del delta de `registro-consumo`: «Base en la versión 2 con eventos pendientes» y «Un evento no se repite dentro de un lote». Sumar las columnas de las dos tablas contra el mapa y el `CHECK` de `comlotEstado`. Verificar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~MigracionLotesTests"` que fallan por el motivo correcto antes de implementar, y pegar la salida
- [x] 2.2 Cambiar las cuatro pruebas existentes de D9, y nada más, con la aprobación de David en el gate:
  - `AperturaBaseTests.Base_vacia_queda_en_version_2_con_las_21_tablas`: versión 3, las dos tablas en `TablasEsperadas`, y su nombre y su `DisplayName`;
  - `ServicioDemostracionTests.cs:27` y su `DisplayName`;
  - `SemillaAtencionTests.cs:69` y su `DisplayName`;
  - `MigracionesTests.cs:33` y su `DisplayName`.

  Verificar con `git diff -- tests/` que el diff toca sólo esas líneas, y pegarlo
- [x] 2.3 Crear `Migraciones/0003_lotes_sincronizacion.sql`, embebido como recurso, y llevar `BaseComedor` a `UltimaVersionConocida = 3` (D9). Agregar `comlotContenidoLote` y `comcasEvidencia` a `docs/nomenclatura-campos-er.csv`. Verificar:
  - con `dotnet test INComedor.sln`, que pasan las nuevas, las cuatro cambiadas y todas las demás;
  - con el chequeo del mapa, que las columnas de la `0003` están en el CSV;
  - con `git diff --stat -- tests/`, que sólo aparecen los cuatro archivos de 2.2 y `MigracionLotesTests.cs`.

  Salida de 2.1 (antes de implementar): `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~MigracionLotesTests"`: `total: 7, error: 7, correcto: 0`. Las dos de versión: `Expected: 3, Actual: 2`; las dos de columnas: `Actual: List<string> []`; el `CHECK` del estado: `Expected: 275, Actual: 1`; la clave primaria y la foránea: `SqliteException : SQLite Error 1: 'no such table: tbComedorLotesSincronizacion'` al armar el lote. Además del escenario de la versión 2, una prueba cubre la parte del escenario «Base en la versión 1» que la prueba existente no afirma («y las tablas de lotes vacías»), porque el gate sólo deja cambiarle la línea 33.

  Salida de 2.2: `git diff -- tests/` toca sólo lo aprobado. `AperturaBaseTests.cs`: `+ "tbComedorLotesSincronizacion",` y `+ "tbComedorLotesSincronizacion_Eventos",` en `TablasEsperadas`; el `DisplayName` de «versión de esquema 2 … 21 tablas» a «versión de esquema 3 … 23 tablas»; `Base_vacia_queda_en_version_2_con_las_21_tablas` a `Base_vacia_queda_en_version_3_con_las_23_tablas`, y `2L` a `3L`. `ServicioDemostracionTests.cs`: «esquema 2» a «esquema 3» y la línea 27 de `2L` a `3L`. `SemillaAtencionTests.cs`: «de la versión 1 a la 2» a «a la 3» y la línea 69 de `2L` a `3L`. `MigracionesTests.cs`: «queda en la versión 2» a «en la versión 3» y la línea 33 de `2L` a `3L`. `11 insertions(+), 9 deletions(-)`.

  Salida de 2.3: con la `0003`, `dotnet test INComedor.sln` dio `total: 213, error: 1`: `SemillaAtencionTests` «Atención sobre una base de la demostración en uso» devolvió `ErrorAlmacenamiento` en la línea 67. Causa, reproducida aparte con el `sqlite3` de Python y los tres scripts: su ayudante `LlevarAVersion1` simula la versión 1 borrando sólo la tabla de la `0002`, y la `0003` choca con `table tbComedorLotesSincronizacion already exists`. Se paró y se le preguntó a David, que aprobó ampliar el `DROP` de la línea 85 a las dos tablas de lotes (registrado en D9, `8076852`, antes del código). Después: `dotnet build INComedor.sln -warnaserror`, `0 Advertencia(s) 0 Errores`; `dotnet test INComedor.sln`, `total: 213, error: 0, correcto: 213` (206 más las 7 de `MigracionLotesTests`). Chequeo del mapa: el de D8 del change del curso revisa el mapa de prácticas, no columnas; para la `0003` se cruzó cada columna de sus `CREATE TABLE` con las filas `(table, column)` del CSV: `columnas de la migración: 7 · sin fila en el mapa: 0`; control negativo con el CSV de `HEAD`: `FALTA EN EL MAPA: tbComedorLotesSincronizacion.comlotContenidoLote`, 1; control positivo con la `0002`: 7 y 0. `comcasEvidencia` se cruza con el script del receptor en el grupo 4. `git diff --stat -- tests/`: sólo los cuatro archivos de 2.2 (`SemillaAtencionTests.cs` con la línea 85), `12 insertions(+), 10 deletions(-)`, más `MigracionLotesTests.cs` sin versionar.

## 3. Cola de lotes del puesto

- [x] 3.1 Crear `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/ColaSincronizacionTests.cs`, con una prueba por escenario de «Lote durable antes de enviar», «La cola avanza sólo con un acuse durable» y «El envío no toca las decisiones» del delta de `envio-lotes` (D10). La foto de «El envío no toca las decisiones» compara byte por byte las filas de intenciones, consumos, usos de cupo, comprobantes y trabajos. La de «El lote no lleva el código» busca el código en `comlotContenidoLote` y `comlotContenidoAcuse`. Verificar con `dotnet test … --filter "FullyQualifiedName~ColaSincronizacionTests"` que fallan antes de implementar, y pegar la salida
- [x] 3.2 Implementar `ColaSincronizacion` con `Preparar`, `RegistrarAcuse` y `RegistrarError`, y los registros de bitácora `SYNC_ACK_INVALID`, `SYNC_BATCH_ACKNOWLEDGED` y `SYNC_BATCH_FAILED` (D10). Verificar con el mismo filtro que pasan, con `dotnet test INComedor.sln` que nada más cambió y con `git diff --stat -- tests/` que no apareció ninguna prueba existente nueva

  Salida de 3.1 (antes de implementar): `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~ColaSincronizacionTests"` no compiló: `error CS0246: El nombre del tipo o del espacio de nombres 'ColaSincronizacion' no se encontró`, con `CS0103` para `ResultadoRegistroAcuse`; exit 1. El código de «El lote no lleva el código» es `ZQXLOTEKJ77`, una credencial sintética más de la persona habilitada, con letras fuera del hexadecimal para que un UUID del lote no pueda contenerlo por azar.

  Salida de 3.2: el filtro da `total: 20, error: 0, correcto: 20` (8 pruebas y dos `Theory`: siete acuses inválidos y cinco respuestas que no acusan). `dotnet build INComedor.sln -warnaserror`: `0 Advertencia(s) 0 Errores`. `dotnet test INComedor.sln`: `total: 233, error: 0, correcto: 233` (213 más 20). `git diff --stat -- tests/` vacío. Además de lo que pide D10, el acuse se rechaza si un resultado no trae el par estado-motivo del `EventAck` (por ejemplo `DENIED/LOCAL_LIMIT_REACHED`), y el `code` del problema entra a la bitácora sólo con la forma `^[A-Z_]{1,40}$`, porque llega del receptor (OWASP A09).

## 4. Receptor: base, autenticación y el fin del esqueleto

- [x] 4.1 Agregar `Microsoft.Data.SqlClient` a `Directory.Packages.props` (la versión estable vigente) y a `Sync.Api`. Verificar:
  - con `dotnet build INComedor.sln -warnaserror`, que NuGet Audit no marca vulnerabilidades;
  - con `dotnet list src/Intelektron.INComedor.Sync.Api package --vulnerable --format json`, leyendo el veredicto del JSON y no el código de salida.
- [x] 4.2 Crear en `Sync.Api.Tests` los auxiliares `CertificadosDePrueba.cs` y `ReceptorConKestrel.cs` (D2 y D13):
  - la autoridad y los certificados en memoria, exportados e importados como PFX;
  - Kestrel real en el puerto 0;
  - una base temporal de LocalDB por clase, borrada al terminar.

  Escribir primero una prueba de humo que abra TLS mutuo con un certificado registrado contra un endpoint de prueba, y pegar su salida antes y después. Si `WebApplicationFactory.UseKestrel` no existe en esta versión, usar `WebApplication` en el puerto 0 y anotarlo en la tarea
- [x] 4.3 Crear `AutenticacionEquipoTests.cs` con los cuatro escenarios de «Equipo autenticado por certificado», cada uno contando que no se guardó ninguna fila. Verificar que fallan antes de implementar, y pegar la salida
- [x] 4.4 Implementar el script `0001_receptor.sql`, su aplicación al arrancar, la conducta sin cadena de conexión (`503`), el filtro de autenticación (D1 y D2) y `ProblemasReceptor` (D8). Borrar en `ReceptorEsqueletoTests.cs` las tres pruebas de D14 y ajustar su resumen, con la aprobación del gate. Verificar:
  - con `dotnet test tests/Intelektron.INComedor.Sync.Api.Tests`, que pasan las de autenticación y `Ruta_no_declarada_responde_404`;
  - con `git diff -- tests/Intelektron.INComedor.Sync.Api.Tests/ReceptorEsqueletoTests.cs`, que sólo se borraron las tres pruebas y cambió el resumen.

  Salida de 4.1: `Microsoft.Data.SqlClient` 7.1.1, la última estable según `dotnet package search Microsoft.Data.SqlClient --exact-match` (después de 7.1.0). El primer `dotnet build INComedor.sln -warnaserror` falló con `error NU1903` y `error NU1902`: `System.Security.Cryptography.Pkcs` 9.0.18, transitivo de SqlClient, con CVE-2026-84008 (alta) y CVE-2026-70162 (moderada), corregidas en 9.0.21, 10.0.13 y 11.0.0-rc.2. Sólo saltaba en `Sync.Api.Tests`: en `Sync.Api`, que es web, el framework de ASP.NET poda el paquete. Se fijó `System.Security.Cryptography.Pkcs` 11.0.0-rc.2.26504.105, el tren de D-C25, en `Directory.Packages.props` y en el `.csproj` de `Sync.Api.Tests`, con un comentario. Después: build con `0 Advertencia(s) 0 Errores`. `dotnet list src/Intelektron.INComedor.Sync.Api package --vulnerable --include-transitive --format json` y el mismo comando sobre `Sync.Api.Tests`, leyendo `topLevelPackages` y `transitivePackages` del JSON: `paquetes vulnerables en el JSON: 0` en los dos. Control positivo, la lectura de antes de fijarlo: `System.Security.Cryptography.Pkcs 9.0.18: High,Moderate`.

  Salida de 4.2: antes de crear `ReceptorConKestrel`, `dotnet test tests/Intelektron.INComedor.Sync.Api.Tests --filter "FullyQualifiedName~TlsMutuoDePruebaTests"` no compiló: `error CS0246: … 'ReceptorConEndpointDePrueba' no se encontró`; exit 1 (`CertificadosDePrueba.cs` ya estaba escrito). Con el ayudante, la primera corrida falló en `InitializeAsync` con `SqlException … error: 50 - Se produjo un error de Local Database Runtime … SQL Server process failed to start` (`0x89c5010a`). Causa, leída en el `error.log` de la instancia: el `master` de `(localdb)\MSSQLLocalDB` de este equipo apunta `model` y `msdb` a `F:\dbs\sh\5uj5\…\mkmastr.nativeproj\model.mdf`, rutas de la compilación de Microsoft que no existen, y el servidor se apaga con el código 18. La instancia se creó el 13/04/2026 y tiene 40 volcados del 13 y el 15/04: nunca anduvo. Una instancia nueva de LocalDB (`SqlLocalDB create INComedorSonda -s`) arrancó bien y se borró. Se paró y se le preguntó a David, que eligió su instancia completa `INMO-DW-AR` (SQL Server 2025 Developer 17.0.1145.4; su usuario es `sysadmin`, memoria compartida, sin cifrado forzado). D1 se corrigió antes del código (`1c9bb9c`): la instancia sale de `INCOMEDOR_SQLSERVER`, con `(localdb)\MSSQLLocalDB` por defecto, que es la de la CI, y la variable quedó en `INMO-DW-AR` en el entorno de usuario de David. Con eso, `total: 4, error: 0, correcto: 4` en 26 s: TLS mutuo real con Kestrel en el puerto 0, el servidor ve la huella del certificado del puesto, un certificado de otra autoridad no corta el handshake, sin certificado el handshake sigue, y la base temporal responde `SELECT DB_NAME()`. `WebApplicationFactory.UseKestrel(Action<KestrelServerOptions>)` existe en Mvc.Testing 11.0.0-rc.2: no hizo falta `WebApplication` en el puerto 0. El certificado registrado se prueba desde la 4.3, porque `tbComedorPuestos` la crea el script de la 4.4. Al terminar no queda ninguna base `INComedorPruebas*` en la instancia.

  Salida de 4.3 (antes de implementar): `total: 4, error: 4`. Sin certificado y sin registrar: `Expected: Unauthorized` y `Expected: Forbidden`, con `Actual: NotImplemented`. Vencido y de otra autoridad: `SqlException : Invalid object name 'tbComedorPuestos'` al registrarlos, porque el esquema del receptor todavía no existe. El vencido y el de otra autoridad se registran a propósito, para que sólo la cadena y la vigencia puedan devolver el `401`.

  Salida de 4.4: `dotnet build INComedor.sln -warnaserror`, `0 Advertencia(s) 0 Errores`; `dotnet test tests/Intelektron.INComedor.Sync.Api.Tests`, `total: 9, error: 0, correcto: 9` (humo 4, autenticación 4 y `Ruta_no_declarada_responde_404`); `dotnet test INComedor.sln`, `total: 238, error: 0, correcto: 238`. `git diff -- …/ReceptorEsqueletoTests.cs`: se borran las tres pruebas del `501`, cambia el resumen y salen tres declaraciones que sólo ellas usaban (`using System.Text`, `RutaLotes` y `TipoProblema`); `Ruta_no_declarada_responde_404` no tiene ninguna línea en el diff, `45 deletions(-)` y 2 inserciones del resumen. Cruce de columnas de `0001_receptor.sql` con el mapa: `columnas de la migración: 24 · sin fila en el mapa: 0`; control negativo con el CSV anterior al grupo 2: `FALTA EN EL MAPA: tbComedorCasosConciliacion.comcasEvidencia`. Hasta los grupos 5 y 6, un pedido autenticado sigue recibiendo el `501` del esqueleto.

## 5. Receptor: ingesta idempotente y resultado por evento

- [x] 5.1 Crear `LoteIdempotenteTests.cs`, con «Lote idempotente por BatchId» y «Lote inválido sin efectos». La prueba de concurrencia manda dos `POST` en paralelo con `Task.WhenAll` y cuenta recibos y consumos consolidados. Verificar que fallan antes de implementar, y pegar la salida
- [x] 5.2 Crear `ResultadoPorEventoTests.cs` con:
  - «Deduplicación y consolidación por evento»;
  - «Ningún acuse reautoriza ni revierte»;
  - «Validación por aplicación de cada evento»;
  - «Eventos que esperan una referencia o una revisión».

  Cada `DisplayName` cita su AT o el texto del contrato. Verificar que fallan antes de implementar, y pegar la salida
- [x] 5.3 Implementar `ValidadorLote` (D5) e `IngestaLotes` (D3, D6 y D7). Verificar con `dotnet test tests/Intelektron.INComedor.Sync.Api.Tests` que pasan las de 5.1 y 5.2, y con `dotnet test INComedor.sln` que nada más cambió

  Salida de 5.1 y 5.2 (antes de implementar), con `INCOMEDOR_SQLSERVER=INMO-DW-AR`: `dotnet test tests/Intelektron.INComedor.Sync.Api.Tests --filter "FullyQualifiedName~LoteIdempotenteTests|FullyQualifiedName~ResultadoPorEventoTests"`: `total: 39, error: 39`, todas con `Actual: NotImplemented` (28 `Expected: Created`, 6 `UnprocessableEntity`, 2 `OK`, 1 `BadRequest`, 1 `Conflict` y la de concurrencia `[NotImplemented, NotImplemented]`): el receptor autenticaba y respondía el `501` del esqueleto. `LoteIdempotenteTests`: 6 pruebas y una `Theory` de 6 lotes inválidos (los tres escenarios más «sin eventos», «un evento sin EventId legible» y una propiedad que `SyncBatchRequest` no declara). `ResultadoPorEventoTests`: 14 pruebas, una por escenario de los cuatro requirements, y una `Theory` de 13 eventos fuera del esquema de su tipo (OWASP A08). «Llega la referencia» verifica el acuse original del primer lote reenviándolo, porque el `GET` es del grupo 6; la consulta por `GET` se suma en `ConsultaYErroresTests`.

  Salida de 5.3: `dotnet test tests/Intelektron.INComedor.Sync.Api.Tests`: la primera corrida dio `total: 48, error: 1`, en la prueba de concurrencia, por un error de la prueba: `Order()` ordena los estados por su número (200 antes que 201) y el esperado decía `[Created, OK]`; se corrigió a `[OK, Created]` y la conducta no cambió. Después: `total: 48, error: 0, correcto: 48`, y la de concurrencia, `correcto: 1` en cinco corridas seguidas. `dotnet build INComedor.sln -warnaserror`: `0 Advertencia(s) 0 Errores`. `dotnet test INComedor.sln`: `total: 277, error: 0, correcto: 277` (238 más 39). No quedan bases `INComedorPruebas*` en la instancia. `git diff --stat -- tests/`: sólo `LotesDePrueba.cs` y `ReceptorConKestrel.cs`, dos ayudantes creados en el grupo 4 de este change. La clave ausente responde `400 INVALID_REQUEST` y una clave que no es el BatchId, `400 IDEMPOTENCY_KEY_MISMATCH`; un cuerpo sin BatchId legible es `422` antes de mirar la clave, porque sin BatchId no hay con qué compararla.

## 6. Receptor: consulta y errores

- [x] 6.1 Crear `ConsultaYErroresTests.cs` con:
  - «Consulta del acuse original»;
  - «Errores que no reflejan la entrada»;
  - la prueba de que sin cadena de conexión las dos operaciones responden `503` con `code` `SERVICE_UNAVAILABLE`.

  La de «Lote de otro puesto» compara las dos respuestas `404` campo por campo, salvo `correlationId`. Verificar que fallan antes de implementar, y pegar la salida
- [x] 6.2 Implementar el `GET`, el tope de 1 MiB y el manejador de excepciones (D7 y D8). Verificar con `dotnet test tests/Intelektron.INComedor.Sync.Api.Tests` que pasan todas, y validar contra el OpenAPI que cada cuerpo de error y de acuse de las pruebas cumple `ProblemDetails` o `CompletedBatchAck` (prueba de contrato en el mismo archivo)

  Salida de 6.1 (antes de implementar): `dotnet test … --filter "FullyQualifiedName~ConsultaYErroresTests|FullyQualifiedName~ExcepcionNoPrevistaTests"`: `total: 13, error: 6`. Las cuatro del `GET` y la de contrato, con `Actual: NotImplemented`; la de la excepción no prevista, `Expected: ServiceUnavailable, Actual: InternalServerError`. Las otras siete ya pasaban, porque el tope de 1 MiB y los `400` del cuerpo entraron en la 5.3 (leer el cuerpo los necesitaba) y el `503` sin configuración, en la 4.4. Para verlas fallar por su motivo se mutó el código con `src` limpio y se restauró con `git restore`: con el tope en 64 MiB, «Cuerpo de más de 1 MiB» dio `Actual: UnprocessableEntity`; sin el chequeo de configuración completa, «Sin cadena de conexión» dio `Actual: Unauthorized`. La excepción no prevista se provoca con un `TimeProvider` que falla, registrado con `ConfigureTestServices` en una subclase de `ReceptorConKestrel`, en la clase `ExcepcionNoPrevistaTests` del mismo archivo.

  Salida de 6.2: el primer build falló (`CS4010` y `CS0029`, el ternario del `GET` sin tipo común), y la corrida con `--no-build` usó los binarios de la mutación; se tipó la lambda como `Task<IResult>` y se recompiló antes de leer resultados. Después, `total: 61, error: 1`: la prueba de contrato decía «CompletedBatchAck no declara la propiedad Status», por un error de su lector de esquemas, que con `\n` al principio de cada coincidencia salteaba una propiedad de cada dos; se pasó a `(?m)^`. La prueba suma un control de ese lector: lista exactamente las propiedades de `ProblemDetails`, `CompletedBatchAck` y `EventAck`, los 12 `code` del enum, y rechaza una propiedad no declarada, un `code` fuera del enum y un `Status` `DENIED`. Con eso: `dotnet test tests/Intelektron.INComedor.Sync.Api.Tests`, `total: 61, error: 0, correcto: 61`; `dotnet build INComedor.sln -warnaserror`, `0 Advertencia(s) 0 Errores`; `dotnet test INComedor.sln`, `total: 290, error: 0, correcto: 290` (277 más 13). La prueba de contrato valida tres acuses (`201`, el `200` del reenvío y el `200` del `GET`) y siete errores (`400` de cuerpo, `400` de clave, `401`, `403`, `404`, `409` y `422`) contra los esquemas leídos de `api/openapi.yaml`. El `GET` con un `batchId` que no es UUID responde `400 INVALID_REQUEST`. No quedan bases `INComedorPruebas*`.

## 7. Sincronizador, cliente HTTP y envío integrado

- [ ] 7.1 Crear `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/SincronizadorPuestoTests.cs` con «Reenvío idéntico ante un acuse perdido» y «Respuestas que no acusan». Usa un `IReceptorLotes` de prueba, el doble aprobado en el gate. Verificar que fallan antes de implementar, y pegar la salida
- [ ] 7.2 Implementar `IReceptorLotes` y `SincronizadorPuesto` con su `EstadoSincronizacion` (D11). Verificar con el mismo filtro que pasan
- [ ] 7.3 Crear `tests/Intelektron.INComedor.Kiosk.Tests/EnvioDeLotesIntegradoTests.cs`, con la referencia a `Sync.Api` y a `Microsoft.AspNetCore.Mvc.Testing`, sobre SQLite real, `ClienteReceptorHttp`, Kestrel con mTLS y LocalDB:
  - AT27: dos ciclos dan un solo consumo consolidado, y un corte después del commit del receptor se resuelve con la consulta;
  - AT13: un certificado vencido retiene los eventos y la confirmación siguiente es `AUTHORIZED`;
  - «Receptor inalcanzable».

  Verificar que fallan antes de implementar `ClienteReceptorHttp`, y pegar la salida
- [ ] 7.4 Implementar `ClienteReceptorHttp` en `src/Intelektron.INComedor.Kiosk/Sincronizacion/` (D11). Verificar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~EnvioDeLotesIntegradoTests"` que pasan, y con `dotnet test INComedor.sln` que todo sigue en verde

## 8. Demostración conectada

- [ ] 8.1 Crear `tests/Intelektron.INComedor.Kiosk.Tests/FranjaSincronizacionTests.cs` (bUnit, los cinco mensajes de D11) y `ModoDemostracionReceptorTests.cs`, con el requirement «Receptor y puesto de demostración conectados» sobre una carpeta temporal. Verificar que fallan antes de implementar, y pegar la salida
- [ ] 8.2 Implementar `--demostracion` en `Sync.Api` (D12), el ciclo periódico en `ServicioAtencion` y `FranjaSincronizacion.razor` (D11). Verificar con `dotnet test INComedor.sln` que todo pasa, y con `git status --short` que no aparece ningún `.pfx` ni `.cer` en el repo
- [ ] 8.3 Actualizar `AGENTS.md` (el comando del receptor deja de decir «responde 501»), `readme.md` («Cómo ver la demostración», con el receptor, la carpeta de certificados, el riesgo de los PFX sin contraseña y cómo reiniciar) y `docs/cobertura-entrega.md`. Verificar con el chequeo de enlaces relativos que no hay enlaces rotos

## 9. Verificación integrada y revisión OWASP

- [ ] 9.1 Revisar contra OWASP Top 10 2021 lo que toca entrada externa: el receptor, el cuerpo del acuse que lee el puesto y los certificados de la demostración. Escribir en esta tarea una tabla A01 a A10 con lo que se encontró y la prueba o la decisión que lo cubre. Verificar que cada fila nombra una prueba existente o un riesgo del design, y correr `gitleaks detect --no-banner` sobre el repo, leyendo que diga `commits scanned` mayor que 0
- [ ] 9.2 Correr el receptor con `--demostracion` y el puesto real. Recorrer:
  - una autorización y un rechazo, con «Sincronización: al día» y los dos eventos en el receptor;
  - el receptor detenido, con «N pendientes» y la atención andando;
  - el receptor de vuelta, con un solo consumo consolidado.

  Guardar las capturas en `C:\AI\INComedor\ai4devs\gestion\evidencias\recibir-lotes-idempotente\`. Al final, borrar `%LOCALAPPDATA%\INComedor\demo\` y la base `INComedorReceptorDemo`, si no existían antes. Pegar la consulta SQL que cuenta los consumos consolidados
- [ ] 9.3 Correr `dotnet build INComedor.sln -warnaserror`, `dotnet test INComedor.sln` y `openspec validate --all --strict`, y pegar la línea final de cada uno, con la tabla de escenarios contra pruebas del change. Verificar con `git diff --stat dac38e5..HEAD -- tests/` que las únicas pruebas existentes cambiadas son las siete del gate

## 10. Tallas y evidencia

- [ ] 10.1 Registrar en `docs/plan-academico.md` (tallas por change) las horas reales de reloj del apply contra la talla de la IA y la de David, sin promediar. Agregar la fila de `recibir-lotes-idempotente` en `docs/aplicacion-del-curso.md` y, si el change dejó un workflow o un ajuste humano nuevo, su entrada en `prompts.md`. Verificar con el chequeo de enlaces relativos que no hay enlaces rotos

## Workflow follow-up

- `/openspec-verify-change` y su informe en el arnés.
- PR, merge y archivado del change: son de David.
- Si la CI no tiene LocalDB, el paso en `ci.yml` lo decide David.
