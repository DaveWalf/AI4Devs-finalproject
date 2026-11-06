# Spec Delta

## ADDED Requirements

### Requirement: La franja horaria de la opción entra a la misma evaluación

La evaluación única SHALL comprobar la franja de la opción después de la persona, la credencial y las reglas, y antes del cupo, con la hora local del puesto en el instante de la decisión. Fuera de toda franja de la opción MUST denegar con `SERVICE_NOT_ENABLED` y la causa interna `OUTSIDE_SERVICE_HOURS`, sin consumo ni cargas de cupo. Una opción sin franjas MUST NOT denegarse por horario.

#### Scenario: Antes, dentro y en el fin de la franja
Origen: pedido de David del 04/11/2026 («franjas horarias en las cuales están habilitados») y la regla general del mismo día («las franjas horarias … se suman a esa evaluación»).
- **GIVEN** una persona con cupo libre y «Almuerzo común» con la franja de 11:00 a 14:00
- **WHEN** se calcula su oferta y confirma una intención nueva por esa opción a las 10:59, a las 11:00 y a las 14:00 de la hora del puesto
- **THEN** a las 11:00 se ofrece y se autoriza
- **AND** a las 10:59 y a las 14:00 se retiene y se deniega con `SERVICE_NOT_ENABLED` y la causa `OUTSIDE_SERVICE_HOURS`, sin consumo

#### Scenario: La franja cierra entre la oferta y la confirmación
Origen: RB06 («decisión calculada bajo el bloqueo»), borde.
- **GIVEN** una persona a la que se le ofreció «Almuerzo común» a las 13:59 con la franja de 11:00 a 14:00, y que lo eligió
- **WHEN** toca «Confirmar» a las 14:00
- **THEN** la decisión es DENIED con `SERVICE_NOT_ENABLED` y la causa `OUTSIDE_SERVICE_HOURS`, sin consumo
- **AND** la pantalla muestra «Esta opción no está disponible en este horario»

#### Scenario: Una regla cero decide antes que la franja
Origen: RB02 («`Limit=0` … prevalece») y AT36, sobre el orden de la evaluación.
- **GIVEN** una persona con una regla cero vigente en ALMUERZO y «Almuerzo común» fuera de su franja
- **WHEN** confirma una intención nueva por esa opción
- **THEN** la decisión es DENIED con `RULE_DENIED`, sin causa de horario

#### Scenario: El cupo agotado sigue a la vista fuera de horario
Origen: pedido de David del 04/11/2026 («informarme con claridad si la persona no tiene cupo»), borde.
- **GIVEN** `0001` con su almuerzo del día ya consumido y las opciones de ALMUERZO fuera de su franja
- **WHEN** se identifica
- **THEN** la selección muestra «Hoy ya usaste: almuerzo 1 de 1»

## MODIFIED Requirements

### Requirement: Vigencia de las reglas en el instante de la decisión

El sistema SHALL considerar aplicables sólo las reglas por persona del servicio y la categoría pedidos cuyo intervalo `[ValidFromUtc, ValidToUtc)` contenga el instante de la decisión según el reloj del puesto; un extremo ausente deja el intervalo abierto. La vigencia es una regla de negocio: MUST NOT depender de la red ni agregar caducidades u horarios que no sean la franja de la opción. Sin regla aplicable MUST denegar con `SERVICE_NOT_ENABLED`.

#### Scenario: Regla con fecha de fin ya alcanzada
Origen: AT29.
- **GIVEN** una persona cuya única regla para ALMUERZO tiene `ValidToUtc` anterior al instante de la decisión, y el puesto sin red
- **WHEN** confirma una intención nueva por ALMUERZO
- **THEN** la decisión es DENIED con `SERVICE_NOT_ENABLED`, como regla de negocio y sin ningún intento de red

#### Scenario: Inicio exacto de la vigencia
Origen: AT36 y AT52 («límites exactos ValidFromUtc/ValidToUtc»).
- **GIVEN** una persona cuya única regla para ALMUERZO tiene `ValidFromUtc` igual al instante de la decisión
- **WHEN** confirma una intención nueva por ALMUERZO
- **THEN** la decisión es AUTHORIZED

#### Scenario: Regla sin vencimiento y un período prolongado sin red
Origen: HU01-H.
- **GIVEN** una persona con una regla sin `ValidToUtc` y el reloj del puesto 400 días después de la activación de su configuración, sin red en todo ese tiempo
- **WHEN** confirma una intención nueva
- **THEN** la decisión es AUTHORIZED: no hay caducidad de caché

#### Scenario: Horario sólo informativo
Origen: AT17.
- **GIVEN** una persona con una regla vigente que no declara horario, por una opción sin franjas
- **WHEN** confirma una intención nueva a las 03:00 de la hora del puesto
- **THEN** la decisión es AUTHORIZED: no se agrega una restricción horaria ajena a la configuración

### Requirement: Una sola evaluación ofrece y confirma

La disponibilidad de una opción para una persona SHALL decidirla una sola evaluación del dominio, la misma que decide una intención nueva: persona activa, credencial de la sesión, reglas vigentes, regla cero, prioridad, franja horaria de la opción y cupo de la fecha de negocio. La oferta del puesto y la confirmación MUST usar esa evaluación, y toda condición nueva de disponibilidad MUST sumarse a ella y no a la pantalla.

#### Scenario: La oferta y la confirmación coinciden
Origen: la regla general de David del 04/11/2026 y la nota de políticas de consumo («no debe quedar dispersa en condiciones de pantalla»), con los motivos de RB02.
- **GIVEN** cinco personas: una armada para cada motivo, `LOCAL_LIMIT_REACHED`, `SERVICE_NOT_ENABLED`, `RULE_DENIED` y `PERSON_INACTIVE`, y una con cupo libre
- **WHEN** se calcula la oferta de cada una y, en el mismo instante, cada una confirma una intención nueva por ALMUERZO
- **THEN** a las cuatro primeras la oferta les retiene el almuerzo con el mismo motivo con que la confirmación lo deniega
- **AND** a la quinta se lo ofrece, y la confirmación lo autoriza

#### Scenario: Calcular la oferta no registra hechos
Origen: RB05 («sin consumo hasta confirmar»), borde.
- **GIVEN** una persona con cupo libre
- **WHEN** se calcula su oferta
- **THEN** no existe ninguna intención, consumo, carga de cupo ni evento de salida nuevos

#### Scenario: Fuera de horario, la oferta y la confirmación también coinciden
Origen: la regla general de David del 04/11/2026 («así la oferta y la confirmación nunca discrepan»).
- **GIVEN** una persona con cupo libre y «Almuerzo común» fuera de su franja
- **WHEN** se calcula su oferta y, en el mismo instante, confirma una intención nueva por esa opción
- **THEN** la oferta la retiene y la confirmación la deniega con el mismo motivo y la misma causa
