# Tasks

Se aplica con `sonnet` · `medium` si David aprueba el design tal cual: es un plan diseñado con su verificador, sin juicio pendiente. Si el gate cambia una decisión, `opus` · `medium`. Los comandos corren desde la raíz del repo y su salida se pega en la tarea. Al cerrar cada grupo hay un commit por rutas, en español, con `git commit -F <archivo>`. Ninguna tarea modifica un test existente: `git diff --stat -- tests/` sólo puede mostrar los dos archivos nuevos. Si una tarea lo necesitara, se para y se le pregunta a David.

## 1. Dominio: el evaluador de habilitación

- [x] 1.1 Crear `tests/Intelektron.INComedor.Domain.Tests/EvaluadorHabilitacionTests.cs` con una prueba por escenario de `specs/habilitacion-local/spec.md` (D9), en memoria, con bordes exactos de vigencia y el `DisplayName` empezando por su AT o su regla; verificar con `dotnet test tests/Intelektron.INComedor.Domain.Tests` que fallan (o no compilan) antes de implementar, y pegar la salida
- [x] 1.2 Implementar `EvaluadorHabilitacion`, sus modelos, `ReglasHabilitacionContradictoriasException` y los cuatro motivos nuevos de `MotivoDenegacion` al final de la enumeración (D1 a D5), sin cambiar la firma de `EvaluadorCupo.Evaluar` ni de `ReglaPersona`; verificar con `dotnet test tests/Intelektron.INComedor.Domain.Tests` que pasan las nuevas y las 21 anteriores, y con `git diff --stat -- tests/` que sólo aparece el archivo nuevo

  Salida de 1.1 (antes de implementar): `dotnet test tests/Intelektron.INComedor.Domain.Tests` no compiló: `error CS0246: El nombre del tipo o del espacio de nombres 'ReglaCandidata' no se encontró`, y lo mismo para `CredencialSesion`, `ResultadoHabilitacion` y `EvaluadorHabilitacion`; `La compilación falló con el código de salida 1.`

  Salida de 1.2: `Resumen de la serie de pruebas: Correcta! total: 46, error: 0, correcto: 46` (25 nuevas en `EvaluadorHabilitacionTests` y las 21 anteriores). `git diff --stat -- tests/` no muestra ningún archivo modificado; `git status --short` sólo tiene el archivo de pruebas nuevo, `EvaluadorHabilitacion.cs` y `ModelosCupo.cs`.

## 2. Infrastructure: la decisión en la unidad de confirmación

- [ ] 2.1 Crear `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/HabilitacionLocalTests.cs`, que hereda `ConfirmacionTestBase` y siembra con `Sql.Ejecutar` las reglas, bajas y credenciales de cada escenario de los dos deltas, sin cambiar `FixtureSintetico` (D9); verificar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~HabilitacionLocalTests"` que fallan antes de implementar, y pegar la salida
- [ ] 2.2 En `UnidadConfirmacion`, reemplazar `LeerReglasVigentes` por `LeerReglasCandidatas` (D2), leer `comperActivo` y la credencial de la sesión (D6), llamar a `EvaluadorHabilitacion` y después a `EvaluadorCupo` dentro de la misma transacción, y traducir los cuatro motivos nuevos en `CodigoDeMotivo` (D4); verificar con `dotnet test INComedor.sln` que pasan las nuevas y las 61 anteriores sin cambios, y con `git diff --stat -- tests/` que sólo aparecen los dos archivos nuevos
- [ ] 2.3 Comprobar que el empate contradictorio no escribe nada: en `HabilitacionLocalTests`, el conteo de `Sql.ConteoDeEscritura` antes y después de la confirmación fallida es igual; verificar con la prueba en verde y pegar su nombre y resultado

## 3. Verificación integrada

- [ ] 3.1 Correr `dotnet build INComedor.sln -warnaserror` (0 advertencias, 0 errores), `dotnet test INComedor.sln` (61 más las nuevas, todas en verde) y `openspec validate --all --strict` (sin errores), y pegar las tres salidas
- [ ] 3.2 Comprobar que cada uno de los 18 escenarios de los deltas tiene su prueba: listar con `rg -n "DisplayName" tests/Intelektron.INComedor.Domain.Tests/EvaluadorHabilitacionTests.cs tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/HabilitacionLocalTests.cs` y pegar la tabla escenario → prueba, sin huecos
- [ ] 3.3 Correr el puesto de demostración (`dotnet run --project src/Intelektron.INComedor.Kiosk`, con la carpeta de demostración respaldada antes y restaurada después) y verificar que el primer toque sigue dando `AUTHORIZED` y el segundo `LOCAL_LIMIT_REACHED`; pegar los dos resultados

## 4. Tallas y evidencia

- [ ] 4.1 Llevar a `docs/plan-academico.md`, sección «Capacidad y reestimación», la talla de David (S, 2 a 4 h), la de la IA (M, 4 a 6 h) y el colchón del 30 % de la lección 50 tal como están en proposal.md, con las horas reales del apply; pasar a `Aplicada` las filas «Capacidad con 30 % de margen» y «La IA estima como un par, no como única fuente» de `docs/aplicacion-del-curso.md`, con enlace al plan y a este change; verificar con el chequeo del mapa (`defectuosas: 0`)
- [ ] 4.2 Agregar la evidencia del change: la fila de `docs/cobertura-entrega.md` «De 3 a 5 funcionalidades» con la habilitación local, la fila del mapa que corresponda en `docs/aplicacion-del-curso.md` y, si el gate dejó un ajuste humano, su entrada en el §5 de `prompts.md`; verificar con el chequeo de enlaces relativos sobre los tres documentos (`enlaces rotos: 0`) y `git status --short`, donde sólo aparecen esos documentos y el `tasks.md` de este change

## Workflow follow-up

- `/openspec-verify-change` con su salida en la descripción del PR.
- Archivar con `openspec archive evaluar-habilitacion-local -y`, que sincroniza la capability nueva `habilitacion-local` y el requirement modificado de `registro-consumo`, y correr `openspec validate --all --strict`.
- Push, PR y merge son de David.
