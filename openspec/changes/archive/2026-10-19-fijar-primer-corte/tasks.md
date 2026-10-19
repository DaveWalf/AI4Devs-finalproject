# Tasks

## 1. Decisión y alcance

- [x] 1.1 Registrar D-C24 en `docs/decisiones.md` y anotar en D-C19 que D-C24 la enmienda, sin borrarla; verificar con `rg -n "D-C19|D-C24" docs/decisiones.md` que ambas filas existen y D-C19 remite a D-C24
- [x] 1.2 Alinear `docs/plan-academico.md` con D-C24 (prioridad por historia, hitos M1 y M2 y punto 4 de la regla de recorte); verificar que `rg -n "no se recorta un must" docs readme.md` ya no encuentra la frase como regla vigente
- [x] 1.3 Alinear `readme.md` §1.2 y `docs/cobertura-entrega.md` con las cinco must, la should y la segunda ola, con F01–F04 conservando sus ID; verificar leyendo las tres listas contra la tabla de `proposal.md`

## 2. Arquitectura y seguridad

- [x] 2.1 Dejar dicho en ADR01 de `docs/arquitectura.md` y en la tabla de componentes de `readme.md` §2.2 que las pantallas son sólo Razor y WPF es la ventana host del BlazorWebView, con WPF XAML como contingencia; verificar con `rg -n "ADR01|Kiosco táctil" docs/arquitectura.md readme.md`
- [x] 2.2 Agregar a `readme.md` §2.5 la protección en reposo del corte de D6; verificar que el párrafo nombra los cinco puntos (código no secreto, clave mTLS no exportable, ACL, cifrado de disco, templates y verificador fuera)

## 3. Contexto del agente y registro

- [x] 3.1 Escribir `AGENTS.md` en la raíz con sólo lo que no se infiere del código, sin árbol de carpetas; verificar con `wc -l AGENTS.md` que tiene entre 40 y 80 líneas
- [x] 3.2 Escribir `CLAUDE.md` con la única línea `@AGENTS.md`; verificar con `cat CLAUDE.md`
- [x] 3.3 Agregar `.github/pull_request_template.md` con «¿Qué cambia? ¿Por qué? ¿Cómo probarlo?», el change de OpenSpec y la salida de `/openspec-verify-change`; verificar leyendo el archivo
- [x] 3.4 Agregar a `prompts.md` la sección «Entrega 2»: tabla de las seis fases (fase → herramienta y modelo → evidencia) y el prompt representativo de este change con la decisión de David; verificar que la tabla tiene seis filas

## 4. Verificación integrada

- [x] 4.1 Correr `openspec validate fijar-primer-corte --strict` y `openspec status --change fijar-primer-corte`; las dos salidas sin errores
- [x] 4.2 Comprobar que cada enlace relativo de los archivos cambiados apunta a un archivo existente; cero enlaces rotos
- [x] 4.3 Correr `rg -n "no se recorta un must|D-C19|D-C24" readme.md docs` y revisar que ninguna línea contradice D-C24
- [x] 4.4 Correr `git status --short` y comprobar que no aparece `devoluciones de AI4DEVS/`

## Workflow follow-up

- Archivar con `openspec archive fijar-primer-corte -y --skip-specs` y correr `openspec validate --all --strict`.
- Commit por rutas en `feature/entrega-2-DW`; push, PR y Typeform son de David.
