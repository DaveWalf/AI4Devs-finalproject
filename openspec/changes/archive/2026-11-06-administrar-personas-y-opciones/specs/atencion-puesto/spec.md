# Spec Delta

## MODIFIED Requirements

### Requirement: Aviso claro cuando no queda ninguna opción

Si la persona no tiene ninguna opción disponible, el puesto SHALL mostrar «No tenés opciones disponibles ahora» con «Hoy ya usaste:» o, sin categoría agotada, el primer motivo que aplique: el de `PERSON_INACTIVE`; «No hay opciones disponibles en este horario» si alguna quedó retenida por su franja; o «No tenés servicios habilitados. Consultá al responsable del comedor». MUST NOT ofrecer opciones ni «Confirmar», y MUST volver a reposo con «Listo» o a los 10 s, sin intención ni consumo.

#### Scenario: Todo el cupo del día usado
Origen: pedido de David del 04/11/2026 («Debe informarme con claridad si la persona no tiene cupo»).
- **GIVEN** `0001` con un almuerzo y dos refrigerios ya consumidos en la fecha de negocio
- **WHEN** se identifica
- **THEN** la pantalla muestra «No tenés opciones disponibles ahora» y «Hoy ya usaste: almuerzo 1 de 1 · refrigerio 2 de 2», sin opciones ni «Confirmar»
- **AND** «Listo» vuelve a reposo sin el nombre, y no existe ninguna intención nueva

#### Scenario: Persona sin ninguna habilitación
Origen: RB02 (`SERVICE_NOT_ENABLED`) y AT02.
- **GIVEN** una persona activa, con su credencial vigente y sin ninguna regla
- **WHEN** se identifica
- **THEN** la pantalla muestra «No tenés opciones disponibles ahora» y «No tenés servicios habilitados. Consultá al responsable del comedor», sin «Hoy ya usaste:»

#### Scenario: Persona inactiva
Origen: AT09 y RB02 («motivos separados»).
- **GIVEN** una persona inactiva con una regla vigente y cupo libre
- **WHEN** se identifica
- **THEN** la pantalla muestra «No tenés opciones disponibles ahora» y «Tu habilitación no está activa. Consultá al responsable»

#### Scenario: Diez segundos sin tocar
Origen: TK02.1, trabajo incluido 1 («resultado y regreso a reposo»), y HU03-E.
- **GIVEN** la pantalla sin opciones de una persona
- **WHEN** pasan 10 s sin un toque
- **THEN** el puesto vuelve a reposo sin el nombre ni el detalle de esa persona

#### Scenario: Fuera de horario
Origen: pedido de David del 04/11/2026 (franjas en Configuración) y la regla general del mismo día («Si no le queda ninguna, una pantalla clara con ese detalle»).
- **GIVEN** `0001` sin consumos en el día y todas las opciones del catálogo con la franja de 11:00 a 14:00
- **WHEN** se identifica a las 16:00
- **THEN** la pantalla muestra «No tenés opciones disponibles ahora» y «No hay opciones disponibles en este horario», sin «Hoy ya usaste:», sin opciones ni «Confirmar»
