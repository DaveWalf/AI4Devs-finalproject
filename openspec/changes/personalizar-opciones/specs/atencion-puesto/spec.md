## ADDED Requirements

### Requirement: La tarjeta de una opción con su presentación

Cada opción del catálogo SHALL dibujarse con el fondo y la transparencia que le dio «Personalizar», con el nombre y el cargo legibles sobre él. Una opción con el precio oculto MUST mostrarse como una sin cargo en la tarjeta y en el cartel de la elección; el comprobante y la bitácora MUST conservar su importe.

#### Scenario: Una opción personalizada
Origen: mejora 7 de David del 10/11/2026.
- **GIVEN** «Almuerzo opcional» con un fondo de color y 25 % de transparencia
- **WHEN** `0001` se identifica
- **THEN** su tarjeta tiene ese fondo con esa transparencia y las demás se ven como siempre

#### Scenario: El precio oculto
Origen: mejora 16 de David del 11/11/2026.
- **GIVEN** «Almuerzo opcional» con 1.800,00 y el precio oculto
- **WHEN** `0001` lo elige y confirma
- **THEN** ni su tarjeta ni el cartel muestran un importe, y el comprobante dice el cargo de 1.800,00
