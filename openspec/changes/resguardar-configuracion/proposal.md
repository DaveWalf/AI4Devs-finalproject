# Proposal

## Why

El 10/11/2026 David pidió (mejora 8a): «General al final, con resguardo y recuperación de toda la configuración entre
puestos y versiones, y el resumen de qué se recupera». Decidió que todo entra en la final, con el gate dado (decisiones
10, 12 y 13 del plan `AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`). Es el change 4 del §6b. El 1a dejó General al final.

Hoy la configuración de un puesto vive en su base (la versión activa: personas, códigos, cupos, opciones, franjas y
permisos) y en archivos de su carpeta de datos (el estilo visual, los logos, los textos del comprobante y la presentación
de cada opción). No hay forma de llevarla a otro puesto, ni de volver a la de ayer después de un cambio equivocado.

Origen: la «configuración por snapshot» de la segunda ola (`AGENTS.md`), que esta mejora trae al primer corte en su parte
local; RB21 (todo cambio con actor y resultado) y RB25 (el código no se reasigna); ticket TK02.1.

## What Changes

- **Guardar resguardo**, en General: un `.zip` en la carpeta de la exportación con la versión activa de la configuración
  (todas sus tablas), el estilo visual, los logos, los textos del comprobante y la presentación de las opciones, más un
  manifiesto con la versión del formato, la del esquema y la huella SHA-256 de cada parte.
- **Recuperar resguardo**: se elige el `.zip`, el puesto lo valida entero (formato, esquema, huellas, nombres de las
  partes, tamaños y que ningún código choque con el de otra persona de este puesto) y muestra el resumen de qué se
  recupera (cuántas personas, opciones y permisos, cuáles de los ajustes locales) con «Recuperar» y «Cancelar».
- **Recuperar** crea una versión nueva de la configuración con el contenido del resguardo y la activa, y reemplaza los
  archivos locales que el resguardo trae; todo o nada. La versión anterior queda en la base, como después de cualquier
  guardado.
- **Bitácora**: `CONFIGURATION_BACKUP_CREATED` con el archivo y su huella; `CONFIGURATION_RESTORED` con la huella, la
  versión de origen, la nueva y qué se recuperó; `CONFIGURATION_RESTORE_REJECTED` con el motivo. Ninguno lleva códigos ni
  nombres.

## Non-goals

- Lo que es de cada equipo o de su estado: el monitor elegido, el modo operativo o de mantenimiento, la simulación, la
  carpeta de la exportación, los certificados (son secretos) y los consumos.
- Recuperar con INWeb como dueño de los datos: la configuración la trae INWeb.
- Un resguardo automático o programado.
- Recuperar un resguardo de otra versión del esquema de la base.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `configuracion-puesto`: guardar y recuperar el resguardo desde General.

## Impact

- **Código**: `Infrastructure.Sqlite` (leer la versión activa entera y crear una versión desde un resguardo, con las
  validaciones de los códigos) y `Kiosk` (el `.zip` con los archivos locales, los botones de General, el resumen y los
  registros). Sin migración.
- **Pruebas**: nuevas en `Infrastructure.Sqlite.Tests`, `Kiosk.Tests` y una E2E a 1024 × 768. Pruebas existentes que
  pueden cambiar (decisión 13): `PuntoFinalTests`, si se suman métodos a `MensajesAtencion`.
- **Documentos**: `readme.md` (General) y la fila que corresponda de `docs/aplicacion-del-curso.md`.

## Gate (decisión 13 del plan: dado, con límites)

Opciones recomendadas, aplicadas sin esperar:

1. «Toda la configuración» es la versión activa de la base y los ajustes locales que la acompañan (estilo, logos, textos
   del comprobante, presentación de las opciones); no lo que es del equipo o de su estado (monitor, modo, simulación,
   carpeta de la exportación) ni los certificados.
2. El resguardo lleva los códigos de las personas, porque sin ellos no se puede recuperar; el resumen y la bitácora no los
   muestran, y el readme dice que el archivo se guarda como un dato de la empresa.
3. Recuperar crea una versión nueva, como cualquier guardado: la anterior queda y la atención usa la nueva desde el próximo
   toque.
4. «Entre versiones» es entre versiones de la configuración y entre puestos de la misma versión del esquema; un resguardo
   de otro esquema se rechaza con su motivo.
5. Un código del resguardo que en este puesto es de otra persona frena la recuperación (RB25): no se reasigna.
6. Con INWeb como dueño, «Recuperar resguardo» no se ofrece; «Guardar resguardo», sí.

Talla: XL. Si una prueba existente falla por otra razón, se para (cláusula del plan).
