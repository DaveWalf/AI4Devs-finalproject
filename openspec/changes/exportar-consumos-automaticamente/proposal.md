# Proposal

## Why

El 10/11/2026 David pidió (mejora 11): «una pestaña de Exportación de consumos, antes de la última, que escriba un
archivo por agregado en cada pedido o varios archivos, por ejemplo diarios, automática, en una carpeta y/o por SFTP, con
control de errores y recuperación automática ante fallas». Decidió que todo entra en la final con los envíos simulados
(decisión 11 del plan `AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`: «envío probado contra una carpeta o un servidor local simulado
y la integración real como pendiente escrita») y el gate dado (decisiones 12 y 13). Es el change 6 del §6b.

Hoy Consumos exporta a mano lo que se filtra, a un CSV o un Excel por pedido. No hay forma de que los consumos lleguen
solos a una carpeta compartida o a un servidor del cliente, ni de saber si la última entrega falló.

Origen: RF de exportación de consumos de la SDD, RB17 (lo confirmado no se pierde) y AT37 (conservación ante error);
ticket TK03.1.

## What Changes

- **La pestaña Exportación**, antes de General: activarla, cada cuántos minutos, si escribe un archivo que crece o uno
  por día, y los destinos: una carpeta y/o un servidor SFTP simulado.
- **La exportación automática**: cada tantos minutos, mientras el puesto está abierto, escribe en cada destino los
  consumos que todavía no le llegaron, con las columnas de la exportación de Consumos y sin ningún código; también con
  «Exportar ahora».
- **El control de errores y la recuperación**: cada destino lleva su marca, que avanza sólo después de escribir; si un
  destino falla, la pestaña dice cuándo y por qué, la bitácora lo registra, y la próxima pasada reintenta desde ahí, sin
  perder ni repetir consumos.
- **El servidor SFTP simulado**: una carpeta del puesto que hace de servidor, con un modo «Caído» para ver la falla y la
  recuperación, como la impresora simulada. La conexión real queda como pendiente escrita.

## Non-goals

- Un SFTP real, sus credenciales y su prueba de conexión: pendiente escrita, como pide la decisión 11.
- Exportar a Excel en forma automática: el archivo que crece se agrega sin reescribir, y eso es un CSV.
- Exportar otra cosa que consumos.

## Capabilities

### New Capabilities

- `exportacion-consumos`: la exportación automática de consumos a una carpeta y a un SFTP simulado, con sus errores y su
  recuperación.

### Modified Capabilities

- `configuracion-puesto`: la pestaña Exportación antes de General.

## Impact

- **Código**: `Infrastructure.Sqlite` (los consumos desde una marca, la línea CSV de la exportación) y `Kiosk` (la
  configuración, el estado, los destinos, el motor y su reloj, la pestaña). Sin migración.
- **Pruebas**: nuevas en `Infrastructure.Sqlite.Tests`, `Kiosk.Tests` y una E2E a 1024 × 768. Pruebas existentes que
  cambian (decisión 13): las que fijan las ocho pestañas y su orden, porque David pidió una más antes de General.
- **Documentos**: `readme.md` (la exportación automática) y la fila que corresponda de `docs/aplicacion-del-curso.md`.

## Gate (decisión 13 del plan: dado, con límites)

Opciones recomendadas, aplicadas sin esperar:

1. «Un archivo por agregado en cada pedido» es un archivo que crece: cada pasada le agrega los consumos nuevos;
   «varios archivos» es uno por fecha de negocio. Los dos en CSV, con las columnas de la exportación de Consumos.
2. Automática es cada N minutos (5 a 1.440, 15 por defecto) mientras el puesto está abierto, más «Exportar ahora».
   Apagada por defecto.
3. Cada destino lleva su marca, el último consumo que recibió: un destino caído no frena al otro, y al volver recibe
   todo lo que le faltó, en orden.
4. El SFTP simulado escribe en `sftp-simulado\<servidor>\<carpeta remota>` de la carpeta de datos, sin clave; la clave
   y la conexión real quedan como pendiente escrita en el readme.
5. Lo exportado lleva el nombre de la persona, como la exportación de Consumos, y nunca su código.
6. La configuración y las marcas son del equipo: no viajan en un resguardo.

Talla: XL. Si una prueba existente falla por otra razón, se para (cláusula del plan).
