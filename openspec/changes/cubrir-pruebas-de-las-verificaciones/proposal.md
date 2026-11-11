## Why

Las verificaciones de tres changes dejaron conductas que el código ya cumple y que ninguna prueba afirma:
`ofrecer-solo-opciones-disponibles` (A, 04/11/2026), `administrar-personas-y-opciones` (C, 06/11/2026) y
`entrar-a-configuracion-con-teclado` (10/11/2026). Una conducta sin prueba puede romperse sin que la CI lo vea.

El 10/11/2026 a las 17:50 David decidió, como decisión 3 del plan `AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`, «Sólo pruebas,
con el gate dado ya»: un change chico que suma sólo las pruebas que faltan de las verificaciones, sin tocar código de
producto ni pruebas existentes. Si una prueba nueva falla porque el código no cumple, sale del change y queda anotada
como hallazgo. Es la tanda 4 del plan.

Origen: épicas HU01 (la atención autónoma: HU01.1 y HU01.3) y HU03 (configurar el puesto sin red); tickets TK02.1 (la
atención) y TK03.1 (la configuración y su acceso).

## What Changes

- **La oferta** (A): la rama de `CatalogoCambiado` que termina sin opciones, y el `OPTIONS_OFFERED` que se registra otra
  vez, en la misma sesión, cuando la oferta se recalcula al cambiar el catálogo.
- **La administración local** (C): el modo de la impresora sigue editable con una versión de INWeb activa; el replay de
  una denegación fuera de horario conserva su causa interna (`ReasonCause`); la baja de una persona y el cambio de precio
  de una opción elegida se ven en la pantalla (hasta ahora sólo se probaban por composición).
- **El acceso** (10/11): el código de una persona al pasar a operativo se rechaza con `DAILY_KEY_REJECTED` y la causa
  `PERSON_CODE`, sin el código y sin cambiar el modo.
- Cada prueba se ve fallar antes, con una afirmación imposible que después se revierte, porque prueba una conducta que
  ya existe.

## Non-goals

- Arreglar conductas: lo que una prueba muestre roto sale del change como hallazgo, sin tocar el código.
- Las guardas de D6 de `entrar-a-configuracion-con-teclado` sobre el modo y la simulación: con el acceso de un operador
  esos botones no existen en la pantalla, así que no se alcanzan desde ella; quedan anotadas como no probables.
- Las demás sugerencias abiertas de esas verificaciones (los nombres con espacios en el borde, los `aria-invalid`
  corridos, la sesión del operador que vence con Configuración abierta): son arreglos de conducta, después de la final.
- Requisitos nuevos o modificados: las conductas ya están en las specs vivas. Por eso el change no tiene delta de specs y lleva `skip_specs: true`
  en su `.openspec.yaml`, como pide `openspec validate --strict` para un change sin requisitos nuevos.

## Capabilities

### New Capabilities

(ninguna)

### Modified Capabilities

(ninguna: las pruebas cubren requisitos vigentes de `atencion-puesto` («Reconfirmación por cambio de catálogo» y «Sólo
las opciones disponibles para la persona»), `administracion-local` («Con INWeb como dueño la edición se bloquea», los
escenarios «Baja de una persona con consumos» y «Cambio de precio con la opción elegida»), `registro-consumo` («Replay
idempotente por IntentId») y `acceso-puesto` («Entrada a Configuración con la clave o un operador»))

## Impact

- **Pruebas**: nuevas en `Kiosk.Tests` (bUnit y la máquina de atención sobre SQLite real) y en
  `Infrastructure.Sqlite.Tests`. Ninguna prueba existente cambia.
- **Código**: ninguno. `git diff --stat origin/final-project-dw -- src/` sale vacío al cerrar cada grupo.
- **Documentos**: la fila que corresponda de `docs/aplicacion-del-curso.md`, si alguna cambia de estado.

## Gate (decisión 3 del plan: dado)

Sólo pruebas, con el gate dado: no toca código de producto ni pruebas existentes; una prueba que falle porque el código
no cumple sale y queda como hallazgo. Talla: S.
