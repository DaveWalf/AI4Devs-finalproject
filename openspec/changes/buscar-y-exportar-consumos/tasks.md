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

- [ ] 4.1 Pruebas nuevas en `Infrastructure.Sqlite.Tests`, vistas fallar antes: el CSV (BOM, `;`, encabezado, filas, el
  apóstrofo de una fórmula), el XLSX (se abre como zip con una hoja, el encabezado, las filas y el cargo numérico), el
  temporal que no queda tras un error, y el tope de 10 000. Verifica: rojo antes y verde después.
- [ ] 4.2 `ExportacionConsumos` (D6). Verifica: 4.1 en verde.
- [ ] 4.3 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: «Exportar CSV» y «Exportar Excel» con un
  `IElectorCarpeta` de prueba, la carpeta recordada en `exportacion.json`, «Cambiar carpeta», sin poder escribir, y
  `CONSUMPTIONS_EXPORTED` en la general. Después, los botones, `IElectorCarpeta` y su registro en la ventana con
  `OpenFolderDialog`. Verifica: rojo antes y verde después.

## 5. El puesto compilado, documentos y verificación integral

- [ ] 5.1 E2E nueva contra el puesto compilado, a 1024 × 768: un consumo, la tabla con las 24 horas y el número, la
  búsqueda, «Ver», la tabla sin desplazamiento horizontal y las medidas de la fila de búsqueda y de las acciones, con
  capturas miradas. Ajustar las E2E existentes que cuentan nueve pestañas. Verifica: `dotnet test
  tests/Intelektron.INComedor.E2E.Tests` en verde, con las medidas pegadas.
- [ ] 5.2 `readme.md` (Consumos, la reimpresión y la exportación) y la fila de `docs/aplicacion-del-curso.md` que
  corresponda. Verifica: el chequeo de enlaces de D8 con su control negativo, `enlaces rotos: 0`.
- [ ] 5.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror`, el puesto
  publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c Release` contra ese EXE, con
  los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
