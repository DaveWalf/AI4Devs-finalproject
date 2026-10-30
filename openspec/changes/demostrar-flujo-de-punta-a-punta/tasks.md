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

- [ ] 2.1 Crear `tests/Intelektron.INComedor.Kiosk.Tests/EntornoDemostracionTests.cs` (D1), un caso por escenario de los tres requirements de `modo-demostracion`:
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

  listado: 456 entradas; con .db/.pfx/.key/.pem o bajo comprobantes/certificados: 0
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

- [ ] 7.1 Crear `.env.example` con las cuatro variables vacías y comentadas (D10). Verificar con gitleaks, con el script del job `secretos`, que no da hallazgos, y pegar la salida
- [ ] 7.2 Escribir «Cómo revisar» al principio del §1.4 del `readme.md` (D10): el Release, el código y el E2E; las credenciales de prueba, con enlace a la tabla de códigos; los datos sembrados; `.env.example`; «`docker-compose`: no aplica», con su motivo; y el enlace al video, que queda pendiente hasta que David lo suba. Poner al día el §2.6 con el E2E y los conteos de `dotnet test`. Verificar siguiendo el camino del código desde un clon limpio del repo en el scratchpad (`git clone` de la carpeta local, en esta rama), con los comandos tal cual los dice el README, y pegar la línea final de cada uno
- [ ] 7.3 Escribir `docs/guion-video.md` (D9): los ocho pasos con su tiempo, 2 a 3 minutos en total, la herramienta de grabación y la carpeta propia con `INCOMEDOR_DEMO_DIR`. Verificar que los pasos suman entre 120 y 180 s, y pegar la suma
- [ ] 7.4 Sumar a `AGENTS.md` el comando del E2E y las tres variables. Verificar que el archivo no pasa de 80 líneas y que el chequeo de enlaces sobre `readme.md`, `AGENTS.md` y `docs/guion-video.md` da `enlaces rotos: 0`. Pegar las salidas

## 8. Verificación integrada y evidencia

- [ ] 8.1 Desde la raíz, pegar la línea final de cada uno:
  - `dotnet build INComedor.sln -warnaserror`;
  - `dotnet test INComedor.sln`, con el total por proyecto;
  - `openspec validate --all --strict`;
  - gitleaks con el script del job `secretos`;
  - `git diff --stat 5843152 -- tests/`, que muestra sólo archivos nuevos;
  - `git diff --stat 5843152 -- api/ .github/workflows/ci.yml src/Intelektron.INComedor.Infrastructure.Sqlite/Migraciones src/Intelektron.INComedor.Sync.Api/Ingesta/Migraciones`, vacío.
- [ ] 8.2 Escribir en esta tarea la tabla de los escenarios de las dos specs contra la prueba que los cubre, y la revisión OWASP A01 a A10 de las tres variables, del puerto de depuración y del workflow. Verificar que cada escenario tiene una prueba o, en los del workflow, la razón por la que se verifica recién con el primer tag
- [ ] 8.3 Evidencia (D11):
  - las filas de `docs/aplicacion-del-curso.md`;
  - `docs/cobertura-entrega.md` y `docs/pruebas.md:46`;
  - el ajuste humano de la elevación en `prompts.md`;
  - las tallas y las horas reales del apply en `docs/plan-academico.md`.

  Verificar con el chequeo del mapa y con el de enlaces sobre esos cuatro, `readme.md` y el change: `enlaces rotos: 0`. Pegar las salidas

## Workflow follow-up

- David revisa el informe de `/openspec-verify-change`, el E2E y el paquete local, y decide el push y el PR.
- El primer push muestra si `windows-latest` corre el E2E (D6). Si falta algo, el paso en `ci.yml` es de David.
- Después del SDK estable del 10/11 (D-C25), David empuja el tag, revisa el borrador, le suma el video y lo publica. El enlace del video en el README se completa entonces.
- El change se archiva en su PR, con los enlaces a su carpeta pasados a `archive/`.
