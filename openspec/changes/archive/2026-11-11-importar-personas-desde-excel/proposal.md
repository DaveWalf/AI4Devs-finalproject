# Proposal

## Why

El 10/11/2026 David pidió (mejora 14): «en Personas, importar un Excel con un listado nuevo que, según se configure,
reemplace lo actual o modifique y agregue; en ese caso, aclarar si los consumos habilitados afectan los ya registrados por
esa persona en la misma jornada; bitácora de lo decidido y del resumen persona por persona; y descargar un Excel de
ejemplo claro y agradable de completar». Ese día decidió que todo entra en la final, con el gate dado (decisiones 10, 12 y
13 del plan `AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`). Es el change 2b del §6b; el 2a dejó la tabla de Personas con el x/n,
el teclado y la exportación.

Hoy las personas se cargan de a una, en la tabla. No hay forma de traer un listado ni una planilla de ejemplo.

Origen: historia HU02.2 (cupos y habilitación local), RB25 (el código no se reasigna), RB03 (el cupo por persona y fecha de
negocio) y RB21 (todo cambio con actor y resultado); ticket TK02.1.

## What Changes

- **Descargar la planilla de ejemplo**: un `.xlsx` con una hoja «Personas» (Nombre, Código y el límite de cada cupo del
  catálogo, con dos filas de ejemplo sintéticas y la columna del código como texto, para que Excel no le saque los ceros)
  y una hoja «Instrucciones» que explica cada columna y los dos modos.
- **Importar un Excel** en Personas: se elige el archivo, el puesto lo lee y muestra un resumen antes de aplicar nada
  (cuántas personas se agregan, cuántas cambian, cuántas se dan de baja y las filas con errores), con tres botones:
  «Modificar y agregar», «Reemplazar» y «Cancelar».
- **Modificar y agregar**: una fila con el código de una persona activa la actualiza (nombre y cupos); una con un código
  nuevo la agrega; las demás personas quedan como estaban.
- **Reemplazar**: lo mismo, y además da de baja a las personas activas que no están en el archivo, salvo a quien tenga un
  permiso de operador o de administrador.
- **Los consumos de hoy siguen contando**: el cupo es diario por persona (RB03); importar no borra ni descuenta lo usado,
  así que una persona con un almuerzo hoy y límite nuevo 1 no puede almorzar otra vez hoy. El resumen lo dice.
- **Todo o nada**: una fila con un error (sin código, un código repetido en el archivo o de una persona dada de baja, un
  nombre inválido, un límite fuera de 0 a 99, una columna desconocida) deja la importación sin aplicar, con la fila y el
  motivo.
- **Bitácora**: una sola versión nueva con un `CONFIGURATION_CHANGED` por persona, como al guardar a mano, y un
  `PEOPLE_IMPORTED` con el modo, la huella SHA-256 del archivo (no su nombre, que lo escribe cualquiera), los totales y el
  resumen persona por persona (agregada, modificada, dada de baja, sin cambios), sin nombres ni códigos.

## Non-goals

- Importar opciones o franjas: es el change 3.
- Reactivar a una persona dada de baja o cambiarle el código: siguen en la segunda ola.
- Importar CSV u otros formatos de planilla.
- Importar mientras INWeb administra la configuración.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `administracion-local`: requisitos nuevos para la planilla de ejemplo y para importar Personas desde Excel.

## Impact

- **Código**: `Infrastructure.Sqlite` (`LectorPlanilla`, que lee la primera hoja de un `.xlsx` con cadenas compartidas o en
  línea y números; `ImportacionPersonas`, que arma el resumen y las ediciones y guarda con `GuardarPersonas`;
  `EscritorPlanilla` con varias hojas y columnas de texto) y `Kiosk` (`IElectorArchivo` con el `OpenFileDialog` de WPF, el
  resumen y los botones en Personas). Sin migración.
- **Pruebas**: nuevas en `Infrastructure.Sqlite.Tests`, `Kiosk.Tests` y una E2E a 1024 × 768. Prueba existente que
  cambia (decisión 13): `PuntoFinalTests`, que obliga a clasificar cada método nuevo de `MensajesAtencion` con un ejemplo;
  suma los de la importación, sin tocar ninguna aserción.
- **Documentos**: `readme.md` (Personas) y la fila que corresponda de `docs/aplicacion-del-curso.md`.

## Gate (decisión 13 del plan: dado, con límites)

Opciones recomendadas, aplicadas sin esperar:

1. Una persona del archivo se reconoce por su código, que es único y no se reasigna (RB25), no por su nombre.
2. El modo se elige al importar, en el resumen, y no queda guardado: cada importación decide.
3. «Reemplazar» no da de baja a quien tiene un permiso de operador o de administrador, para que una lista de comensales no
   deje el puesto sin operadores.
4. Los consumos ya registrados hoy cuentan contra los cupos nuevos (RB03), como al bajar un límite a mano.
5. Una fila con error frena toda la importación, como un guardado a mano con un campo inválido.
6. Una columna de cupo que el archivo no trae deja ese cupo como estaba; una celda vacía en una columna que sí trae lo
   quita, como vaciar la celda en la tabla.
7. (Agregada al aplicar.) Una credencial sin revocar de una persona activa la reconoce aunque esté vencida o todavía no
   rija, y un permiso de operador vencido pero sin revocar la conserva al «Reemplazar»: en los dos casos, lo que no está
   revocado sigue siendo de esa persona.
8. (Agregada al aplicar.) Un operador que entró con su código puede importar con los dos modos, como ya puede dar de baja a
   mano desde la tabla; queda como actor de cada cambio.
9. (Agregada por la verificación.) La bitácora guarda la huella SHA-256 del archivo y no su nombre; el resumen avisa si
   quedan filas de la planilla de ejemplo y si hay códigos guardados como número, y «Reemplazar» dice que la baja revoca
   los códigos sin vuelta atrás.

Talla: L. Si una prueba existente falla por otra razón, se para (cláusula del plan).
