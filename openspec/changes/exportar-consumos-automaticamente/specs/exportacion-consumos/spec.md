## ADDED Requirements

### Requirement: La exportación automática de consumos

Con la exportación activa, el puesto SHALL escribir cada N minutos, de 5 a 1.440, y con «Exportar ahora», en cada destino activo los consumos que todavía no le llegaron, en orden, con las columnas de la exportación de Consumos y sin ningún código: en un archivo que crece o en uno por fecha de negocio. Cada escritura MUST dejar el archivo de antes o el completo, nunca uno a medias.

#### Scenario: Un archivo que crece
Origen: mejora 11 de David del 10/11/2026 («un archivo por agregado en cada pedido»).
- **GIVEN** la exportación activa a una carpeta, con «Un archivo que crece», y dos consumos
- **WHEN** corre una pasada, se confirma un consumo más y corre otra
- **THEN** la carpeta tiene `INComedor-consumos.csv` con el encabezado una vez y los tres consumos en orden, sin el código de nadie

#### Scenario: Un archivo por día
Origen: mejora 11 de David del 10/11/2026 («varios archivos, por ejemplo diarios»).
- **GIVEN** la exportación activa con «Un archivo por día» y consumos de dos fechas de negocio
- **WHEN** corre una pasada
- **THEN** la carpeta tiene un archivo `INComedor-consumos-AAAAMMDD.csv` por fecha, cada uno con los consumos de su día

#### Scenario: Sin consumos nuevos
Origen: D5 de este change, borde.
- **GIVEN** un destino que ya recibió todos los consumos
- **WHEN** corre otra pasada
- **THEN** el archivo no cambia y la bitácora no registra nada

### Requirement: Los errores de la exportación y su recuperación

Cada destino SHALL llevar su marca, el último consumo que recibió, y MUST avanzarla sólo después de escribir. Si un destino falla, MUST conservar su marca, mostrar cuándo y por qué en la pestaña, registrar `CONSUMPTIONS_EXPORT_FAILED` y reintentar en la pasada siguiente desde su marca, sin frenar a los otros destinos. Lo exportado MUST NOT perderse ni repetirse.

#### Scenario: El servidor cae y vuelve
Origen: mejora 11 de David del 10/11/2026 («control de errores y recuperación automática ante fallas»).
- **GIVEN** la exportación a una carpeta y al SFTP simulado, con el SFTP en «Caído»
- **WHEN** corre una pasada, el SFTP vuelve a «Disponible» y corre otra
- **THEN** después de la primera la carpeta tiene los consumos, el SFTP no y la pestaña dice su falla; después de la segunda el SFTP tiene los mismos consumos, una sola vez

#### Scenario: La bitácora de una pasada
Origen: RB21 («todo cambio … tiene permiso, actor y resultado») y D5 de este change.
- **GIVEN** una pasada con un destino que escribe y otro que falla
- **WHEN** se lee la general
- **THEN** tiene `CONSUMPTIONS_EXPORTED` con el destino, los archivos, las filas y la secuencia, y `CONSUMPTIONS_EXPORT_FAILED` con el otro destino y su motivo, sin nombres ni códigos

### Requirement: La configuración de la exportación

La pestaña Exportación SHALL ofrecer activarla, el intervalo, el tipo de archivo, una carpeta absoluta y un SFTP simulado (servidor, puerto, usuario y carpeta remota, sin `..`), con su «Guardar», que MUST rechazar lo que no tiene forma con su campo señalado y registrar `CONFIGURATION_CHANGED` de `CONSUMPTION_EXPORT`. El SFTP simulado MUST escribir en una carpeta del puesto y ofrecer «Disponible» y «Caído». La conexión real queda pendiente.

#### Scenario: Guardar la exportación
Origen: mejora 11 de David del 10/11/2026 («en una carpeta y/o por SFTP») y decisión 11 del plan (envíos simulados).
- **GIVEN** la pestaña Exportación
- **WHEN** se activa con 15 minutos, «Un archivo por día», una carpeta y el SFTP simulado `sftp.ejemplo.local`, y se toca «Guardar»
- **THEN** la pestaña avisa lo guardado y la general tiene `CONFIGURATION_CHANGED` de `CONSUMPTION_EXPORT` con el antes y el después

#### Scenario: Datos fuera de forma
Origen: `AGENTS.md` (OWASP: la configuración que llega al puesto), borde.
- **GIVEN** la pestaña Exportación
- **WHEN** se intenta guardar 2 minutos, una carpeta relativa y una carpeta remota con `..`
- **THEN** cada campo se señala y la configuración no cambia
