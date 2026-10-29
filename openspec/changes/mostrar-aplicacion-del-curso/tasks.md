# Tasks

Los chequeos de enlaces, del mapa y de `prompts.md` son los de design D8: corren en PowerShell 7 desde la raíz del repo y su salida se pega en la tarea. Al cerrar cada grupo hay un commit por rutas, en español.

## 1. Mapa de prácticas y devolución

- [x] 1.1 Crear `docs/aplicacion-del-curso.md` con el título, «Cómo leerlo» (los tres estados de D4) y la convención de citas de D5 en una línea; verificar leyendo que los tres estados y la convención están escritos
- [x] 1.2 Agregar el mapa por área del catálogo (3.1 a 3.10) con las columnas `| Práctica | Lección | Estado | Evidencia o motivo |`: las 67 prácticas, cada una con el estado que muestra el repo hoy y las lecciones renumeradas según D5; verificar con el chequeo del mapa (D8) que da 67 filas o más y `defectuosas: 0`
- [x] 1.3 Agregar la tabla «Proyecto final» con las prácticas del curso v20261027 de D4, con las mismas columnas; verificar con el chequeo del mapa que el total sube en la cantidad de filas agregadas y sigue en `defectuosas: 0`
- [x] 1.4 Agregar «Lo que no se aplica» con los descartes del §7 del catálogo y su motivo, y comprobar que cada descarte tiene su fila `No aplica` en el mapa; verificar contando que cada descarte de la sección aparece como `No aplica` en alguna fila
- [x] 1.5 Agregar la tabla «La devolución pidió | Respuesta | Evidencia» con las diez filas del §5b del análisis, al día con el CI en verde y los PR #2 y #3; verificar leyendo que cada una de las diez filas tiene un enlace en la columna Evidencia
- [x] 1.6 Correr el chequeo del mapa sobre una copia con una fila `Aplicada` sin enlace y otra con un estado inventado (debe dar `SIN EVIDENCIA` y `ESTADO INVÁLIDO`), y después sobre el archivo real; correr también el chequeo de enlaces sobre `docs/aplicacion-del-curso.md`, con salida `enlaces rotos: 0`. Pegar las tres salidas

## 2. `prompts.md` con las cinco partes

- [x] 2.1 Pasar el contenido actual a «Anexo A. Registro de la entrega 1 (29/09/2026)» (líneas 8-203) y «Anexo B. Registro de la entrega 2 (20/10/2026)» (líneas 205-244), bajando cada encabezado un nivel, y agregar al pie de cada anexo la línea que remite a §1-§5; verificar con `git diff --word-diff HEAD -- prompts.md` que en los anexos sólo cambian los `#` de los encabezados y las dos líneas agregadas
- [x] 2.2 Escribir el párrafo inicial y «## 1. Herramientas por fase», con la tabla de las seis fases de L58:232 (Fase | Herramienta | Modelo | Evidencia) según D2; verificar leyendo que hay seis filas y que cada una tiene un enlace relativo a un archivo del repo
- [x] 2.3 Escribir «## 2. Modelos y para qué» y «## 3. Contexto avanzado» según D2, sin presentar los hooks como existentes; verificar con `rg -n "hook" prompts.md` que toda mención de hooks los da como pendientes de la acción 2
- [x] 2.4 Escribir «## 4. Prompts y workflows clave» con los cuatro workflows de D3, cada uno con herramienta y modelo, el prompt (extracto real o resumen rotulado), qué produjo, qué ajustó el humano y un enlace a evidencia; verificar que el chequeo de `prompts.md` (D8) da `workflows: 4`
- [x] 2.5 Escribir «## 5. Criterio humano» con los seis ajustes de D2, cada uno con su enlace, y un enlace a `docs/aplicacion-del-curso.md`; verificar que el chequeo de `prompts.md` lista los siete encabezados de D2 en orden y que el de enlaces da `enlaces rotos: 0`

## 3. Cobertura de la entrega final

- [x] 3.1 Comprobar las dos plantillas con `gh api repos/LIDR-academy-temp/AI4Devs-finalproject --jq .full_name` (debe responder) y `gh api repos/LIDR-academy/AI4Devs-finalproject` (404 esperado), y pegar las dos salidas. Si la vigente tampoco responde, citar el commit `bcde5c4` según el riesgo del design
- [x] 3.2 Reescribir `docs/cobertura-entrega.md` según D6: la tabla `| Requisito | Fuente | Evidencia | Estado |` con los requisitos de la final y las secciones de la plantilla, las fuentes de la consigna en la plantilla vigente, la línea que enlaza la cobertura de la entrega 1 en `feature/entrega-1-DW` y el párrafo de D-C24 al día; verificar con el chequeo de enlaces (`enlaces rotos: 0`) y con `rg -n "\| (Cubierto|Pendiente) \|" docs/cobertura-entrega.md`, que debe contar tantas filas como la tabla tiene, y comprobar leyendo que cada `Pendiente` nombra su change o acción

## 4. README y regla de evidencia

- [ ] 4.1 Aplicar las tres ediciones de D9 en `readme.md` (estado, árbol y enlaces de documentación); verificar que `rg -n "vence el 22/10|Cobertura de E1" readme.md` no encuentra nada y que el chequeo de enlaces sobre `readme.md` da `enlaces rotos: 0`
- [ ] 4.2 Agregar a `rules.tasks` de `openspec/config.yaml` la regla de D7, textual; verificar que `openspec instructions tasks --change mostrar-aplicacion-del-curso --json` la devuelve en `rules` y que `openspec validate --all --strict` no da errores

## 5. Verificación integrada

- [ ] 5.1 Correr el chequeo de enlaces sobre `prompts.md`, `readme.md`, `docs/aplicacion-del-curso.md` y `docs/cobertura-entrega.md` juntos; salida `enlaces rotos: 0`
- [ ] 5.2 Correr `openspec validate --all --strict` y `openspec status --change mostrar-aplicacion-del-curso`; las dos sin errores
- [ ] 5.3 Aplicar a este change su propia regla: agregar en el mapa la evidencia de este change (la fila de `prompts.md` de la tabla «Proyecto final» enlaza este change) y volver a correr el chequeo del mapa, con `defectuosas: 0`
- [ ] 5.4 Correr `git status --short` y comprobar que sólo aparecen `prompts.md`, `readme.md`, `docs/aplicacion-del-curso.md`, `docs/cobertura-entrega.md`, `openspec/config.yaml` y el `tasks.md` de este change, y que no aparece `devoluciones de AI4DEVS/`

## Workflow follow-up

- Revisión de David del mapa contra el repo, antes del PR.
- Archivar con `openspec archive mostrar-aplicacion-del-curso -y --skip-specs` y correr `openspec validate --all --strict`.
- Commit por rutas en `feature/entrega-2-DW`; push, PR y Typeform son de David.
