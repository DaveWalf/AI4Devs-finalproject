# Spec Delta

## Purpose

Mostrar, sin cambiar nada del sistema operativo, los datos de red del equipo, y probar por etapas la conexión con el receptor central para saber dónde falla, sin enviar ningún lote ni tocar un consumo.

## ADDED Requirements

### Requirement: Los datos de red del equipo, sólo para leer

La pestaña Red SHALL mostrar el nombre del equipo y, por cada adaptador de red activo, su nombre, sus direcciones IPv4 con su máscara, su puerta de enlace y sus servidores DNS. MUST NOT ofrecer cambiar ninguno de esos datos ni tocar la configuración de red del sistema operativo.

#### Scenario: Un equipo con un adaptador activo
Origen: decisión de David del 03/11/2026 («parámetros de red propios del dispositivo»).
- **GIVEN** un equipo con un adaptador activo, con la IPv4 `192.168.10.25/24`, la puerta `192.168.10.1` y un DNS
- **WHEN** se abre la pestaña Red
- **THEN** muestra el nombre del equipo y ese adaptador con su IPv4, su máscara, su puerta de enlace y su DNS

#### Scenario: Sin adaptadores activos
Origen: HU03-D («API caída … y periférico desconectado: distingue estados»), borde.
- **GIVEN** un equipo sin adaptadores de red activos
- **WHEN** se abre la pestaña Red
- **THEN** dice «Sin conexión de red» y la atención sigue

#### Scenario: Nada se edita
Origen: nota de pantalla y configuración, punto 3 («registrar esta capacidad no cambia interfaces, rutas, DNS ni conectividad real»), borde.
- **GIVEN** la pestaña Red abierta
- **WHEN** se revisan sus controles
- **THEN** no tiene campos editables ni «Guardar»

### Requirement: Prueba de conexión con el receptor

«Probar conexión» SHALL probar la conexión con el receptor central configurado en cuatro etapas, nombre, conexión, TLS mutuo con los certificados del puesto y respuesta del receptor, y mostrar el resultado de cada una y en cuál falló, sin enviar ningún lote. Sin los certificados del puesto MUST decirlo sin probar. Cada prueba MUST quedar en la bitácora general con sus etapas, la dirección y el resultado, sin certificados ni claves.

#### Scenario: El receptor de demostración responde
Origen: decisión de David del 03/11/2026 («parámetros de conectividad con el INWeb con un botón de prueba»), RF41 y HU03-G.
- **GIVEN** el receptor de demostración en marcha y los certificados del puesto en su carpeta
- **WHEN** se toca «Probar conexión»
- **THEN** las cuatro etapas dicen que funcionaron y la pestaña muestra la dirección probada

#### Scenario: Puerto cerrado
Origen: AT38 («errores inyectados … resultado por componente»), borde.
- **GIVEN** una dirección del receptor cuyo puerto no escucha
- **WHEN** se toca «Probar conexión»
- **THEN** la etapa del nombre funcionó, la de la conexión falló con su motivo y las dos siguientes no se probaron

#### Scenario: Un nombre que no se resuelve
Origen: AT38 («errores inyectados»), borde.
- **GIVEN** una dirección del receptor con un nombre que no existe
- **WHEN** se toca «Probar conexión»
- **THEN** la etapa del nombre falló y las demás no se probaron

#### Scenario: Sin certificados
Origen: D12 de `recibir-lotes-idempotente` (sin certificados, el puesto atiende sin receptor), borde.
- **GIVEN** una carpeta de datos sin los certificados del puesto
- **WHEN** se abre la pestaña Red
- **THEN** dice que el puesto no tiene certificados y atiende sin receptor, y «Probar conexión» no prueba nada

#### Scenario: La prueba no envía lotes
Origen: AT38 («sin consumo ni secretos») y RNF14 («diagnóstico … sin modificar efectos económicos»), borde.
- **GIVEN** un evento pendiente en la cola de salida
- **WHEN** se prueba la conexión con el receptor de demostración
- **THEN** el evento sigue pendiente y el receptor no recibió ningún lote

#### Scenario: La prueba en la bitácora
Origen: RB21 («todo cambio/prueba tiene permiso, actor y resultado») y RF41 («registrar ejecución/resultado»).
- **GIVEN** el receptor de demostración en marcha
- **WHEN** se prueba la conexión
- **THEN** la general tiene un registro de la prueba con la dirección, las cuatro etapas con su resultado y el actor nulo con `NO_AUTHENTICATION`, sin certificados
