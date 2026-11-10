# Proposal

## Why

El 10/11/2026, después de probar el puesto publicado, David pidió dos mejoras de su lista de ese día, que el arnés `AI4DEVS-PROGRESS.md` guarda entera:

- **Mejora 3**: «Cuando toco el menú configuración, por ahora me debe pedir la clave del día aceptando cualquier cosa, hasta tanto tenga esa funcionalidad implementada: para poder tipear en pantalla, debe presentar un miniteclado tipo calculadora con la disposición del teclado extendido, y el botón ENTER. Luego, al dar ENTER, aclarar que todavía se encuentra sin validar ese número e ingresar.»
- **Mejora 13**: «Si el que está logueado es un usuario operador, al tocar el botón de configuración, sólo debe habilitarle las secciones de Consumos, Personas e Impresora por ahora, sin permitirle alterar ninguna otra configuración significativa ni cerrar al programa.»

A las 10:36 decidió que la 3 entra en la final, y a las 10:50, que la 13 entra dentro de ella. La recomendación era dejar la 13 para después: los permisos de operador están en la segunda ola del `AGENTS.md`, y mientras la clave acepte cualquier número, tipear otro número da acceso completo.

Hoy, en mantenimiento, el engranaje abre Configuración directo; en operativo pide la clave en un campo con «Aceptar», que necesita el teclado táctil de Windows. Nadie inicia sesión en Configuración: los operadores (`9001` a `9004`) sólo tipean su código en Comprobantes.

Origen: historia HU02.2 (operación por el operador, con el permiso `OPERATOR` de RB26) y la entrada a Configuración de `configurar-puesto-por-pestanas`; tickets TK02 (la pantalla del puesto) y TK03 (los permisos locales).

## What Changes

- **La clave también en mantenimiento**: el engranaje pide la clave del día en los dos modos. Sigue aceptando cualquier número de 1 a 12 dígitos, y al entrar, Configuración avisa que la clave no se validó.
- **El teclado en pantalla**: el diálogo de la clave tiene un teclado numérico con la disposición del teclado extendido, con «ENTER» en lugar de «Aceptar», «Borrar» y «Limpiar». El teclado físico sigue tipeando, y el campo no abre el teclado táctil de Windows.
- **El operador entra con su código**: en el mismo diálogo, el código de un operador con `OPERATOR` vigente abre Configuración sólo con Consumos, Personas e Impresora, en Consumos. No ve General, así que no cambia el modo, no apaga la simulación y no cierra el puesto. Un código de una persona nunca vale como clave del día, ni para cerrar ni para pasar a operativo.
- **Lo que queda registrado**: la entrada de un operador va a la bitácora general con `Access = "OPERATOR"` y su persona como actor, y lo que guarde en Personas o Impresora, también, sin su código. Entrar en mantenimiento queda con `DAILY_KEY`.

## Non-goals

- La validación real de la clave del día, que llega con la tecnología de INWeb detrás de `IValidadorClaveDelDia`, y la cuenta individual de mantenimiento: siguen en la segunda ola.
- Otros perfiles u otras pestañas para el operador. Comprobantes sigue pidiendo su código, como hoy, a quien entra con la clave.
- Un plazo de inactividad propio del operador: su acceso dura lo que Configuración abierta.
- El teclado en pantalla para los demás campos de Configuración, que es la mejora 8b, después de la final.
- La bitácora persona por persona de las raciones, que es la mejora 5, después de la final.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `acceso-puesto`:
  - «Clave del día de desarrollo» suma el teclado en pantalla, el teclado físico y el aviso de clave sin validar.
  - «Entrada a Configuración según el modo» se retira y la reemplaza «Entrada a Configuración con la clave o un operador», con la clave también en mantenimiento.
  - «Cada acceso queda en la bitácora general» suma el acceso `OPERATOR` con su actor.
  - Un requisito nuevo, el acceso de operador a Configuración.
- `configuracion-puesto`: «Configuración por pestañas» muestra tres pestañas al operador.
- `consulta-consumos`: la consulta es de quien entra con la clave o con un operador, con su actor; el requisito de antes se retira porque nombraba la entrada sin código en mantenimiento.

## Impact

- **Código**:
  - `Infrastructure.Sqlite`: saber si un valor es un código del snapshot activo sin registrar nada, y un actor opcional en lo que `AdministracionLocal` registra al guardar. Las firmas de hoy siguen valiendo, así que ninguna prueba de esa capa cambia.
  - `Kiosk`: el teclado de `DialogoClaveDelDia.razor`, la entrada por la clave o el operador en `PantallaAtencion.razor`, y las pestañas según el acceso en `PanelConfiguracion.razor`.
  - `Domain` no cambia. Sin migración: la versión de esquema sigue en 4.
- **Pruebas**: nuevas en `Kiosk.Tests`, `Infrastructure.Sqlite.Tests` y `E2E.Tests`. **Cambian las pruebas existentes que entran a Configuración por el engranaje en mantenimiento, con tu aprobación** (punto 8 del gate).
- **Documentos**: `AGENTS.md` y `openspec/config.yaml` (alcance y la clave en los dos modos), `readme.md` (el recorrido y los códigos de operador) y `docs/aplicacion-del-curso.md`.
- Sin dependencias nuevas, sin cambios en la CI ni en el receptor.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David | Sin talla propia (gate del 10/11 a las 12:08) | | |
| La IA (Claude Code, Opus 5.5), 10/11/2026 | **M** | 5 a 6 h | 6,5 a 7,8 h de capacidad |

**Por qué la IA da M y no S.** Son 3 requisitos modificados, 3 nuevos y 2 retirados, con 13 escenarios nuevos o cambiados, en tres capas. Lo que más pesa no es el código sino las pruebas existentes que suman la clave: 28 archivos nombran el engranaje y unos 23 entran por él en mantenimiento. Lo que no lo lleva a L es que la sesión del operador ya existe (`Asistencia.Entrar`, RB26) y el diálogo de la clave también.

## Para el gate de David

Cada punto lleva una propuesta y su alternativa; lo que decidas queda escrito acá antes del apply.

1. **La clave también en mantenimiento** (D1). Es tu pedido, y reemplaza tu decisión del 03/11 («entrar en el modo configuración directamente»). Propuesta: en los dos modos, y cerrar con Escape en mantenimiento sigue directo, porque tu pedido nombra sólo el menú de configuración. Alternativa: también pedirla para cerrar en mantenimiento.
2. **Qué acepta** (D2). Propuesta: cualquier número de 1 a 12 dígitos, como hoy. «ENTER» con el campo vacío avisa que la clave no es válida, para que un toque sin querer no abra Configuración. Alternativa: aceptar también el campo vacío, que es lo más literal de «cualquier cosa».
3. **El aviso de clave sin validar** (D4). Propuesta: arriba de Configuración, «Entraste con una clave del día sin validar: esta versión acepta cualquier número», hasta cerrarla. Alternativa: un cartel de 2 s antes de entrar.
4. **El teclado** (D3). Propuesta, la disposición del teclado numérico extendido: el «+» pasa a «Limpiar», que borra todo; el «.» (Supr), a «Borrar», que borra el último dígito; y «Bloq Num / * -» no van:
   ```
   ┌─────┬─────┬─────┬─────────┐
   │  7  │  8  │  9  │         │
   ├─────┼─────┼─────┤ Limpiar │
   │  4  │  5  │  6  │         │
   ├─────┼─────┼─────┼─────────┤
   │  1  │  2  │  3  │         │
   ├─────┴─────┼─────┤  ENTER  │
   │     0     │  ⌫  │         │
   └───────────┴─────┴─────────┘
               [ Cancelar ]
   ```
   Cada tecla mide al menos 56 px, y el campo muestra un punto por dígito. Alternativa: el orden de un teléfono (1 2 3 arriba), que no es el del teclado extendido que pediste.
5. **Cómo entra el operador** (D5). Propuesta: en el mismo diálogo, sin elegir antes. Si lo tipeado es un código del snapshot activo, se trata como el de un operador, con los rechazos de RB25 y RB26 (`9002` vencido, `9003` revocado, `9004` sin `OPERATOR`, `0001` persona). Si no es un código, es la clave del día. Alternativa: dos botones en el diálogo, «Clave del día» y «Soy operador». Es más explícito, pero suma un toque y una pantalla más.
6. **Qué ve el operador** (D6). Propuesta: Consumos, Personas e Impresora, en ese orden y abriendo en Consumos. No ve General, Opciones, Comprobantes, Identidad visual, Pantalla ni Red. En el diálogo de cerrar o de pasar a operativo, su código se rechaza con «El código de un operador no cierra el puesto» y queda en la bitácora. Alternativa: también Comprobantes, que hoy pide el código de un operador.
7. **El actor en la bitácora** (D7). Propuesta: la entrada, las consultas de Consumos y lo que guarde en Personas e Impresora llevan su persona como actor, sin su código. Adelanta una parte de tu mejora 5 («qué operador hizo qué cambios») porque, sabiendo quién es, registrar un actor nulo sería falso. Alternativa: el actor nulo de hoy hasta la mejora 5.
8. **Pruebas existentes que cambian, con tu aprobación** (D8). Las que entran a Configuración tocando el engranaje en mantenimiento, unas 23 de componentes y 5 E2E, de 28 archivos que lo nombran; las de `simular-identificacion-por-lista` y `confirmar-eleccion-en-cartel` incluidas. El cambio de cada una se limita a dos cosas, y ninguna otra:
   - el toque del engranaje pasa a un ayudante nuevo, en un archivo nuevo, que además tipea una clave y toca «ENTER»;
   - donde se mide cómo se entró, `MAINTENANCE_MODE` pasa a `DAILY_KEY`.
   Si una prueba existente falla por otra razón, se para y se te pregunta. El diff de `tests/` va en la evidencia para que lo revises entero.
9. **`AGENTS.md` y `openspec/config.yaml`**. En el alcance: la clave del día de desarrollo se pide en los dos modos, y el acceso del operador a Consumos, Personas e Impresora sale de la segunda ola por este change. Siguen en ella la cuenta individual de mantenimiento, los demás permisos de operador y la clave real de INWeb.
10. **Calendario y recorte.** Se aplica después del 2, apilado sobre él, en 5 a 6 h. Tu prueba en el ejecutable va una sola vez, con los tres cambios. Recorte propuesto: si el 11/11 a las 08:00 este change no está archivado y en verde, queda fuera de la final, y la entrega queda con el 1 y el 2.
11. **Rama.** `feature/entrar-a-configuracion-con-teclado-dw`, desde `feature/confirmar-eleccion-en-cartel-dw` en `8a57ef0` y sin seguimiento remoto. Su PR apunta a esa rama y se integra después de ella.
12. **Tallas.** La de la IA es M; la tuya la das acá.

### Decisión del gate

El 10/11/2026, a las 12:08, David respondió «Todo aprobado.» al gate de las tres propuestas del día, con las recomendadas y sin talla propia. Queda así:

- Punto 1 con la propuesta: la clave en los dos modos, y cerrar con Escape en mantenimiento sigue directo.
- Punto 2 con la propuesta: de 1 a 12 dígitos, y «ENTER» con el campo vacío rechaza.
- Punto 3 con la propuesta: el aviso de clave sin validar arriba de Configuración, hasta cerrarla.
- Punto 4 con la propuesta: el teclado numérico extendido del dibujo.
- Punto 5 con la propuesta: el operador entra en el mismo diálogo, sin elegir antes.
- Punto 6 con la propuesta: el operador ve Consumos, Personas e Impresora, abriendo en Consumos.
- Punto 7 con la propuesta: su persona como actor, sin su código.
- Punto 8 como está: las pruebas existentes cambian sólo como dice D8. Si una falla por otra razón, se para.
- Punto 9 con la propuesta: el alcance de D9 en `AGENTS.md` y `openspec/config.yaml`.
- Punto 10 con la propuesta: el recorte del 11/11 a las 08:00.
- Punto 11 como está, con la base nueva: la rama se rebasó sobre la punta de `confirmar-eleccion-en-cartel`, `cf1b137`, en lugar de `8a57ef0`. Esa punta es la base del `git diff` de pruebas del encabezado de `tasks.md`.
- Punto 12 sin talla propia, así que la tabla de tallas queda con la de la IA.

### Decisión durante el apply: dos pruebas fuera de D8

El 10/11/2026, a las 14:24, el apply paró en la tarea 2.2: `ClaveDelDiaTests.Cualquier_numero_en_desarrollo` afirma `inputmode="numeric"`, y D3 lo cambia a `"none"`; esa prueba entra en operativo y no mide el acceso, así que D8 no la cubre. Se le preguntó a David por AskUserQuestion (sin piloto: la respuesta es suya), y eligió «Aprobar las dos», la recomendada:

- en `ClaveDelDiaTests.Cualquier_numero_en_desarrollo`, la línea del `inputmode` espera `"none"`, y nada más;
- `AccesoPuestoTests.Mantenimiento_entra_directo`, que con D8 pasaría pero seguiría titulándose «entra directo … sin clave», pasa a llamarse `Mantenimiento_tambien_pide_la_clave`, con el `DisplayName` del escenario «Mantenimiento también pide la clave», además de su cambio de D8.
