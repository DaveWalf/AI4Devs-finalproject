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

- [ ] 3.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: «0/1» y «0/2» en la fila de 0001; «1/1» con un almuerzo
  hoy; tocar la celda REFRIGERIO abre la edición con ese límite elegido; «Exportar CSV» y «Exportar Excel» en Personas con
  `PEOPLE_EXPORTED`. Verifica: rojo antes y verde después.
- [ ] 3.2 `PanelPersonas.razor` con el x/n, la celda tocable y los botones de exportar (D1, D2 y D5). Verifica: 3.1 en
  verde.
- [ ] 3.3 Las pruebas existentes que leen el límite como el texto de la celda pasan a leer el x/n, y nada más (decisión
  13). Verifica: `Kiosk.Tests` en verde y la lista de archivos cambiados pegada.

## 4. El teclado en pantalla

- [ ] 4.1 Pruebas nuevas, vistas fallar antes: escribir «ANA» en un nombre; una letra no entra en el límite; ocultar y
  mostrar el teclado, que sigue oculto al volver a editar (D4). Verifica: rojo antes y verde después.
- [ ] 4.2 `TecladoEnPantalla.razor` y su uso en `PanelPersonas.razor`. Verifica: 4.1 en verde.

## 5. El puesto compilado, documentos y verificación integral

- [ ] 5.1 E2E nueva contra el puesto compilado, a 1024 × 768: el x/n en la tabla, tocar una celda, el teclado con sus
  teclas de 56 px dentro de la ventana y del mismo ancho por fila, y escribir con él; medidas pegadas y capturas miradas.
  Ajustar las E2E existentes que leen el límite como texto. Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests`
  en verde.
- [ ] 5.2 `readme.md` (Personas) y la fila que corresponda de `docs/aplicacion-del-curso.md`. Verifica: el chequeo de
  enlaces de D8 de `mostrar-aplicacion-del-curso` con su control negativo, `enlaces rotos: 0`.
- [ ] 5.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror`, el puesto
  publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c Release` contra ese EXE, con
  los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
