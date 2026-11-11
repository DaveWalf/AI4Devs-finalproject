## 1. Exportar, la planilla base y revisar y aplicar la importación

- [x] 1.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests`, vistas fallar antes: exportar el catálogo y volver a leerlo con
  `LectorPlanilla`; la planilla base con su encabezado y sus dos filas; revisar y aplicar reemplazando (una modificada,
  una agregada, una quitada, una sin cambios, en una sola versión); el importe como número y como texto; las filas con
  error (sin nombre o repetido, servicio o categoría desconocidos, unidades fuera de rango, sin cargas, franja mal
  escrita) y las columnas que faltan o sobran; un archivo sin opciones; aplicar sin cambios no crea versión (D1 a D5).
  Verifica: rojo antes y verde después.
  - `ImportacionOpcionesTests` (6) contra el esqueleto: `total: 6`, `error: 6` (`NotImplementedException`).
- [x] 1.2 `ImportacionOpciones` (exportar, la planilla base, revisar y aplicar). Verifica: 1.1 en verde y `Infrastructure.Sqlite.Tests` entero.
  - Las dos en `ImportacionOpciones` (exportar, la planilla base, revisar y aplicar), con el encabezado y la letra de
    columna de la importación de Personas, que pasan a internas. `Infrastructure.Sqlite.Tests`: `total: 268`, `error: 0`;
    build con `-warnaserror` sin advertencias.

## 2. En la pantalla

- [x] 2.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: «Exportar Excel» y «Planilla base» en el pie de Opciones
  (y cancelar la carpeta); «Importar Excel» muestra el resumen con «Reemplazar» y «Cancelar»; «Reemplazar» deja la
  versión nueva y el catálogo a la vista leído de nuevo; una importación con errores no deja aplicar; los tres registros
  en la general, sin el contenido del archivo; con INWeb como dueño no se importa (D4 a D6). Verifica: rojo antes y verde
  después.
  - `ImportarOpcionesTests` (6) contra los mensajes ya escritos: `total: 6`, `error: 6` (sin `#exportar-opciones`,
    `#importar-opciones` ni `#planilla-opciones`). Tres cayeron primero por un error de la prueba (leer el catálogo antes
    de dibujar el puesto), corregido con un elector de archivos cuya ruta se da después.
- [x] 2.2 Los botones del pie, el cartel del resumen y los registros. Verifica: 2.1 en verde y `Kiosk.Tests` entero, con
  las pruebas existentes que cambien listadas con su porqué.
  - Los tres botones en el pie de Opciones (la importación, sin INWeb), el cartel con «Reemplazar» y «Cancelar» o los
    errores, el catálogo leído de nuevo al aplicar y `MENU_OPTIONS_EXPORTED`, `MENU_OPTIONS_IMPORTED` y
    `MENU_OPTIONS_IMPORT_REJECTED`. `Kiosk.Tests`: `total: 492`, `error: 0`; build con `-warnaserror` sin advertencias.
    Prueba existente cambiada (decisión 13): `PuntoFinalTests`, que suma los cuatro mensajes nuevos y los motivos de
    Opciones con sus ejemplos (8 líneas, sin cambiar ninguna aserción).

## 3. El puesto compilado, documentos y verificación integral

- [x] 3.1 E2E nueva a 1024 × 768: exportar, cambiar el archivo (el nombre escrito de una opción, que se ubica en el XML
  mejor que un número), importarlo con el diálogo de archivos, el
  resumen y «Reemplazar»; los tres botones del pie y los del resumen medidos, con capturas miradas (D7). Verifica: `dotnet
  test tests/Intelektron.INComedor.E2E.Tests` en verde.
  - `ImportarOpcionesE2ETests`: el cambio del archivo es el nombre escrito de «Almuerzo común» («Almuerzo Común»), la
    misma opción, que se modifica sin perder su id; se ubica más fácil en el XML que un número. Los tres botones del pie
    miden 147,6 × 56 px desde 679, con la letra de 18 px de «Cerrar»; el resumen, «Reemplazar» 134,2 y «Cancelar» 120 px,
    los dos de 56 px desde 432, y el cartel termina en 513 de 768; «Reemplazar» deja la versión 2. El diálogo de archivos
    tiene su título propio, «Planilla de Opciones para importar» (`IElectorArchivo.Elegir` con título, con la versión
    sin título por defecto: los dobles existentes no cambian), y lo contesta `DialogoDeArchivosE2E`, compartido, sin
    tocar la E2E de Personas. Capturas miradas: el pie, el resumen y el catálogo importado.
- [x] 3.2 `readme.md` (Opciones) y la fila que corresponda de `docs/aplicacion-del-curso.md`. Verifica: el chequeo de
  enlaces de D8 de `mostrar-aplicacion-del-curso` con su control negativo, `enlaces rotos: 0`.
  - `readme.md` (Opciones) y la fila de OWASP del mapa. Enlaces de los dos y del change: `enlaces rotos: 0`; el control
    negativo, `enlaces rotos: 2`.
- [x] 3.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror`, el puesto
  publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c Release` contra ese EXE, con
  los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
  - Sobre `c72fc28`, de 04:09 a 04:17: validate 16 de 16, build sin advertencias, el paquete con su E2E y `total: 973`,
    `error: 0` (Contracts 23, Domain 91, Sqlite 268, Kiosk 492, Sync.Api 62, E2E 37). Prueba existente cambiada: sólo
    `PuntoFinalTests`. La integral después de las correcciones y del archivado va en el PR.

## 4. Correcciones de la verificación

- [x] 4.1 Lo que marcó el agente de sólo lectura sobre `c72fc28` (3 críticos, 10 advertencias y 14 sugerencias), con
  pruebas nuevas vistas fallar antes. Verifica: `Infrastructure.Sqlite.Tests` y `Kiosk.Tests` enteros en verde.
  - El importe: un texto con otra forma que la de es-AR («1800.50», «1.8», «18.00») es `NOT_DIGITS` en vez de leerse
    multiplicado; el tope se mira antes de multiplicar, así 28 dígitos dan `OUT_OF_RANGE` y no desbordan; el ruido de la
    coma flotante de Excel se lleva a la escala. «00» unidades es ninguna carga. Una columna de categoría que falta
    conserva esas cargas, como Personas. El nombre se reconoce primero exacto. El resumen cuenta las filas de ejemplo.
    `ImportacionOpcionesTests` suma 4: `error: 4` antes de corregir (los importes, los valores que se leen bien, la columna
    que falta y las filas de ejemplo).
  - La pantalla: `MensajesAtencion.ErrorDeImportacionOpciones`, con los rangos de Opciones (los errores reusaban los de
    Personas: «de 0 a 99», «ni el límite de un cupo»); el resumen nombra las que se quitan, avisa las filas de ejemplo y
    destaca «Cancelar» con el foco; el pie de Opciones exige también que no haya un operador; el título del diálogo, una
    constante de `IElectorArchivo`. `ImportarOpcionesTests` suma 5 y extiende 2 propias del change: en un worktree de
    `c72fc28` con sólo la superficie, `total: 11`, `error: 4` (los textos de error en dos pruebas, los nombres que se
    quitan y el aviso de la planilla base); la opción quitada sin ofrecer y con sus consumos, importar sin cambios,
    Escape y el pie con la edición abierta fijan lo que ya andaba. `Kiosk.Tests`: `total: 497`; `Sqlite`: 272.
  - La spec cita AT37 y AT47, suma el importe escrito de otra forma y aclara que el rechazo al aplicar queda como el de un
    guardado a mano; el diseño, la columna que falta, el reconocimiento exacto primero, la forma del importe, las franjas
    que se aceptan y la medida de los botones; el proposal, las tildes y el nombre de la clase.
  - `PuntoFinalTests` clasifica también `ErrorDeImportacionOpciones` e `ImportacionOpcionesConFilasDeEjemplo`, con un
    ejemplo de cada motivo de Opciones (antes iban en la lista de Personas).
  - Quedan sin cambiar, con su porqué: `DialogoDeArchivosE2E` duplica el código de la E2E de Personas, que no se toca
    sin que el comportamiento lo exija (decisión 13).
