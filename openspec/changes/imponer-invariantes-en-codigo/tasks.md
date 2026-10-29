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

- [ ] 3.1 Crear `.github/scripts/auditar-dependencias.ps1` con el texto de D3 y correrlo desde la raíz; verificar `proyectos auditados: 9 de 9 · paquetes vulnerables: 0` y exit 0. En un clon descartable, agregar `Newtonsoft.Json.Bson` 1.0.2 por `Directory.Packages.props` y `Directory.Build.props` (D9): debe dar exit 1 con la línea `::error::` de `Newtonsoft.Json 12.0.1`. Con una versión inexistente, debe dar exit 1 por `problems`. Pegar las tres salidas
- [ ] 3.2 Crear `.github/scripts/escanear-secretos.sh` con el texto de D4 y correrlo desde la raíz con el gitleaks local (`gitleaks version` debe decir `8.30.1`); verificar que escaneados es igual a esperados y que sale con exit 0. En un clon descartable, correr los cuatro controles de D4 (secreto sintético, stub, base inexistente, `.gitleaksignore`), que deben salir distinto de 0. Pegar la tabla con las cinco salidas
- [ ] 3.3 En un clon descartable, quitar los escenarios del requirement de `openspec/specs/sincronizacion-lotes/spec.md` y correr `openspec validate --all --strict`: debe dar `ERROR` y exit 1; restaurado, exit 0. Pegar las dos salidas
- [ ] 3.4 Reemplazar `.github/workflows/ci.yml` por el YAML de D1; verificar parseándolo con la librería `yaml` de OpenSpec (`npm root -g`, `@fission-ai/openspec/node_modules/yaml`), que debe listar `build-test`, `specs` y `secretos` con los pasos de D1, y pegar la salida
- [ ] 3.5 Pedirle permiso a David para bajar `actionlint` 1.7.12 al scratchpad, verificado con el SHA-256 de su `checksums.txt`, y correrlo con `-shellcheck= -pyflakes=` sobre `ci.yml`: debe salir sin hallazgos. Si David no lo autoriza, anotar en la tarea que no corrió y por qué
- [ ] 3.6 Correr `dotnet build INComedor.sln -warnaserror` (0 advertencias, 0 errores), `dotnet test INComedor.sln` (61 de 61) y `openspec validate --all --strict` (sin errores), pegar las tres salidas y hacer el commit por rutas de `ci.yml` y los dos scripts

## 4. `AGENTS.md`

- [ ] 4.1 Agregar a las dos reglas de `AGENTS.md` la mención de su barrera, con el texto de D8 y sin cambiar el resto de la línea; verificar con `git diff --word-diff -- AGENTS.md` que sólo se agregan los dos paréntesis, y hacer el commit por rutas

## 5. Corrida real de la CI

- [ ] 5.1 David: empujar la rama de trabajo que elija dentro de `feature/**` (ver el riesgo «Empujar la rama de trabajo» del design); verificar con `gh run view <id>` que `build-test`, `specs` y `secretos` terminan en verde y que el log muestra `proyectos auditados: 9 de 9`, `Totals: … passed, 0 failed` y `commits escaneados: N · esperados: N`. Pegar el resumen y el enlace de la corrida
- [ ] 5.2 Preparar en local la rama `feature/control-ci-rojo` desde la rama de trabajo, con el commit de D9 (el spec sin escenarios y `Newtonsoft.Json.Bson` por los dos `Directory.*.props`), sin push; verificar con `git show --stat HEAD` que sólo cambian `openspec/specs/sincronizacion-lotes/spec.md`, `Directory.Packages.props` y `Directory.Build.props`
- [ ] 5.3 David: empujar `feature/control-ci-rojo`; verificar con `gh run view <id>` que `specs` falla en «openspec validate», que `build-test` falla en «Dependencias vulnerables» con Build y Test salteados y que `secretos` queda en verde. Pegar el resumen y el enlace. Después David borra la rama remota y el agente la local; verificar que `git ls-remote --heads origin feature/control-ci-rojo` no devuelve nada y que `git branch --list feature/control-ci-rojo` sale vacío

## 6. Evidencia

- [ ] 6.1 Actualizar en `docs/aplicacion-del-curso.md` las ocho filas de D10, partir la de secretos en dos, poner al día «Actualizado el …» y la frase de «Cómo leerlo» sobre la acción 2. Cada `Aplicada` nueva enlaza su archivo del repo y, cuando corresponde, la corrida de 5.1 o la de 5.3. Verificar con el chequeo del mapa (D11), con `defectuosas: 0`, y con `rg -n "acción 2" docs/aplicacion-del-curso.md`, que no debe devolver ninguna mención en futuro
- [ ] 6.2 Reescribir en `prompts.md` la entrada «Hooks» de §3 y agregar a §5 el ajuste humano de D10, cada uno con su enlace; verificar con `rg -n -i "hook" prompts.md` que ninguna mención los da como pendientes
- [ ] 6.3 Verificación integrada: el chequeo de enlaces relativos sobre `docs/aplicacion-del-curso.md`, `prompts.md` y `AGENTS.md` (`enlaces rotos: 0`), `openspec validate --all --strict` sin errores y `git status --short`, en el que sólo aparecen los dos documentos y el `tasks.md` de este change, y no aparece `devoluciones de AI4DEVS/`. Pegar las tres salidas y hacer el commit por rutas

## Workflow follow-up

- `/openspec-verify-change` con su salida en la descripción del PR.
- Archivar con `openspec archive imponer-invariantes-en-codigo -y --skip-specs` y correr `openspec validate --all --strict`.
- Decisión de David, fuera del repo: si exige los tres checks en la protección de ramas del fork.
- Push y PR son de David; el PR #1 no se toca.
