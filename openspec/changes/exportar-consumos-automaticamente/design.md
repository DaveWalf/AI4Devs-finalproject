## Context

Consumos exporta a mano, a un CSV o un Excel por pedido, lo que se filtró (`ExportacionConsumos` con `EscritorPlanilla`),
sin ningún código. Cada consumo tiene una secuencia del puesto, `comconSecuenciaCliente`, que la confirmación asigna como
el máximo más uno dentro de la transacción de RB06: crece siempre y no se repite. La impresora simulada y su modo «Sin
papel» son el antecedente de un destino simulado que se puede hacer fallar. Las bitácoras del puesto corren sus pasadas
fuera del hilo de la pantalla.

## Goals / Non-Goals

**Goals:** que los consumos lleguen solos a una carpeta y a un SFTP simulado, cada uno a su ritmo, sin perder ni repetir
uno ante una falla, con su estado a la vista y en la bitácora.

**Non-Goals:** un SFTP real, la exportación automática a Excel, exportar otra cosa que consumos.

## Decisions

### D1. Qué se exporta

`ConsultaConsumos.Desde(secuencia, tope)` devuelve los consumos con secuencia mayor, en orden de secuencia, hasta 10.000
por pasada, con la persona y la opción como eran al consumir. Cada fila lleva las columnas de la exportación de Consumos
(fecha y hora, número del comprobante, persona, opción, servicio, cargo, moneda y estado del comprobante) y ninguna más;
nunca el código ni la credencial. La línea CSV la arma `EscritorPlanilla`, con el apóstrofo de las fórmulas.

### D2. Los archivos

«Un archivo que crece»: `INComedor-consumos.csv`, al que cada pasada le agrega los consumos nuevos. «Un archivo por día»:
`INComedor-consumos-AAAAMMDD.csv` por fecha de negocio. El encabezado va sólo al crear el archivo. Escribir es copiar lo
que había a un temporal, agregar y reemplazar: un corte deja el archivo de antes o el de después, nunca uno a medias.

- **Descartada: abrir el archivo para agregar.** Un corte a mitad de una línea dejaría una línea rota, y la pasada
  siguiente agregaría otra vez lo mismo.

### D3. Los destinos

Una carpeta absoluta, elegida con el diálogo de carpetas, y un servidor SFTP simulado (servidor, puerto 1 a 65535,
usuario y carpeta remota), cada uno activable por su lado. El simulado escribe en `sftp-simulado\<servidor>\<carpeta
remota>` de la carpeta de datos, con la misma escritura de D2, y tiene un modo «Caído» que falla como un servidor que no
responde. Los nombres del servidor y de la carpeta remota se validan (letras, dígitos, punto, guion y barras; nunca `..`).
La conexión real, su clave y su prueba quedan como pendiente escrita.

### D4. La pasada automática

`ExportacionAutomatica.Exportar()` hace una pasada: por cada destino activo, los consumos desde su marca, los archivos y
la marca nueva. Un reloj la corre cada N minutos (5 a 1.440, 15 por defecto) mientras el puesto está abierto y la
exportación está activa, fuera del hilo de la pantalla y de a una por vez; «Exportar ahora» corre una en el acto. Sólo
lee la base.

### D5. Errores y recuperación

Cada destino guarda en `exportacion-estado.json` su marca (la última secuencia que recibió), el último intento, el último
éxito y el último error. La marca avanza recién después de escribir: si un destino falla, conserva su marca y la pasada
siguiente reintenta desde ahí; un destino caído no frena al otro. La bitácora general registra `CONSUMPTIONS_AUTO_EXPORTED`
(destino, archivos, filas y la secuencia hasta la que llegó) y `CONSUMPTIONS_AUTO_EXPORT_FAILED` (destino y motivo), sin
nombres ni códigos. Una pasada sin consumos nuevos no registra nada.

### D6. La pestaña

«Exportación», antes de General y sólo con la clave del día: «Activa» o «Apagada», cada cuántos minutos, «Un archivo que
crece» o «Un archivo por día», la carpeta con «Elegir carpeta», el SFTP simulado con sus datos y su modo, «Guardar» (que
valida y deja `CONFIGURATION_CHANGED` del ajuste `CONSUMPTION_EXPORT`), el estado de cada destino y «Exportar ahora».
`exportacion-automatica.json` guarda la configuración; las dos son del equipo y no viajan en un resguardo.

### D7. Prolijidad

Los campos, los botones de elección y «Guardar» con el alto táctil, la letra y la columna de rótulos de Impresora; la
fila de pestañas sigue entrando en 1024 × 768. Medido por CDP, con capturas.

## Risks / Trade-offs

- [Reescribir el archivo que crece en cada pasada] → con un año de consumos son unos pocos MB; el archivo por día no
  crece.
- [El nombre de la persona fuera del puesto] → como la exportación de Consumos; el código nunca.
- [Una marca perdida] → si se borra `exportacion-estado.json`, el destino recibe todo otra vez desde el principio:
  repetir es preferible a perder.

## Migration Plan

Sin migración. La reversión es revertir el PR.

## Open Questions

Ninguna.
