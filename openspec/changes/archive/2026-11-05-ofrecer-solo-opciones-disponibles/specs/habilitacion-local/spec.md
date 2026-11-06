# Spec Delta

## ADDED Requirements

### Requirement: Una sola evaluación ofrece y confirma

La disponibilidad de una opción para una persona SHALL decidirla una sola evaluación del dominio, la misma que decide una intención nueva: persona activa, credencial de la sesión, reglas vigentes, regla cero, prioridad y cupo de la fecha de negocio. La oferta del puesto y la confirmación MUST usar esa evaluación, y toda condición nueva de disponibilidad MUST sumarse a ella y no a la pantalla.

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
