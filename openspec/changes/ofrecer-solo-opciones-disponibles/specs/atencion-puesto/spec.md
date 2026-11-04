# Spec Delta

## ADDED Requirements

### Requirement: Sólo las opciones disponibles para la persona

Al abrirse la sesión, el puesto SHALL ofrecer sólo las opciones del snapshot activo que la evaluación de la confirmación autorizaría a esa persona en ese instante, en el orden del catálogo, y MUST aplicarles la distribución por cantidad. Si alguna categoría de cupo de la persona está agotada en la fecha de negocio, la selección MUST mostrar «Hoy ya usaste:» con cada categoría agotada, su uso y su límite.

#### Scenario: El almuerzo del día ya consumido
Origen: pedido de David del 04/11/2026 («sólo debe presentar como disponibles aquellas opciones que la persona sí tenga habilitado») y AT02.
- **GIVEN** `0001`, con límite 1 en ALMUERZO y 2 en REFRIGERIO, el catálogo de nueve y un almuerzo ya consumido en la fecha de negocio
- **WHEN** se identifica
- **THEN** la selección ofrece sólo las cinco opciones de REFRIGERIO, en el orden del catálogo, en una columna y sin «Anterior» ni «Siguiente»
- **AND** muestra «Hoy ya usaste: almuerzo 1 de 1»

#### Scenario: Nada usado en el día
Origen: AT01 y AT16, borde: sin consumos, la oferta es el catálogo entero.
- **GIVEN** `0001` sin consumos en la fecha de negocio y el catálogo de nueve
- **WHEN** se identifica
- **THEN** la selección ofrece las nueve opciones y no muestra «Hoy ya usaste:»

#### Scenario: Una regla cero no es cupo usado
Origen: RB02 («la regla cero prevalece») y AT36.
- **GIVEN** una persona con límite 1 en ALMUERZO y una regla de REFRIGERIO con `Limit=0`, sin consumos en el día
- **WHEN** se identifica
- **THEN** la selección ofrece sólo las opciones de ALMUERZO y no muestra «Hoy ya usaste:»

#### Scenario: La opción se agota entre la oferta y la confirmación
Origen: RB06 («decisión calculada bajo el bloqueo») y AT02.
- **GIVEN** `0001` con el almuerzo ofrecido y elegido, y un uso de su cupo de ALMUERZO registrado en la fecha de negocio antes de que confirme
- **WHEN** toca «Confirmar»
- **THEN** la pantalla muestra el mensaje de `LOCAL_LIMIT_REACHED` y no existe ningún consumo nuevo

### Requirement: Aviso claro cuando no queda ninguna opción

Si la persona no tiene ninguna opción disponible, el puesto SHALL mostrar «No tenés opciones disponibles ahora» con la línea «Hoy ya usaste:» o, sin categoría agotada, un motivo: el de `PERSON_INACTIVE` si la persona está inactiva y, si no, «No tenés servicios habilitados. Consultá al responsable del comedor». MUST NOT ofrecer opciones ni «Confirmar», y MUST volver a reposo con «Listo» o a los 10 s, sin intención ni consumo.

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

### Requirement: La oferta queda en la bitácora

Cada oferta SHALL quedar en la bitácora como un registro `OPTIONS_OFFERED` con la sesión, la persona, la versión de configuración, las opciones ofrecidas con su versión, las retenidas con su motivo y las categorías agotadas con su uso y su límite. MUST NOT contener el código de identificación.

#### Scenario: Oferta con una categoría agotada
Origen: RF43 a RF46 y AT41 (reconstruir la interacción), y pedido de David del 04/11/2026 («una completa reconstrucción de qué se hizo o qué ocurrió en cada interacción»).
- **GIVEN** una persona con un código sintético reconocible, límite 1 en ALMUERZO y 2 en REFRIGERIO, el catálogo de nueve y un almuerzo ya consumido en el día
- **WHEN** se identifica
- **THEN** existe un registro `OPTIONS_OFFERED` con cinco opciones ofrecidas, cuatro retenidas con `LOCAL_LIMIT_REACHED` y la categoría de ALMUERZO con uso 1 y límite 1
- **AND** el registro no contiene el código

#### Scenario: Oferta vacía
Origen: AT41, borde: una oferta sin opciones también se reconstruye.
- **GIVEN** una persona activa sin ninguna regla
- **WHEN** se identifica
- **THEN** existe un registro `OPTIONS_OFFERED` sin opciones ofrecidas y con todas las del catálogo retenidas con `SERVICE_NOT_ENABLED`

## MODIFIED Requirements

### Requirement: Catálogo y selección sin consumo hasta confirmar

Con la sesión abierta, el puesto SHALL mostrar las opciones de menú que le ofrece a esa persona, con su nombre y su cargo informado, como botones operables por toque y por teclado (RF04); una opción con importe cero MUST mostrarse sin importe. Cancelar, o 30 s sin actividad antes de confirmar, MUST volver a reposo sin escribir ninguna intención ni consumo (RB05), y la persona siguiente MUST empezar sin rastro de la anterior.

#### Scenario: Opción sin cargo
Origen: decisión de David del 02/11/2026 (subvención total: valor cero, ningún importe visible, confirmación habilitada).
- **GIVEN** un catálogo con una opción de importe cero
- **WHEN** la persona la elige
- **THEN** la opción y su confirmación no muestran ningún importe
- **AND** «Confirmar» queda habilitado

#### Scenario: Opciones y navegación alcanzables con el teclado
Origen: TK02.1, trabajo incluido 2 («operable por toque y teclado»).
- **GIVEN** el catálogo visible
- **WHEN** se revisan sus controles
- **THEN** cada opción, cada control de página cuando hay más de diez opciones, «Cancelar» y «Confirmar» son botones que el teclado alcanza

#### Scenario: Cancelar antes de confirmar
Origen: HU01-B.
- **GIVEN** una persona que eligió una opción y no confirmó
- **WHEN** toca «Cancelar»
- **THEN** el puesto vuelve a reposo y no existe ninguna intención ni consumo nuevos

#### Scenario: Treinta segundos sin actividad
Origen: AT16 («timeout antes de confirmar … cero consumo; siguiente persona inicia limpia»).
- **GIVEN** una persona en la selección cuyo último toque fue hace exactamente 30 s
- **WHEN** el puesto revisa la inactividad
- **THEN** vuelve a reposo sin intención ni consumo nuevos
- **AND** cuando otra persona se identifica, la pantalla no muestra el nombre ni la elección de la anterior

#### Scenario: Un toque reinicia el plazo
Origen: AT16, borde del plazo.
- **GIVEN** una persona en la selección que toca «Siguiente» a los 29 s de identificarse
- **WHEN** pasan 45 s desde que se identificó
- **THEN** sigue en la selección, y vuelve a reposo recién a los 30 s de su último toque
