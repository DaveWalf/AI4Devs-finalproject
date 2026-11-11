## 1. La presentación de las opciones

- [x] 1.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes: `AlmacenPresentacionOpciones` guarda y lee el color, la
  imagen (por su huella, en `opciones\`), la transparencia y el precio visible de una opción; descarta un color fuera de
  la paleta, una transparencia fuera de los cuatro pasos y una imagen que no existe; sin archivo, ninguna opción
  personalizada (D3). Verifica: rojo antes y verde después.
  - `PresentacionOpcionesTests` (5) contra el esqueleto: `total: 5`, `error: 5` (`NotImplementedException`).
- [x] 1.2 `PresentacionOpciones` y `AlmacenPresentacionOpciones`, registrado en la ventana. Verifica: 1.1 en verde.
  - Escritura atómica como el estilo visual; la imagen se revalida con las reglas de los logos al leerla, y un nombre que
    no es una huella con su extensión (por ejemplo `..\..\secreto.png`) no se abre. `total: 5`, `error: 0`.

## 2. La pestaña Opciones

- [x] 2.1 Pruebas nuevas, vistas fallar antes: el catálogo a la vista con todas sus opciones; «Editar» en una opción abre
  la edición en el lugar y enfoca su nombre; «Personalizar» con un color y 25 %, con una imagen válida y con una que no
  lo es, con el precio oculto y «Cancelar»; `OPTION_STYLE_CHANGED` en la general con la huella y sin el contenido (D1,
  D2 y D5). Verifica: rojo antes y verde después.
  - `OpcionesEnElLugarTests` (6) contra el almacén y `MensajesAtencion.PresentacionGuardada`: `total: 6`, `error: 6` (sin
    `#catalogo-opciones`). La primera corrida en verde contó 15 y no 9: el puesto de prueba siembra el catálogo de
    quince; la prueba y el escenario cuentan las del catálogo activo.
- [x] 2.2 `#catalogo-opciones`, el cartel `#personalizar-opcion` y el registro. Verifica: 2.1 en verde y `Kiosk.Tests`
  entero en verde, con las pruebas existentes que cambien listadas con su porqué.
  - El catálogo con su muestra (`PresentacionOpcion.EstiloCss` y `Clase`, compartidas con la atención), «Editar» que abre
    `PanelOpciones` con el foco en el nombre (por `data-indice`, sin `@ref`), el cartel con nueve fondos, la imagen con
    `LogosLocales.Validar`, cuatro pasos y el precio, y `OPTION_STYLE_CHANGED`. `Kiosk.Tests`: `total: 475`, `error: 0`;
    build con `-warnaserror` sin advertencias. Prueba existente cambiada (decisión 13): `PuntoFinalTests`, que suma
    `PresentacionGuardada` con su ejemplo; `PanelOpcionesTests` y las demás de Opciones, sin cambios.

## 3. La atención

- [x] 3.1 Pruebas nuevas, vistas fallar antes: la tarjeta personalizada con su fondo y su transparencia, las demás como
  siempre; con el precio oculto, ni la tarjeta ni el cartel muestran importe y el comprobante sí (D4). Verifica: rojo
  antes y verde después.
  - `AtencionPersonalizadaTests` (2): `total: 2`, `error: 2` (la tarjeta sin su clase y con su cargo). El comprobante se
    prueba con `ImpresoraDePrueba`: `EmployeeChargeMinor` es el importe de la opción.
- [x] 3.2 La tarjeta y el cartel de `PantallaAtencion` y `puesto.css`. Verifica: 3.1 en verde y `Kiosk.Tests` entero.
  - La tarjeta con `PresentacionOpcion.Clase` y `EstiloCss` y el cargo de `CargoVisible`, igual en el cartel; la máquina
    y el comprobante no cambian. `Kiosk.Tests`: `total: 477`, `error: 0`.

## 4. El puesto compilado, documentos y verificación integral

- [x] 4.1 E2E nueva a 1024 × 768: el catálogo a la vista, el cartel con sus colores y sus pasos de transparencia medidos,
  una opción personalizada y otra con el precio oculto en la atención, con capturas miradas (D6). Verifica: `dotnet test
  tests/Intelektron.INComedor.E2E.Tests` en verde.
  - `PersonalizarOpcionesE2ETests`: tarjetas del catálogo de 295,7 px, sus botones de 130,8 × 56 px con la letra de
    18 px de «Cerrar»; el cartel con 9 fondos de 95,6 × 56 px y 4 pasos de 121,5 × 56 px, que termina en 713 de 768; en
    la atención, «Almuerzo común» con el fondo `rgb(221, 176, 137)` a 0,75 y otra opción sin precio. Dos tropiezos de la
    prueba, corregidos: tipear el código con el diálogo todavía abierto (la atención es inerte hasta que se va) y elegir
    la misma opción para el fondo y el precio oculto. Las capturas mostraron el precio partido en la muestra angosta:
    `nowrap`. Capturas miradas: el catálogo, el cartel y la atención.
- [x] 4.2 `readme.md` (Opciones) y la fila que corresponda de `docs/aplicacion-del-curso.md`. Verifica: el chequeo de
  enlaces de D8 de `mostrar-aplicacion-del-curso` con su control negativo, `enlaces rotos: 0`.
  - `readme.md` (Opciones) y la fila de OWASP del mapa (la presentación leída campo por campo). Enlaces de los dos y del
    change: `enlaces rotos: 0`; el control negativo, `enlaces rotos: 2`.
- [ ] 4.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror`, el puesto
  publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c Release` contra ese EXE, con
  los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.

## 5. Correcciones de la verificación

- [x] 5.1 Lo que marcó el agente de sólo lectura sobre `e799f27` (1 crítico, 12 advertencias y 10 sugerencias), con
  pruebas nuevas vistas fallar antes. Verifica: `Kiosk.Tests` entero en verde y la E2E del change.
  - La spec: los tres requisitos vivos de la atención que decían «su cargo informado» (el catálogo, el cartel y la
    reconfirmación) pasan a MODIFIED con la excepción del precio oculto, sin tocar sus escenarios; se suman la
    reconfirmación con el precio oculto, la imagen que ya no está, el guardado fallido e INWeb como dueño.
  - La reconfirmación por cambio de precio ya no muestra el cargo de una opción con el precio oculto (opción 6 del gate,
    para mostrarle a David).
  - La bitácora pasa a `CONFIGURATION_CHANGED` del ajuste `OPTION_STYLE`, como el estilo y los logos, con el antes, el
    después, el resultado (`APPLIED` o `FAILED`) y lo vigente; un guardado fallido deja el cartel abierto con su aviso
    (`#aviso-presentacion`) y se registra.
  - Un archivo inválido elegido después de uno válido descarta también el válido, como un logo. El almacén: la imagen se
    lee con tope y con su huella comprobada, una vez; el archivo de presentación, hasta 1 MB; el nombre con `\z`; el
    estilo de cada opción, armado una vez por guardado (con una imagen pesa megabytes).
  - La pantalla: un halo del papel en el texto de las tarjetas personalizadas, en lugar del respaldo con relleno que lo
    corría 6 px; la transparencia deshabilitada sin fondo y «Mostrar el precio» sólo con cargo; el foco vuelve a
    «Personalizar» al cerrar el cartel.
  - Pruebas: 9 vistas fallar contra `e799f27` (la huella y el tope del almacén; la reconfirmación; el registro en cinco
    pruebas de la pestaña; el guardado fallido; la imagen inválida después de la válida) y las que fijan lo que ya andaba
    (la ruta con `..` con un PNG válido plantado, «Editar» y «Guardar» con una sola versión, reabrir Configuración, Escape,
    INWeb, la imagen que ya no está, el JSON roto con la atención). `Kiosk.Tests`: `total: 486`, `error: 0`. La E2E suma
    el foco real en el nombre después de «Editar», una imagen, 75 % de transparencia y las tarjetas de la atención
    medidas: diez de 486 × 64 px, con el nombre desde 41 o 535 px según la columna; captura mirada.
  - Quedan sin cambiar, con su porqué: las imágenes sin uso en `opciones\` no se borran (son pocas y chicas en la
    demostración); la muestra se ve sobre el diálogo y no sobre el fondo de la atención; el peso de diez imágenes de 2 MB
    no se midió.
