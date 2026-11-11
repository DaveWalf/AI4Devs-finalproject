## Context

Personas se edita en la tabla de `PanelPersonas` y se guarda con `AdministracionLocal.GuardarPersonas`, que valida todo
(código de sólo dígitos y hasta 20, que no se reasigna aunque esté revocado, RB25; nombre; límites de 0 a 99) y crea una
sola versión con un `CONFIGURATION_CHANGED` por persona, con el operador como actor. La exportación (`EscritorPlanilla`)
escribe CSV y un libro mínimo de una hoja; la carpeta la recuerda `AlmacenExportacion` y la pide `IElectorCarpeta`. Las
columnas de cupo salen de `VistaAdministracion.ParesDeCupo` y `NombrePar`. Los permisos están en
`tbComedorPermisosOperadores` (`OPERATOR` y `SYSTEM_ADMINISTRATOR`).

## Goals / Non-Goals

**Goals:** una planilla de ejemplo; importar un `.xlsx` en dos modos con un resumen antes de aplicar; todo o nada; la
bitácora de lo decidido y persona por persona.

**Non-Goals:** opciones o franjas; reactivar o cambiar códigos; CSV; importar con INWeb como dueño.

## Decisions

### D1. Leer la planilla

`LectorPlanilla` (en `Infrastructure.Sqlite`) lee la primera hoja de un `.xlsx`: `xl/workbook.xml` y sus relaciones dan
la ruta de la hoja; las celdas pueden ser cadenas compartidas (`sharedStrings.xml`, como guarda Excel), en línea (como
escribe el puesto), fórmulas con su valor de texto o números, que se leen en cultura invariante y sin «.0» si son
enteros. La referencia de cada celda («C4») ubica su columna aunque falten celdas. Con `XmlReader` sin DTD (OWASP, XXE);
el archivo hasta 5 MB y cada parte hasta 20 MB descomprimida (una bomba de zip se rechaza), y hasta 2 000 filas.

- **Descartada: una biblioteca para leer `.xlsx`.** Suma una dependencia y su auditoría para leer una hoja de texto y
  números; el puesto ya escribe el formato.

### D2. Las columnas

La fila 1 es el encabezado: «Nombre», «Código» y, por cada cupo, «<cupo>: límite», con el nombre de la tabla
(`NombrePar`), sin distinguir mayúsculas ni espacios de más. Falta «Nombre» o «Código», o hay una columna que no es de
ningún cupo: error del archivo. Una columna de cupo que no está deja ese cupo como estaba; una celda vacía en una que
está lo quita, como vaciar la celda en la tabla.

### D3. Quién es quién

Una fila se reconoce por su código, `Trim()` como al identificar: si es la credencial vigente de una persona activa de la
versión activa, la fila la modifica (nombre y cupos); si el código no existe, la agrega; si es de una persona dada de baja
o revocado, es un error de la fila (RB25: el código no se reasigna ni se reactiva acá). Un código repetido en el archivo
es error de las dos filas.

- **Descartada: reconocer por el nombre.** Dos personas pueden llamarse igual, y el nombre se corrige; el código no.

### D4. El resumen y los dos modos

Antes de aplicar, `ImportacionPersonas.Revisar` devuelve el resumen: las agregadas, las modificadas, las que quedan sin
cambios y, para «Reemplazar», las que se dan de baja (activas, que no están en el archivo y sin ningún permiso de
operador o de administrador, que se conservan). Con errores, el resumen los lista por fila y no deja aplicar. Sin
errores, el puesto muestra los totales de los dos modos y tres botones: «Modificar y agregar», «Reemplazar» y
«Cancelar». Aplicar arma las ediciones (modificar con el `PersonaId`, agregar con el código, dar de baja con `Quitar`) y
las guarda con `GuardarPersonas` en una sola versión: valida otra vez, y si algo cambió entre el resumen y el toque, el
rechazo se muestra como en un guardado a mano.

- **Descartada: que el modo quede configurado.** Cada importación decide, viendo cuántas personas se darían de baja.

### D5. Los consumos de hoy

El cupo es diario por persona (RB03) y lo usado está en `tbComedorUsosCupo`: importar no lo toca. Una persona con un
almuerzo hoy y límite nuevo 1 ya no puede almorzar hoy; con límite 2, puede una vez más. El resumen lo dice en una
línea, y la tabla de Personas lo muestra con el x/n.

### D6. La planilla de ejemplo

«Planilla de ejemplo» escribe, en la carpeta de la exportación, `INComedor-planilla-personas-AAAAMMDD-HHmmss.xlsx`:
la hoja «Personas» con el encabezado de D2, dos filas de ejemplo sintéticas y la columna «Código» con formato de texto
(para que Excel no le saque los ceros del principio a lo que se escriba después), y la hoja «Instrucciones», una oración
por fila. `EscritorPlanilla` suma varias hojas y columnas de texto (un `styles.xml` mínimo con el formato «@»).

### D7. Elegir el archivo

`IElectorArchivo` pide el `.xlsx`: la ventana lo registra con el `OpenFileDialog` de WPF sobre su ventana, como el de
carpetas, y las pruebas lo reemplazan por un doble.

### D8. La bitácora

Cada persona cambiada deja su `CONFIGURATION_CHANGED` de siempre, con `QuotaChanges` y el actor. La importación deja
además `PEOPLE_IMPORTED` en la general: el modo (`MERGE` o `REPLACE`), el nombre del archivo, los totales y, persona por
persona, su `PersonId` y lo que pasó (`ADDED`, `UPDATED`, `DEACTIVATED`, `UNCHANGED`), sin nombres ni códigos. Una
importación con errores deja `PEOPLE_IMPORT_REJECTED` con los totales de errores por motivo, sin aplicar nada.

### D9. Prolijidad

Los cuatro botones del pie de Personas («Exportar CSV», «Exportar Excel», «Importar Excel», «Planilla de ejemplo») miden
lo mismo, el ancho del más largo, con el alto y la letra de «Cerrar». El resumen es un cartel como el de «Ver» en
Consumos, con sus botones del mismo alto. Medido por CDP a 1024 × 768, con capturas.

## Risks / Trade-offs

- [Un código escrito como número en Excel pierde los ceros] → la planilla de ejemplo trae la columna como texto; un
  código importado sin ceros es otro código, y el resumen lo cuenta como agregado: se ve antes de aplicar.
- [Un archivo grande] → los topes de D1.

## Migration Plan

Sin migración. La reversión es revertir el PR.

## Open Questions

Ninguna.
