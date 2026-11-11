## Context

La configuración de un puesto está en dos lugares. En la base, la versión activa: las diez tablas de
`AdministracionLocal.TablasDeConfiguracion` (personas, credenciales, servicios, ámbitos, categorías, opciones, cargas,
reglas, permisos y franjas), que `HashDeContenido` ya sabe serializar tabla por tabla, y que cada guardado copia a una
versión nueva y activa con `ActivarVersion`. En la carpeta de datos, los ajustes locales: `estilo-visual.json`,
`impresora.json` (los textos del comprobante), `presentacion-opciones.json` con `opciones\`, y los logos de `marcas\`;
cada uno con su almacén, que valida al leer. El resto de la carpeta es del equipo o de su estado (`pantalla.json`,
`modo-puesto.json`, `simulacion.json`, `exportacion.json`, `certificados\`, `comprobantes\`).

## Goals / Non-Goals

**Goals:** guardar la configuración entera en un archivo; recuperarla en este u otro puesto con el mismo esquema, con un
resumen antes y todo o nada; la bitácora.

**Non-Goals:** lo del equipo o su estado; recuperar con INWeb como dueño; otro esquema; resguardos automáticos.

## Decisions

### D1. El archivo

`INComedor-resguardo-AAAAMMDD-HHmmss.zip` en la carpeta de la exportación, con `manifiesto.json` (formato 1, esquema de la
base, versión de configuración, fecha y, por cada parte, su nombre, su tamaño y su SHA-256), `configuracion.json` (la
versión activa tabla por tabla, con las columnas de `TablasDeConfiguracion`) y los archivos locales que existan con su
nombre de siempre (`estilo-visual.json`, `impresora.json`, `presentacion-opciones.json`, `marcas\…`, `opciones\…`).

- **Descartada: copiar la base entera.** Llevaría los consumos, las bitácoras y las versiones anteriores a otro puesto, y
  el resguardo dejaría de ser «la configuración».

### D2. Revisar antes de recuperar

`ResguardoPuesto.Revisar` lee el `.zip` sin escribir nada y lo rechaza con un motivo si: no es un zip o pesa más de
50 MB; trae una parte que no es del resguardo (sólo esos nombres; ninguna ruta con `..`); una parte pesa más de 20 MB
descomprimida o no tiene la huella del manifiesto; el formato o el esquema no son los de este puesto; `configuracion.json`
no tiene las diez tablas con sus columnas; un código del resguardo es, en este puesto, de otra persona (RB25); o un
archivo local no pasa la validación de su almacén. El resumen dice la versión y la fecha de origen, cuántas personas,
opciones y permisos trae, y qué ajustes locales trae.

- **Descartada: recuperar lo que se pueda.** Una configuración a medias (las personas sí, sus cupos no) es peor que la
  de antes; el resumen dice por qué no se recupera nada.

### D3. Recuperar

`AdministracionLocal.RecuperarVersion` crea una versión nueva con las filas del resguardo, en una transacción
`BEGIN IMMEDIATE`, y la activa como cualquier guardado (los números de padrón, políticas y catálogo suben, y el hash se
recalcula); un error de la base deshace todo. Después, los archivos locales del resguardo reemplazan a los del puesto,
cada uno con escritura atómica; un logo o una presentación que el resguardo no trae vuelve al de ejemplo o a ninguna.
La pantalla vuelve a leer el estilo, los logos, los textos y la presentación.

- **Descartada: escribir los archivos antes que la base.** Si la base rechaza el resguardo, los archivos ya estarían
  cambiados; la base es lo que puede fallar por su contenido.

### D4. La pantalla

General, al final de la pestaña, tiene «Guardar resguardo» y «Recuperar resguardo», éste sólo sin INWeb como dueño. El
resumen va en un cartel como el de las importaciones, con «Recuperar» y «Cancelar» (destacado y con el foco, porque
recuperar reemplaza todo), o con el motivo del rechazo y «Cerrar». El archivo se elige con el diálogo de archivos, con su
título y su filtro (`.zip`).

- **Descartada: una pestaña propia.** La mejora la pone en General, que ya está al final.

### D5. La bitácora

`CONFIGURATION_BACKUP_CREATED` con el nombre del archivo que genera el puesto, su SHA-256 y la versión; al recuperar,
`CONFIGURATION_RESTORED` con la huella del archivo, la versión de origen, la nueva y qué partes se recuperaron; un
rechazo, `CONFIGURATION_RESTORE_REJECTED` con el motivo. Ninguno lleva códigos, nombres ni el contenido.

- **Descartada: un `CONFIGURATION_CHANGED` por persona y opción recuperadas.** Recuperar no edita entidades: reemplaza la
  configuración entera, y un registro con su huella la identifica.

### D6. Prolijidad

Los dos botones de General miden lo mismo, con el alto y la letra de «Cerrar»; los del cartel, el alto táctil, la misma
letra y la misma altura. Medido por CDP a 1024 × 768, con capturas.

- **Descartada: medir sólo con bUnit.** bUnit no dibuja.

## Risks / Trade-offs

- [El resguardo lleva los códigos] → el archivo es un dato de la empresa; el readme lo dice y ni el resumen ni la bitácora
  los muestran.
- [Un resguardo grande por las imágenes] → los topes de D2.

## Migration Plan

Sin migración. La reversión es revertir el PR.

## Open Questions

Ninguna.
