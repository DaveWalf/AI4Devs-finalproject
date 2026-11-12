## ADDED Requirements

### Requirement: Un operador que se identifica en reposo

Si quien se identifica en reposo, con su código o en el combo de la simulación, tiene un permiso `OPERATOR` vigente (RB26), la selección SHALL ofrecer «Configuración» junto a «Cancelar», y la pantalla sin opciones, junto a «Listo». Tocarlo MUST volver a comprobar el permiso, terminar la atención sin consumo y abrir Configuración como su código en el engranaje: Consumos, Personas e Impresora. Sin ese permiso MUST NOT mostrarse.

#### Scenario: Un operador con comidas
Origen: mejora 5 de David del 11/11/2026 («Un operador debe poder comer si así lo tiene asignado y, además, debe poder hacer los otros pasos de configuración») y su respuesta 5 al gate.
- **GIVEN** una persona con un permiso `OPERATOR` vigente y un cupo de ALMUERZO, en reposo
- **WHEN** tipea su código y toca «Ingresar»
- **THEN** la selección muestra sus opciones de ALMUERZO, «Cancelar» y «Configuración»
- **AND** al tocar una opción y «Confirmar», el consumo se registra como el de cualquier comensal

#### Scenario: Un operador sin comidas
Origen: respuesta 6 de David al gate del 11/11/2026 («El aviso con "Configuración"»), borde: hoy ve sólo el aviso.
- **GIVEN** el puesto en reposo
- **WHEN** se tipea `9001`, que no tiene ningún cupo, y se toca «Ingresar»
- **THEN** la pantalla dice «No tenés opciones disponibles ahora.» con «Listo» y «Configuración»
- **AND** al tocar «Configuración», se abre con Consumos, Personas e Impresora, con Consumos elegida y sin General

#### Scenario: Desde el combo de la simulación
Origen: mejora 5 («seleccionar del combo o tipiear su número debería remitir a la misma funcionalidad») y la respuesta 8 de David al gate («Evitemos solicitar durante esta etapa de desarrollos validaciones extras»).
- **GIVEN** el puesto en mantenimiento, con la simulación encendida
- **WHEN** se elige en el combo a la persona de `9001` y se toca «Configuración»
- **THEN** Configuración se abre con Consumos, Personas e Impresora, sin pedir ninguna clave ni código

#### Scenario: Una persona sin permiso de operador
Origen: RB26 («roles sin herencia») y AT54, borde.
- **GIVEN** el puesto en reposo
- **WHEN** se tipea `0001` y, otra vez, `9004` (administrador sin `OPERATOR`), y se toca «Ingresar»
- **THEN** ninguna de las dos pantallas tiene «Configuración»

#### Scenario: Un permiso vencido
Origen: RB26 y la tabla de códigos sintéticos del `readme.md` (`9002`, «Operador con el permiso vencido»), borde.
- **GIVEN** el puesto en reposo
- **WHEN** se tipea `9002` y se toca «Ingresar»
- **THEN** la pantalla no tiene «Configuración»

#### Scenario: El permiso cae antes de tocar «Configuración»
Origen: RB26 («comprobar el permiso en cada acción») y AT54 («siguiente acción rechazada»), borde.
- **GIVEN** un operador en la selección, cuyo permiso `OPERATOR` vence después de identificarse
- **WHEN** toca «Configuración»
- **THEN** Configuración sigue cerrada, la pantalla avisa que su permiso está vencido y la general tiene la denegación con `PERMISSION_EXPIRED`

#### Scenario: Configuración no deja consumo
Origen: RB06 (la confirmación es la única que registra) y HU03-E («vuelve a reposo sin privilegios ni identidad de la persona anterior»), borde.
- **GIVEN** un operador con comidas en la selección, sin elegir ninguna
- **WHEN** toca «Configuración» y, después, cierra Configuración
- **THEN** no hay ninguna intención ni consumo nuevos, su sesión de consumo ya no es utilizable y el puesto queda en reposo, sin su nombre
