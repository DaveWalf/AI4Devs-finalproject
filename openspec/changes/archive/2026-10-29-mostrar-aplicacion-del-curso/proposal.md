# Proposal

## Why

En la opción A del Proyecto Final el evaluador mira el repo y nada más: «código y documentación viven juntos» (L58:169, curso v20261027). La final evalúa también «las técnicas de prompting» (L58:240-245), y `prompts.md` es «lo que nos permite evaluar tus habilidades adquiridas» (L58:133). INComedor aplica el curso con rigor, pero buena parte de esa evidencia vive fuera del repo: el catálogo de prácticas con sus descartes, la respuesta a la devolución de la entrega 1 y los gates del autor. Además, `prompts.md` sigue la plantilla vieja («hasta 3 prompts por sección»), no las cinco partes que pide la L58 reemplazada el 23/10 (L58:137-145), y `docs/cobertura-entrega.md` cubre la entrega 1 contra la lección retirada y enlaza una plantilla que hoy responde 404.

Va ahora, antes de `evaluar-habilitacion-local`, para que cada change siguiente deje su evidencia en un lugar que ya existe en vez de reconstruirla al final.

Origen: acción 1 del plan del análisis contra el curso v20261027 (28/10/2026), aprobado tal cual por David en el gate del 29/10/2026, y la devolución de LIDR de la entrega 1 (14/10). No nace de una historia ni de un ticket: no cambia el producto.

## What Changes

- **`prompts.md` con las cinco partes de L58:137-145**: herramientas por fase, modelos y para qué, contexto avanzado, entre 3 y 5 workflows clave agrupados por fase, y criterio humano. Las seis fases del ciclo (L58:232) quedan con herramienta, modelo y un enlace a evidencia del repo. Dice también que la IA está en el proceso y no en el producto. El registro de las entregas 1 y 2 pasa, sin cambios de contenido, a dos anexos.
- **`docs/aplicacion-del-curso.md`, nuevo**:
  - el mapa «práctica del curso → evidencia en el repo, pendiente con su destino, o no aplica y por qué», derivado del catálogo de prácticas del 18/10 y puesto al día con los cambios del curso v20261027;
  - la tabla «la devolución pidió → respuesta → evidencia».
- **`docs/cobertura-entrega.md`**: pasa a cubrir la entrega final contra la L58 vigente y la plantilla de `LIDR-academy-temp/AI4Devs-finalproject`. Marca cada requisito como cubierto, con su enlace, o pendiente, con el change o la acción que lo trae. La cobertura de la entrega 1 queda enlazada en su rama.
- **`readme.md`**: el estado deja de decir «entrega 2 en curso (vence el 22/10)», y los enlaces del árbol y de la documentación nombran el documento nuevo y la cobertura de la final. Nada más del README.
- **`openspec/config.yaml`**: una regla de `tasks` para que la última tarea de cada change agregue su evidencia al mapa y, si corresponde, a `prompts.md`.

## Non-goals

- Sin código, ni pruebas, ni cambios de comportamiento: por eso `skip_specs: true`.
- Sin cambios en la CI ni hooks: son la acción 2 (`openspec validate` en la CI, paquetes vulnerables, secretos y el hook sobre tests).
- Sin E2E, Release, video, «Cómo revisar» en el README ni `.env.example`: son la acción 3.
- No se activan GitHub Issues (backlog 4 del análisis, decisión de David).
- No se tocan los changes archivados ni sus citas `L58:…`, `L81:…`: registran el curso a su fecha (backlog 9).
- No se alinea la tabla del backlog de `docs/historias-y-tickets.md` con D-C24 (backlog 8).
- No se traen al repo las capturas de `conectar-puesto-y-receptor` ni los borradores de PR: los PR #2 y #3 ya están abiertos y la evidencia de funcionamiento llega con la acción 3.
- Sin push, sin PR y sin tocar el PR #1: publicar es de David.

## Capabilities

### New Capabilities

Ninguna. El change declara `skip_specs: true`: es documentación y una regla de proceso, no comportamiento del sistema.

### Modified Capabilities

Ninguna.

## Impact

- Documentos: `prompts.md` (reestructurado), `docs/aplicacion-del-curso.md` (nuevo), `docs/cobertura-entrega.md` (reescrito para la final) y `readme.md` (estado y enlaces).
- Configuración: `openspec/config.yaml`, una regla más en `rules.tasks`. Los changes siguientes la reciben en sus instrucciones de `tasks`.
- Sin efecto en `src/`, `tests/`, `api/`, la CI ni las specs vivas.
