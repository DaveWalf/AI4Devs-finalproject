# Proposal

## Why

Tres reglas duras de `AGENTS.md` son hoy texto que el agente interpreta: no modificar un test existente sin aprobación de David, no commitear secretos y correr `openspec validate --all --strict` antes de cada PR. El curso pide lo contrario para un invariante crítico: «Si necesitas garantizar que algo *nunca* pase, no se lo pidas al modelo en CLAUDE.md. Escríbelo como hook» (lección 40), y una CI que sea compuerta, que «bloquea merge si tests/coverage/security fallan» (lección 16), con «controles automáticos que ejecutan o aseguran la política» de seguridad (lección 138). El repo no tiene hooks, y su CI está en el primer escalón: restore, build y test. La sesión 15 pide crecer así, de a un escalón: «que compile, que chequee código estático y cada vez un poco más, que chequee las vulnerabilidades».

Va ahora, antes de `evaluar-habilitacion-local`, para que los tres changes funcionales que siguen corran con las barreras puestas.

Origen: acción 2 del plan del análisis contra el curso v20261027 (28/10/2026), aprobado tal cual por David en el gate del 29/10/2026. No nace de una historia ni de un ticket: no cambia el producto.

## What Changes

- **CI en tres jobs** (`.github/workflows/ci.yml`):
  - `build-test` (`windows-latest`) suma, entre Restore y Build, el paso «Dependencias vulnerables»: `dotnet list package --vulnerable --include-transitive --format json`, leído por `.github/scripts/auditar-dependencias.ps1`. Falla si encuentra un paquete vulnerable, o si no auditó todos los proyectos de la solución.
  - `specs` (`ubuntu-latest`), nuevo: instala OpenSpec 1.15.0 con versión fija y corre `openspec validate --all --strict`.
  - `secretos` (`ubuntu-latest`), nuevo: baja gitleaks 8.30.1 verificado por SHA-256 y corre `.github/scripts/escanear-secretos.sh` sobre la historia propia del fork, desde `bcde5c4`. Falla si encuentra un secreto, si los commits escaneados no son los esperados o si existe un `.gitleaksignore`.
- **Hook `PreToolUse` versionado**: `.claude/settings.json` y `.claude/hooks/confirmar-edicion-de-test.ps1`. Pide confirmación (`ask`) antes de que Claude Code edite o sobrescriba un archivo que ya existe en `tests/`. Un test nuevo pasa sin preguntar.
- **Puntero local, fuera del repo**: `C:\AI\INComedor\.claude\settings.json` llama al mismo script, para que el hook corra también en las sesiones que abren en la carpeta padre, que son las de David.
- **`AGENTS.md`**: las dos reglas nombran su barrera, sin cambiar su texto: el hook para los tests y el job `secretos` para los secretos.
- **Evidencia**, por la regla de `config.yaml`:
  - en `docs/aplicacion-del-curso.md` pasan a `Aplicada` las filas de hooks, dependencias vulnerables y barreras automáticas;
  - la fila de secretos se parte en dos: el escaneo queda `Aplicada` y el `.env.example` sigue `Pendiente` (acción 3);
  - se ponen al día las filas que nombran la acción 2 como futura;
  - en `prompts.md` se reescribe la entrada «Hooks» de §3 y se agrega a §5 el criterio humano de este change.

## Non-goals

- Sin cambios en `src/` ni en `tests/`, ni de comportamiento: por eso `skip_specs: true`.
- Sin E2E, Release, video, «Cómo revisar» en el README ni `.env.example`: son la acción 3.
- No se activan GitHub Issues (backlog 4 del análisis, decisión de David).
- Sin SAST (CodeQL), cobertura ni mutación: son escalones siguientes (backlog 5 y 6). Este escalón trae lo que el plan nombró: specs, dependencias y secretos.
- Sin cambios en la configuración del repo en GitHub: ni Dependabot, ni checks requeridos o protección de ramas, ni el secret scanning y el push protection, que ya están activos. Son ajustes del servidor y los decide David.
- El hook no cubre a Codex ni las ediciones por shell (`Set-Content`, `sed -i`); la regla de `AGENTS.md` sigue escrita para esos casos (design D7).
- Sin push, sin PR y sin tocar el PR #1: publicar es de David. La rama descartable del control negativo la empuja y la borra él.

## Capabilities

### New Capabilities

Ninguna. El change declara `skip_specs: true`: son barreras del proceso de desarrollo, no comportamiento del sistema.

### Modified Capabilities

Ninguna.

## Impact

- Nuevos en el repo: `.claude/settings.json`, `.claude/hooks/confirmar-edicion-de-test.ps1`, `.github/scripts/auditar-dependencias.ps1` y `.github/scripts/escanear-secretos.sh`.
- Modificados en el repo: `.github/workflows/ci.yml`, `AGENTS.md`, `docs/aplicacion-del-curso.md` y `prompts.md`.
- Fuera del repo: `C:\AI\INComedor\.claude\settings.json`, nuevo.
- La CI pasa de un check a tres en cada push a `feature/**` y en cada PR. Dependencias nuevas del pipeline, todas con versión fija: `actions/setup-node@v7`, Node 24, `@fission-ai/openspec@1.15.0` y gitleaks 8.30.1.
- Las sesiones de Claude Code que abren en la raíz del repo o en `C:\AI\INComedor` piden confirmación al editar un test existente, también en auto mode.
