# Tasks

Ninguna prueba existente cambia (punto 6 del gate). Al cerrar cada grupo, `git diff --diff-filter=M --name-only 07a2329 -- tests/` sale vacío; si una prueba existente falla, se para y se le pregunta a David. Cada prueba nueva se ve fallar antes de implementar lo que la hace pasar, y su `DisplayName` empieza con la capability y el nombre del escenario («atencion-puesto · Enter envía el código»). Las de `Kiosk.Tests` y la E2E corren con `INCOMEDOR_SQLSERVER` apuntando a la instancia del equipo, como pide `AGENTS.md`.

## 1. Enter en el campo del código

- [ ] 1.1 Agregar a `Kiosk.Tests` el archivo nuevo `EnterEnElCodigoTests.cs` con «Enter envía el código» y «Enter rechaza igual que «Ingresar» y una sola vez» (un solo `IDENTIFICATION_REJECTED` nuevo, sin `7777` en la bitácora ni en la pantalla), y verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~EnterEnElCodigo"`.
- [ ] 1.2 Sumar `@onkeydown` al `<input id="codigo">` de `PantallaAtencion.razor`, con Enter llamando a `Ingresar()` (D8), y verificar con el mismo filtro que las dos pasan y que `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` no tiene fallas.

## 2. Identificar por la credencial elegida

- [ ] 2.1 Agregar a `Infrastructure.Sqlite.Tests` el archivo nuevo `IdentificacionPorCredencialTests.cs`, sobre un SQLite real temporal con la semilla de la atención, y verlas fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~IdentificacionPorCredencial"`. Son tres:
  - la credencial de `0001` abre una sesión `CONSUMPTION` con método `CODE` y un `SESSION_OPENED` con `Origin = "SIMULATION_LIST"`;
  - la de `0002` da `CREDENTIAL_EXPIRED` sin sesión, con `Origin` en el rechazo;
  - un GUID que no está en el snapshot activo da `IDENTITY_NOT_RESOLVED` con `UNKNOWN_CODE` y sin `PersonId`.
- [ ] 2.2 Implementar `IdentificacionPorCodigo.IdentificarPorCredencial`, compartiendo con `Identificar` la clasificación, la sesión y el registro, con la clave `Origin` sólo en este camino (D2, D7). Verificar con el filtro de 2.1 que las tres pasan, y con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` que las de la identificación tipeada siguen en verde sin cambios.
- [ ] 2.3 Agregar a `IdentificacionPorCredencialTests.cs` la prueba de la lista: ocho entradas, en orden alfabético `es-AR`, con valores que son GUID de credenciales y textos sin ninguno de los ocho códigos. Verla fallar, implementar `ListarParaSimulacion()` (D3) y verla pasar con el mismo filtro.

## 3. La máquina identifica por la credencial

- [ ] 3.1 Agregar a `Kiosk.Tests` el archivo nuevo `MaquinaSimulacionTests.cs` y verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~MaquinaSimulacion"`. Son dos:
  - la credencial de `0001` lleva a la selección con su nombre;
  - durante una atención, otra credencial da «Ocupado» sin cambiar la persona ni la opción.
- [ ] 3.2 Implementar `MaquinaAtencion.IdentificarPorCredencial` con las guardas y el después de `Identificar`, sin duplicarlos (D2), y verificar con el filtro de 3.1 que pasan.

## 4. El modo simulación y su almacén

- [ ] 4.1 Agregar a `Kiosk.Tests` el archivo nuevo `AlmacenSimulacionTests.cs` y verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~AlmacenSimulacion"`. Son tres:
  - sin archivo, ilegible o con otro valor, está apagada;
  - guardar encendida y leer da encendida;
  - una escritura que falla deja el archivo anterior y lanza la excepción a quien llama.
- [ ] 4.2 Implementar `AlmacenSimulacion` (`simulacion.json`, escritura atómica, D4), registrarlo en `MainWindow` junto a `AlmacenModoPuesto`, y verificar con el filtro de 4.1 que pasan y con `dotnet build INComedor.sln -warnaserror` que compila sin advertencias.

## 5. El interruptor en General

- [ ] 5.1 Agregar a `Kiosk.Tests` el archivo nuevo `ModoSimulacionTests.cs`, con los cinco escenarios de «Modo simulación sólo en mantenimiento» y los tres de «El modo simulación queda en la bitácora general». Verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~ModoSimulacion"`. Entre ellas van:
  - el orden de los dos `CONFIGURATION_CHANGED` al pasar a operativo, los dos con `DAILY_KEY`;
  - el arranque en operativo con el archivo encendido, que lo deja guardado apagado;
  - la carpeta sin escritura, con `#aviso-simulacion` y `Result = "FAILED"`.
- [ ] 5.2 Implementar en `PanelConfiguracion.razor` y en `PantallaAtencion.razor` el interruptor, la regla efectiva y su registro (D5, D6, D7), con `MensajesAtencion.SimulacionNoGuardada`. Verificar con el filtro de 5.1 que las ocho pasan, y con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` que `AccesoPuestoTests` y `PestanasConfiguracionTests` siguen en verde sin cambios.

## 6. El combo en la pantalla de identificación

- [ ] 6.1 Agregar a `Kiosk.Tests` el archivo nuevo `ComboSimulacionTests.cs` con los seis escenarios de «Identificación eligiendo a la persona en modo simulación», más una prueba de borde: un valor que no es un GUID se ignora sin identificar. Verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~ComboSimulacion"`.
- [ ] 6.2 Agregar a `E2E.Tests` el archivo nuevo `SimulacionE2ETests.cs`. Sobre el puesto compilado: encender la simulación en General, cerrar Configuración, elegir en el combo a la persona de `0001` y llegar a «Elegí tu servicio de hoy» con su nombre, sin que el texto de la página contenga `0001`. Verla fallar con `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter "FullyQualifiedName~SimulacionE2E"`. Es la tarea que sale con el recorte del punto 1 del gate.
- [ ] 6.3 Implementar el combo (`<select id="simulacion-persona">` con su `<label>`, D9) y la lectura de la lista (D10) en `PantallaAtencion.razor`, y su alto táctil en `wwwroot/css/puesto.css`. Verificar con los filtros de 6.1 y 6.2 que pasan, y con `dotnet test INComedor.sln` que no hay ninguna falla.

## 7. Documentos y evidencia

- [ ] 7.1 Sumar a `AGENTS.md` y a `openspec/config.yaml` la línea de alcance del punto 8 del gate (D11), y verificar con `openspec validate --all --strict` que no hay errores.
- [ ] 7.2 Sumar al recorrido de verificación de `readme.md` cómo encender la simulación en General y elegir a una persona en el combo, sin nombrar ningún código como valor del combo.
- [ ] 7.3 Sumar la fila del change a `docs/aplicacion-del-curso.md`, con el nombre del change y sin enlace propio a su carpeta (D5 de `mostrar-aplicacion-del-curso`); sin entrada en `prompts.md`, porque no deja un workflow ni un ajuste humano nuevo. Verificar 7.2 y 7.3 con el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso` sobre `readme.md`, `AGENTS.md` y `docs/aplicacion-del-curso.md`, con su control negativo (un enlace roto a propósito que el chequeo tiene que marcar).

## 8. Verificación integral

- [ ] 8.1 Desde la raíz del repo, correr y pegar las salidas:
  - `dotnet build INComedor.sln -warnaserror`, con 0 advertencias;
  - `dotnet test INComedor.sln`, con 0 fallas;
  - `openspec validate --all --strict`, sin errores;
  - `git diff --diff-filter=M --name-only 07a2329 -- tests/`, vacío.
- [ ] 8.2 Publicar el puesto con `pwsh .github/scripts/publicar-puesto.ps1 -Version 0.0.0-local`, que corre la E2E contra el EXE, para que David lo pruebe desde `artifacts\publicacion\puesto`.

## Workflow follow-up

- La revisión de David (el gate de `proposal.md`) va antes del apply, y lo que decida queda escrito en `proposal.md`.
- Después de su prueba en el ejecutable, `/openspec-verify-change`, y el archivado en el mismo PR hacia `final-project-dw`, con la salida del verify en su descripción. El push y el merge son de David.
