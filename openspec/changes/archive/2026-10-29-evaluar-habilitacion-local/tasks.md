# Tasks

Se aplica con `sonnet` · `medium` si David aprueba el design tal cual: es un plan diseñado con su verificador, sin juicio pendiente. Si el gate cambia una decisión, `opus` · `medium`. Los comandos corren desde la raíz del repo y su salida se pega en la tarea. Al cerrar cada grupo hay un commit por rutas, en español, con `git commit -F <archivo>`. Ninguna tarea modifica un test existente: `git diff --stat -- tests/` sólo puede mostrar los dos archivos nuevos. Si una tarea lo necesitara, se para y se le pregunta a David.

## 1. Dominio: el evaluador de habilitación

- [x] 1.1 Crear `tests/Intelektron.INComedor.Domain.Tests/EvaluadorHabilitacionTests.cs` con una prueba por escenario de `specs/habilitacion-local/spec.md` (D9), en memoria, con bordes exactos de vigencia y el `DisplayName` empezando por su AT o su regla; verificar con `dotnet test tests/Intelektron.INComedor.Domain.Tests` que fallan (o no compilan) antes de implementar, y pegar la salida
- [x] 1.2 Implementar `EvaluadorHabilitacion`, sus modelos, `ReglasHabilitacionContradictoriasException` y los cuatro motivos nuevos de `MotivoDenegacion` al final de la enumeración (D1 a D5), sin cambiar la firma de `EvaluadorCupo.Evaluar` ni de `ReglaPersona`; verificar con `dotnet test tests/Intelektron.INComedor.Domain.Tests` que pasan las nuevas y las 21 anteriores, y con `git diff --stat -- tests/` que sólo aparece el archivo nuevo

  Salida de 1.1 (antes de implementar): `dotnet test tests/Intelektron.INComedor.Domain.Tests` no compiló: `error CS0246: El nombre del tipo o del espacio de nombres 'ReglaCandidata' no se encontró`, y lo mismo para `CredencialSesion`, `ResultadoHabilitacion` y `EvaluadorHabilitacion`; `La compilación falló con el código de salida 1.`

  Salida de 1.2: `Resumen de la serie de pruebas: Correcta! total: 46, error: 0, correcto: 46` (25 nuevas en `EvaluadorHabilitacionTests` y las 21 anteriores). `git diff --stat -- tests/` no muestra ningún archivo modificado; `git status --short` sólo tiene el archivo de pruebas nuevo, `EvaluadorHabilitacion.cs` y `ModelosCupo.cs`.

## 2. Infrastructure: la decisión en la unidad de confirmación

- [x] 2.1 Crear `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/HabilitacionLocalTests.cs`, que hereda `ConfirmacionTestBase` y siembra con `Sql.Ejecutar` las reglas, bajas y credenciales de cada escenario de los dos deltas, sin cambiar `FixtureSintetico` (D9); verificar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~HabilitacionLocalTests"` que fallan antes de implementar, y pegar la salida
- [x] 2.2 En `UnidadConfirmacion`, reemplazar `LeerReglasVigentes` por `LeerReglasCandidatas` (D2), leer `comperActivo` y la credencial de la sesión (D6), llamar a `EvaluadorHabilitacion` y después a `EvaluadorCupo` dentro de la misma transacción, y traducir los cuatro motivos nuevos en `CodigoDeMotivo` (D4); verificar con `dotnet test INComedor.sln` que pasan las nuevas y las 61 anteriores sin cambios, y con `git diff --stat -- tests/` que sólo aparecen los dos archivos nuevos
- [x] 2.3 Comprobar que el empate contradictorio no escribe nada: en `HabilitacionLocalTests`, el conteo de `Sql.ConteoDeEscritura` antes y después de la confirmación fallida es igual; verificar con la prueba en verde y pegar su nombre y resultado

  Salida de 2.1 (antes de implementar): `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~HabilitacionLocalTests"`: `total: 16, error: 9, correcto: 7`. Fallaron los nueve que piden el comportamiento nuevo: persona inactiva, credencial revocada, credencial vencida, regla cero (tres) y empate contradictorio (`Expected: ReglasHabilitacionContradictoriasException, Actual: ReglasCupoAmbiguasException`). Los siete que pasaron ya describen lo que el puesto hacía bien (regla vencida, inicio exacto, 400 días, 03:00, sesión sin credencial, permiso de mayor prioridad, empate sin contradicción); quedan como protección contra regresión.

  Salida de 2.2: `dotnet build INComedor.sln -warnaserror`: `Compilación correcta. 0 Advertencia(s) 0 Errores`. `dotnet test INComedor.sln`: `total: 102, error: 0, correcto: 102` (Domain 46, Sqlite 43, Kiosk 9, Sync.Api 4: las 61 anteriores más 25 y 16 nuevas). `git diff --stat -- tests/` vacío; `git status --short` sólo muestra `UnidadConfirmacion.cs` y el archivo de pruebas nuevo.

  Salida de 2.3: `Empate_contradictorio_no_escribe_nada` (`RB02 · Empate contradictorio en la mayor prioridad: error de configuración y la base queda igual`): `Resumen de la serie de pruebas: Correcta! total: 1, correcto: 1`. Compara `Sql.ConteoDeEscritura` antes y después de la excepción y comprueba que la sesión sigue sin consumir.

## 3. Verificación integrada

- [x] 3.1 Correr `dotnet build INComedor.sln -warnaserror` (0 advertencias, 0 errores), `dotnet test INComedor.sln` (61 más las nuevas, todas en verde) y `openspec validate --all --strict` (sin errores), y pegar las tres salidas
- [x] 3.2 Comprobar que cada uno de los 18 escenarios de los deltas tiene su prueba: listar con `rg -n "DisplayName" tests/Intelektron.INComedor.Domain.Tests/EvaluadorHabilitacionTests.cs tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/HabilitacionLocalTests.cs` y pegar la tabla escenario → prueba, sin huecos
- [x] 3.3 Correr el puesto de demostración (`dotnet run --project src/Intelektron.INComedor.Kiosk`, con la carpeta de demostración respaldada antes y restaurada después) y verificar que el primer toque sigue dando `AUTHORIZED` y el segundo `LOCAL_LIMIT_REACHED`; pegar los dos resultados

  Salida de 3.1: `dotnet build INComedor.sln -warnaserror`: `Compilación correcta. 0 Advertencia(s) 0 Errores`. `dotnet test INComedor.sln`: `total: 102, error: 0, correcto: 102, omitido: 0` (61 anteriores más 25 de dominio y 16 de SQLite). `openspec validate --all --strict`: `Totals: 5 passed, 0 failed (5 items)`, con `✓ change/evaluar-habilitacion-local`, `✓ spec/registro-consumo` y las demás.

  Salida de 3.2, escenario → prueba (D = `Domain.Tests/EvaluadorHabilitacionTests.cs`, S = `Infrastructure.Sqlite.Tests/HabilitacionLocalTests.cs`; la línea es la del atributo `Fact`):

  | # | Escenario del delta | Dominio (D) | SQLite (S) |
  |---|---|---|---|
  | 1 | Persona dada de baja con regla vigente y cupo libre | :43 | :63 |
  | 2 | Credencial de la sesión revocada | :52 | :76 |
  | 3 | Credencial de la sesión vencida en el instante de la decisión | :72 | :91 |
  | 4 | Persona inactiva con una regla cero vigente | :88 | :102 |
  | 5 | Sesión sin credencial | :96 | :113 |
  | 6 | Regla con fecha de fin ya alcanzada | :106 | :126 |
  | 7 | Inicio exacto de la vigencia | :122 | :136 |
  | 8 | Regla sin vencimiento y un período prolongado sin red | :138 | :156 |
  | 9 | Horario sólo informativo | :148 | :167 |
  | 10 | Regla cero vigente frente a un permiso de mayor prioridad | :160 | :179 |
  | 11 | En el fin exacto de la regla cero se evalúa el permiso | :172 | :191 |
  | 12 | Una regla cero en una de dos categorías deniega la opción | :202 | :208 |
  | 13 | Gana la de mayor prioridad | :227 | :227 |
  | 14 | Empate contradictorio en la mayor prioridad | :246 | :241 |
  | 15 | Empate sin contradicción | :263 | :258 |
  | 16 | Variantes que comparten categoría cuentan juntas (`registro-consumo`, sin cambios) | `EvaluadorCupoTests.cs:47` | `DecisionNuevaTests.cs:83` |
  | 17 | Otra categoría no consume el mismo cupo (`registro-consumo`, sin cambios) | `EvaluadorCupoTests.cs:59` | `DecisionNuevaTests.cs:96` |
  | 18 | Persona sin regla aplicable (`registro-consumo`, sin cambios) | `EvaluadorCupoTests.cs:70` | `DecisionNuevaTests.cs:72` |

  Sin huecos: 18 de 18. Los tres últimos ya tenían su prueba y siguen en verde con el requirement modificado.

  Salida de 3.3: el Kiosk (`Intelektron.INComedor.Kiosk.exe` de `bin\Debug\net11.0-windows10.0.17763.0`, PID 41736) se lanzó con la carpeta `%LOCALAPPDATA%\INComedor\demo\` inexistente (no había nada que respaldar). Dos toques sobre «Confirmar» enviados a la ventana web por mensajes de Windows y capturados con `PrintWindow`: el primero mostró `Decisión AUTHORIZED` con `Consumo 3806c7ae-555f-45ad-912a-548dd7ba6925` y `Comprobante b675648a-e2cc-47eb-8337-020d0514a07c`; el segundo, `Decisión DENIED` y `Motivo LOCAL_LIMIT_REACHED`. Se cerró por su PID y se borró la carpeta `demo`, que no existía antes (restaurada al estado inicial). Se usó el ejecutable ya compilado en lugar de `dotnet run` porque la ventana se maneja por su PID; es el mismo binario.

## 4. Tallas y evidencia

- [x] 4.1 Llevar a `docs/plan-academico.md`, sección «Capacidad y reestimación», la talla de David (S, 2 a 4 h), la de la IA (M, 4 a 6 h) y el colchón del 30 % de la lección 50 tal como están en proposal.md, con las horas reales del apply; pasar a `Aplicada` las filas «Capacidad con 30 % de margen» y «La IA estima como un par, no como única fuente» de `docs/aplicacion-del-curso.md`, con enlace al plan y a este change; verificar con el chequeo del mapa (`defectuosas: 0`)
- [x] 4.2 Agregar la evidencia del change: la fila de `docs/cobertura-entrega.md` «De 3 a 5 funcionalidades» con la habilitación local, la fila del mapa que corresponda en `docs/aplicacion-del-curso.md` y, si el gate dejó un ajuste humano, su entrada en el §5 de `prompts.md`; verificar con el chequeo de enlaces relativos sobre los tres documentos (`enlaces rotos: 0`) y `git status --short`, donde sólo aparecen esos documentos y el `tasks.md` de este change

  Salida de 4.1: `docs/plan-academico.md`, sección «Capacidad y reestimación», subsección «Tallas por change y colchón»: S de David (2 a 4 h, 2,9 a 5,7 h con el colchón), M de la IA (4 a 6 h, 5,7 a 8,6 h) y las horas reales del apply, 12 minutos de reloj (0,2 h, de las 20:32 a las 20:44 del 29/10/2026). Las filas «Capacidad con 30 % de margen» y «La IA estima como un par, no como única fuente» de `docs/aplicacion-del-curso.md` pasan a `Aplicada`, con enlace al plan y a la propuesta de este change. Chequeo del mapa: `filas del mapa: 82 · defectuosas: 0`.

  Salida de 4.2: la fila «De 3 a 5 funcionalidades» de `docs/cobertura-entrega.md` pasa de tres a cuatro piezas con la habilitación local (spec, evaluador y 41 pruebas), y la fila «Que la IA genere los tests desde los escenarios» del mapa suma las dos pruebas nuevas. Sin entrada nueva en el §5 de `prompts.md`: el gate no dejó ningún ajuste humano que registrar. Chequeo de enlaces relativos sobre `docs/plan-academico.md`, `docs/aplicacion-del-curso.md` y `docs/cobertura-entrega.md`: `enlaces rotos: 0`. `git status --short` muestra sólo esos tres documentos y este `tasks.md`.

## Workflow follow-up

- `/openspec-verify-change` con su salida en la descripción del PR.
- Archivar con `openspec archive evaluar-habilitacion-local -y`, que sincroniza la capability nueva `habilitacion-local` y el requirement modificado de `registro-consumo`, y correr `openspec validate --all --strict`.
- Push, PR y merge son de David.
