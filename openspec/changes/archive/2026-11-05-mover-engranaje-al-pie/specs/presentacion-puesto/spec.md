# Spec Delta

## ADDED Requirements

### Requirement: Cabecera con logos y el engranaje en el pie

La cabecera SHALL mostrar el logo de la empresa, el nombre del servicio y el logo de la concesionaria, y MUST NOT tener controles. El pie MUST mostrar el estado de sincronización y, a la derecha, la marca Intelektron, discreta. En reposo, el engranaje de Configuración MUST ir en el pie, en el borde derecho y al lado de la marca, con el nombre accesible «Configuración» y un área de toque de al menos 56 px; durante una atención MUST NOT mostrarse.

#### Scenario: El engranaje en el borde inferior derecho
Origen: pedido de David del 04/11/2026 («que el botón de configuración esté en el borde inferior derecho, al lado del logo de Intelektron»).
- **GIVEN** el puesto en reposo, con un área de 1024 × 768
- **WHEN** se revisan la cabecera y el pie
- **THEN** la cabecera no tiene botones y el pie tiene uno solo, con el nombre «Configuración»
- **AND** ese botón queda a la derecha de la marca Intelektron, con su borde derecho y su borde inferior a no más de 16 px de los de la ventana, y mide al menos 56 × 56 px

#### Scenario: Sin engranaje durante una atención
Origen: AT16 («siguiente persona inicia limpia») y RB05: Configuración no se abre con una persona en curso.
- **GIVEN** una persona identificada en la selección
- **WHEN** se revisan la cabecera y el pie
- **THEN** no hay ningún botón «Configuración»

#### Scenario: El pie no cambia de alto al salir de reposo
Origen: «zonas de acción estables» de los criterios táctiles de David del 03/11/2026.
- **GIVEN** el puesto en reposo, con el catálogo de nueve opciones y un área de 1024 × 768
- **WHEN** una persona se identifica y se muestra la selección
- **THEN** el pie mide lo mismo que en reposo
- **AND** las nueve opciones, «Cancelar» y «Confirmar» se siguen viendo sin desplazar la pantalla

## REMOVED Requirements

### Requirement: Cabecera con logos y un solo control
**Reason**: El 04/11/2026 David pidió el engranaje en el borde inferior derecho, al lado de la marca Intelektron; la cabecera queda sin controles. Lo reemplaza «Cabecera con logos y el engranaje en el pie», que conserva los logos, la marca, la sincronización, el nombre accesible, el área de toque y el escenario «Sin engranaje durante una atención».
**Migration**: El escenario «Un solo control en la cabecera» pasa a «El engranaje en el borde inferior derecho». Quien busque el engranaje en la cabecera lo encuentra en el pie, con el mismo nombre «Configuración».
