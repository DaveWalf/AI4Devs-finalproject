# Tasks

Se aplica con `opus` · `xhigh`, supervisado. Es un plan diseñado con su verificador, pero tiene seguridad en alcance (un puerto de depuración y un workflow con permiso de escritura), y el apéndice de Claude Code manda para eso la línea de seguridad. Los comandos corren desde la raíz del repo y su salida se pega en la tarea. Al cerrar cada grupo hay un commit por rutas, en español, con `git commit -F <archivo>`.

Ninguna prueba existente se toca: `git diff --stat -- tests/` sólo puede mostrar archivos nuevos. Si una tarea necesitara cambiar una prueba existente, se para y se le pregunta a David. Tampoco se tocan `api/openapi.yaml`, `.github/workflows/ci.yml` ni las migraciones. Los chequeos de enlaces relativos y del mapa son los de D8 en `openspec/changes/archive/2026-10-29-mostrar-aplicacion-del-curso/design.md`. Nada de push, tag, Release ni video: son de David.

## 1. Precondiciones y dependencia

- [x] 1.1 Comprobar la línea de base:
  - `recibir-lotes-idempotente` archivado (`openspec list` sólo muestra este change e `imponer-invariantes-en-codigo`);
  - `openspec validate --all --strict` sin errores;
  - `dotnet test INComedor.sln` con 322 en verde, con `INCOMEDOR_SQLSERVER` en `INMO-DW-AR` en este equipo.

  Pegar las tres líneas finales.

  Hecho el 30/10 a las 12:48, sobre `9828599`:

  ```text
  demostrar-flujo-de-punta-a-punta     0/21 tasks    2m ago
  imponer-invariantes-en-codigo        17/19 tasks   17h ago
  Totals: 9 passed, 0 failed (9 items)
  total: 322 · error: 0 (Contracts 23, Domain 68, Sync.Api 62, Infrastructure.Sqlite 117, Kiosk 52)
  ```
- [x] 1.2 Agregar `Microsoft.Playwright` 1.64.1 a `Directory.Packages.props` y crear `tests/Intelektron.INComedor.E2E.Tests` en la solución (D5): xUnit v3, `net11.0-windows10.0.17763.0`, con `Microsoft.Data.SqlClient` y referencias al puesto y al receptor con `ReferenceOutputAssembly="false"`. Si un transitivo trae una vulnerabilidad, fijar la versión corregida como en `Kiosk.Tests`. Verificar con `./.github/scripts/auditar-dependencias.ps1` (exit 0), con `dotnet build INComedor.sln -warnaserror` (0 advertencias y 0 errores) y con `git diff --stat -- INComedor.sln`, que muestra sólo el proyecto nuevo. Pegar las salidas

  Hecho. `Microsoft.Data.SqlClient` 7.1.1 trae el mismo `Pkcs` 9.0.18 que en el change 5, así que el proyecto fija `System.Security.Cryptography.Pkcs` 11.0.0-rc.2, como `Kiosk.Tests`. La configuración de la prueba entra como `AssemblyMetadata` («Configuracion»), para buscar los EXE en `bin\<Configuracion>\`. `dotnet sln add` de .NET 11 suma también las referencias y aborta porque ya están en la solución; se agregó con `--include-references false`.

  ```text
  proyectos auditados: 11 de 11 · paquetes vulnerables: 0
  exit auditoría 0
  Compilación correcta.
      0 Advertencia(s)
      0 Errores
   INComedor.sln | 15 +++++++++++++++
   1 file changed, 15 insertions(+)        (un Project, sus 12 configuraciones y su carpeta «tests»)
  ```

  Hasta la 3.1 el proyecto no tiene pruebas, y `dotnet test` sobre él sale 8 («No tests ran»), medido.

## 2. El entorno de la demostración, sin ventana

- [x] 2.1 Crear `tests/Intelektron.INComedor.Kiosk.Tests/EntornoDemostracionTests.cs` (D1), un caso por escenario de los tres requirements de `modo-demostracion`:
  - la carpeta absoluta, sin la variable y relativa;
  - la dirección `https` en otro puerto, `http`, sin esquema y sin la variable;
  - el puerto en 1024, 9333 y 65535, sin la variable, `abc`, `80`, `1023`, `65536` y `70000`.

  También, que la carpeta de datos del WebView2 sólo se fija con `INCOMEDOR_DEMO_DIR`. La variable se lee de un diccionario, nunca del entorno del proceso. Verificar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` que no compilan antes de implementar, y pegar la salida

  Hecho: 8 pruebas, 27 casos. Además de los de la tarea, la carpeta suma `C:demo-relativa`, `\demo-relativa` y el valor vacío (relativas a la unidad o a la raíz, que no son absolutas); la dirección, `ftp://` y una ruta sin servidor; y el puerto, `-9333`, `+9333` y ` 9333`, que un `int.TryParse` común aceptaría con signo o espacios.

  ```text
  EntornoDemostracionTests.cs(116,20): error CS0246: El nombre del tipo o del espacio de nombres 'EntornoDemostracion' no se encontró
  exit 1
  ```
- [x] 2.2 Implementar `EntornoDemostracion` en `src/Intelektron.INComedor.Kiosk` (D1 y D3). Verificar con el mismo comando que pasan, y con `dotnet test INComedor.sln` que las 322 anteriores siguen en verde. Pegar los totales

  Hecho: un record con `Resolver(variable, datosLocales)`. La carpeta exige `Path.IsPathFullyQualified`, la dirección `https` absoluta y el puerto sólo dígitos (`NumberStyles.None`) entre 1024 y 65535.

  ```text
  Kiosk.Tests correcto [+79/x0/?0]            (52 anteriores + 27 casos nuevos)
  E2E.Tests No se ejecutaron pruebas [+0/x0/?0]
  Contracts.Tests 23 · Domain.Tests 68 · Sync.Api.Tests 62 · Infrastructure.Sqlite.Tests 117
    total: 349
    error: 0
    correcto: 349
  exit 0
  ```

## 3. Las costuras en la ventana real

- [x] 3.1 Escribir la preparación del E2E (D5 y D6):
  - las tres comprobaciones previas: WebView2 Runtime, SQL Server y sesión interactiva;
  - la carpeta temporal, los dos puertos y la base `INComedorE2E_<8 hex>`;
  - el arranque y el cierre por PID con su árbol;
  - el borrado de la base y de la carpeta en `finally`;
  - la foto de `%LOCALAPPDATA%\INComedor\demo\` antes y después;
  - la lectura de los puertos en escucha del árbol de procesos (`CreateToolhelp32Snapshot` y `GetExtendedTcpTable`).

  Control negativo: con `INCOMEDOR_SQLSERVER` en una instancia inexistente, la prueba falla con un mensaje que nombra SQL Server, y después de la corrida no queda ningún proceso del puesto ni del receptor. Pegar el mensaje y el conteo de procesos

  Hecho en `EscenarioE2E.cs` y `ProcesosYPuertos.cs`. Las tres comprobaciones se juntan en un solo mensaje que nombra cada falta y su remedio en la CI. Las variables `INCOMEDOR_*` y `WEBVIEW2_*` heredadas no pasan al puesto: la prueba fija las suyas. Cada cierre registra cuántos procesos tenía el árbol y cuántos quedaron vivos, y falla si queda alguno. Control negativo, con las 5 pruebas de la 3.2 y `INCOMEDOR_SQLSERVER=E2E-NO-EXISTE\NINGUNA` sólo en ese proceso:

  ```text
  System.InvalidOperationException : El E2E no puede correr en este entorno. Falta:
  - SQL Server: no hay conexión con «E2E-NO-EXISTE\NINGUNA» (INCOMEDOR_SQLSERVER, o LocalDB por defecto; en la CI, «sqllocaldb start MSSQLLocalDB» en ci.yml): Error relacionado con la red o específico de la instancia [...] (provider: Interfaces de red SQL, error: 26 - Error al buscar el servidor/instancia especificado)
  Intelektron.INComedor.E2E.Tests.dll (net11.0|x64) error con 5 errores [+0/x5/?0] (20s 832ms)
  procesos del puesto: 0 · del receptor: 0
  ```
- [x] 3.2 Escribir las pruebas de las costuras con la ventana real: «Puerto válido» (se conecta por CDP, ve `#codigo` y el puerto escucha sólo en loopback), «Sin la variable», y `abc`, `80` y `70000` (la ventana abre y ningún proceso del puesto escucha). Verificar con `dotnet test tests/Intelektron.INComedor.E2E.Tests` que «Puerto válido» falla contra el `MainWindow` de hoy, porque nada escucha, y pegar la salida

  Hecho en `CosturasDelPuestoTests.cs`. Sin CDP, «la ventana abre en reposo» se comprueba así: el puesto sigue vivo, tiene su ventana principal, su WebView2 arrancó y la pantalla de atención preparó su base en la carpeta de la prueba (`MaquinaAtencion.Iniciar`). Las escuchas del árbol se observan durante 5 s.

  **Cambio de orden, y por qué.** El `MainWindow` de hoy ignora `INCOMEDOR_DEMO_DIR` y habría creado `%LOCALAPPDATA%\INComedor\demo\`, que no existía y en este apply sólo se lee. Por eso el rojo se vio con la primera mitad de la 3.3 ya puesta (la carpeta, la dirección y `UserDataFolder`) y sin el puerto. Falla por la misma razón: nada escucha.

  ```text
  System.TimeoutException : CDP no respondió en 127.0.0.1:52582 en 60 s.
  Intelektron.INComedor.E2E.Tests.dll (net11.0|x64) error con 1 errores [+4/x1/?0] (1m 31s 014ms)
  total: 5 · error: 1 · correcto: 4
  carpeta demo existe: False · procesos del puesto vivos: 0
  registro de «Sin la variable»: ventana «INComedor · puesto de demostración» en reposo a los 1380 ms · 6 procesos de WebView2 · base preparada
                                 escuchas del árbol del puesto en 5 s: ninguna
                                 PID 15952 cerrado con su árbol: 7 procesos, vivos después: 0
  ```
- [x] 3.3 Aplicar el entorno en `MainWindow` (D2 y D4): la carpeta, la dirección del receptor, `UserDataFolder` en `<carpeta>\webview2` y el puerto por `BlazorWebViewInitializing`, este último sólo con un puerto válido. Verificar con `dotnet test tests/Intelektron.INComedor.E2E.Tests` que las pruebas de 3.2 pasan, y con `dotnet test INComedor.sln` que todo sigue en verde. Pegar los totales

  Hecho. Sin ninguna de las dos variables, `MainWindow` no asigna `BlazorWebViewInitializing`, y el WebView2 arranca como hoy. «Puerto válido» deja su captura `01-reposo-por-cdp.png`, y su registro es el control positivo de la lectura de escuchas: ve `127.0.0.1:<puerto>` en el proceso del navegador del WebView2, hijo del puesto, y nada más.

  Las pruebas de ausencia (`abc`, `80` y `70000`) pasaban antes de que existiera el puerto, así que se vieron fallar con una mutación: el parseo del spike, `int.TryParse` sin rango, en lugar de `entorno.PuertoCdp`. Después se restauró, con el hash comparado. Para que MSBuild recompilara se tocó la fecha del fuente, porque `Copy-Item` conserva la del respaldo, y sin eso corrieron los binarios mutados.

  ```text
  E2E.Tests correcto [+5/x0/?0] (35s 110ms)
  registro de «Puerto válido»: conectado por CDP a los 1164 ms · WebView2 156.0.4314.50
                               escuchas del árbol del puesto en 5 s: 127.0.0.1:6920 (PID 9540)
  mutación (int.TryParse sin rango): con errores [...] ningún proceso del puesto escucha(valor: "80")
      Assert.Empty() Failure: Collection was not empty
      Collection: [Escucha { Direccion = 127.0.0.1, Puerto = 80, Pid = 39616 }]
      [+2/x1/?0]
  restaurada: 8F7DEA33...C6BD7414 igual al respaldo

  Domain 68 · Contracts 23 · Sync.Api 62 · Infrastructure.Sqlite 117 · Kiosk 79 · E2E 5
    total: 354
    error: 0
    correcto: 354
  carpeta demo existe: False · procesos del puesto vivos: 0
  ```
- [x] 3.4 Correr el E2E con el puesto abierto en otra ventana, sin variables (`dotnet run --project src/Intelektron.INComedor.Kiosk`). Verificar que pasa y que la foto de `%LOCALAPPDATA%\INComedor\demo\` no cambió (D4). Cerrar el puesto por su PID. Pegar el resultado

  Hecho el 30/10 a las 13:12. El puesto abierto sin variables crea `%LOCALAPPDATA%\INComedor\demo\`, que antes no existía. David decidió en el apply correr la tarea y, como no existía, borrarla al final para dejarla como estaba. El puesto abierto usa la carpeta de datos del WebView2 de siempre, y cada prueba del E2E usa la suya, en `<carpeta>\webview2`.

  ```text
  carpeta demo antes de abrir el puesto: no existe
  dotnet run PID 35996 → Intelektron.INComedor.Kiosk.exe PID 37700, «INComedor · puesto de demostración»
  E2E.Tests correcto [+5/x0/?0] (34s 146ms) · total: 5 · error: 0
  foto propia antes y después del E2E: igual (\incomedor.db 253952 2026-10-30T16:12:25.6133343Z)
  registro de cada prueba: foto de %LOCALAPPDATA%\INComedor\demo\: igual (existe)   (5 de 5)
  árbol del dotnet run antes de cerrar: 35996, 36824, 37700, 40152 · vivos después: 0
  procesos del puesto o de su WebView2 que quedan: 0
  carpeta demo después de borrarla: no existe
  ```

## 4. El flujo principal de punta a punta

- [x] 4.1 Escribir la prueba del flujo principal con los siete pasos de D5, con una captura por paso en la carpeta de resultados de la prueba:
  - para «Almuerzo común», pasar de página con `#siguiente` si no está en la primera;
  - el mensaje de cupo, la franja y la consulta a `tbComedorConsumosConsolidados`, como dicen las specs vivas de `atencion-puesto` y `envio-lotes`.

  Verificar que pasa y verla fallar con una mutación: `MainWindow` ignorando `INCOMEDOR_RECEPTOR_URL` hace fallar el paso 6. Restaurar la mutación y pegar las dos salidas

  Hecho en `FlujoPrincipalTests.cs`. Los textos esperados son los de las specs vivas, escritos en la prueba, porque el E2E no carga el ensamblado del puesto. Para contar los consumos de la base del puesto (AT02), en sólo lectura y sin pool, el proyecto suma `Microsoft.Data.Sqlite`, que ya estaba en `Directory.Packages.props`: es una precisión de D5, que nombraba sólo Playwright y SqlClient. El receptor y el puesto tienen el único reintento por puertos de los Risks del design (`ArrancarReceptorAsync` y `AbrirPuestoAsync`). Las capturas `02-reposo` a `06-al-dia` quedan en `TestResults\e2e\<corrida>\`, junto al ensamblado de la prueba.

  ```text
  correcto [+1/x0/?0] (1m 04s 624ms)
  1. receptor preparado y apagado · 1,1 s
  2. puesto en reposo · 3,1 s
  3. consumo registrado y comprobante impreso · 3,6 s
  4. cupo: un solo consumo en el puesto, 7822462a-832c-4b01-9564-577deb87004e · 4,0 s
  5. franja: 2 pendientes · 37,6 s
  6. receptor de vuelta, franja al día · 63,5 s
  7. receptor: un consumo consolidado, 7822462a-832c-4b01-9564-577deb87004e · 63,5 s
  PID 32000 cerrado con su árbol: 1 procesos, vivos después: 0 · PID 20228 cerrado con su árbol: 7 procesos, vivos después: 0
  foto de %LOCALAPPDATA%\INComedor\demo\: igual (no existe)

  mutación (ClienteReceptorHttp con https://localhost:5001/ fijo):
  Microsoft.Playwright.PlaywrightException : Locator expected to contain text 'al día'
  92 × locator resolved to <section role="status" aria-live="polite" class="franja-sincronizacion" aria-label="Sincronización con el receptor">…</section>
  error con 1 errores [+0/x1/?0] (1m 23s 529ms)      (pasaron los pasos 1 a 5; falló el 6)
  restaurada con git restore a las 13:19:02 · DLL del puesto 13:19:03 · posterior: True
  ```
- [x] 4.2 Correr la prueba del flujo principal tres veces seguidas y pegar la duración de cada una. Si alguna pasa de 3 minutos o falla sin cambios en el código, parar: es una prueba inestable y se mira antes de seguir

  Hecho, sobre los binarios restaurados de la 4.1 y sin cambios entre corridas. Las tres duran lo mismo porque las esperas las marca el ciclo de envío, que arranca con el puesto: el paso 5 llega con el ciclo de los 30 s y el 6 con el de los 60.

  ```text
  corrida 1 · exit 0 · correcto [+1/x0/?0] (1m 03s 943ms) · reloj 64.7 s
  corrida 2 · exit 0 · correcto [+1/x0/?0] (1m 03s 789ms) · reloj 64.5 s
  corrida 3 · exit 0 · correcto [+1/x0/?0] (1m 04s 169ms) · reloj 64.9 s
  carpeta demo existe: False
  procesos del puesto vivos: 0
  ```

## 5. El paquete del puesto

- [x] 5.1 Escribir `.github/scripts/publicar-puesto.ps1 -Version <v>` (D7) y el modo paquete de la preparación: con `INCOMEDOR_E2E_KIOSK_EXE`, el puesto se lanza con un `PATH` sin carpetas de `dotnet` y con `DOTNET_ROOT` en una carpeta vacía. Agregar `artifacts/` al `.gitignore` si falta. Verificar con `git check-ignore artifacts/x` que se ignora

  Hecho. Además de lo que dice la tarea:
  - el script valida la versión (sólo letras, dígitos, punto y guion), porque en el workflow llega del nombre de un tag (OWASP A03);
  - revisa la publicación dos veces, antes del E2E y después, por si la corrida dejó algo junto al EXE, y una tercera vez las entradas del zip ya escrito;
  - en el modo paquete, la prueba comprueba que el `coreclr.dll` cargado por el proceso del puesto sale de la carpeta del EXE. Es una prueba más directa que el entorno de que el runtime es el del paquete.

  ```text
  .gitignore:4:artifacts/	artifacts/x
  exit 0
  dotnet build INComedor.sln -warnaserror: 0 Errores, exit 0
  ```
- [x] 5.2 Correr `./.github/scripts/publicar-puesto.ps1 -Version 0.0.0-local`. Verificar y pegar:
  - que el E2E corrió contra el EXE publicado;
  - el listado del zip, sin `.db`, `.pfx`, `.key`, `.pem`, `comprobantes` ni `certificados`;
  - el tamaño del zip;
  - que `Get-FileHash` coincide con el `.sha256`.

  Hecho el 30/10 a las 13:25, en 85 s, con el build en `Release` en 0 advertencias y 0 errores.

  ```text
  publicación revisada: 474 entradas, ninguna con datos ni secretos
  modo paquete: PATH sin 5 carpetas de dotnet (C:\Program Files\dotnet\, C:\Users\dwalfisch\.dotnet\tools, …) · DOTNET_ROOT=…\incomedor-e2e\8abe1f1f-sin-dotnet, vacía
  puesto PID 8048 · artifacts\publicacion\puesto\Intelektron.INComedor.Kiosk.exe · INCOMEDOR_CDP_PORT=19439
  pantalla de reposo en https://0.0.0.1/ a los 2415 ms
  modo paquete: coreclr.dll cargado desde artifacts\publicacion\puesto\coreclr.dll
  2. puesto en reposo · 5,5 s … 6. receptor de vuelta, franja al día · 65,9 s · 7. receptor: un consumo consolidado
  Intelektron.INComedor.E2E.Tests.dll (net11.0|x64) correcto [+1/x0/?0] (1m 07s 563ms)
  zip: artifacts\INComedor-Puesto-0.0.0-local-win-x64.zip · 76978066 bytes · sha256 aa05a1e63ebeb0d90a82a34657af5aa148edaa125e46d6b0b26b005af4de4074

  listado: 456 entradas (los 456 archivos de la publicación; sus 474 entradas de arriba suman además 18 carpetas); con .db/.pfx/.key/.pem o bajo comprobantes/certificados: 0
  raíz del zip: Accessibility.dll, clretwrc.dll, clrgc.dll, clrgcexp.dll, clrjit.dll, coreclr.dll, createdump.exe, D3DCompiler_47_cor3.dll, DirectWriteForwarder.dll, e_sqlite3.dll, hostfxr.dll, hostpolicy.dll …
  carpetas de primer nivel: cs, de, es, fr, it, ja, ko, pl, pt-BR, ru, runtimes, tr, wwwroot, zh-Hans, zh-Hant
  .sha256: aa05a1e63ebeb0d90a82a34657af5aa148edaa125e46d6b0b26b005af4de4074  INComedor-Puesto-0.0.0-local-win-x64.zip
  Get-FileHash: AA05A1E63EBEB0D90A82A34657AF5AA148EDAA125E46D6B0B26B005AF4DE4074 · coincide: True
  ```
- [x] 5.3 Controles negativos del script:
  - con un `.db` plantado en la carpeta de publicación, falla, nombra el archivo y no deja zip;
  - con `INCOMEDOR_SQLSERVER` en una instancia inexistente, el E2E falla y no queda zip.

  Pegar las dos salidas y `Test-Path` del zip en cada caso

  Hecho, cada control con su propia versión, para no pisar el zip de la 5.2. El `.db` se plantó en `src/Intelektron.INComedor.Kiosk/wwwroot/plantado.db`, que es por donde se colaría por error: `wwwroot` se publica entero, y `*.db` lo ignora git. Se borró al terminar.

  ```text
  .db plantado (-Version 0.0.0-control-db):
  script: La publicación trae datos o secretos: wwwroot\plantado.db.
  zip del control: False · suma: False · ¿corrió el E2E?: False · plantado en src: False

  SQL Server inexistente (-Version 0.0.0-control-sql, INCOMEDOR_SQLSERVER=E2E-NO-EXISTE\NINGUNA sólo en ese proceso):
  publicación revisada: 474 entradas, ninguna con datos ni secretos
  - SQL Server: no hay conexión con «E2E-NO-EXISTE\NINGUNA» (INCOMEDOR_SQLSERVER, o LocalDB por defecto; en la CI, «sqllocaldb start MSSQLLocalDB» en ci.yml): Error relacionado […]
  Intelektron.INComedor.E2E.Tests.dll (net11.0|x64) error con 1 errores [+0/x1/?0] (20s 834ms)
  script: El E2E contra el EXE publicado salió 2.
  zip del control: False · suma: False · procesos del puesto o del receptor vivos: 0
  en artifacts\ queda sólo INComedor-Puesto-0.0.0-local-win-x64.zip y su .sha256, de la 5.2
  ```

## 6. El workflow del Release

- [x] 6.1 Escribir `.github/workflows/release.yml` (D8): `push: tags: ['v*']`, `contents: read` arriba y `contents: write` sólo en el job, `windows-latest`, `actions/checkout@v7`, `actions/setup-dotnet@v6` con `global-json-file`, el script de 5.1 y `gh release create <tag> --draft` con el zip y su suma. Verificar:
  - que el YAML parsea;
  - con `rg -n "softprops|uses:" .github/workflows/release.yml`, que no hay acciones de terceros;
  - con `git diff --stat -- .github/workflows/ci.yml`, que `ci.yml` no cambió.

  Si `actionlint` no se puede correr (el 29/10 el clasificador de auto mode lo denegó), decirlo y dejarlo para David. Pegar las salidas

  Hecho. Antes del script van `Restore` y «Dependencias vulnerables», como en `ci.yml`, porque la auditoría necesita el restore. Además:
  - el clon va con `persist-credentials: false`, porque `gh` usa su propio token y nadie empuja desde el job;
  - el tag entra por `env:`, nunca interpolado en `run:` (OWASP A03);
  - `gh release create` lleva `--verify-tag`;
  - el job tiene un tope de 30 minutos.

  El YAML se parseó con la biblioteca `yaml` del CLI de OpenSpec, porque no hay PyYAML. Control: con la sangría rota, el mismo parser falla.

  ```text
  parsea: sí
  on: {"push":{"tags":["v*"]}}
  permissions arriba: {"contents":"read"}
  job paquete: runs-on windows-latest · permissions {"contents":"write"} · timeout 30
    - actions/checkout@v7 · actions/setup-dotnet@v6 · Restore · Dependencias vulnerables · Paquete probado · Release en borrador
  control con la sangría rota: errores: Flow sequence in block collection must be sufficiently indented and end with a ] at line 15, column 4
  rg -n "softprops|uses:" .github/workflows/release.yml
  21:      - uses: actions/checkout@v7
  26:      - uses: actions/setup-dotnet@v6
  git diff --stat -- .github/workflows/ci.yml: (vacío)
  ```

  `actionlint` no está instalado en este equipo (`Get-Command actionlint` no lo encuentra), y bajarlo es lo que el clasificador denegó el 29/10: no se intentó. Queda para David.

## 7. «Cómo revisar», `.env.example` y el guion del video

- [x] 7.1 Crear `.env.example` con las cuatro variables vacías y comentadas (D10). Verificar con gitleaks, con el script del job `secretos`, que no da hallazgos, y pegar la salida

  Hecho, y commiteado aparte en `794e9d3` para que el escaneo de la historia lo incluya. En esta sesión Git Bash no arranca (`add_item ... errno 1` de MSYS), y el remedio conocido mata por nombre los `bash.exe` de otras sesiones, que este apply tiene prohibido. Por eso el script del job se trasladó a PowerShell en el scratchpad, con el mismo rango, el mismo conteo de commits esperados (incluido el `NF == 3` del awk), las mismas opciones de `gitleaks git` y las mismas dos fallas.

  La primera corrida dio `77 commits scanned` de 82, con `~1.41 MB in 15.5s`, `no leaks found` y exit 0 de gitleaks. Fue una falla abierta, y la compuerta del conteo la atajó (exit 1). Cada uno de los 82 commits escaneado solo dio 1. El rango con el SHA de `HEAD` y tres corridas más del traslado, sin cambios, dieron 82 de 82:

  ```text
  gitleaks 8.30.1
  primera: commits escaneados: 77 · esperados: 82 · exit de gitleaks: 0 → ::error::gitleaks escaneó 77 commits y se esperaban 82.
  corrida 1 · exit 0 · commits escaneados: 82 · esperados: 82 · exit de gitleaks: 0 · scanned ~3143954 bytes (3.14 MB) in 847ms
  corrida 2 · exit 0 · commits escaneados: 82 · esperados: 82 · exit de gitleaks: 0 · scanned ~3143954 bytes (3.14 MB) in 872ms
  corrida 3 · exit 0 · commits escaneados: 82 · esperados: 82 · exit de gitleaks: 0 · scanned ~3143954 bytes (3.14 MB) in 855ms
  INF no leaks found
  ```
- [x] 7.2 Escribir «Cómo revisar» al principio del §1.4 del `readme.md` (D10): el Release, el código y el E2E; las credenciales de prueba, con enlace a la tabla de códigos; los datos sembrados; `.env.example`; «`docker-compose`: no aplica», con su motivo; y el enlace al video, que queda pendiente hasta que David lo suba. Poner al día el §2.6 con el E2E y los conteos de `dotnet test`. Verificar siguiendo el camino del código desde un clon limpio del repo en el scratchpad (`git clone` de la carpeta local, en esta rama), con los comandos tal cual los dice el README, y pegar la línea final de cada uno

  Hecho en `af84112`. También quedaron al día el estado del principio (355 pruebas), lo pendiente del §1.4 (el paquete existe; falta un instalador) y el §2.3, que contaba cuatro proyectos de pruebas cuando ya eran cinco y decía que el E2E no existía.

  **La verificación tuvo dos intentos fallidos, y se cuentan:**
  1. `git clone` de la carpeta local pasa por `sh.exe`, que en esta sesión muere con `add_item ... errno 1`. El script no cortaba ante el error, así que el `Set-Location` falló en silencio y el build y el test corrieron en el repo de trabajo. Los 355 de esa corrida no cuentan como verificación desde un clon.
  2. Se rehízo con `git archive` de la rama en una carpeta nueva del scratchpad: los mismos archivos versionados, sin `bin`, `obj` ni `artifacts`, y sin pasar por `sh`. El build pasó, pero 6 del E2E y 16 de `Kiosk.Tests` fallaron con `DllNotFoundException: Microsoft.Data.SqlClient.SNI.dll ... El nombre del archivo o la extensión es demasiado largo (0x800700CE)`. Las rutas del DLL medían 254 y 256 caracteres, en la ventana de 250 a 259 donde esa DLL no carga, medida el 04/10 en otro proyecto. En el repo de trabajo miden 166. La fragilidad es anterior al change (`Kiosk.Tests` ya la tenía), y queda en el informe.
  3. La misma extracción, con la carpeta montada en `Q:` con `subst` (sigue físicamente en el scratchpad, con rutas cortas), con los comandos del README:

  ```text
  == git archive de feature/demostrar-flujo-de-punta-a-punta-dw (af84112): 885295 bytes
  == carpeta: Q:\cl · bin u obj presentes: 0
  dotnet build INComedor.sln → 0 Advertencia(s) · 0 Errores · exit build 0
  dotnet test INComedor.sln → Contracts 23 · Domain 68 · Sync.Api 62 · Infrastructure.Sqlite 117 · Kiosk 79 · E2E 6 (1m 43s 223ms)
    total: 355 · error: 0 · correcto: 355 · exit test 0
  dotnet run --project src/Intelektron.INComedor.Kiosk (con $env:INCOMEDOR_DEMO_DIR, como sugiere el README)
    → puesto PID 41300 · ventana «INComedor · puesto de demostración» · base en la carpeta propia: True a los 3 s
    → carpeta demo del usuario existe: False · cerrado por el PID del dotnet run con su árbol: vivos 0
  ```

  `Q:` se desmontó y las copias se borraron al terminar.
- [x] 7.3 Escribir `docs/guion-video.md` (D9): los ocho pasos con su tiempo, 2 a 3 minutos en total, la herramienta de grabación y la carpeta propia con `INCOMEDOR_DEMO_DIR`. Verificar que los pasos suman entre 120 y 180 s, y pegar la suma

  Hecho. La carpeta propia lleva también `Receptor__CarpetaDemostracion` y `Receptor__BaseDemostracion`, para que el receptor del paso 7 deje sus certificados donde el puesto los busca y no use la base de demostración de siempre. Suma, leída de la tabla:

  ```text
  paso 1: 15 s · paso 2: 15 s · paso 3: 30 s · paso 4: 15 s · paso 5: 20 s · paso 6: 30 s · paso 7: 30 s · paso 8: 15 s
  pasos: 8 · suma: 170 s · entre 120 y 180: True
  total escrito: 170 s
  ```
- [x] 7.4 Sumar a `AGENTS.md` el comando del E2E y las tres variables. Verificar que el archivo no pasa de 80 líneas y que el chequeo de enlaces sobre `readme.md`, `AGENTS.md` y `docs/guion-video.md` da `enlaces rotos: 0`. Pegar las salidas

  Hecho, con el comando del paquete en la misma línea que las variables. El chequeo es el de D8, guardado tal cual en el scratchpad. Su control negativo es un `.md` con un archivo inexistente, un ancla falsa y un ancla válida, contra una copia del README.

  ```text
  líneas de AGENTS.md: 76
  enlaces rotos: 0
  control negativo:
  ROTO control.md:1 no-existe.md
  ANCLA control.md:2 readme.md#no-hay-tal-ancla
  enlaces rotos: 2
  ```

## 8. Verificación integrada y evidencia

- [x] 8.1 Desde la raíz, pegar la línea final de cada uno:
  - `dotnet build INComedor.sln -warnaserror`;
  - `dotnet test INComedor.sln`, con el total por proyecto;
  - `openspec validate --all --strict`;
  - gitleaks con el script del job `secretos`;
  - `git diff --stat 5843152 -- tests/`, que muestra sólo archivos nuevos;
  - `git diff --stat 5843152 -- api/ .github/workflows/ci.yml src/Intelektron.INComedor.Infrastructure.Sqlite/Migraciones src/Intelektron.INComedor.Sync.Api/Ingesta/Migraciones`, vacío.

  Hecho el 30/10 sobre `b320b83`. gitleaks, con el traslado a PowerShell del script del job (7.1), porque Git Bash sigue sin arrancar en esta sesión:

  ```text
  build: 0 Advertencia(s) · 0 Errores · exit 0
  Contracts.Tests 23 · Domain.Tests 68 · Sync.Api.Tests 62 · Infrastructure.Sqlite.Tests 117 · Kiosk.Tests 79 · E2E.Tests 6 (1m 39s 572ms)
    total: 355 · error: 0 · correcto: 355 · exit test 0
  Totals: 9 passed, 0 failed (9 items)
  commits escaneados: 84 · esperados: 84 · exit de gitleaks: 0 · INF no leaks found
  git diff --stat 5843152 -- tests/: 7 files changed, 1127 insertions(+), sólo archivos nuevos
    (CosturasDelPuestoTests.cs, EscenarioE2E.cs, FlujoPrincipalTests.cs, Intelektron.INComedor.E2E.Tests.csproj,
     ProcesosYPuertos.cs, Propiedades.cs y EntornoDemostracionTests.cs)
  git diff --stat --diff-filter=MDR 5843152 -- tests/: (vacío)
  git diff --stat 5843152 -- api/ .github/workflows/ci.yml …/Migraciones …/Ingesta/Migraciones: (vacío)
  ```
- [x] 8.2 Escribir en esta tarea la tabla de los escenarios de las dos specs contra la prueba que los cubre, y la revisión OWASP A01 a A10 de las tres variables, del puerto de depuración y del workflow. Verificar que cada escenario tiene una prueba o, en los del workflow, la razón por la que se verifica recién con el primer tag

  Cerrada a las 13:47:33. Verificación: los 16 escenarios tienen fila. Doce tienen prueba completa y cuatro son parciales, cada uno con su razón:
  - «Dirección sin TLS», sin prueba con la ventana de que ningún pedido llegue al puerto `http`;
  - «Prueba en verde», «Prueba en rojo» y «Push a una rama», que se confirman recién con el primer tag o el primer push de David.

  **Escenarios contra pruebas** (16 de 16):

  | Spec · escenario | Prueba o verificación | Cobertura |
  |---|---|---|
  | modo-demostracion · Carpeta elegida por la variable | `EntornoDemostracionTests.Con_una_ruta_absoluta_todo_va_a_esa_carpeta`; con la ventana real, `FlujoPrincipalTests` (la base con el consumo, el comprobante y, desde el verify, los datos del WebView2 en la carpeta de la prueba, y la foto de `%LOCALAPPDATA%\INComedor\demo\` igual) | completa |
  | modo-demostracion · Sin la variable | `EntornoDemostracionTests.Sin_la_variable_de_la_carpeta_usa_la_de_siempre` y, desde el verify, `Sin_las_variables_el_webview2_arranca_como_siempre` (sin variables, `MainWindow` no toca el arranque del WebView2); con la ventana real, la tarea 3.4 (el puesto sin variables creó su base en `%LOCALAPPDATA%\INComedor\demo\`) | completa; la parte con ventana, manual |
  | modo-demostracion · Ruta relativa | `EntornoDemostracionTests.Con_una_ruta_relativa_usa_la_carpeta_por_defecto` (`demo-relativa`, `C:demo-relativa`, `\demo-relativa` y vacía) | completa, sin ventana |
  | modo-demostracion · Dirección https elegida por la variable | `FlujoPrincipalTests`, pasos 6 y 7 (receptor en un puerto libre, franja al día y el consumo consolidado), visto fallar con la mutación de la 4.1; `EntornoDemostracionTests.Con_una_direccion_https_usa_esa_direccion` | completa |
  | modo-demostracion · Dirección sin TLS | `EntornoDemostracionTests.Sin_una_direccion_https_usa_la_de_siempre` (`http://`) | parcial: se prueba que el puesto resuelve `https://localhost:5001/`; que ningún pedido llegue al puerto `http` no tiene prueba con la ventana |
  | modo-demostracion · Valor que no es una dirección | `EntornoDemostracionTests.Sin_una_direccion_https_usa_la_de_siempre` (`localhost:5001`) | completa, sin ventana |
  | modo-demostracion · Puerto válido | `CosturasDelPuestoTests.Con_un_puerto_valido_se_conecta_por_cdp_y_escucha_solo_en_loopback`, visto fallar sin el puerto (3.2); `EntornoDemostracionTests` (1024, 9333 y 65535) | completa |
  | modo-demostracion · Sin la variable (puerto) | `CosturasDelPuestoTests.Sin_la_variable_ningun_proceso_del_puesto_escucha`; `EntornoDemostracionTests` (sin la variable) | completa |
  | modo-demostracion · Valor fuera de rango o que no es un número | `CosturasDelPuestoTests.Con_un_valor_invalido_ningun_proceso_del_puesto_escucha` (`abc`, `80` y `70000`), visto fallar con la mutación de la 3.3; `EntornoDemostracionTests` (diez valores) | completa |
  | distribucion-puesto · Abre sin .NET instalado | `FlujoPrincipalTests` en el modo paquete, desde `publicar-puesto.ps1` (5.2): `PATH` sin dotnet, `DOTNET_ROOT` vacía y `coreclr.dll` de la carpeta del paquete | completa |
  | distribucion-puesto · Sin datos ni secretos | `publicar-puesto.ps1`, que revisa la publicación dos veces y las entradas del zip; el listado de la 5.2 | completa |
  | distribucion-puesto · Un archivo de datos colado en la publicación | control de la 5.3 con `wwwroot\plantado.db` | completa |
  | distribucion-puesto · La suma acompaña al zip | 5.2: `Get-FileHash` igual al `.sha256` | completa |
  | distribucion-puesto · Prueba en verde | el script, en la 5.2; el borrador lo crea `release.yml` | parcial: el paso de `gh release create --draft` corre recién con el primer tag, porque sólo un tag `v*` dispara el workflow y empujarlo es de David |
  | distribucion-puesto · Prueba en rojo | control de la 5.3 con SQL Server inexistente: el script falla sin zip, y en el workflow el paso de `gh` no llega a correr | parcial: el script, en local; el workflow, con el primer tag que falle, por lo mismo |
  | distribucion-puesto · Push a una rama | `release.yml` sólo tiene `push: tags: ['v*']` (parseado en la 6.1), y `ci.yml`, que corre en `feature/**`, no tiene pasos de publicación ni de Release | por lectura; se confirma en el primer push de David, que no tiene que dejar ningún Release |

  **Elevación.** El requirement del puerto dice «esto vale también cuando el puesto corre elevado». Las corridas de este apply fueron todas sin elevar: el registro del E2E lo anota desde el verify («proceso de prueba elevado: no»), y `whoami /groups` da `Medium Mandatory Level`. La costura se ejerció igual, porque el E2E saca del entorno del puesto las variables `WEBVIEW2_*` y el puerto entra sólo por `INCOMEDOR_CDP_PORT`. El caso elevado lo midió el spike del 29/10, con la misma costura, en 4 corridas de 4; en este apply queda sin verificar.

  **OWASP A01 a A10**, sobre las tres variables, el puerto de depuración y el workflow:

  | Categoría | Revisión |
  |---|---|
  | A01 Control de acceso | Con el puerto de CDP abierto, cualquier proceso del equipo maneja la pantalla y lee lo que muestra. Por eso abre sólo con la variable, sólo en loopback (sin `--remote-debugging-address`, comprobado con la tabla de escuchas) y nunca por defecto. En el workflow, `contents: write` sólo en su job, y la escritura sólo crea un borrador; empujar un tag `v*` exige permiso de escritura en el repo. |
  | A02 Fallas criptográficas | `INCOMEDOR_RECEPTOR_URL` sólo admite `https`: ningún valor hace viajar un lote sin TLS. El TLS mutuo no cambia, y el cliente sigue validando el servidor contra la autoridad configurada, aunque la dirección sea otra. El zip va con su SHA-256, que da integridad pero no autoría, porque el EXE no se firma (non-goal). |
  | A03 Inyección | `INCOMEDOR_DEMO_DIR` sólo como ruta absoluta y normalizada con `GetFullPath`; nada se arma con texto de las variables en SQL ni en un shell. La versión del Release llega por `env:` y no interpolada en `run:`, y el script la valida (`^[0-9A-Za-z][0-9A-Za-z.\-]{0,63}$`), así que un tag no arma otra ruta. |
  | A04 Diseño inseguro | Las tres costuras son inertes sin su variable, y separadas (D3): aislar los datos no abre la depuración. El Release es un borrador probado antes de ofrecerse. |
  | A05 Configuración insegura | Por defecto, ningún puerto; un valor inválido vuelve al de siempre, nunca a un destino relativo ni a `http`. `.env.example` y el README marcan `INCOMEDOR_CDP_PORT` como sólo para pruebas. El workflow clona sin credenciales guardadas (`persist-credentials: false`) y tiene un tope de 30 minutos. |
  | A06 Componentes vulnerables | `Microsoft.Playwright` 1.64.1 y sus transitivos, sin vulnerables (`auditar-dependencias.ps1`, 11 de 11 proyectos); `Pkcs` fijado en el proyecto E2E como en `Kiosk.Tests`. El workflow corre la misma auditoría antes de empaquetar. |
  | A07 Autenticación | CDP no tiene autenticación: lo cubren el loopback y que sea opcional. Una dirección de receptor ajena no recibe lotes, porque el puesto sólo acepta un servidor emitido por la autoridad de la demostración. |
  | A08 Integridad del software | Sin acciones de terceros: sólo `actions/checkout@v7` y `actions/setup-dotnet@v6`, las de `ci.yml`. El zip es la carpeta que probó el E2E, con su suma, y `gh release create --verify-tag` exige que el tag exista. |
  | A09 Registro y monitoreo | El registro del E2E lleva puertos, PID, rutas y tiempos, nunca un código tipeado. El puesto no registra que abrió el puerto de depuración: queda como sugerencia, porque hoy no tiene bitácora propia de arranque. |
  | A10 SSRF | La dirección del receptor la fija quien controla el entorno del proceso, que ya controla el puesto; sólo `https` y sin redirecciones (`AllowAutoRedirect = false`, de `recibir-lotes-idempotente`). |
- [x] 8.3 Evidencia (D11):
  - las filas de `docs/aplicacion-del-curso.md`;
  - `docs/cobertura-entrega.md` y `docs/pruebas.md:46`;
  - el ajuste humano de la elevación en `prompts.md`;
  - las tallas y las horas reales del apply en `docs/plan-academico.md`.

  Hecho. En el mapa del curso pasan a `Aplicada` ocho filas:
  - el spike con tope;
  - el E2E vertical;
  - `.env.example`;
  - construir una vez y promover el mismo artefacto;
  - las tres de la L58 (el E2E, la evidencia, con la tercera opción, y el README);
  - y la del pipeline que publica, en la tabla de la devolución.

  En la cobertura, las cuatro filas de la acción 3 pasan a `Cubierto`, con el Release y el video como del autor. `prompts.md` suma el punto 12 del criterio humano. El plan, las tallas S de los dos y 1,0 h de apply. Los chequeos son los de D8, guardados tal cual en el scratchpad; el control del mapa le suma a una copia una fila con un estado inválido y una `Aplicada` sin enlace:

  ```text
  filas del mapa: 82 · defectuosas: 0
  control negativo: ESTADO INVÁLIDO: | Práctica falsa | lección 0 | Hecha | algo |
                    SIN EVIDENCIA: | Otra falsa | lección 0 | Aplicada | sin enlace |
                    filas del mapa: 83 · defectuosas: 2
  enlaces, sobre aplicacion-del-curso.md, cobertura-entrega.md, pruebas.md, plan-academico.md, prompts.md, readme.md,
  AGENTS.md, guion-video.md y los cinco archivos del change: enlaces rotos: 0
  ```

## Workflow follow-up

- David revisa el informe de `/openspec-verify-change`, el E2E y el paquete local, y decide el push y el PR.
- El primer push muestra si `windows-latest` corre el E2E (D6). Si falta algo, el paso en `ci.yml` es de David.
- Lo que `ci.yml` necesite en `windows-latest` (por ejemplo `sqllocaldb start MSSQLLocalDB`) le va a faltar también a `release.yml`, que corre el mismo E2E. Antes del primer tag real conviene ensayar el workflow con un tag descartable, por ejemplo `v0.0.0-ensayo`, y borrar después el tag y su borrador (WARNING 3 del verify). El ensayo y el tag son de David.
- Después del SDK estable del 10/11 (D-C25), David empuja el tag, revisa el borrador, le suma el video y lo publica. El enlace del video en el README se completa entonces.
- El change se archiva en su PR, con los enlaces a su carpeta pasados a `archive/`.
