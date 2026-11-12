## ADDED Requirements

### Requirement: El paquete lleva su versión

El EXE de un paquete SHALL llevar como versión visible el tag con que se armó, ya validado, y la fecha y hora en que se compiló. El tag MUST NOT entrar a MSBuild como la propiedad `Version`, que rompe el restore con un tag como «v0.0.0-ensayo». La prueba de punta a punta del paquete MUST comprobar que la cabecera del EXE muestra ese tag antes de escribir el zip.

#### Scenario: El tag en la cabecera del EXE
Origen: respuesta 3 de David al gate del 11/11/2026 (el tag del Release llega al EXE) y el requisito «Paquete probado antes de ofrecerse».
- **GIVEN** el paquete armado con el tag `v1.0.1`
- **WHEN** la prueba de punta a punta abre el EXE publicado
- **THEN** la cabecera dice «v1.0.1» y el commit corto del código empaquetado, y recién entonces se escribe el zip

#### Scenario: Un tag que rompía el restore
Origen: las corridas 41132357042, 41139494001 y 41140418958 («v0.0.0-ensayo» como `Version` falló con MSB4181), borde.
- **GIVEN** el tag `v0.0.0-ensayo`
- **WHEN** se arma el paquete
- **THEN** el restore y el build terminan sin error, y la cabecera del EXE dice «v0.0.0-ensayo»

#### Scenario: La cabecera no muestra el tag
Origen: el requisito «Paquete probado antes de ofrecerse» («si la prueba falla, no queda ningún Release»), borde.
- **GIVEN** un EXE publicado cuya cabecera dice «desarrollo» aunque el paquete se armó con un tag
- **WHEN** corre la prueba de punta a punta del paquete
- **THEN** el armado termina con error y no deja ningún zip
