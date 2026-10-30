# Registro de trabajo con IA — INComedor

Actualizado el 29/10/2026. Autor: David Walfisch. Este archivo registra cómo se usó la IA en INComedor, en las cinco partes que pide la lección 58 del curso (L58:137-145): herramientas por fase, modelos y para qué, contexto avanzado, prompts y workflows clave, y criterio humano. La IA está en el proceso de construcción y no en el producto: INComedor no llama a ningún modelo, y las decisiones de consumo son reglas deterministas de la aplicación.

Las secciones 1 a 5 son lo vigente. Los anexos A y B conservan, sin cambios, el registro de las entregas 1 y 2 a su fecha. Qué prácticas del curso se aplicaron, con su evidencia, está en [aplicacion-del-curso.md](docs/aplicacion-del-curso.md). Las citas son extractos reales del autor o resúmenes rotulados «resumen, no cita literal»; no se enlazan conversaciones privadas.

## 1. Herramientas por fase

Las seis fases del ciclo de vida que nombra la lección 58 (L58:232), cada una con la herramienta, el modelo y un enlace a su evidencia en el repo.

| Fase | Herramienta | Modelo | Evidencia |
|---|---|---|---|
| Análisis | Claude Code | `opus` (Opus 5.5), esfuerzo medium | Change [`fijar-primer-corte`](openspec/changes/archive/2026-10-19-fijar-primer-corte/proposal.md) (commit [`36b20d8`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/36b20d8)): la IA contrastó la devolución de LIDR, el material del curso y el paquete; el autor decidió el recorte. |
| Diseño | Claude Code con `/openspec-explore` y `/openspec-propose` | `opus` | Proposal, specs, design y tasks de [`registrar-consumo-atomico`](openspec/changes/archive/2026-10-20-registrar-consumo-atomico/design.md) (commits [`644bfb3`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/644bfb3), ajustado en [`27f5709`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/27f5709)) y de [`conectar-puesto-y-receptor`](openspec/changes/archive/2026-10-20-conectar-puesto-y-receptor/design.md) ([`b5ea3a0`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/b5ea3a0)), cada decisión con su alternativa descartada. El autor los aprobó en el gate antes de cada apply. |
| Implementación | Claude Code con `/openspec-apply-change` | `sonnet` (Sonnet 5.5) | Los cinco proyectos de [`src/`](src/Intelektron.INComedor.Domain/EvaluadorCupo.cs): 26 tareas de [`registrar-consumo-atomico`](openspec/changes/archive/2026-10-20-registrar-consumo-atomico/tasks.md) ([`5dc9614`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/5dc9614), [`97d7717`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/97d7717), [`a48bdcc`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/a48bdcc), [`9f80550`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/9f80550)) y 15 de [`conectar-puesto-y-receptor`](openspec/changes/archive/2026-10-20-conectar-puesto-y-receptor/tasks.md) ([`ea6e7d5`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/ea6e7d5), [`f7337e8`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/f7337e8), [`71f4f5d`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/71f4f5d)). |
| Pruebas | Claude Code | `sonnet` | 61 pruebas en [`tests/`](tests/Intelektron.INComedor.Domain.Tests/EvaluadorCupoTests.cs), cada una escrita desde un escenario de la spec y vista fallar antes de implementar ([`tasks.md`, línea 3](openspec/changes/archive/2026-10-20-registrar-consumo-atomico/tasks.md)). Ensayo de caídas y concurrencia sobre SQLite real en [`CaidasYConcurrenciaTests`](tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/CaidasYConcurrenciaTests.cs) ([`34e21d8`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/34e21d8)); 61 de 61 en el CI ([corrida 40795276016](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40795276016)). |
| Despliegue | Claude Code: workflow de CI generado y revisado | `sonnet` | [`ci.yml`](.github/workflows/ci.yml) ([`c122028`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/c122028)): restore, build `-warnaserror` y test en `windows-latest`, en verde en la [corrida 40802117357](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40802117357). El Release con el EXE llega con la acción 3 del plan de adopción. |
| Mantenimiento | Claude Code con `/openspec-verify-change` y `openspec validate --all --strict`; `AGENTS.md` como contexto vivo | `sonnet` en el verify que cierra cada apply; `opus` (Opus 5.5) al volver a verificar `registrar-consumo-atomico` tras sus ajustes y al depurar el primer CI rojo | [`AGENTS.md`](AGENTS.md) ([`7a57612`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/7a57612)), las [specs vivas](openspec/specs/registro-consumo/spec.md) y la salida de verify de cada change en la descripción de los PR [#2](https://github.com/DaveWalf/AI4Devs-finalproject/pull/2) y [#3](https://github.com/DaveWalf/AI4Devs-finalproject/pull/3). La corrección del primer CI rojo es el workflow 4 de la sección 4. |

## 2. Modelos y para qué

Cada modelo se eligió por lo que la etapa pide, no por el nombre.

| Entrega | Etapa | Herramienta y modelo | Para qué y por qué |
|---|---|---|---|
| 1 | Investigación, especificación y diseño | Codex, modelo configurado `gpt-6-astra` | Leer la documentación existente, contrastar consignas y preparar Markdown, diagramas editables y OpenAPI. Hace el trabajo documental de punta a punta. |
| 1 | Revisión independiente y evaluación simulada | Claude Code, `claude-opus-5-5[1m]` (esfuerzo `max` en la revisión independiente, según su registro) | Contrastar el planeamiento y detectar inconsistencias con otro modelo y sin heredar el contexto de quien lo escribió: esa revisión cruzada mejoró la auditoría dentro de la transacción de negocio y el detalle de historias y tickets (Anexo A). |
| 2 | Analizar, explorar y proponer un change | Claude Code, `opus` (Opus 5.5) | Lo difícil de proponer es ponderar: qué entra en el corte, qué alternativa se descarta y por qué. Es una decisión de criterio, y el humano la revisa en el gate. |
| 2 | Aplicar un change | Claude Code, `sonnet` (Sonnet 5.5), en una sesión nueva (`/clear`) | El apply sigue tareas que ya se decidieron y se revisaron, y cada una tiene su prueba o su comando de verificación; no hay criterio nuevo que ponderar. |

## 3. Contexto avanzado

Lo que se le dio al agente para que trabaje con poco contexto y de alta señal, todo versionado en el repo:

- **`AGENTS.md` y `CLAUDE.md`.** [`AGENTS.md`](AGENTS.md) lleva sólo lo que el código no muestra: invariantes del producto, el alcance del primer corte, reglas, comandos y lo que no se construye. [`CLAUDE.md`](CLAUDE.md) contiene `@AGENTS.md`, para que Claude Code y Codex lean la misma fuente.
- **`openspec/config.yaml`.** [El contexto del primer corte y una regla por artefacto](openspec/config.yaml) (proposal, specs, design y tasks), que llegan a las instrucciones de cada change.
- **Skills de OpenSpec.** Siete skills, las mismas para Claude Code y para Codex: [`.claude/skills`](.claude/skills/openspec-apply-change/SKILL.md) y [`.agents/skills`](.agents/skills/openspec-apply-change/SKILL.md). Explorar, proponer, actualizar, aplicar, verificar, sincronizar y archivar son pasos separados.
- **Subagentes de sólo lectura.** En la entrega 1 se distribuyeron análisis entre subagentes (requisitos e historias, API y datos, revisión independiente; ver el Anexo A). En la entrega 2 se usaron para leer el material del curso y el repo sin cargar con esas lecturas el contexto principal.
- **MCP de Microsoft Learn.** Para consultar las APIs de .NET y SQLite: la sesión que propuso `registrar-consumo-atomico` leyó por ese MCP las páginas de transacciones, tipos y errores de `Microsoft.Data.Sqlite`, y su [design](openspec/changes/archive/2026-10-20-registrar-consumo-atomico/design.md) apoya ahí sus decisiones.
- **Un agente por fase, con gates humanos y sin orquestador.** El flujo es el del curso: del problema al PRD, de ahí a las funcionalidades, las historias, los tickets y el SDD por ticket (L59:132). Cada fase la hace un agente con un prompt propio, y entre una y otra espera el gate del autor. El orquestador es opcional: «¿Es obligatorio para aprobar? No. Puedes entregar un proyecto excelente operando tus agentes de forma manual» (L59:144). En un proyecto de una persona y un producto sin agentes, un orquestador sumaría una pieza que mantener sin sumar evidencia.
- **Hooks.** Hubo un hook `PreToolUse` de Claude Code que, antes de cada Edit o Write, pedía confirmación (`ask`) si el archivo era un test existente. Se probó con ediciones reales en `claude -p` y con su control sin el hook (tarea 1.4 de [`imponer-invariantes-en-codigo`](openspec/changes/archive/2026-10-30-imponer-invariantes-en-codigo/tasks.md)). El autor lo quitó el 30/10/2026, durante el apply de `atender-seleccion-y-confirmacion`: contaba como existente cualquier prueba que ya estuviera en disco, también las que el change acababa de crear, y pedía permiso en cada corrección. La regla sigue escrita en [`AGENTS.md`](AGENTS.md), y cada tarea la verifica con `git diff --stat -- tests/`.

## 4. Prompts y workflows clave

Cuatro workflows que fueron el núcleo del desarrollo, agrupados por fase: análisis, diseño, implementación y pruebas, y despliegue y mantenimiento. Cada uno dice la herramienta y el modelo, el prompt (un extracto real o un resumen rotulado «resumen, no cita literal»), qué produjo, qué ajustó el humano y dónde está la evidencia.

### Workflow 1 — Fijar el alcance desde la devolución

**Fase:** análisis. **Herramienta y modelo:** Claude Code, `opus` (Opus 5.5), esfuerzo medium.

**Prompt (extracto real del paso ejecutado el 19/10, redactado en una sesión anterior y lanzado por el autor):**

> Tomá como dadas las decisiones de David del 18/10/2026, que enmienda D-C19, y del 19/10 […]: el recorte de LIDR es la versión que se presenta (entrega 2 y final). […] Creá el change con `openspec new change fijar-primer-corte`. En su `.openspec.yaml` agregá `skip_specs: true`, porque decide alcance y contexto, no comportamiento. […] Mostrale a David los tres artefactos […] Es la revisión humana antes de aplicar que el curso llama «el oro de la sesión». No sigas sin su aprobación.

**Qué produjo:** la IA contrastó la devolución de LIDR, el material del curso y el paquete, y propuso cinco historias must (HU01.1, HU01.3, HU02.1, HU02.2 sin el escenario F y HU03.1) y una should (HU01.2 reducida al código). Marcó HU03.1 como la must más discutible y presentó dos ajustes posibles: pasarla a should o quitar la should.

**Qué ajustó el humano:** el autor decidió el recorte. En la revisión del change respondió «Aprobar tal cual», y con esa aprobación se aplicaron D-C24, el plan, el README, `AGENTS.md` y la plantilla de PR.

**Evidencia:** change [`fijar-primer-corte`](openspec/changes/archive/2026-10-19-fijar-primer-corte/proposal.md), [D-C24 en `decisiones.md`](docs/decisiones.md) y el commit [`36b20d8`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/36b20d8).

### Workflow 2 — Proponer un change con gate humano

**Fase:** diseño. **Herramienta y modelo:** Claude Code con `/openspec-explore` y `/openspec-propose`, `opus` (Opus 5.5).

**Prompt (resumen, no cita literal):** explorar el tema y proponer el change `registrar-consumo-atomico` (TK03.1) con las reglas de [`config.yaml`](openspec/config.yaml): specs con un escenario de error o de borde por requirement, design con la alternativa descartada de cada decisión y tasks que nombran su prueba o su comando. Mostrar los cuatro artefactos y parar, sin aplicar: la revisión del delta es del autor.

**Qué produjo:** la capability `registro-consumo`, con 8 requirements y 22 escenarios que citan su AT; un design de doce decisiones, cada una con su alternativa descartada, y las tareas en siete grupos.

**Qué ajustó el humano:** en el gate el autor aprobó el change tal cual y decidió que la regla RB02 completa (HU01.3-H e I) fuera a un change propio, `evaluar-habilitacion-local`, en lugar de crecer este. Después aprobó tres cambios sobre el change ya propuesto: el runner de `global.json` en D1, los cuatro enums sin `CHECK` en D5 y el escenario «Caída entre el commit y el resultado», que pasa a `QUEUED`.

**Evidencia:** [proposal](openspec/changes/archive/2026-10-20-registrar-consumo-atomico/proposal.md), [design](openspec/changes/archive/2026-10-20-registrar-consumo-atomico/design.md) y [spec](openspec/changes/archive/2026-10-20-registrar-consumo-atomico/specs/registro-consumo/spec.md), en los commits [`644bfb3`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/644bfb3) (propuesta) y [`27f5709`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/27f5709) (ajustes aprobados).

### Workflow 3 — Aplicar con tests vistos fallar

**Fase:** implementación y pruebas. **Herramienta y modelo:** Claude Code con `/openspec-apply-change`, `sonnet` (Sonnet 5.5), en una sesión nueva (`/clear`).

**Prompt (resumen, no cita literal):** aplicar `registrar-consumo-atomico` y, después, `conectar-puesto-y-receptor`, grupo por grupo y con un commit por rutas por grupo. Cada test se escribe antes de implementar, se corre y se ve fallar por la razón esperada; si pasa sin cambios, se rompe la línea que lo sostiene, se lo ve fallar y se la restaura. No se modifica un test visto en verde sin aprobación. La migración se lee entera antes de aplicarla. El receptor se revisa contra OWASP Top 10 y la ventana del puesto se corre de verdad antes de cerrar la tarea 3.6. Se pregunta sólo si nada avanza sin el autor.

**Qué produjo:** solución .NET 11 con dominio de fecha de negocio y cupo, migración `0001`, unidad de trabajo con `BEGIN IMMEDIATE`, replay por `IntentId` y ensayos de caída y concurrencia (48 pruebas); después los cinco proyectos de `src/`, el receptor que responde `501` y el puesto con su pantalla Razor (61 pruebas). La ventana mostró `AUTHORIZED` en el primer toque y `LOCAL_LIMIT_REACHED` en el segundo.

**Qué ajustó el humano:** el autor leyó y aprobó la migración `0001` (tarea 4.2), aprobó el cambio del `DisplayName` de una prueba ya vista en verde y aprobó corregir el TFM del Kiosk que sólo falló al correr la ventana (ver la sección 5).

**Evidencia:** las tareas de [`registrar-consumo-atomico`](openspec/changes/archive/2026-10-20-registrar-consumo-atomico/tasks.md) y de [`conectar-puesto-y-receptor`](openspec/changes/archive/2026-10-20-conectar-puesto-y-receptor/tasks.md), [`CaidasYConcurrenciaTests`](tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/CaidasYConcurrenciaTests.cs) y [`PantallaDemostracionTests`](tests/Intelektron.INComedor.Kiosk.Tests/PantallaDemostracionTests.cs), con los commits [`9f80550`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/9f80550), [`34e21d8`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/34e21d8) y [`71f4f5d`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/71f4f5d).

### Workflow 4 — Verificar y depurar lo que sólo falla al correr

**Fase:** despliegue y mantenimiento. **Herramienta y modelo:** Claude Code con `/openspec-verify-change` y GitHub Actions, `opus` (Opus 5.5) en la depuración; los verify que cierran cada apply corrieron con `sonnet`.

**Prompt (resumen, no cita literal):** con el primer CI en rojo (corrida [40790002939](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40790002939), 46 pruebas de 48), corregir antes de seguir con cualquier otra cosa y reproducir la causa en un checkout con `core.autocrlf=true` antes de tocar nada. Sin push de la rama de la entrega 2, ni PR, ni envío hasta que el CI de la rama del primer change pase.

**Qué produjo:** el repo no tenía `.gitattributes`; `api/openapi.yaml` está en LF y el checkout de `windows-latest` lo deja en CRLF, y `ContratoEventosTests` busca el esquema con saltos LF. La causa se reprodujo en un worktree CRLF de cada rama (46 de 48 y 59 de 61) antes de corregir. Antes, `/openspec-verify-change` ya había aportado hallazgos reales, como el prefijo «D6» de `CodigoProtegidoTests`, que debía ser «D12».

**Qué ajustó el humano:** había dos salidas, tocar la prueba o fijar el fin de línea. El autor eligió `.gitattributes` (`api/openapi.yaml text eol=lf`) y no tocar las pruebas, que es lo que `AGENTS.md` exige. En el worktree CRLF la rama pasó de 46 a 48 de 48.

**Evidencia:** [`.gitattributes`](.gitattributes), el commit [`6fc1964`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/6fc1964) y el merge [`acd400b`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/acd400b), y las corridas en verde [40794862257](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40794862257) y [40795276016](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40795276016). El prefijo «D6» está en [`CodigoProtegidoTests`](tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/CodigoProtegidoTests.cs).

## 5. Criterio humano

Los ajustes manuales que el autor hizo sobre lo que produjo la IA, cada uno con su evidencia. Qué prácticas del curso respaldan este criterio, y con qué evidencia, está en [aplicacion-del-curso.md](docs/aplicacion-del-curso.md).

1. **Autonomía local en lugar de autorización central.** La primera propuesta de la IA autorizaba los consumos en un servidor central. El autor la corrigió: el puesto autoriza y registra sin red, con sus reglas y su base locales, y ningún acuse central revierte un consumo ya confirmado. Está en el [Anexo A](#anexo-a-registro-de-la-entrega-1-29092026), en el invariante de [`AGENTS.md`](AGENTS.md) y en las [decisiones](docs/decisiones.md).
2. **El recorte D-C24.** Ante el alcance excesivo que marcó la devolución, el autor aceptó el recorte a cinco historias must y una should, en lugar de las doce must que comprometía la entrega 1. Evidencia: [D-C24](docs/decisiones.md) y el change [`fijar-primer-corte`](openspec/changes/archive/2026-10-19-fijar-primer-corte/proposal.md).
3. **La migración `0001`, leída antes de aplicarla.** El autor leyó entera y aprobó la migración que crea las tablas de la transacción de confirmación antes de que se aplicara: [`0001_transaccion_consumo.sql`](src/Intelektron.INComedor.Infrastructure.Sqlite/Migraciones/0001_transaccion_consumo.sql), tarea 4.2 de [`registrar-consumo-atomico`](openspec/changes/archive/2026-10-20-registrar-consumo-atomico/tasks.md) y commit [`a48bdcc`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/a48bdcc).
4. **El `DisplayName` de una prueba ya vista en verde.** La regla del proyecto prohíbe tocar un test existente sin aprobación del autor: el autor aprobó cambiar el nombre del escenario «Un rechazo tampoco lo registra» en una prueba que ya se había visto en verde. Evidencia: [`CodigoProtegidoTests`](tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/CodigoProtegidoTests.cs) y la [regla de `AGENTS.md`](AGENTS.md).
5. **El TFM del Kiosk.** El design proponía `net11.0-windows`, y la ventana se cerraba al abrir (`Microsoft.Windows.SDK.NET`) y el proyecto de pruebas fallaba con NU1201; lo detectó correr la ventana de verdad, no el build ni las pruebas. El autor aprobó, el 20/10, corregirlo a `net11.0-windows10.0.17763.0` y propagarlo al design, al proposal y a las tareas: [`design.md`](openspec/changes/archive/2026-10-20-conectar-puesto-y-receptor/design.md), [`Kiosk.csproj`](src/Intelektron.INComedor.Kiosk/Intelektron.INComedor.Kiosk.csproj) y el commit [`10b04ab`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/10b04ab).
6. **El primer CI rojo y su `.gitattributes`.** El CI falló por el fin de línea de `api/openapi.yaml` en el runner de Windows. El autor eligió fijar el fin de línea y no tocar las pruebas: [`.gitattributes`](.gitattributes), el commit [`6fc1964`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/6fc1964) y el workflow 4 de la sección 4.
7. **Las barreras de la acción 2.** Sobre la propuesta de la IA, el autor decidió el 29/10 cuatro cosas: gitleaks como CLI verificado por SHA-256, en lugar de la action de licencia propia, de TruffleHog o de sólo el secret scanning de GitHub; que el hook pida confirmación y no niegue, para poder aprobar una edición como la del punto 4; el hook en el repo más un puntero local, porque sus sesiones abren en la carpeta padre; y el control negativo de la CI en una rama descartable sin el secreto sintético, que no se sube al fork público. Evidencia: el [design de `imponer-invariantes-en-codigo`](openspec/changes/archive/2026-10-30-imponer-invariantes-en-codigo/design.md) (D4, D6, D7 y D9), la [corrida en verde](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40953247197) y el [control en rojo](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40953621473).
8. **Dos pruebas existentes, aprobadas en el gate.** La migración `0002` lleva la base a la versión 2, y dos pruebas afirmaban la versión 1. En el gate de `atender-seleccion-y-confirmacion`, el 30/10, el autor aprobó cambiar esas dos y nada más: la versión y el conteo de tablas de `AperturaBaseTests` y la versión de `ServicioDemostracionTests`. Evidencia: D1 del [design](openspec/changes/archive/2026-10-30-atender-seleccion-y-confirmacion/design.md), la tarea 1.2 de sus [tareas](openspec/changes/archive/2026-10-30-atender-seleccion-y-confirmacion/tasks.md), con el diff pegado, y el punto 1 de «Para el gate de David» en el [proposal](openspec/changes/archive/2026-10-30-atender-seleccion-y-confirmacion/proposal.md).
9. **Quitar el hook de tests.** Durante ese apply, el hook del punto 7 pedía confirmación en cada corrección de las pruebas nuevas del change, porque contaba como existente cualquier archivo de `tests/` ya presente en disco. Entre acotarlo o quitarlo, el autor eligió quitarlo; la regla sigue en [`AGENTS.md`](AGENTS.md), verificada en cada tarea con `git diff --stat -- tests/`. Evidencia: la fila «Hooks para invariantes» del [mapa](docs/aplicacion-del-curso.md) y la sección 3 de este registro.
10. **El gate de `recibir-lotes-idempotente` y lo que cambió en el apply.** El 30/10 el autor aprobó tal cual siete cambios a pruebas existentes (cuatro por la versión 3 y las tres del `501`, que se borran) y un doble fuera de los de impresora y lector. Al archivar el change anterior, un requirement quedó por encima del largo que admite `openspec validate --strict`, y eligió partirlo en dos con el texto tal cual. En el apply aprobó una línea más: el ayudante de prueba que simula la versión 1, que la migración `0003` rompía. Evidencia: «Para el gate de David» en el [proposal](openspec/changes/archive/2026-10-30-recibir-lotes-idempotente/proposal.md#para-el-gate-de-david), D9, D13 y D14 del [design](openspec/changes/archive/2026-10-30-recibir-lotes-idempotente/design.md) y las salidas de 1.1 y 2.3 en sus [tareas](openspec/changes/archive/2026-10-30-recibir-lotes-idempotente/tasks.md).
11. **La instancia de SQL Server.** La LocalDB del equipo del autor no arrancaba desde que se creó. En lugar de recrearla, eligió su instancia completa de SQL Server, y la instancia quedó en una variable de entorno con LocalDB por defecto, que es la de la CI. Evidencia: D1 del [design](openspec/changes/archive/2026-10-30-recibir-lotes-idempotente/design.md) y la salida de 4.2 en las [tareas](openspec/changes/archive/2026-10-30-recibir-lotes-idempotente/tasks.md).
12. **La elevación y el puerto de depuración.** El spike del 29/10 encontró que una app elevada ignora la variable documentada de WebView2 para abrir el puerto de depuración, así que el E2E no llegaba a la pantalla. En el gate del 30/10, el autor eligió:
    - que el puerto entre por código en todos los builds, inerte sin su variable, para que el EXE del Release sea el mismo que prueba el E2E, en lugar de un `#if DEBUG`;
    - tres variables y no una, porque aislar los datos, cambiar el receptor y abrir la depuración son cosas distintas.

    En el apply, la tarea que abre el puesto sin variables creaba la carpeta de demostración del usuario, que el paso del apply sólo dejaba leer. El autor eligió correrla y borrar la carpeta al final, porque antes no existía. Evidencia: los puntos 8 y 9 de «Para el gate de David» en el [proposal](openspec/changes/archive/2026-10-30-demostrar-flujo-de-punta-a-punta/proposal.md#para-el-gate-de-david), D2 y D3 del [design](openspec/changes/archive/2026-10-30-demostrar-flujo-de-punta-a-punta/design.md) y las salidas de 3.3 y 3.4 en las [tareas](openspec/changes/archive/2026-10-30-demostrar-flujo-de-punta-a-punta/tasks.md).

## Anexo A. Registro de la entrega 1 (29/09/2026)

Primera entrega, actualizada el 29/09/2026. Autor: David Walfisch.
Este registro cubre investigación, especificación, revisión y preparación
documental. Las siguientes entregas incorporarán el trabajo de implementación,
pruebas operativas y despliegue.

### Contexto, herramientas y verificación humana

Se utilizó Codex, con modelo configurado `gpt-6-astra`, para leer la documentación
existente, recuperar las conversaciones pertinentes, contrastar consignas y
preparar Markdown, diagramas editables y OpenAPI. La investigación previa del
26/09 generó una especificación institucional con la skill de documentos institucionales `itk-word`; esta fase reutiliza
su evidencia para formular el alcance académico.

En esta fase se distribuyeron análisis entre subagentes: requisitos/historias,
API/datos y revisión independiente. El agente principal integró resultados y
consultó criterios al autor. Se usaron lectura local, comparación SHA256, lectura
de chats, documentación oficial y validaciones estáticas. Las decisiones
de producto y de alcance fueron confirmadas por el autor.

Las fuentes empresariales y el historial completo se conservan en el entorno
interno. Este paquete usa cliente anonimizado y datos sintéticos. Las citas
siguientes son extractos reales del autor, o resúmenes expresamente identificados.
No se enlazan conversaciones privadas ni se publican documentos internos.

#### Herramientas y modelos por fase

| Fase | Herramienta y modelo registrado | Aporte |
|---|---|---|
| Investigación y especificación inicial | Codex, modelo configurado `gpt-6-astra` | Contraste de fuentes y generación documental con la skill `itk-word`. |
| Diseño académico | Codex, modelo configurado `gpt-6-astra` | Arquitectura, especificación, historias, datos, diagramas, OpenAPI y controles estáticos. |
| Revisión independiente | Claude Code, `claude-opus-5-5[1m]`, esfuerzo `max`, según el registro de la revisión | Contraste del planeamiento y detección de inconsistencias. |
| Revisión e integración | Codex, modelo configurado `gpt-6-astra` | Contraste de observaciones con fuentes, corrección de contratos y revisión de coherencia documental. |
| Evaluación simulada y refinamiento | Claude Code, `claude-opus-5-5[1m]` | Evaluación con una rúbrica orientativa y aplicación, por pedido del autor, de sus cuatro mejoras prioritarias: épicas e historias de tamaño real, credenciales y permisos locales, priorización y consistencia del contrato. |

La revisión cruzada mejoró la auditoría dentro de la transacción de negocio,
la diferenciación de motivos de rechazo y el detalle de historias y tickets.
Las observaciones se contrastaron con la especificación antes de integrarlas.

En la revisión final del diseño se distribuyeron tres focos entre subagentes:
consigna y presentación académica, coherencia de datos/contratos y viabilidad del
stack. El autor confirmó que las cuatro funcionalidades son obligatorias y que
el puesto debe seguir la hora de Windows y auditar sus saltos. Estas decisiones
se incorporaron al alcance, al plan de implementación y a los criterios de aceptación.

### 1. Descripción general del producto

**Prompt 1 — extracto real del pedido inicial, 26/09:**

> El sistema INComedor deberá poder recibir algún tipo de identificación desde diferentes dispositivos: a priori, consideremos que capturará un evento en un puerto serie (virtualizado a través de un dispositivo que se lo aporte con el código de identificación de la persona). También deberá poder comandar al un biomini de última generación conectado a USB. Como plataforma, usaremos una All-in-One de gama media, con pantalla táctil y Windows 11.

Resultado: definición de un kiosco con adaptadores, persistencia propia y relaciones
con INWeb. El extracto conserva la redacción original y omite referencias
confidenciales del caso empresarial.

**Prompt 2 — extracto real, 27/09:**

> Sobre el proyecto, analizá lo que estuvimos preparando sobre el INComedor, y generá con eso una base con todo que piden para el proyecto […]

Resultado: paquete de primera entrega con README, registro de IA, especificación,
historias, tickets, datos y contrato. El autor confirmó el recorte con adaptadores
de prueba y homologación posterior para INWeb/BioMini reales.

**Prompt 3 — aclaración real del autor sobre BioMini, 26/09:**

> El biomini es el de Suprema. ¿es el mismo que consideraste? En ese caso, la idea es que tenga los templates de las personas habilitadas, y a través de una búsqueda 1:n o una identificación previa con un código, y luego una verificación, se certifique que la persona es tal, y con su ID asociado se siga el resto del circuito.

Corrección humana incorporada: ambos flujos son requisitos del producto. Modelo,
SDK, compatibilidad y residencia de templates siguen sujetos a homologación.

### 2. Arquitectura del sistema

**Prompt 1 — aclaración real del autor, 27/09:**

> El INComedor debe nutrirse del INWeb y generar una base propia, como lo haría cualquier equipo, sin depender de que el INWeb esté en línea en ningún momento. Debe funcionar de modo completamente desatendido, con las últimas reglas que se le hayan configurado, y con una base local propia, que puede ser el SQL Lite si considerás que es más eficiente. Reformulá lo que sea necesario en el proyecto para asegurar este esquema.

Impacto: se reemplazó la propuesta inicial de commit central/ONLINE_ONLY por
autoridad local SQLite, arranque offline provisionado y sincronización eventual.
Se reformularon arquitectura, requisitos, API, datos, historias y pruebas.

**Prompt 2 — aclaración real sobre varios puestos, 27/09:**

> La mayoría de los esquemas tendrá un único puesto. En caso de más de uno, y donde se necesite auditar, sí o sí deberán estar informando al INWeb en línea, y los que sean deberán además actualizar inmediatamente su registro también. Existe una ventana potencial donde la persona igual saque más de uno, que deberá generar un aviso para su posterior auditoría.

Impacto: publicar y propagar hechos inmediatamente en el perfil multipuesto
auditado, actualizar registros locales sin dobleconteo y generar avisos por
excesos concurrentes. No se promete exclusión global instantánea.

**Prompt 3 — decisión real del autor, 29/09:**

Ante las opciones de continuidad, suspensión o configuración por instalación,
el autor respondió «3», eligiendo «Permitir configurarlo por instalación, con
continuidad local como valor inicial».

Impacto: D-A02 cerrada; se documentaron CONTINUE_LOCAL y SUSPEND_NEW_CONSUMPTIONS,
persistencia de la política, incidencias y recuperación del feed. La suspensión
afecta confirmaciones nuevas; conserva replay y comprobantes ya registrados.
Se actualizaron el contrato, el modelo y los escenarios, sin ejecutar el producto.

### 3. Modelo de datos

**Prompt 1 — resumen de instrucción al subagente de datos, no cita literal:**

Diseñar padrón/configuración versionados, intención/consumo/contador locales,
ticket, outbox, lotes y acuses; templates separados; incorporar consumos observados
de otros puestos e impedir que un eco propio incremente el contador otra vez.

Resultado: diccionario y diagrama ER con restricciones, ubicación y responsabilidades.
Se revisan claves de intención, evento, lote, consumo y copia de ticket. Los datos
de ejemplo son sintéticos; no se vuelcan huellas, tarifas ni legajos empresariales.

**Prompt 2 — resumen del pedido y decisiones reales del autor, 29/09:**

Incorporar interfaz administrativa y diagnóstico por componente, emisión futura
para terceros internos/externos y bitácoras general/interacciones externas a DB,
de texto plano mensual AAAAMM. Ante la consulta de fallo de archivo, el autor eligió
continuar con respaldo durable SQLite y reconstrucción con intervalo degradado.
Para terceros confirmó: «Sólo reglas del beneficiario, sin excepciones de cupo».

Resultado: clasificación funcional/no funcional/futura, roles, matriz de diagnóstico,
archivos independientes y cola de recuperación; RF40–46 y AT37–44. Se actualizaron
arquitectura, datos, UX, historias/tickets y límites del contrato API.

**Prompt 3 — resumen del pedido y decisión real del autor, 29/09:**

Contrastar los nombres de tablas, campos y objetos C# de INWeb para que INComedor
sea coherente con su ecosistema. El autor eligió adoptar la convención en
persistencia y C#, con un mapa explícito que conserve el contrato JSON y las
identidades; solicitó incorporar el resultado a la documentación académica.

Resultado: contraste de DDL, entidades y mappings; perfil `tbComedor...`, columnas
mnemónicas y negocio español. Se separaron claves enteras nativas y UUID del
módulo. La revisión corrigió una propiedad con el mismo nombre que su clase:
`VersionConfiguracion.NumeroVersionConfiguracion`. Se incorporaron los mapas del ER
(42 entidades y 233 atributos entonces; 44 y 256 tras el refinamiento), ADR11, D-C18, RNF15 y AT51, con trabajo
asignado a TK03/TK01. Codex integró los cambios y validó en copia el paquete y el
contrato estático; las pruebas de producto permanecen previstas, no ejecutadas.

### 4. Especificación de la API

**Prompt 1 — resumen de instrucción al subagente de API, no cita literal:**

Preparar tres operaciones OpenAPI 3.1: configuración completa, envío de lotes y
consulta de acuse. Modelar errores, identidad del equipo, idempotencia y conflictos.
La recepción central no vuelve a autorizar hechos ya confirmados localmente.

Resultado: contrato propuesto con ejemplos y validación estática. El diccionario
define los campos de la distribución biométrica y de la propagación de consumos
pares; su transporte se especificará en M0 como contrato complementario. OpenAPI
desarrolla las tres operaciones principales seleccionadas; en el refinamiento
posterior incorporó credenciales y permisos locales al snapshot.

### 5. Historias de usuario

**Prompt 1 — resumen de instrucción al subagente SDD, no cita literal:**

Construir tres historias con precondiciones y escenarios verificables: consumo
autónomo, continuidad/sincronización y recuperación de comprobante. Trazar ambos
flujos biométricos, fallas, reglas y dependencias, distinguiendo producto y demo.

Resultado: HU01–HU03 y escenarios vinculados. El autor corrigió la autoridad de
decisión y la política entre puestos; esas correcciones prevalecen sobre el borrador previo.
La evaluación simulada señaló que las tres tenían tamaño de épica; se rotularon
como épicas y se agregaron HU01.1, HU02.1 y HU02.2 con un backlog de historias hijas.

### 6. Tickets de trabajo

**Prompt 1 — resumen de instrucción de planificación, no cita literal:**

Derivar tres tickets representativos de backend/dominio, frontend y datos con
dependencias, entregables y definición de terminado; separar homologación real y
trabajo adicional del alcance de esos tres ejemplos.

Resultado: TK01–TK03 con criterios de aceptación y evidencia requerida para
considerar terminada cada tarea. En el refinamiento pasaron a épicas técnicas y se
agregaron TK03.1, TK02.1 y TK01.1, de tamaño tarea, con AT propios y un responsable.

### 7. Pull requests

El registro de prompts para revisión de código se incorporará durante las
entregas de implementación, junto con los enlaces de las revisiones realizadas.

### Intervención humana

El autor definió plataforma, dispositivos, autonomía, ambos modos biométricos,
alcance académico, administración local y comportamiento entre puestos. Corrigió
la propuesta inicial de autorización central para convertirla en una operación
autónoma con reglas y persistencia locales.

También confirmó anonimización del cliente, datos sintéticos, cuatro
funcionalidades obligatorias y continuidad ante saltos del reloj con auditoría
del cambio y del riesgo de fecha/cupo. Las decisiones vigentes se conservan en
[decisiones.md](docs/decisiones.md).

La IA se utilizó como asistencia a la investigación, al diseño y a su revisión.
Las decisiones de consumo del producto serán reglas determinísticas de la aplicación.

### Corrección posterior a la revisión independiente — 29/09/2026

Herramienta: Codex; modelo: gpt-6-astra; trabajo inline, sin subagentes. El autor pidió aplicar una matriz acotada de coherencia documental, conservar antecedentes y verificar en copia, sin publicar ni implementar. Confirmó ambas políticas multipuesto en la demo, 6 h semanales, M0 objetivo 06/10, códigos no reasignables, revocación recibida sin programación futura, roles sin herencia, cuenta individual local para mantenimiento y motivo externo genérico para código futuro.

La intervención ajustó especificación, contratos, responsabilidades y planificación; agregó AT56 dentro de administración. Las decisiones técnicas de provisión, protección de cuentas e historial de códigos quedan abiertas en M0. Los controles estáticos y simulaciones de fixtures no se presentan como ejecución del producto. El autor conserva la decisión de formalizar la entrega.

Este anexo es el registro de la entrega 1 a su fecha, 29/09/2026. Lo vigente está en las secciones 1 a 5 de este archivo.

## Anexo B. Registro de la entrega 2 (20/10/2026)

### Entrega 2

Actualizado el 20/10/2026. Tras la devolución de LIDR de la entrega 1 (alcance excesivo), el autor aceptó el recorte (D-C24) y adoptó OpenSpec: cada avance es un change con proposal, design, tasks y, cuando cambia comportamiento, specs; el autor revisa el change antes de aplicarlo.

#### IA en las seis fases

| Fase | Herramienta y modelo | Evidencia |
|---|---|---|
| Análisis | Claude Code, `opus` (Opus 5.5), esfuerzo medium | Change `fijar-primer-corte` (commit `36b20d8`): la IA contrastó la devolución, el material del curso y el paquete; el autor decidió el recorte. Archivado en `openspec/changes/archive/` |
| Diseño | Claude Code, `opus` (Opus 5.5) | Proposal, specs, design y tasks de `registrar-consumo-atomico` (`644bfb3`, ajustado en `27f5709`) y de `conectar-puesto-y-receptor` (`b5ea3a0`), con la alternativa descartada de cada decisión. El autor los aprobó en el gate antes de cada apply |
| Implementación | Claude Code con `/openspec-apply-change`, `sonnet` (Sonnet 5.5) | `registrar-consumo-atomico` (TK03.1), 26 de 26 tareas: `5dc9614`, `97d7717`, `a48bdcc`, `9f80550`. `conectar-puesto-y-receptor` (TK02.1 y TK01.1 como andamios), 15 de 15 tareas: `ea6e7d5`, `f7337e8`, `71f4f5d`. Los dos changes siguen sin archivar |
| Pruebas | Claude Code, `sonnet`: cada test se escribió desde un escenario de la spec y se vio fallar antes de implementar | 61 pruebas en verde con `dotnet test INComedor.sln`: 21 de Domain, 27 de Infrastructure.Sqlite sobre un archivo SQLite real, 9 del Kiosk y 4 del receptor. Ensayo de caídas y concurrencia en `34e21d8`, código no filtrado en `256e87c` |
| Despliegue | Claude Code, `sonnet`: workflow de CI generado y revisado | `.github/workflows/ci.yml` (`c122028`): restore, build y test en `windows-latest`. Todavía sin primera corrida: la rama no se publicó; no hay ejecución que mostrar |
| Mantenimiento | `/openspec-verify-change` y `openspec validate --all --strict`; `AGENTS.md` como contexto vivo | `AGENTS.md` actualizado con los comandos de run y el gotcha del TFM (`7a57612`); `openspec validate --all --strict`: 2 de 2 válidos; salida de verify de cada change en el borrador de su PR. Los PR todavía no están abiertos |

#### Prompt representativo: fijar el primer corte

**Extracto del paso ejecutado el 19/10, redactado en una sesión anterior y lanzado por el autor:**

> Tomá como dadas las decisiones de David del 18/10/2026, que enmienda D-C19, y del 19/10 […]: el recorte de LIDR es la versión que se presenta (entrega 2 y final). […] Creá el change con `openspec new change fijar-primer-corte`. En su `.openspec.yaml` agregá `skip_specs: true`, porque decide alcance y contexto, no comportamiento. […] Mostrale a David los tres artefactos […] Es la revisión humana antes de aplicar que el curso llama «el oro de la sesión». No sigas sin su aprobación.

Resultado: la IA propuso cinco historias must (HU01.1, HU01.3, HU02.1, HU02.2 sin el escenario F y HU03.1) y una should (HU01.2 reducida al código), y marcó HU03.1 como la must más discutible. Presentó dos ajustes posibles: pasar HU03.1 a should o quitar la should.

**Decisión del autor en la revisión del change:** «Aprobar tal cual». Con esa aprobación se aplicaron D-C24, el plan, el README, `AGENTS.md` y la plantilla de PR.

#### Prompt representativo: aplicar `registrar-consumo-atomico`

**Resumen de la instrucción, no cita literal.** Sesión nueva (`/clear`) con `sonnet`, y `/openspec-apply-change registrar-consumo-atomico`. El paso pide cumplir las tareas por grupo y comitear cada grupo por rutas; escribir cada test antes de implementar, correrlo y verlo fallar por la razón esperada (si pasa sin cambios, romper la línea que lo sostiene, verlo fallar y restaurarla); no modificar un test que ya se vio en verde sin aprobación; leer completa la migración antes de aplicarla; y parar a preguntar sólo si nada avanza sin el autor.

Resultado: solución .NET 11 con dominio de fecha de negocio y cupo, migración `0001`, unidad de trabajo con `BEGIN IMMEDIATE`, replay por `IntentId` y ensayos de caída y concurrencia; 48 pruebas en verde.

**Ajustes humanos:** el autor leyó y aprobó la migración `0001` en la tarea 4.2. Aprobó además cambiar el `DisplayName` de una prueba ya vista en verde (escenario «Un rechazo tampoco lo registra»), porque la regla del proyecto prohíbe tocar un test sin su aprobación.

#### Prompt representativo: aplicar `conectar-puesto-y-receptor`

**Resumen de la instrucción, no cita literal.** El mismo patrón: `/clear`, `sonnet` y `/openspec-apply-change conectar-puesto-y-receptor`. Pide mover los eventos de outbox a `Contracts` sin cambiar su JSON, crear el receptor `Sync.Api` que responde `501` sin leer el cuerpo y sin acusar, y el puesto `Kiosk` con una pantalla Razor de demostración sobre SQLite real. Pide revisar el receptor contra OWASP Top 10, no tocar los 48 tests existentes y correr la ventana de verdad antes de dar por cerrada la tarea 3.6.

Resultado: cinco proyectos en `src/`, cuatro en `tests/` y 61 pruebas en verde; la ventana mostró `AUTHORIZED` en el primer toque y `LOCAL_LIMIT_REACHED` en el segundo.

**Ajustes humanos:** el TFM `net11.0-windows` del diseño era incorrecto: al correr la ventana se cerraba al abrir (`Microsoft.Windows.SDK.NET`) y el proyecto de pruebas fallaba con NU1201. Lo detectó la corrida de la ventana, no el build ni las pruebas. El autor aprobó (20/10) corregirlo a `net11.0-windows10.0.17763.0` en `design.md` y propagarlo a `proposal.md` y a las tareas 3.1 y 3.2 (`10b04ab`).

Este anexo es el registro de la entrega 2 a su fecha, 20/10/2026. Lo vigente está en las secciones 1 a 5 de este archivo.
