## ADDED Requirements

### Requirement: La tarjeta de una opción con su presentación

Cada opción del catálogo SHALL dibujarse con el fondo y la transparencia que le dio «Personalizar», con el nombre y el cargo legibles sobre él y alineados con los de las tarjetas hermanas. Una opción con el precio oculto MUST mostrarse como una sin cargo en la pantalla; el comprobante y la bitácora MUST conservar su importe.

#### Scenario: Una opción personalizada
Origen: mejora 7 de David del 10/11/2026.
- **GIVEN** «Almuerzo opcional» con un fondo de color y 25 % de transparencia
- **WHEN** `0001` se identifica
- **THEN** su tarjeta tiene ese fondo con esa transparencia y las demás se ven como siempre, con el nombre desde el mismo borde

#### Scenario: El precio oculto
Origen: mejora 16 de David del 11/11/2026.
- **GIVEN** «Almuerzo opcional» con 1.800,00 y el precio oculto
- **WHEN** `0001` lo elige y confirma
- **THEN** ni su tarjeta ni el cartel muestran un importe, y el comprobante dice el cargo de 1.800,00

#### Scenario: Una imagen que ya no está
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** una opción personalizada con una imagen cuyo archivo se borró de la carpeta de datos
- **WHEN** el puesto abre y `0001` se identifica
- **THEN** esa tarjeta se ve sin fondo y la atención sigue

## MODIFIED Requirements

### Requirement: Catálogo y selección sin consumo hasta confirmar

Con la sesión abierta, el puesto SHALL mostrar las opciones de menú que le ofrece a esa persona, con su nombre y su cargo informado, como botones operables por toque y por teclado (RF04); una opción con importe cero, o con el precio oculto por «Personalizar», MUST mostrarse sin importe. Cancelar, o 30 s sin actividad antes de confirmar, MUST volver a reposo sin escribir ninguna intención ni consumo (RB05), y la persona siguiente MUST empezar sin rastro de la anterior.

#### Scenario: Opción sin cargo
Origen: decisión de David del 02/11/2026 (subvención total: valor cero, ningún importe visible, confirmación habilitada).
- **GIVEN** un catálogo con una opción de importe cero
- **WHEN** la persona la elige
- **THEN** la opción y el cartel de la elección no muestran ningún importe
- **AND** el «Confirmar» del cartel queda habilitado

#### Scenario: Opciones y navegación alcanzables con el teclado
Origen: TK02.1, trabajo incluido 2 («operable por toque y teclado»).
- **GIVEN** el catálogo visible
- **WHEN** se revisan sus controles
- **THEN** cada opción, cada control de página cuando hay más de diez opciones y «Cancelar» son botones que el teclado alcanza
- **AND** con el cartel abierto, «Confirmar» y «Elegir otra» son botones, el foco está en «Confirmar» y el resto de la pantalla queda inerte

#### Scenario: Cancelar antes de confirmar
Origen: HU01-B.
- **GIVEN** una persona que eligió una opción, cerró el cartel con «Elegir otra» y no confirmó
- **WHEN** toca «Cancelar»
- **THEN** el puesto vuelve a reposo y no existe ninguna intención ni consumo nuevos

#### Scenario: Treinta segundos sin actividad
Origen: AT16 («timeout antes de confirmar … cero consumo; siguiente persona inicia limpia»).
- **GIVEN** una persona en la selección, con el cartel abierto, cuyo último toque fue hace exactamente 30 s
- **WHEN** el puesto revisa la inactividad
- **THEN** vuelve a reposo sin intención ni consumo nuevos
- **AND** cuando otra persona se identifica, la pantalla no muestra el nombre ni la elección de la anterior, ni el cartel

#### Scenario: Un toque reinicia el plazo
Origen: AT16, borde del plazo.
- **GIVEN** una persona en la selección que toca «Siguiente» a los 29 s de identificarse
- **WHEN** pasan 45 s desde que se identificó
- **THEN** sigue en la selección, y vuelve a reposo recién a los 30 s de su último toque

### Requirement: Reconfirmación por cambio de catálogo

Si al confirmar la opción elegida tiene otra versión en el snapshot activo, el puesto SHALL mostrar su nombre y su cargo vigentes, sin el cargo si su precio está oculto, y pedir reconfirmación, sin escribir nada (RB10). Reconfirmar MUST usar un `IntentId` nuevo con la versión vigente. Si la opción ya no está en el catálogo, MUST volver a la selección avisándolo.

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

#### Scenario: Cambio de precio con el precio oculto
Origen: mejora 16 de David del 11/11/2026 («más allá de lo que esté configurado») y HU01-D, borde.
- **GIVEN** una opción con el precio oculto que, entre la selección y la confirmación, cambia de importe
- **WHEN** la persona toca «Confirmar»
- **THEN** la pantalla de reconfirmación muestra su nombre sin ningún importe y pide reconfirmar, sin escribir nada

### Requirement: Cartel de la elección

Tocar una opción SHALL abrir un cartel que la repite con su cargo informado, ninguno si su precio está oculto, con «Confirmar» y «Elegir otra». Sin opción elegida, la selección MUST NOT tener «Confirmar». «Elegir otra» y Escape MUST cerrar el cartel sin escribir nada, con la misma persona y la marca en la opción elegida.

#### Scenario: Elegir abre el cartel con lo elegido
Origen: mejora 2 de David del 10/11/2026 («un cartel que aparezca recién una vez seleccionada la opción, y te refuerce qué seleccionaste») y TK02.1.
- **GIVEN** una persona en la selección, sin opción elegida, y una opción con un cargo de 1.500,00
- **WHEN** toca esa opción
- **THEN** se abre un cartel con el nombre de la opción, «Cargo informado: 1.500,00», «Confirmar» y «Elegir otra»
- **AND** antes del toque la pantalla no tenía «Confirmar», y no se escribió ninguna intención

#### Scenario: Elegir otra vuelve a la selección
Origen: mejora 2 de David del 10/11/2026 («si querés continuar o volver a seleccionar otra opción») y RB05.
- **GIVEN** el cartel abierto con una opción elegida
- **WHEN** la persona toca «Elegir otra» y, después, otra opción
- **THEN** el cartel se cierra con la misma persona y la marca en la primera opción, y al tocar la otra se abre con la otra
- **AND** no se escribió ninguna intención

#### Scenario: Escape en el cartel
Origen: mejora 2 de David del 10/11/2026 y AT16 (una atención no se interrumpe por fuera de sus acciones), borde.
- **GIVEN** el cartel abierto con una opción elegida
- **WHEN** se pulsa Escape
- **THEN** el cartel se cierra como con «Elegir otra», el puesto sigue abierto con la misma persona y la misma elección, y no se escribió ninguna intención
