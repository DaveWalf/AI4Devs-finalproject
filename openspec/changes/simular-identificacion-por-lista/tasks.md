# Tasks

Ninguna prueba existente cambia (punto 6 del gate); la ampliación del grupo 9 cambia cuatro de este mismo change, con la aprobación de David (A4), como dice su encabezado. Al cerrar cada grupo, `git diff --diff-filter=M --name-only 07a2329 -- tests/` sale vacío; si una prueba existente falla, se para y se le pregunta a David. Cada prueba nueva se ve fallar antes de implementar lo que la hace pasar, y su `DisplayName` empieza con la capability y el nombre del escenario («atencion-puesto · Enter envía el código»). Las de `Kiosk.Tests` y la E2E corren con `INCOMEDOR_SQLSERVER` apuntando a la instancia del equipo, como pide `AGENTS.md`.

## 1. Enter en el campo del código

- [x] 1.1 Agregar a `Kiosk.Tests` el archivo nuevo `EnterEnElCodigoTests.cs` con «Enter envía el código» y «Enter rechaza igual que «Ingresar» y una sola vez» (un solo `IDENTIFICATION_REJECTED` nuevo, sin `7777` en la bitácora ni en la pantalla), y verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~EnterEnElCodigo"`.
- [x] 1.2 Sumar `@onkeydown` al `<input id="codigo">` de `PantallaAtencion.razor`, con Enter llamando a `Ingresar()` (D8), y verificar con el mismo filtro que las dos pasan y que `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` no tiene fallas.

## 2. Identificar por la credencial elegida

Nota del apply: la semilla de la atención, con los ocho códigos, es un recurso del Kiosk que `Infrastructure.Sqlite.Tests` no referencia. Las pruebas de 2.1 y 2.3 corren sobre el fixture sintético de ese proyecto (su persona habilitada, una credencial vencida insertada y un GUID desconocido), y la lista de las ocho personas sembradas se prueba en `ComboSimulacionTests` (6.1, «El combo no muestra ni lleva códigos»).

- [x] 2.1 Agregar a `Infrastructure.Sqlite.Tests` el archivo nuevo `IdentificacionPorCredencialTests.cs`, sobre un SQLite real temporal con la semilla de la atención, y verlas fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~IdentificacionPorCredencial"`. Son tres:
  - la credencial de `0001` abre una sesión `CONSUMPTION` con método `CODE` y un `SESSION_OPENED` con `Origin = "SIMULATION_LIST"`;
  - la de `0002` da `CREDENTIAL_EXPIRED` sin sesión, con `Origin` en el rechazo;
  - un GUID que no está en el snapshot activo da `IDENTITY_NOT_RESOLVED` con `UNKNOWN_CODE` y sin `PersonId`.
- [x] 2.2 Implementar `IdentificacionPorCodigo.IdentificarPorCredencial`, compartiendo con `Identificar` la clasificación, la sesión y el registro, con la clave `Origin` sólo en este camino (D2, D7). Verificar con el filtro de 2.1 que las tres pasan, y con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` que las de la identificación tipeada siguen en verde sin cambios.
- [x] 2.3 Agregar a `IdentificacionPorCredencialTests.cs` la prueba de la lista: ocho entradas, en orden alfabético `es-AR`, con valores que son GUID de credenciales y textos sin ninguno de los ocho códigos. Verla fallar, implementar `ListarParaSimulacion()` (D3) y verla pasar con el mismo filtro.

## 3. La máquina identifica por la credencial

- [x] 3.1 Agregar a `Kiosk.Tests` el archivo nuevo `MaquinaSimulacionTests.cs` y verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~MaquinaSimulacion"`. Son dos:
  - la credencial de `0001` lleva a la selección con su nombre;
  - durante una atención, otra credencial da «Ocupado» sin cambiar la persona ni la opción.
- [x] 3.2 Implementar `MaquinaAtencion.IdentificarPorCredencial` con las guardas y el después de `Identificar`, sin duplicarlos (D2), y verificar con el filtro de 3.1 que pasan.

## 4. El modo simulación y su almacén

- [x] 4.1 Agregar a `Kiosk.Tests` el archivo nuevo `AlmacenSimulacionTests.cs` y verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~AlmacenSimulacion"`. Son tres:
  - sin archivo, ilegible o con otro valor, está apagada;
  - guardar encendida y leer da encendida;
  - una escritura que falla deja el archivo anterior y lanza la excepción a quien llama.
- [x] 4.2 Implementar `AlmacenSimulacion` (`simulacion.json`, escritura atómica, D4), registrarlo en `MainWindow` junto a `AlmacenModoPuesto`, y verificar con el filtro de 4.1 que pasan y con `dotnet build INComedor.sln -warnaserror` que compila sin advertencias.

## 5. El interruptor en General

- [x] 5.1 Agregar a `Kiosk.Tests` el archivo nuevo `ModoSimulacionTests.cs`, con los cinco escenarios de «Modo simulación sólo en mantenimiento» y los tres de «El modo simulación queda en la bitácora general». Verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~ModoSimulacion"`. Entre ellas van:
  - el orden de los dos `CONFIGURATION_CHANGED` al pasar a operativo, los dos con `DAILY_KEY`;
  - el arranque en operativo con el archivo encendido, que lo deja guardado apagado;
  - la carpeta sin escritura, con `#aviso-simulacion` y `Result = "FAILED"`.
- [x] 5.2 Implementar en `PanelConfiguracion.razor` y en `PantallaAtencion.razor` el interruptor, la regla efectiva y su registro (D5, D6, D7), con `MensajesAtencion.SimulacionNoGuardada`. Verificar con el filtro de 5.1 que las ocho pasan, y con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` que `AccesoPuestoTests` y `PestanasConfiguracionTests` siguen en verde sin cambios.

## 6. El combo en la pantalla de identificación

- [x] 6.1 Agregar a `Kiosk.Tests` el archivo nuevo `ComboSimulacionTests.cs` con los seis escenarios de «Identificación eligiendo a la persona en modo simulación», más una prueba de borde: un valor que no es un GUID se ignora sin identificar. Verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~ComboSimulacion"`.
  - Fallaron seis de siete. «Sin el modo simulación no hay combo» pasó desde antes: prueba una ausencia, que el código anterior ya cumplía, y queda como guarda.
  - Dos pruebas de 5.1, «Encenderla se conserva al volver a abrir» y «Pasar a operativo la apaga», miran el combo: fallaron en su línea del combo hasta 6.3, y desde ahí pasan las ocho.
- [x] 6.2 Agregar a `E2E.Tests` el archivo nuevo `SimulacionE2ETests.cs`. Sobre el puesto compilado: encender la simulación en General, cerrar Configuración, elegir en el combo a la persona de `0001` y llegar a «Elegí tu servicio de hoy» con su nombre, sin que el texto de la página contenga `0001`. Verla fallar con `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter "FullyQualifiedName~SimulacionE2E"`. Es la tarea que sale con el recorte del punto 1 del gate.
  - Además, antes de elegir a `0001`, elige a la persona de `0002` y comprueba en el DOM real que el combo vuelve a la opción vacía.
  - Falló a las 03:46 contra el EXE de `bin\Debug` sin el cambio (`TimeoutException` esperando el interruptor) y pasó a las 03:55 contra el EXE publicado en 8.2 (su `registro.txt` nombra `artifacts\publicacion\puesto\Intelektron.INComedor.Kiosk.exe`).
- [x] 6.3 Implementar el combo (`<select id="simulacion-persona">` con su `<label>`, D9) y la lectura de la lista (D10) en `PantallaAtencion.razor`, y su alto táctil en `wwwroot/css/puesto.css`. Verificar con los filtros de 6.1 y 6.2 que pasan, y con `dotnet test INComedor.sln` que no hay ninguna falla.

## 7. Documentos y evidencia

- [x] 7.1 Sumar a `AGENTS.md` y a `openspec/config.yaml` la línea de alcance del punto 8 del gate (D11), y verificar con `openspec validate --all --strict` que no hay errores.
  - Salida del 10/11 a las 03:53: `Totals: 16 passed, 0 failed (16 items)`.
- [x] 7.2 Sumar al recorrido de verificación de `readme.md` cómo encender la simulación en General y elegir a una persona en el combo, sin nombrar ningún código como valor del combo.
- [x] 7.3 Sumar la fila del change a `docs/aplicacion-del-curso.md`, con el nombre del change y sin enlace propio a su carpeta (D5 de `mostrar-aplicacion-del-curso`); sin entrada en `prompts.md`, porque no deja un workflow ni un ajuste humano nuevo. Verificar 7.2 y 7.3 con el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso` sobre `readme.md`, `AGENTS.md` y `docs/aplicacion-del-curso.md`, con su control negativo (un enlace roto a propósito que el chequeo tiene que marcar).
  - La evidencia va en la fila existente de OWASP, que ya nombra cada change que la aplicó: el mapa no tiene una fila por change.
  - Control negativo, un `.md` temporal con un archivo inexistente, un ancla falsa y una válida: `ROTO`, `ANCLA` y `enlaces rotos: 2`. Sobre `readme.md`, `AGENTS.md` y `docs/aplicacion-del-curso.md`: `enlaces rotos: 0`.

## 8. Verificación integral

- [x] 8.1 Desde la raíz del repo, correr y pegar las salidas:
  - `dotnet build INComedor.sln -warnaserror`, con 0 advertencias;
  - `dotnet test INComedor.sln`, con 0 fallas;
  - `openspec validate --all --strict`, sin errores;
  - `git diff --diff-filter=M --name-only 07a2329 -- tests/`, vacío.
  - Nota del apply: un puesto abierto con `dotnet run` desde las 03:25 tenía tomado `src\Intelektron.INComedor.Kiosk\bin\Debug`, así que el build en Debug corrió con `--artifacts-path artifacts/apply-sim` (03:59: `0 Advertencia(s)`, `0 Errores`). La solución corrió en Release, compilada por 8.2 con `-warnaserror` (`0 Advertencia(s)`), con `INCOMEDOR_E2E_KIOSK_EXE` apuntando al EXE publicado: a las 03:58, `total: 764`, `error: 0` (Contracts 23, Domain 91, Sync.Api 62, Sqlite 206, Kiosk 356 y E2E 26). Son las 733 de `07a2329` más las 31 nuevas.
  - `openspec validate --all --strict` a las 03:59: `Totals: 16 passed, 0 failed (16 items)`. El `git diff` de pruebas modificadas, vacío.
- [x] 8.2 Publicar el puesto con `pwsh .github/scripts/publicar-puesto.ps1 -Version 0.0.0-local`, que corre la E2E contra el EXE, para que David lo pruebe desde `artifacts\publicacion\puesto`.
  - A las 03:54: build Release `-warnaserror` con 0 advertencias, «publicación revisada: 482 entradas, ninguna con datos ni secretos», el E2E del flujo principal contra el EXE 1 de 1 y el zip de 77.496.728 bytes (sha256 `a7a7b278…0f8373`).

## 9. Ampliación del 10/11: encendida por omisión y la fila del código

Sólo después del gate de la ampliación (`proposal.md`, puntos A1 a A8). Cada prueba que cambia está en la lista del punto A4, y ninguna otra; al cerrar cada tarea, `git diff --diff-filter=M --name-only f9cd6c0 -- tests/` lista sólo `AlmacenSimulacionTests.cs`, `ModoSimulacionTests.cs`, `ComboSimulacionTests.cs` y `SimulacionE2ETests.cs`, y `git diff --diff-filter=M --name-only 07a2329 -- tests/` sigue vacío.

- [x] 9.1 Cambiar en `AlmacenSimulacionTests.cs` la teoría del ajuste ausente o ilegible a «encendida», y en `ModoSimulacionTests.cs` las ocho pruebas del punto A4, con los `DisplayName` de los escenarios nuevos de `acceso-puesto`. Verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~AlmacenSimulacion|FullyQualifiedName~ModoSimulacion"`.
  - Salida del 10/11 a las 13:28, contra el código de `f9cd6c0`: `total: 15`, `error: 12`, `correcto: 3`. Fallaron los cinco casos de la teoría (`Assert.True() Failure`) y siete de las ocho de `ModoSimulacionTests`: la carpeta nueva y el ilegible sin combo (`Assert.NotEmpty() Failure`), «Apagarla se conserva» y «Apagar la simulación» sin `#apagar-simulacion`, «En operativo nunca se ve» y el arranque en operativo con lo guardado apagado (`Assert.True() Failure`), y «Pasar a operativo no la registra» con `SIMULATION_MODE` de más (`Collections differ`).
  - Pasaron las dos del almacén que no cambian y «La simulación no se puede guardar»: apagar y encender fallan igual, así que su conducta ya existía. Se la vio fallar con un mutante de 9.2 (lo guardado asignado antes de escribir el archivo): `Expected: "Simulación: encendida"`, `Actual: "Simulación: apagada"`, `error: 1`; el mutante se revirtió.
- [x] 9.2 En `AlmacenSimulacion.Leer`, sólo `"APAGADA"` da apagada (D4). En `PantallaAtencion.razor`, sacar el apagado al pasar a operativo y el del arranque con `STARTUP` (D5, D12). Verificar con el filtro de 9.1 que pasan, y con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` que no hay ninguna falla.
  - El filtro de 9.1: `total: 15`, `error: 0`.
  - `Kiosk.Tests` entero: `total: 356`, `error: 1`. La única roja es `ComboSimulacionTests` «Sin el modo simulación no hay combo» (`Assert.Empty() Failure: Collection was not empty`), que usa una carpeta nueva y ahora ve el combo: es la que cambia 9.3 por el punto A4, y falla sólo por el valor por omisión nuevo. Ninguna de `07a2329` falló.
- [x] 9.3 Agregar a `Kiosk.Tests` el archivo nuevo `PantallaIdentificacionTests.cs`, con «El título invita a disfrutar la comida» y el orden de la fila en el DOM: el combo antes del campo, y el campo e «Ingresar» juntos en `.ingreso`, con y sin simulación. Cambiar en `ComboSimulacionTests.cs` «Sin el modo simulación no hay combo» para que guarde la simulación apagada. Verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~PantallaIdentificacion|FullyQualifiedName~ComboSimulacion"`.
  - Salida del 10/11 a las 13:32: `total: 10`, `error: 3`. Las tres nuevas en rojo: el título (`Expected: "¡Hola! Identificate y disfrutá tu comida"`, `Actual: "Identificate para comenzar"`), el orden con simulación (`Collections differ at index 0`, `Expected: "simulacion-persona"`, `Actual: "codigo"`) y la fila sin simulación (`No elements were found that matches the selector '.atencion .ingreso'`).
  - «Sin el modo simulación no hay combo», ya cambiada, pasa: una simulación guardada apagada no tiene combo desde `b5ee4d3`. Su rojo es el de 9.2, cuando la versión de antes, con una carpeta nueva, vio el combo.
- [x] 9.4 Implementar `MensajesAtencion.TituloReposo` (D13) y la fila `.ingreso` en `PantallaAtencion.razor`, con el combo arriba, y su CSS en `wwwroot/css/puesto.css` (D12). Verificar con el filtro de 9.3 que pasan.
  - `Kiosk.Tests` entero a las 13:33: `total: 359`, `error: 0` (las 356 de 9.2 más las tres nuevas).
- [x] 9.5 Cambiar `SimulacionE2ETests.cs`: sin el toque de «Encender la simulación», y con la medida de las cajas en 1024 × 768 (el combo entero por encima del campo, «Ingresar» a la derecha del campo y en su fila, los tres de al menos 56 px). Verla fallar contra el EXE publicado a las 03:52 y pasar contra el que publique 9.7.
  - El área de 1024 × 768 se fija por CDP como en `PresentacionTactilTests`, y las medidas y la captura `reposo-simulacion-1024x768` quedan en el `registro.txt` de la corrida.
  - A las 13:34, contra el EXE publicado a las 03:52 (`INCOMEDOR_E2E_KIOSK_EXE`): `error: 1`, `Locator expected to be visible` en `Locator("#simulacion-persona")`, porque una carpeta nueva arrancaba sin combo.
  - A las 13:35, contra el puesto compilado en Debug con 9.4: `error: 0`; el registro dice «combo de 217,0 a 697,0 px y de 220,5 a 276,5 px, 56,0 px de alto; campo de 217,0 a 673,6 px y de 292,5 a 348,5 px, 56,0 px de alto; «Ingresar» de 685,6 a 807,0 px y de 292,5 a 348,5 px, 56,0 px de alto». La captura, mirada: el título nuevo, el combo arriba y la fila del código entera.
  - Mutante de la medida, revertido: `.ingreso` en columna da `«Ingresar» empieza en 217 px y el campo termina en 807 px`, `error: 1`.
  - A las 13:42, dentro de la corrida de 9.7, contra el EXE que publicó 9.7: en verde, con las mismas medidas, y su `registro.txt` nombra `artifacts\publicacion\puesto\Intelektron.INComedor.Kiosk.exe`.
- [x] 9.6 Actualizar el paso 8 del recorrido de `readme.md`: la simulación viene encendida, «Apagar la simulación» la apaga, y en operativo no se ve, pero vuelve en mantenimiento. Si el gate aprueba el punto A6, sumar la decisión 7 a `IDSerialSim/AGENTS.md`, en su repo y sin push. Verificar con el chequeo de enlaces de 7.3 que `readme.md` sigue con 0 rotos.
  - El paso 9 nombraba «el encendido del paso 8»: dice ahora «el apagado y el encendido del paso 8».
  - La decisión 7, en `6755e97` de `IDSerialSim` («Decisión 7: la simulación por lista, encendida por omisión»), sin push.
  - Chequeo de enlaces del 10/11 a las 13:37: el control negativo da `ROTO`, `ANCLA` y `enlaces rotos: 2`; `readme.md`, `AGENTS.md` y `docs/aplicacion-del-curso.md`, `enlaces rotos: 0`.
- [x] 9.7 Repetir la verificación integral de 8.1 y la publicación de 8.2, y pegar las salidas, con los dos `git diff` de pruebas del encabezado de este grupo.
  - Publicación, de 13:38 a 13:40, sobre `718d8ed`: `pwsh .github/scripts/publicar-puesto.ps1 -Version 0.0.0-local` salió 0, con el build Release `-warnaserror` en `0 Advertencia(s)` y `0 Errores`, «publicación revisada: 482 entradas, ninguna con datos ni secretos» dos veces, el E2E del flujo principal contra el EXE en `total: 1`, `error: 0`, y el zip de 77.496.526 bytes (sha256 `8aec2bb5…91c56bb8`).
  - `dotnet build INComedor.sln -warnaserror`: `0 Advertencia(s)`, `0 Errores`.
  - `dotnet test INComedor.sln -c Release`, con `INCOMEDOR_SQLSERVER=INMO-DW-AR` e `INCOMEDOR_E2E_KIOSK_EXE` en el EXE publicado, de 13:40 a 13:45: `total: 767`, `error: 0` (Contracts 23, Domain 91, Sync.Api 62, Sqlite 206, Kiosk 359 y E2E 26). Son las 764 de `f9cd6c0` más las tres de `PantallaIdentificacionTests`.
  - `openspec validate --all --strict`: `Totals: 16 passed, 0 failed (16 items)`.
  - `git diff --diff-filter=M --name-only f9cd6c0 -- tests/`: `SimulacionE2ETests.cs`, `AlmacenSimulacionTests.cs`, `ComboSimulacionTests.cs` y `ModoSimulacionTests.cs`, los cuatro de A4. `git diff --diff-filter=M --name-only 07a2329 -- tests/`: vacío.

## 10. Correcciones de la verificación

La verificación del 10/11 a las 15:48, sobre `6657f78` y sin críticos, dejó una advertencia y tres sugerencias; David eligió a las 15:50 «Corregir y archivar».

- [x] 10.1 Corregir el «Migration Plan» de `design.md`: un puesto sin `simulacion.json` arranca con la simulación encendida desde D12, no apagada (la advertencia).
- [x] 10.2 Marcar en `design.md` el objetivo «Que ninguna prueba existente cambie» y el no-objetivo «Reordenar la pantalla…» como superados por la ampliación, y remitir el encabezado de este archivo al grupo 9.
- [x] 10.3 Cambiar el `DisplayName` de `AlmacenSimulacionTests.Guardar_y_leer` del escenario reemplazado «Encenderla se conserva al volver a abrir» a «Apagarla se conserva al volver a abrir», con la aprobación de David de las 15:50; sólo el nombre. Con el filtro `FullyQualifiedName~AlmacenSimulacion`: `total: 7`, `error: 0`; `openspec validate --all --strict`: 16 de 16.

## Workflow follow-up

- La revisión de David (el gate de `proposal.md`) va antes del apply, y lo que decida queda escrito en `proposal.md`.
- Después de su prueba en el ejecutable, `/openspec-verify-change`, y el archivado en el mismo PR hacia `final-project-dw`, con la salida del verify en su descripción. El push y el merge son de David.
