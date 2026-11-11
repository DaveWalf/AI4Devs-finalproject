## 1. La base

- [ ] 1.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests`, vistas fallar antes: `ConsultaConsumos.Desde` devuelve los
  consumos con secuencia mayor, en orden, con su secuencia, hasta el tope; la línea CSV de una fila de consumo tiene las
  columnas de la exportación de Consumos, el apóstrofo de las fórmulas y ningún código (D1). Verifica: rojo antes y verde
  después.
- [ ] 1.2 `ConsultaConsumos.Desde` y la línea CSV de `EscritorPlanilla` y `ExportacionConsumos`. Verifica: 1.1 en verde y
  `Infrastructure.Sqlite.Tests` entero.

## 2. La exportación en el puesto

- [ ] 2.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: la configuración y su validación (`exportacion-automatica.json`);
  una pasada escribe el archivo que crece y el archivo por día, con el encabezado una vez; una pasada sin consumos nuevos
  no cambia nada; el SFTP simulado en «Caído» conserva su marca y la carpeta avanza, y al volver recibe lo que le faltó
  una sola vez; las marcas sobreviven a otro arranque; los registros, sin nombres ni códigos (D2 a D5). Verifica: rojo
  antes y verde después.
- [ ] 2.2 `ConfiguracionExportacion`, `EstadoExportacion`, los dos destinos y `ExportacionAutomatica`. Verifica: 2.1 en
  verde.

## 3. En la pantalla

- [ ] 3.1 Pruebas nuevas, vistas fallar antes: la pestaña Exportación antes de General guarda y valida, registra
  `CONSUMPTION_EXPORT`, muestra el estado de cada destino y «Exportar ahora» corre una pasada; con un operador no existe;
  el reloj corre las pasadas con la exportación activa (D4 y D6). Cambian, por la decisión 13, la lista de ocho pestañas
  y los siete paneles ocultos de `PestanasConfiguracionTests`. Verifica: rojo antes y verde después.
- [ ] 3.2 La pestaña, sus registros, el reloj y el cableado en la ventana. Verifica: 3.1 en verde y `Kiosk.Tests` entero,
  con las pruebas existentes que cambien listadas con su porqué.

## 4. El puesto compilado, documentos y verificación integral

- [ ] 4.1 E2E nueva a 1024 × 768: la fila de nueve pestañas entra; la pestaña Exportación medida contra Impresora; activar
  con una carpeta y el SFTP simulado caído, confirmar un consumo, «Exportar ahora», ver la falla, levantar el SFTP y ver
  los dos destinos al día, con capturas miradas (D7). Cambia, por la decisión 13, la cuenta de ocho pestañas de
  `AccesoOperadorE2ETests`. Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests` en verde.
- [ ] 4.2 `readme.md` (la exportación automática y el SFTP como pendiente) y la fila que corresponda de
  `docs/aplicacion-del-curso.md`. Verifica: el chequeo de enlaces de D8 de `mostrar-aplicacion-del-curso` con su control
  negativo, `enlaces rotos: 0`.
- [ ] 4.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror
  --no-incremental`, el puesto publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c
  Release` contra ese EXE, con los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
