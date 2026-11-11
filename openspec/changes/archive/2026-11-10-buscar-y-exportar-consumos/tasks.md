# Tasks

Rama `feature/buscar-y-exportar-consumos-dw`, apilada en local sobre `feature/ordenar-configuracion-dw` (`796e041`) y
rebasada sobre la punta de `origin/final-project-dw` cuando el #20 esté integrado, antes de publicarla. Gate dado por la
decisión 13 del plan del cierre final. Cada grupo cierra con `git diff --diff-filter=M --name-only <base> -- tests/`
pegado, para ver que sólo cambian las pruebas existentes de la propuesta. Las pruebas de base y receptor corren con
`INCOMEDOR_SQLSERVER=INMO-DW-AR`. Cada tarea que toca una pantalla sigue la regla de prolijidad de `config.yaml`.

## 1. La consulta: 24 horas, texto, código y número

- [x] 1.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests`, vistas fallar antes: el rango por instante (24 h y el borde de
  24 h y un minuto), el texto sin acentos ni mayúsculas, el número de comprobante, los comodines escapados y el filtro
  por persona del código. Verifica: `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests
  --filter-class *ConsultaConsumos*`, rojo antes y verde después.
  - `BusquedaConsumosTests` (8 casos) contra el esqueleto de la API: `total: 8`, `error: 8` (por ejemplo `Expected: 3` /
    `Actual: 4` en las 24 horas, y `Expected: "UNKNOWN_CODE"` / `Actual: null`). Después: con `ConsultaConsumosTests`,
    `total: 22`, `error: 0`.
- [x] 1.2 `FiltroConsumos` con `DesdeUtc`, `HastaUtc`, `Texto` y `PersonaCodigo` opcionales al final; la función
  `normalizar`; el número de comprobante en `ConsumoConsultado` (D1 y D2). Verifica: 1.1 en verde y las pruebas
  existentes de la consulta sin cambios.
  - `FiltroConsumos` suma `DesdeUtc`, `HastaUtc`, `Texto` y `Codigo` al final, con `UltimasHoras` y `PorInstante`;
    `ConsumoConsultado` suma `TicketId`, `TrabajoOriginalId` y `EstadoOriginal` al final, y `Numero`; la conexión registra
    `normalizar` (NFD sin marcas, minúsculas) y el patrón escapa `\`, `%` y `_` con `ESCAPE '\'`. Las 14 de
    `ConsultaConsumosTests` pasan sin cambios.
- [x] 1.3 Resolver un código a su persona sin abrir sesión (D3), con su prueba vista fallar antes. Verifica: la prueba en
  verde.
  - Dentro de la consulta: el código, con `Trim()` como al identificar, se busca en las credenciales `SERIAL_CODE` de
    cualquier versión; si no es de nadie, `UNKNOWN_CODE` sin consultar. `El_codigo_de_una_persona` y
    `Un_codigo_que_no_es_de_nadie`, en rojo antes y en verde después.

## 2. La pestaña Consumos: 24 horas, búsqueda y número

- [x] 2.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: abre con las últimas 24 horas y la fecha en la hora;
  «Últimas 24 h»; el texto; el código que se vacía y no queda en la pestaña ni en la bitácora; el código que no es de
  nadie; la columna del número. Verifica: rojo antes y verde después.
  - `BusquedaEnConsumosTests` (6 casos), contra la pestaña sin los campos nuevos: `total: 6`, `error: 6`
    (`ElementNotFoundException` de `#consumos-texto`, `#consumos-codigo` y `#ultimas-24h`). Un falso positivo de la prueba
    del código (el «0001» de la cola del GUID del puesto en la bitácora) se corrigió buscando el código como valor JSON.
- [x] 2.2 `PestanaConsumos.razor` con la fila de búsqueda y la columna nueva; `CONSUMPTIONS_QUERIED` con `TextSearched` y
  `PersonCode` sin valores. Verifica: 2.1 en verde.
  - El campo del código es `type="text"` enmascarado con `-webkit-text-security`, no `password`, porque Consumos no pide
    un código y así lo sigue afirmando `PestanaConsumosTests.En_mantenimiento_sin_ningun_codigo`. La bitácora suma
    `LastHours`, `TextSearched` y `PersonCode`. Con `PestanaConsumosTests`: `total: 13`, `error: 0`.
- [x] 2.3 Ajustar las pruebas existentes que afirman el día de negocio por defecto, vistas fallar antes. Verifica:
  `Kiosk.Tests` en verde y la lista de archivos cambiados pegada.
  - Ninguna hizo falta: `Kiosk.Tests` entero, `total: 406`, `error: 0`, sin pruebas existentes cambiadas en este grupo
    (los consumos de las pruebas son del instante del reloj, dentro de las 24 horas).

## 3. Ver, Reimprimir y Recuperar en cada fila, sin Comprobantes

- [x] 3.1 Pruebas nuevas, vistas fallar antes: «Ver» sin operador, sin trabajo ni copia; «Reimprimir» pide el código
  con la clave del día y no con un operador que entró con el suyo (`SesionOperador.Adoptar`); «Recuperar» sólo con el
  original en `FAILED`; ninguna pestaña Comprobantes. Verifica: rojo antes y verde después.
  - `AccionesEnConsumosTests` (6 casos), copiada a un worktree de `67c1e88` (sin el grupo 3): `total: 6`, `error: 6`
    (`ElementNotFoundException` de `.ver`, `.reimprimir` y `.recuperar` en la fila, y nueve pestañas). Después, verde.
    La reimpresión y la recuperación se leen en `tbComedorBitacora`, donde las escribe la asistencia.
- [x] 3.2 Las acciones en `PestanaConsumos.razor`, la entrada del operador en la pestaña, `Adoptar` y el diálogo de «Ver»
  (D4 y D5); el `enum Pestana` sin Comprobantes y `PestanaComprobantes.razor` borrado (D7). Verifica: 3.1 en verde.
  - El contenedor del motivo se llama `pedido-reimpresion`: con `motivo-reimpresion`, su clase empezaba como la de los
    botones `.motivo` y `PantallaAtencionTests.Aviso_de_incertidumbre_antes_del_motivo`, que busca `class="motivo`,
    lo encontraba antes del aviso. Se cambió el nombre, no la prueba.
- [x] 3.3 Pasar a Consumos las pruebas existentes de `PestanaComprobantesTests` y las de la asistencia en
  `PantallaAtencionTests`, y las que cuentan nueve pestañas, sin cambiar lo que afirman; vistas fallar antes. Verifica:
  `Kiosk.Tests` en verde y la lista de archivos cambiados pegada.
  - Con el grupo 3 y las pruebas sin tocar: `total: 412`, `error: 15` (las 2 propias que leían otra bitácora y 13
    existentes). Después: `total: 412`, `error: 0`. Pruebas existentes cambiadas contra `b9a794f`
    (`git diff --diff-filter=M --name-only b9a794f -- tests/`): `AccesoOperadorTests.cs` (nueve pestañas pasan a ocho),
    `PestanasConfiguracionTests.cs` (la lista sin Comprobantes, ocho `aria-selected`, siete paneles ocultos),
    `PestanaConsumosTests.cs` («Reimprimir» en la fila pide el código, en lugar de la pestaña Comprobantes),
    `PestanaComprobantesTests.cs` y `PantallaAtencionTests.cs` (la asistencia entra por «Reimprimir» de una fila, con
    un consumo antes) y `TextosComprobanteTests.cs` (la copia se pide desde Consumos). Ninguna afirmación se aflojó.

## 4. Exportar a CSV y a Excel

- [x] 4.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests`, vistas fallar antes: el CSV (BOM, `;`, encabezado, filas, el
  apóstrofo de una fórmula), el XLSX (se abre como zip con una hoja, el encabezado, las filas y el cargo numérico), el
  temporal que no queda tras un error, y el tope de 10 000. Verifica: rojo antes y verde después.
  - `ExportacionConsumosTests` (9 casos, uno por cada prefijo de fórmula), contra un esqueleto que compila: `total: 9`,
    `error: 9` (`NotImplementedException`). Después, 9 de 9. El temporal tras un error: con un archivo del mismo nombre ya
    en la carpeta, el renombrado falla, el `.tmp` no queda y el de antes no cambia; una carpeta borrada no se recrea.
- [x] 4.2 `ExportacionConsumos` (D6). Verifica: 4.1 en verde.
  - `ConsultaConsumos.Leer(filtro, tope)` lee hasta 10 000 con la misma consulta; el estado sale de
    `ExportacionConsumos.TextoEstado`, que ahora usa también la tabla. `Infrastructure.Sqlite.Tests`: `total: 228`,
    `error: 0`. Un libro de la prueba abierto con Excel por COM: una hoja «Consumos», 3 filas por 8 columnas, `F2` como
    `Double` 500 y `SUMA(F2:F3)` 1000, sin pedir reparación.
- [x] 4.3 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: «Exportar CSV» y «Exportar Excel» con un
  `IElectorCarpeta` de prueba, la carpeta recordada en `exportacion.json`, «Cambiar carpeta», sin poder escribir, y
  `CONSUMPTIONS_EXPORTED` en la general. Después, los botones, `IElectorCarpeta` y su registro en la ventana con
  `OpenFolderDialog`. Verifica: rojo antes y verde después.
  - `ExportarConsumosTests` (6 casos, con «cancelar la elección» de más), contra la pestaña sin botones: `total: 6`,
    `error: 6` (`ElementNotFoundException` de `#exportar-csv`, `#exportar-xlsx` y `#cambiar-carpeta`). «Sin poder
    escribir» niega crear archivos en la carpeta con una regla de ACL del usuario actual, que la prueba saca al final.
    Después, `Kiosk.Tests` `total: 418`, `error: 0`, sin pruebas existentes cambiadas en este grupo. El diálogo real
    (`ElectorCarpetaWindows`, con la ventana como dueña) no lo maneja ninguna prueba: lo ve David en su prueba del EXE.

## 5. El puesto compilado, documentos y verificación integral

- [x] 5.1 E2E nueva contra el puesto compilado, a 1024 × 768: un consumo, la tabla con las 24 horas y el número, la
  búsqueda, «Ver», la tabla sin desplazamiento horizontal y las medidas de la fila de búsqueda y de las acciones, con
  capturas miradas. Ajustar las E2E existentes que cuentan nueve pestañas. Verifica: `dotnet test
  tests/Intelektron.INComedor.E2E.Tests` en verde, con las medidas pegadas.
  - `BuscarYExportarConsumosE2ETests`. La primera corrida pasó, pero sus medidas mostraron «Consultar» solo en un tercer
    renglón (de 325,5 a 381,5 px), «Últimas 24 h» y «Consultar» con letra de 18 px al lado de campos de 16 px, y los
    tres botones de exportar de 145,1, 152,7 y 172,0 px: la prueba sumó esas tres afirmaciones (ningún renglón con un
    control solo, una sola letra, el mismo ancho), que fallan con esas medidas, y el CSS y el orden se corrigieron (D8).
  - Medidas finales, a 1024 × 768: filtros en dos renglones, de 172,0 a 228,0 px (`#consumos-desde`, `#consumos-hasta`,
    `#ultimas-24h`, `#consumos-texto`, `#consumos-codigo`) y de 261,5 a 317,5 px (`#consumos-persona`,
    `#consumos-servicio`, `#consumos-opcion`, `#consultar-consumos`), todos de 56 px y letra de 16 px; acciones de la
    fila `.ver` y `.reimprimir` de 44 px y 15 px; exportar de 611,0 a 667,0 px, tres de 157,6 px y 16 px; la tabla de
    924 px en su caja de 924 px; el diálogo termina en 752 px de 768. Capturas miradas: `consumos-busqueda-1024x768`,
    `consumos-ver-1024x768` y `consumos-exportado-1024x768`, en `TestResults/e2e/20261110-212937-buscar-exportar-84931ad6`.
  - `AccesoOperadorE2ETests` y `ConfiguracionPorPestanasE2ETests` cuentan ocho pestañas en lugar de nueve.
    `dotnet test tests/Intelektron.INComedor.E2E.Tests`: `total: 32`, `error: 0` (21:30 a 21:33).
- [x] 5.2 `readme.md` (Consumos, la reimpresión y la exportación) y la fila de `docs/aplicacion-del-curso.md` que
  corresponda. Verifica: el chequeo de enlaces de D8 con su control negativo, `enlaces rotos: 0`.
  - `readme.md`: los pasos 5 y 6 de la demostración y la tabla de códigos pasan de la pestaña Comprobantes a las filas de
    Consumos; la asistencia con «Ver», «Reimprimir» y «Recuperar»; las pestañas sin Comprobantes; Consumos con las 24
    horas, la búsqueda, el código y la exportación. La fila de OWASP de `docs/aplicacion-del-curso.md` suma el `LIKE`
    escapado y el apóstrofo de las fórmulas; `prompts.md` §5, el criterio humano 16 (el punto final de la interfaz). Las
    capturas 10 y 15 siguen mostrando nueve pestañas y los filtros de antes hasta que la tanda 3 las rehaga.
  - El chequeo de enlaces de D8 de `mostrar-aplicacion-del-curso`: el control negativo da `ROTO`, `ANCLA` y
    `enlaces rotos: 2`; sobre `readme.md`, `prompts.md`, `docs/aplicacion-del-curso.md` y `docs/cobertura-entrega.md`,
    `enlaces rotos: 0`; el mapa, `filas del mapa: 82 · defectuosas: 0`.
- [x] 5.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror`, el puesto
  publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c Release` contra ese EXE, con
  los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
  - Sobre `2823860`, de 21:39 a 21:44: `openspec validate --all --strict` → `Totals: 16 passed, 0 failed (16 items)`;
    build → `0 Advertencia(s)`, `0 Errores`; el paquete, con su E2E contra el EXE `total: 1`, `error: 0`, zip de
    77.579.535 bytes; `dotnet test INComedor.sln -c Release` contra ese EXE → `total: 854`, `error: 0` (Contracts 23,
    Domain 91, Sqlite 228, Kiosk 418, Sync.Api 62, E2E 32). Pruebas existentes cambiadas contra `b9a794f`
    (`git diff --diff-filter=M --name-only`): `AccesoOperadorE2ETests.cs`, `ConfiguracionPorPestanasE2ETests.cs`,
    `AccesoOperadorTests.cs`, `PantallaAtencionTests.cs`, `PestanaComprobantesTests.cs`, `PestanaConsumosTests.cs`,
    `PestanasConfiguracionTests.cs` y `TextosComprobanteTests.cs`, las de 3.3 y 5.1.
- [x] 5.4 Correcciones de la verificación (`/openspec-verify-change` por un agente de sólo lectura sobre `2823860`, sin
  críticos, 9 advertencias y 14 sugerencias), con el gate dado (decisión 13).
  - Advertencia 1: el MODIFIED de «Asistencia con permiso OPERATOR» vuelve a decir «en el snapshot activo» y que
    `PersonId`, `GrantId` y `GrantedRole` se auditan sólo si existen (491 caracteres).
  - Advertencias 2 y 3 y sugerencia 7: la acción que espera un código vive en `SesionOperador.Pendiente`, el pedido dice
    qué va a hacer y con qué número, y se cierra con «Cancelar», al dejar la pestaña o a los 30 s sin un toque; la sesión
    adoptada es prestada: «Salir» y los 30 s la olvidan sin cerrarla, y Configuración la cierra una vez. Requisito ADDED
    «El pedido del código de un operador en Consumos», cuatro escenarios; D5.
  - Advertencia 4: «Cancelar» descarta también el aviso de una denegación (`DescartarAviso`).
  - Advertencia 5, en pruebas existentes (decisión 13): `PestanaComprobantesTests.Operador_con_permiso_vigente` afirma
    los cuatro motivos en lugar de `table`, que ya no probaba nada; `PestanaConsumosTests` nombra el requisito, no un
    escenario que no ejerce.
  - Advertencia 6 y sugerencia 12: D3 dice por qué el campo es `text` enmascarado y no `password`, suma
    `spellcheck="false"`, y que el código se busca en cualquier versión.
  - Advertencia 7 y sugerencia 14: las pruebas comparan los mensajes enteros (`ExportacionHecha`, `CarpetaElegida`,
    `CarpetaNoGuardada`, `ExportacionTopada`, `SinCarpeta`); cancelar la elección avisa; `CONSUMPTIONS_EXPORTED` suma los
    filtros como la consulta; D8 dice «las oraciones nuevas».
  - Advertencia 2: la carpeta que no se pudo guardar se usa hasta cerrar Configuración y su aviso va antes del resultado.
  - Advertencia 8: la fila del pedido de código con letra de 16 px, medida en la E2E: `#codigo-operador`,
    `#entrar-operador` y `#cancelar-operador` de 367,0 a 423,0 px, los tres de 16 px; captura
    `consumos-pedido-operador-1024x768`, mirada.
  - Advertencia 9: un nombre ya tomado en el mismo segundo pasa a `-2`, hasta `-9`; el temporal lleva un GUID y se borra
    en un `finally`.
  - Sugerencias 1 a 6, 10 y 11: comentarios al día; el libro conserva los caracteres fuera del plano básico (por `Rune`);
    `Hoy()` dentro del `try`; la pestaña dice que la tabla está filtrada por un código, sin mostrarlo, y `FiltroConsumos`
    no lo escribe en su `ToString`; pruebas más fuertes (el contenido de «Ver», el `PersonId` de 9001, «a_b» contra
    «AnaXBel», el tabulador y el retorno como fórmulas, «Recuperar» por el pedido); el paso 6 del readme después de
    «Salir»; D8 dice qué botones van a 16 px. La 9 (el propósito de la spec viva) se hace al archivar.
  - Quedan anotadas en el PR, sin cambio: la 8 (los nombres `PestanaComprobantesTests` y los escenarios del requisito
    retirado en `ConsultaConsumosTests`), la 13 («Ver» sin encabezado ni pie, como el comprobante en pantalla, que
    tampoco los tiene) y la del diálogo de carpetas modal con el temporizador, que ve David en su prueba del EXE.
  - Vistas fallar contra `2823860` (un worktree con las pruebas nuevas y sólo lo que necesitan para compilar: los textos
    de `MensajesAtencion` y la constante `NombresPorSegundo`): Sqlite `total: 234`, `error: 3`; Kiosk `total: 428`,
    `error: 10`. Las demás pruebas nuevas cubren comportamiento que ya andaba. Sobre las correcciones: Sqlite 234 de 234,
    Kiosk 428 de 428 y la E2E nueva en verde.
