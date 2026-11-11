# Proposal

## Why

El 11/11/2026 a las 18:45 David pidió, textual: «Sí será importante que en configuración de Personas exista un ícono que
claramente distinga a las personas que son operadores, que se pueda editar allí, y que esto además se refleje en los
archivos de exportación, importación y ejemplos relacionados.»

Hoy el permiso `OPERATOR` vive en `tbComedorPermisosOperadores` de cada versión, con su vigencia y su revocación. La
pestaña Personas no lo muestra ni lo edita; sólo lo usa «Reemplazar» de la importación, para no dar de baja a los
operadores. Los Excel de Personas no lo llevan. Un operador se hace hoy sólo con la semilla o con una versión que lo
traiga.

Origen: historias HU03.1 (el operador con permiso) y HU03.3 (configurar el equipo sin red), RB26 y AT54; ticket TK02.1
(interfaz). Saca de la segunda ola, recortado, el alta y la baja del permiso `OPERATOR` desde el puesto (`AGENTS.md`,
«los demás permisos de operador»).

## Gate respondido (David, 11/11/2026)

1. **Qué se edita**: sólo sí o no. Marcarlo da el permiso desde el guardado y sin fin; quitarlo lo revoca en la versión
   nueva, sin borrar la fila.
2. **Quién**: con la clave del día, a cualquiera. Un operador marca y desmarca a otros, pero no a sí mismo.
3. **El ícono**: una columna «Operador» después de «Estado», con un escudo y el texto accesible «Operador», ordenable;
   en edición, la misma celda se toca para marcar.
4. **Los Excel**: una columna «Operador» con «Sí» o «No» en la exportación, la planilla de ejemplo y la importación. Al
   importar, vacía o ausente no cambia nada; otro valor es un error de su fila.
5. **Entra en `final-project-dw` antes del Typeform del 12/11**, en el mismo goal que las otras cuatro mejoras
   aprobadas.

El delta lo aprobó David el 11/11/2026 a las 19:05 («aprobado el delta»), y aparte el cambio de la prueba de la tarea 1.1.

## What Changes

- **La columna «Operador»** en la tabla de Personas: el escudo para quien tiene `OPERATOR` vigente, ordenable, y en
  edición una celda que se marca o se desmarca.
- **El guardado** da o revoca el permiso en la versión nueva, y lo deja en el `CONFIGURATION_CHANGED` de la persona.
- **Un operador no cambia su propio permiso**: su celda no se toca, y el servicio deniega el pedido.
- **Los Excel**: «Operador» al final de la exportación; después de «Código» en la planilla de ejemplo, con la segunda
  fila de ejemplo como operador; y en la importación, con el resumen y `PEOPLE_IMPORTED` contando permisos dados y
  quitados.

## Non-goals

- No se editan el permiso `SYSTEM_ADMINISTRATOR` ni la vigencia del permiso.
- No cambian el engranaje, la entrada del operador ni lo que puede hacer en Configuración.
- Con una versión de INWeb activa, la edición sigue bloqueada, como todo Personas.
- El consumo excepcional del operador (la mejora 6) sigue abierto.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `administracion-local`:
  - se suman «Los operadores en Personas» y «El permiso de operador al importar Personas»;
  - «Exportar Personas a CSV y a Excel» y «La planilla de ejemplo de Personas» suman la columna «Operador».

  Ningún otro change sin archivar de la rama modifica estos requisitos.

## Impact

- Código:
  - `AdministracionLocal.cs`: dar y revocar en la versión nueva, y la regla del propio permiso;
  - `PanelPersonas.razor` y `wwwroot/css/puesto.css`: la columna y la celda;
  - `ExportacionPersonas.cs` e `ImportacionPersonas.cs`: la columna, los valores, los errores, la planilla y sus
    instrucciones;
  - `MensajesAtencion.cs`.
- Pruebas existentes que el cambio exige tocar, con la aprobación de David: **una**,
  `ImportacionPersonasTests` («Descargar la planilla de ejemplo»), que compara el encabezado entero de la planilla y
  pasa a esperar «Operador» después de «Código». Las de la exportación leen las celdas por posición, y no cambian
  porque «Operador» va al final. Ver la tarea 1.1.
- Documentación: `readme.md` (la tabla de códigos y Personas) y `docs/aplicacion-del-curso.md` (la evidencia).
