## ADDED Requirements

### Requirement: La versión del programa en General

Al final de la pestaña General, la sección «Versión del programa» SHALL mostrar la versión del paquete («desarrollo» sin ella), el commit completo, la fecha y hora de compilación en la zona del puesto y la versión de .NET. Sin fecha de compilación guardada, MUST usar la del archivo del programa; sin commit, MUST decir «sin commit». MUST NOT mostrar rutas, el nombre del equipo ni otros datos del equipo.

#### Scenario: La versión de un paquete publicado
Origen: mejora 3 de David del 11/11/2026 («agregar información de la versión en la última sección dentro de Configuración») y su respuesta 4 al gate, con HU03-D y RF41.
- **GIVEN** el EXE armado con el tag `v1.0.1`, compilado el 11/11/2026 a las 21:30 UTC, y el puesto en la zona de Buenos Aires
- **WHEN** se abre la pestaña General
- **THEN** su última sección, «Versión del programa», dice `v1.0.1`, el commit de 40 caracteres, «11/11/2026 18:30» y la versión de .NET

#### Scenario: La versión de un build de desarrollo
Origen: respuesta 5 de David al gate del 11/11/2026 («desarrollo» y el commit), borde: sin tag ni fecha guardada.
- **GIVEN** el puesto compilado desde el código, sin versión de paquete ni fecha de compilación guardadas
- **WHEN** se abre la pestaña General
- **THEN** la sección dice «desarrollo», el commit y, como fecha de compilación, la del archivo del programa

#### Scenario: Sin datos del equipo
Origen: `AGENTS.md` (sólo datos sintéticos) y HU03-D («sin biometría ni secretos»), borde.
- **GIVEN** el puesto en una carpeta cuya ruta tiene el nombre del usuario
- **WHEN** se lee la sección «Versión del programa»
- **THEN** no contiene ninguna ruta, ni el nombre del equipo ni el del usuario
