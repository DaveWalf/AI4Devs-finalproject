# Proposal

## Why

El primer corte (D-C24) se sostiene sobre una sola garantía: una confirmación en el puesto es todo o nada (RB06) y repetirla nunca crea un segundo consumo. Hoy esa garantía es sólo documento; sin ella, la selección, el comprobante y el outbox de los changes siguientes no tienen sobre qué apoyarse. Es el paso 2 del plan hacia la entrega 2 del 22/10, que pide los scaffolds de back, front y BD conectados (L58:112-130), y este change trae el primero: la solución .NET y la base SQLite.

Origen: **TK03.1** (Datos: migración inicial y transacción de confirmación), al servicio de **HU01.1** (confirmar un servicio sin red y sin duplicarlo, escenario HU01-G) y **HU02.1** (recuperar el resultado después de una caída, escenarios HU02-B y HU02-L). Cubre también HU01.3-C (último cupo con dos confirmaciones concurrentes), que es AT03, el AT propio de TK03.1.

## What Changes

- **Solución .NET mínima del corte**, con los nombres de `readme.md` §2.3: `Intelektron.INComedor.Domain`, `Intelektron.INComedor.Infrastructure.Sqlite` y sus dos proyectos de pruebas, más `INComedor.sln`, `global.json` y la gestión central de paquetes.
- **Migración `0001`** con las tablas que la transacción de confirmación escribe y las de configuración que sus claves referencian, con los nombres físicos del mapa D-C18. Apertura con `PRAGMA foreign_keys = ON`, comprobación de la versión de esquema y rechazo de una base de versión desconocida.
- **Confirmación atómica (RB06)**: búsqueda previa por `IntentId`; transacción `BEGIN IMMEDIATE` con relectura de la intención, del snapshot activo y del cupo bajo el bloqueo; decisión y persistencia en la misma transacción. Una autorización escribe intención, consumo, consumo conocido propio con sus cargas, uso de cupo, comprobante, trabajo de impresión original, evento de outbox y registro de bitácora; un rechazo escribe intención, evento y registro, sin consumo ni comprobante.
- **Replay por `IntentId`** (RB07): mismo `IntentId` con el mismo contenido devuelve el resultado original sin revalidar; con otro contenido es conflicto y no escribe nada.
- **Evaluador mínimo de dominio** para que AT03 decida de verdad: cupo por `PersonId` + `QuotaScopeId` + `QuotaBucketId` + `BusinessDate` (RB03) con el límite de la regla por persona vigente, y fecha de negocio con la zona y el corte configurados (RB04).
- **D-C25** en `docs/decisiones.md`: el SDK pasa de .NET 10 a .NET 11 (`11.0.100-rc.2.26504.105`, `net11.0`, paquetes de Microsoft `11.0.0-rc.2.*`); enmienda la selección técnica sin borrarla. Se alinean las siete menciones de «.NET 10» del repo y el comando de `AGENTS.md` queda vigente.

## Non-goals

- **Reglas de habilitación completas (RB02)**, que TK03.1 deja a TK01: persona inactiva, `Limit=0` con precedencia, `Priority` entre reglas, reglas por grupo y vigencia de credenciales. El evaluador de este change sólo resuelve el cupo con la única regla por persona vigente; HU01.3-H e HU01.3-I no se cubren aquí.
- Pantallas, identificación simulada y selección (`atender-seleccion-y-confirmacion`, TK02.1); la sesión de identidad llega como dato de prueba.
- Reconfirmación por cambio de catálogo (HU01-D, RB10) más allá de no confirmar con una versión de menú que no es la vigente.
- Sincronización, envío de lotes y receptor (`recibir-lotes-idempotente`, TK01.1); el outbox queda escrito, nadie lo envía.
- Impresión física y reimpresión (HU03.1): se crea el trabajo original con copia 0 y nada lo procesa.
- Feed de pares, multipuesto, archivos mensuales de bitácora, backup, activación de snapshots y auditoría de saltos del reloj (segunda ola, D-C24).
- Canonicalización RFC 8785 general y DTO en `Contracts`: llegan con el receptor.
- CI y el proyecto `Kiosk`: son pasos 3 y 4 del plan, cada uno con su change o su commit.
- Actualizar los mapas CSV de nomenclatura y el ER: los nombres de las columnas sin mapa se registran en `design.md`.
- Pasar a la versión estable de .NET 11: es la tarea del 10/11/2026, antes de la entrega final del 12/11.

## Capabilities

### New Capabilities

- `registro-consumo`: confirmación local atómica de una intención de consumo (RB06), su replay idempotente por `IntentId` (RB07), la recuperación tras una caída (RB17) y la decisión de cupo por fecha de negocio (RB03 y RB04) sobre la base SQLite del puesto.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacía.

## Impact

- Código nuevo: `global.json`, `INComedor.sln`, `Directory.Build.props`, `Directory.Packages.props`, `src/Intelektron.INComedor.Domain/`, `src/Intelektron.INComedor.Infrastructure.Sqlite/` (con `Migraciones/0001_transaccion_consumo.sql`), `tests/Intelektron.INComedor.Domain.Tests/` y `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/`.
- Dependencias: `Microsoft.Data.Sqlite` en producción; xUnit v3, su runner y `Microsoft.NET.Test.Sdk` sólo en pruebas. Sin ORM.
- Documentos: `docs/decisiones.md` (D-C25), `readme.md` (líneas 123 y 153), `openspec/config.yaml`, `docs/arquitectura.md` (líneas 3 y 111), `diagrams/despliegue.dot` y su `.svg`, y `AGENTS.md` (comandos vigentes).
- Sin efecto en el OpenAPI: los eventos de outbox siguen `ConsumptionRecorded` y `ConsumptionDenied` con `PayloadVersion` 1.
