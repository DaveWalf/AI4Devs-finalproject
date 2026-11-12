# Proposal

## Why

El 11/11/2026 a las 16:40 David pidió, textual: «Debajo del título de INComedor, debe mostrar con menor tamaño y más
atenuada la versión del programa, de modo de poder dar seguimiento visual. También agregar información de la versión en
la última sección dentro de Configuración.» La última sección es la pestaña General.

Hoy el puesto no muestra ninguna versión. Además, todo EXE dice `1.0.0`: `publicar-puesto.ps1` usa el tag del Release
(`-Version`) sólo para nombrar el zip, y `release.yml` no se lo pasa a MSBuild como `Version` a propósito, porque
«v0.0.0-ensayo» como `Version` rompió el restore con MSB4181 en tres corridas. Lo único que distingue una compilación
de otra es el commit que el SDK agrega a la versión informativa (`1.0.0+7b80d45…`).

Origen: historia HU03.2 (consultar el estado del puesto), escenario HU03-D («Distingue estados, versiones y última
sincronización») y RF41 («… red y versiones»); ticket TK02.1 (interfaz). El diagnóstico de versiones por componente
(AT38, HU03.4) sigue en la segunda ola.

## Gate respondido (David, 11/11/2026)

1. **Bajo el título**: la versión del tag del Release, que pasa a llegar al EXE, con el commit corto al lado
   («v1.0.1 · 7b80d45»).
2. **En General**: la versión, el commit, la fecha y hora de compilación y la versión de .NET.
3. **Sin tag** (al correr desde el código o en un build de la CI): «desarrollo» y el commit
   («desarrollo · 7b80d45»).
4. La mejora entra en `final-project-dw` antes del Typeform del 12/11/2026 (decisión de las 17:55).

El delta lo aprobó David el 11/11/2026 a las 18:23 («aprobados los deltas»).

## What Changes

- **La versión bajo el título**: debajo de «INComedor · tu pausa del día», en letra más chica y atenuada, sin cambiar
  el alto de la cabecera.
- **La sección «Versión del programa» al final de General**: la versión, el commit completo, la fecha y hora de
  compilación en la zona del puesto y la versión de .NET.
- **El tag llega al EXE**: `publicar-puesto.ps1` pasa el tag ya validado y la fecha de compilación como propiedades
  propias (`VersionPuesto` y `FechaCompilacionPuesto`), nunca como `Version`, y el Kiosk los guarda como metadatos del
  ensamblado. El E2E contra el EXE comprueba que la cabecera muestra el tag.
- **Sin tag**: «desarrollo» en lugar de la versión, y la fecha del archivo del programa como fecha de compilación.

## Non-goals

- No cambian la versión de ensamblado ni la de archivo (`1.0.0`), ni el nombre del zip, ni cuándo se arma un Release.
- No se agrega el diagnóstico de versiones por componente (AT38), que sigue en la segunda ola.
- Un operador sigue sin ver la pestaña General: ve la versión en la cabecera.
- Las otras mejoras del 11/11 van en sus propios changes.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `presentacion-puesto`: se suma «La versión del programa bajo el título».
- `configuracion-puesto`: se suma «La versión del programa en General».
- `distribucion-puesto`: se suma «El paquete lleva su versión».

## Impact

- Código: `PantallaAtencion.razor` (la línea bajo el título), `PanelConfiguracion.razor` (la sección), una clase nueva que
  lee los metadatos del ensamblado del Kiosk, `Intelektron.INComedor.Kiosk.csproj` (los `AssemblyMetadata`),
  `wwwroot/css/puesto.css`, `.github/scripts/publicar-puesto.ps1` y el E2E que ese script corre contra el EXE.
- Pruebas existentes que el cambio exige tocar: ninguna prevista. Las de la cabecera y de General comparan con
  `Contains` o buscan `.servicio`, que no cambia. La comprobación del tag va en una prueba E2E nueva. La lista se
  confirma con `git diff --diff-filter=M` en cada grupo de `tasks.md`.
- Documentación: `readme.md` (dónde se ve la versión) y `docs/aplicacion-del-curso.md` (la evidencia).
