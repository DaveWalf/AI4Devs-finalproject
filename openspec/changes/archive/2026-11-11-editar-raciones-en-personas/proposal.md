# Proposal

## Why

El 10/11/2026 David pidió, entre sus mejoras para Configuración (mejora 5): «Personas con raciones por tipo en x/n,
editables al tocar, con bitácora de qué operador cambió qué a quién y cuándo, teclado en pantalla que se muestra u
oculta, y exportación». Ese día decidió que todo entra en la final, con el gate dado (decisiones 10, 12 y 13 del plan
`AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`). El change 2 del §6b se parte en dos, como el 1, para que cada PR sea revisable:
éste es el 2a; el 2b importa personas desde Excel (mejora 14).

Hoy la tabla de Personas muestra sólo el límite de cada cupo, sin lo usado en el día, y se edita con «Editar», que abre
toda la tabla. No hay teclado en pantalla para el nombre, el código o el límite, y Personas no se exporta. La bitácora ya
registra cada guardado con el operador que entró con su código como actor (`entrar-a-configuracion-con-teclado`), pero
sólo con el antes y el después enteros de la persona.

Origen: historia HU02.2 (cupos y habilitación local), RB03 (el cupo por persona y fecha de negocio), RB21 (todo cambio con
actor y resultado) y AT41 (reconstruir sin consultar la base); ticket TK02.1.

## What Changes

- **Raciones en x/n**: cada cupo de la tabla dice lo usado hoy sobre su límite («1/2»), con «—» sin cupo.
- **Editables al tocar**: tocar la celda de un cupo abre la edición de la tabla con ese campo elegido, como «Editar».
- **La bitácora dice qué cupo cambió**: cada `CONFIGURATION_CHANGED` de una persona suma la lista de cupos cambiados, con
  su límite antes y después, además del antes y el después enteros, el actor y la hora que ya tiene.
- **Teclado en pantalla**: al editar Personas, un teclado con letras, números y borrar escribe en el campo elegido; «Ocultar
  teclado» lo esconde y «Mostrar teclado» lo vuelve a mostrar, hasta cerrar Configuración.
- **Exportar Personas** a CSV y a Excel, en la carpeta de la exportación de Consumos, sin ningún código; cada exportación
  queda en la general como `PEOPLE_EXPORTED`.

## Non-goals

- Importar personas desde Excel: es el change 2b.
- Ayudas de pantalla para todo el puesto, desactivables por sesión: es el change 9.
- Cambiar las reglas del cupo, la validación del nombre o del código, o la versión que crea cada guardado.
- Un visor de la bitácora en la pantalla.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `administracion-local`: «Personas habilitadas con sus opciones y cupos» pasa al x/n y a la edición al tocar; «Cada
  guardado queda en la bitácora general» suma los cupos cambiados y dice el actor como `acceso-puesto`; requisitos nuevos
  para el teclado en pantalla y la exportación de Personas.

## Impact

- **Código**: `Infrastructure.Sqlite` (`AdministracionLocal`: lo usado hoy por persona y categoría y los cupos cambiados
  en el registro; la escritura de planillas, compartida con la exportación de Consumos) y `Kiosk` (`PanelPersonas.razor`
  con el x/n, el toque, el teclado y la exportación; `TecladoEnPantalla.razor`, nuevo). Sin migración.
- **Pruebas**: nuevas en `Infrastructure.Sqlite.Tests` y `Kiosk.Tests`, y una E2E a 1024 × 768 con medidas. Las existentes
  que leen el límite como texto de la celda cambian para leer el x/n (decisión 13), listadas en la tarea y en el PR.
- **Documentos**: `readme.md` (Personas) y la fila que corresponda de `docs/aplicacion-del-curso.md`.

## Gate (decisión 13 del plan: dado, con límites)

Opciones recomendadas, aplicadas sin esperar:

1. x es lo usado hoy en esa categoría por la persona, de la misma tabla que cuenta el cupo en la atención
   (`tbComedorUsosCupo` en la fecha de negocio actual); n es el límite.
2. Tocar una celda abre la edición de toda la tabla, como «Editar», con ese campo elegido, y no una edición de una sola
   celda: guardar sigue siendo una versión nueva con su validación.
3. El teclado se muestra al editar, y «Ocultar teclado» lo esconde hasta cerrar Configuración; letras con la Ñ, números,
   espacio y borrar, en filas de 56 px.
4. La exportación usa la carpeta ya elegida para Consumos; las columnas son la persona, el estado, lo usado hoy y el
   límite de cada cupo, y las opciones habilitadas.
5. La bitácora suma `QuotaChanges` (categoría, servicio, límite antes y después) a lo que ya registra.

Talla: L. Si una prueba existente falla por otra razón que el x/n, se para (cláusula del plan).
