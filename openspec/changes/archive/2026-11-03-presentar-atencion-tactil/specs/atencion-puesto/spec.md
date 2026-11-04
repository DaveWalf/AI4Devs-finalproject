# Spec Delta

## MODIFIED Requirements

### Requirement: Catálogo y selección sin consumo hasta confirmar

Con la sesión abierta, el puesto SHALL mostrar las opciones de menú del snapshot activo con su nombre y su cargo informado, como botones operables por toque y por teclado (RF04); una opción con importe cero MUST mostrarse sin importe. Hasta diez opciones MUST mostrarse juntas, sin controles de página: hasta ocho en una columna, y nueve o diez en dos columnas de hasta cinco, completando la primera antes de la segunda, cuando el ancho lo admite. Desde once MUST mostrarse en páginas de diez, con «Anterior» y «Siguiente», y cada página MUST distribuir sus opciones con la misma regla. Cancelar, o 30 s sin actividad antes de confirmar, MUST volver a reposo sin escribir ninguna intención ni consumo (RB05), y la persona siguiente MUST empezar sin rastro de la anterior.

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
