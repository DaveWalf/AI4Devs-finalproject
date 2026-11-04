# Tasks

Se aplica con `opus` · `medium`: es un plan diseñado con sus verificadores, pero cruza la máquina de estados, Razor con su hoja de estilos, la semilla SQL y la ventana real, y la composición para 1024 × 768 pide juicio visual. `sonnet` · `medium` puede quedarse en una capa. Los comandos corren desde la raíz del repo y su salida se pega en la tarea. Al cerrar cada grupo hay un commit por rutas, en español, con `git commit -F <archivo>`.

Las pruebas existentes no se tocan, salvo las tres de D1 que aprobó David en el gate. `git diff --stat -- tests/` sólo puede mostrar archivos nuevos y esos dos archivos (`MaquinaAtencionTests.cs` y `PantallaAtencionTests.cs`). Las pruebas nuevas van en archivos nuevos. Si otra tarea necesitara cambiar una prueba existente, se para y se le pregunta a David. Los chequeos de enlaces relativos y del mapa son los de D8 en `openspec/changes/archive/2026-10-29-mostrar-aplicacion-del-curso/design.md`.

## 1. Catálogo sintético de nueve opciones

- [x] 1.1 Crear `tests/Intelektron.INComedor.Kiosk.Tests/CatalogoNueveTests.cs` con los escenarios del delta de `modo-demostracion`: `nueve`, ` NUEVE ` y `9` en la variable, ausente, base nueva con nueve opciones y tres en cero, base ya sembrada sin cambios, y `0001` abriendo la selección con el catálogo de nueve. Verificar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~CatalogoNueveTests"` que fallan o no compilan antes de implementar, y pegar la salida
  ```
  CatalogoNueveTests.cs(30,53): error CS0117: 'EntornoDemostracion' no contiene una definición para 'VariableCatalogo'
  CatalogoNueveTests.cs(32,64): error CS0117: 'EntornoDemostracion' no contiene una definición para 'VariableCatalogo'
  CatalogoNueveTests.cs(33,37): error CS1061: "EntornoDemostracion" no contiene una definición para "CatalogoNueve" …
  CatalogoNueveTests.cs(39,91): error CS1739: La mejor sobrecarga para 'ServicioAtencion' no tiene un parámetro denominado 'catalogoNueve'
  ```
  ` NUEVE ` con espacios cuenta como valor desconocido («sólo con `nueve` sin espacios alrededor», D6).
- [x] 1.2 Partir la semilla en `semilla_atencion_comun.sql`, `opciones_quince.sql` y `opciones_nueve.sql`, con las ocho opciones de la tabla de D6; sumar `VariableCatalogo` y `CatalogoNueve` a `EntornoDemostracion` y pasarlo de `MainWindow` a `ServicioAtencion` y `SemillaAtencion` (D6). Verificar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` que pasan las nuevas y todas las anteriores, incluidas `SemillaAtencionTests` sin cambios
  ```
  Intelektron.INComedor.Kiosk.Tests.dll (net11.0|x64) correcto [+92/x0/?0] (5s 348ms)
    error: 0
    correcto: 92
  ```

## 2. Distribución por cantidad y cargo cero

- [x] 2.1 Crear `tests/Intelektron.INComedor.Kiosk.Tests/DistribucionCatalogoTests.cs` (máquina: `ColumnasPagina` y `TotalPaginas` con 8, 9, 10 y 15 opciones; 15 en una página de diez y otra de cinco; `MensajesAtencion.Cargo` con importe cero devuelve vacío) y `PantallaDistribucionTests.cs` (bUnit: la lista lleva una o dos columnas según la cantidad, sin «Anterior» ni «Siguiente» hasta diez; nueve con las cinco primeras en la primera columna; la opción tocada dice «Elegida» y la marca se mueve al tocar otra; la opción sin cargo no muestra importe y «Confirmar» queda habilitado; con nueve, cada opción, «Cancelar» y «Confirmar» son botones sin `tabindex` negativo). Los catálogos de 8 y 10 se arman sobre la semilla de nueve, borrando o agregando una opción en la base temporal (D9). Verificar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~Distribucion"` que fallan antes de implementar, y pegar la salida
  ```
  DistribucionCatalogoTests.cs(26,40): error CS1061: "MaquinaAtencion" no contiene una definición para "ColumnasPagina" …
  DistribucionCatalogoTests.cs(40,33): error CS1061: "MaquinaAtencion" no contiene una definición para "ColumnasPagina" …
  DistribucionCatalogoTests.cs(45,33): error CS1061: "MaquinaAtencion" no contiene una definición para "ColumnasPagina" …
  ```
  Los catálogos de 8 y 10 los arma `CatalogoDePrueba.cs`, también nuevo.
- [x] 2.2 Cambiar las tres pruebas existentes de D1, y nada más, con la aprobación de David en el gate: en `MaquinaAtencionTests.Recorre_las_15_opciones_paginadas`, `Assert.Equal(3, maquina.TotalPaginas)` a `2`; en `PantallaAtencionTests.Controles_de_la_seleccion_son_botones`, `6 + 4` a `10 + 4`; en `PantallaAtencionTests.Codigo_vigente_muestra_el_nombre_y_el_catalogo`, `Assert.Equal(6, …)` a `10`. Verificar con `git diff -- tests/` que el diff toca sólo esas tres líneas, y pegarlo
  ```diff
  --- a/tests/Intelektron.INComedor.Kiosk.Tests/MaquinaAtencionTests.cs
  @@ -49,7 +49,7 @@ public class MaquinaAtencionTests : IDisposable
  -        Assert.Equal(3, maquina.TotalPaginas);
  +        Assert.Equal(2, maquina.TotalPaginas);
  --- a/tests/Intelektron.INComedor.Kiosk.Tests/PantallaAtencionTests.cs
  @@ -43,7 +43,7 @@ public class PantallaAtencionTests : IDisposable
  -        Assert.Equal(6 + 4, controles.Count);
  +        Assert.Equal(10 + 4, controles.Count);
  @@ -62,7 +62,7 @@ public class PantallaAtencionTests : IDisposable
  -        Assert.Equal(6, pantalla.FindAll(".opcion").Count);
  +        Assert.Equal(10, pantalla.FindAll(".opcion").Count);
   2 files changed, 3 insertions(+), 3 deletions(-)
  ```
- [x] 2.3 Implementar D1 y D2: `TamanoPagina = 10`, `ColumnasPagina`, controles de página sólo con más de una página, la grilla por columnas con la consulta de contenedor, la marca «Elegida» y el cargo vacío para importe cero. Verificar con `dotnet test INComedor.sln` que pasan las nuevas, las tres cambiadas y el resto, y con `git diff --stat -- tests/` que sólo aparecen los archivos nuevos y los dos de 2.2
  ```
  Contracts.Tests correcto [+23/x0/?0] · Domain.Tests [+68/x0/?0] · Sync.Api.Tests [+62/x0/?0]
  Kiosk.Tests [+105/x0/?0] · Infrastructure.Sqlite.Tests [+117/x0/?0] · E2E.Tests [+6/x0/?0] (1m 48s)
    total: 381 · error: 0 · correcto: 381
  git diff --stat -- tests/
   tests/Intelektron.INComedor.Kiosk.Tests/MaquinaAtencionTests.cs  | 2 +-
   tests/Intelektron.INComedor.Kiosk.Tests/PantallaAtencionTests.cs | 4 ++--
  ?? CatalogoDePrueba.cs, DistribucionCatalogoTests.cs, PantallaDistribucionTests.cs (nuevos)
  ```

## 3. Aspecto, cabecera y ventana

- [x] 3.1 Crear `tests/Intelektron.INComedor.Kiosk.Tests/CabeceraTests.cs` (bUnit: en reposo, un solo botón en la cabecera, con el nombre «Configuración»; ninguno durante la selección; los logos de ejemplo con su texto alternativo; el pie con la marca Intelektron y la franja de sincronización con su rol y su nombre) y `tests/Intelektron.INComedor.E2E.Tests/PresentacionTactilTests.cs` (ventana real con `INCOMEDOR_CATALOGO=nueve`: la ventana maximizada según `IsZoomed`; en un área de 1024 × 768 fijada por CDP, las nueve opciones, «Cancelar» y «Confirmar» dentro de 768 y de al menos 56 px de alto; en 600 de ancho, una columna con el mismo tamaño de letra; con `prefers-reduced-motion: reduce`, el comprobante sin animación) (D8, D9). Verificar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~CabeceraTests"` y `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter "FullyQualifiedName~PresentacionTactilTests"` que fallan antes de implementar, y pegar las dos salidas
  ```
  CabeceraTests: error: 4 · correcto: 0 (los cuatro casos)
  PresentacionTactilTests: error: 3 · correcto: 0
    Abre maximizada: la ventana no está maximizada según IsZoomed
    1024 × 768: 9 opciones en 2 columnas, alto mínimo 50 px, última caja en 635 px, documento 812 px, letra 20px
    Movimiento reducido: Locator expected to be visible (.comprobante)
  ```
  El E2E lanza el puesto desde la prueba nueva con `INCOMEDOR_CATALOGO=nueve`, porque `EscenarioE2E.LanzarPuesto` quita
  las variables `INCOMEDOR_*` heredadas y no se modifica; lo cierra con `DetenerAsync` del escenario.
- [x] 3.2 Implementar D3 y D8: `wwwroot/css/puesto.css` enlazada desde `index.html`, el fondo `wwwroot/img/fondo-cocina.jpg` copiado de `C:\AI\INComedor\investigacion\estilos-graficos-20261103\cocina-sonrisas\fondo-chefs.jpg`, `wwwroot/img/PROCEDENCIA.md` con el prompt de `PROMPT-IMAGEN.txt` de esa carpeta y el origen de la marca, `intelektron.svg` copiado de `C:\AI\INComedor\investigacion\interfaces-comedor-20261101\opcion-4-20261102\marcas-20261103\intelektron.svg`, los dos logos de ejemplo dibujados para el repo, las variables CSS del estilo, el engranaje sólo en reposo, el rediseño de reposo, selección, reconfirmación, resultado y asistencia, y `WindowState="Maximized"`. Verificar con `dotnet build INComedor.sln -warnaserror`, con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde y con `CabeceraTests` en verde; las partes del E2E que dependen de los grupos 5 y 6 pueden seguir en rojo hasta entonces
  ```
  dotnet build INComedor.sln -warnaserror → Compilación correcta.
  Kiosk.Tests → error: 0 · correcto: 109 (incluidas las cuatro de CabeceraTests)
  E2E.Tests → error: 1 · correcto: 8; sigue en rojo «Movimiento reducido» (comprobante en pantalla, grupo 6)
    1024 × 768: 9 opciones en 2 columnas, alto mínimo 56 px, última caja en 580 px, documento 768 px, letra 20px
    600: 9 opciones en 1 columnas, alto mínimo 56 px, última caja en 946 px, documento 1139 px, letra 20px
  ```
  Captura de 1024 × 768 mirada: chefs a los costados, texto opaco, nada cortado; la franja de la impresora todavía
  está en la atención hasta el grupo 5.

## 4. Logos locales

- [x] 4.1 Crear `tests/Intelektron.INComedor.Kiosk.Tests/LogosLocalesTests.cs` con los escenarios del requirement de logos y los bordes de D4: PNG, WebP y SVG válidos devuelven una URI `data:` de su tipo; 2 MB exactos se aceptan y 2 MB más un byte no; un `.png` con texto se rechaza; un SVG con DTD se rechaza; un SVG con `script` se entrega como imagen y el marcado de la pantalla no contiene `<script`; con `.png` y `.svg` gana el `.png`; sin carpeta `marcas`, los de ejemplo. Verificar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~LogosLocalesTests"` que fallan o no compilan antes de implementar, y pegar la salida
  ```
  LogosLocalesTests.cs(37,25): error CS0246: El nombre del tipo o del espacio de nombres 'LogosLocales' no se encontró
  LogosLocalesTests.cs(40,22): error CS0103: El nombre 'LogosLocales' no existe en el contexto actual
  LogosLocalesTests.cs(49,25): error CS0246: El nombre del tipo o del espacio de nombres 'LogosLocales' no se encontró
  ```
  Suma un caso de borde: un SVG mal formado o con otra raíz se rechaza.
- [x] 4.2 Implementar `LogosLocales` y usarlo en la cabecera (D4). Verificar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde y revisar con OWASP A03, A05 y A08 la lectura del archivo; anotar el resultado en la tarea
  ```
  Kiosk.Tests → error: 0 · correcto: 118
  ```
  Revisión OWASP de `LogosLocales`:
  - **A03 (inyección)**: el SVG nunca entra al DOM; va como URI `data:` en el `src` de un `img`, donde el navegador no
    ejecuta scripts. Razor codifica el atributo. Lo prueba «Un SVG con un script se muestra como imagen».
  - **A05 (configuración insegura)**: las rutas son fijas (`marcas\empresa.*`, `marcas\concesionaria.*`) bajo la carpeta
    de datos, que sólo acepta una ruta absoluta; nada del archivo arma una ruta. El XML se lee con
    `DtdProcessing.Prohibit` y sin `XmlResolver`, sin entidades ni recursos externos. El tope de 2 MB se mide antes y
    durante la lectura, con un búfer acotado.
  - **A08 (integridad)**: no hay red ni descarga; el contenido se valida contra su extensión (firma PNG, `RIFF`/`WEBP`,
    raíz `svg`) y un archivo inválido deja el de ejemplo, registrado en la consola de depuración, sin cortar la atención.
  - Queda abierto, fuera de este change: quien escribe en la carpeta de datos del puesto ya controla la base; un enlace
    simbólico en `marcas\` lee otro archivo, que sólo se muestra si pasa la misma validación.

## 5. Configuración: estilo visual e impresora simulada

- [x] 5.1 Crear `tests/Intelektron.INComedor.Kiosk.Tests/AlmacenEstiloVisualTests.cs` (sin archivo: 76 y 4; guardar y volver a leer; opacidad 150 o desenfoque -3: 76 y 4; JSON roto: 76 y 4; después de guardar no queda el archivo temporal) y `PanelConfiguracionTests.cs` (bUnit: el engranaje abre un diálogo modal; los rangos van de 72 a 88 y de 0 a 12; mover un rango cambia la variable CSS al instante; «Guardar» escribe el archivo; «Cancelar» y Escape restauran y no escriben; la sección «Impresora simulada» aparece sólo con `ImpresoraSimulada`; elegir «Sin papel» y confirmar deja el comprobante pendiente; la atención no muestra los botones de modo). Verificar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~EstiloVisual|FullyQualifiedName~PanelConfiguracion"` que fallan o no compilan antes de implementar, y pegar la salida
  ```
  AlmacenEstiloVisualTests.cs(31,26): error CS0246: El nombre del tipo o del espacio de nombres 'AlmacenEstiloVisual' no se encontró
  AlmacenEstiloVisualTests.cs(33,26): error CS0246: El nombre del tipo o del espacio de nombres 'EstiloVisual' no se encontró
  AlmacenEstiloVisualTests.cs(34,22): error CS0103: El nombre 'EstiloVisual' no existe en el contexto actual
  ```
  Suman bordes: los extremos 72/0 y 88/12 se aceptan, y un valor como texto (`"80"`) o un JSON cortado dan 76 y 4.
- [x] 5.2 Implementar D5: `EstiloVisual`, `AlmacenEstiloVisual` con escritura por archivo temporal, `PanelConfiguracion.razor` con sus dos secciones, y la franja de impresora fuera de la atención, con los mismos `id` dentro del panel. Verificar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde y con `git diff --stat -- tests/` que no cambió ninguna prueba existente en este grupo
  ```
  Kiosk.Tests → error: 0 · correcto: 137
  git diff --stat -- tests/ → (vacío: ninguna prueba existente cambió en este grupo)
  ?? tests/Intelektron.INComedor.Kiosk.Tests/AlmacenEstiloVisualTests.cs
  ?? tests/Intelektron.INComedor.Kiosk.Tests/PanelConfiguracionTests.cs
  ```
  El modo de la impresora aplica al tocarlo, como antes en la franja; «Cancelar» restaura sólo el estilo visual.

## 6. Comprobante en pantalla

- [ ] 6.1 Crear `tests/Intelektron.INComedor.Kiosk.Tests/ComprobanteEnPantallaTests.cs` (bUnit con `ImpresoraSimulada` sobre una carpeta temporal y `RelojManual`): autorizado en «Disponible» muestra la impresora y el comprobante con la opción y el cargo, sin el nombre; tocar el comprobante vuelve a reposo; «Sin papel» e «Incierta» no muestran comprobante; un rechazo `LOCAL_LIMIT_REACHED` no muestra la impresora; a los 10 s vuelve a reposo; con `ImpresoraDePrueba` queda el botón «Listo» de siempre. Verificar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~ComprobanteEnPantallaTests"` que fallan antes de implementar, y pegar la salida
- [ ] 6.2 Implementar D7: el último comprobante en `ImpresoraSimulada`, `ComprobanteEnPantalla` en la máquina, la impresora y el comprobante con `id="listo"` en el resultado, y la animación anulada por `prefers-reduced-motion`. Verificar con `dotnet test INComedor.sln` en verde, incluido `PresentacionTactilTests`, y con `git diff --stat -- tests/` que sólo aparecen los archivos nuevos y los dos de 2.2

## 7. La ventana real

- [ ] 7.1 Correr el puesto con `INCOMEDOR_CATALOGO=nueve` en una carpeta nueva de `INCOMEDOR_DEMO_DIR` y sin ella, y sacar por CDP capturas de 1024 × 768 del reposo, la selección de nueve, la primera página de quince y el comprobante en pantalla, guardadas en `docs/capturas/`. Mirarlas y corregir lo que no coincida con D3 (fondo a los costados, texto opaco, nada cortado). Verificar con `dotnet test INComedor.sln` completo en verde y pegar los totales por proyecto
- [ ] 7.2 Correr `dotnet build INComedor.sln -warnaserror`, `openspec validate --all --strict` y `git diff --stat -- tests/`, y pegar las tres salidas

## 8. Tallas y evidencia

- [ ] 8.1 Llevar a la tabla de tallas de `docs/plan-academico.md` la talla de David que quedó en el gate y la de la IA (M, 4 a 6 h, 5,7 a 8,6 h con el colchón), con las horas reales del apply medidas con el reloj de la sesión. Verificar con el chequeo del mapa (`defectuosas: 0`)
- [ ] 8.2 Agregar la evidencia del change (D10): en `readme.md`, «Cómo ver la demostración» con los logos locales, el catálogo de nueve, Configuración sin clave en este corte y las capturas de 7.1; `INCOMEDOR_CATALOGO` en `.env.example`; en `docs/aplicacion-del-curso.md`, la fila del mapa que corresponda con este change; en `docs/cobertura-entrega.md`, la funcionalidad de atención con su aspecto táctil; y en el §5 de `prompts.md`, la maqueta en Codex como paso previo a la spec y la aprobación de las tres pruebas de D1. Verificar con el chequeo de enlaces relativos sobre esos documentos (`enlaces rotos: 0`) y con `git status --short`, donde sólo aparecen esos documentos, las capturas y este `tasks.md`

## Workflow follow-up

- `/openspec-verify-change` con su salida en la descripción del PR.
- Archivar con `openspec archive presentar-atencion-tactil -y`, que crea la capability `presentacion-puesto` y modifica `atencion-puesto`, `comprobante-local` y `modo-demostracion`. Después, correr `openspec validate --all --strict`.
- El PR hacia `final-project-dw` se abre sólo si David decide que va bien («si vemos que va bien, lo incorporamos a la entrega final»). Push, PR y merge son de David.
