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
- [ ] 2.2 Implementar `EntornoDemostracion` en `src/Intelektron.INComedor.Kiosk` (D1 y D3). Verificar con el mismo comando que pasan, y con `dotnet test INComedor.sln` que las 322 anteriores siguen en verde. Pegar los totales

## 3. Las costuras en la ventana real

- [ ] 3.1 Escribir la preparación del E2E (D5 y D6):
  - las tres comprobaciones previas: WebView2 Runtime, SQL Server y sesión interactiva;
  - la carpeta temporal, los dos puertos y la base `INComedorE2E_<8 hex>`;
  - el arranque y el cierre por PID con su árbol;
  - el borrado de la base y de la carpeta en `finally`;
  - la foto de `%LOCALAPPDATA%\INComedor\demo\` antes y después;
  - la lectura de los puertos en escucha del árbol de procesos (`CreateToolhelp32Snapshot` y `GetExtendedTcpTable`).

  Control negativo: con `INCOMEDOR_SQLSERVER` en una instancia inexistente, la prueba falla con un mensaje que nombra SQL Server, y después de la corrida no queda ningún proceso del puesto ni del receptor. Pegar el mensaje y el conteo de procesos
- [ ] 3.2 Escribir las pruebas de las costuras con la ventana real: «Puerto válido» (se conecta por CDP, ve `#codigo` y el puerto escucha sólo en loopback), «Sin la variable», y `abc`, `80` y `70000` (la ventana abre y ningún proceso del puesto escucha). Verificar con `dotnet test tests/Intelektron.INComedor.E2E.Tests` que «Puerto válido» falla contra el `MainWindow` de hoy, porque nada escucha, y pegar la salida
- [ ] 3.3 Aplicar el entorno en `MainWindow` (D2 y D4): la carpeta, la dirección del receptor, `UserDataFolder` en `<carpeta>\webview2` y el puerto por `BlazorWebViewInitializing`, este último sólo con un puerto válido. Verificar con `dotnet test tests/Intelektron.INComedor.E2E.Tests` que las pruebas de 3.2 pasan, y con `dotnet test INComedor.sln` que todo sigue en verde. Pegar los totales
- [ ] 3.4 Correr el E2E con el puesto abierto en otra ventana, sin variables (`dotnet run --project src/Intelektron.INComedor.Kiosk`). Verificar que pasa y que la foto de `%LOCALAPPDATA%\INComedor\demo\` no cambió (D4). Cerrar el puesto por su PID. Pegar el resultado

## 4. El flujo principal de punta a punta

- [ ] 4.1 Escribir la prueba del flujo principal con los siete pasos de D5, con una captura por paso en la carpeta de resultados de la prueba:
  - para «Almuerzo común», pasar de página con `#siguiente` si no está en la primera;
  - el mensaje de cupo, la franja y la consulta a `tbComedorConsumosConsolidados`, como dicen las specs vivas de `atencion-puesto` y `envio-lotes`.

  Verificar que pasa y verla fallar con una mutación: `MainWindow` ignorando `INCOMEDOR_RECEPTOR_URL` hace fallar el paso 6. Restaurar la mutación y pegar las dos salidas
- [ ] 4.2 Correr la prueba del flujo principal tres veces seguidas y pegar la duración de cada una. Si alguna pasa de 3 minutos o falla sin cambios en el código, parar: es una prueba inestable y se mira antes de seguir

## 5. El paquete del puesto

- [ ] 5.1 Escribir `.github/scripts/publicar-puesto.ps1 -Version <v>` (D7) y el modo paquete de la preparación: con `INCOMEDOR_E2E_KIOSK_EXE`, el puesto se lanza con un `PATH` sin carpetas de `dotnet` y con `DOTNET_ROOT` en una carpeta vacía. Agregar `artifacts/` al `.gitignore` si falta. Verificar con `git check-ignore artifacts/x` que se ignora
- [ ] 5.2 Correr `./.github/scripts/publicar-puesto.ps1 -Version 0.0.0-local`. Verificar y pegar:
  - que el E2E corrió contra el EXE publicado;
  - el listado del zip, sin `.db`, `.pfx`, `.key`, `.pem`, `comprobantes` ni `certificados`;
  - el tamaño del zip;
  - que `Get-FileHash` coincide con el `.sha256`.
- [ ] 5.3 Controles negativos del script:
  - con un `.db` plantado en la carpeta de publicación, falla, nombra el archivo y no deja zip;
  - con `INCOMEDOR_SQLSERVER` en una instancia inexistente, el E2E falla y no queda zip.

  Pegar las dos salidas y `Test-Path` del zip en cada caso

## 6. El workflow del Release

- [ ] 6.1 Escribir `.github/workflows/release.yml` (D8): `push: tags: ['v*']`, `contents: read` arriba y `contents: write` sólo en el job, `windows-latest`, `actions/checkout@v7`, `actions/setup-dotnet@v6` con `global-json-file`, el script de 5.1 y `gh release create <tag> --draft` con el zip y su suma. Verificar:
  - que el YAML parsea;
  - con `rg -n "softprops|uses:" .github/workflows/release.yml`, que no hay acciones de terceros;
  - con `git diff --stat -- .github/workflows/ci.yml`, que `ci.yml` no cambió.

  Si `actionlint` no se puede correr (el 29/10 el clasificador de auto mode lo denegó), decirlo y dejarlo para David. Pegar las salidas

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
