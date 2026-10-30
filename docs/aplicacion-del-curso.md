# Cómo se aplicó el curso en INComedor

Actualizado el 29/10/2026. Este documento responde dos preguntas del evaluador: ¿aplicó el autor lo que enseña el curso AI4Devs?, y ¿respondió a lo que pidió la devolución de la entrega 1? Cada práctica figura con su estado y con la evidencia que el repo muestra hoy, o con lo que falta y con el change o la acción que lo trae, o con el motivo por el que no aplica.

## Cómo leerlo

El curso lo dice dos veces: «Usa solo lo que tenga sentido para tu proyecto» (L58:269) y «No intentes incluir todo lo aprendido de una sola vez» (L58:55). Por eso el mapa muestra cada práctica aplicada con su evidencia y cada práctica descartada con su motivo.

Cada fila tiene uno de tres estados:

- **Aplicada**: la última celda enlaza un archivo del repo, un commit, un PR o una corrida del CI que lo prueba. Un enlace a un commit o a una corrida va además de un enlace relativo a un archivo.
- **Pendiente**: la práctica sirve al proyecto y todavía no está. La última celda nombra el change o la acción que la trae.
- **No aplica**: la última celda da el motivo.

Las citas siguen la numeración del curso v20261027, la vigente: `L58:232` es la lección 58, línea 232. Llevan línea sólo las citas de las lecciones 58 y 59 que se leyeron en esa versión; el resto va por número y título corto, y las sesiones (`sesión N`) sin minuto ni página. Las acciones 1 a 3 son las del plan de adopción del análisis contra el curso (28/10/2026); la 1 es el change que escribió este documento, la 2 puso en código tres reglas que eran texto (`imponer-invariantes-en-codigo`: el hook de tests y la CI con specs, dependencias y secretos) y la 3 agrega el E2E y la evidencia de funcionamiento.

## El mapa

### Análisis y planificación

| Práctica | Lección | Estado | Evidencia o motivo |
|---|---|---|---|
| De 3 a 5 historias must y 1 o 2 should | lección 58 (L58:55) | Aplicada | D-C24 recorta de doce historias must a cinco must y una should: [decisiones](decisiones.md), [`fijar-primer-corte`](../openspec/changes/archive/2026-10-19-fijar-primer-corte/proposal.md) y el commit [`36b20d8`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/36b20d8). |
| Revisar el backlog generado en tres ejes: granularidad, features fuera del MVP y criterios genéricos | sesión 4 | Aplicada | Granularidad: las tres historias de tamaño épica se partieron en historias de tamaño real (HU01.1, HU02.1 y HU02.2) en [historias y tickets](historias-y-tickets.md). Features fuera del MVP: al fijar D-C24 se tachó lo que no servía al flujo del primer corte y pasó a la segunda ola ([`fijar-primer-corte`](../openspec/changes/archive/2026-10-19-fijar-primer-corte/proposal.md)). Criterios: en lugar de criterios genéricos, cada escenario de la [spec viva](../openspec/specs/registro-consumo/spec.md) declara su origen, casi siempre un AT. |
| INVEST como filtro y non-goals por historia | lección 49 | Aplicada | La regla de proposal de [`config.yaml`](../openspec/config.yaml) exige non-goals; los tres [proposals archivados](../openspec/changes/archive/2026-10-20-registrar-consumo-atomico/proposal.md) los llevan. |
| De ticket a change: la primera tarea de una historia es generar su change | lección 49, sesión 4 | Aplicada | TK03.1, TK02.1 y TK01.1 son changes de OpenSpec: [`registrar-consumo-atomico`](../openspec/changes/archive/2026-10-20-registrar-consumo-atomico/proposal.md) y [`conectar-puesto-y-receptor`](../openspec/changes/archive/2026-10-20-conectar-puesto-y-receptor/proposal.md). |
| Definición de terminado por tipo de trabajo (feature, bug, refactor, docs, spike) | lección 52 | Pendiente | Está la de feature, en la [plantilla de PR](../.github/pull_request_template.md) y en los «Terminado» de [historias y tickets](historias-y-tickets.md). La de spike llega con el spike del E2E (acción 3). |
| Spike con tope en horas para lo incierto | sesión 4 | Pendiente | Acción 3: spike de 2 h para el E2E sobre WebView2, con tope y un E2E alternativo si no cierra. |
| Capacidad con 30 % de margen | lección 50 | Aplicada | El [plan académico](plan-academico.md#tallas-por-change-y-colchón) divide cada talla por 0,7 y lo aplicó por primera vez a [`evaluar-habilitacion-local`](../openspec/changes/archive/2026-10-29-evaluar-habilitacion-local/proposal.md#tallas): S de David de 2 a 4 h pasa a 2,9 a 5,7 h, y M de la IA de 4 a 6 h pasa a 5,7 a 8,6 h. |
| La IA estima como un par, no como única fuente | lección 50 | Aplicada | [`evaluar-habilitacion-local`](../openspec/changes/archive/2026-10-29-evaluar-habilitacion-local/proposal.md#tallas) registra la talla de David (S) y la de la IA (M) sin promediarlas, y la diferencia va al gate; las dos y las horas reales del apply están en el [plan académico](plan-academico.md#tallas-por-change-y-colchón). |
| Linear o GitHub Projects con MCP | lección 51 | No aplica | Proyecto de una persona: alcanzan los changes de OpenSpec y los PR. Tampoco aplican planning poker ni velocity segmentada. |

### Especificación con OpenSpec

| Práctica | Lección | Estado | Evidencia o motivo |
|---|---|---|---|
| `openspec init` con las herramientas que se usan | lección 29 | Aplicada | Las skills de OpenSpec para Claude Code y para Codex están versionadas: [`.claude/skills`](../.claude/skills/openspec-apply-change/SKILL.md) y [`.agents/skills`](../.agents/skills/openspec-apply-change/SKILL.md). Alta en el commit [`36b20d8`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/36b20d8) y regeneradas con la CLI 1.15.0 en [`6708409`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/6708409). |
| Contexto y reglas por artefacto en `openspec/config.yaml` | sesión 2 | Aplicada | [`openspec/config.yaml`](../openspec/config.yaml): contexto del primer corte y reglas de proposal, specs, design y tasks. |
| Ciclo proposal, specs delta, design y tasks | lección 32, sesión 2 | Aplicada | Tres changes completos y archivados: [`fijar-primer-corte`](../openspec/changes/archive/2026-10-19-fijar-primer-corte/tasks.md), [`registrar-consumo-atomico`](../openspec/changes/archive/2026-10-20-registrar-consumo-atomico/tasks.md) y [`conectar-puesto-y-receptor`](../openspec/changes/archive/2026-10-20-conectar-puesto-y-receptor/tasks.md). |
| Escenarios con error o borde, no sólo el camino feliz | lección 32 | Aplicada | Regla de specs en [`config.yaml`](../openspec/config.yaml) y su efecto en la [spec viva `registro-consumo`](../openspec/specs/registro-consumo/spec.md): 8 requirements y 22 escenarios. |
| Coherencia entre los cuatro artefactos | sesión 2 | Aplicada | Cuando correr la ventana mostró que el TFM del Kiosk era otro, el design de [`conectar-puesto-y-receptor`](../openspec/changes/archive/2026-10-20-conectar-puesto-y-receptor/design.md) se corrigió en el mismo commit que el código ([`71f4f5d`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/71f4f5d)) y, con la aprobación del autor, la corrección se propagó a proposal y tasks antes de archivar ([`10b04ab`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/10b04ab)). |
| `/clear` antes de `apply`; modelo fuerte para proponer | lección 32, sesión 2 | Aplicada | Proponer con `opus` y aplicar con `sonnet`, en sesiones distintas, como cuenta el [registro de IA](../prompts.md). |
| Validar con `--strict` y archivar apenas se mergea | lección 32 | Aplicada | `openspec validate --all --strict` está en [`AGENTS.md`](../AGENTS.md) y en la [plantilla de PR](../.github/pull_request_template.md); los archivados son [`8a7f72e`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/8a7f72e) y [`14ae7a3`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/14ae7a3), con sus [specs vivas](../openspec/specs/registro-consumo/spec.md). Desde la acción 2, el job `specs` de [`ci.yml`](../.github/workflows/ci.yml) la corre en cada push y en cada PR ([corrida 40953247197](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40953247197)). |
| Si cambia el requisito, primero la spec | ebook de SDD | Aplicada | Regla de [`AGENTS.md`](../AGENTS.md): «Si cambia un requisito, primero se cambia el change». Un requisito nuevo va a su propio change antes de tocar código: RB02 completa quedó para `evaluar-habilitacion-local` (workflow 2 de [`prompts.md`](../prompts.md)). Lo que el apply descubrió llegó al change en el mismo commit que el código o después, pero siempre antes de archivar: el TFM del Kiosk ([`71f4f5d`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/71f4f5d) y [`10b04ab`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/10b04ab)) y tres precisiones de [`registrar-consumo-atomico`](../openspec/changes/archive/2026-10-20-registrar-consumo-atomico/design.md) que no cambian el requisito ([`27f5709`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/27f5709)). Ningún código quedó sin su change al día. |
| No reemplazar la revisión humana por el revisor del agente | ebook de SDD | Aplicada | [`AGENTS.md`](../AGENTS.md): «la revisión del delta no se delega». Cada change esperó el gate del autor antes de aplicarse; el registro de esos gates está en [`prompts.md`](../prompts.md). |

### Contexto del agente

| Práctica | Lección | Estado | Evidencia o motivo |
|---|---|---|---|
| `AGENTS.md` mínimo, en cada repo | sesión 1, lecciones 23 y 40 | Aplicada | [`AGENTS.md`](../AGENTS.md): 64 líneas, menos de las 200 que el curso pone de tope. |
| Contenido que el código no muestra: invariantes, comandos no triviales, restricciones, «never commit secrets»; sin árbol de carpetas | lección 40 | Aplicada | [`AGENTS.md`](../AGENTS.md): secciones «Invariantes que el código no muestra», «Reglas» y «Comandos», y lo que no se construye. |
| Una sola fuente para varias herramientas | lecciones 23 y 42 | Aplicada | [`CLAUDE.md`](../CLAUDE.md) contiene sólo `@AGENTS.md`; Codex lee `AGENTS.md` directo. |
| Archivos del agente tratados como código | lecciones 23 y 42 | Aplicada | `AGENTS.md`, `CLAUDE.md` y las skills están versionados y cambian por commit: [`7a57612`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/7a57612) sumó los comandos de run y el gotcha del TFM a [`AGENTS.md`](../AGENTS.md). |
| Explore, Plan, Execute | lección 41 | Aplicada | Skills de [explorar](../.claude/skills/openspec-explore/SKILL.md), [proponer](../.claude/skills/openspec-propose/SKILL.md) y [aplicar](../.claude/skills/openspec-apply-change/SKILL.md), con el gate humano entre proponer y aplicar. |
| Hooks para invariantes, no prompts | lecciones 40 a 42 | Aplicada | Un hook `PreToolUse` en [`.claude/settings.json`](../.claude/settings.json) corre [`confirmar-edicion-de-test.ps1`](../.claude/hooks/confirmar-edicion-de-test.ps1), que pide confirmación antes de editar un test existente; un test nuevo pasa. Un puntero fuera del repo lo activa también en las sesiones que abren en la carpeta padre. No cubre a Codex ni las escrituras por shell: para eso la regla sigue en [`AGENTS.md`](../AGENTS.md), que ahora nombra su barrera. |
| Context7 o Microsoft Learn MCP para APIs recientes | lección 64 | Aplicada | La sesión que propuso [`registrar-consumo-atomico`](../openspec/changes/archive/2026-10-20-registrar-consumo-atomico/design.md) leyó por el MCP de Microsoft Learn las páginas de transacciones, tipos y errores de `Microsoft.Data.Sqlite`, y el design apoya ahí sus decisiones: el `Guid` guardado como TEXT y el reintento ante busy o locked. El servidor se configura en la herramienta; el repo no lleva `.mcp.json`. |
| `llms.txt` | lección 65 | No aplica | Costo bajo, pero no suma frente a `AGENTS.md` más las specs vivas, que ya son el contexto corto del agente. |

### Implementación

| Práctica | Lección | Estado | Evidencia o motivo |
|---|---|---|---|
| Plan o contrato antes del código; «si no sabés, preguntá» | sesiones 9 y 10 | Aplicada | Es el ciclo de OpenSpec: ningún código se escribió sin un change aprobado, y el apply pide parar a preguntar ([`openspec-apply-change`](../.claude/skills/openspec-apply-change/SKILL.md)). |
| Revisar el diff y aceptar por partes; rechazar lo que excede la orden | charla «De ticket a pull request en 30 minutos» | Aplicada | Un commit por grupo de tareas, revisable por separado: [`5dc9614`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/5dc9614), [`97d7717`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/97d7717), [`a48bdcc`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/a48bdcc) y [`9f80550`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/9f80550), según las [tareas](../openspec/changes/archive/2026-10-20-registrar-consumo-atomico/tasks.md). El diff más delicado, la migración `0001`, lo leyó entero el autor antes de aplicarlo (tarea 4.2). |
| Una tarea, un chat; un commit por avance | sesión 13 | Aplicada | Sesión nueva por change y commit por grupo, por rutas ([`AGENTS.md`](../AGENTS.md), regla de commits). Ejemplo: los cuatro grupos de [`conectar-puesto-y-receptor`](../openspec/changes/archive/2026-10-20-conectar-puesto-y-receptor/tasks.md) están en [`ea6e7d5`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/ea6e7d5), [`f7337e8`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/f7337e8), [`71f4f5d`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/71f4f5d) y [`7a57612`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/7a57612). |
| Capas o hexagonal con criterio | lección 95 | Aplicada | Proyectos separados por responsabilidad: [Domain](../src/Intelektron.INComedor.Domain/EvaluadorCupo.cs), [Infrastructure.Sqlite](../src/Intelektron.INComedor.Infrastructure.Sqlite/UnidadConfirmacion.cs), [Contracts](../src/Intelektron.INComedor.Contracts/Eventos/EventosSalida.cs), [Kiosk](../src/Intelektron.INComedor.Kiosk/MainWindow.xaml.cs) y [Sync.Api](../src/Intelektron.INComedor.Sync.Api/Program.cs). Sin hexagonal completo en el MVP. |
| Value objects con invariantes y entidades no anémicas | lección 95 | Aplicada | [`CodigoIntencion`](../src/Intelektron.INComedor.Domain/CodigoIntencion.cs), [`FechaNegocio`](../src/Intelektron.INComedor.Domain/FechaNegocio.cs) y [`ClaveCupo`](../src/Intelektron.INComedor.Domain/ClaveCupo.cs), con sus [pruebas de invariantes](../tests/Intelektron.INComedor.Domain.Tests/ValueObjectsTests.cs). |
| Dependencias mínimas | sesión 10 | Aplicada | Sin ORM en el puesto: `Microsoft.Data.Sqlite` y SQL explícito para RB06, por regla de [`AGENTS.md`](../AGENTS.md) y con versiones fijadas en [`Directory.Packages.props`](../Directory.Packages.props). |
| Componentes pequeños, estados de UI y accesibilidad WCAG 2.2 AA | lección 101, sesión 10 | Pendiente | Reposo, selección, confirmando, resultado y error, con foco y teclado: `atender-seleccion-y-confirmacion`. La pantalla de demostración actual tiene un solo botón. |
| Refactor con tests de caracterización | sesión 9 | Aplicada | [`ea6e7d5`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/ea6e7d5) movió los eventos de outbox a `Contracts` sin cambiar su JSON, y [`ContratoEventosTests`](../tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/ContratoEventosTests.cs) fijó ese JSON. |

### Pruebas

| Práctica | Lección | Estado | Evidencia o motivo |
|---|---|---|---|
| TDD: el humano fija el criterio; un test que falla antes de implementar | lecciones 79 y 82 | Aplicada | Regla de todo el change: cada test se escribe, se corre y se ve fallar por la razón esperada ([`tasks.md`, línea 3](../openspec/changes/archive/2026-10-20-registrar-consumo-atomico/tasks.md)). Regla de [`AGENTS.md`](../AGENTS.md) y casilla de la plantilla de PR. |
| Mockear sólo en las fronteras; base real efímera | lecciones 81 y 87 | Aplicada | Cada prueba de persistencia abre un [archivo SQLite temporal](../tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/ArchivoTemporal.cs) y nunca reemplaza la base. Los únicos dobles son de fronteras: el reloj ([`RelojFijo`](../tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/RelojFijo.cs)) y los puntos de falla que simulan una caída ([`PuntosDeFallaDePrueba`](../tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/PuntosDeFallaDePrueba.cs)). La impresora y el lector todavía no tienen adaptador en el código; cuando lo tengan, la regla de [`AGENTS.md`](../AGENTS.md) limita a ellos los dobles. |
| Casos críticos del producto: doble toque, caída tras el commit, replay | devolución de LIDR | Aplicada | [`CaidasYConcurrenciaTests`](../tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/CaidasYConcurrenciaTests.cs) y [`ReplayTests`](../tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/ReplayTests.cs), sobre SQLite real; commit [`34e21d8`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/34e21d8). |
| Integración del receptor con `WebApplicationFactory` | lección 108 | Aplicada | [`ReceptorEsqueletoTests`](../tests/Intelektron.INComedor.Sync.Api.Tests/ReceptorEsqueletoTests.cs) levanta el receptor en memoria. Hoy responde `501`; las pruebas del receptor de verdad llegan con `recibir-lotes-idempotente`. |
| E2E vertical del flujo principal | lección 58 (L58:96) y lección 108, sesión 11 | Pendiente | Acción 3: Playwright por CDP sobre el WebView2 tras un spike, o un E2E sobre la raíz de composición si el spike no cierra. El escenario ya está escrito en [`docs/pruebas.md`](pruebas.md). |
| Romper el test a propósito; mutación en rutas críticas | lección 82, sesión 7 | Pendiente | Backlog posterior a la acción 3: Stryker.NET sólo sobre la transacción RB06. Mientras tanto, el apply rompe la línea que sostiene un test que pasó sin cambios y lo ve fallar. |
| BDD sin segunda fuente de verdad | sesión 11 | No aplica | No se agrega Reqnroll: los escenarios de OpenSpec ya son el Given/When/Then y cada test cita su escenario. |
| Cobertura como señal, no como meta | lección 81 | Pendiente | Backlog posterior a la acción 3: reportarla en la CI, sin umbral. Hoy la CI no la mide. |
| No delegar la verificación a la IA | sesión 7 | Aplicada | El autor leyó y aprobó la migración `0001` (tarea 4.2) y aprobó el cambio de una prueba ya vista en verde, como cuenta [`prompts.md`](../prompts.md). La tarea 4.2 de las [tareas](../openspec/changes/archive/2026-10-20-registrar-consumo-atomico/tasks.md) exige mostrarle el SQL completo y esperar su aprobación. |

### Datos y base de datos

| Práctica | Lección | Estado | Evidencia o motivo |
|---|---|---|---|
| Revisión humana de toda migración | lección 88 | Aplicada | La migración [`0001`](../src/Intelektron.INComedor.Infrastructure.Sqlite/Migraciones/0001_transaccion_consumo.sql) se leyó entera antes de aplicarla y el autor la aprobó: commit [`a48bdcc`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/a48bdcc). |
| Sin datos reales; escalera del esquema a los datos ficticios | sesión 8 | Aplicada | Sólo datos sintéticos, por regla de [`AGENTS.md`](../AGENTS.md): [fixture de pruebas](../tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/FixtureSintetico.cs) y [semilla de la demostración](../src/Intelektron.INComedor.Kiosk/Demostracion/semilla_demostracion.sql). |
| Transacción y auditoría del código que usa la base | sesión 8 | Aplicada | RB06 es una sola transacción en [`UnidadConfirmacion`](../src/Intelektron.INComedor.Infrastructure.Sqlite/UnidadConfirmacion.cs); [`CaidasYConcurrenciaTests`](../tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/CaidasYConcurrenciaTests.cs) corta antes del commit y entre el commit y el resultado: no queda rastro a medias, y la recuperación devuelve los mismos identificadores. |
| EXPLAIN e índices medidos | lección 87 | No aplica | Con el volumen del MVP, una base local de un puesto, no hace falta. |
| Usuario de base de sólo lectura para el agente | sesión 8 | No aplica | SQLite local de prueba: no hay usuarios de base. |

### Seguridad y privacidad

| Práctica | Lección | Estado | Evidencia o motivo |
|---|---|---|---|
| Secretos fuera del repo y escaneo en la CI | lección 9, sesión 13 | Aplicada | La regla «Never commit secrets» de [`AGENTS.md`](../AGENTS.md) nombra su barrera: el job `secretos` de [`ci.yml`](../.github/workflows/ci.yml) corre gitleaks 8.30.1, verificado por SHA-256, con [`escanear-secretos.sh`](../.github/scripts/escanear-secretos.sh) sobre la historia propia desde `bcde5c4`, y falla también si no escaneó los commits esperados ([corrida 40953247197](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40953247197)). El secreto sintético del control negativo se probó sólo en local, para no subirlo al fork público. |
| `.env.example`, o la línea que diga que no hace falta | lección 9, sesión 13 | Pendiente | Acción 3, en el «Cómo revisar» del README. |
| Dependencias vulnerables | sesiones 13 y 15 | Aplicada | El paso «Dependencias vulnerables» de [`ci.yml`](../.github/workflows/ci.yml) corre [`auditar-dependencias.ps1`](../.github/scripts/auditar-dependencias.ps1), que lee el JSON de `dotnet list package --vulnerable --include-transitive` porque el comando sale 0 aunque encuentre algo: en verde en la [corrida 40953247197](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40953247197) y en rojo, con `Newtonsoft.Json` 12.0.1, en el [control 40953621473](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40953621473). |
| OWASP Top 10 como criterio fijo del contexto | lección 140, charla de adopción 2 | Aplicada | Regla de [`AGENTS.md`](../AGENTS.md) y casilla de la [plantilla de PR](../.github/pull_request_template.md). El receptor se revisó contra A01, A03, A05 y A09 en la [tarea 2.4](../openspec/changes/archive/2026-10-20-conectar-puesto-y-receptor/tasks.md). |
| Privacidad por diseño, minimización y datos sintéticos | lecciones 71 y 73 | Aplicada | El código de identificación no es un secreto, pero no se escribe en logs, tickets ni eventos ([`AGENTS.md`](../AGENTS.md)), y [`CodigoProtegidoTests`](../tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/CodigoProtegidoTests.cs) lo verifica; cliente anonimizado, sin templates. |
| Clasificación por AI Act y DPIA | lección 71 | No aplica | El primer corte no usa IA en el producto ni biometría. La biometría es segunda ola y exige revisión legal antes de usar datos reales; el curso no cubre la ley argentina de datos personales. |
| Presidio, Privacy Vault y modelos locales | lecciones 73, 74 y 76 | No aplica | No se pasan datos personales reales a ningún modelo y el producto no incluye IA. |

### Despliegue y CI

| Práctica | Lección | Estado | Evidencia o motivo |
|---|---|---|---|
| Pipeline mínimo en cada PR: compila y prueba | sesión 13, lección 129 | Aplicada | [`ci.yml`](../.github/workflows/ci.yml) (commit [`c122028`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/c122028)): restore, build `-warnaserror` y test en `windows-latest`. En verde: [corrida 40795276016](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40795276016) y [corrida 40802117357](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40802117357). |
| Crecer por escalones y pedir el mínimo a la IA | sesión 15 | Aplicada | El primer escalón de [`ci.yml`](../.github/workflows/ci.yml) fue sólo restore, build y test. El primer rojo se corrigió con [`.gitattributes`](../.gitattributes) en [`6fc1964`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/6fc1964). El segundo escalón, de la acción 2, sumó specs, dependencias vulnerables y secretos: los tres en verde en la [corrida 40953247197](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40953247197), specs y dependencias en rojo en el [control 40953621473](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40953621473) y secretos en rojo sólo en local. |
| Construir una vez y promover el mismo artefacto | sesión 13 | Pendiente | Acción 3: `dotnet publish` y `upload-artifact` en la CI, y un Release de GitHub con el EXE al etiquetar `v*`. |
| Instalación local reproducible | plantilla 1.4, lección 58 (L58:121) | Aplicada | [`readme.md`, sección 1.4](../readme.md#14-instrucciones-de-instalación): SDK fijado por `global.json`, comandos y base sembrada con datos sintéticos al abrir el puesto. El README declara pendiente un script de verificación del entorno; el «Cómo revisar» con credenciales de prueba es la acción 3. |
| No sobreingenierizar | sesiones 13 y 15 | No aplica | Un puesto de escritorio con SQLite local: sin Docker, ni Terraform, ni Kubernetes. La observabilidad (Prometheus, Grafana, OpenTelemetry) ya está diseñada como bitácoras y diagnóstico, que son segunda ola. |

### Mantenimiento, PR y documentación

| Práctica | Lección | Estado | Evidencia o motivo |
|---|---|---|---|
| PR con «¿Qué cambia? ¿Por qué? ¿Cómo probarlo?», commits atómicos y el humano firma el merge | lección 16 | Aplicada | [Plantilla de PR](../.github/pull_request_template.md); los PR [#2](https://github.com/DaveWalf/AI4Devs-finalproject/pull/2) y [#3](https://github.com/DaveWalf/AI4Devs-finalproject/pull/3), uno por change, abiertos por el autor; push y merge son suyos ([`AGENTS.md`](../AGENTS.md)). |
| Revisión asistida del PR contra los criterios | lección 127 | Aplicada | La [plantilla de PR](../.github/pull_request_template.md) pide la salida de `/openspec-verify-change` (la skill [`openspec-verify-change`](../.claude/skills/openspec-verify-change/SKILL.md)) y la de `openspec validate --all --strict`; la llevan el [PR #2](https://github.com/DaveWalf/AI4Devs-finalproject/pull/2) y el [PR #3](https://github.com/DaveWalf/AI4Devs-finalproject/pull/3). |
| Contexto vivo: revisar `AGENTS.md` y `config.yaml` al terminar | charla de adopción 1 | Aplicada | Casilla de la [plantilla de PR](../.github/pull_request_template.md): «`AGENTS.md` y `openspec/config.yaml` siguen vigentes». Ejemplo: [`7a57612`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/7a57612) actualizó [`AGENTS.md`](../AGENTS.md) al cerrar `conectar-puesto-y-receptor`. |
| ADR en MADR, uno por archivo | lección 63 | No aplica | Las decisiones nuevas viven en el `design.md` de cada change, cada una con su alternativa descartada; las ADR existentes se conservan en [`arquitectura.md`](arquitectura.md). Reescribir las viejas en MADR no suma al corte. |
| Diagramas como texto (Mermaid, C4) | lección 63 | Aplicada | Los diagramas son fuentes de texto versionadas: [`flujo.dot`](../diagrams/flujo.dot), [`componentes.dot`](../diagrams/componentes.dot) y el [ER en Mermaid](../diagrams/datos.mmd). |
| Documentación en la CI (markdownlint, lychee) | lección 65 | No aplica | Agregan una herramienta para pocos archivos. El chequeo de enlaces relativos que cada change corre sobre los documentos vivos alcanza; el segundo escalón de la CI trajo specs, dependencias y secretos, no documentación. |

### Registro del uso de IA

| Práctica | Lección | Estado | Evidencia o motivo |
|---|---|---|---|
| `prompts.md` con herramienta y modelo por fase, prompts clave y ajustes humanos | lección 58 (L58:137-145) | Aplicada | [`prompts.md`](../prompts.md): las seis fases con herramienta, modelo y evidencia, y los prompts de cada change con sus ajustes humanos. La forma de las cinco partes de L58 está en la tabla «Proyecto final». |
| Carpeta de evidencias: salidas de `validate`, de `dotnet test` y del CI enlazadas desde cada PR | sesión 13 | Aplicada | Las salidas están en la descripción de los PR [#2](https://github.com/DaveWalf/AI4Devs-finalproject/pull/2) y [#3](https://github.com/DaveWalf/AI4Devs-finalproject/pull/3), con la [corrida del CI](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40794862257) de la rama del primer change; la [plantilla](../.github/pull_request_template.md) las pide. |

### Proyecto final

Prácticas que agregó el curso v20261027 después del catálogo del 18/10.

| Práctica | Lección | Estado | Evidencia o motivo |
|---|---|---|---|
| IA en al menos 4 de las 6 fases del ciclo | lección 58 (L58:232) | Aplicada | [`prompts.md`](../prompts.md) documenta las seis, cada una con herramienta, modelo y evidencia del repo. |
| `prompts.md` con las cinco partes: herramientas por fase, modelos, contexto avanzado, entre 3 y 5 workflows clave y criterio humano | lección 58 (L58:137-145) | Aplicada | [`prompts.md`](../prompts.md): herramientas por fase, modelos y para qué, contexto avanzado, cuatro workflows clave y criterio humano, en ese orden; la historia de las entregas 1 y 2 quedó en dos anexos. La reestructuró el change [`mostrar-aplicacion-del-curso`](../openspec/changes/archive/2026-10-29-mostrar-aplicacion-del-curso/proposal.md). |
| Al menos un E2E del flujo principal | lección 58 (L58:90-107) | Pendiente | Acción 3. |
| Evidencia de funcionamiento: URL, video de 2 a 3 minutos o repo con credenciales de prueba | lección 58 (L58:111-117) | Pendiente | Acción 3: Release con el EXE y video. |
| README para revisar en local, con `.env.example` y `docker-compose` si aplican, y credenciales de prueba listadas | lección 58 (L58:121-125) | Pendiente | Acción 3: «Cómo revisar», con los códigos de prueba, quién reimprime y la línea que diga que ni `.env.example` ni `docker-compose` hacen falta. |
| Ramas en minúsculas y con el formato exacto | lección 58 (L58:195-201) | Pendiente | La rama final se llamará `final-project-dw`; se corta al final, con el CI en verde y tras el paso al SDK estable del 10/11 (D-C25). Las ramas de las entregas 1 y 2 usan `DW`. |
| Entrega por rama y Typeform, no por PR | lección 58 (L58:213) | Aplicada | Las entregas 1 y 2 son ramas del fork: [`feature/entrega-1-DW`](https://github.com/DaveWalf/AI4Devs-finalproject/tree/feature/entrega-1-DW) y [`feature/entrega-2-DW`](https://github.com/DaveWalf/AI4Devs-finalproject/tree/feature/entrega-2-DW), con el [§7 del README](../readme.md#7-pull-requests). Los PR dentro del fork ordenan la revisión; no son la entrega. |
| GitHub Issues para historias y tickets, con «Closes #n» | lección 59 (L59:108) | Pendiente | Backlog posterior a la acción 3. Activar Issues en el fork se ve desde afuera y lo decide el autor. Hoy los tickets viven en [`historias-y-tickets.md`](historias-y-tickets.md). |
| Barreras automáticas de seguridad en la CI | lecciones 138 y 140 | Aplicada | Dependencias vulnerables y secretos, como pasos y jobs de [`ci.yml`](../.github/workflows/ci.yml) con su lógica en [`auditar-dependencias.ps1`](../.github/scripts/auditar-dependencias.ps1) y [`escanear-secretos.sh`](../.github/scripts/escanear-secretos.sh): en verde en la [corrida 40953247197](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40953247197) y con el rojo esperado del control en la [corrida 40953621473](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40953621473). La revisión OWASP del receptor sigue siendo manual, con su casilla en la [plantilla de PR](../.github/pull_request_template.md). |
| La CI crece de a poco | sesión 15 | Aplicada | Primer escalón en verde, con restore, build y test ([corrida 40802117357](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40802117357)); segundo escalón en tres jobs, con specs, dependencias y secretos: [`ci.yml`](../.github/workflows/ci.yml) y la [corrida 40953247197](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40953247197). |
| ¿Un agente, varios o un flujo? | sesión 16 (L59:144) | Aplicada | Un agente por fase, con gates humanos y sin orquestador, y por qué: [`prompts.md`](../prompts.md), sección «Contexto avanzado». |
| Producto con LLM | sesión 14 | No aplica | El producto no llama a ningún modelo; las decisiones de consumo son reglas deterministas (RB06, cupo, fecha de negocio). La IA está en el proceso, no en el producto. |
| Laboratorios de RAG y frameworks de agentes | lecciones 116 a 120 y 151 | No aplica | El producto no usa LLM: no hay recuperación aumentada ni agentes dentro de INComedor. |
| Adopción de IA en el equipo | lección 164 | No aplica | El equipo es una persona. |

## Lo que no se aplica

El curso valora elegir, y un descarte con su motivo es evidencia. Cada uno de estos descartes tiene su fila `No aplica` en el mapa.

- **Laboratorios de RAG (LangChain, Streamlit, bases vectoriales) y frameworks de agentes (CrewAI, AutoGen, LangGraph):** el producto no usa LLM.
- **Presidio, Privacy Vault, AI Gateway y modelos locales para el producto:** no hay datos personales reales ni IA dentro de INComedor.
- **Docker, Terraform, Kubernetes, Prometheus, Grafana y OpenTelemetry:** es un puesto de escritorio con SQLite local. La observabilidad del producto ya está diseñada como bitácoras y diagnóstico, que quedan para la segunda ola.
- **Planning poker, velocity segmentada y Linear con MCP:** el equipo es una persona; alcanza la estimación doble de cada change.
- **Reqnroll o Gherkin aparte:** sería una segunda fuente de verdad junto a los escenarios de OpenSpec.
- **ADR en archivos MADR para lo ya decidido:** las decisiones nuevas viven en el `design.md` de cada change; reescribir las viejas no suma al corte.

## La devolución de la entrega 1 y su respuesta

La devolución de LIDR del 14/10/2026 marcó luz amarilla: «avanzar ajustando», con los diez ítems obligatorios completos y el riesgo en el alcance. Esta tabla junta, en un lugar, lo que pidió y cómo se respondió.

| La devolución pidió | Respuesta | Evidencia |
|---|---|---|
| Fijar un primer corte de un solo puesto | D-C24, aceptada tal cual el 18/10 | [`decisiones.md`](decisiones.md), el change [`fijar-primer-corte`](../openspec/changes/archive/2026-10-19-fijar-primer-corte/proposal.md) y el [§1.2 del README](../readme.md#12-características-y-funcionalidades-principales) |
| Elegir una sola tecnología de interfaz | Pantallas sólo en Razor; WPF es la ventana que hospeda el `BlazorWebView` | Regla de [`AGENTS.md`](../AGENTS.md) y ADR01 de [`arquitectura.md`](arquitectura.md) |
| Dejar fuera del corte los templates y la difusión entre puestos | Pasaron a la segunda ola, diseñada y no demostrada | [`AGENTS.md`](../AGENTS.md), sección «No se construye» |
| Proteger en reposo lo que baja al puesto | Protección en reposo del primer corte | [`readme.md`, sección 2.5](../readme.md#25-seguridad) |
| Un resumen corto del diseño para el agente | `AGENTS.md`, `openspec/config.yaml` y las specs vivas, en lugar de los anexos | [`AGENTS.md`](../AGENTS.md), [`config.yaml`](../openspec/config.yaml) y [`openspec/specs/registro-consumo`](../openspec/specs/registro-consumo/spec.md) |
| Un archivo de reglas para la implementación con IA | `AGENTS.md`, con `CLAUDE.md` que lo importa | [`AGENTS.md`](../AGENTS.md), [`CLAUDE.md`](../CLAUDE.md) y el commit [`36b20d8`](https://github.com/DaveWalf/AI4Devs-finalproject/commit/36b20d8) |
| Que la IA genere los tests desde los escenarios | Cada test nombra en su `DisplayName` el escenario o la regla que prueba, con su AT cuando lo tiene, y se vio fallar antes de implementar | [`tasks.md`, línea 3](../openspec/changes/archive/2026-10-20-registrar-consumo-atomico/tasks.md) y [`CaidasYConcurrenciaTests`](../tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/CaidasYConcurrenciaTests.cs); `evaluar-habilitacion-local` suma 41 pruebas, una por escenario de su delta y en dos niveles, vistas fallar antes en sus [tareas](../openspec/changes/archive/2026-10-29-evaluar-habilitacion-local/tasks.md): [`EvaluadorHabilitacionTests`](../tests/Intelektron.INComedor.Domain.Tests/EvaluadorHabilitacionTests.cs) y [`HabilitacionLocalTests`](../tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/HabilitacionLocalTests.cs) |
| Empezar por la transacción, el doble toque y la caída tras el commit, sobre SQLite real | Fue el primer change: [`registrar-consumo-atomico`](../openspec/changes/archive/2026-10-20-registrar-consumo-atomico/proposal.md) | [`UnidadConfirmacion`](../src/Intelektron.INComedor.Infrastructure.Sqlite/UnidadConfirmacion.cs), [`ReplayTests`](../tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/ReplayTests.cs) y [`CaidasYConcurrenciaTests`](../tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/CaidasYConcurrenciaTests.cs), sobre SQLite real |
| Un pipeline que compile, pruebe y publique | Compila y prueba, en verde; falta publicar (acción 3) | [`ci.yml`](../.github/workflows/ci.yml) y la [corrida 40802117357](https://github.com/DaveWalf/AI4Devs-finalproject/actions/runs/40802117357) |
| Revisión asistida de cada PR contra los criterios | `/openspec-verify-change` y la plantilla de PR; los PR #2 y #3 están abiertos | [Plantilla de PR](../.github/pull_request_template.md), [PR #2](https://github.com/DaveWalf/AI4Devs-finalproject/pull/2) y [PR #3](https://github.com/DaveWalf/AI4Devs-finalproject/pull/3) |
