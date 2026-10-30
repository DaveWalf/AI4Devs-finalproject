# Design

## Context

El motivo está en proposal.md («Why»). Lo que condiciona el cómo:

- **El outbox ya existe y nadie lo lee.** `UnidadConfirmacion` inserta en `tbComedorEventosSalida` un evento por decisión, en la transacción de RB06:
  - el estado es `'PENDING'` y la columna no tiene `CHECK`;
  - `comevsContenido` es el JSON de `ConsumptionRecordedEvento` o `ConsumptionDeniedEvento` (`Contracts/Eventos/EventosSalida.cs`), sin código de identificación;
  - `comevsHashContenido` es el SHA-256 de ese texto.

  La tabla no tiene columna de orden, pero sí `rowid`, que sigue el orden de inserción.
- **La base del puesto está en la versión 2** (`BaseComedor`, lista ordenada de migraciones del change 4). El mapa D-C18 ya nombra las tablas del lote: `tbComedorLotesSincronizacion` con `comlotCodigo`, `comlotHashContenidoLote`, `comlotEstado` y `comlotContenidoAcuse`, y `tbComedorLotesSincronizacion_Eventos` (`docs/nomenclatura-campos-er.csv:161-166`).
- **Cuatro pruebas existentes afirman la versión 2** con un literal: `AperturaBaseTests.cs:43`, `ServicioDemostracionTests.cs:27`, `SemillaAtencionTests.cs:69` y `MigracionesTests.cs:33`.
- **El receptor es un esqueleto.** `Sync.Api/Program.cs` mapea las dos rutas a `501`, y `ReceptorEsqueletoTests` lo afirma en tres pruebas, más una cuarta que pide `404` para una ruta no declarada. La clase usa un `WebApplicationFactory<Program>` sin configuración.
- **El contrato es `api/openapi.yaml` 0.6.0-draft.** Ya define el lote, los cuatro tipos de evento, el acuse por evento con sus pares estado-motivo, `ProblemDetails` con `code` y `correlationId`, y mTLS como esquema de seguridad. Las tablas centrales están en el mapa: `tbComedorPuestos`, `tbComedorAcusesLotes`, `tbComedorEventosEntrada`, `tbComedorConsumosConsolidados` y `tbComedorCasosConciliacion` (`nomenclatura-campos-er.csv:186-200` y siguientes).
- **El hash del contenido de una intención** (`HashIntencion`) ya escribe JSON ordenado para una forma restringida. No hay un canonicalizador RFC 8785 general.
- **El equipo tiene SQL Server LocalDB 17.0** (`SqlLocalDB.exe` en `C:\Program Files\Microsoft SQL Server\170\Tools\Binn\`). La CI corre las pruebas en `windows-latest`, cuya imagen trae `Microsoft.VisualStudio.Component.SQL.LocalDB.Runtime`.

## Goals / Non-Goals

**Goals:**

- Que la idempotencia se pruebe con los dos lados reales: el receptor con SQL Server y TLS mutuo, y la cola del puesto con SQLite real. Los únicos dobles son la impresora y el lector, como pide `AGENTS.md`.
- Que nada del envío pueda cambiar una decisión ya tomada, ni en el puesto ni en el receptor.
- Que ninguna entrada del receptor llegue a un `500`, se refleje en una respuesta o se guarde sin validarse.

**Non-Goals:**

- Escala, límite de tasa y procesamiento en segundo plano del receptor: hay un solo puesto.
- Rotación de certificados, revocación por CRL u OCSP y un almacén de certificados de Windows: la demostración usa archivos sintéticos.

## Decisions

### D1. El receptor guarda en SQL Server LocalDB con SQL parametrizado

`Sync.Api` usa `Microsoft.Data.SqlClient`, con la versión estable vigente al aplicar fijada en `Directory.Packages.props`. Corre un script idempotente, `Ingesta/Migraciones/0001_receptor.sql`, embebido como recurso, con un `IF OBJECT_ID(...) IS NULL` por tabla.

| Tabla | Columnas del mapa | Restricciones |
|---|---|---|
| `tbComedorPuestos` | `compueCodigo`, `comInstalacionCodigo`, `comOrganizacionCodigo`, `compueIdentidadCertificado` | PK; `UQ` sobre la identidad |
| `tbComedorAcusesLotes` | `compueCodigo`, `comlotCodigo`, `comaclHashContenido`, `comaclEstado`, `comaclContenidoAcuse` | PK `(compueCodigo, comlotCodigo)`; FK al puesto |
| `tbComedorEventosEntrada` | `comevCodigo`, `compueCodigo`, `comeveHashContenido`, `comeveContenido`, `comeveFechaRecepcionUtc`, `comeveEstadoProyeccion` | PK global `comevCodigo`; `CHECK` del estado: `PROJECTED`, `REJECTED`, `PENDING_REFERENCE`, `IN_REVIEW` |
| `tbComedorConsumosConsolidados` | `comconCodigo`, `comevCodigoOrigen`, `compueCodigo`, `comccoConsumoOriginal` | PK; `UQ` del evento de origen; FK al evento |
| `tbComedorCasosConciliacion` | `comcasCodigo`, `comevCodigo`, `comcasTipo`, `comcasEstado` y `comcasEvidencia` (nueva) | PK; FK al evento original |

- **Conexión**: sale de la configuración (`ConnectionStrings:Receptor`), nunca del código ni del repo.
- **Sin cadena de conexión**: el receptor arranca igual, y las dos operaciones responden `503 SERVICE_UNAVAILABLE`. Así la prueba existente de la ruta no declarada, que no configura nada, sigue pasando sin tocarse (D14).
- **Pruebas**: cada clase crea una base `INComedorPruebas_<guid>` en `(localdb)\MSSQLLocalDB` y la borra al terminar, con `SET SINGLE_USER WITH ROLLBACK IMMEDIATE`.

Descartadas:

- SQLite en el receptor: más simple y sin riesgo de CI, pero contradice ADR02 y deja sin demostrar el «SQL Server de prueba» de TK01.1.
- EF Core: agrega dependencias y esconde el bloqueo de D3, que es lo que la prueba de concurrencia tiene que ver.
- Contenedores (Testcontainers): `windows-latest` no corre contenedores Linux.
- Crear ya `Integration.INWeb` con la migración, como dice TK01.1: un proyecto con un solo consumidor y sin adaptador INWeb. Queda para cuando exista el adaptador.

### D2. Equipo autenticado por mTLS, validado en el receptor

Kestrel escucha HTTPS con `ClientCertificateMode.AllowCertificate`, y un filtro de los dos endpoints valida el certificado:

1. Sin certificado: `401`.
2. Cadena contra la autoridad configurada, con `X509ChainTrustMode.CustomRootTrust`, sin el almacén del sistema y sin revocación (Non-Goals). Si no encadena: `401`.
3. Fuera de su vigencia según el `TimeProvider` del receptor: `401`.
4. Huella SHA-256 sin registro en `tbComedorPuestos`: `403`.

Si pasa, el `KioskId` del registro acota todo lo que sigue. Los tres casos de `401` responden lo mismo (`EQUIPMENT_AUTH_REQUIRED`), para no revelar cuál falló.

- **Pruebas**: generan en memoria, con `CertificateRequest`, una autoridad, un certificado de servidor y certificados de cliente vigentes, vencidos, de otra autoridad y sin registrar. Los exportan a PFX y los vuelven a importar, porque SChannel no usa claves efímeras. Levantan el receptor con Kestrel real: `WebApplicationFactory.UseKestrel(0)`, o `WebApplication` en el puerto 0 si esa API no estuviera.
- **Repo**: ningún certificado entra (`AGENTS.md` y el job de gitleaks).

Descartadas:

- `RequireCertificate` en el handshake: un certificado vencido cortaría la conexión sin respuesta HTTP, y HU02-G pide un error técnico visible y probable.
- El paquete `Microsoft.AspNetCore.Authentication.Certificate`: una dependencia más para cuatro comprobaciones.
- Un encabezado con el `KioskId`: falsificable (OWASP A07).
- Desactivar la autenticación en desarrollo: una puerta que alguien deja abierta.

### D3. Ingesta en una transacción, con un bloqueo de aplicación

`IngestaLotes.Procesar(kioskId, idempotencyKey, cuerpo)`:

1. Si `Idempotency-Key` no es igual a `BatchId`: `400 IDEMPOTENCY_KEY_MISMATCH`.
2. Canonicaliza el cuerpo (D4) y calcula su SHA-256.
3. Abre una transacción `READ COMMITTED` y toma `sp_getapplock 'ingesta-lotes'` exclusivo. Relee el recibo `(kioskId, BatchId)`:
   - con el mismo hash, devuelve `200` con `comaclContenidoAcuse` tal cual se guardó;
   - con otro hash, devuelve `409 BATCH_PAYLOAD_CONFLICT`.
4. Si el lote es nuevo, valida el lote como un todo (D5): si falla, `422` sin escribir nada.
5. Decide cada evento (D6), arma el acuse con `ServerAcknowledgedAtUtc` y `CorrelationId` nuevos, y guarda recibo, eventos de entrada, consumos consolidados y casos.
6. Hace commit y responde `201`.

- **Encabezado**: `X-Correlation-Id` repite el `CorrelationId` del acuse, también en el `200`, que devuelve el original.
- **`202 PENDING`**: nunca se emite. El contrato lo permite y no lo exige.

Descartadas:

- `SERIALIZABLE` con bloqueos de rango: obliga a manejar interbloqueos entre lotes que comparten eventos.
- Insertar y atrapar la violación de clave: sirve para el recibo, pero no para dos lotes distintos que traen el mismo `EventId` a la vez.
- Procesar en segundo plano con `202`: con un puesto no gana nada, y obliga a probar un estado intermedio más.

### D4. JSON canónico RFC 8785 propio, en `Contracts`

`JsonCanonico.Canonicalizar(ReadOnlySpan<byte>)` lee con `JsonDocument`, con `AllowDuplicateProperties = false`, y escribe la forma canónica:

- propiedades ordenadas por unidades UTF-16;
- sin espacios;
- cadenas con el escape mínimo del RFC;
- enteros sin exponente.

Rechaza con `JsonCanonicoException` los números no enteros o fuera de ±(2^53 − 1), que el contrato no usa. Lo usan:

- el receptor, para el hash del lote y de cada evento;
- el puesto, que envía el texto canónico mismo como cuerpo. Así el hash guardado es el hash de los bytes enviados.

Las pruebas usan los ejemplos de orden y de escape de cadenas del RFC 8785.

Descartadas:

- Un paquete NuGet de JCS: una dependencia de terceros para unas 80 líneas (OWASP A06 y A08).
- El hash de los bytes crudos: el mismo lote con otros espacios sería un conflicto, y el contrato pide el cuerpo canónico.

### D5. Validación propia contra el contrato, en dos niveles

`ValidadorLote` decide entre el pedido completo y cada evento:

| Nivel | Qué valida | Si falla |
|---|---|---|
| Pedido | JSON legible, sin propiedades repetidas, números enteros, a lo sumo 1 MiB | `400 INVALID_REQUEST` |
| Lote | `SchemaVersion` 1, `BatchId` y `CreatedAtUtc` con formato, entre 1 y 500 eventos, cada uno con un `EventId` UUID legible y sin repetir | `422 INVALID_BATCH` |
| Evento | El esquema de su tipo, más la regla de aplicación de AT50 | `REJECTED/INVALID_EVENT` |

El esquema de cada evento es el del OpenAPI:

- **Forma**: el discriminador `EventType`, los requeridos y `additionalProperties: false`.
- **Valores**: `PayloadVersion` 1, enums, UUID, `date` y `UtcTimestamp` terminado en `Z`, rangos, y `Quantity` 1.
- **Listas**: `AppliedQuotaCharges` con al menos un elemento.

El límite de 1 MiB se lee con un tope propio sobre el cuerpo. Si Kestrel corta antes, se atrapa su excepción y se responde `400`, no `413`, que el contrato no declara.

Descartadas:

- Una biblioteca de JSON Schema: una dependencia, y la igualdad de fechas de AT50 queda fuera del esquema de todos modos.
- Deserializar directo a los records de `Contracts` y confiar en eso: `System.Text.Json` acepta propiedades extra y valores por defecto que el contrato prohíbe.

### D6. Resultado por evento

Con `h` el SHA-256 del evento canónico, para cada evento válido en el orden del lote:

| Situación en `tbComedorEventosEntrada` | Resultado | Efecto |
|---|---|---|
| `EventId` nuevo, `CONSUMPTION_RECORDED`, `ConsumptionId` nuevo | `ACCEPTED/RECORDED` | entrada `PROJECTED` y consumo consolidado |
| `EventId` nuevo, `CONSUMPTION_RECORDED`, `ConsumptionId` ya consolidado | `CONFLICT/EVENT_PAYLOAD_CONFLICT` | caso `CONSUMPTION_ID_CONFLICT`; el evento no se guarda como entrada |
| `EventId` nuevo, `CONSUMPTION_DENIED` | `ACCEPTED/RECORDED` | entrada `PROJECTED`, sin consumo |
| `EventId` nuevo e inválido (D5) | `REJECTED/INVALID_EVENT` | entrada `REJECTED` con el contenido recibido |
| `EventId` existente del mismo puesto, mismo `h` | según la entrada: `PROJECTED` da `DUPLICATE/ALREADY_RECORDED`; `REJECTED` da `REJECTED/INVALID_EVENT`; `PENDING_REFERENCE` o `IN_REVIEW` repiten su `NEEDS_REVIEW` | ninguno |
| `EventId` existente con otro `h` o de otro puesto | `CONFLICT/EVENT_PAYLOAD_CONFLICT` | caso `EVENT_PAYLOAD_CONFLICT`; la entrada original no cambia |

- **Caso de conciliación**: `comcasEvidencia` guarda un JSON con el puesto, el `BatchId`, la hora y el evento recibido. Nunca guarda el certificado. El estado del caso es `OPEN`; resolverlo es del contrato complementario de auditoría, fuera de este change.
- **Columna nueva**: `comcasEvidencia` no está en el mapa. Se agrega a `docs/nomenclatura-campos-er.csv` en este change.

Descartadas:

- Guardar el contenido conflictivo en `tbComedorEventosEntrada` con otra clave: rompe la PK global por `EventId` del modelo.
- Resolver el conflicto quedándose con el último: reescribe un hecho ya incorporado.

### D7. Impresión e incidencias en el receptor; el puesto no las emite

- **`PRINT_EVENT_RECORDED`**:
  - si el `ConsumptionId` está consolidado para el mismo puesto, la entrada queda `PROJECTED` y el resultado es `ACCEPTED/RECORDED`;
  - si no, queda `PENDING_REFERENCE` y el resultado es `NEEDS_REVIEW/RELATED_EVENT_PENDING`.

  Al consolidar un consumo, en la misma transacción, toda entrada `PENDING_REFERENCE` de ese consumo y de ese puesto pasa a `PROJECTED`. El acuse ya guardado de su lote no cambia.
- **`CONNECTIVITY_INCIDENT_RECORDED`** válido: entrada `IN_REVIEW`, resultado `NEEDS_REVIEW/MULTI_KIOSK_POLICY_REVIEW`.
- **`UNKNOWN_CONFIGURATION_VERSION`**: no se emite, porque el receptor no publica configuración en este corte.

El puesto no emite `PRINT_EVENT_RECORDED`:

- ningún AT de HU02.2 lo necesita;
- la bitácora ya registra cada cambio de impresión;
- emitirlo cambiaría `TrabajosImpresion` y `Asistencia` del change 4, con sus pruebas.

Descartadas:

- Rechazar esos dos tipos con `REJECTED/INVALID_EVENT` o con un `422`: diría que un evento válido del contrato es inválido.
- Emitir los eventos de impresión ya: más superficie en un change que no los necesita para cerrar sus AT.

### D8. Errores con la forma del contrato y sin reflejar la entrada

`ProblemasReceptor` arma cada error:

- `type` es `https://api.incomedor.example/problems/<código-en-kebab>`;
- `title` es un texto fijo por código;
- `code` sale del enum del contrato;
- `correlationId` es un UUID nuevo, repetido en `X-Correlation-Id`;
- no hay `detail`.

Una excepción no prevista pasa por `UseExceptionHandler` y responde `503 SERVICE_UNAVAILABLE` con `Retry-After: 2`. El cliente reintenta el mismo lote, y eso es inocuo porque el lote es idempotente. El log lleva el `correlationId` y el tipo de la excepción, nunca el cuerpo.

Descartadas:

- `Results.Problem` tal como está: no lleva `code` ni `correlationId`, que el esquema `ProblemDetails` exige.
- Responder `500`: el contrato no lo declara.

### D9. Migración `0003` del puesto: lotes con su cuerpo

`0003_lotes_sincronizacion.sql`:

- **`tbComedorLotesSincronizacion`**:
  - `comlotCodigo` es la PK;
  - `comlotContenidoLote` guarda el texto canónico enviado, y es una columna nueva del mapa;
  - `comlotHashContenidoLote` es un BLOB de 32 bytes;
  - `comlotEstado` tiene `CHECK IN ('PENDING', 'COMPLETED')`;
  - `comlotContenidoAcuse` admite nulo.
- **`tbComedorLotesSincronizacion_Eventos`**: PK `(comlotCodigo, comevCodigo)`, con FK al lote y a `tbComedorEventosSalida`.

`BaseComedor` pasa a `UltimaVersionConocida = 3`. Los estados del evento en `comevsEstado` son `PENDING`, `ACKNOWLEDGED`, `CONFLICT`, `REJECTED` y `NEEDS_REVIEW`. Los valida el código: agregar un `CHECK` a una tabla existente obliga en SQLite a reconstruirla.

Cambian cuatro pruebas existentes, con la aprobación del gate y sólo en esto:

- `AperturaBaseTests.Base_vacia_queda_en_version_2_con_las_21_tablas`: `user_version` 3, las dos tablas nuevas en `TablasEsperadas`, y su nombre y su `DisplayName` («versión 3», «23 tablas»).
- `ServicioDemostracionTests.Primer_arranque_siembra_los_datos_sinteticos`: la línea 27 espera 3 y su `DisplayName` dice «esquema 3».
- `SemillaAtencionTests.Atencion_sobre_una_base_de_la_demostracion_en_version_1`: la línea 69 espera 3 y su `DisplayName` dice «de la versión 1 a la 3».
- `MigracionesTests.Base_en_version_1_se_actualiza_sin_perder_el_consumo`: la línea 33 espera 3 y su `DisplayName` dice «queda en la versión 3».

Descartadas:

- Reconstruir el cuerpo desde los eventos en cada reintento: depende de que el orden y el texto de cada evento no cambien nunca, y el contrato pide reenviar «exactamente el lote original».
- Cambiar las cuatro pruebas para que lean `UltimaVersionConocida`: una prueba que compara el código consigo mismo deja de detectar una migración olvidada.

### D10. La cola del puesto

`ColaSincronizacion`, en `Infrastructure.Sqlite`, trabaja en `BEGIN IMMEDIATE`:

- **`Preparar()`**:
  - si hay un lote `PENDING`, lo devuelve tal cual;
  - si no, toma hasta 100 eventos `PENDING` que no estén en ningún lote, ordenados por `rowid`, y arma el cuerpo con un `BatchId` nuevo, `SchemaVersion` 1, `CreatedAtUtc` del reloj y `Events` con el contenido guardado de cada evento;
  - lo canonicaliza (D4), guarda lote e ítems y lo devuelve. Sin eventos, devuelve nada.
- **`RegistrarAcuse(batchId, acuse)`**:
  - valida que sea `COMPLETED`, del mismo `BatchId` y con exactamente un resultado por evento del lote. Si no, no cambia nada y escribe `SYNC_ACK_INVALID` en la bitácora;
  - si vale, pasa cada evento a `ACKNOWLEDGED` (por `ACCEPTED` y `DUPLICATE`) o a `CONFLICT`, `REJECTED` o `NEEDS_REVIEW`;
  - guarda el acuse, deja el lote `COMPLETED` y escribe `SYNC_BATCH_ACKNOWLEDGED` con los conteos por estado.
- **`RegistrarError(batchId, codigo)`**: escribe `SYNC_BATCH_FAILED` con el código HTTP o `NETWORK`, sin tocar el lote.

Ninguna de las tres toca intenciones, consumos, cupo, comprobantes ni trabajos.

Descartadas:

- Marcar los eventos `SENT` al enviarlos: una respuesta perdida dejaría eventos marcados sin acuse durable (AT27).
- Reenviar solos los eventos en `CONFLICT` o `REJECTED`: el contrato pide conservarlos para diagnóstico y conciliación, y un reenvío daría el mismo resultado.

### D11. El sincronizador y el cliente HTTP

- **`IReceptorLotes`** (puerto, en `Infrastructure.Sqlite`) tiene `EnviarAsync(lote)` y `ConsultarAsync(batchId)`. Devuelve `Acusado(acuse)`, `NoEncontrado`, `Rechazado(status, code)` o `SinRespuesta`.
- **`SincronizadorPuesto.EnviarUnaVezAsync()`** recorre un ciclo:
  1. Prepara.
  2. Si el lote ya tuvo un intento sin respuesta, consulta primero. Si encuentra el acuse, lo registra; si responde `NoEncontrado`, reenvía el mismo cuerpo.
  3. Registra el acuse o el error.

  El intento queda en memoria, y tras un reinicio se consulta siempre, porque consultar es inocuo. El ciclo publica un `EstadoSincronizacion` con pendientes, retenidos, el último error y si es de credencial.
- **`ClienteReceptorHttp`** (en `Kiosk/Sincronizacion/`) usa un `HttpClient` con el certificado del puesto y valida el servidor contra la autoridad de la demostración, con `CustomRootTrust`. El tiempo de espera es de 10 s, y `Idempotency-Key` es el `BatchId`.
- **Ciclo periódico**: un `PeriodicTimer` de 30 s, de a un ciclo por vez, arrancado por `ServicioAtencion` sólo si hay receptor configurado.
- **`FranjaSincronizacion.razor`**, al pie de la atención junto a la franja de la impresora, muestra:
  - «Sincronización: al día»;
  - «N pendientes»;
  - «N retenidos para revisión»;
  - «Error técnico: credencial del equipo»;
  - «Sin receptor configurado».

Descartadas:

- Enviar dentro de la confirmación: ata la atención a la red, y AT01 y HU02-C lo prohíben.
- Cliente HTTP en `Infrastructure.Sqlite`: mezcla red y base en un proyecto que nombra su motor. El puerto queda ahí, como `IImpresoraComprobantes`.
- Un trabajador sin estado visible: HU02-G pide un error técnico visible.

### D12. Certificados y modo demostración

`Sync.Api` con `--demostracion` hace esto:

1. Si falta `%LOCALAPPDATA%\INComedor\demo\certificados\`, genera:
   - `autoridad.pfx`, con `CN=INComedor Autoridad Demostracion`;
   - `receptor.pfx`, para `localhost`;
   - `puesto.pfx`, con `CN=INComedor Puesto Demostracion`.

   Los tres van sin contraseña.
2. Usa la base `INComedorReceptorDemo` de LocalDB.
3. Registra la huella del puesto con el `KioskId` del puesto de la semilla de la demostración.
4. Escucha en `https://localhost:5001`.

El Kiosk configura el receptor si encuentra `puesto.pfx` y `autoridad.pfx` en esa carpeta. Sin `--demostracion`, el receptor lee las rutas, la autoridad y la cadena de conexión de su configuración, y no trae ningún valor por defecto con secretos.

Descartadas:

- Commitear certificados sintéticos: `AGENTS.md` lo prohíbe y gitleaks lo marca.
- Proteger los PFX con una contraseña guardada con DPAPI: agrega `System.Security.Cryptography.ProtectedData` para material sintético que ya vive en el perfil del usuario.
- Que el puesto confíe en cualquier certificado de servidor: OWASP A07.

### D13. Pruebas: cada escenario con la suya, vista en rojo antes

| Proyecto | Archivo nuevo | Qué prueba |
|---|---|---|
| `Contracts.Tests` (proyecto nuevo) | `JsonCanonicoTests.cs` | D4, con los ejemplos del RFC 8785 |
| `Sync.Api.Tests` | `CertificadosDePrueba.cs` y `ReceptorConKestrel.cs` | auxiliares: autoridad y certificados en memoria, Kestrel real y base temporal de LocalDB |
| `Sync.Api.Tests` | `AutenticacionEquipoTests.cs` | «Equipo autenticado por certificado» |
| `Sync.Api.Tests` | `LoteIdempotenteTests.cs` | «Lote idempotente por BatchId» y «Lote inválido sin efectos» |
| `Sync.Api.Tests` | `ResultadoPorEventoTests.cs` | deduplicación, consolidación, RB16, AT50, eventos en revisión |
| `Sync.Api.Tests` | `ConsultaYErroresTests.cs` | consulta del acuse, errores sin reflejar y la ruta sin cadena de conexión |
| `Infrastructure.Sqlite.Tests` | `MigracionLotesTests.cs` | escenarios nuevos del delta de `registro-consumo` |
| `Infrastructure.Sqlite.Tests` | `ColaSincronizacionTests.cs` | D10 y los escenarios de `envio-lotes` sin red |
| `Infrastructure.Sqlite.Tests` | `SincronizadorPuestoTests.cs` | D11 con un `IReceptorLotes` de prueba que simula respuestas perdidas, `404`, `409` y `401` |
| `Kiosk.Tests` | `EnvioDeLotesIntegradoTests.cs` | AT27 y AT13 de punta a punta: SQLite del puesto, `ClienteReceptorHttp`, Kestrel con mTLS y LocalDB |
| `Kiosk.Tests` | `FranjaSincronizacionTests.cs` | bUnit: los cinco mensajes |
| `Kiosk.Tests` | `ModoDemostracionReceptorTests.cs` | «Receptor y puesto de demostración conectados», con una carpeta temporal en lugar de `%LOCALAPPDATA%` |

- **El doble de `IReceptorLotes`** en `SincronizadorPuestoTests` es una excepción declarada a «dobles sólo para impresora y lector». Hace falta para provocar la respuesta perdida en el punto exacto. La misma situación se prueba también de punta a punta en `EnvioDeLotesIntegradoTests`, cortando la conexión después de que el receptor hizo commit.
- **Nombres**: cada `DisplayName` empieza por su AT, su regla o su categoría OWASP.

### D14. Las pruebas del esqueleto

En `ReceptorEsqueletoTests.cs`, con la aprobación del gate:

- se borran las pruebas `Envio_de_un_lote_responde_501_problem_json`, `Consulta_de_un_lote_responde_501_problem_json` y `Cuerpo_que_no_es_json_responde_501_sin_reflejarlo`;
- el resumen de la clase pasa a decir que cubre la ruta no declarada;
- `Ruta_no_declarada_responde_404` queda igual, byte por byte, y pasa sin configuración gracias a D1.

La intención de «Cuerpo que no es JSON» sigue en `ConsultaYErroresTests`, con un cliente autenticado y `400`.

Descartada: cambiarlas a `400` y `201` en el mismo archivo. Sin certificado, las tres darían `401`, así que habría que reescribirlas enteras sobre otra fábrica. Es más claro borrarlas y probar la conducta nueva en archivos nuevos.

## Risks / Trade-offs

- **[LocalDB no arranca en la CI]** La imagen trae el componente, pero no se probó en este repo. → La primera corrida de la CI después del push lo muestra. Si falla, hace falta un paso `sqllocaldb start MSSQLLocalDB` en `ci.yml`, configuración compartida que decide David. Saltear las pruebas no es una opción.
- **[`Microsoft.Data.SqlClient` y su DLL nativa]** Hay una lección guardada sobre la DLL SNI en una ventana de ruta. → Las pruebas corren el camino real desde `dotnet test`, no desde una copia de la salida.
- **[Claves efímeras con SChannel]** Windows no acepta un certificado de `CertificateRequest` con clave efímera para TLS. → Exportar e importar el PFX en `CertificadosDePrueba` (D2), como primera prueba del grupo.
- **[PFX sin contraseña en el perfil]** Son sintéticos y viven en la carpeta que la demostración borra. Robarlos sólo permite enviar lotes sintéticos a un receptor local. → Se declara en el README.
- **[Un bloqueo de aplicación global serializa toda la ingesta]** Con un puesto y lotes de hasta 500 eventos no se nota. → El multipuesto de la segunda ola lo revisa.
- **[Orden de archivado]** El delta de `modo-demostracion` modifica «Conjunto sintético de atención», que agrega el change 4. `openspec validate` avisa que el archivado lo rechazaría hoy. → El change 4 se archiva antes que este, y eso queda como precondición del apply.
- **[Siete pruebas existentes cambian]** Es la mayor superficie de pruebas tocadas en un change. → Cada cambio está nombrado en D9 y D14, y la tarea lo verifica con `git diff -- tests/`.
- **[Sin `429`]** Un cliente autenticado puede enviar sin pausa. → Los límites de 1 MiB y 500 eventos acotan cada pedido, y sólo un puesto registrado llega a la ingesta.

## Migration Plan

- **Puesto**: abrir la base aplica la `0003`, sin pasos manuales. Los eventos `PENDING` que ya existan salen en el primer lote.
- **Receptor**: el script `0001_receptor.sql` corre al arrancar con una cadena de conexión configurada.
- **Vuelta atrás**: borrar `%LOCALAPPDATA%\INComedor\demo\` y la base `INComedorReceptorDemo` de LocalDB.
- **CI**: puede necesitar el paso de LocalDB (Risks).
