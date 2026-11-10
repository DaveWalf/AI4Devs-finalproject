## MODIFIED Requirements

### Requirement: Catálogo y selección sin consumo hasta confirmar

Con la sesión abierta, el puesto SHALL mostrar las opciones de menú que le ofrece a esa persona, con su nombre y su cargo informado, como botones operables por toque y por teclado (RF04); una opción con importe cero MUST mostrarse sin importe. Cancelar, o 30 s sin actividad antes de confirmar, MUST volver a reposo sin escribir ninguna intención ni consumo (RB05), y la persona siguiente MUST empezar sin rastro de la anterior.

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

## ADDED Requirements

### Requirement: Cartel de la elección

Tocar una opción SHALL abrir un cartel que la repite con su cargo informado, con «Confirmar» y «Elegir otra». Sin opción elegida, la selección MUST NOT tener «Confirmar». «Elegir otra» y Escape MUST cerrar el cartel sin escribir nada, con la misma persona y la marca en la opción elegida.

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
