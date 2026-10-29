# Tasks

Se aplica con `opus` · `medium`, porque toca configuración compartida (la CI y los hooks) y sus decisiones ya están en el design. Los chequeos son los de design D11; corren desde la raíz del repo, o desde un clon descartable en el scratchpad cuando el control rompe algo, y su salida se pega en la tarea. Al cerrar cada grupo hay un commit por rutas, en español, con `git commit -F <archivo>`. Ninguna tarea toca `src/` ni `tests/` del repo, y ninguna modifica un test existente. Las tareas marcadas «David» son suyas: el agente prepara y espera.

## 1. Hook en el repo

- [x] 1.1 Crear `.claude/hooks/confirmar-edicion-de-test.ps1` con el texto de design D6, en UTF-8 con BOM; verificar con el chequeo de BOM de D11, que debe imprimir `EF BB BF`

  ```text
  EF BB BF
  ```

- [x] 1.2 Crear `.claude/settings.json` con el JSON de D6; verificar con `pwsh -NoProfile -Command "(Get-Content .claude/settings.json -Raw | ConvertFrom-Json).hooks.PreToolUse.matcher"`, que debe imprimir `Edit|Write|MultiEdit|NotebookEdit`

  ```text
  Edit|Write|MultiEdit|NotebookEdit
  ```

- [x] 1.3 Correr el probador de D11 sobre el repo con `powershell.exe` y con `pwsh`; verificar que las siete líneas dan lo esperado en los dos motores (tres `ask`, cuatro sin salida, «aprobación» bien escrito) y pegar las catorce

  ```text
  --- powershell.exe
  Edit de test existente      exit=0  ask | Test existente: C:\AI\INComedor\INComedor-AI4DEVS\tests\Intelektron.INComedor.Domain.Tests\ValueObjectsTests.cs. AGENTS.md pide la aprobación explícita de David para modificarlo.
  Write de test nuevo         exit=0  (sin salida: sigue el flujo normal)
  Write sobre test existente  exit=0  ask | Test existente: C:\AI\INComedor\INComedor-AI4DEVS\tests\Intelektron.INComedor.Domain.Tests\ValueObjectsTests.cs. AGENTS.md pide la aprobación explícita de David para modificarlo.
  Edit fuera de tests         exit=0  (sin salida: sigue el flujo normal)
  Carpeta parecida tests2     exit=0  (sin salida: sigue el flujo normal)
  Ruta con ..                 exit=0  ask | Test existente: C:\AI\INComedor\INComedor-AI4DEVS\tests\Intelektron.INComedor.Domain.Tests\ValueObjectsTests.cs. AGENTS.md pide la aprobación explícita de David para modificarlo.
  Sin file_path               exit=0  (sin salida: sigue el flujo normal)
  --- pwsh
  Edit de test existente      exit=0  ask | Test existente: C:\AI\INComedor\INComedor-AI4DEVS\tests\Intelektron.INComedor.Domain.Tests\ValueObjectsTests.cs. AGENTS.md pide la aprobación explícita de David para modificarlo.
  Write de test nuevo         exit=0  (sin salida: sigue el flujo normal)
  Write sobre test existente  exit=0  ask | Test existente: C:\AI\INComedor\INComedor-AI4DEVS\tests\Intelektron.INComedor.Domain.Tests\ValueObjectsTests.cs. AGENTS.md pide la aprobación explícita de David para modificarlo.
  Edit fuera de tests         exit=0  (sin salida: sigue el flujo normal)
  Carpeta parecida tests2     exit=0  (sin salida: sigue el flujo normal)
  Ruta con ..                 exit=0  ask | Test existente: C:\AI\INComedor\INComedor-AI4DEVS\tests\Intelektron.INComedor.Domain.Tests\ValueObjectsTests.cs. AGENTS.md pide la aprobación explícita de David para modificarlo.
  Sin file_path               exit=0  (sin salida: sigue el flujo normal)
  ```

- [x] 1.4 Commit por rutas de los dos archivos. Después, en un clon descartable de ese commit, correr las ediciones reales A, B, C y D de D11 con `claude -p`; verificar que A da una denegación con el motivo del hook y el test intacto, que B y C pasan y que D pasa sin el hook, y pegar `permission_denials` y `git status --short` de cada una

  Commit `12cbb0e`. Clon de ese commit en el scratchpad, Claude Code 2.1.312, `claude -p "<pedido>" --model haiku --permission-mode acceptEdits --output-format json`, en orden, sin limpiar entre A, B y C:

  ```text
  === A (Edit de tests/…/ValueObjectsTests.cs)
  permission_denials: [{"tool_name": "Edit", "tool_input": {"file_path": "…\\clon\\tests\\Intelektron.INComedor.Domain.Tests\\ValueObjectsTests.cs", "old_string": "using Intelektron.INComedor.Domain;\n", "new_string": "// prueba del hook\nusing Intelektron.INComedor.Domain;\n", …}}]
  result: La lectura de `ValueObjectsTests.cs` salió bien, pero la edición fue denegada y el archivo no cambió. El motivo que recibí: `Test existente: …\clon\tests\Intelektron.INComedor.Domain.Tests\ValueObjectsTests.cs. AGENTS.md pide la aprobación explícita de David para modificarlo.`
  git status --short:
  (vacío)
  === B (Write de tests/…/HookNuevoTests.cs)
  permission_denials: []
  git status --short:
  ?? tests/Intelektron.INComedor.Domain.Tests/HookNuevoTests.cs
  === C (Edit de src/Intelektron.INComedor.Domain/FechaNegocio.cs)
  permission_denials: []
  git status --short:
   M src/Intelektron.INComedor.Domain/FechaNegocio.cs
  ?? tests/Intelektron.INComedor.Domain.Tests/HookNuevoTests.cs
  (src y el test nuevo restaurados; control D: .claude/settings.json renombrado)
  === D (el pedido de A, sin el hook)
  permission_denials: []
  git status --short:
   D .claude/settings.json
   M tests/Intelektron.INComedor.Domain.Tests/ValueObjectsTests.cs
  ?? .claude/settings.json.off
  === restaurado: git status --short vacío
  ```

## 2. Puntero local

- [x] 2.1 Comprobar que `C:\AI\INComedor\.claude\settings.json` no existe y crearlo con el JSON de D7; verificar que parsea y que su ruta apunta al script, con `Test-Path` sobre la ruta del `command`, que debe dar `True`

  ```text
  settings.json no existe          (antes de crearlo; en C:\AI\INComedor\.claude sólo estaba skills\)
  matcher: Edit|Write|MultiEdit|NotebookEdit
  command: powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:/AI/INComedor/INComedor-AI4DEVS/.claude/hooks/confirmar-edicion-de-test.ps1"
  ruta: C:/AI/INComedor/INComedor-AI4DEVS/.claude/hooks/confirmar-edicion-de-test.ps1
  Test-Path: True
  ```
- [ ] 2.2 David: abrir una sesión nueva de Claude Code en `C:\AI\INComedor` y pedirle que edite `tests/Intelektron.INComedor.Domain.Tests/ValueObjectsTests.cs`; verificar que aparece el pedido de confirmación con `[settings]` y el motivo del hook, rechazarlo y pegar el texto del pedido

## 3. CI: scripts, controles locales y workflow

- [x] 3.1 Crear `.github/scripts/auditar-dependencias.ps1` con el texto de D3 y correrlo desde la raíz; verificar `proyectos auditados: 9 de 9 · paquetes vulnerables: 0` y exit 0. En un clon descartable, agregar `Newtonsoft.Json.Bson` 1.0.2 por `Directory.Packages.props` y `Directory.Build.props` (D9): debe dar exit 1 con la línea `::error::` de `Newtonsoft.Json 12.0.1`. Con una versión inexistente, debe dar exit 1 por `problems`. Pegar las tres salidas

  ```text
  === repo, desde la raíz
  proyectos auditados: 9 de 9 · paquetes vulnerables: 0
  exit=0
  === clon con Newtonsoft.Json.Bson 1.0.2 (restore exit=0)
  proyectos auditados: 9 de 9 · paquetes vulnerables: 9
  ::error::Intelektron.INComedor.Domain.csproj · Newtonsoft.Json 12.0.1 · High · https://github.com/advisories/GHSA-5crp-9r3c-p9vr
  … (la misma línea para los otros ocho proyectos: el ItemGroup de Directory.Build.props alcanza a todos)
  exit=1
  === clon con Newtonsoft.Json.Bson 99.99.99 (restore exit=1)
     "problems": [
           "text": "Error en la restauración. Ejecute "dotnet restore" para obtener más detalles sobre el problema.",
           "level": "error"
       | dotnet list package salió 1.
  exit=1
  ```

  Con el SDK de `global.json`, una restauración rota hace que `dotnet list` salga 1 y el JSON traiga `problems`: corta la primera compuerta de D3, no la segunda. El resultado es el que pide la tarea (exit 1, con `problems` a la vista) y el design no cambia.

- [x] 3.2 Crear `.github/scripts/escanear-secretos.sh` con el texto de D4 y correrlo desde la raíz con el gitleaks local (`gitleaks version` debe decir `8.30.1`); verificar que escaneados es igual a esperados y que sale con exit 0. En un clon descartable, correr los cuatro controles de D4 (secreto sintético, stub, base inexistente, `.gitleaksignore`), que deben salir distinto de 0. Pegar la tabla con las cinco salidas

  `gitleaks version`: `8.30.1`. Desde la raíz: `37 commits scanned`, `no leaks found`, `commits escaneados: 37 · esperados: 37 · exit de gitleaks: 0`, exit 0. Controles en un clon de `f75ec9d` que no se empuja:

  | Caso | Salida | Exit |
  |---|---|---|
  | Historia propia, limpia | `commits escaneados: 37 · esperados: 37 · exit de gitleaks: 0` | 0 |
  | Stub que imprime `0 commits scanned` y sale 0 | `::error::gitleaks escaneó 0 commits y se esperaban 37.` | 1 |
  | Base inexistente | `fatal: Invalid revision range 0123456789abcdef0123456789abcdef01234567..HEAD` | 128 |
  | Con un `.gitleaksignore` | `::error::Hay un .gitleaksignore: silencia hallazgos sin pasar por la revisión del workflow.` | 1 |
  | Commit con un `api_key` sintético | `leaks found: 1` · `commits escaneados: 38 · esperados: 38 · exit de gitleaks: 1` · `::error::gitleaks encontró secretos o falló (exit 1).` | 1 |

- [x] 3.3 En un clon descartable, quitar los escenarios del requirement de `openspec/specs/sincronizacion-lotes/spec.md` y correr `openspec validate --all --strict`: debe dar `ERROR` y exit 1; restaurado, exit 0. Pegar las dos salidas

  ```text
  === sin escenarios (0 escenarios)
  ✗ spec/sincronizacion-lotes
    ✗ [ERROR] requirements.0.scenarios: Requirement must have at least one scenario
  Totals: 3 passed, 1 failed (4 items)
  exit=1
  === restaurado (4 escenarios)
  ✓ spec/modo-demostracion
  ✓ spec/registro-consumo
  ✓ spec/sincronizacion-lotes
  Totals: 4 passed, 0 failed (4 items)
  exit=0
  ```

- [x] 3.4 Reemplazar `.github/workflows/ci.yml` por el YAML de D1; verificar parseándolo con la librería `yaml` de OpenSpec (`npm root -g`, `@fission-ai/openspec/node_modules/yaml`), que debe listar `build-test`, `specs` y `secretos` con los pasos de D1, y pegar la salida

  ```text
  build-test (windows-latest):
    - actions/checkout@v7
    - actions/setup-dotnet@v6
    - Restore
    - Dependencias vulnerables
    - Build
    - Test
  specs (ubuntu-latest):
    - actions/checkout@v7
    - actions/setup-node@v7
    - Instalar OpenSpec
    - openspec validate
  secretos (ubuntu-latest):
    - actions/checkout@v7
    - Instalar gitleaks
    - Secretos
  exit=0
  ```

- [ ] 3.5 Pedirle permiso a David para bajar `actionlint` 1.7.12 al scratchpad, verificado con el SHA-256 de su `checksums.txt`, y correrlo con `-shellcheck= -pyflakes=` sobre `ci.yml`: debe salir sin hallazgos. Si David no lo autoriza, anotar en la tarea que no corrió y por qué

  No corrió. David lo autorizó el 29/10 (decisión 3 del plan del goal). La descarga y el hash pasaron: `actionlint_1.7.12_windows_amd64.zip`, `6e7241b51e6817ea6a047693d8e6fed13b31819c9a0dd6c5a726e1592d22f6e9` en `checksums.txt` y en `sha256sum`. Ejecutarlo lo denegó el clasificador de auto mode de Claude Code, y no se reintentó por otra vía:

  ```text
  Permission for this action was denied by the Claude Code auto mode classifier. Reason: [Code from External].
  ```

  Queda para David: correrlo él desde la raíz del repo, con el binario verificado.

- [x] 3.6 Correr `dotnet build INComedor.sln -warnaserror` (0 advertencias, 0 errores), `dotnet test INComedor.sln` (61 de 61) y `openspec validate --all --strict` (sin errores), pegar las tres salidas y hacer el commit por rutas de `ci.yml` y los dos scripts

  ```text
  dotnet build INComedor.sln -warnaserror
  Compilación correcta.
      0 Advertencia(s)
      0 Errores
  build exit=0
  dotnet test INComedor.sln
    total: 61
    error: 0
    correcto: 61
    omitido: 0
  test exit=0
  openspec validate --all --strict
  ✓ change/imponer-invariantes-en-codigo
  ✓ spec/modo-demostracion
  ✓ spec/registro-consumo
  ✓ spec/sincronizacion-lotes
  Totals: 4 passed, 0 failed (4 items)
  validate exit=0
  ```

## 4. `AGENTS.md`

- [x] 4.1 Agregar a las dos reglas de `AGENTS.md` la mención de su barrera, con el texto de D8 y sin cambiar el resto de la línea; verificar con `git diff --word-diff -- AGENTS.md` que sólo se agregan los dos paréntesis, y hacer el commit por rutas

  ```text
  - No modificar un test existente sin aprobación explícita de [-David.-]{+David (en Claude Code, el hook de `.claude/settings.json` pide la confirmación).+}
  - Never commit secrets: ni certificados, ni claves, ni cadenas de conexión con [-credenciales.-]{+credenciales (el job `secretos` de la CI los busca con gitleaks).+}
   AGENTS.md | 4 ++--
   1 file changed, 2 insertions(+), 2 deletions(-)
  ```

## 5. Corrida real de la CI

- [x] 5.1 David: empujar la rama de trabajo que elija dentro de `feature/**` (ver el riesgo «Empujar la rama de trabajo» del design); verificar con `gh run view <id>` que `build-test`, `specs` y `secretos` terminan en verde y que el log muestra `proyectos auditados: 9 de 9`, `Totals: … passed, 0 failed` y `commits escaneados: N · esperados: N`. Pegar el resumen y el enlace de la corrida

  David eligió el 29/10 una rama nueva, `feature/invariantes-dw` desde `ff6b41f`, y autorizó que la empujara el goal: `feature/entrega-2-DW`, la del Typeform, no se movió. Push de `ef90dc6`; corrida [40953247197](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40953247197):

  ```text
  ✓ feature/invariantes-dw CI · 40953247197
  ✓ build-test in 1m44s
  ✓ secretos in 6s
  ✓ specs in 8s
  build-test | Dependencias vulnerables | proyectos auditados: 9 de 9 · paquetes vulnerables: 0
  build-test | Test | Test run summary: Passed!  total: 61  failed: 0  succeeded: 61
  secretos | Instalar gitleaks | /home/runner/work/_temp/gitleaks.tar.gz: OK
  secretos | Secretos | commits escaneados: 39 · esperados: 39 · exit de gitleaks: 0
  specs | openspec validate | Totals: 4 passed, 0 failed (4 items)
  ```

- [x] 5.2 Preparar en local la rama `feature/control-ci-rojo` desde la rama de trabajo, con el commit de D9 (el spec sin escenarios y `Newtonsoft.Json.Bson` por los dos `Directory.*.props`), sin push; verificar con `git show --stat HEAD` que sólo cambian `openspec/specs/sincronizacion-lotes/spec.md`, `Directory.Packages.props` y `Directory.Build.props`

  ```text
  8642c95 control: CI en rojo a propósito (imponer-invariantes-en-codigo, tarea 5.2; rama descartable)
   Directory.Build.props                       |  3 +++
   Directory.Packages.props                    |  1 +
   openspec/specs/sincronizacion-lotes/spec.md | 25 -------------------------
   3 files changed, 4 insertions(+), 25 deletions(-)
  ```

- [x] 5.3 David: empujar `feature/control-ci-rojo`; verificar con `gh run view <id>` que `specs` falla en «openspec validate», que `build-test` falla en «Dependencias vulnerables» con Build y Test salteados y que `secretos` queda en verde. Pegar el resumen y el enlace. Después David borra la rama remota y el agente la local; verificar que `git ls-remote --heads origin feature/control-ci-rojo` no devuelve nada y que `git branch --list feature/control-ci-rojo` sale vacío

  Empujada y borrada por el goal, con la autorización de David del 29/10. Corrida [40953621473](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40953621473):

  ```text
  X feature/control-ci-rojo CI · 40953621473
  specs: failure | … Instalar OpenSpec=success, openspec validate=failure
  secretos: success | … Instalar gitleaks=success, Secretos=success
  build-test: failure | … Restore=success, Dependencias vulnerables=failure, Build=skipped, Test=skipped
  specs | openspec validate |   ✗ [ERROR] requirements.0.scenarios: Requirement must have at least one scenario
  specs | openspec validate | Totals: 3 passed, 1 failed (4 items)
  build-test | Dependencias vulnerables | proyectos auditados: 9 de 9 · paquetes vulnerables: 9
  secretos | Secretos | commits escaneados: 40 · esperados: 40 · exit de gitleaks: 0
  $ git push origin --delete feature/control-ci-rojo
   - [deleted]         feature/control-ci-rojo
  $ git branch -D feature/control-ci-rojo
  Deleted branch feature/control-ci-rojo (was 8642c95).
  $ git ls-remote --heads origin feature/control-ci-rojo
  (vacío)
  $ git branch --list feature/control-ci-rojo
  (vacío)
  ```

  Las líneas `::error::` del script no aparecen en el log: GitHub las convierte en anotaciones de la corrida.

## 6. Evidencia

- [x] 6.1 Actualizar en `docs/aplicacion-del-curso.md` las ocho filas de D10, partir la de secretos en dos, poner al día «Actualizado el …» y la frase de «Cómo leerlo» sobre la acción 2. Cada `Aplicada` nueva enlaza su archivo del repo y, cuando corresponde, la corrida de 5.1 o la de 5.3. Verificar con el chequeo del mapa (D11), con `defectuosas: 0`, y con `rg -n "acción 2" docs/aplicacion-del-curso.md`, que no debe devolver ninguna mención en futuro

  «Actualizado el 29/10/2026» ya decía la fecha del día y queda igual. Las dos menciones que quedan están en pasado: «Desde la acción 2, el job `specs` … la corre» (fila «Validar con `--strict`») y «El segundo escalón, de la acción 2, sumó …» (fila «Crecer por escalones»).

  ```text
  filas del mapa: 82 · defectuosas: 0
  rg -n "acción 2" docs/aplicacion-del-curso.md
  43:| Validar con `--strict` … Desde la acción 2, el job `specs` de [`ci.yml`](…) la corre en cada push y en cada PR ([corrida 40953247197](…)). |
  114:| Crecer por escalones y pedir el mínimo a la IA | sesión 15 | Aplicada | … El segundo escalón, de la acción 2, sumó specs, dependencias vulnerables y secretos: … |
  ```

- [x] 6.2 Reescribir en `prompts.md` la entrada «Hooks» de §3 y agregar a §5 el ajuste humano de D10, cada uno con su enlace; verificar con `rg -n -i "hook" prompts.md` que ninguna mención los da como pendientes

  ```text
  41:- **Hooks.** Un hook `PreToolUse` de Claude Code, versionado en [`.claude/settings.json`](.claude/settings.json), corre [`confirmar-edicion-de-test.ps1`](…) antes de cada Edit~
  107:7. **Las barreras de la acción 2.** Sobre la propuesta de la IA, el autor decidió el 29/10 cuatro cosas: …
  ```

- [x] 6.3 Verificación integrada: el chequeo de enlaces relativos sobre `docs/aplicacion-del-curso.md`, `prompts.md` y `AGENTS.md` (`enlaces rotos: 0`), `openspec validate --all --strict` sin errores y `git status --short`, en el que sólo aparecen los dos documentos y el `tasks.md` de este change, y no aparece `devoluciones de AI4DEVS/`. Pegar las tres salidas y hacer el commit por rutas

  ```text
  enlaces rotos: 0
  Totals: 4 passed, 0 failed (4 items)
  validate exit=0
   M docs/aplicacion-del-curso.md
   M openspec/changes/imponer-invariantes-en-codigo/tasks.md
   M prompts.md
  ```

## Workflow follow-up

- `/openspec-verify-change` con su salida en la descripción del PR.
- Archivar con `openspec archive imponer-invariantes-en-codigo -y --skip-specs` y correr `openspec validate --all --strict`.
- Decisión de David, fuera del repo: si exige los tres checks en la protección de ramas del fork.
- Push y PR son de David; el PR #1 no se toca.
