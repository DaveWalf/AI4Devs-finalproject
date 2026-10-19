# Tasks

Regla de todo el change: cada test nuevo se escribe primero, se corre y se ve fallar por la razón esperada (un fallo de aserción o un `NotImplementedException` de un esqueleto, no un error de compilación); recién después se implementa lo que lo hace pasar. Si un test pasa sin cambios de producción, se rompe a propósito la línea que lo sostiene, se lo ve fallar y se restaura (L81:36-43). Ningún test, una vez visto en verde, se modifica sin aprobación de David. Cada test lleva en su `DisplayName` el AT y el nombre del escenario del spec.

## 1. SDK, solución y andamio

- [x] 1.1 Crear `global.json` (`sdk.version` `11.0.100-rc.2.26504.105`, `rollForward` `latestFeature`, `allowPrerelease` true), `Directory.Build.props` (`net11.0`, `Nullable` y `ImplicitUsings` activos) y `Directory.Packages.props` con las versiones exactas de D2; verificar con `dotnet --version` en la raíz, que imprime `11.0.100-rc.2.26504.105`
- [x] 1.2 Crear `INComedor.sln` con `dotnet new sln --format sln` y los cuatro proyectos de D3 con sus referencias (Infrastructure.Sqlite → Domain; cada proyecto de pruebas → su proyecto de `src`), `InternalsVisibleTo` de Infrastructure.Sqlite hacia sus pruebas y un `.gitignore` para `bin/`, `obj/`, `TestResults/` y `*.db`; verificar con `dotnet build INComedor.sln -warnaserror` (0 advertencias, 0 errores) y `dotnet test INComedor.sln` (corre sin tests)
- [x] 1.3 Actualizar la sección «Comandos» de `AGENTS.md`: quitar «(pendiente)» de build y test, ya vigentes; verificar que los dos comandos corren tal como quedan escritos

## 2. D-C25 y menciones de .NET 10

- [x] 2.1 Agregar D-C25 a `docs/decisiones.md`: SDK .NET 11, versión vigente y fecha (19/10/2026, decisión de David), `global.json`, `net11.0`, paquetes `11.0.0-rc.2.*` fijados en su versión exacta, paso a estable el 10/11/2026 antes de la entrega final del 12/11, y como descartadas el SDK 10 LTS con `net10.0` y el SDK 11 con `net10.0`, cada una con su porqué; anotar en la línea «Interfaz» de «Selección técnica del MVP» que D-C25 la enmienda, sin borrarla. Subir la versión documental a 1.3. Verificar con `rg -n "D-C25" docs/decisiones.md`: aparecen la fila y la nota
- [x] 2.2 Cambiar «.NET 10» por «.NET 11» en `readme.md:123` y `:153`, `openspec/config.yaml:12`, `docs/arquitectura.md:3` y `:111` y `diagrams/despliegue.dot:7`, y en el nodo de texto equivalente de `diagrams/despliegue.svg` (a mano: Graphviz no está instalado en la máquina); verificar con `rg -n "\.NET 10" readme.md openspec docs diagrams` que sólo quedan las menciones de D-C25 a lo descartado

## 3. Dominio: fecha de negocio y cupo

- [ ] 3.1 Escribir en Domain.Tests las pruebas de `CalculadoraFechaNegocio` (escenario «Confirmaciones a ambos lados del corte», AT04: `02:59:59Z` y `03:00:00Z` con `America/Argentina/Buenos_Aires` y corte 00:00 dan días consecutivos; un corte distinto de 00:00 desplaza el día) contra un esqueleto que lanza `NotImplementedException`; verlas fallar con `dotnet test --filter "FullyQualifiedName~FechaNegocio"`
- [ ] 3.2 Implementar `CalculadoraFechaNegocio` (D4); verificar que las pruebas de 3.1 pasan
- [ ] 3.3 Escribir las pruebas de invariantes de `FechaNegocio`, `ClaveCupo` y `CodigoIntencion` (un UUID vacío se rechaza al construir); verlas fallar y después implementar los value objects; verificar con `dotnet test --filter "FullyQualifiedName~Domain.Tests"`
- [ ] 3.4 Escribir las pruebas de `EvaluadorCupo`: autoriza bajo el límite; `LOCAL_LIMIT_REACHED` en el límite (AT02); variantes que comparten categoría cuentan juntas (AT02); otra categoría no consume el mismo cupo (AT02); sin regla, `SERVICE_NOT_ENABLED`; una opción con dos cargas se deniega si cualquiera excede; más de una regla vigente para la misma clave lanza excepción. Verlas fallar contra el esqueleto
- [ ] 3.5 Implementar `EvaluadorCupo` (D4); verificar que pasan todas las pruebas de Domain.Tests

## 4. Base SQLite y migración 0001

- [ ] 4.1 Escribir la prueba de bloqueo inmediato: con una transacción abierta por `BaseComedor.IniciarTransaccionEscritura()` en la conexión A, sin escrituras, la conexión B con `DefaultTimeout = 1` no logra abrir la suya y recibe `SQLITE_BUSY`. Verla fallar contra un esqueleto; implementar con `BeginTransaction(deferred: false)` y, si la prueba sigue fallando, con `BEGIN IMMEDIATE` explícito (D7); verificar que pasa
- [ ] 4.2 Escribir `Migraciones/0001_transaccion_consumo.sql` con las 20 tablas de D5, los nombres del mapa y de la tabla de D5, PK, FK compuestas, unicidades y `CHECK`; **mostrarle a David el SQL completo y esperar su aprobación antes de aplicarlo** (L87:444-461). Verificar con la respuesta de David
- [ ] 4.3 Escribir las pruebas de apertura: escenario «Base vacía» (versión 1, `foreign_keys` en 1 y la lista exacta de las 20 tablas `tbComedor…`), «Versión de esquema desconocida» (excepción y hash SHA-256 del archivo igual antes y después) y «Unicidad de consumo por intención» (la segunda inserción directa falla con `SQLITE_CONSTRAINT`). Verlas fallar contra el esqueleto de `BaseComedor.Abrir`
- [ ] 4.4 Implementar `BaseComedor.Abrir` y el migrador con `PRAGMA user_version`, WAL y `synchronous = FULL` en el orden de D6; verificar con `dotnet test --filter "FullyQualifiedName~Infrastructure.Sqlite.Tests"`

## 5. Confirmación y replay

- [ ] 5.1 Escribir el soporte de pruebas de D10 y D11: archivo temporal por test con borrado de `-wal` y `-shm`, `RelojFijo` sobre `TimeProvider`, `PuntosDeFallaDePrueba` y `FixtureSintetico` (un puesto, tres personas, ALMUERZO con dos variantes, REFRIGERIO y quince opciones en ARS con escala 2, el código `000123`); verificar con una prueba que cuenta 3 personas y 15 opciones después de cargarlo
- [ ] 5.2 Escribir las pruebas de decisión nueva sobre SQLite real: «Autorización con cupo libre y sin red» (AT01, conteo exacto por tabla e identificadores coincidentes), «Rechazo por cupo agotado» (AT02), «Persona sin regla aplicable», «Variantes que comparten categoría cuentan juntas», «Otra categoría no consume el mismo cupo», «Confirmaciones a ambos lados del corte» (AT04), «Las cargas llevan la fecha del consumo» (AT50, cargas y `AppliedQuotaCharges` del evento) y «Versión de menú que dejó de ser la vigente» (AT47, cero filas nuevas). Verlas fallar contra el esqueleto de `UnidadConfirmacion.Confirmar`
- [ ] 5.3 Implementar `UnidadConfirmacion` con los pasos 1 a 7 de D7, el hash de D8 y los eventos de D9; verificar que pasan las pruebas de 5.2 y que el JSON de cada evento tiene exactamente las propiedades requeridas de `ConsumptionRecorded` o `ConsumptionDenied` en el OpenAPI
- [ ] 5.4 Escribir las pruebas de replay: «Doble toque con el mismo IntentId» (AT26), «Otra intención con una sesión ya usada» (AT26), «Replay después de vencer la sesión o cambiar el padrón o el catálogo» (AT35, incluida la intención nueva con la sesión vencida que no escribe nada) y «Mismo IntentId con otro contenido» (AT25, la fila original igual byte por byte). Verlas fallar; si alguna pasa por la implementación de 5.3, aplicar la regla de romper a propósito
- [ ] 5.5 Completar lo que falte del replay y de la validación de la sesión; verificar que pasan todas las pruebas de los grupos 4 y 5

## 6. Caídas y concurrencia

- [ ] 6.1 Escribir las pruebas con `IPuntosDeFalla`, todavía sin invocar desde la unidad de trabajo: «Caída antes del commit no deja rastro» (AT05, cero filas de la intención en todas las tablas y una confirmación posterior que produce un solo consumo), «Caída entre el commit y el resultado» (AT05, `ClearAllPools`, conexión nueva y mismos `DecisionId`, `ConsumptionId` y `TicketId`), «Recuperar no reconstruye el trabajo original» (AT05/RB17), «Dos conexiones compiten por el último cupo» (AT03, `Barrier(2)` con timeout de 10 s) y «Mismo IntentId concurrente en dos conexiones» (AT03). Verlas fallar porque la unidad de trabajo todavía no llama a los puntos de falla
- [ ] 6.2 Invocar `DespuesDeLecturaPrevia`, `AntesDelCommit` y `DespuesDelCommit` desde `UnidadConfirmacion` (D10); verificar que pasan las pruebas de 6.1

## 7. Datos protegidos y verificación integrada

- [ ] 7.1 Escribir «Una autorización no registra el código» y «Un rechazo tampoco lo registra» (buscan `000123` en toda columna de texto de intenciones, eventos de salida, comprobantes y bitácora); como deben pasar sin cambios, verlas fallar escribiendo a propósito el código en el contexto de bitácora y restaurar; verificar que pasan
- [ ] 7.2 Correr `dotnet build INComedor.sln -warnaserror` y `dotnet test INComedor.sln`, y pegar en la respuesta su salida literal: 0 advertencias, 0 errores y todas las pruebas en verde, con su cantidad
- [ ] 7.3 Correr 20 veces seguidas `dotnet test INComedor.sln --filter "DisplayName~AT03"` y pegar el conteo: 20 de 20 en verde
- [ ] 7.4 Comprobar que cada uno de los 22 escenarios del spec tiene al menos un test cuyo `DisplayName` lo nombra (DoD de feature, L52:72-73); verificar con `rg -c "DisplayName" tests` y una lista escenario → test en la respuesta
- [ ] 7.5 Revisar `AGENTS.md` y `openspec/config.yaml` contra lo que dejó este change (comandos, stack .NET 11) y correr `openspec validate --all --strict` sin errores; correr `git status --short` y comprobar que no aparecen `bin/`, `obj/`, archivos `.db` ni `devoluciones de AI4DEVS/`

## Workflow follow-up

- `/openspec-verify-change` y su salida en la descripción del PR, con la plantilla «¿Qué cambia? ¿Por qué? ¿Cómo probarlo?».
- Commit por grupo de tareas, por rutas, en `feature/entrega-2-DW`; push, PR y merge son de David, igual que la revisión humana de los tests de RB06 (DoD de feature).
- Archivar con `/openspec-archive-change` en el mismo PR y correr `openspec validate --all --strict`.
