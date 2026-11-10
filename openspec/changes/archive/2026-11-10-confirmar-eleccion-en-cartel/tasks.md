# Tasks

Sólo cambian las tres pruebas existentes del punto 3 del gate, con la aprobación de David. Al cerrar cada grupo, `git diff --diff-filter=M --name-only <base> -- tests/` lista sólo `PantallaAtencionTests.cs`, `PantallaDistribucionTests.cs` y `ConfiguracionPorPestanasE2ETests.cs`, donde `<base>` es la punta de la ampliación del 1 sobre la que se apila este change. Si otra prueba existente falla, se para y se le pregunta a David. Cada prueba nueva se ve fallar antes de implementar lo que la hace pasar, y su `DisplayName` empieza con la capability y el nombre del escenario. Las de `Kiosk.Tests` y la E2E corren con `INCOMEDOR_SQLSERVER` apuntando a la instancia del equipo, como pide `AGENTS.md`.

## 1. El cartel de la elección

- [x] 1.1 Agregar a `Kiosk.Tests` el archivo nuevo `CartelEleccionTests.cs`, con «Elegir abre el cartel con lo elegido», «Elegir otra vuelve a la selección», «Escape en el cartel», «Opción sin cargo» (cartel sin importe) y «Treinta segundos sin actividad» con el cartel abierto. Verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~CartelEleccion"`.
  - Salida del 10/11 a las 13:48, contra el código de `5bd4677`: fallaron cuatro de cinco. «Elegir abre el cartel» (`Assert.Empty() Failure: Collection was not empty`: había «Confirmar» sin opción elegida), «Elegir otra» (`No elements were found that matches the selector '#elegir-otra'`), «Opción sin cargo» (`'#cartel-eleccion #eleccion'`) y «Treinta segundos» (`Assert.NotEmpty() Failure`: no había cartel abierto).
  - «Escape en el cartel» pasó, porque no comprobaba el cartel antes de Escape. Se le sumó esa precondición, que es el GIVEN del escenario, y falló: `Assert.NotEmpty() Failure: Collection was empty`.
  - «Elegir abre el cartel» mira también que el título, la lista, las páginas, la cabecera y el pie quedan `inert` y el cartel no.
  - En «Treinta segundos», la persona siguiente es `9001`, que no tiene opciones: la semilla no tiene otra persona que pueda consumir.
- [x] 1.2 Cambiar `PantallaAtencionTests.Controles_de_la_seleccion_son_botones` y `PantallaDistribucionTests.Con_nueve_los_controles_son_botones` para que toquen una opción antes de buscar «Confirmar», y sumar a la primera que «Cancelar» está fuera del cartel y que, con el cartel abierto, el título y la lista están `inert`. Verlas fallar con el filtro `FullyQualifiedName~Controles_de_la_seleccion|FullyQualifiedName~Con_nueve_los_controles`.
  - A las 13:48, contra el código de `5bd4677`: falló la primera (`Assert.NotNull() Failure: Value is null`, «Confirmar» no estaba en un cartel). La segunda pasó: sólo suma el toque, que el código de antes no necesitaba.
  - Su rojo, a las 13:53: las versiones originales de las dos, corridas contra el código de 1.3, fallan sólo por eso, `No elements were found that matches the selector '#confirmar'`, `error: 2`. Se restauraron las cambiadas.
- [x] 1.3 Implementar el cartel (D1), «Cancelar» en `.titulo` sin la barra del pie (D2) y la inercia y Escape (D3) en `PantallaAtencion.razor`, con su CSS en `wwwroot/css/puesto.css` (D4). Verificar con los filtros de 1.1 y 1.2 que pasan, y con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` que no hay ninguna otra falla.
  - `Kiosk.Tests` entero a las 13:52: `total: 364`, `error: 0` (las 359 de la base más las cinco nuevas).
  - La página cierra el cartel también cuando la confirmación vuelve a la selección con un aviso (la opción ya no está), para que el aviso no quede tapado, y la cabecera y el pie quedan `inert` con el cartel, como «el resto de la pantalla» del escenario del teclado.

## 2. El cartel en el puesto compilado

- [x] 2.1 Agregar a `E2E.Tests` el archivo nuevo `CartelEleccionE2ETests.cs`: en 1024 × 768, sin opción elegida no hay «Confirmar»; al tocar una, el foco está en «Confirmar», el cartel y sus dos botones miden al menos 56 px y entran en la ventana; «Elegir otra» lo cierra; y confirmar desde el cartel llega a «Consumo registrado». Cambiar `ConfiguracionPorPestanasE2ETests.La_ventana_angosta_sigue_desplazandose` para que toque la última opción y mida «Confirmar» del cartel dentro de la ventana. Verlas fallar con `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter "FullyQualifiedName~CartelEleccionE2E|FullyQualifiedName~La_ventana_angosta"` contra el EXE publicado de la ampliación del 1.
  - A las 13:55, contra el EXE publicado a las 13:38 desde `718d8ed`: `error: 2`. La nueva, `Locator expected to have count '0'` en `Locator("#confirmar")` (había «Confirmar» sin opción elegida); la cambiada, `Locator expected to be in viewport` en `Locator("#cartel-eleccion #confirmar")`.
  - La nueva prueba también Escape con el foco en «Confirmar» del WebView2, que sube hasta la raíz: bUnit no lo ve.
  - La cambiada desplaza hasta la última opción en lugar de hasta el «Confirmar» del pie, la toca y mide «Confirmar» del cartel entero en la ventana, con la página donde quedó.
- [x] 2.2 Publicar con `pwsh .github/scripts/publicar-puesto.ps1 -Version 0.0.0-local` y verificar con el filtro de 2.1, con `INCOMEDOR_E2E_KIOSK_EXE` apuntando al EXE publicado, que pasan. Correr también `PresentacionTactilTests`: si `Nueve_opciones_en_1024_por_768_y_una_columna_en_600` falla, se para y se le pregunta a David (riesgo del design).
  - Publicación de 13:55 a 13:56, sobre `b712702` más las pruebas de 2.1: salió 0, build Release `-warnaserror` en `0 Advertencia(s)`, «publicación revisada: 482 entradas, ninguna con datos ni secretos», el E2E del flujo principal contra el EXE `total: 1`, `error: 0`, zip de 77.498.659 bytes (sha256 `84127f6b…30f00f09`).
  - El filtro de 2.1 más `PresentacionTactil`, en Release contra el EXE publicado, a las 13:57: `total: 6`, `error: 0`. `Nueve_opciones_en_1024_por_768_y_una_columna_en_600` pasó: a 1024 × 768, «9 opciones en 2 columnas, alto mínimo 56 px, última caja en 514 px, documento 768 px», y a 600, «documento 906 px», así que la página sigue desplazándose sin la barra y no hubo que parar.
  - Medidas del cartel a 1024 × 768: «cartel de 232,0 a 792,0 px y de 262,0 a 506,0 px», «Confirmar» de 607,0 a 767,0 px y «Elegir otra» de 435,0 a 595,0 px, los dos de 425,0 a 481,0 px y 56,0 px de alto. A 600: la página se desplazó 278,5 px hasta la última opción y quedó en 278,5 px con «Confirmar» del cartel entero a la vista.
  - Capturas miradas: `cartel-1024x768.png` (el cartel centrado con la opción, su cargo y los dos botones, «Cancelar» en el título y lo de atrás atenuado) y `seleccion-600.png` (el cartel entero en la ventana angosta).

## 3. Documentos y evidencia

- [x] 3.1 Actualizar el paso 1 del recorrido de `readme.md` («Almuerzo común» y «Confirmar» en el cartel) y sumar la evidencia a la fila que corresponda de `docs/aplicacion-del-curso.md`, sin enlace propio a la carpeta del change. Verificar con el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso`, con su control negativo, que hay 0 rotos.
  - El paso 1 del recorrido abre el cartel al tocar «Almuerzo común» y confirma en él; la evidencia va en la fila de componentes, estados de UI y accesibilidad, con el enlace a `CartelEleccionE2ETests` y sin enlace a la carpeta del change.
  - Chequeo de enlaces a las 13:58: el control negativo da `ROTO`, `ANCLA` y `enlaces rotos: 2`; `readme.md`, `AGENTS.md` y `docs/aplicacion-del-curso.md`, `enlaces rotos: 0`.
- [x] 3.2 Desde la raíz del repo, correr y pegar las salidas: `dotnet build INComedor.sln -warnaserror` con 0 advertencias, `dotnet test INComedor.sln` con 0 fallas, `openspec validate --all --strict` sin errores y el `git diff` de pruebas modificadas del encabezado, con sólo los tres archivos.
  - `dotnet build INComedor.sln -warnaserror`, a las 13:59: `0 Advertencia(s)`, `0 Errores`.
  - `dotnet test INComedor.sln -c Release`, con `INCOMEDOR_SQLSERVER=INMO-DW-AR` e `INCOMEDOR_E2E_KIOSK_EXE` en el EXE que publicó 2.2 (`git diff --stat b712702 HEAD -- src/` vacío: es el código de la punta), de 13:59 a 14:02: `total: 773`, `error: 0` (Contracts 23, Domain 91, Sync.Api 62, Sqlite 206, Kiosk 364 y E2E 27). Son las 767 de la base más las cinco de `CartelEleccionTests` y la de `CartelEleccionE2ETests`.
  - `openspec validate --all --strict`: `Totals: 17 passed, 0 failed (17 items)`.
  - `git diff --diff-filter=M --name-only d0da333 -- tests/`: `ConfiguracionPorPestanasE2ETests.cs`, `PantallaAtencionTests.cs` y `PantallaDistribucionTests.cs`, los tres del punto 3 del gate, y lo mismo contra `07a2329`.

## 4. Correcciones de la verificación

La verificación del 10/11 a las 15:48, sobre `6657f78` y sin críticos, dejó una advertencia y dos sugerencias; David eligió a las 15:50 «Corregir y archivar».

- [x] 4.1 Cubrir «Cancelar antes de confirmar» con el cartel cerrado por «Elegir otra» (la advertencia): `CartelEleccionTests.Cancelar_antes_de_confirmar_con_el_cartel_cerrado`, nueva. La conducta ya existía, así que se la vio fallar con una afirmación imposible revertida (`Expected: 1`, `Actual: 0` en las intenciones); con el filtro `FullyQualifiedName~CartelEleccion`, `total: 6`, `error: 0`.
- [x] 4.2 Decir en D3 de `design.md` que con el cartel abierto queda inerte todo el resto de la pantalla, cabecera, aviso y pie incluidos, como hace el código.
- La otra sugerencia, devolver el foco a la opción elegida al cerrar el cartel con «Elegir otra» o Escape, queda abierta en el borrador del PR.

## Workflow follow-up

- La revisión de David (el gate de `proposal.md`) va antes del apply, y lo que decida queda escrito en `proposal.md`.
- Después de su prueba en el ejecutable, con los tres cambios juntos, `/openspec-verify-change` y el archivado en el mismo PR, que apunta a `feature/simular-identificacion-por-lista-dw` y se integra después de ella. El push y el merge son de David.
