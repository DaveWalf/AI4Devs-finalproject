# atencion-puesto Specification

## Purpose
Atender al comensal en el puesto, sin red: identificarlo por su código sintético, mostrarle el catálogo del snapshot activo, confirmar su elección con la confirmación atómica (RB06) sin duplicarla ni cambiar de persona, y pedir reconfirmación si el catálogo cambió.

## Requirements

### Requirement: Identificación simulada por código

Al tipear un código, el puesto SHALL resolverlo por tipo `SERIAL_CODE` y valor normalizado, sin espacios alrededor y con sus ceros iniciales (RB01), contra el snapshot activo (RB25). Un código vigente MUST abrir una sesión de identidad de consumo con esa credencial y mostrar la selección. En este corte el código solo abre la sesión, sin huella (identificación simulada de D-C24). El puesto MUST NOT mostrar el código después de enviarlo.

#### Scenario: Código vigente abre la selección
Origen: AT52, la parte sin huella, y AT01.
- **GIVEN** una persona activa con el código sintético `0001`, vigente y sin revocar
- **WHEN** tipea `0001` y lo envía
- **THEN** existe una sesión de identidad de propósito `CONSUMPTION` con esa persona y esa credencial
- **AND** la pantalla muestra el nombre visible de la persona y el catálogo, el campo del código queda vacío y no se escribió ninguna intención

#### Scenario: Ceros iniciales y espacios alrededor
Origen: RB01 («conservar ceros iniciales») y RB25 («valor normalizado»).
- **GIVEN** el código `0001` en el snapshot activo y ningún código `1`
- **WHEN** se envía `1`, y después se envía `0001` con espacios antes y después
- **THEN** `1` no se resuelve y `0001` con espacios abre la sesión de la persona de `0001`

### Requirement: Rechazo de un código inválido

Un código revocado, vencido, todavía no vigente o desconocido SHALL mostrar el mensaje de su motivo y volver a reposo sin abrir sesión ni selección, y sin escribir intención, consumo ni ticket. Cada rechazo MUST quedar en la bitácora con su motivo y su causa interna, sin el código en claro (RB25).

#### Scenario: Código revocado
Origen: AT52 («revocado: CREDENTIAL_REVOKED»).
- **GIVEN** un código con fecha de revocación anterior al instante de la identificación
- **WHEN** se envía
- **THEN** la pantalla muestra el mensaje de `CREDENTIAL_REVOKED` y vuelve a reposo
- **AND** no existe ninguna sesión nueva y la bitácora tiene un registro de identificación rechazada con `CREDENTIAL_REVOKED`

#### Scenario: Código vencido en el límite exacto
Origen: AT52 («límites exactos ValidFromUtc/ValidToUtc»; «vencido: CREDENTIAL_EXPIRED»).
- **GIVEN** un código con `ValidToUtc` igual al instante de la identificación
- **WHEN** se envía
- **THEN** el motivo es `CREDENTIAL_EXPIRED` y no se abre sesión

#### Scenario: Revocado y vencido a la vez
Origen: RB25 («Si tiene RevokedAtUtc, da CREDENTIAL_REVOKED desde la activación»).
- **GIVEN** un código revocado cuya `ValidToUtc` también ya pasó
- **WHEN** se envía
- **THEN** el motivo es `CREDENTIAL_REVOKED`

#### Scenario: Código todavía no vigente
Origen: AT52 («futuro/desconocido: IDENTITY_NOT_RESOLVED con causa interna distinta»).
- **GIVEN** un código con `ValidFromUtc` un milisegundo posterior al instante de la identificación
- **WHEN** se envía
- **THEN** el motivo es `IDENTITY_NOT_RESOLVED` y la bitácora registra la causa interna `NOT_YET_VALID`

#### Scenario: Código desconocido
Origen: AT52 y RB25 (causa interna `UNKNOWN_CODE`).
- **GIVEN** un código que no existe en el snapshot activo
- **WHEN** se envía
- **THEN** el motivo es `IDENTITY_NOT_RESOLVED` y la bitácora registra la causa interna `UNKNOWN_CODE`, sin `PersonId`

#### Scenario: Ningún rechazo registra el código
Origen: AT52 («sin código en logs») y `AGENTS.md`.
- **GIVEN** los cuatro rechazos anteriores, cada uno con un código sintético reconocible
- **WHEN** se revisan la bitácora, las sesiones de identidad y el texto de la pantalla después de cada rechazo
- **THEN** ninguno contiene el código enviado

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

### Requirement: Distribución del catálogo por cantidad

Hasta diez opciones MUST mostrarse juntas, sin controles de página: hasta ocho en una columna, y nueve o diez en dos columnas de hasta cinco, completando la primera antes de la segunda, cuando el ancho lo admite. Desde once MUST mostrarse en páginas de diez, con «Anterior» y «Siguiente», y cada página MUST distribuir sus opciones con la misma regla.

#### Scenario: Recorrer un catálogo de 15 opciones
Origen: AT16 («catálogo de 15 opciones … navegación usable») y decisión de David del 03/11/2026 («más de 10 opciones, debe paginar»).
- **GIVEN** un snapshot activo con 15 opciones de menú
- **WHEN** la persona avanza por todas las páginas y vuelve a la primera
- **THEN** cada una de las 15 opciones aparece exactamente una vez entre las páginas
- **AND** la primera página muestra diez opciones en dos columnas de cinco y la segunda, cinco en una columna
- **AND** en la primera página no se puede retroceder y en la última no se puede avanzar

#### Scenario: Nueve opciones juntas en dos columnas
Origen: decisión de David del 02/11/2026 («hasta 10 opciones, serán dos columnas de 5»; nueve son cinco y cuatro).
- **GIVEN** un snapshot activo con nueve opciones y un área de 1024 px de ancho
- **WHEN** se muestra la selección
- **THEN** la primera columna tiene las cinco primeras opciones en el orden del catálogo y la segunda, las cuatro restantes, sin ninguna opción de relleno
- **AND** no hay «Anterior» ni «Siguiente»

#### Scenario: Ocho en una columna y diez en dos de cinco
Origen: decisión de David del 02/11/2026 («hasta 8 opciones, una columna»; la regla de ocho se aplica antes que la de diez).
- **GIVEN** un snapshot activo con ocho opciones, y después otro con diez
- **WHEN** se muestra la selección de cada uno en un área de 1024 px de ancho
- **THEN** las ocho van en una sola columna y las diez, en dos columnas de cinco
- **AND** en ninguno de los dos hay «Anterior» ni «Siguiente»

### Requirement: Confirmación con un solo IntentId y la persona fija

Al tocar «Confirmar», el puesto SHALL generar un `IntentId` y confirmar con la confirmación atómica (RB06), sin red. Mientras confirma, el botón MUST quedar ocupado, un toque repetido MUST reutilizar el mismo `IntentId` y otra identificación MUST NOT cambiar la persona ni la opción (RB05, RB07). El resultado MUST mostrar el mensaje de su motivo y volver a reposo sin la persona.

#### Scenario: Autorización sin red
Origen: AT01 y HU01-A.
- **GIVEN** una persona habilitada con cupo libre, el puesto sin red y una opción elegida
- **WHEN** toca «Confirmar»
- **THEN** la pantalla muestra «Consumo registrado. Retirá tu comprobante»
- **AND** existe un solo consumo, de esa persona y esa opción, con su comprobante

#### Scenario: Doble toque
Origen: AT26 («una intención y persona original; sin segundo consumo») y HU01-G.
- **GIVEN** una persona que tocó «Confirmar» y la confirmación sigue en curso
- **WHEN** vuelve a tocar «Confirmar»
- **THEN** las dos confirmaciones usan el mismo `IntentId`
- **AND** existe una sola intención y un solo consumo

#### Scenario: Otra identificación durante la confirmación
Origen: AT26 («otra lectura durante confirmación») y HU01-G.
- **GIVEN** una persona cuya confirmación sigue en curso
- **WHEN** se envía el código de otra persona
- **THEN** la pantalla señala que el puesto está ocupado y no abre otra sesión
- **AND** el consumo registrado es de la persona original

#### Scenario: Cada rechazo con su mensaje
Origen: AT02 visto desde el puesto, y el catálogo de estados de `docs/experiencia.md`.
- **GIVEN** una confirmación que la unidad de RB06 deniega con `LOCAL_LIMIT_REACHED`, `SERVICE_NOT_ENABLED`, `RULE_DENIED`, `PERSON_INACTIVE`, `CREDENTIAL_REVOKED` o `CREDENTIAL_EXPIRED`
- **WHEN** termina la confirmación
- **THEN** la pantalla muestra el mensaje de ese motivo, distinto del de los demás, y no informa ningún consumo

#### Scenario: La sesión venció mientras elegía
Origen: AT35, segunda parte («una intención distinta con esa sesión vencida … no se confirma»), visto desde el puesto.
- **GIVEN** una persona que siguió tocando el catálogo hasta pasar el vencimiento de su sesión
- **WHEN** toca «Confirmar»
- **THEN** la pantalla pide identificarse de nuevo y vuelve a reposo, sin escribir nada

#### Scenario: Vuelta a reposo después del resultado
Origen: TK02.1, trabajo incluido 1 («resultado y regreso a reposo»), y HU03-E («sin … identidad de la persona anterior»).
- **GIVEN** la pantalla de resultado de una confirmación
- **WHEN** se toca «Listo» o pasan 10 s
- **THEN** el puesto vuelve a reposo sin el nombre ni la elección de esa persona

#### Scenario: Base de una versión de esquema desconocida
Origen: TK03.1, trabajo incluido 2, y «Disco lleno/base dañada» de `docs/experiencia.md`.
- **GIVEN** una base con una versión de esquema mayor que la última conocida
- **WHEN** arranca la atención
- **THEN** la pantalla informa un error de almacenamiento y no ofrece identificarse
- **AND** el archivo queda sin cambios

### Requirement: Reconfirmación por cambio de catálogo

Si al confirmar la opción elegida tiene otra versión en el snapshot activo, el puesto SHALL mostrar su nombre y su cargo vigentes y pedir reconfirmación, sin escribir nada (RB10). Reconfirmar MUST usar un `IntentId` nuevo con la versión vigente. Si la opción ya no está en el catálogo, MUST volver a la selección avisándolo.

#### Scenario: Cambio de precio entre la selección y la confirmación
Origen: AT47 («informa nuevo valor y exige reconfirmación antes de crear consumo/ticket») y HU01-D.
- **GIVEN** una persona que eligió una opción mostrada con un cargo de 1.500,00 y, antes de confirmar, la opción pasa a la versión 2 con un cargo de 1.800,00
- **WHEN** toca «Confirmar»
- **THEN** la pantalla muestra el cargo de 1.800,00 y pide reconfirmar
- **AND** no existe ninguna intención nueva

#### Scenario: Reconfirmar con el valor vigente
Origen: AT47 («consumos anteriores conservan su snapshot monetario»).
- **GIVEN** la pantalla de reconfirmación del escenario anterior
- **WHEN** la persona toca «Reconfirmar»
- **THEN** la decisión es AUTHORIZED con la versión 2 y el cargo de 1.800,00
- **AND** existe una sola intención, con un `IntentId` distinto del primer intento

#### Scenario: Volver sin reconfirmar
Origen: HU01-D («no cambia el importe silenciosamente»).
- **GIVEN** la pantalla de reconfirmación
- **WHEN** la persona toca «Volver»
- **THEN** el puesto vuelve a la selección y no se escribió ninguna intención

#### Scenario: Opción retirada del catálogo
Origen: AT47, borde: la opción no está en el snapshot activo.
- **GIVEN** una persona que eligió una opción que, antes de confirmar, ya no está en el snapshot activo
- **WHEN** toca «Confirmar»
- **THEN** el puesto vuelve a la selección con el aviso de que la opción ya no está disponible, sin ofrecerla
- **AND** no se escribió ninguna intención

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
- **GIVEN** una persona con un código sintético reconocible, límite 1 en ALMUERZO y 2 en REFRIGERIO, el catálogo de quince y un almuerzo ya consumido en el día
- **WHEN** se identifica
- **THEN** existe un registro `OPTIONS_OFFERED` con seis opciones ofrecidas, nueve retenidas con `LOCAL_LIMIT_REACHED` y la categoría de ALMUERZO con uso 1 y límite 1
- **AND** el registro no contiene el código

#### Scenario: Oferta vacía
Origen: AT41, borde: una oferta sin opciones también se reconstruye.
- **GIVEN** una persona activa sin ninguna regla
- **WHEN** se identifica
- **THEN** existe un registro `OPTIONS_OFFERED` sin opciones ofrecidas y con todas las del catálogo retenidas con `SERVICE_NOT_ENABLED`
