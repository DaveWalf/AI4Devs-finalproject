# Design

## Context

El motivo está en proposal.md. Lo que sigue es el estado de partida y lo que se midió el 29/10/2026 sobre `0cc906b`, en clones descartables y sin tocar el repo.

- **CI.** Hay un solo job, `build-test`, en `windows-latest`: restore, build `-warnaserror` y test. Corre en `pull_request` y en `push` a `feature/**`, con `contents: read`. Está en verde desde `6fc1964`.
- **Hooks.** No hay `.claude/settings.json` en el repo. Claude Code carga la configuración `.claude/` del proyecto sólo desde la carpeta donde abre la sesión. Las sesiones de David abren en `C:\AI\INComedor`, la carpeta padre del repo. La prueba de eso es que una sesión abierta ahí no ve las skills `openspec-*` del repo.
- **El fork.** Es público, de una cuenta personal, y ya tiene activos el secret scanning y el push protection de GitHub. Son ajustes del servidor: el repo no los muestra.

Lo medido que da forma a las decisiones:

| Suposición del plan | Medido | Consecuencia |
|---|---|---|
| `openspec validate --all --strict` falla con un spec roto | Depende de la rotura. Un requirement sin escenarios da `ERROR` y exit 1. Uno sin `SHALL` ni `MUST` da un `WARNING` que `--strict` vuelve exit 1. Cambiar un `SHALL` por `should` en un requirement que conserva su `MUST NOT` da exit 0, y renombrar `#### Scenario:` a `#### Escenario:` también, porque cuenta cualquier encabezado de nivel 4 | El control negativo usa un requirement sin escenarios (D9) |
| `dotnet list package --vulnerable` falla si encuentra algo | Sale **0** con `Newtonsoft.Json` 12.0.1 vulnerable (GHSA-5crp-9r3c-p9vr), tanto directo como transitivo vía `Newtonsoft.Json.Bson` 1.0.2. En `--format json` el hallazgo está en `vulnerabilities`. Un proyecto sin hallazgos viene sin `frameworks`, y un restore roto viene con `problems` | El veredicto lo da un script que lee el JSON (D3) |
| — | NuGet Audit ya está activo con el SDK de `global.json`: el `build -warnaserror` actual falla con `NU1903`, también por el transitivo | El paso nuevo va antes del build (D3) |
| El escaneo de secretos detecta un secreto | gitleaks 8.30.1 sobre la historia completa da 10 hallazgos, todos de `64a9d6a` y `57cc057`: el PR #107 de otro alumno a la plantilla de LIDR, de febrero de 2026, que LIDR revirtió en `bcde5c4`. Son JWT de ejemplo truncados. Desde `bcde5c4`: 34 commits escaneados contra 34 esperados y 0 hallazgos. Un secreto sintético commiteado da exit 1 | Se escanea la historia propia (D5) |
| — | Con un rango inválido, gitleaks escribe `0 commits scanned` y `no leaks found` y sale **0** | La compuerta de conteo es parte del paso (D4) |
| El hook pide confirmación | El prototipo pasó siete casos directos, y una edición real con `claude -p` en modo `acceptEdits` fue denegada con el motivo del hook. Sin el hook, la misma edición pasó. Con `powershell.exe` 5.1, un `.ps1` en UTF-8 sin BOM se lee como ANSI: «aprobaciÃ³n» | D6 y D7 |

## Goals / Non-Goals

**Goals:**

- Que cada barrera tenga un control negativo medido: algo concreto que la hace fallar. Así ninguna puede quedar en verde sin haber mirado nada.
- Que lo que corre en la CI sea lo mismo que se prueba en local: la lógica vive en dos scripts versionados, y el YAML sólo los llama.
- Que cada versión externa quede fija: acciones por versión mayor, Node, OpenSpec y gitleaks con su SHA-256.

**Non-Goals:**

- Escribir un test automatizado del hook en la CI. Su prueba es la de D11, corrida a mano en el apply. Si el hook cambia, se vuelve a correr.
- Bloquear el merge con checks requeridos: es configuración del repo en GitHub, y la decide David (riesgos).

## Decisions

### D1. Tres jobs, no más pasos en el job existente

`build-test` sigue en `windows-latest` y suma el paso de dependencias. `specs` y `secretos` son jobs nuevos en `ubuntu-latest`. Corren en paralelo, y cada uno aparece como su propio check en el PR (`CI / build-test`, `CI / specs`, `CI / secretos`). Así, un rojo en una barrera no esconde a las demás.

```yaml
name: CI

on:
  pull_request:
  push:
    branches:
      - "feature/**"

permissions:
  contents: read

jobs:
  build-test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v7

      # El SDK sale de global.json (RC fijada con versión exacta: se instala tal cual, sin rollForward).
      - uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json

      - name: Restore
        run: dotnet restore INComedor.sln

      # Antes del build: NuGet Audit también lo haría fallar (NU1901-NU1904 con -warnaserror), pero sin nombrar el paquete.
      - name: Dependencias vulnerables
        shell: pwsh
        run: ./.github/scripts/auditar-dependencias.ps1

      - name: Build
        run: dotnet build INComedor.sln -warnaserror --no-restore

      - name: Test
        run: dotnet test INComedor.sln --no-build

  specs:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7

      - uses: actions/setup-node@v7
        with:
          node-version: "24"

      # La versión que generó las skills de .claude y .agents; sin scripts de instalación de terceros.
      - name: Instalar OpenSpec
        run: npm install --global --ignore-scripts --no-fund --no-audit @fission-ai/openspec@1.15.0

      - name: openspec validate
        run: openspec validate --all --strict

  secretos:
    runs-on: ubuntu-latest
    env:
      GITLEAKS_VERSION: 8.30.1
      GITLEAKS_SHA256: 551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb
    steps:
      # Historia completa: el escaneo recorre los commits propios desde el fork, no sólo el último.
      - uses: actions/checkout@v7
        with:
          fetch-depth: 0

      - name: Instalar gitleaks
        run: |
          curl -sSfL -o "$RUNNER_TEMP/gitleaks.tar.gz" \
            "https://github.com/gitleaks/gitleaks/releases/download/v${GITLEAKS_VERSION}/gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz"
          echo "${GITLEAKS_SHA256}  $RUNNER_TEMP/gitleaks.tar.gz" | sha256sum --check -
          tar -xzf "$RUNNER_TEMP/gitleaks.tar.gz" -C "$RUNNER_TEMP" gitleaks

      - name: Secretos
        run: bash .github/scripts/escanear-secretos.sh "$RUNNER_TEMP/gitleaks"
```

El YAML lo parseó el 29/10 la librería `yaml` que trae OpenSpec 1.15.0: tres jobs con los pasos de arriba. `actionlint` no corrió, porque bajarlo y ejecutarlo es código externo nuevo y queda para el apply, con permiso de David (tarea 3.5).

- **Descartada: todo en `build-test`, con `if: ${{ !cancelled() }}` en cada barrera.** Un solo check en el PR y condiciones que hay que leer para entender qué corre. Además, specs y secretos no necesitan Windows.
- **Descartada: un workflow por barrera.** Serían tres archivos con los mismos disparadores y permisos, que pueden divergir.

### D2. OpenSpec en la CI

Se instala con `setup-node@v7` y Node 24, y después `npm install --global --ignore-scripts @fission-ai/openspec@1.15.0`. Es la versión que regeneró las skills en `6708409`, y la que corre en local. En Actions la variable `CI=true` apaga su telemetría (`dist/telemetry/index.js`, regla 3). El paquete no tiene `preinstall` ni `postinstall` propios, y `--ignore-scripts` impide que corran los de sus 57 dependencias. Medido en un prefijo aislado: 7 s de instalación, y después `validate` 3 de 3.

- **Descartada: `npx @fission-ai/openspec` sin versión.** La versión de la CI dejaría de ser la del repo sin que nadie lo decida.
- **Descartada: un `package.json` con `package-lock.json` para fijar también las transitivas.** Mete un proyecto Node, con su mantenimiento, en un repo .NET, para una herramienta de un paso. El riesgo que queda está en los riesgos.

### D3. Dependencias vulnerables: un script que lee el JSON, antes del build

`.github/scripts/auditar-dependencias.ps1` corre `dotnet list INComedor.sln package --vulnerable --include-transitive --format json` y falla en tres casos:

- si `dotnet list` sale distinto de 0;
- si los proyectos del informe no son todos los de `dotnet sln list`, o si alguno trae `problems` (una corrida que no auditó nada también sale 0);
- si algún paquete, directo o transitivo, trae `vulnerabilities`. Cada uno se informa con `::error::`: proyecto, paquete, versión, gravedad y aviso.

```powershell
# Falla si la solución tiene un paquete NuGet vulnerable, directo o transitivo.
# `dotnet list package --vulnerable` sale 0 aunque encuentre uno: el veredicto sale de su JSON.
# Uso: pwsh .github/scripts/auditar-dependencias.ps1 [-Solucion INComedor.sln]   (desde la raíz del repo)
param([string] $Solucion = 'INComedor.sln')
$ErrorActionPreference = 'Stop'

$salida = dotnet list $Solucion package --vulnerable --include-transitive --format json
if ($LASTEXITCODE -ne 0) { Write-Output $salida; throw "dotnet list package salió $LASTEXITCODE." }
$informe = ($salida -join "`n") | ConvertFrom-Json

# Una corrida que no auditó nada también sale 0: tienen que estar todos los proyectos de la solución, sin
# `problems` (un restore fallido). Un proyecto sin vulnerabilidades viene sin `frameworks`: eso no es un error.
$esperados = @(dotnet sln $Solucion list | Where-Object { $_ -match '\.csproj$' }).Count
$auditados = @($informe.projects).Count
$conProblemas = @($informe.projects | Where-Object { $_.problems })
if ($esperados -eq 0 -or $auditados -ne $esperados -or $conProblemas.Count -gt 0) {
    Write-Output ($informe | ConvertTo-Json -Depth 8)
    throw "Se auditaron $auditados de $esperados proyectos; $($conProblemas.Count) con problemas."
}

$vulnerables = @(
    foreach ($p in $informe.projects) {
        foreach ($f in $p.frameworks) {
            foreach ($paquete in @($f.topLevelPackages) + @($f.transitivePackages)) {
                foreach ($v in @($paquete.vulnerabilities | Where-Object { $_ })) {
                    '{0} · {1} {2} · {3} · {4}' -f (Split-Path $p.path -Leaf), $paquete.id, $paquete.resolvedVersion, $v.severity, $v.advisoryurl
                }
            }
        }
    }
)
"proyectos auditados: $auditados de $esperados · paquetes vulnerables: $($vulnerables.Count)"
if ($vulnerables.Count -gt 0) {
    $vulnerables | ForEach-Object { "::error::$_" }
    exit 1
}
```

Medido el 29/10 con pwsh 7:

- repo en `0cc906b`: `proyectos auditados: 9 de 9 · paquetes vulnerables: 0`, exit 0;
- `Newtonsoft.Json` transitivo: exit 1, con la línea `::error::Lib.csproj · Newtonsoft.Json 12.0.1 · High · …`;
- restore roto (una versión inexistente): exit 1, por `problems`.

El primer borrador exigía `frameworks` en cada proyecto, y el repo limpio dio «0 de 9»: así se encontró el criterio de arriba.

Va entre Restore y Build porque NuGet Audit ya hace fallar el build con `NU1903`. Si fuera después, el build se cortaría antes y el paso que nombra el paquete nunca correría.

- **Descartada: confiar sólo en NuGet Audit.** Su falla depende de que nadie le quite `-warnaserror` al build, y aparece como un error de compilación más, sin un paso que diga «dependencias».
- **Descartada: buscar texto en la salida normal.** La salida está localizada (en esta máquina sale «El proyecto "Lib" tiene los paquetes vulnerables siguientes»), y el runner la da en inglés.
- **Descartada: Dependabot.** Es configuración del repo en GitHub, no un control del pipeline, y abre PR solos.

### D4. Secretos: gitleaks CLI con compuerta de conteo

Decidido por David el 29/10. El binario es el de la release 8.30.1 (licencia MIT) para `linux_x64`, verificado con el SHA-256 que publica su `checksums.txt`:

```text
551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb  gitleaks_8.30.1_linux_x64.tar.gz
```

`.github/scripts/escanear-secretos.sh` recibe la ruta del binario y falla en cuatro casos:

- si existe un `.gitleaksignore`, que silenciaría hallazgos por huella sin pasar por esta revisión;
- si gitleaks sale distinto de 0;
- si los commits escaneados no son los esperados (commits con alguna línea agregada, con el mismo `--diff-merges=first-parent` en los dos lados);
- si `git log` falla con la base dada (`set -euo pipefail`).

```bash
#!/usr/bin/env bash
# Escanea con gitleaks la historia propia del fork y falla si encuentra un secreto o si no escaneó
# lo que debía: gitleaks sale 0 con «0 commits scanned» cuando su git log interno falla.
# Uso: escanear-secretos.sh <gitleaks> [base]   (desde la raíz del repo, con la historia completa)
set -euo pipefail

gitleaks="$1"
# bcde5c4: main del fork, revert de LIDR del PR #107. Lo anterior es la plantilla, no este proyecto.
base="${2:-bcde5c4a233375c0f5ea3c8475786f8f2d3a6f49}"
rango="$base..HEAD"

if [ -e .gitleaksignore ]; then
  echo "::error::Hay un .gitleaksignore: silencia hallazgos sin pasar por la revisión del workflow."
  exit 1
fi

# Commits con alguna línea agregada, con el mismo --diff-merges que usa gitleaks.
esperado=$(git log --diff-merges=first-parent --numstat --format='C %H' "$rango" |
  awk '/^C /{ if (c) n++; c = 0; next } NF == 3 && $1 != "-" && $1 > 0 { c = 1 } END { if (c) n++; print n + 0 }')

registro="$(mktemp)"
set +e
"$gitleaks" git --no-banner --no-color --redact --log-opts="--diff-merges=first-parent $rango" . 2>&1 | tee "$registro"
estado=${PIPESTATUS[0]}
set -e

escaneados=$(grep -oE '[0-9]+ commits scanned' "$registro" | grep -oE '^[0-9]+' || echo 0)
echo "commits escaneados: $escaneados · esperados: $esperado · exit de gitleaks: $estado"

if [ "$estado" -ne 0 ]; then
  echo "::error::gitleaks encontró secretos o falló (exit $estado)."
  exit 1
fi
if [ "$escaneados" -ne "$esperado" ]; then
  echo "::error::gitleaks escaneó $escaneados commits y se esperaban $esperado."
  exit 1
fi
```

Medido el 29/10 en Git Bash, con gitleaks 8.30.1, sobre un clon de `0cc906b`:

| Caso | Salida | Exit |
|---|---|---|
| Historia propia, limpia | `commits escaneados: 34 · esperados: 34` | 0 |
| Commit con un `api_key` sintético | `leaks found: 1` | 1 |
| Un gitleaks que no escanea nada (un stub que imprime `0 commits scanned` y sale 0) | `gitleaks escaneó 0 commits y se esperaban 34` | 1 |
| Base inexistente | `git log` falla | 128 |
| Con un `.gitleaksignore` | `Hay un .gitleaksignore` | 1 |

Un falso positivo futuro se resuelve con el comentario `gitleaks:allow` en esa línea, que queda a la vista en el diff del PR.

- **Descartada: `gitleaks/gitleaks-action@v3`.** Tiene licencia propia (no MIT) y comenta en los PR. Además, no permite la compuerta de conteo contra la falla abierta medida.
- **Descartada: TruffleHog.** Es una acción Docker y puede verificar contra los servicios reales los secretos que encuentra, o sea, mandarlos por la red.
- **Descartada: sólo el secret scanning de GitHub.** Ya está activo y se mantiene, pero no está en el repo: el evaluador no lo ve, y la regla seguiría siendo sólo texto en el repo.

### D5. Secretos: la historia propia, desde `bcde5c4`

El rango es `bcde5c4..HEAD`. `bcde5c4` es el `main` del fork, el revert con el que LIDR deshizo el PR #107. Lo anterior es la plantilla del curso, escrita por otros. En un `pull_request`, `HEAD` es el merge de prueba que arma GitHub, y el mismo `--diff-merges=first-parent` en los dos lados del conteo lo cubre.

- **Descartada: toda la historia, con un `.gitleaksignore` de 10 huellas.** Pone en el repo un canal que silencia hallazgos (D4) para tapar commits que no son de este proyecto y que ya están revertidos.
- **Descartada: sólo el árbol actual (`gitleaks dir`).** No ve un secreto que se commiteó y se borró en el commit siguiente, que es justo el caso que la historia conserva.

### D6. El hook: pide confirmación al tocar un test existente

Decidido por David el 29/10: confirmar, no negar. El matcher es `Edit|Write|MultiEdit|NotebookEdit`. El script:

- toma `tool_input.file_path` (o `notebook_path`), que Claude Code da siempre absoluto;
- lo normaliza con `GetFullPath`, así una ruta con `..` no se escapa;
- devuelve `permissionDecision: "ask"` si la ruta cae bajo `<repo>\tests\` y el archivo ya existe;
- en cualquier otro caso no escribe nada, y sigue el flujo normal de permisos;
- ante un error (una entrada ilegible, una excepción), también pide confirmación. Un error del hook no deja pasar la edición en silencio: un exit distinto de 0 y de 2 sería un error no bloqueante, y la edición pasaría.

El repo se toma de la ubicación del script (`$PSScriptRoot\..\..`), no de `CLAUDE_PROJECT_DIR`, que es la carpeta donde abrió la sesión. Así funciona igual desde la raíz del repo y desde el puntero de la carpeta padre (D7).

Según la documentación de hooks de Claude Code, un `ask` muestra el pedido con la etiqueta `[settings]` y el motivo, y fuerza ese pedido también en auto mode: el clasificador puede denegar, pero no aprobar en silencio. En `claude -p`, donde nadie puede contestar, la llamada se deniega y el modelo lee el motivo. Eso es lo que se midió.

`.claude/settings.json` del repo:

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Edit|Write|MultiEdit|NotebookEdit",
        "hooks": [
          {
            "type": "command",
            "command": "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"$CLAUDE_PROJECT_DIR/.claude/hooks/confirmar-edicion-de-test.ps1\""
          }
        ]
      }
    ]
  }
}
```

`.claude/hooks/confirmar-edicion-de-test.ps1`, guardado en **UTF-8 con BOM** (D7):

```powershell
# PreToolUse de Claude Code: pide confirmación antes de editar o sobrescribir un archivo que ya
# existe en tests/. Es la regla de AGENTS.md «No modificar un test existente sin aprobación
# explícita de David» hecha código. Un test nuevo (Write sobre una ruta que no existe) pasa.
# Ante cualquier error pide confirmación: un hook que falla no deja pasar la edición en silencio.
# Guardado en UTF-8 con BOM: powershell.exe 5.1 lee como ANSI un .ps1 sin BOM y rompe los acentos.
$ErrorActionPreference = 'Stop'

function Pedir-Confirmacion([string] $motivo) {
    [Console]::OutputEncoding = [Text.Encoding]::UTF8
    @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'ask'
            permissionDecisionReason = $motivo
        }
    } | ConvertTo-Json -Compress -Depth 3
    exit 0
}

try {
    $evento = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $ruta = $evento.tool_input.file_path
    if (-not $ruta) { $ruta = $evento.tool_input.notebook_path }
    if (-not $ruta) { exit 0 }

    $tests = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\tests')).TrimEnd('\') + '\'
    $destino = [IO.Path]::GetFullPath($ruta)
    if (-not $destino.StartsWith($tests, [StringComparison]::OrdinalIgnoreCase)) { exit 0 }
    if (-not (Test-Path -LiteralPath $destino -PathType Leaf)) { exit 0 }

    Pedir-Confirmacion "Test existente: $destino. AGENTS.md pide la aprobación explícita de David para modificarlo."
}
catch {
    Pedir-Confirmacion "El hook de tests no pudo evaluar la edición ($($_.Exception.Message)); se pide confirmación."
}
```

- **Descartada: negar con exit 2.** Lo descartó David: con exit 2, el agente no podría editar un test existente ni con su aprobación. Un cambio como el del `DisplayName` del 19/10 tendría que hacerse a mano o apagando el hook.
- **Descartada: una regla `permissions.ask` con `Edit(tests/**)`.** Es declarativa y no necesita script, pero también pide confirmación para crear un test nuevo. Iría contra «un test nuevo se ve fallar antes de implementar», que exige escribir tests seguido.
- **Descartada: un matcher sobre `Bash` y `PowerShell` que busque `tests/` en el comando.** Es una heurística que confunde `dotnet test tests/…` con una escritura, y aun así no cubre todas las formas de escribir un archivo. Ese hueco queda en los riesgos y en D7.

### D7. Dónde vive el hook y con qué intérprete

Decidido por David el 29/10: en el repo, más un puntero local.

- **En el repo**: `.claude/settings.json` y el script. Corren en cualquier sesión que abra en la raíz del repo, incluida la de quien lo clone, y el evaluador los ve.
- **Puntero local**: `C:\AI\INComedor\.claude\settings.json`, fuera del repo, con el mismo matcher y la ruta absoluta del script versionado. Así hay una sola copia de la lógica.

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Edit|Write|MultiEdit|NotebookEdit",
        "hooks": [
          {
            "type": "command",
            "command": "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"C:/AI/INComedor/INComedor-AI4DEVS/.claude/hooks/confirmar-edicion-de-test.ps1\""
          }
        ]
      }
    ]
  }
}
```

El intérprete es `powershell.exe` (Windows PowerShell 5.1), que viene en todo Windows. Con `pwsh`, una máquina que no tenga PowerShell 7 daría «command not found»: un error no bloqueante, y la edición pasaría. Por 5.1, el script va con BOM: sin él, 5.1 lee el UTF-8 como ANSI y el motivo sale «aprobaciÃ³n». La lógica funciona igual, pero el texto que lee David no. Medido en 5.1 y en 7, con y sin BOM.

- **Descartada: sólo en el repo.** El hook no correría en las sesiones de David, ni en las de `/seguir`, que abren en la carpeta padre.
- **Descartada: sólo local.** No queda en el repo: el evaluador no lo ve, y la fila «Hooks para invariantes» no podría pasar a `Aplicada`.
- **Descartada: una copia del script en la carpeta padre.** Dos copias de la misma lógica terminan divergiendo.

### D8. `AGENTS.md`: las reglas se quedan y nombran su barrera

La lección 41 pide no repetir en el prompt lo que ya garantiza un hook. Aun así, las dos reglas se quedan, porque el hook no cubre todo: Codex lee el mismo `AGENTS.md` y no tiene ese hook, y una escritura por shell no pasa por él. Cada una suma una mención corta de su barrera:

- «No modificar un test existente sin aprobación explícita de David (en Claude Code, el hook de `.claude/settings.json` pide la confirmación).»
- «Never commit secrets: ni certificados, ni claves, ni cadenas de conexión con credenciales (el job `secretos` de la CI los busca con gitleaks).»

- **Descartada: sacar la regla de los tests de `AGENTS.md`.** Codex quedaría sin ella.

### D9. Controles negativos

Decidido por David el 29/10. Hay dos niveles:

- **En local, con los mismos scripts y comandos que la CI**, en un clon descartable:
  - el spec sin escenarios hace fallar `openspec validate --all --strict`;
  - `Newtonsoft.Json.Bson` 1.0.2 hace fallar `auditar-dependencias.ps1`;
  - un `api_key` sintético commiteado hace fallar `escanear-secretos.sh`;
  - el stub de gitleaks hace fallar la compuerta de conteo.
- **En la CI real**, con la rama descartable `feature/control-ci-rojo`. Va bajo `feature/**` porque el `push` sólo dispara en esas ramas: un nombre como `control/ci-rojo` no correría la CI. David la empuja y después la borra. Lleva un solo commit sobre la rama de trabajo:
  - el requirement de `openspec/specs/sincronizacion-lotes/spec.md` sin sus escenarios;
  - `Newtonsoft.Json.Bson` 1.0.2, con su `PackageVersion` en `Directory.Packages.props` y su `PackageReference` en un `ItemGroup` de `Directory.Build.props`, para no tocar ningún `.csproj` de `src/` ni de `tests/`.

  Esperado: `CI / specs` en rojo en «openspec validate», `CI / build-test` en rojo en «Dependencias vulnerables» con Build y Test salteados, y `CI / secretos` en verde.

El secreto sintético no se empuja: aun borrando la rama, quedaría en un repo público.

- **Descartada: todo en local.** Muestra que falla el comando, no que falla el job de GitHub.
- **Descartada: la rama con los tres controles.** Subiría un secreto falso a la historia pública del fork.

### D10. La evidencia: el mapa y `prompts.md`

En `docs/aplicacion-del-curso.md`:

| Fila | Hoy | Queda |
|---|---|---|
| Validar con `--strict` y archivar | `Aplicada`, «La CI todavía no corre la validación: acción 2» | `Aplicada`, con el job `specs` de `ci.yml` |
| Hooks para invariantes, no prompts | `Pendiente` | `Aplicada`: el `.claude/settings.json` y el script del repo, el puntero local y lo que no cubre (D8) |
| Secretos fuera del repo, `.env.example` y escaneo en la CI | `Pendiente` | Se parte en dos. «Secretos fuera del repo y escaneo en la CI» queda `Aplicada`, con el job `secretos` y su script. «`.env.example`, o la línea que diga que no hace falta» sigue `Pendiente`, acción 3 |
| Dependencias vulnerables | `Pendiente` | `Aplicada`: el paso, el script y la corrida en verde |
| Crecer por escalones y pedir el mínimo a la IA | «Los escalones siguientes, la acción 2» | El segundo escalón, con la corrida que lo muestra |
| Documentación en la CI | `No aplica`, «(acción 2)» | `No aplica`; el segundo escalón trajo specs, dependencias y secretos, no documentación |
| Barreras automáticas de seguridad en la CI | `Pendiente` | `Aplicada`: dependencias y secretos, con la corrida en rojo del control y la corrida en verde |
| La CI crece de a poco | `Aplicada`, primer escalón | `Aplicada`, con el segundo escalón y su corrida |

La línea «Actualizado el …» y la frase de «Cómo leerlo» que describe la acción 2 en futuro pasan al día. La corrida en verde y la del control negativo sólo existen después del push de David, por eso esta tarea va al final.

En `prompts.md`:

- la entrada «Hooks» de §3 se reescribe con lo que existe (D6 y D7) y lo que no cubre;
- §5 suma un ajuste humano: las cuatro decisiones de David del 29/10 (gitleaks CLI, confirmar y no negar, repo más puntero local, el control negativo en una rama descartable sin el secreto).

Los 4 workflows de §4 no cambian: este change sigue el workflow 2 (proponer con gate humano) y el 4 (verificar lo que sólo falla al correr).

- **Descartada: dejar la fila de secretos entera en `Pendiente` hasta la acción 3.** El escaneo, que existe, quedaría escondido detrás del `.env.example`, que todavía no.

### D11. Verificación

Todo desde la raíz del repo, o desde un clon descartable cuando el control rompe algo. La salida de cada chequeo va pegada en su tarea.

- **Hook, directo.** El probador manda siete entradas al script con `powershell.exe` y muestra la decisión. Esperado:
  - `ask` para «Edit de test existente», «Write sobre test existente» y «Ruta con ..»;
  - sin salida para «Write de test nuevo», «Edit fuera de tests», «Carpeta parecida tests2» y «Sin file_path»;
  - el motivo con «aprobación» bien escrito.

  ```powershell
  param([string] $Repo, [string] $Motor = 'powershell.exe')
  $hook = Join-Path $Repo '.claude\hooks\confirmar-edicion-de-test.ps1'
  $casos = [ordered]@{
      'Edit de test existente'     = @{ tool_name = 'Edit';  tool_input = @{ file_path = (Join-Path $Repo 'tests\Intelektron.INComedor.Domain.Tests\ValueObjectsTests.cs') } }
      'Write de test nuevo'        = @{ tool_name = 'Write'; tool_input = @{ file_path = (Join-Path $Repo 'tests\Intelektron.INComedor.Domain.Tests\NoExisteTests.cs') } }
      'Write sobre test existente' = @{ tool_name = 'Write'; tool_input = @{ file_path = (Join-Path $Repo 'tests\Intelektron.INComedor.Domain.Tests\ValueObjectsTests.cs') } }
      'Edit fuera de tests'        = @{ tool_name = 'Edit';  tool_input = @{ file_path = (Join-Path $Repo 'src\Intelektron.INComedor.Domain\FechaNegocio.cs') } }
      'Carpeta parecida tests2'    = @{ tool_name = 'Edit';  tool_input = @{ file_path = (Join-Path $Repo 'tests2\X.cs') } }
      'Ruta con ..'                = @{ tool_name = 'Edit';  tool_input = @{ file_path = (Join-Path $Repo 'src\..\tests\Intelektron.INComedor.Domain.Tests\ValueObjectsTests.cs') } }
      'Sin file_path'              = @{ tool_name = 'Edit';  tool_input = @{ } }
  }
  foreach ($c in $casos.GetEnumerator()) {
      $json = $c.Value | ConvertTo-Json -Compress -Depth 3
      $salida = $json | & $Motor -NoProfile -ExecutionPolicy Bypass -File $hook
      $codigo = $LASTEXITCODE
      if ($salida) { $d = ($salida | ConvertFrom-Json).hookSpecificOutput; $txt = "$($d.permissionDecision) | $($d.permissionDecisionReason)" } else { $txt = '(sin salida: sigue el flujo normal)' }
      '{0,-27} exit={1}  {2}' -f $c.Key, $codigo, $txt
  }
  ```

- **Hook, edición real.** En un clon descartable del repo, ya con el hook commiteado, se corre `claude -p "<pedido>" --model haiku --permission-mode acceptEdits --output-format json` cuatro veces:
  - A: editar con Edit un test existente → una denegación con el motivo del hook, y el archivo intacto;
  - B: crear un test nuevo con Write → creado;
  - C: editar un archivo de `src/` → editado;
  - D: con `.claude/settings.json` renombrado, la misma edición de A → pasa. Es el control que muestra que la denegación de A la causa el hook.

  Se pega `permission_denials` y el `git status --short` del clon.
- **Hook, interactivo.** David abre una sesión nueva en `C:\AI\INComedor`, ya con el puntero, y le pide al agente que edite un test existente. Tiene que ver el pedido de confirmación con `[settings]` y el motivo, y lo rechaza. Se pega el texto del pedido.
- **BOM.** Los tres primeros bytes del script son `EF BB BF`:

  ```powershell
  ([IO.File]::ReadAllBytes('.claude/hooks/confirmar-edicion-de-test.ps1')[0..2] | ForEach-Object { $_.ToString('X2') }) -join ' '
  ```

- **Workflow.** El YAML se parsea con la librería `yaml` de OpenSpec (tres jobs y sus pasos). `actionlint` 1.7.12 corre sólo si David autoriza bajarlo, verificado por SHA-256 al scratchpad.
- **Scripts y controles locales.** Los casos de D3 y D4, más el spec sin escenarios contra `openspec validate --all --strict` (exit 1) y su vuelta atrás (exit 0).
- **CI real.** La corrida de la rama de trabajo, con los tres jobs en verde, y la de `feature/control-ci-rojo`, con el rojo esperado de D9. Se pega el resumen de `gh run view <id>` de cada una.
- **Mapa y enlaces.** Los chequeos del mapa y de enlaces relativos de `mostrar-aplicacion-del-curso` (design D8, archivado en `openspec/changes/archive/2026-10-29-mostrar-aplicacion-del-curso/design.md`), con `defectuosas: 0` y `enlaces rotos: 0`.

## Risks / Trade-offs

- [Un rojo de la CI no bloquea el merge: no hay checks requeridos ni protección de ramas] → Hoy el merge es humano, y lo hace David con el PR a la vista. Exigir los checks es un ajuste del repo en GitHub, y se lo deja anotado como decisión suya.
- [El hook no ve las escrituras por shell ni las de Codex] → La regla sigue en `AGENTS.md` (D8), y la casilla «No se modificó ningún test existente sin aprobación» de la plantilla de PR sigue en pie. El diff del PR muestra cualquier test tocado.
- [Una herramienta reescribe el script y le quita el BOM] → La lógica no cambia: sólo el motivo sale con caracteres rotos. La cabecera del script lo avisa, y el chequeo de BOM de D11 lo detecta.
- [Las dependencias transitivas de OpenSpec flotan entre corridas] → `--ignore-scripts` impide que una dependencia comprometida ejecute código al instalarse. El CLI corre con permisos `contents: read` y sin secretos del repo. Si hace falta más, la alternativa es la descartada en D2.
- [Aparece un aviso nuevo para un paquete fijado y la CI se pone roja sin cambios en el código] → Es lo que se busca. El paso nombra el paquete y su aviso, y el arreglo es subir la versión en `Directory.Packages.props`, con su change si toca el comportamiento.
- [Un falso positivo de gitleaks en un documento futuro] → El comentario `gitleaks:allow` en esa línea, visible en el diff. Nunca un `.gitleaksignore`, que el script rechaza.
- [Empujar la rama de trabajo] → Si la rama es `feature/entrega-2-DW`, el push mueve la rama que el Typeform de la entrega 2 nombró el 29/10 (entonces en `5cc5afc`). Qué rama empuja, y cuándo, lo decide David. La CI corre en cualquier `feature/**`.
- [La corrida suma unos minutos y dos runners Linux] → Los jobs corren en paralelo. El más largo sigue siendo `build-test`.

## Migration Plan

No hay despliegue. En el repo son commits por rutas en la rama de trabajo, que se revierten con `git revert`. El puntero local se quita borrando `C:\AI\INComedor\.claude\settings.json`. La rama `feature/control-ci-rojo` la borra David después de su corrida (`git push origin --delete feature/control-ci-rojo`), y su corrida queda en el historial de Actions como evidencia. El PR #1 y las ramas de las entregas 1 y 2 no se tocan desde el apply.
