# Design

## Context

Ver `proposal.md` (Why). El repo todavía no tiene código: sólo documentación, `openspec/` y `AGENTS.md`. La máquina de desarrollo tiene el SDK `11.0.100-rc.2.26504.105` con sus runtimes `11.0.0-rc.2.26504.105` (NETCore, AspNetCore y WindowsDesktop); no tiene SDK 10, sólo runtimes 10.0.13. En NuGet, la última `Microsoft.Data.Sqlite` `11.*` es `11.0.0-rc.2.26504.105` (consultado el 19/10/2026).

Restricciones que vienen de antes: `Microsoft.Data.Sqlite` con SQL parametrizado y sin ORM (AGENTS.md); nombres físicos `tbComedor…` del mapa D-C18 ([nomenclatura](../../../docs/nomenclatura-integracion.md)); las 27 entidades de D4 de `fijar-primer-corte`; pruebas sobre un archivo SQLite real, con dobles sólo en las fronteras. Según Microsoft Learn, el proveedor guarda `Guid` como TEXT y, ante busy o locked, reintenta hasta `DefaultTimeout` (30 s por defecto); la página de transacciones no dice qué SQL emite `BeginTransaction()`.

## Goals / Non-Goals

**Goals:**

- Que la transacción de RB06 y el replay de RB07 se demuestren contra un archivo SQLite real, con los AT del spec como tests vistos en rojo antes de implementar.
- Que la solución y la migración sean el andamio de los changes siguientes, sin un proyecto ni una tabla que hoy ningún test ejercite.
- Que un solo archivo cambie cuando .NET 11 pase a estable el 10/11.

**Non-Goals:**

- Ver `proposal.md` (Non-goals). A nivel de diseño, además: sin API asincrónica, sin capa `Application` y sin inyección de dependencias de contenedor.

## Decisions

### D1. SDK .NET 11 fijado con `global.json` (D-C25)

`global.json` en la raíz con `sdk.version` `11.0.100-rc.2.26504.105`, `rollForward: latestFeature` y `allowPrerelease: true`; los proyectos apuntan a `net11.0`. D-C25 se agrega a `docs/decisiones.md` y enmienda la línea «Interfaz» de la selección técnica sin borrarla. La versión estable sale el 10/11/2026 y se adopta ese día, antes de la entrega final del 12/11, en una tarea aparte.

- **Descartada: SDK 10 LTS con `net10.0`.** Era lo documentado, pero la máquina ya no tiene SDK 10 y David decidió el 19/10 trabajar sobre .NET 11. Volver a 10 obliga a instalar un SDK que el resto del ecosistema local no usa.
- **Descartada: SDK 11 con `net10.0`.** Compila, pero deja el runtime de destino en una versión que no es la elegida y suma una segunda línea de versiones que mantener sin beneficio para el corte.

### D2. Versiones de paquetes en un único `Directory.Packages.props`

Gestión central de paquetes (`ManagePackageVersionsCentrally`), con `Microsoft.Data.Sqlite` fijado en `11.0.0-rc.2.26504.105`, la última de la familia `11.0.0-rc.2.*`. En pruebas: `xunit.v3` 4.0.2, `xunit.runner.visualstudio` 4.0.1 y `Microsoft.NET.Test.Sdk` 18.11.0, las estables observadas el 19/10. `Directory.Build.props` fija `net11.0`, `Nullable` e `ImplicitUsings`. El paso a estable del 10/11 toca sólo `global.json` y este archivo.

- **Descartada: versión flotante `11.0.0-rc.2.*` en el `PackageReference`.** Cada restauración podría resolver un build distinto; con una versión exacta, el build es reproducible y sigue dentro de la familia que pidió David.
- **Descartada: versiones en cada `.csproj`.** El paso a estable tocaría cuatro archivos en lugar de uno.

### D3. Cuatro proyectos y una solución `.sln`

`INComedor.sln` (creado con `dotnet new sln --format sln`, porque `AGENTS.md` y el README nombran ese archivo) con `src/Intelektron.INComedor.Domain` (sin dependencias), `src/Intelektron.INComedor.Infrastructure.Sqlite` (referencia Domain y `Microsoft.Data.Sqlite`), `tests/Intelektron.INComedor.Domain.Tests` y `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests`. Los nombres son los de `readme.md` §2.3.

- **Descartada: crear ya `Application`, `Contracts` y `Kiosk`.** Quedarían vacíos o como pasamanos; entran con el change que los ejercita (`atender-seleccion-y-confirmacion` y `recibir-lotes-idempotente`).
- **Descartada: `.slnx`, el formato por defecto del SDK actual.** Obligaría a cambiar los comandos de `AGENTS.md` y del README sin que el corte gane nada.

### D4. Evaluador mínimo de dominio, no un doble

Domain trae dos piezas puras. **`CalculadoraFechaNegocio`** convierte el instante UTC a la zona del puesto y resta un día si la hora local es anterior a `BusinessDayStartLocal` (RB04). **`EvaluadorCupo`** recibe las reglas por persona vigentes para servicio, ámbito y categoría, el uso de cupo y las unidades de cada carga de la opción de menú: sin regla, `SERVICE_NOT_ENABLED`; con una, autoriza si `Used + Units <= Limit` en todas las cargas y, si no, `LOCAL_LIMIT_REACHED` (RB03). Con más de una regla vigente para la misma clave, el evaluador no decide: lanza una excepción, la transacción hace rollback y no se escribe nada. El fixture tiene exactamente una regla por clave.

Value objects con su invariante en el constructor: `FechaNegocio` (DateOnly), `ClaveCupo` (persona, ámbito, categoría y fecha, ningún UUID vacío) y `CodigoIntencion` (UUID no vacío).

- **Descartada: un doble del evaluador.** AT03 quedaría verificando que el doble devuelve lo programado y no que la relectura bajo el bloqueo decide con el cupo real. Es la validación circular que el curso marca (L80:162).
- **Descartada: RB02 completa en este change** (persona inactiva, `Limit=0` con precedencia, `Priority`, grupos). TK03.1 la deja explícitamente a TK01, y duplicaría el tamaño del change. Va a un change propio; ver Open Questions.

### D5. Migración `0001` con las 20 tablas que la transacción usa

`Migraciones/0001_transaccion_consumo.sql`, embebida como recurso. Escribe la transacción: intenciones, consumos, consumos conocidos, cargos de cupo conocidos, usos de cupo, comprobantes, trabajos de impresión, eventos de salida y bitácora; también actualiza `ConsumedByIntentId` en la sesión de identidad. Lee, y sus claves referencian: configuración de puestos, versiones de configuración, personas, credenciales, tipos de servicio, ámbitos y categorías de cupo, opciones de menú y sus cargas, reglas de habilitación y sesiones de identidad. Lleva todas las PK, FK compuestas y unicidades del diccionario, y `CHECK` para enums, `Units > 0`, `Used >= 0` y `Limit >= 0`.

Representación física: UUID como TEXT por parámetro `Guid` (la conversión por defecto del proveedor), instantes UTC como TEXT ISO 8601 con `Z`, fechas de negocio como TEXT `yyyy-MM-dd`, hashes como BLOB de 32 bytes y JSON como TEXT.

Columnas sin mapa (los 13 campos que el modelo dejaba para M0), con nombres según la convención D-C18:

| Campo lógico | Columna |
|---|---|
| `ConfigurationSnapshot.ActivatedAtUtc` | `comvcFechaActivacionUtc` |
| `IdentitySession.IssuedAtUtc` | `comsidFechaEmisionUtc` |
| `IdentitySession.ConsumedByIntentId` | `comicCodigoConsumidora` |
| `ConsumptionIntent.HashSchema` | `comicEsquemaHash` |
| `ConsumptionIntent.CreatedAtUtc` | `comicFechaCreacionUtc` |
| `ObservedConsumption.ObservedAtUtc` | `comccFechaObservacionUtc` |
| `Ticket.CreatedAtUtc` | `comcmpFechaCreacionUtc` |
| `PrintJob.LastErrorCode` | `comimpCodigoUltimoError` |
| `AuditRecord.TimeZone` | `combitZonaHoraria` |
| `AuditRecord.Offset` | `combitDesplazamientoUtc` |
| `AuditRecord.Severity` | `combitSeveridad` |
| `AuditRecord.Component` | `combitComponente` |
| `AuditRecord.SchemaVersion` | `combitVersionEsquema` |

- **Descartada: las 23 tablas locales de D4 en `0001`.** `LocalOperatorGrant`, `SyncBatch` y `SyncBatchItem` no las ejercita ningún test de este change; entran en `0002` con la reimpresión y el envío de lotes.
- **Descartada: actualizar ahora los mapas CSV.** El de atributos dice explícitamente que mapea el ER (256 filas); agregarle 13 filas que no están en el diagrama rompe esa correspondencia. Se registran aquí y en un comentario de la migración.

### D6. Apertura de la base y versión de esquema con `PRAGMA user_version`

`BaseComedor.Abrir(ruta)` lee primero `PRAGMA user_version`. Si es mayor que la última migración conocida, lanza `EsquemaDesconocidoException` antes de cualquier otro PRAGMA, para no tocar el archivo. Si es 0, aplica `0001` y pone `user_version = 1` en la misma transacción. Recién después activa `PRAGMA journal_mode = WAL` y `PRAGMA synchronous = FULL`; en cada conexión, `PRAGMA foreign_keys = ON`.

- **Descartada: una tabla `__migraciones`.** Es otra tabla que mantener; `user_version` vive en el encabezado del archivo y cambia en la misma transacción que la migración.
- **Descartada: `synchronous = NORMAL` en WAL.** Ante un corte de energía podría perderse un commit ya informado, con el comprobante impreso. FULL cuesta un fsync por confirmación, irrelevante con un puesto y una persona a la vez.

### D7. Transacción con `BeginTransaction()` no diferida, verificada por test

`UnidadConfirmacion.Confirmar(solicitud)`:

1. **Lectura previa** del `IntentId`, sin transacción. Mismo hash: devuelve el `ResultPayload` guardado. Hash distinto: `Conflicto`. Ninguno de los dos escribe.
2. `BeginTransaction(deferred: false)` toma el bloqueo de escritura al empezar. Bajo el bloqueo se vuelve a buscar el `IntentId`, porque otra conexión pudo decidirlo entre la lectura previa y el bloqueo.
3. Se releen bajo el bloqueo el snapshot activo (`KioskConfiguration.ActiveSnapshotId`), la sesión, la persona, la opción de menú con sus cargas, las reglas por persona y los usos de cupo.
4. Validaciones previas a decidir, que no escriben nada: la sesión debe existir, ser `CONSUMPTION`, pertenecer a la persona, no estar vencida y no estar consumida por otra intención (si no, `SesionNoUtilizable`); la versión de menú debe ser la vigente (si no, `CatalogoCambiado`, AT47).
5. Deciden `CalculadoraFechaNegocio` y `EvaluadorCupo`.
6. Persistencia de la decisión según RB06. `ClientSequence` y `AuditRecord.Sequence` se calculan como `MAX + 1` bajo el bloqueo. `QuotaUsage` se inserta o se incrementa con `Version + 1`.
7. Commit y devolución del resultado.

Que `BeginTransaction()` emite `BEGIN IMMEDIATE` es una afirmación sobre el proveedor que la documentación no hace: la prueba de la tarea 4.1 lo comprueba. Con una transacción abierta y sin escrituras en la conexión A, la conexión B, con `DefaultTimeout = 1`, no puede abrir la suya. Si la prueba falla, se emite `BEGIN IMMEDIATE` explícito.

- **Descartada: transacción diferida con reintento.** El ascenso de lectura a escritura puede fallar con la base bloqueada y obliga a reintentar toda la transacción; el bloqueo inmediato serializa las confirmaciones, que es lo que RB06 pide.
- **Descartada: API asíncrona del proveedor.** `Microsoft.Data.Sqlite` ejecuta en forma sincrónica aun con los métodos `Async`; la selección técnica ya pone las operaciones en una cola fuera del hilo de interfaz.

### D8. Hash de la intención con forma restringida y esquema versionado

`PayloadHash` = SHA-256 de los bytes UTF-8 de un JSON con las propiedades en orden ordinal y sin espacios: `IdentitySessionId`, `MenuItemId` y `PersonId` como UUID en minúscula, y `MenuVersion` y `Quantity` como enteros. `HashSchema` = `ConsumptionIntent/1`. Para esa forma (sólo ASCII, sin decimales ni anidamiento), la serialización coincide con RFC 8785. El `PayloadHash` del outbox es SHA-256 de los bytes exactos guardados en `comevsContenido`.

- **Descartada: implementar o tomar una librería RFC 8785 general.** Es una dependencia o un parser de números que el corte no necesita todavía; si el receptor lo requiere, llega con `recibir-lotes-idempotente`.
- **Descartada: hashear la fila de la intención.** Mezclaría campos que no son contenido (estado y fechas) y cambiaría al decidir.

### D9. Eventos de outbox con `System.Text.Json` y los nombres del OpenAPI

Records internos de Infrastructure.Sqlite con `[JsonPropertyName]` iguales a `ConsumptionRecorded`, `AppliedQuotaCharge`, `PriceSnapshot` y `ConsumptionDenied` del [OpenAPI](../../../api/openapi.yaml), con `PayloadVersion` 1. El evento no lleva credencial ni código. El estado inicial del evento es `PENDING`.

- **Descartada: DTO en `Contracts` ahora.** Ningún otro proyecto los consume todavía; pasan a `Contracts` cuando el receptor los comparta.

### D10. Reloj y puntos de falla como únicas costuras

`TimeProvider`, que viene en .NET, inyectado en la unidad de trabajo; las pruebas usan una subclase de 10 líneas que fija `GetUtcNow()`. Un `IPuntosDeFalla` interno con `DespuesDeLecturaPrevia`, `AntesDelCommit` y `DespuesDelCommit`, sin operación por defecto y visible al proyecto de pruebas con `InternalsVisibleTo`. Las pruebas de caída lanzan una excepción en `AntesDelCommit` o `DespuesDelCommit`, llaman a `SqliteConnection.ClearAllPools()` y reabren con una conexión nueva. AT03 sincroniza las dos conexiones con un `Barrier(2)` en `DespuesDeLecturaPrevia`, para que ambas hayan visto disponibilidad antes de bloquear.

- **Descartada: un proceso hijo terminado con `Environment.FailFast`.** Es más fiel ante estado sin vaciar, pero suma un proyecto ejecutable y la orquestación del proceso. Lo que se prueba es que el commit deja el estado completo y el replay lo encuentra; la durabilidad del commit frente al proceso es de SQLite con `synchronous = FULL`.
- **Descartada: `Microsoft.Extensions.TimeProvider.Testing`.** Es un paquete más para un reloj fijo que se escribe en diez líneas.

### D11. Pruebas sobre un archivo SQLite temporal por test

Cada test crea `Path.Combine(Path.GetTempPath(), "incomedor-tests", Guid.NewGuid() + ".db")`, lo abre con `Pooling=False` y lo borra al terminar, junto con los archivos `-wal` y `-shm`. Un `FixtureSintetico` carga el escenario de `docs/pruebas.md`: un puesto `SINGLE_KIOSK` en `America/Argentina/Buenos_Aires` con corte 00:00; tres personas (habilitada, inactiva y sin derecho); ALMUERZO compartido por las variantes común y opcional, REFRIGERIO independiente, y quince opciones de menú en ARS con escala 2. Todos los valores son sintéticos. Domain.Tests no usa base.

- **Descartada: SQLite en memoria (`Data Source=:memory:`).** No ejercita WAL, el archivo ni dos conexiones reales, que son justamente lo que prueban AT03 y AT05.
- **Descartada: rollback por test sobre una base compartida.** La unidad de trabajo abre su propia transacción; anidarla en otra cambia la semántica del bloqueo que se está probando.

### D12. Código de identificación fuera de los hechos

La unidad de trabajo nunca lee `NormalizedValue`: recibe la sesión ya abierta. El fixture le da a la persona habilitada un código sintético reconocible (`000123`), y un test busca esa cadena en todas las columnas de texto de intenciones, eventos, comprobantes y bitácora.

- **Descartada: confiar en la revisión de código.** La regla está en `AGENTS.md` y la puede romper cualquier change siguiente; un test la hace verificable.

## Risks / Trade-offs

- [`BeginTransaction()` resulta diferida en esta versión del proveedor] → La prueba 4.1 lo detecta antes de implementar la unidad de trabajo; el plan B es `BEGIN IMMEDIATE` explícito.
- [Una RC con un defecto de `Microsoft.Data.Sqlite` o del SDK] → Se usa la RC2 go-live; el paso a estable del 10/11 es una tarea con fecha y toca dos archivos (D2).
- [Los 13 nombres de columna derivados chocan con la convención de INWeb] → Están en la tabla de D5 para que David los revise en el gate, antes de que exista la migración.
- [El evaluador mínimo se toma como RB02 cumplida] → El proposal la nombra como non-goal y la excepción con más de una regla impide una decisión silenciosa equivocada.
- [Flakiness de AT03 por tiempos] → El `Barrier` fija el orden de lectura; el resultado se verifica por conteo de filas, no por el orden en que terminan las tareas. Se corre 20 veces seguidas en la verificación (tarea 7.3).
- [El ensayo de caída se lee como resistencia a la pérdida del disco] → TK03.1 ya lo dice: no la acredita; el README de la entrega lo repite.

## Migration Plan

No hay despliegue ni datos previos: la migración `0001` crea una base nueva. Antes de aplicarla, David la lee entera (L87:444-461); al ser la primera, no tiene `DROP`, `ALTER` ni `UNIQUE` sobre datos existentes. Revertir es revertir el commit del change y borrar el archivo de base de prueba.

## Open Questions

- ¿Dónde va RB02 completa (HU01.3-H e HU01.3-I)? Decidido por David en el gate del 19/10/2026: un change propio, `evaluar-habilitacion-local`, después de este y antes de `atender-seleccion-y-confirmacion`. No cambia los specs ni las tareas de este change.
