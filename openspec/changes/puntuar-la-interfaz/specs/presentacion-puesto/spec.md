## ADDED Requirements

### Requirement: Las oraciones de la pantalla terminan en punto

Toda oración que la pantalla del puesto muestra (un aviso, un mensaje, una explicación o una indicación) SHALL terminar en punto. Un título, un rótulo, un botón, una opción o un estado MUST NOT llevarlo. Una spec que cita un mensaje entre comillas latinas deja el punto final fuera de ellas, como pide la RAE.

#### Scenario: Un aviso de la atención
Origen: pedido de David del 10/11/2026 («no dejar oraciones sin el punto final donde corresponda en la interfaz»).
- **GIVEN** una credencial vencida
- **WHEN** se ingresa su código
- **THEN** la pantalla dice «Esta credencial está vencida. Solicitá asistencia», con el punto final después de «asistencia»

#### Scenario: Un título y un estado no llevan punto
Origen: pedido de David del 10/11/2026 («donde corresponda»), borde.
- **GIVEN** el puesto en reposo y la pestaña Red sin red
- **WHEN** se muestran
- **THEN** el título «¡Hola! Identificate y disfrutá tu comida» y el estado «Sin conexión de red» no terminan en punto

#### Scenario: Un mensaje nuevo sin punto
Origen: `openspec/config.yaml` (la regla de puntuación de la interfaz), borde.
- **GIVEN** los mensajes del puesto
- **WHEN** se agrega uno que es una oración sin el punto final
- **THEN** la prueba que los recorre falla y lo nombra
