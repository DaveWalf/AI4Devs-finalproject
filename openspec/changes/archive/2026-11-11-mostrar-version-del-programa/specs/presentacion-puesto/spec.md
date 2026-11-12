## ADDED Requirements

### Requirement: La versión del programa bajo el título

Debajo de «INComedor · tu pausa del día», la cabecera SHALL mostrar en letra más chica y atenuada la versión del paquete y el commit corto, como «v1.0.1 · 7b80d45», en reposo y durante una atención. Sin versión de paquete, o con una fuera de forma, MUST decir «desarrollo» en su lugar, y sin commit, «sin commit». La línea MUST NOT ser un control ni cambiar el alto de la cabecera.

#### Scenario: Un paquete publicado
Origen: mejora 3 de David del 11/11/2026 («la versión del programa, de modo de poder dar seguimiento visual») y su respuesta 3 al gate (el tag del Release), con HU03-D.
- **GIVEN** el EXE armado con el tag `v1.0.1` sobre el commit `7b80d45…`
- **WHEN** se revisa la cabecera en reposo, y otra vez con una persona en la selección
- **THEN** debajo de «INComedor · tu pausa del día» dice «v1.0.1 · 7b80d45» las dos veces, más chico que el nombre del servicio

#### Scenario: Un build de desarrollo
Origen: respuesta 5 de David al gate del 11/11/2026 («desarrollo» y el commit), borde: sin tag.
- **GIVEN** el puesto compilado desde el código, sin versión de paquete, sobre el commit `7b80d45…`
- **WHEN** se revisa la cabecera
- **THEN** dice «desarrollo · 7b80d45»

#### Scenario: Una versión fuera de forma
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto) y la validación del tag de `publicar-puesto.ps1`, borde.
- **GIVEN** un EXE cuyo metadato de versión tiene 80 caracteres y un salto de línea
- **WHEN** se revisa la cabecera
- **THEN** dice «desarrollo» y el commit corto, sin ningún carácter del metadato

#### Scenario: La cabecera no cambia
Origen: «zonas de acción estables» de los criterios táctiles de David del 03/11/2026 y el requisito «Cabecera con logos y el engranaje en el pie», borde.
- **GIVEN** el puesto en reposo, a 1024 × 768
- **WHEN** se mide la cabecera
- **THEN** mide 64 px de alto como antes, sigue sin botones, y la versión queda centrada debajo del nombre del servicio, con su letra más chica que la de él
