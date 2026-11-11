## 1. Lo usado hoy y los cupos cambiados

- [x] 1.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests`, vistas fallar antes: la vista trae lo usado hoy por persona y
  categoría (un almuerzo hoy y otro ayer dan 1); `CONFIGURATION_CHANGED` de una persona trae `QuotaChanges` con el límite
  antes y después, y con el actor de un operador (D1 y D3). Verifica: rojo antes y verde después.
  - `RacionesDePersonasTests` (3), con `UsadoHoy` en 0 y sin `QuotaChanges`: `total: 3`, `error: 3` («Expected: 1,
    Actual: 0» y «The given key 'QuotaChanges' was not present»).
- [x] 1.2 `AdministracionLocal`: lo usado hoy en `VistaAdministracion` y `QuotaChanges` en el registro. Verifica: 1.1 en
  verde y `Infrastructure.Sqlite.Tests` entero en verde.
  - `UsadoHoy` sale de `tbComedorUsosCupo` sumado por persona y categoría en la fecha de negocio de
    `CalculadoraFechaNegocio`; `QuotaChanges` compara los cupos de antes y de después, también en el alta (todos con el
    antes nulo). `Infrastructure.Sqlite.Tests`: `total: 237`, `error: 0`, sin pruebas existentes cambiadas.

## 2. La planilla compartida y la exportación de Personas

- [x] 2.1 `EscritorPlanilla` con la escritura de CSV y XLSX de `ExportacionConsumos` (D5), sin cambiar su salida.
  Verifica: `ExportacionConsumosTests` en verde sin cambios.
  - `EscritorPlanilla` (el temporal con GUID, el nombre libre del segundo, el apóstrofo de las fórmulas, el libro mínimo,
    con columnas más allá de la Z) y `NumeroPlanilla`; `ExportacionConsumos` arma sus filas y lo llama. Sin tocar sus
    pruebas: `ExportacionConsumosTests` 13 de 13 y `ExportarConsumosTests` 9 de 9.
- [x] 2.2 Pruebas nuevas de `ExportacionPersonas`, vistas fallar antes: el CSV con encabezado, una fila por persona, lo
  usado y el límite de cada cupo, sin códigos; el XLSX con números. Después, `ExportacionPersonas`. Verifica: rojo antes y
  verde después.
  - `ExportacionPersonasTests` (2) contra un esqueleto: `total: 2`, `error: 2` (`NotImplementedException`). Las columnas
    salen de `VistaAdministracion.ParesDeCupo` y `NombrePar`, que la tabla también usa. `Infrastructure.Sqlite.Tests`:
    `total: 239`, `error: 0`.

## 3. La tabla: x/n, tocar para editar y exportar

- [x] 3.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: «0/1» y «0/2» en la fila de 0001; «1/1» con un almuerzo
  hoy; tocar la celda REFRIGERIO abre la edición con ese límite elegido; «Exportar CSV» y «Exportar Excel» en Personas con
  `PEOPLE_EXPORTED`. Verifica: rojo antes y verde después.
  - `RacionesEnPersonasTests` (5) contra la tabla de antes: `total: 5`, `error: 5` («Expected: "0/1", Actual: "1"», el
    cupo sin `.cupo-tocable` y `#exportar-personas-csv` que no existe). `PuntoFinalTests` suma el mensaje nuevo
    `ExportacionPersonasHecha` a su lista, como esa prueba pide para cada método nuevo.
- [x] 3.2 `PanelPersonas.razor` con el x/n, la celda tocable y los botones de exportar (D1, D2 y D5). Verifica: 3.1 en
  verde.
  - Las columnas salen de `VistaAdministracion.ParesDeCupo`, con el orden de servicio y categoría que tenía el panel
    (`ParesDeColumnas` se borra); el campo elegido lleva la clase `elegido` y recibe el foco al dibujarse. Una fila nueva
    trae lo usado del largo de las columnas (sin eso, `IndexOutOfRangeException` en 7 pruebas que agregan personas).
- [x] 3.3 Las pruebas existentes que leen el límite como el texto de la celda pasan a leer el x/n, y nada más (decisión
  13). Verifica: `Kiosk.Tests` en verde y la lista de archivos cambiados pegada.
  - Con el x/n y las pruebas sin tocar: `total: 439`, `error: 4`, todas en `TablaPersonasTests` y por leer el límite como
    el texto de la celda. Cambian sus literales a «0/1» y «0/2», los dos `DisplayName` que repiten ese texto (los
    escenarios del delta) y el orden por cupo, que toma el límite después de la «/». Después: `total: 439`, `error: 0`.
    Pruebas existentes cambiadas: `TablaPersonasTests.cs` y `PuntoFinalTests.cs` (la lista de métodos).

## 4. El teclado en pantalla

- [x] 4.1 Pruebas nuevas, vistas fallar antes: escribir «ANA» en un nombre; una letra no entra en el límite; ocultar y
  mostrar el teclado, que sigue oculto al volver a editar (D4). Verifica: rojo antes y verde después.
  - `TecladoEnPersonasTests` (4) contra el panel sin teclado: `total: 4`, `error: 3` (sin `@onfocus` en el nombre y sin
    `.teclado-en-pantalla`); la cuarta, que no esté en la tabla de lectura, cuida lo que no se toca. Los escenarios del
    delta se precisaron: «Mayús» antes de «ANA», y borrar, «A» y «1» dejan «1».
- [x] 4.2 `TecladoEnPantalla.razor` y su uso en `PanelPersonas.razor`. Verifica: 4.1 en verde.
  - El campo elegido es el nombre, el límite o el código de una fila (`CampoElegido`), al recibir el foco o al tocar su
    cupo; las teclas no sacan el foco (`@onmousedown:preventDefault`). `Kiosk.Tests`: `total: 443`, `error: 0`, sin pruebas
    existentes cambiadas en este grupo.

## 5. El puesto compilado, documentos y verificación integral

- [x] 5.1 E2E nueva contra el puesto compilado, a 1024 × 768: el x/n en la tabla, tocar una celda, el teclado con sus
  teclas de 56 px dentro de la ventana y del mismo ancho por fila, y escribir con él; medidas pegadas y capturas miradas.
  Ajustar las E2E existentes que leen el límite como texto. Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests`
  en verde.
  - `RacionesEnPersonasE2ETests`. La primera captura mostró la tabla aplastada a una fila y media con el teclado y el
    campo elegido tapado por el encabezado fijo; la E2E entera, que «Muchas personas en 1024 × 768» (existente) veía 9
    filas en lugar de 10, por la barra de exportar debajo de la tabla. Corregido sin tocar esa prueba: el teclado en cuatro
    filas, el título que cede su alto con el teclado, `scroll-margin-top` en los campos, lo usado con su ancho fijo para
    alinear los campos de cada columna, y la exportación de Personas en el pie del diálogo (D5 y D6).
  - Medidas finales a 1024 × 768: el teclado, cuatro filas de 10 teclas de 87,2 × 56,0 px; el diálogo termina en 752 px y
    «Guardar» en 667; el límite elegido de 255,5 a 299,5 px, debajo del encabezado (192,5) y dentro de la tabla (345,0),
    con 3 filas enteras a la vista; los límites de REFRIGERIO empiezan los nueve en 540,8 px; el pie, «Exportar CSV» y
    «Exportar Excel» de 152,7 × 56,0 y «Cerrar» de 120,0 × 56,0, los tres desde 679,0 y con letra de 18 px. Capturas
    miradas: `personas-x-de-n-1024x768` y `personas-teclado-1024x768`. Ninguna E2E existente leía el límite como texto.
- [x] 5.2 `readme.md` (Personas) y la fila que corresponda de `docs/aplicacion-del-curso.md`. Verifica: el chequeo de
  enlaces de D8 de `mostrar-aplicacion-del-curso` con su control negativo, `enlaces rotos: 0`.
  - `readme.md`: Personas con el x/n, el toque, el teclado, los cupos cambiados en la general y la exportación en el pie.
    `docs/aplicacion-del-curso.md`, la fila de las reglas de `config.yaml`: los dos defectos que atrapó la regla de
    prolijidad. Control negativo `enlaces rotos: 2`; los cuatro documentos, `enlaces rotos: 0`.
- [ ] 5.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror`, el puesto
  publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c Release` contra ese EXE, con
  los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
