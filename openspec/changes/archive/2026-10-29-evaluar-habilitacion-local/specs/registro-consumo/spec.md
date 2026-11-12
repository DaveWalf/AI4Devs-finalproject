# Spec Delta

## MODIFIED Requirements

### Requirement: Cupo por persona, ámbito, categoría y fecha de negocio

El sistema SHALL decidir el cupo de una intención nueva con la clave `PersonId` + `QuotaScopeId` + `QuotaBucketId` + `BusinessDate` (RB03): autoriza sólo si, para cada carga de la opción de menú, el uso más las unidades no supera el límite de la regla que eligió la habilitación local para ese servicio y esa categoría (capability `habilitacion-local`, RB02), y la clave toma el ámbito de esa misma regla. Sin regla aplicable MUST denegar con `SERVICE_NOT_ENABLED`.

#### Scenario: Variantes que comparten categoría cuentan juntas
Origen: AT02 (RB03, «Variantes pueden compartir categoría»).
- **GIVEN** una persona con límite 1 en ALMUERZO que ya consumió la variante común
- **WHEN** confirma una intención nueva por la variante opcional del mismo ALMUERZO
- **THEN** la decisión es DENIED con `LOCAL_LIMIT_REACHED`

#### Scenario: Otra categoría no consume el mismo cupo
Origen: AT02.
- **GIVEN** una persona que agotó ALMUERZO y tiene cupo libre en REFRIGERIO
- **WHEN** confirma una intención nueva por una opción de REFRIGERIO
- **THEN** la decisión es AUTHORIZED

#### Scenario: Persona sin regla aplicable
Origen: sin AT propio en el corte; RB02, «Sin regla aplicable se deniega», con la persona sin derecho del fixture.
- **GIVEN** una persona sin ninguna regla por persona vigente para ese servicio, ámbito y categoría
- **WHEN** confirma una intención nueva
- **THEN** la decisión es DENIED con `SERVICE_NOT_ENABLED` y no se crea consumo
