# Proposal

## Why

La entrega 2 del 22/10 pide los scaffolds de front, back y base de datos conectados y el flujo principal «casi completo» (L58:112-130); «no se espera que todo el proyecto esté terminado» (L56:23). Después de `registrar-consumo-atomico`, la base y la confirmación atómica (RB06) existen y están probadas, pero nadie las alcanza desde una pantalla y no hay receptor. Este change es el paso 4 del plan aprobado (§6 del documento de prácticas): una página Razor mínima y el esqueleto del receptor, conectados al caso de uso.

Origen: **HU01.1** (confirmar un servicio sin red y sin duplicarlo) por el lado del puesto, como primer andamio de **TK02.1** (interfaz); y **HU02.2** (sincronización idempotente de lotes) por el lado central, como primer andamio de **TK01.1** (receptor). No cierra ningún AT propio de esos tickets: los cierran `atender-seleccion-y-confirmacion` y `recibir-lotes-idempotente`.

## What Changes

- **Puesto con pantalla Razor (`Intelektron.INComedor.Kiosk`).** Ventana host WPF con un `BlazorWebView` (ADR01) y una sola pantalla de demostración, con persona y opción sintéticas fijas y un botón «Confirmar». Cada toque abre una sesión de prueba, genera un `IntentId` nuevo, confirma con la unidad de trabajo de RB06 sobre la base SQLite local y muestra la decisión y sus identificadores. Con el límite 1 de la persona, el segundo toque del mismo día muestra el rechazo por cupo.
- **Modo demostración con datos sintéticos.** La primera vez que el puesto abre una base vacía, la crea y carga un conjunto fijo de datos sintéticos: un puesto, una persona habilitada, ALMUERZO con una categoría y un ámbito, una opción y una regla. Una base que ya tiene datos no se vuelve a sembrar.
- **Contrato de eventos compartido (`Intelektron.INComedor.Contracts`).** Los records de los eventos de outbox pasan de `Infrastructure.Sqlite` a este proyecto, sin cambiar un byte de su JSON. D9 de `registrar-consumo-atomico` lo preveía para cuando el receptor los compartiera.
- **Esqueleto del receptor (`Intelektron.INComedor.Sync.Api`).** Proyecto ASP.NET Core que declara las dos operaciones de TK01.1, `POST /api/incomedor/v1/sync-batches` y `GET /api/incomedor/v1/sync-batches/{batchId}`. Ambas responden `501` con `application/problem+json`, sin leer el cuerpo ni persistir nada. Referencia `Contracts`.
- **Pruebas** de los tres proyectos nuevos, todas vistas en rojo antes de implementar: `Kiosk.Tests` (siembra, confirmación y componente Razor) y `Sync.Api.Tests` (las dos rutas en 501).

## Non-goals

- **Atención completa (TK02.1, `atender-seleccion-y-confirmacion`)**: identificación simulada por código, catálogo de 15 opciones, estados de reposo, confirmando y resultado, protección del doble toque en pantalla, timeout de 30 s, reconfirmación por cambio de catálogo y mensajes del catálogo de estados. La pantalla de este change es de demostración y no se toma como cumplimiento de AT16, AT26 ni AT47.
- **Comprobante e impresión (HU03.1)**: la confirmación deja el trabajo de impresión que ya crea RB06, y nadie lo procesa.
- **Ingesta idempotente (TK01.1, `recibir-lotes-idempotente`)**: autenticación por certificado, `Idempotency-Key`, hash RFC 8785, recibo durable, resultado por `EventId`, conciliación, SQL Server de prueba y cliente sincronizador. El esqueleto no acusa ningún lote.
- **RB02 completa (`evaluar-habilitacion-local`)**: la persona sintética tiene una sola regla vigente por clave, como en el change anterior.
- La capa `Application`, configuración por snapshot, administración, multipuesto, biometría y las demás piezas de la segunda ola (D-C24).
- Pruebas E2E sobre WebView2 con Playwright: son el spike y el E2E de la entrega final (§3.5).
- Cambiar el modelo de datos o la migración `0001`.

## Capabilities

### New Capabilities

- `modo-demostracion`: arranque del puesto sobre una base vacía con un conjunto fijo de datos sintéticos, y una pantalla que confirma intenciones contra la base local con la confirmación atómica, para mostrar el flujo sin datos reales.
- `sincronizacion-lotes`: comportamiento del receptor central de lotes. En este change, un solo requirement: ninguna respuesta del receptor acusa un lote que no procesó de forma durable.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacía. `registro-consumo` sigue en el change anterior, sin archivar, y este change no cambia sus requirements; el JSON de los eventos queda igual.

## Impact

- **Proyectos nuevos:**
  - `src/Intelektron.INComedor.Kiosk/` (`net11.0-windows10.0.17763.0`, WPF y Razor);
  - `src/Intelektron.INComedor.Contracts/`;
  - `src/Intelektron.INComedor.Sync.Api/` (`Sdk.Web`);
  - `tests/Intelektron.INComedor.Kiosk.Tests/`;
  - `tests/Intelektron.INComedor.Sync.Api.Tests/`.
  Los cinco se agregan a `INComedor.sln`.
- **Código existente:**
  - `EventosSalida.cs` se mueve a `Contracts`, con sus records públicos;
  - `Infrastructure.Sqlite` pasa a referenciar `Contracts`;
  - `UnidadConfirmacion` no cambia su lógica.
  - Ningún test existente se modifica.
- **Dependencias** en `Directory.Packages.props`:
  - `Microsoft.AspNetCore.Components.WebView.Wpf` `11.0.0-rc.2.26505.5`;
  - `Microsoft.AspNetCore.Mvc.Testing` `11.0.0-rc.2.26504.105`, sólo en pruebas;
  - `bunit` `2.12.5`, sólo en pruebas.
- **Entrada externa:** el receptor expone dos rutas HTTP. No lee el cuerpo, no refleja la entrada y no persiste nada; la autenticación llega con `recibir-lotes-idempotente`. Se revisa contra OWASP Top 10 en la tarea correspondiente.
- **Documentos:** `AGENTS.md` (comando para correr el puesto y el receptor) y `readme.md` (cómo ver la demostración).
- **CI:** sin cambios en `ci.yml`. `windows-latest` compila WPF, y los proyectos nuevos entran por la solución.
- **OpenAPI:** sin cambios. El cuerpo de error es el `ProblemDetails` que el contrato ya define. El código `501` no figura entre las respuestas declaradas de esas operaciones: es el estado transitorio del esqueleto y desaparece con `recibir-lotes-idempotente` (ver design).
