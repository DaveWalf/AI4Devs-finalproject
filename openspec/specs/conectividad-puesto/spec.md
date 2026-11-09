# conectividad-puesto Specification

## Purpose
Mostrar los datos de red del equipo, editar la IPv4 de un adaptador con `netsh` y el permiso de administrador que Windows pide en cada paso, y probar por etapas la conexión con el receptor central para saber dónde falla, sin enviar ningún lote ni tocar un consumo.

## Requirements

### Requirement: Los datos de red del equipo

La pestaña Red SHALL mostrar el nombre del equipo y, por cada adaptador de red activo, su nombre, si toma la IPv4 por DHCP, sus direcciones IPv4 con su máscara, su puerta de enlace y sus servidores DNS.

#### Scenario: Un equipo con un adaptador activo
Origen: decisión de David del 03/11/2026 («parámetros de red propios del dispositivo»).
- **GIVEN** un equipo con un adaptador activo, con la IPv4 `192.168.10.25/24`, la puerta `192.168.10.1` y un DNS
- **WHEN** se abre la pestaña Red
- **THEN** muestra el nombre del equipo y ese adaptador con su IPv4, su máscara, su puerta de enlace, su DNS y si usa DHCP

#### Scenario: Sin adaptadores activos
Origen: HU03-D («API caída … y periférico desconectado: distingue estados»), borde.
- **GIVEN** un equipo sin adaptadores de red activos
- **WHEN** se abre la pestaña Red
- **THEN** dice «Sin conexión de red» y la atención sigue

### Requirement: Editar la IPv4 de un adaptador

La pestaña Red SHALL permitir, en un adaptador elegido de la lista del equipo y nunca tipeado, fijar su IPv4 (dirección, máscara, puerta y hasta dos DNS) o volver a DHCP. Cada valor MUST ser una IPv4 válida y los comandos de `netsh` MUST armarse sólo con valores validados. Aplicar MUST pedir antes una confirmación que avise que la conexión puede cortarse. El puesto MUST NOT correr elevado: cada `netsh` pide su permiso.

#### Scenario: IPv4 fija con confirmación
Origen: decisión de David del 06/11/2026 (gate de D, punto 8: «Red: editar la IPv4 con `netsh`») y su pedido del 03/11/2026 («parámetros de red propios del dispositivo»).
- **GIVEN** la pestaña Red y un adaptador activo elegido de la lista
- **WHEN** se cargan la IPv4 `192.168.10.30`, la máscara `255.255.255.0`, la puerta `192.168.10.1` y el DNS `192.168.10.2`, y se toca «Aplicar»
- **THEN** la pestaña pide confirmar y avisa que el cambio puede cortar la conexión del puesto
- **AND** hasta confirmar no se ejecutó ningún comando

#### Scenario: Los comandos de una IPv4 fija
Origen: decisión de David del 06/11/2026 (gate de D, punto 8: los argumentos se arman sólo con valores validados) y `AGENTS.md` (OWASP: lo que se tipea en el puesto).
- **GIVEN** el adaptador de índice 12 y los valores del escenario anterior
- **WHEN** se arman los comandos
- **THEN** son `interface ipv4 set address name=12 source=static address=192.168.10.30 mask=255.255.255.0 gateway=192.168.10.1` y `interface ipv4 set dnsservers name=12 source=static address=192.168.10.2 register=primary validate=no`, cada uno con su permiso de administrador

#### Scenario: Volver a DHCP
Origen: decisión de David del 06/11/2026 (gate de D, punto 8: «IPv4 fija … o DHCP»).
- **GIVEN** el adaptador de índice 12
- **WHEN** se elige DHCP y se arman los comandos
- **THEN** son `interface ipv4 set address name=12 source=dhcp` y `interface ipv4 set dnsservers name=12 source=dhcp`

#### Scenario: Un valor que no es una IPv4
Origen: `AGENTS.md` (OWASP: lo que se tipea en el puesto) y la decisión de David del 06/11/2026 (gate de D, punto 8: cada valor con `IPAddress.TryParse`), borde.
- **GIVEN** la pestaña Red con un adaptador elegido
- **WHEN** se intenta aplicar la IPv4 `192.168.10.300`, después `1`, después `192.168.10.30 & del`, y después la máscara `255.0.255.0`
- **THEN** cada vez se señala el campo, no se pide confirmación y no se arma ningún comando

#### Scenario: Cancelar la confirmación
Origen: decisión de David del 06/11/2026 (gate de D, punto 8: «confirmación que avisa que puede cortar la conexión»), borde.
- **GIVEN** la confirmación de una IPv4 fija
- **WHEN** se toca «Cancelar»
- **THEN** no se ejecuta ningún comando, no queda ningún registro de red y los valores cargados siguen en la pestaña

#### Scenario: El permiso de Windows rechazado
Origen: decisión de David del 06/11/2026 (gate de D, punto 8: sólo `netsh` pide elevación, en cada aplicación), borde.
- **GIVEN** la confirmación aceptada
- **WHEN** se rechaza el pedido de permiso de administrador del primer comando
- **THEN** la pestaña dice que la red no cambió y no se ejecuta el comando siguiente

### Requirement: Cada aplicación de red queda en la bitácora general

Cada aplicación confirmada SHALL quedar en la bitácora general como `CONFIGURATION_CHANGED` con el ajuste `NETWORK_IPV4`, el adaptador, la IPv4 de antes y la pedida, el resultado (aplicada, cancelada en un permiso o fallida) con el paso en que se cortó y su código, el actor nulo con `NO_AUTHENTICATION` y cómo se entró a Configuración.

#### Scenario: Una aplicación completa
Origen: RB21 («todo cambio/prueba tiene permiso, actor y resultado») y RF41 («registrar ejecución/resultado»).
- **GIVEN** una IPv4 fija confirmada con sus dos comandos terminados en 0
- **WHEN** termina la aplicación
- **THEN** la general tiene `CONFIGURATION_CHANGED` con `NETWORK_IPV4`, el adaptador, la IPv4 de antes y la pedida, el resultado aplicado con sus dos pasos, el actor nulo, `NO_AUTHENTICATION` y `MAINTENANCE_MODE`

#### Scenario: Un permiso rechazado
Origen: decisión de David del 06/11/2026 (gate de D, punto 8: «cada aplicación a la bitácora con su resultado»), borde.
- **GIVEN** una IPv4 fija confirmada
- **WHEN** se rechaza el permiso de administrador del primer comando
- **THEN** la general tiene la aplicación cancelada en el primer paso, sin pasos aplicados

#### Scenario: Un comando que falla
Origen: AT38 («resultado por componente»), borde.
- **GIVEN** una IPv4 fija confirmada
- **WHEN** el primer comando de `netsh` termina con un código distinto de cero
- **THEN** no se ejecuta el siguiente, la pestaña dice en qué paso falló y la general tiene la aplicación fallida con ese paso y su código

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
