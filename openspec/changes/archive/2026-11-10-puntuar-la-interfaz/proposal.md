# Proposal

## Why

El 10/11/2026, durante `buscar-y-exportar-consumos`, David pidió: «Asegurate de no dejar oraciones sin el punto final
donde corresponda en la interfaz.» En ese momento los 55 mensajes de `MensajesAtencion` terminaban sin punto, como los
textos sueltos de las pantallas, sin que ninguna regla lo pidiera; en la punta de este change, después de
`buscar-y-exportar-consumos`, son 52 de sus 59 constantes. La regla ya está en `openspec/config.yaml` (rules de `design` y
`tasks`) y los textos nuevos de aquel change la cumplen; éste puntúa el resto. Es el change 1c del §6b del plan
`AI4DEVS-GOAL-CIERRE-FINAL-11-11.md` (decisión 15), después del 1b y antes del 2.

Origen: el pedido de David del 10/11/2026 y la presentación de la atención (HU01.1, HU02.1 y HU03.1, cuyos mensajes son
los que se puntúan); ticket TK01.1.

## What Changes

- **El punto final** en cada oración que la pantalla muestra: los avisos de la atención, los motivos de un rechazo, los
  mensajes de Configuración, de la red, de la asistencia y de las pruebas de impresión y de conexión.
- **Las oraciones sueltas pasan a `MensajesAtencion`**: las de `PantallaAtencion.razor`, `PanelConfiguracion.razor`,
  `PanelPersonas.razor` y `PantallaDemostracion.razor`, los resultados de `ResultadoRed` y los de `SesionOperador`, para
  que una sola prueba las vea.
- **Una prueba que recorre los mensajes** y exige el punto en cada oración, con una lista explícita de lo que no lo lleva:
  títulos, rótulos, botones, opciones y estados.

## Non-goals

- Cambiar la redacción de un mensaje, más allá del punto final.
- Los textos impresos del comprobante, que no son la pantalla, y las bitácoras, que no son texto para una persona.
- Las specs y el `readme.md`: citan los mensajes entre comillas latinas, y el punto de una oración citada va fuera de las
  comillas, como pide la RAE.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `presentacion-puesto`: requisito nuevo, «Las oraciones de la pantalla terminan en punto».

## Impact

- **Código**: `Kiosk` (`MensajesAtencion.cs` con el punto y las oraciones que se mudan; los `.razor` y las clases que
  las usaban, `SesionOperador.cs` y `Red/ResultadoRed.cs`). Sin cambios en la base, el receptor ni el contrato.
- **Pruebas**: `PuntoFinalTests` nueva en `Kiosk.Tests`. Las pruebas existentes que comparan un mensaje con un literal,
  en lugar de su constante, cambian sólo para sumar el punto (decisión 13 del plan); la lista va en la tarea y en el PR.
- **Documentos**: `prompts.md`, el criterio humano 16, dice que el resto ya se puntuó, y la fila de las reglas de
  `openspec/config.yaml` en `docs/aplicacion-del-curso.md` nombra la regla y su prueba.

## Gate (decisión 13 del plan: dado, con límites)

Opciones recomendadas, aplicadas sin esperar:

1. Llevan punto las oraciones; no lo llevan el título del reposo, los estados («Sin conexión de red», «Bitácora general:
   al día», «Hoy ya usaste: …», «Se da de baja al guardar»), los motivos que son botones, la leyenda «Identificación
   simulada por código (demostración)» y el detalle de cada etapa de la prueba de conexión, que va después de su nombre.
2. Las oraciones sueltas se mudan a `MensajesAtencion`, en lugar de dejar los literales donde están.
3. Una prueba por reflexión sobre `MensajesAtencion`, en lugar de un analizador de código.

Talla: S. Si una prueba existente falla por otra razón que el punto, se para (cláusula del plan).
