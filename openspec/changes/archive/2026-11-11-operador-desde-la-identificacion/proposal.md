# Proposal

## Why

El 11/11/2026 a las 16:50 David pidió, textual: «Un operador debe poder comer si así lo tiene asignado y, además, debe
poder hacer los otros pasos de configuración, así que seleccionar del combo o tipiear su número debería remitir a la
misma funcionalidad.»

Hoy son dos caminos separados. El código de un operador en el diálogo del engranaje abre Configuración como operador,
con Consumos, Personas e Impresora. Ese mismo código tipeado en reposo, o la persona elegida en el combo de la
simulación, lo identifica sólo como comensal, y los operadores sintéticos, sin cupos, ven «No tenés opciones
disponibles ahora.».

Origen: historias HU03.1 (el operador con permiso) y HU01.1 (identificarse y elegir), RB26 y AT54; ticket TK02.1
(interfaz).

## Gate respondido (David, 11/11/2026)

1. **La pantalla**: sus opciones de comida, como cualquier comensal, más «Configuración» junto a «Cancelar».
2. **Sin comidas asignadas**: la pantalla de hoy, «No tenés opciones disponibles ahora.», con «Configuración» junto a
   «Listo».
3. **El engranaje** sigue aceptando el código del operador, como hoy.
4. **El combo** lleva a lo mismo, sin validaciones extra. Textual: «Esa ventana de selección de la persona sólo estará
   disponible mientras el kiosco no esté operativo: una vez operativo, se identificarán por otro medio, y no existirá el
   riesgo. Evitemos solicitar durante esta etapa de desarrollos validaciones extras a las ya establecidas.»
5. **La sesión que vence durante Configuración** la resolvió David en `reimprimir-sin-codigo` (opción a, `d6bbb24`): un
   operador que entra desde la identificación abre la misma sesión y recibe el mismo aviso.
6. La mejora entra en `final-project-dw` antes del Typeform del 12/11/2026 (decisión de las 17:55).

El delta lo aprobó David el 11/11/2026 a las 18:50 («aprobados los deltas»), con el aviso por correo de un operador sin comidas como hoy (riesgo del design).

## What Changes

- **«Configuración» en la atención** de quien tiene un permiso `OPERATOR` vigente: en la selección junto a «Cancelar»,
  y en la pantalla sin opciones junto a «Listo». Por el código o por el combo, igual.
- **Tocarlo** vuelve a comprobar el permiso, termina la atención sin consumo y abre Configuración como el engranaje con
  el código del operador: Consumos, Personas e Impresora, su sesión privilegiada y su nombre como actor.
- **La bitácora** distingue la entrada: `CONFIGURATION_OPENED` con `Access` `OPERATOR_IDENTIFICATION`, frente al
  `OPERATOR` del engranaje.
- **Una persona sin el permiso** (o con el permiso vencido o revocado) no ve el botón.

## Non-goals

- No cambian el engranaje, sus rechazos ni lo que un operador puede hacer en Configuración.
- No se suman otros permisos de operador, que siguen en la segunda ola.
- El ícono de operador en Personas, editable y reflejado en la importación y la exportación (la mejora 7, pedida a las
  18:45), va en su propio change.
- El consumo excepcional del operador (la mejora 6) quedó abierto.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `atencion-puesto`: se suma «Un operador que se identifica en reposo».
- `acceso-puesto`: «Cada acceso queda en la bitácora general» suma `OPERATOR_IDENTIFICATION`.

## Impact

- Código:
  - `MaquinaAtencion.cs`: expone la sesión de consumo y si la persona es operador;
  - `Asistencia.cs`: abre la sesión privilegiada desde la sesión de consumo, sin el código;
  - `PantallaAtencion.razor`: el botón y la apertura de Configuración;
  - `MensajesAtencion.cs`.
- Pruebas existentes que el cambio exige tocar: ninguna prevista. Las que identifican a `9001` en reposo no miran
  botones, y mostrar «Configuración» no abre ninguna sesión, así que la cuenta de sesiones de identidad no cambia. Se
  confirma con `git diff --diff-filter=M` en cada grupo de `tasks.md`.
- Documentación: `readme.md` (los códigos sintéticos y cómo entra un operador) y `docs/aplicacion-del-curso.md` (la
  evidencia).
