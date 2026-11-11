## 1. Exportar, la planilla base y revisar y aplicar la importación

- [ ] 1.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests`, vistas fallar antes: exportar el catálogo y volver a leerlo con
  `LectorPlanilla`; la planilla base con su encabezado y sus dos filas; revisar y aplicar reemplazando (una modificada,
  una agregada, una quitada, una sin cambios, en una sola versión); el importe como número y como texto; las filas con
  error (sin nombre o repetido, servicio o categoría desconocidos, unidades fuera de rango, sin cargas, franja mal
  escrita) y las columnas que faltan o sobran; un archivo sin opciones; aplicar sin cambios no crea versión (D1 a D5).
  Verifica: rojo antes y verde después.
- [ ] 1.2 `ExportacionOpciones` e `ImportacionOpciones`. Verifica: 1.1 en verde y `Infrastructure.Sqlite.Tests` entero.

## 2. En la pantalla

- [ ] 2.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: «Exportar Excel» y «Planilla base» en el pie de Opciones
  (y cancelar la carpeta); «Importar Excel» muestra el resumen con «Reemplazar» y «Cancelar»; «Reemplazar» deja la
  versión nueva y el catálogo a la vista leído de nuevo; una importación con errores no deja aplicar; los tres registros
  en la general, sin el contenido del archivo; con INWeb como dueño no se importa (D4 a D6). Verifica: rojo antes y verde
  después.
- [ ] 2.2 Los botones del pie, el cartel del resumen y los registros. Verifica: 2.1 en verde y `Kiosk.Tests` entero, con
  las pruebas existentes que cambien listadas con su porqué.

## 3. El puesto compilado, documentos y verificación integral

- [ ] 3.1 E2E nueva a 1024 × 768: exportar, cambiar un importe en el archivo, importarlo con el diálogo de archivos, el
  resumen y «Reemplazar»; los tres botones del pie y los del resumen medidos, con capturas miradas (D7). Verifica: `dotnet
  test tests/Intelektron.INComedor.E2E.Tests` en verde.
- [ ] 3.2 `readme.md` (Opciones) y la fila que corresponda de `docs/aplicacion-del-curso.md`. Verifica: el chequeo de
  enlaces de D8 de `mostrar-aplicacion-del-curso` con su control negativo, `enlaces rotos: 0`.
- [ ] 3.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror`, el puesto
  publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c Release` contra ese EXE, con
  los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
