## Context

`PanelPersonas.razor` muestra la tabla de Personas de la versión activa (`AdministracionLocal.LeerVista`): una fila por
persona con su nombre, su estado, una columna por cada par de servicio y categoría con el límite, y las opciones
habilitadas. «Editar» pone los campos en la misma tabla; «Guardar» crea una versión nueva con
`AdministracionLocal.GuardarPersonas`, que registra un `CONFIGURATION_CHANGED` por persona cambiada con el antes, el
después, las versiones y el actor (`ConActor`: el operador que entró con su código, o nulo con `NO_AUTHENTICATION`). Lo
usado en el día está en `tbComedorUsosCupo`, por persona, ámbito, categoría y fecha de negocio, que la atención lee para
decidir (`LecturaDecision.LeerUsos`). La exportación de Consumos (`ExportacionConsumos`) ya escribe CSV y XLSX con una
carpeta elegida y recordada (`AlmacenExportacion`, `IElectorCarpeta`).

## Goals / Non-Goals

**Goals:** el x/n en cada cupo; tocar para editar; los cupos cambiados en la bitácora; un teclado en pantalla que se
muestra u oculta; exportar Personas.

**Non-Goals:** importar (2b); las ayudas de pantalla de todo el puesto (change 9); cambiar las reglas del cupo o la
validación; un visor de la bitácora.

## Decisions

### D1. Lo usado hoy

`VistaAdministracion` suma, por persona, lo usado hoy por categoría: la suma de `comucUsado` de `tbComedorUsosCupo` para la
persona, la categoría y la fecha de negocio actual (de `CalculadoraFechaNegocio`, como la atención). La celda dice «x/n»;
sin cupo, «—» aunque haya usos. Al editar, la celda muestra «x/» antes del campo del límite. Suma todos los ámbitos de
la categoría, mientras que la atención cuenta el ámbito de la regla que decide: hoy coinciden, porque cada versión tiene
un solo ámbito y `AplicarPersonas` lo usa para cada regla; con varios ámbitos habría que contar por ámbito.

- **Descartada: contar los consumos de `tbComedorConsumos`.** El cupo se decide con `tbComedorUsosCupo` y con las cargas de
  cada opción; contar consumos daría otro número que el de la atención.

### D2. Tocar para editar

En la tabla de lectura, cada celda de cupo es un botón (`.cupo-tocable`) que, si se puede editar, abre la edición como
«Editar» y deja elegido el campo del límite de esa celda. Con INWeb como dueño o con Opciones en edición, la celda no es
tocable.

- **Descartada: editar una sola celda y guardarla sola.** Cada guardado es una versión nueva con su validación; un
  guardado por celda llenaría la historia de versiones.

### D3. Los cupos cambiados en la bitácora

El registro de una persona cambiada suma `QuotaChanges`: una entrada por par de servicio y categoría cuyo límite cambió,
con `ServiceId`, `Service`, `CategoryId`, `Category`, `Before` y `After` (nulo cuando no había cupo o se quitó). El actor
y la hora ya están: con el código de un operador, su persona; con la clave del día, nulo con `NO_AUTHENTICATION`.

- **Descartada: un evento por cupo.** Partiría un guardado en varias líneas sin la persona entera de antes y de después.

### D4. El teclado en pantalla

`TecladoEnPantalla.razor` dibuja filas de teclas de 56 px (números; QWERTYUIOP; ASDFGHJKLÑ; ZXCVBNM con borrar; espacio)
y avisa cada tecla con un `EventCallback<string>`. Las teclas usan `@onmousedown:preventDefault` para no sacar el foco del
campo. `PanelPersonas` recuerda el campo elegido (el nombre, el límite o el código de una fila, al recibir el foco o al
tocar su celda) y le agrega la tecla o le borra el último carácter; en el límite y el código sólo entran dígitos. Se
muestra al editar; «Ocultar teclado» y «Mostrar teclado» lo alternan, y el estado vive en el panel hasta cerrar
Configuración: lo guarda `PanelConfiguracion`, porque en lectura el panel se vuelve a crear al cambiar de pestaña. Va
debajo de la tabla, dentro del panel, con las teclas de una fila del mismo ancho (D8 de prolijidad). Corta el largo como
el campo: el nombre hasta 80, el código hasta 20 y el límite en dos dígitos. El código de una persona nueva no se dibuja
(D10 de administrar-personas-y-opciones): el teclado le pone el valor al campo como propiedad del DOM con
`incomedor.ponerValor` (`wwwroot/js/puesto.js`), sin atributo ni árbol de render, y el foco del límite tocado lo pone
`incomedor.enfocar`; los dos buscan el campo por su `data-campo`, sin referencias de Blazor, que agregan a la página un
identificador al azar, así que se ve enmascarado y lo que se
tipee después con un teclado físico lo continúa. Descartar una fila nueva corre el campo elegido si venía después. Queda
abierto si el teclado táctil de Windows aparece igual al tocar un campo (`inputmode`): depende de la configuración del
equipo y lo ve David en su monitor.

- **Descartada: el teclado táctil de Windows.** Aparece fuera de la ventana del puesto y no se controla desde la página;
  el diálogo de la clave ya tiene uno propio por eso.

### D5. Exportar Personas

«Exportar CSV» y «Exportar Excel», en el pie del diálogo a la izquierda de «Cerrar» y sólo con Personas en lectura, escriben, con la carpeta de `AlmacenExportacion` (la de Consumos)
o pidiéndola la primera vez, `INComedor-personas-AAAAMMDD-HHmmss.csv` o `.xlsx`: la persona, el estado, por cada cupo lo
usado hoy y el límite, y las opciones habilitadas, sin ningún código. La escritura de CSV y XLSX sale de
`ExportacionConsumos` a `EscritorPlanilla` (en `Infrastructure.Sqlite`), que comparten las dos: temporal con GUID,
nombre libre con `-2` a `-9`, el apóstrofo de las fórmulas y el libro mínimo. `PEOPLE_EXPORTED` va a la general con el
formato, las filas y el nombre del archivo. Sin poder escribir, el aviso dice
dónde cambiar la carpeta, porque «Cambiar carpeta» está en Consumos. El encabezado también pasa por el apóstrofo de las
fórmulas: en Personas lleva los nombres de los servicios y las categorías, que llegan de la configuración.

- **Descartada: una carpeta propia de Personas.** Dos carpetas para lo mismo confunden; «Cambiar carpeta» de Consumos ya
  la cambia.
- **Descartada: los botones debajo de la tabla, como en Consumos.** Le quitaban 68 px a la tabla de lectura, que a
  1024 × 768 dejaba de mostrar diez filas («Se ven 9 filas enteras», `ConfiguracionPorPestanasE2ETests`); en el pie, al
  lado de «Cerrar», hay lugar.

### D6. Prolijidad

La celda «x/n» se lee como el límite de antes: la misma letra y alineación de la columna; al editar, lo usado ocupa
siempre su ancho, también vacío, para que los campos de una columna empiecen en el mismo borde. El teclado tiene cuatro
filas (el espacio va con «Mayús» y borrar), las teclas de una fila miden lo mismo y 56 px de alto, y con el teclado a la
vista el título de la edición cede su alto a la tabla; el campo elegido queda debajo del encabezado fijo
(`scroll-margin-top`). Los botones de exportar del pie miden como «Cerrar», 56 px y su letra de 18 px, y entre ellos el
ancho del más largo. Las teclas especiales («Mayús», «Espacio», borrar) van a 16 px para que «Espacio» entre en su
tecla; las letras y los números, a 20. Los botones de la edición («Agregar persona», «Ocultar teclado», «Cancelar»,
«Guardar») miden lo mismo de alto y de letra. Un campo elegido que el guardado rechazó se marca en rojo, como los demás.
Medido por CDP a 1024 × 768, con capturas.

- **Descartada: filas más bajas para hacer lugar al teclado.** La regla táctil pide 44 px por fila; con cuatro filas de
  teclado y el título fuera, se ven tres filas enteras, y «Ocultar teclado» devuelve el resto.

## Risks / Trade-offs

- [El teclado achica la tabla al editar] → se oculta con un toque, y la tabla de lectura, donde se ven diez filas, no lo
  tiene.
- [Lo usado hoy cambia mientras la pestaña está abierta] → se lee al abrir Personas y al guardar, como la vista.

## Migration Plan

Sin migración: se leen tablas que ya existen. La reversión es revertir el PR.

## Open Questions

Ninguna.
