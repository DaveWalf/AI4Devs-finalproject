## 1. La base

- [x] 1.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests`, vistas fallar antes: `ConsultaConsumos.Desde` devuelve los
  consumos con secuencia mayor, en orden, con su secuencia, hasta el tope; la línea CSV de una fila de consumo tiene las
  columnas de la exportación de Consumos, el apóstrofo de las fórmulas y ningún código (D1). Verifica: rojo antes y verde
  después. Hecho: 2 en `ExportacionDesdeMarcaTests`, vistas fallar con `NotImplementedException`.
- [x] 1.2 `ConsultaConsumos.Desde` y la línea CSV de `EscritorPlanilla` y `ExportacionConsumos`. Verifica: 1.1 en verde y
  `Infrastructure.Sqlite.Tests` entero. Hecho: Sqlite 292 de 292; `Leer` y `Desde` comparten columnas, origen y
  lectura de fila, y el CSV de siempre sale de `LineaCsv`.

## 2. La exportación en el puesto

- [x] 2.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: la configuración y su validación (`exportacion-automatica.json`);
  una pasada escribe el archivo que crece y el archivo por día, con el encabezado una vez; una pasada sin consumos nuevos
  no cambia nada; el SFTP simulado en «Caído» conserva su marca y la carpeta avanza, y al volver recibe lo que le faltó
  una sola vez; las marcas sobreviven a otro arranque; los registros, sin nombres ni códigos (D2 a D5). Verifica: rojo
  antes y verde después. Hecho: 6 en `ExportacionAutomaticaTests` (con el reloj de las pasadas, con
  un `TimeProvider` que anota sus temporizadores), vistas fallar con `NotImplementedException`.
- [x] 2.2 `ConfiguracionExportacion`, `EstadoExportacion`, los dos destinos y `ExportacionAutomatica`. Verifica: 2.1 en
  verde. Hecho: Kiosk 532 de 532; cada tramo del mismo archivo mueve la marca, así una falla en el segundo
  archivo de una pasada no vuelve a escribir el primero.

## 3. En la pantalla

- [x] 3.1 Pruebas nuevas, vistas fallar antes: la pestaña Exportación antes de General guarda y valida, registra
  `CONSUMPTION_EXPORT`, muestra el estado de cada destino y «Exportar ahora» corre una pasada; con un operador no existe;
  el reloj corre las pasadas con la exportación activa (D4 y D6). Cambian, por la decisión 13, la lista de ocho pestañas,
  sus `aria-selected` y los siete paneles ocultos de `PestanasConfiguracionTests`, la cuenta de ocho de
  `AccesoOperadorTests` y `AccionesEnConsumosTests`, y `PuntoFinalTests` con los dos métodos nuevos. Verifica: rojo antes
  y verde después. Hecho: 4 en `ExportacionEnPantallaTests`, 3 vistas fallar porque la pestaña
  no existía (la del operador fija que no aparezca); el reloj se prueba en `ExportacionAutomaticaTests`.
- [x] 3.2 La pestaña, sus registros, el reloj y el cableado en la ventana. Verifica: 3.1 en verde y `Kiosk.Tests` entero,
  con las pruebas existentes que cambien listadas con su porqué. Hecho: Kiosk 536 de 536.

## 4. El puesto compilado, documentos y verificación integral

- [x] 4.1 E2E nueva a 1024 × 768: la fila de nueve pestañas entra; la pestaña Exportación medida contra Impresora; activar
  con una carpeta y el SFTP simulado caído, confirmar un consumo, «Exportar ahora», ver la falla, levantar el SFTP y ver
  los dos destinos al día, con capturas miradas (D7). Cambia, por la decisión 13, la cuenta de ocho pestañas de
  `AccesoOperadorE2ETests` y de `ConfiguracionPorPestanasE2ETests` (ésta, vista en la primera integral). Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests` en verde. Hecho:
  `ExportacionE2ETests` en verde; las nueve pestañas en una fila hasta x 887; los cinco campos desde x 273,5 con
  686,5 × 56 px y los once botones de 56 px con letra de 18 px; tres capturas miradas. Los eventos de la exportación
  automática pasan a `CONSUMPTIONS_AUTO_EXPORTED` y `CONSUMPTIONS_AUTO_EXPORT_FAILED`, porque la exportación a mano ya
  registra `CONSUMPTIONS_EXPORTED`.
- [x] 4.2 `readme.md` (la exportación automática y el SFTP como pendiente) y la fila que corresponda de
  `docs/aplicacion-del-curso.md`. Verifica: el chequeo de enlaces de D8 de `mostrar-aplicacion-del-curso` con su control
  negativo, `enlaces rotos: 0`. Hecho: 0.
- [ ] 4.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror
  --no-incremental`, el puesto publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c
  Release` contra ese EXE, con los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.

## 5. Correcciones de la verificación

- [x] 5.1 Lo que encontró la verificación por un agente de sólo lectura (08:07 a ~08:20), corregido con el gate dado
  (decisión 13) y primero con las pruebas que lo muestran, vistas fallar: un nulo donde va un texto y una ruta de
  dispositivo (`\\.\`, `\\?\`) vuelven a lo de fábrica; la primera pasada corre al minuto, o cuando le toca por el
  último intento; un archivo abierto por otro programa falla como `FILE_IN_USE`, sin temporales; una carpeta que no existe
  conserva la marca; cada destino guarda su identidad y otra carpeta, otro servidor u otro tipo de archivo recibe toda la
  historia, como una marca de una base anterior (`ConsultaConsumos.UltimaSecuencia`); las marcas viven en memoria y un
  estado que no se guarda no es una falla ni repite; un estado abierto por otro programa no escribe
  (`STATE_UNREADABLE`, vista fallar contra un mutante); la pasada del reloj registra cualquier falla como
  `UNEXPECTED_ERROR` sin cerrar el puesto; `Dispose` espera la pasada en curso; el temporal es propio y se fuerza a disco.
  En la pantalla: «Exportar ahora» fuera del hilo de la pantalla con «Exportando…», sin destinos lo dice, una base ocupada
  lo dice, el mensaje tiene su propio id (`#mensaje-exportacion-automatica`, que Consumos ya usaba), el estado dice «último
  envío a las HH:mm» en lugar de «al día», «Elegir» limpia el aviso y `CONSUMPTION_EXPORT` dice si cambió la carpeta o el
  servidor. La E2E mide la columna contra Impresora y «Elegir» al borde de los campos. Verifica: `dotnet test --project
  tests/Intelektron.INComedor.Kiosk.Tests` y la E2E de la exportación en verde. Hecho: Sqlite 3 de 3 en la clase, Kiosk
  543 de 543, `ExportacionE2ETests` en verde con los campos desde x 273,5 como `#numero-puesto` y «Elegir» hasta x 960,0;
  capturas miradas.
