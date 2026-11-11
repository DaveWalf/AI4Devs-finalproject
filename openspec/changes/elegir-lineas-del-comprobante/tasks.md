# Tasks

Rama local `feature/mejoras-2-5-dw`, en el worktree `C:\AI\INComedor\INComedor-AI4DEVS-mejoras`, sobre `d6bbb24` (la
mejora 1 aplicada). Gate respondido por David el 11/11/2026 (proposal). Cada grupo cierra con
`git diff --diff-filter=M --name-only d6bbb24 -- tests/` pegado, que tiene que salir vacío: este change no prevé tocar
pruebas existentes. Cada tarea que toca una pantalla sigue la regla de prolijidad de `config.yaml`. **Se archiva después
de `reimprimir-sin-codigo`**, porque su bloque de «Recuperar o reimprimir sin tocar el consumo» parte del de aquél.

## 1. Las pruebas existentes

- [x] 1.1 Confirmar que ninguna prueba existente cambia. Las del papel y del comprobante en pantalla (también las que
  agregó la mejora 1, como `ConsumosSinCodigoTests` y la marca SIN VALIDEZ) corren con las tres elecciones en
  «Imprimir», que es el valor por defecto. `ResguardoEnGeneralTests` escribe un `impresora.json` sin las elecciones, que
  sigue siendo válido. Verifica: `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde después del grupo 3, y
  el `git diff --diff-filter=M` del encabezado vacío. Si alguna prueba existente se pone roja, se para y se le pide a
  David la aprobación de esa prueba.
  Hecho el 11/11/2026: `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` 621 de 621 después del grupo 3 (también `ConsumosSinCodigoTests`, `MarcaSinValidezTests` y `ResguardoEnGeneralTests`), y `git diff --diff-filter=M --name-only d6bbb24 -- tests/` vacío.

## 2. Las elecciones en `impresora.json`

- [x] 2.1 Pruebas nuevas en `Kiosk.Tests` (`LineasDelComprobanteTests`), vistas fallar antes: un archivo sin las
  elecciones da las tres en `true`; uno con «comensal» igual a `"no"` da el valor por defecto entero; guardar y releer
  conserva las cinco; `Serializar` escribe las cinco. Verifica: el filtro de la clase, en rojo antes y en verde después.
  Hecho: en `LineasDelComprobanteTests`, el archivo sin elecciones (las tres en `true`), tres elecciones que no son booleanos JSON (`"no"`, `0` y `null`: el valor por defecto entero), guardar y releer las cinco, y `Serializar` con los cinco campos en orden. Vistas fallar antes (salvo la del archivo sin elecciones, que ya pasaba por los valores por defecto del record) y en verde después.
- [x] 2.2 D1 en `TextosComprobante.cs`. Verifica: 2.1 en verde y `TextosComprobanteTests` sin cambios y en verde.
  Hecho: `TextosComprobante` suma `Comensal`, `AvisoReimpresion` y `Operador` con valor por defecto `true`; `Interpretar` las lee opcionales y toma el archivo entero como fuera de forma si una no es booleana; `Serializar` escribe las cinco. 2.1 en verde y `TextosComprobanteTests` 12 de 12 sin cambios.

## 3. La pestaña Impresora, el papel y la pantalla

- [x] 3.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes, una por escenario del delta:
  - «Elegir qué imprime»: el aviso, `TICKET_TEXTS` con el antes y el después, y la elección que sigue al reabrir;
  - «Un archivo de textos de antes»;
  - «Una elección fuera de forma»;
  - «Un comprobante sin el comensal»: el papel y la pantalla sin la línea, y «Ver» con el nombre;
  - «Una copia sin aviso ni operador»: «Copia: 1», sin REIMPRESIÓN, «ORIGINAL» ni «Habilitó:»;
  - «La marca de la clave del día no se apaga».
  Hecho: las seis de pantalla en `LineasDelComprobanteTests` (las del escenario «Una copia sin aviso ni operador» con `9001` y «La marca de la clave del día no se apaga» con la clave del día), 5 en rojo antes y 12 de 12 en verde después. Al aplicar se vio que el delta tenía un `## REMOVED Requirements` suelto que hacía que OpenSpec descartara esos dos escenarios del requisito «Recuperar o reimprimir sin tocar el consumo» (8 parseados de 10); se quitó el encabezado, sin cambiar ningún texto aprobado, y el requisito quedó con sus 10 (`openspec show --json --deltas-only`).

  Verifica: en rojo antes y en verde después.
- [x] 3.2 D2 en `PanelConfiguracion.razor` y `PantallaAtencion.razor` (el guardado y `TICKET_TEXTS`), y D3 en
  `ImpresoraSimulada.cs` y `ComprobanteEnPantalla.cs`. Verifica: 3.1 en verde y
  `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde.
  Hecho: los tres pares en la sección de textos de Impresora (`#comensal-imprimir`, `#comensal-no-imprimir` y sus hermanos), guardados por `#guardar-textos` con un solo `TextosComprobante`; `TICKET_TEXTS` con `PrintPersonName`, `PrintReprintNotice` y `PrintOperator` antes y después; el papel y el comprobante de la confirmación siguen las elecciones y «Ver» las usa todas. 3.1 en verde y Kiosk.Tests 621 de 621.
- [x] 3.3 Prolijidad. Por CDP, a 1024 × 768, en la pestaña Impresora se miden los bordes izquierdos de los rótulos y de
  los tres pares de botones, y el ancho, el alto y la letra de los seis botones, contra los de `#correos-activos` y
  `#correos-apagados` (tolerancia de 1 px). Queda la captura `impresora-lineas-1024x768`. Va en una prueba E2E nueva,
  `LineasDelComprobanteE2ETests`. Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter-class
  *LineasDelComprobante*`, en primer plano, con las medidas pegadas de `registro.txt`.
  Hecho: `LineasDelComprobanteE2ETests` en verde, en primer plano. Medidas de `registro.txt`: los cinco rótulos en x 49,0 con letra 17 px; los primeros botones de los tres pares en x 273,5, como el campo del pie; los seis botones de 162,0 × 56,0 px, letra 18 px y radio 12 px, con el borde del estado presionado igual al de Mails; con «No imprimir» presionado en los tres, el mismo ancho. Mails mide 120,0 y 130,2 px de ancho y 56 px de alto: sus dos botones no comparten ancho (lo da el texto), así que se mide que los seis de las líneas comparten el suyo (`min-width: 9em`, el de «No imprimir» en negrita) y no son más angostos que Mails, con el alto, la letra y el borde de Mails. Capturas `impresora-lineas-1024x768` y `impresora-lineas-no-imprimir-1024x768`.

## 4. Documentación y verificación completa

- [x] 4.1 `readme.md`: las tres elecciones de Impresora, y que la marca de la clave del día no se apaga. Verifica:
  `git grep -n "No imprimir" -- readme.md` con la línea nueva.
  Hecho: `readme.md` describe las tres elecciones, que «Ver» las muestra siempre, la copia sin aviso con su «Copia: N» y que la marca de la clave del día no se apaga. `git grep -n "No imprimir" -- readme.md` da la línea 490.
- [ ] 4.2 La verificación completa: `dotnet build INComedor.sln -warnaserror`, `dotnet test INComedor.sln` y
  `openspec validate --all --strict`, sin errores. Verifica: las tres salidas pegadas, con el total de pruebas.
  Se cierra con la corrida única del final (§6.6 del plan del goal): pendiente hasta después del quinto change.
- [x] 4.3 La evidencia: la fila nueva o actualizada en `docs/aplicacion-del-curso.md` (el pedido de David, con la
  distinción entre operador y técnico de su respuesta 3) y, si deja un workflow o un ajuste humano nuevo, su entrada en
  `prompts.md`. Verifica: el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso`, con su control
  negativo, `enlaces rotos: 0`.
  Hecho: la fila «No reemplazar la revisión humana por el revisor del agente» de `docs/aplicacion-del-curso.md` suma la respuesta 3 del gate (operador frente a técnico). No deja un workflow ni un ajuste humano nuevo para `prompts.md`. Chequeo de D8: `enlaces rotos: 0`; control negativo `enlaces rotos: 2`.

## Workflow follow-up

- Archivar después de `reimprimir-sin-codigo`; `/openspec-verify-change elegir-lineas-del-comprobante` y su salida en la
  descripción del PR. Push, PR y merge a `final-project-dw` son de David.
