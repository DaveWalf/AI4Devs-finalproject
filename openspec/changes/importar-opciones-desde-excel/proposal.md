# Proposal

## Why

El 10/11/2026 David pidió (mejora 15): «Opciones exporta e importa Excel, con la descarga de una planilla base; lo
importado reemplaza todas las opciones vigentes». Decidió que todo entra en la final, con el gate dado (decisiones 10, 12
y 13 del plan `AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`). Es el change 3b del §6b; el 3a dejó el catálogo a la vista y la
personalización de cada opción, y el 2b, el lector y el escritor de planillas y el elector de archivos.

Hoy las opciones se cargan de a una en su edición. No hay forma de sacar el catálogo a una planilla ni de traer uno.

Origen: historia HU01.1 (la selección), RF04 (el catálogo), RB10 (un cambio de precio pide reconfirmar) y RB21 (todo
cambio con actor y resultado); ticket TK02.1.

## What Changes

- **Exportar Excel**, en el pie de Opciones: el catálogo vigente en un `.xlsx` con las columnas que se importan (Nombre,
  Servicio, Importe, las unidades de cada categoría de cupo y las franjas), en la carpeta de la exportación de Consumos.
- **Planilla base**: el mismo libro sin opciones del puesto, con dos filas de ejemplo y una hoja «Instrucciones».
- **Importar Excel**: lee el `.xlsx` y muestra un resumen antes de aplicar (cuántas opciones se agregan, cuántas cambian,
  cuántas quedan igual y cuántas se quitan), con «Reemplazar» y «Cancelar». Lo importado reemplaza todo el catálogo: una
  opción se reconoce por su nombre; la que no está en el archivo se quita, sin tocar sus consumos.
- **Todo o nada**: una fila con un error (sin nombre o repetido, un servicio o una categoría desconocidos, un importe o
  unas unidades fuera de rango, una franja mal escrita, ninguna carga) o un archivo sin opciones deja el catálogo como
  estaba, con la fila y el motivo.
- **Bitácora**: una sola versión nueva con un `CONFIGURATION_CHANGED` por opción, como al guardar a mano, y
  `MENU_OPTIONS_IMPORTED` o `MENU_OPTIONS_IMPORT_REJECTED` con la huella del archivo y el resumen opción por opción;
  `MENU_OPTIONS_EXPORTED` al exportar.

## Non-goals

- Importar personas (2b) o la presentación de cada opción (3a): el fondo y el precio visible quedan como estaban por
  opción, también para una opción modificada por la importación.
- Un modo «Modificar y agregar» para Opciones: la mejora pide reemplazar.
- CSV.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `administracion-local`: exportar Opciones, la planilla base e importarlas reemplazando el catálogo.

## Impact

- **Código**: `Infrastructure.Sqlite` (`ExportacionOpciones`, `ImportacionOpciones`, que reúsan `EscritorPlanilla`,
  `LectorPlanilla` y la validación de `GuardarOpciones`) y `Kiosk` (los botones del pie de Opciones, el resumen en el
  cartel de la importación y los registros). Sin migración.
- **Pruebas**: nuevas en `Infrastructure.Sqlite.Tests`, `Kiosk.Tests` y una E2E a 1024 × 768. Pruebas existentes que
  pueden cambiar (decisión 13): `PuntoFinalTests`, si se suman métodos a `MensajesAtencion`.
- **Documentos**: `readme.md` (Opciones) y la fila que corresponda de `docs/aplicacion-del-curso.md`.

## Gate (decisión 13 del plan: dado, con límites)

Opciones recomendadas, aplicadas sin esperar:

1. Una opción del archivo se reconoce por su nombre, sin distinguir mayúsculas ni espacios de más: las opciones no
   tienen código, y el nombre es lo que se ve en la atención. Un nombre repetido en el archivo es un error.
2. «Reemplazar» es el único modo: lo pide la mejora. La opción que no está en el archivo se quita del catálogo activo y sus
   consumos quedan registrados, como al quitarla a mano.
3. Las cargas van en una columna por categoría de cupo («<CATEGORÍA>: unidades»), vacía o cero sin carga; las franjas,
   en una columna de texto («11:00 a 14:00; 18:00 a 21:00»), vacía para todo el día.
4. El importe se lee como número de Excel o como texto con coma decimal («1.800,00»), en pesos.
5. Un cambio de nombre es, para el archivo, una opción nueva y otra que se quita; la planilla de instrucciones lo dice.
6. Un archivo sin ninguna opción es un error: dejaría la atención sin nada que ofrecer.

Talla: L. Si una prueba existente falla por otra razón, se para (cláusula del plan).
