# habilitacion-local Specification

## Purpose
Decidir en el puesto, sin red y con la base local, si una persona está habilitada para el servicio que pide (RB02): persona activa, credencial de la sesión vigente, reglas vigentes en el instante de la decisión, la denegación explícita `Limit=0` y la prioridad entre reglas positivas. La decisión es la de una intención nueva y forma parte de la confirmación atómica (RB06).

## Requirements

### Requirement: Persona activa y credencial vigente antes que las reglas

Para una intención nueva, el sistema SHALL evaluar primero que la persona esté activa y, si la sesión lleva una credencial, que no esté revocada ni vencida en el instante de la decisión. Cada falla MUST denegar con su propio motivo, en este orden: `PERSON_INACTIVE`, `CREDENTIAL_REVOKED`, `CREDENTIAL_EXPIRED`, como un rechazo de RB06: sin consumo, cargas de cupo ni ticket. Una sesión sin credencial no evalúa credencial.

#### Scenario: Persona dada de baja con regla vigente y cupo libre
Origen: AT09, la parte que no necesita snapshot («aplica baja … sin borrar consumos previos»), y RB02 («motivos separados»).
- **GIVEN** una persona inactiva que tiene una regla vigente con cupo libre y un consumo anterior registrado
- **WHEN** confirma una intención nueva
- **THEN** la decisión es DENIED con `PERSON_INACTIVE`, sin consumo ni cargas de cupo
- **AND** su consumo anterior sigue registrado

#### Scenario: Credencial de la sesión revocada
Origen: AT53, la parte local (credencial ya revocada en la base y replay de una intención ya decidida), y RB25 («Si tiene RevokedAtUtc, da CREDENTIAL_REVOKED»).
- **GIVEN** una sesión cuya credencial tiene fecha de revocación, y una intención anterior de esa persona ya decidida como AUTHORIZED
- **WHEN** confirma una intención nueva y repite la intención anterior
- **THEN** la intención nueva es DENIED con `CREDENTIAL_REVOKED`
- **AND** el replay de la anterior devuelve su resultado original, y ningún consumo previo se borra

#### Scenario: Credencial de la sesión vencida en el instante de la decisión
Origen: AT52, la parte sin huella (límites exactos de `ValidToUtc`), y RB25 («Vencida da CREDENTIAL_EXPIRED»).
- **GIVEN** una sesión todavía abierta cuya credencial tiene `ValidToUtc` igual al instante de la decisión
- **WHEN** confirma una intención nueva
- **THEN** la decisión es DENIED con `CREDENTIAL_EXPIRED`

#### Scenario: Persona inactiva con una regla cero vigente
Origen: RB02 («una persona inactiva … conserva su motivo propio y no se clasifica como regla cero») y AT36.
- **GIVEN** una persona inactiva que además tiene una regla aplicable con `Limit=0`
- **WHEN** confirma una intención nueva
- **THEN** la decisión es DENIED con `PERSON_INACTIVE`, no con `RULE_DENIED`

#### Scenario: Sesión sin credencial
Origen: AT01 y la sesión de prueba sin código (D6 de `fijar-primer-corte`).
- **GIVEN** una persona activa con una regla vigente y cupo libre, en una sesión que no lleva credencial
- **WHEN** confirma una intención nueva
- **THEN** la decisión es AUTHORIZED: sin credencial no hay nada que vencer ni revocar

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

### Requirement: La regla cero prevalece sobre cualquier permiso

Entre las reglas aplicables de una categoría, una con `Limit=0` SHALL denegar con `RULE_DENIED` aunque haya un permiso positivo de mayor `Priority`. Una regla cero que dejó de aplicar por su vigencia MUST NOT participar: se evalúan las reglas restantes. Si una opción de menú tiene cargas en varias categorías, una regla cero en cualquiera de ellas deniega la opción entera.

#### Scenario: Regla cero vigente frente a un permiso de mayor prioridad
Origen: AT36 y HU01-I.
- **GIVEN** una persona activa con un permiso para ALMUERZO de `Priority` 10 y límite 1, y una regla cero para ALMUERZO de `Priority` 1 con inicio y fin definidos, vigente en el instante de la decisión
- **WHEN** confirma una intención nueva por ALMUERZO
- **THEN** la decisión es DENIED con `RULE_DENIED`, no por baja ni por cupo

#### Scenario: En el fin exacto de la regla cero se evalúa el permiso
Origen: AT36 («En el límite final la regla cero ya no aplica y se evalúa el permiso restante») y HU01-I.
- **GIVEN** la misma persona, con el instante de la decisión igual al `ValidToUtc` de la regla cero
- **WHEN** confirma una intención nueva por ALMUERZO
- **THEN** la decisión es AUTHORIZED con el límite del permiso

#### Scenario: Una regla cero en una de dos categorías deniega la opción
Origen: RB02 y AT36, sobre la opción con dos cargas de AT02.
- **GIVEN** una persona con permiso en ALMUERZO y una regla cero vigente en REFRIGERIO
- **WHEN** confirma una intención nueva por la opción que carga ALMUERZO y REFRIGERIO
- **THEN** la decisión es DENIED con `RULE_DENIED` y no se carga ninguna de las dos categorías

### Requirement: Entre reglas positivas decide la de mayor prioridad

Entre las reglas positivas aplicables de una categoría, el sistema SHALL tomar la de mayor `Priority`, y el cupo SHALL usar su límite y su ámbito. Si en la mayor prioridad hay más de una regla y difieren en límite o en ámbito, el sistema MUST NOT decidir: la confirmación termina con un error de configuración y no escribe intención, decisión, consumo ni auditoría. Reglas empatadas con el mismo límite y el mismo ámbito no son contradictorias.

#### Scenario: Gana la de mayor prioridad
Origen: RB02 («Entre reglas positivas gana mayor Priority») y TK01 punto 2.
- **GIVEN** una persona con dos permisos para ALMUERZO, uno de `Priority` 2 y límite 2 y otro de `Priority` 1 y límite 1, que ya consumió una vez en el día
- **WHEN** confirma una intención nueva por ALMUERZO
- **THEN** la decisión es AUTHORIZED, con el límite 2 de la regla de mayor prioridad

#### Scenario: Empate contradictorio en la mayor prioridad
Origen: RB02 («empate contradictorio impide publicar»).
- **GIVEN** una persona con dos permisos para ALMUERZO de igual `Priority` y límites 1 y 2
- **WHEN** confirma una intención nueva por ALMUERZO
- **THEN** la confirmación termina con un error de configuración
- **AND** la base no tiene ninguna intención, decisión, consumo ni registro de auditoría nuevos

#### Scenario: Empate sin contradicción
Origen: RB02; el empate sólo impide decidir si las reglas dicen cosas distintas.
- **GIVEN** una persona con dos permisos para ALMUERZO de igual `Priority`, igual límite 1 e igual ámbito
- **WHEN** confirma una intención nueva por ALMUERZO
- **THEN** la decisión es AUTHORIZED con límite 1

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
