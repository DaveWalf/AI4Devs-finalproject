## 1. Leer y escribir planillas

- [x] 1.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests`, vistas fallar antes: `LectorPlanilla` lee un libro del puesto
  (cadenas en línea y números) y uno con cadenas compartidas (como lo guarda Excel), ubica las celdas por su referencia
  aunque falten, y rechaza un archivo que no es un zip, uno sin hojas y uno con una parte de más de 20 MB; `EscritorPlanilla`
  escribe un libro de dos hojas con una columna de texto. Verifica: rojo antes y verde después.
  - `PlanillasTests` (6) contra esqueletos: `total: 6`, `error: 6` (`NotImplementedException`).
- [x] 1.2 `LectorPlanilla` y `EscritorPlanilla` con varias hojas y columnas de texto (D1 y D6). Verifica: 1.1 en verde y
  las pruebas de las exportaciones sin cambios.
  - `LectorPlanilla` con `XmlReader` sin DTD, topes de 5 MB, 20 MB por parte y 2 000 filas; `EscritorPlanilla` arma el
    libro con N hojas y, sólo si alguna pide columnas de texto, un `styles.xml` mínimo con el formato «@» y las columnas con
    su ancho. `Infrastructure.Sqlite.Tests`: `total: 246`, `error: 0`, con las de exportación sin cambios.

## 2. Revisar y aplicar la importación

- [x] 2.1 Pruebas nuevas, vistas fallar antes: el resumen de «Modificar y agregar» y de «Reemplazar» (que conserva a los
  operadores y al administrador); las filas con error (sin código, repetido, de una persona dada de baja, nombre inválido,
  límite fuera de rango) y las columnas que faltan o sobran; aplicar crea una sola versión con un `CONFIGURATION_CHANGED`
  por persona; lo usado hoy no cambia; la planilla de ejemplo con su encabezado, el código como texto y ningún código de la
  base (D2 a D6). Verifica: rojo antes y verde después.
  - `ImportacionPersonasTests` (6) contra el esqueleto: `total: 6`, `error: 6` (`NotImplementedException`). Suman la
    planilla sin personas, que es un error del archivo (D2, agregado al aplicar).
- [x] 2.2 `ImportacionPersonas` (`Revisar`, `Aplicar` y la planilla de ejemplo). Verifica: 2.1 en verde y
  `Infrastructure.Sqlite.Tests` entero en verde.
  - `AdministracionLocal.LeerParaImportar` (interno) lee en una transacción la vista, los códigos vigentes de las
    personas activas, los usados en cualquier versión y las personas con un permiso sin revocar. `Infrastructure.Sqlite.Tests`:
    `total: 252`, `error: 0`, sin pruebas existentes cambiadas; build con `-warnaserror` sin advertencias. La planilla de
    ejemplo escrita sobre una copia de la base de la demostración se abrió con Excel por COM: hojas «Personas» e
    «Instrucciones», encabezado `Nombre | Código | ALMUERZO: límite | REFRIGERIO: límite`, la columna B con formato `@`
    y un «0105» escrito después en B10 queda «0105».

## 3. En la pantalla

- [ ] 3.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: «Planilla de ejemplo» (y cancelar la carpeta); «Importar
  Excel» con un `IElectorArchivo` de prueba muestra el resumen; «Modificar y agregar», «Reemplazar» y «Cancelar»; una
  importación con errores no deja aplicar; `PEOPLE_IMPORTED` y `PEOPLE_IMPORT_REJECTED` en la general sin nombres ni
  códigos (D4, D7 y D8). Verifica: rojo antes y verde después.
- [ ] 3.2 `IElectorArchivo` con el `OpenFileDialog` de la ventana, los botones del pie y el cartel del resumen. Verifica:
  3.1 en verde y `Kiosk.Tests` entero en verde.

## 4. El puesto compilado, documentos y verificación integral

- [ ] 4.1 E2E nueva a 1024 × 768: la planilla de ejemplo descargada y vuelta a importar con un cambio, el resumen y
  «Modificar y agregar»; los cuatro botones del pie y los del resumen medidos, con capturas miradas. Verifica: `dotnet test
  tests/Intelektron.INComedor.E2E.Tests` en verde.
- [ ] 4.2 `readme.md` (Personas) y la fila que corresponda de `docs/aplicacion-del-curso.md`. Verifica: el chequeo de
  enlaces de D8 de `mostrar-aplicacion-del-curso` con su control negativo, `enlaces rotos: 0`.
- [ ] 4.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror`, el puesto
  publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c Release` contra ese EXE, con
  los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
