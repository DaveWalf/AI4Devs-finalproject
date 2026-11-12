# Tasks

Rama local `feature/mejoras-2-5-dw`, en el worktree `C:\AI\INComedor\INComedor-AI4DEVS-mejoras`, sobre `a2f0026` (los
cinco changes de las mejoras 2 a 7 archivados). Se aplica después del gate de David (proposal). Cada grupo cierra con
`git diff --diff-filter=M --name-only a2f0026 -- tests/` pegado, que tiene que listar sólo las dos pruebas aprobadas en
la 1.1. Las pruebas de base y receptor corren con `INCOMEDOR_SQLSERVER=INMO-DW-AR`. Cada tarea que toca una pantalla
sigue la regla de prolijidad de `config.yaml`.

## 1. Las pruebas existentes

- [x] 1.1 Mostrarle a David el cambio de las tres pruebas de D6 (`VersionEnPantallaTests.La_version_en_reposo`,
  `VersionEnPantallaTests.La_version_durante_una_atencion` y `VersionDelPaqueteE2ETests.Version_del_paquete_en_la_cabecera`)
  y esperar su aprobación escrita antes de tocarlas. Verifica: la aprobación citada en esta tarea, con la hora.
  Hecho: David las aprobó el 11/11/2026 a las 23:25, «Aprobadas», tal como las describe el punto 6 del gate y D6.

## 2. La versión en el pie

- [x] 2.1 Pruebas nuevas en `Kiosk.Tests` (`VersionEnElPieTests`), vistas fallar antes: en reposo, la cabecera no
  tiene `#version-programa`; el pie tiene uno solo, con «desarrollo · » y el commit, sin la palabra «versión», sin
  `tabindex` ni `button`; con una persona en la selección, el mismo texto en el pie. Verifica: el filtro de la clase, en rojo antes
  y en verde después.
  Hecho el 11/11/2026: 2 casos, 2 en rojo antes («Assert.Null() Failure: Value is not null»: la cabecera tenía la
  versión) y 2 en verde después de la 2.3.
- [x] 2.2 Las dos pruebas de `VersionEnPantallaTests` aprobadas en la 1.1, como dice D6. Verifica: rojas contra el
  código de hoy y verdes después de la 2.3.
  Hecho: las afirmaciones, como las aprobó David; además, sus dos `DisplayName` y el resumen de la clase dicen «el pie»
  donde decían «la cabecera», para que el nombre no contradiga lo que se prueba. Verdes después de la 2.3.
- [x] 2.3 `PantallaAtencion.razor` y `puesto.css` (D1, D2 y D3): la versión sale de la cabecera, entra al centro del pie
  con el texto de hoy, y el pie pasa a tres columnas. Verifica: 2.1 y 2.2 en verde, `CabeceraTests` y `NombreDelServicioTests`
  en verde sin cambios, y `dotnet build INComedor.sln -warnaserror` sin errores.
  Hecho: el pie en `grid` con `minmax(0, 1fr) auto minmax(0, 1fr)`; `.titulo-cabecera` y `.cabecera #version-programa`
  quitados. `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter` de las cuatro clases: total 11, error 0;
  build `-warnaserror`: 0 errores.
- [x] 2.4 La E2E aprobada en la 1.1 (D6): el texto del pie, la versión centrada en la ventana con 1 px de tolerancia,
  su letra más chica que la del pie, sin solaparse con la franja ni con la marca, la cabecera en 64 px sin botones, y
  el engranaje a no más de 16 px de los bordes derecho e inferior. Mide por CDP a 1024 × 768, pega las medidas del
  `registro.txt` y deja la captura `pie-version-1024x768`. Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests
  --filter "FullyQualifiedName~VersionDelPaqueteE2ETests"` en verde.
  Hecho: la primera corrida dio la versión 4 px a la derecha (`CentroVersion = 516`), porque el pie tenía relleno de
  16 px a la izquierda y 8 a la derecha; pasó a 8 y 8, con 8 px de margen en la franja, que sigue a 16 px del borde.
  Medidas del `registro.txt` de la segunda: `AltoCabecera = 64, VersionesEnCabecera = 0, BotonesEnCabecera = 0,
  CentroVentana = 512, CentroVersion = 512, IzquierdaVersion = 459,84, DerechaVersion = 564,16, DerechaFranja = 284,71,
  IzquierdaMarca = 877,74, LetraPie = 14, LetraVersion = 12, MargenDerechoEngranaje = 8, MargenInferiorEngranaje = 0`;
  total 1, error 0; captura `pie-version-1024x768.png` mirada.
- [x] 2.5 Agregada al revisar el change contra su spec: el escenario «El pie y la cabecera no cambian de lugar» pide
  medir otra vez con la sincronización en otro estado, y la 2.4 mide sólo «Sin receptor configurado». Prueba E2E nueva
  `VersionEnElPieE2ETests`: mide, cambia el texto de la franja por uno largo (el de un error de credencial con
  pendientes y retenidos) y vuelve a medir. Verifica: el filtro de la clase en verde y su control negativo en rojo.
  Hecho: escrita después del código, así que su control negativo fue volver un momento el pie a `display: flex`: en
  rojo («La versión no está centrada: … CentroVersion = 354,32»), y en verde con la grilla: `CentroVersion = 512` antes
  y después, `DerechaFranja` de 284,71 a 442,39, `AltoPie = 57` las dos veces; `puesto.css` restaurado con el mismo
  SHA-256. Captura `pie-sincronizacion-larga-1024x768`.

## 3. El título en Heebo

- [x] 3.1 Copiar `NGS6v5_NC0k9P9H2TbE.woff2` de
  `C:\dotnet-projects\Aplicaciones\Web\Intelektron.web.NET\Intelektron.web.NET\wwwroot\vendor\google\` a
  `src/Intelektron.INComedor.Kiosk/wwwroot/fonts/heebo-latin.woff2`, y el texto de la OFL 1.1 de Heebo de
  `https://raw.githubusercontent.com/google/fonts/main/ofl/heebo/OFL.txt` a `wwwroot/fonts/OFL-Heebo.txt` (D4).
  Verifica: `Get-FileHash` del `.woff2` igual al de INWeb, 30.240 bytes, y el `.txt` empieza con el aviso de copyright
  de Heebo y contiene «SIL OPEN FONT LICENSE Version 1.1».
  Hecho: SHA-256 `964DFE7C…24A5C431` en los dos, 30.240 bytes; `OFL-Heebo.txt` (4.474 bytes) empieza con «Copyright
  2014 The Heebo Project Authors (https://github.com/OdedEzer/heebo)» y su línea 9 dice «SIL OPEN FONT LICENSE Version
  1.1 - 26 February 2007».
- [x] 3.2 Prueba E2E nueva `TituloConLaLetraDeInwebE2ETests`, vista fallar antes: a 1024 × 768, por CDP,
  `CSS.getPlatformFontsForNode` del texto del título da sólo «Heebo», `fontWeight` da `300`, el título entero se ve
  sin recortar entre los dos logos y la cabecera mide 64 px; ninguna regla de `puesto.css` nombra un dominio externo.
  Pega las medidas y deja la captura `titulo-heebo-1024x768`. Verifica: el filtro de la clase, en rojo antes y en verde
  después de la 3.3.
  Hecho: en rojo antes («Expected: "Heebo" · Actual: "Georgia"»), en verde después. Medidas: `familias usadas Heebo;
  Peso = 300, Letra = 22, AltoCabecera = 64, IzquierdaTexto = 371,95, DerechaTexto = 652,04, DerechaLogoEmpresa =
  173,14, IzquierdaLogoConcesionaria = 850,86, AnchoUtil = 646, AnchoTexto = 646, ReglasExternas = 0`; captura
  `titulo-heebo-1024x768.png` mirada.
- [x] 3.3 La `@font-face` y la letra del título en `puesto.css` (D4). Verifica: 3.2 en verde y
  `dotnet build INComedor.sln -warnaserror` sin errores.
  Hecho: una `@font-face` con el `unicode-range` latino de INWeb, igual al de su `css2.css`; el título en
  `Heebo, "Segoe UI", system-ui, sans-serif`, peso 300, 22 px. Build `-warnaserror`: 0 errores.
- [x] 3.4 La fuente que falta: con `heebo-latin.woff2` renombrado en la salida del build de la prueba, el título se ve
  entero en la sans-serif del sistema y la cabecera sigue en 64 px; el archivo vuelve a su nombre al terminar.
  Verifica: un caso más de `TituloConLaLetraDeInwebE2ETests` en verde.
  Hecho de otra forma, con el mismo resultado: en vez de renombrar el archivo en la salida del build, que comparten las
  E2E que corren en paralelo, la prueba lo bloquea por CDP (`Network.setBlockedURLs` con `*heebo-latin.woff2*` y la caché
  apagada) y recarga la página. En rojo antes («Expected: "Segoe UI" · Actual: "Georgia"»), en verde después: `familias
  usadas Segoe UI Light` (la cara de 300 de Segoe UI), `Peso = 300, AltoCabecera = 64, AnchoTexto = 646` en 646;
  captura `titulo-sin-heebo-1024x768`.

## 4. Documentación y verificación

- [x] 4.1 `readme.md`: dónde se ve la versión (al centro del pie) y la letra del título.
  `docs/aplicacion-del-curso.md`: la evidencia del change, con su gate. Verifica: el chequeo de enlaces relativos del D8
  de `mostrar-aplicacion-del-curso`, con su control negativo.
  Hecho: «La versión que corre» del README y la fila «No reemplazar la revisión humana…» del mapa. Chequeo de D8: el
  control negativo da `ROTO`, `ANCLA` y `enlaces rotos: 2`; `readme.md`, `prompts.md`, `docs/cobertura-entrega.md` y
  `docs/aplicacion-del-curso.md`, de a uno, `enlaces rotos: 0`.
- [x] 4.2 La solución entera en primer plano, con `INCOMEDOR_SQLSERVER=INMO-DW-AR`: `dotnet build INComedor.sln
  -warnaserror` y `dotnet test INComedor.sln`, `openspec validate --all --strict` y
  `git diff --diff-filter=M --name-only a2f0026 -- tests/` con sólo las dos clases aprobadas. Verifica: los resúmenes
  literales pegados en esta tarea.
  Hecho el 11/11/2026 de 23:43 a 23:48, sobre `082db8d`, sin commits durante la corrida: build «0 Advertencia(s) · 0
  Errores»; `dotnet test INComedor.sln` «Resumen de la serie de pruebas: Correcta! · total: 1220 · error: 0 · correcto:
  1220 · omitido: 0 · duración: 4m 33s 310ms» (Contracts 23, Domain 91, Sync.Api 62, Infrastructure.Sqlite 329, Kiosk
  663 y E2E 52); `openspec validate --all --strict` «Totals: 20 passed, 0 failed (20 items)»; el `git diff` lista
  `tests/Intelektron.INComedor.E2E.Tests/VersionDelPaqueteE2ETests.cs` y
  `tests/Intelektron.INComedor.Kiosk.Tests/VersionEnPantallaTests.cs`, las dos aprobadas. Una corrida anterior, de las
  23:35, dio 1218 de 1219: la única roja comparaba el commit del puesto compilado (`b71ad49`) con un HEAD que se movió
  por dos commits de documentación durante la corrida; repetida sola sobre el HEAD, en verde.
- [x] 4.3 Agregada por el verify independiente en sólo lectura (11/11, 23:44 a 23:50), sin defectos de implementación:
  corregir lo que encontró antes de archivar. Verifica: `openspec validate --all --strict`, el archivado ensayado en
  una copia de `openspec/` y la E2E de la 2.5 en verde.
  Hecho: la 2.4 tildada (decía «Hecho» sin la casilla); el delta nuevo de `distribucion-puesto` («El paquete lleva su
  versión en el pie» en lugar de «El paquete lleva su versión», que pedía el tag en la cabecera), aprobado por David a
  las 23:51; D1 con la grilla real y la franja en dos renglones con el texto largo (decía que se recortaba); los
  comentarios de `VersionPrograma.cs`, `publicar-puesto.ps1` y el resumen de `VersionDelPaqueteE2ETests` dicen «el
  pie»; la 2.5 mide con los tres mensajes reales de la franja y el borde de su texto con un `Range`: `CentroVersion =
  512` antes y después, `DerechaFranja` 274,71 y 369,95, `AltoFranja` 18,5 y 37 (dos renglones), `AltoPie = 57`, total 1,
  error 0. `validate --all --strict` 20 de 20; el archivado ensayado da +3, −2 y `validate --specs --strict` 19 de 19.
  Queda como está, por ser pruebas existentes sin aprobación: los `DisplayName` de `VersionProgramaTests.cs:16` y `:26`
  dicen «la cabecera dice…».
