# Design

## Context

Ver `proposal.md` (Why). Estado de partida, en `5cc5afc` de `feature/entrega-2-DW`:

- `prompts.md` (244 líneas) tiene el registro de la entrega 1 ordenado por las secciones de la plantilla vieja y una sección «Entrega 2» con la tabla de las seis fases y tres prompts representativos. No tiene un apartado de contexto avanzado, y la tabla de fases quedó en el 20/10: dice que el CI no corrió y que los changes no están archivados.
- `docs/cobertura-entrega.md` (57 líneas) cubre la entrega 1. Enlaza la plantilla en `LIDR-academy/AI4Devs-finalproject`, que el 28/10 respondió 404; la vigente es `LIDR-academy-temp/AI4Devs-finalproject`.
- `readme.md:13` dice «entrega 2 en curso (vence el 22/10)»; `readme.md:510-513` presenta el registro de IA y la «Cobertura de E1».
- Las fuentes del contenido viven fuera del repo, en la carpeta de gestión del autor: el catálogo de prácticas del 18/10 (§3 catálogo, §7 descartes) y el análisis del 28/10 (§2 cambios del curso, §5b devolución, §4 requisitos de la final). La sesión que aplique este change las recibe en su prompt.
- El curso se renumeró después del catálogo: la lección 58 se reemplazó el 23/10, y el 27/10 todo desde la 59 se corrió un lugar (la de ADR, que el catálogo cita como L62, es hoy la 63).

## Goals / Non-Goals

**Goals:**

- Que el evaluador encuentre en `prompts.md`, en el orden de L58:141-145, las cinco cosas que pide, y desde cada fase un enlace a evidencia del repo.
- Que cada práctica del catálogo tenga en el repo una de tres salidas, verificable por un script: aplicada con su enlace, pendiente con su destino, o no aplica con su motivo.
- Que la regla nueva haga crecer el mapa solo, un change a la vez.

**Non-Goals:**

- No se reescribe la historia de las entregas 1 y 2: se conserva como registro a su fecha.
- No se vuelven a leer las líneas citadas en el catálogo. D5 fija cómo se cita sin releerlas.

## Decisions

### D1. Tres lugares, cada uno con un lector

- `prompts.md`: cómo se usó la IA, en las cinco partes de la L58. Es lo que se evalúa como técnica de prompting.
- `docs/aplicacion-del-curso.md`, nuevo: el mapa de prácticas y la tabla de la devolución. Responde «¿aplicó lo que enseña el curso, y lo que pidió la devolución?».
- `docs/cobertura-entrega.md`: requisito de la final → dónde está, o qué falta. Responde «¿está todo lo que pide la entrega?».

`prompts.md`, la cobertura y el README enlazan al documento nuevo.

- **Descartada: todo en `prompts.md`.** Lo duplicaría en largo y mezclaría el uso de la IA con prácticas que no la involucran (la migración leída, el pipeline, la ACL).
- **Descartada: un archivo para el mapa y otro para la devolución.** La tabla de la devolución tiene diez filas, y las dos responden a la misma pregunta del evaluador.

### D2. `prompts.md`: cinco partes y dos anexos

Estructura, con estos encabezados exactos, que el chequeo de D8 cuenta:

```
# Registro de trabajo con IA — INComedor
(párrafo: fecha, autor, cómo leer; la IA está en el proceso y no en el producto)
## 1. Herramientas por fase
## 2. Modelos y para qué
## 3. Contexto avanzado
## 4. Prompts y workflows clave
### Workflow 1 — …   (de 3 a 5, ver D3)
## 5. Criterio humano
## Anexo A. Registro de la entrega 1 (29/09/2026)
## Anexo B. Registro de la entrega 2 (20/10/2026)
```

- **§1** es la tabla de las seis fases de L58:232 (análisis, diseño, implementación, pruebas, despliegue, mantenimiento), con columnas Fase | Herramienta | Modelo | Evidencia. Cada fila lleva al menos un enlace relativo a un archivo del repo, como el change archivado, el workflow de CI o la carpeta de pruebas. Los enlaces a corridas del CI o a PR van además, no en lugar del relativo. La fila de despliegue nombra lo que existe (CI en verde) y lo que llega con la acción 3 (Release).
- **§2**: qué modelo para qué. Para la entrega 1, Codex con `gpt-6-astra` en diseño y Claude Code con Opus 5.5 en la revisión independiente. Para la entrega 2, `opus` (Opus 5.5) para analizar y proponer, y `sonnet` (Sonnet 5.5) para aplicar, cada uno con su porqué: lo difícil de proponer es ponderar, y el apply sigue tareas ya decididas.
- **§3**:
  - `AGENTS.md` y `CLAUDE.md` = `@AGENTS.md`;
  - `openspec/config.yaml`, con contexto y reglas por artefacto;
  - las skills de OpenSpec en `.claude/skills` y `.agents/skills`;
  - los subagentes de sólo lectura;
  - el MCP de Microsoft Learn para las APIs de .NET;
  - la decisión «un agente por fase con gates humanos, sin orquestador» y su porqué (L59:144);
  - los hooks: hoy ninguno, y llegan con la acción 2. Dicho así, sin presentarlos como existentes.
- **§5**: los ajustes humanos, cada uno con su enlace:
  - la autonomía local en lugar de la autorización central;
  - el recorte D-C24;
  - la migración `0001` leída antes de aplicarla;
  - el `DisplayName` aprobado;
  - el TFM del Kiosk;
  - el primer CI rojo por el fin de línea y su `.gitattributes`.
- **Anexos A y B**: el texto actual de `prompts.md` (líneas 8-203 y 205-244), sin cambios de contenido. Cada encabezado baja un nivel para quedar bajo su anexo. Una línea al pie de cada anexo dice que es el registro a esa fecha, y que lo vigente está en §1-§5.

- **Descartada: actualizar la historia en su lugar.** Borra lo que se dijo a cada fecha, que es parte de la evidencia.
- **Descartada: mover la historia a `docs/prompts-historico.md`.** El evaluador busca el registro en `prompts.md`, el archivo que pide la plantilla.

### D3. Cuatro workflows, agrupados por fase

| Workflow | Fase | Change o hecho | Por qué lo muestra |
|---|---|---|---|
| 1. Fijar el alcance desde la devolución | Análisis | `fijar-primer-corte` | La IA contrasta devolución, curso y paquete. El humano decide el recorte |
| 2. Proponer un change con gate humano | Diseño | `registrar-consumo-atomico` (propuesta) | Explorar → proponer con `opus` → revisión del delta → «Aprobar tal cual» |
| 3. Aplicar con tests vistos fallar | Implementación y pruebas | `registrar-consumo-atomico` y `conectar-puesto-y-receptor` (apply) | `/clear`, `sonnet`, un test por escenario visto en rojo, commit por grupo, la ventana corrida de verdad |
| 4. Verificar y depurar lo que sólo falla al correr | Despliegue y mantenimiento | `/openspec-verify-change` y el primer CI rojo | Una causa reproducida en un worktree CRLF antes de corregir. El humano elige `.gitattributes` y no tocar las pruebas |

Cada workflow tiene la misma forma:

- la herramienta y el modelo;
- el prompt: un extracto real, o un resumen rotulado «resumen, no cita literal», como ya hace `prompts.md`;
- qué produjo;
- qué ajustó el humano;
- un enlace a la evidencia.

Los textos salen de `prompts.md` y del arnés del autor; no se inventan citas.

- **Descartada: cinco, con este mismo análisis como quinto.** Es autorreferente, y el análisis vive fuera del repo.
- **Descartada: tres, sin el 4.** Pierde el mejor ejemplo de criterio humano sobre algo que la IA generó y que sólo falló al correr.

### D4. El mapa: una fila por práctica, tres estados

`docs/aplicacion-del-curso.md` tiene cuatro secciones:

1. Cómo leerlo: los estados y la convención de citas de D5.
2. El mapa, con una tabla por área del catálogo (3.1 a 3.10) y una tabla más, «Proyecto final», para las prácticas que agregó el curso v20261027 (§2 del análisis):
   - IA en 4 de 6 fases;
   - `prompts.md` con cinco partes;
   - E2E;
   - evidencia de funcionamiento;
   - README con `.env.example` y `docker-compose`;
   - ramas en minúsculas;
   - entrega por rama y Typeform;
   - GitHub Issues;
   - barreras automáticas de seguridad;
   - CI por escalones;
   - un agente o varios;
   - producto con LLM;
   - adopción en equipo.
3. Lo que no se aplica: los descartes del §7 del catálogo, agrupados, con su motivo. Cada uno tiene también su fila en el mapa con estado «No aplica».
4. La devolución de la entrega 1 y su respuesta: la tabla del §5b del análisis, puesta al día. Por ejemplo, el pipeline ya compila y prueba en verde, y los PR #2 y #3 están abiertos.

Todas las tablas del mapa tienen las columnas `| Práctica | Lección | Estado | Evidencia o motivo |`. El chequeo de D8 depende de ellas. Estado es exactamente uno de tres valores:

- `Aplicada`: la última celda tiene al menos un enlace (`](`) a un archivo del repo, un commit o una corrida.
- `Pendiente`: la última celda nombra el change o la acción que la trae, por ejemplo «acción 3» o «`recibir-lotes-idempotente`».
- `No aplica`: la última celda da el motivo.

Las 67 prácticas del §3 del catálogo entran todas, y el estado se toma de la realidad del repo al aplicar, no del «Cuándo» del catálogo. Por ejemplo:

- los hooks figuran como `Pendiente`, acción 2;
- el `WebApplicationFactory` figura como `Aplicada`, con enlace a `tests/`.

- **Descartada: ordenar por las 13 áreas del kit de auditoría.** Es la taxonomía de una herramienta del autor, no la del curso, y el evaluador no la conoce.
- **Descartada: listar sólo lo aplicado.** El curso valora elegir: «Usa solo lo que tenga sentido para tu proyecto» (L58:269). Un descarte con su motivo es evidencia.

### D5. Citas del curso en documentos vivos

- Se cita con la numeración v20261027. Una lección del catálogo con número 59 o mayor suma uno: L62 pasa a ser la 63.
- Llevan línea sólo las citas de L58 y L59 que el análisis del 28/10 leyó en la versión vigente.
- El resto va por número y título corto, sin línea, por ejemplo «lección 41, Explore-Plan-Execute».
- Las sesiones y slides (`T…`, `S…`) se citan como «sesión N», sin minuto ni página.
- El encabezado del documento lo dice en una línea.

- **Descartada: copiar las citas del catálogo.** Apuntan a líneas que hoy dicen otra cosa.
- **Descartada: releer cada línea citada.** Son más de cien citas; el número y el título alcanzan para que el evaluador encuentre la lección.

### D6. La cobertura pasa a la final

`docs/cobertura-entrega.md` tiene una tabla `| Requisito | Fuente | Evidencia | Estado |` con dos estados: `Cubierto`, con su enlace, o `Pendiente`, con su destino. Las filas son:

- los requisitos de la final del §4 del análisis: de 3 a 5 funcionalidades, unitarios, integración, E2E, evidencia de funcionamiento, README local, credenciales de prueba, `prompts.md`, IA en las fases y rama más Typeform;
- las secciones de la plantilla vigente: ficha, producto, arquitectura, datos, API, historias, tickets y PR.

Las fuentes de la consigna pasan a `LIDR-academy-temp/AI4Devs-finalproject`, después de comprobar que responde y que la anterior no. La cobertura de la entrega 1 queda en una línea que la enlaza en `feature/entrega-1-DW` en GitHub. También se conserva el párrafo de D-C24 y la segunda ola, actualizado.

- **Descartada: dejar la tabla de la entrega 1 y sumar la de la final.** Duplica el largo y pone en el mismo lugar requisitos que ya no rigen.

### D7. La regla de evidencia en `config.yaml`

Se agrega a `rules.tasks`:

> La última tarea del change agrega su evidencia: la fila nueva o actualizada en `docs/aplicacion-del-curso.md` y, si deja un workflow o un ajuste humano nuevo, su entrada en `prompts.md`; se verifica con el chequeo de enlaces relativos.

- **Descartada: un chequeo en la CI que exija una fila por change.** Contaría filas, sin juzgar si la evidencia importa. Además, la CI es la acción 2.
- **Descartada: la regla en `AGENTS.md`.** Las reglas de `config.yaml` llegan a las instrucciones de `tasks` de cada change, que es donde se escribe esa tarea. `AGENTS.md` sigue corto.

### D8. Verificación

Los tres chequeos corren en PowerShell 7 desde la raíz del repo. Cada uno se prueba primero con su control negativo, y la salida va pegada en la tarea.

**Enlaces relativos.** Comprueba el archivo de destino y, para un `.md`, el ancla contra el slug de GitHub de sus encabezados. Las URL externas y las anclas del mismo archivo no se comprueban.

```powershell
param([string[]]$Archivos)
$rotos = 0
foreach ($a in $Archivos) {
  $dir = Split-Path -Parent (Resolve-Path -LiteralPath $a).Path
  foreach ($hit in Select-String -LiteralPath $a -Pattern '\]\((<[^>]+>|[^)\s]+)\)' -AllMatches) {
    foreach ($m in $hit.Matches) {
      $destino = $m.Groups[1].Value.Trim('<', '>')
      if ($destino -match '^(https?|mailto):' -or $destino.StartsWith('#')) { continue }
      $ruta, $ancla = $destino -split '#', 2
      $completa = Join-Path $dir ([uri]::UnescapeDataString($ruta))
      if (-not (Test-Path -LiteralPath $completa)) { "ROTO ${a}:$($hit.LineNumber) $destino"; $rotos++; continue }
      if ($ancla -and $completa -like '*.md') {
        $slugs = Select-String -LiteralPath $completa -Pattern '^#{1,6} (.+)$' | ForEach-Object {
          $_.Matches[0].Groups[1].Value.ToLowerInvariant() -replace '[^\p{L}\p{N} _-]', '' -replace ' ', '-' }
        if ([uri]::UnescapeDataString($ancla) -notin $slugs) { "ANCLA ${a}:$($hit.LineNumber) $destino"; $rotos++ }
      }
    }
  }
}
"enlaces rotos: $rotos"
```

Probado el 29/10 sobre `prompts.md`, `readme.md` y `docs/cobertura-entrega.md` en `5cc5afc`: `enlaces rotos: 0`. El control negativo, un `.md` con un archivo inexistente, un ancla falsa y un ancla válida, dio `ROTO`, `ANCLA` y `enlaces rotos: 2`.

**Mapa.** Toda fila de 4 columnas que no sea un encabezado tiene un estado válido. Una `Aplicada` lleva un enlace, y ninguna tiene la última celda vacía.

```powershell
$malas = 0; $n = 0
foreach ($l in Get-Content -LiteralPath docs/aplicacion-del-curso.md) {
  if ($l -notmatch '^\|') { continue }
  $c = @($l -split '\|' | ForEach-Object Trim)
  if ($c.Count -ne 6 -or $c[3] -eq 'Estado' -or $c[3] -match '^:?-+:?$') { continue }
  if ($c[3] -notin 'Aplicada', 'Pendiente', 'No aplica') { "ESTADO INVÁLIDO: $l"; $malas++; continue }
  $n++
  if ($c[4] -eq '' -or ($c[3] -eq 'Aplicada' -and $c[4] -notmatch '\]\(')) { "SIN EVIDENCIA: $l"; $malas++ }
}
"filas del mapa: $n · defectuosas: $malas"
```

Esperado: 67 filas o más (todas las del catálogo más las del curso nuevo) y `defectuosas: 0`.

**`prompts.md`.** Comprueba los encabezados de D2, la cantidad de workflows y las seis fases:

```powershell
Select-String -LiteralPath prompts.md -Pattern '^## ' | ForEach-Object Line
"workflows: " + @(Select-String -LiteralPath prompts.md -Pattern '^### Workflow ').Count
```

Esperado: los siete encabezados de D2, en ese orden, y `workflows:` entre 3 y 5. La tabla de §1 se lee: seis filas, y cada una con herramienta, modelo y un enlace relativo.

- **Descartada: lychee o markdownlint.** Agregan una herramienta al proyecto para cuatro archivos. La lección 65 los presenta para la CI, y la CI es la acción 2.

### D9. El README, mínimo

En el README cambian sólo tres cosas:

- `readme.md:13`: el estado pasa a «entrega 2 presentada el 29/10; entrega final en curso»;
- el árbol de `readme.md:229` suma `aplicacion-del-curso.md`;
- `readme.md:510-513` describe el registro de IA con sus cinco partes, nombra la cobertura de la final y enlaza el documento nuevo.

- **Descartada: escribir ahora «Cómo revisar».** Necesita las credenciales, el Release y el E2E, que llegan con la acción 3.

## Risks / Trade-offs

- [El mapa promete como `Aplicada` algo que el repo no muestra] → La regla de D4 exige un enlace en cada `Aplicada`. En la revisión del change, David lee el mapa contra el repo.
- [Un evaluador lee el Anexo B, que dice que el CI no corrió, y no ve §1] → Cada anexo cierra con la línea que remite a §1-§5, y §1 va primero.
- [El chequeo de anclas no reproduce el slug de GitHub en todos los casos, por ejemplo encabezados con emojis o con dos guiones] → Los encabezados del repo no usan emojis. Un `ANCLA` dudoso se resuelve abriendo el archivo en GitHub, y se anota en la tarea.
- [La regla de D7 alarga cada change] → Es una fila y, a veces, una entrada. Sin ella el mapa se reconstruye al final, con más costo y menos memoria de lo que pasó.
- [La plantilla cambia otra vez de organización antes de la final] → La tarea comprueba las dos URL al aplicar. Si `LIDR-academy-temp` tampoco responde, se cita el commit `bcde5c4` del `main` del fork.

## Migration Plan

No hay despliegue: es documentación y una regla de `config.yaml` en `feature/entrega-2-DW`. Se revierte con `git revert` de los commits del apply. El PR #1 y la rama `feature/entrega-1-DW` no se tocan.
