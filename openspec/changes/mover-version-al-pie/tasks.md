# Tasks

Rama local `feature/mejoras-2-5-dw`, en el worktree `C:\AI\INComedor\INComedor-AI4DEVS-mejoras`, sobre `a2f0026` (los
cinco changes de las mejoras 2 a 7 archivados). Se aplica después del gate de David (proposal). Cada grupo cierra con
`git diff --diff-filter=M --name-only a2f0026 -- tests/` pegado, que tiene que listar sólo las dos pruebas aprobadas en
la 1.1. Las pruebas de base y receptor corren con `INCOMEDOR_SQLSERVER=INMO-DW-AR`. Cada tarea que toca una pantalla
sigue la regla de prolijidad de `config.yaml`.

## 1. Las pruebas existentes

- [ ] 1.1 Mostrarle a David el cambio de las tres pruebas de D6 (`VersionEnPantallaTests.La_version_en_reposo`,
  `VersionEnPantallaTests.La_version_durante_una_atencion` y `VersionDelPaqueteE2ETests.Version_del_paquete_en_la_cabecera`)
  y esperar su aprobación escrita antes de tocarlas. Verifica: la aprobación citada en esta tarea, con la hora.

## 2. La versión en el pie

- [ ] 2.1 Pruebas nuevas en `Kiosk.Tests` (`VersionEnElPieTests`), vistas fallar antes: en reposo, la cabecera no
  tiene `#version-programa`; el pie tiene uno solo, con «versión desarrollo · » y el commit, sin `tabindex` ni
  `button`; con una persona en la selección, el mismo texto en el pie. Verifica: el filtro de la clase, en rojo antes
  y en verde después.
- [ ] 2.2 Las dos pruebas de `VersionEnPantallaTests` aprobadas en la 1.1, como dice D6. Verifica: rojas contra el
  código de hoy y verdes después de la 2.3.
- [ ] 2.3 `PantallaAtencion.razor` y `puesto.css` (D1, D2 y D3): la versión sale de la cabecera, entra al centro del pie
  con «versión », y el pie pasa a tres columnas. Verifica: 2.1 y 2.2 en verde, `CabeceraTests` y `NombreDelServicioTests`
  en verde sin cambios, y `dotnet build INComedor.sln -warnaserror` sin errores.
- [ ] 2.4 La E2E aprobada en la 1.1 (D6): el texto del pie, la versión centrada en la ventana con 1 px de tolerancia,
  su letra más chica que la del pie, sin solaparse con la franja ni con la marca, la cabecera en 64 px sin botones, y
  el engranaje a no más de 16 px de los bordes derecho e inferior. Mide por CDP a 1024 × 768, pega las medidas del
  `registro.txt` y deja la captura `pie-version-1024x768`. Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests
  --filter "FullyQualifiedName~VersionDelPaqueteE2ETests"` en verde.

## 3. El título en Heebo

- [ ] 3.1 Copiar `NGS6v5_NC0k9P9H2TbE.woff2` de
  `C:\dotnet-projects\Aplicaciones\Web\Intelektron.web.NET\Intelektron.web.NET\wwwroot\vendor\google\` a
  `src/Intelektron.INComedor.Kiosk/wwwroot/fonts/heebo-latin.woff2`, y el texto de la OFL 1.1 de Heebo de
  `https://raw.githubusercontent.com/google/fonts/main/ofl/heebo/OFL.txt` a `wwwroot/fonts/OFL-Heebo.txt` (D4).
  Verifica: `Get-FileHash` del `.woff2` igual al de INWeb, 30.240 bytes, y el `.txt` empieza con el aviso de copyright
  de Heebo y contiene «SIL OPEN FONT LICENSE Version 1.1».
- [ ] 3.2 Prueba E2E nueva `TituloConLaLetraDeInwebE2ETests`, vista fallar antes: a 1024 × 768, por CDP,
  `CSS.getPlatformFontsForNode` del texto del título da sólo «Heebo», `fontWeight` da `300`, el título entero se ve
  sin recortar entre los dos logos y la cabecera mide 64 px; ninguna regla de `puesto.css` nombra un dominio externo.
  Pega las medidas y deja la captura `titulo-heebo-1024x768`. Verifica: el filtro de la clase, en rojo antes y en verde
  después de la 3.3.
- [ ] 3.3 La `@font-face` y la letra del título en `puesto.css` (D4). Verifica: 3.2 en verde y
  `dotnet build INComedor.sln -warnaserror` sin errores.
- [ ] 3.4 La fuente que falta: con `heebo-latin.woff2` renombrado en la salida del build de la prueba, el título se ve
  entero en la sans-serif del sistema y la cabecera sigue en 64 px; el archivo vuelve a su nombre al terminar.
  Verifica: un caso más de `TituloConLaLetraDeInwebE2ETests` en verde.

## 4. Documentación y verificación

- [ ] 4.1 `readme.md`: dónde se ve la versión («al centro del pie, con "versión" adelante») y la letra del título.
  `docs/aplicacion-del-curso.md`: la evidencia del change, con su gate. Verifica: el chequeo de enlaces relativos del D8
  de `mostrar-aplicacion-del-curso`, con su control negativo.
- [ ] 4.2 La solución entera en primer plano, con `INCOMEDOR_SQLSERVER=INMO-DW-AR`: `dotnet build INComedor.sln
  -warnaserror` y `dotnet test INComedor.sln`, `openspec validate --all --strict` y
  `git diff --diff-filter=M --name-only a2f0026 -- tests/` con sólo las dos clases aprobadas. Verifica: los resúmenes
  literales pegados en esta tarea.
