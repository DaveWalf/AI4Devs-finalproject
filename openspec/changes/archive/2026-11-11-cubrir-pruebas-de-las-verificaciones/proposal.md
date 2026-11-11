## Why

Las verificaciones de tres changes dejaron conductas que el código ya cumple y que ninguna prueba afirma:
`ofrecer-solo-opciones-disponibles` (A, 04/11/2026), `administrar-personas-y-opciones` (C, 06/11/2026) y
`entrar-a-configuracion-con-teclado` (10/11/2026). Una conducta sin prueba puede romperse sin que la CI lo vea.

El 10/11/2026 a las 17:50 David decidió, como decisión 3 del plan `AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`, «Sólo pruebas,
con el gate dado ya»: un change chico que suma sólo las pruebas que faltan de las verificaciones, sin tocar código de
producto ni pruebas existentes. Si una prueba nueva falla porque el código no cumple, sale del change y queda anotada
como hallazgo. Es la tanda 4 del plan.

Origen: historias HU01.1 y HU01.3 (épica HU01, la atención autónoma) y HU03.3 (configurar el equipo sin red, con acceso
alternativo de mantenimiento); tickets TK02.1 (la interfaz de la atención), TK03.1 (la transacción de confirmación: el
replay) y la épica técnica TK02 (el acceso y la sesión).

## What Changes

- **La oferta** (A): la rama de `CatalogoCambiado` que termina sin opciones, y el `OPTIONS_OFFERED` que se registra otra
  vez, en la misma sesión, cuando la oferta se recalcula al cambiar el catálogo.
- **La administración local** (C): el modo de la impresora sigue editable con una versión de INWeb activa; el replay de
  una denegación fuera de horario conserva su causa interna (`ReasonCause`); la baja de una persona y el cambio de precio
  de una opción elegida se ven en la pantalla (hasta ahora sólo se probaban por composición).
- **El acceso** (10/11): el código de una persona al pasar a operativo se rechaza con `DAILY_KEY_REJECTED` y la causa
  `PERSON_CODE`, sin el código y sin cambiar el modo; y las guardas de D6 de `entrar-a-configuracion-con-teclado` sobre el
  modo y la simulación, con el acceso de un operador, por los callbacks del panel (sus botones no están en el DOM).
- Cada prueba se ve fallar antes, con una afirmación imposible que después se revierte, porque prueba una conducta que
  ya existe.

## Non-goals

- Arreglar conductas: lo que una prueba muestre roto sale del change como hallazgo, sin tocar el código.
- Las demás sugerencias abiertas de esas verificaciones, entre otras: los nombres con espacios en el borde, los
  `aria-invalid` corridos, la sesión del operador que vence con Configuración abierta, el guardado sin cambios que crea
  una versión sin registro, los paneles que atrapan sólo `SqliteException`, la carga sin rótulo visible, el registro de
  la oferta que no distingue si vino de identificarse o de recalcular y `ServicioAtencion.Catalogo` sin uso. Son
  arreglos de conducta o de código, después de la final.
- **Hallazgos de este change, sin arreglar** (decisión 3):
  - `Elegir` de una opción que no se ofrece, con otra ya elegida, la borra (`MaquinaAtencion.cs:250-264`); la prueba se
    vio fallar («Expected: el id de la elegida · Actual: null») y salió del change.
  - El código de una persona tipeado como la clave de «Pasar a operativo» muestra «El código de un operador no cierra el
    puesto.» (`MensajesAtencion.CodigoNoEsClave`), que no dice lo que pasa: no es un operador y la acción no es cerrar.
    La prueba afirma la constante y el rechazo, no el texto.
- Un escenario de spec para el cruce de «Reconfirmación por cambio de catálogo» con «Sólo las opciones disponibles»
  (la elegida cambió de versión y ya no se ofrece): después de la final, en otro change con su delta.
- Requisitos nuevos o modificados: las conductas ya están en las specs vivas. Por eso el change no tiene delta de specs y lleva `skip_specs: true`
  en su `.openspec.yaml`, como pide `openspec validate --strict` para un change sin requisitos nuevos.

## Capabilities

### New Capabilities

(ninguna)

### Modified Capabilities

(ninguna: las pruebas cubren requisitos vigentes de `atencion-puesto` («Reconfirmación por cambio de catálogo», «Sólo
las opciones disponibles para la persona», «Aviso claro cuando no queda ninguna opción» y «La oferta queda en la
bitácora»), `administracion-local` («Con INWeb como dueño la edición se bloquea», los escenarios «Baja de una persona
con consumos» y «Cambio de precio con la opción elegida»), `registro-consumo` («Replay idempotente por IntentId») y
`acceso-puesto` («Entrada a Configuración con la clave o un operador» y «Acceso de operador a Configuración»))

## Impact

- **Pruebas**: nuevas en `Kiosk.Tests` (bUnit y la máquina de atención sobre SQLite real) y en
  `Infrastructure.Sqlite.Tests`. Ninguna prueba existente cambia.
- **Código**: ninguno. `git diff --stat origin/final-project-dw -- src/` sale vacío al cerrar cada grupo.
- **Documentos**: la fila de TDD de `docs/aplicacion-del-curso.md`, con la evidencia de este change.

## Gate (decisión 3 del plan: dado)

Sólo pruebas, con el gate dado: no toca código de producto ni pruebas existentes; una prueba que falle porque el código
no cumple sale y queda como hallazgo. Talla: S.
