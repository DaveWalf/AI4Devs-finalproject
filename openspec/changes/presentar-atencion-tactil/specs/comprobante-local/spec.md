# Spec Delta

## ADDED Requirements

### Requirement: Comprobante en pantalla con la impresora simulada

Con la impresora simulada, cuando una confirmación se autoriza y el comprobante sale, el resultado SHALL mostrar una impresora de la que sale el comprobante, con los datos que la impresora simulada escribe en su archivo y sin el nombre de la persona ni el código. Tocar el comprobante MUST equivaler a «Listo». Si el comprobante no salió, MUST NOT mostrarse ninguno. Con movimiento reducido, MUST aparecer sin animación.

#### Scenario: Autorización con la impresora disponible
Origen: decisión de David del 03/11/2026 (impresora simulada, pasos 1 a 4) y AT06.
- **GIVEN** la impresora simulada en «Disponible» y una persona con una opción elegida
- **WHEN** confirma y la confirmación se autoriza
- **THEN** la pantalla muestra «Consumo registrado. Retirá tu comprobante», la impresora y el comprobante con la opción y el cargo de ese consumo, sin el nombre de la persona
- **AND** al tocar el comprobante el puesto vuelve a reposo sin el nombre ni la elección

#### Scenario: Sin papel no sale comprobante
Origen: AT06 («consumo preservado, trabajo recuperable») y «Sin papel» de `docs/experiencia.md`.
- **GIVEN** la impresora simulada en «Sin papel»
- **WHEN** una confirmación se autoriza
- **THEN** la pantalla muestra «Tu consumo está registrado. El comprobante queda pendiente» y ningún comprobante

#### Scenario: Un rechazo no muestra la impresora
Origen: AT02 visto desde el puesto: un rechazo no crea comprobante.
- **GIVEN** una persona que ya usó su cupo de ALMUERZO
- **WHEN** confirma otra opción de ALMUERZO
- **THEN** la pantalla muestra el mensaje de `LOCAL_LIMIT_REACHED` y no muestra la impresora

#### Scenario: Diez segundos sin tocar el comprobante
Origen: TK02.1, trabajo incluido 1 («resultado y regreso a reposo»), y HU03-E.
- **GIVEN** el comprobante en pantalla
- **WHEN** pasan 10 s sin que nadie lo toque
- **THEN** el puesto vuelve a reposo sin el nombre ni la elección

#### Scenario: Movimiento reducido
Origen: decisión de David del 03/11/2026 (UX táctil, criterio 6: «alternativa sin movimiento cuando el dispositivo la solicite»).
- **GIVEN** el sistema pide movimiento reducido
- **WHEN** sale el comprobante
- **THEN** aparece completo desde el primer cuadro, sin animación

#### Scenario: Con otra impresora queda «Listo»
Origen: D11 de `atender-seleccion-y-confirmacion` (la simulación es sólo de la demostración).
- **GIVEN** un puesto cuya impresora no es la simulada
- **WHEN** una confirmación se autoriza
- **THEN** el resultado muestra el mensaje y el botón «Listo», sin impresora en pantalla

### Requirement: Modo de la impresora simulada desde Configuración

Con la impresora simulada, Configuración SHALL ofrecer la sección «Impresora simulada» con los modos «Disponible», «Sin papel» e «Incierta»; el modo elegido MUST aplicar desde el próximo envío. Las pantallas de atención MUST NOT mostrar esos modos. Con una impresora que no es la simulada, la sección MUST NOT aparecer.

#### Scenario: Elegir «Sin papel» desde Configuración
Origen: AT06, con el modo elegido fuera de la pantalla de atención.
- **GIVEN** la impresora simulada en «Disponible»
- **WHEN** en Configuración se elige «Sin papel», se vuelve a reposo y una persona confirma
- **THEN** el consumo queda registrado y el comprobante, pendiente

#### Scenario: Con otra impresora no hay sección
Origen: D11 de `atender-seleccion-y-confirmacion` (la simulación es sólo de la demostración).
- **GIVEN** un puesto cuya impresora no es la simulada
- **WHEN** se abre Configuración
- **THEN** no hay sección «Impresora simulada»
