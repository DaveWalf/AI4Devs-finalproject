# Proposal

## Why

El puesto ya escribe un evento de outbox por cada decisión (`CONSUMPTION_RECORDED` o `CONSUMPTION_DENIED`, en `PENDING`), pero nadie los lee: no hay lotes, no hay envío y el receptor responde `501` a todo. La meta de la final según D-C24 incluye el «outbox y receptor idempotente», y HU02.2 es must. Sin este change, un acuse perdido no tiene cómo reenviarse y no hay ningún lugar donde los hechos se consoliden sin duplicarse.

Va ahora porque es el quinto change del orden vigente (§4.4 del documento de prácticas): la confirmación atómica, la habilitación local y la atención ya escriben los hechos que este change envía.

Origen: historia HU02.2 (escenarios HU02-C, HU02-D y HU02-G; el F queda fuera por D-C24) y ticket TK01.1 (receptor idempotente de lotes), con sus AT13, AT27 y AT50. El AT12, conflicto de cupo entre dos terminales, es el escenario F y queda en la segunda ola.

## What Changes

- **Receptor que ingiere lotes**: `POST /api/incomedor/v1/sync-batches` y `GET /api/incomedor/v1/sync-batches/{batchId}` se comportan como dice `api/openapi.yaml` 0.6.0-draft. **BREAKING** para el esqueleto: deja de responder `501`, y tres pruebas existentes que lo afirman se reemplazan sólo con tu aprobación en este gate.
- **Equipo autenticado por certificado (mTLS)**: un certificado ausente, vencido o de una autoridad no confiable responde `401 EQUIPMENT_AUTH_REQUIRED`, y uno válido pero no registrado responde `403 EQUIPMENT_FORBIDDEN`. Un lote de otro puesto se presenta como desconocido (AT13).
- **Idempotencia por lote**: `Idempotency-Key` igual a `BatchId` y hash SHA-256 del cuerpo canónico RFC 8785. El mismo lote devuelve el acuse original y otro cuerpo con la misma clave responde `409`, sin alterar nada (AT27).
- **Resultado por evento**: deduplicación global por `EventId` (`ACCEPTED`, `DUPLICATE`, `CONFLICT`, `REJECTED` o `NEEDS_REVIEW`) y un consumo consolidado por `ConsumptionId`. La igualdad de `BusinessDate` entre el consumo y sus cargas se valida por aplicación (AT50). Un conflicto conserva las dos evidencias y nunca niega una comida (RB16).
- **Persistencia central en SQL Server de prueba (LocalDB)**, con SQL parametrizado y los nombres del mapa D-C18 (`tbComedorAcusesLotes`, `tbComedorEventosEntrada`, `tbComedorConsumosConsolidados`, `tbComedorCasosConciliacion` y `tbComedorPuestos`).
- **Cola de envío en el puesto**: el lote se guarda antes de enviarse y se reenvía idéntico. La cola sólo avanza con un acuse durable y válido. Los eventos en conflicto, rechazados o en revisión quedan retenidos, sin purgarse ni reenviarse solos. Un error de credencial queda visible y la atención sigue sin red (HU02-C y HU02-G).
- **Migración `0003` del puesto**: `tbComedorLotesSincronizacion` y `tbComedorLotesSincronizacion_Eventos`, con la base en la versión de esquema 3. **BREAKING** para cuatro pruebas existentes que afirman la versión 2; cambian sólo con tu aprobación en este gate.
- **Demostración conectada**: el receptor arranca con `--demostracion`, genera certificados sintéticos fuera del repo y registra el puesto de la demostración. El puesto los usa si existen, y muestra el estado de la sincronización al pie de la atención.
- **Evidencia y tallas**: la talla de la IA y la de David, con el colchón del 30 %, en `docs/plan-academico.md` y `docs/aplicacion-del-curso.md`.

## Non-goals

- **HU02-F y AT12**: discrepancia de cupo entre puestos y su caso de conciliación (D-C24, multipuesto en la segunda ola). Este change sí abre un caso cuando hay un conflicto técnico de contenido de un mismo `EventId` o de un mismo `ConsumptionId`.
- **Emitir `PRINT_EVENT_RECORDED` desde el puesto**: los cambios del trabajo de impresión siguen en la bitácora (D9 del change 4). El receptor sí acepta ese tipo de evento porque el contrato lo declara (D7 del design).
- **`GET /configuration`, publicación de snapshots, feed de pares, incidencias de conectividad y políticas multipuesto**: segunda ola. Por eso el receptor no emite `UNKNOWN_CONFIGURATION_VERSION`.
- **Emisión, renovación y revocación reales de certificados de equipo**: el contrato las deja «antes de implementación». La demostración usa certificados sintéticos generados en el equipo.
- **Procesamiento asíncrono (`202 PENDING`), límite de tasa (`429`) y el adaptador INWeb real** (`Integration.INWeb`).
- **Cambios en `api/openapi.yaml`, en las migraciones `0001` y `0002` o en la decisión de RB06 y RB02.**

## Capabilities

### New Capabilities

- `envio-lotes`: la cola de salida del puesto, que arma lotes durables con los eventos del outbox, los reenvía idénticos, avanza sólo con un acuse durable, retiene lo que el receptor no incorporó y muestra el estado sin cortar la atención.

### Modified Capabilities

- `sincronizacion-lotes`: se quita «Ningún acuse sin ingesta durable» (el `501` del esqueleto). Se agregan la autenticación del equipo, la idempotencia por lote, el resultado por evento, la validación por aplicación, la consulta del acuse, los errores que no reflejan la entrada y la ruta no declarada.
- `registro-consumo`: «Base SQLite con esquema versionado» aplica `0001` a `0003` y queda en la versión 3, y «Unicidades del modelo en la base» suma un evento por lote una sola vez. Las dos salen de partir en el gate un requirement que pasaba los 500 caracteres de `openspec validate --strict`, con el texto tal cual.
- `modo-demostracion`: la base de la demostración queda en la versión 3 y se agrega «Receptor y puesto de demostración conectados».

## Impact

- **Código**:
  - `src/Intelektron.INComedor.Contracts`: el JSON canónico RFC 8785, compartido por el puesto y el receptor, y los records del lote y del acuse.
  - `src/Intelektron.INComedor.Sync.Api`: autenticación por certificado, validación, ingesta, consulta, errores, migración del receptor y modo demostración.
  - `src/Intelektron.INComedor.Infrastructure.Sqlite`: la migración `0003`, la cola de lotes, el puerto `IReceptorLotes` y el sincronizador.
  - `src/Intelektron.INComedor.Kiosk`: el cliente HTTP con certificado, el ciclo periódico y la franja de estado.
- **Pruebas**: un proyecto nuevo, `tests/Intelektron.INComedor.Contracts.Tests`, y archivos nuevos en `Sync.Api.Tests`, `Infrastructure.Sqlite.Tests` y `Kiosk.Tests`. Cambian siete pruebas existentes, sujetas al gate.
- **Dependencia nueva**: `Microsoft.Data.SqlClient`, sólo en el receptor.
- **CI**: las pruebas del receptor necesitan LocalDB en `windows-latest`. Si el runner no lo trae, hace falta un paso en `ci.yml`, que es tuyo.
- **Documentos**: `AGENTS.md` (comando del receptor), `readme.md`, `docs/nomenclatura-campos-er.csv` (dos columnas nuevas), `docs/plan-academico.md`, `docs/aplicacion-del-curso.md` y `docs/cobertura-entrega.md`.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David, 30/10/2026 (en el gate) | **S** | 2 a 4 h (`docs/plan-academico.md`: «**S** de 2 a 4 h») | 2,9 a 5,7 h de capacidad |
| La IA (Claude Code, Opus 5.5), 30/10/2026 | **S** | 2 a 4 h | 2,9 a 5,7 h de capacidad |

El colchón es el de la lección 50 («Buffers realistas: 30-40%, no 10%»): se compromete el 70 % de la capacidad, así que cada talla pide su valor dividido por 0,7.

**Por qué la IA da S.** Parte de lo medido, sin promediar:
- **Change 3**: la IA dijo M y el apply le llevó 0,2 h para 10 tareas.
- **Change 4**: dijo S, estimó 1 a 1,5 h de reloj y le llevó 1,0 h para 24 tareas, 81 pruebas nuevas, cuatro capas y una migración.

Este change tiene una superficie parecida, cerca de 1,2 veces la del change 4: un receptor desde cero, una cola en el puesto, una migración y del orden de 90 pruebas. Suma dos fricciones que el change 4 no tuvo:
- SQL Server LocalDB con `Microsoft.Data.SqlClient`;
- TLS mutuo real en las pruebas, donde Windows exige claves persistidas para los certificados generados.

La estimación de reloj de la IA es de 1,5 a 2,5 h: cae en el piso de la S, y el resto de la S es margen para esas dos fricciones.

No se promedia (lección 50: «Se converge por discusión»): si tu talla difiere, la diferencia se conversa en el gate.

## Para el gate de David

Lo que en otras exploraciones se te preguntaba; cada punto con la opción que se tomó y por qué. El detalle está en el design.

1. **Siete pruebas existentes cambian.** Tu aprobación del gate es la aprobación de estos cambios y de ninguno más:
   - **Por la versión de esquema 3** (D9):
     - `AperturaBaseTests.Base_vacia_queda_en_version_2_con_las_21_tablas`: versión 3 y 23 tablas (suma `tbComedorLotesSincronizacion` y `tbComedorLotesSincronizacion_Eventos`), con su nombre y su `DisplayName`.
     - `ServicioDemostracionTests.Primer_arranque_siembra_los_datos_sinteticos`: la línea 27 espera 3, y su `DisplayName` dice «esquema 3».
     - `SemillaAtencionTests.Atencion_sobre_una_base_de_la_demostracion_en_version_1`: la línea 69 espera 3, y su `DisplayName` dice «de la versión 1 a la 3».
     - `MigracionesTests.Base_en_version_1_se_actualiza_sin_perder_el_consumo`: la línea 33 espera 3, y su `DisplayName` dice «queda en la versión 3».
   - **Por el fin del `501`** (D14): en `ReceptorEsqueletoTests` se borran las tres pruebas que afirman `501` («Envío de un lote», «Consulta de un lote» y «Cuerpo que no es JSON») y el resumen de la clase. Su intención pasa a pruebas nuevas: «Cuerpo que no es JSON» vuelve como `400 INVALID_REQUEST` sin reflejar la entrada. «Ruta no declarada: 404» queda igual, byte por byte.

   Descartada: una tabla de lotes sin versión nueva, que no existe en SQLite sin migración. También, guardar el lote fuera de la base, que pierde la durabilidad que pide el contrato («El cliente guarda BatchId y el lote antes de enviar»).
2. **El receptor guarda en SQL Server LocalDB, no en SQLite** (D1). Tomada: es lo que dicen ADR02 y TK01.1 («sobre SQL Server de prueba»). LocalDB está en este equipo y, como componente de Visual Studio, en la imagen `windows-latest`. Descartadas:
   - SQLite, más simple y sin riesgo de CI, pero deja sin demostrar el «SQL Server de prueba» del plan;
   - EF Core, que agrega una dependencia y esconde los bloqueos que prueban la idempotencia concurrente.

   Si preferís SQLite, cambian D1, D3 y el grupo 3 de las tareas, no las specs.
3. **La autenticación es mTLS real, también en las pruebas** (D2). Tomada: certificados sintéticos generados en memoria en cada corrida, nunca en el repo (`AGENTS.md` y gitleaks). Descartadas:
   - un encabezado con el `KioskId`, falsificable (OWASP A07);
   - un atajo que desactive la autenticación en desarrollo.
4. **Certificados de la demostración** (D12). Tomada: el receptor con `--demostracion` los genera en `%LOCALAPPDATA%\INComedor\demo\certificados\`, la carpeta que `AGENTS.md` ya manda borrar para reiniciar. Los PFX quedan sin contraseña dentro del perfil del usuario porque son sintéticos; el riesgo está en el design. Descartado: commitearlos.
5. **`PRINT_EVENT_RECORDED`: el receptor lo acepta, el puesto no lo emite** (D7). Tomada así porque:
   - ningún AT de HU02.2 lo pide;
   - emitirlo cambiaría las transacciones de impresión y asistencia del change 4, con sus pruebas;
   - la bitácora ya lo audita.

   El receptor lo acepta porque es una entrada válida del contrato: lo consolida si conoce el consumo, y si no, queda `NEEDS_REVIEW/RELATED_EVENT_PENDING` hasta que el consumo llegue.
6. **`CONNECTIVITY_INCIDENT_RECORDED` queda en revisión** (D7). Tomada: se guarda como `NEEDS_REVIEW/MULTI_KIOSK_POLICY_REVIEW`, porque las incidencias son de la segunda ola y el contrato no tiene un motivo «no soportado». Descartada: `REJECTED/INVALID_EVENT`, que diría que el evento es inválido cuando no lo es.
7. **El receptor procesa en el mismo pedido y nunca responde `202`** (D3). Tomada: el contrato lo permite y no lo exige; con un puesto, procesar en el pedido deja el acuse durable antes de responder. Sin cambios en `api/openapi.yaml`.
8. **El ciclo de envío corre cada 30 s en segundo plano** (D11). HU02-C pide que el puesto siga atendiendo mientras sincroniza. El change 4 descartó un trabajador para imprimir (su D7), pero acá el envío depende de la red y no puede bloquear la pantalla.
9. **Dos columnas fuera del mapa D-C18** (D9 y D6): `comlotContenidoLote`, para reenviar el lote byte por byte, y `comcasEvidencia`, para conservar la evidencia de un conflicto. Se agregan al mapa en este change. Descartados: reconstruir el lote desde los eventos, que depende de que nada cambie el orden, y descartar la evidencia, que incumple el contrato («conserva ambas evidencias»).
10. **Un doble más que la impresora y el lector** (D13). Tomada: un `IReceptorLotes` de prueba en `SincronizadorPuestoTests`, para cortar la respuesta en el punto exacto. La misma situación se prueba también de punta a punta, contra el receptor real. `AGENTS.md` admite dobles sólo para la impresora y el lector, así que necesita tu aprobación. Descartada: probar el sincronizador sólo de punta a punta, donde algunos cortes (el `202`, un acuse incompleto) no se pueden provocar sin tocar el receptor.
11. **El change 4 se archiva antes que este** (Risks del design). El delta de `modo-demostracion` corrige a «versión 3» un escenario que agrega el change 4; `openspec validate` avisa que el archivado lo rechazaría mientras el change 4 no esté archivado. El archivado es tuyo, y queda como precondición del apply.
12. **Aplicar con `opus` · `xhigh`, supervisado.** El apéndice manda la línea de seguridad cuando hay seguridad en alcance (mTLS y entrada externa), y un salto de `medium` a `xhigh` es el costo de eso. Descartado: `opus` · `medium` como en el change 4, que no tenía entrada externa.
13. **Tallas**: la tuya queda vacía para que la pongas acá. En el gate del 30/10/2026 David puso **S**, la misma talla que la IA.
