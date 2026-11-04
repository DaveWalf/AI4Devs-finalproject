# Tasks

Las pruebas existentes no se tocan, salvo `CabeceraTests.En_reposo_un_solo_boton_configuracion`, que aprobó David en el gate (D3). `git diff --diff-filter=M --stat 2ca6559 -- tests/` sólo puede mostrar `CabeceraTests.cs` y, si la prueba E2E nueva va en ese archivo, `E2E.Tests/PresentacionTactilTests.cs` con sólo líneas agregadas. Cada prueba nueva se ve fallar antes de implementar. Si otra tarea necesitara cambiar una prueba existente, se para y se le pregunta a David.

## 1. El engranaje en el pie

- [x] 1.1 Cambiar `CabeceraTests.En_reposo_un_solo_boton_configuracion` como dice D3, y agregar en un archivo nuevo de `Kiosk.Tests` dos casos bUnit: en reposo, dentro de `footer`, la marca Intelektron va antes del engranaje y son sus dos últimos elementos; en la selección, el pie tiene `span.lugar-engranaje` y ningún botón. Verlos fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~Cabecera|FullyQualifiedName~PieEngranaje"` antes de tocar el código, y pegar la salida
  Rojas antes de tocar el código (`dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~Cabecera|FullyQualifiedName~PieEngranaje"`, 04/11 20:4x, sobre `0d15c2d`):
  ```
  con errores presentacion-puesto · El engranaje en el borde inferior derecho: la cabecera sin botones y el pie con uno, «Configuración»
    Assert.Empty() Failure: Collection was not empty   (CabeceraTests.cs:29)
  con errores presentacion-puesto · El engranaje en el borde inferior derecho: en reposo, dentro del pie, la marca Intelektron va antes del engranaje y son lo último del pie
    Assert.Single() Failure: The collection was empty   (PieEngranajeTests.cs:32)
  con errores presentacion-puesto · El pie no cambia de alto al salir de reposo: en la selección el pie tiene el lugar reservado del engranaje y ningún botón
    Assert.NotNull() Failure: Value is null            (PieEngranajeTests.cs:49)
    total: 6 · error: 3 · correcto: 3
  ```
  Las tres que fallan son la cambiada y las dos nuevas; `Sin_engranaje_durante_la_seleccion`, `Logos_de_ejemplo_con_texto_alternativo` y `Pie_con_la_marca_y_la_sincronizacion` siguen en verde.
- [x] 1.2 Mover en `PantallaAtencion.razor` el botón y su lugar reservado de la cabecera al pie, en el orden de D2, y ajustar `puesto.css` (pie de 56 px, grupo de la derecha, padding de D2, sin `.cabecera .lugar-engranaje`; actualizar el comentario del presupuesto de alto). Verificar con el filtro de 1.1 en verde y con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` completo en verde
  El botón y su lugar reservado pasan de `header.cabecera` a un `div.pie-derecha` del pie, después de la marca; `puesto.css`: pie de `min-height: 56px`, `padding: 0 8px 0 16px`, grupo con `margin-left: auto` y `gap: 8px`, `.lugar-engranaje` bajo `.pie`, comentario del presupuesto en unos 648 px.
  ```
  filtro Cabecera|PieEngranaje: correcto [+6/x0/?0] (1s 933ms)   total: 6 · error: 0 · correcto: 6
  Kiosk.Tests completo:        correcto [+151/x0/?0] (6s 076ms)  total: 151 · error: 0 · correcto: 151
  ```
- [x] 1.3 Agregar a `E2E.Tests/PresentacionTactilTests.cs` el escenario de D4 (engranaje en el borde inferior derecho a 1024 × 768 y pie del mismo alto en reposo y en la selección de nueve), verlo fallar sobre `2ca6559` compilado y pasar después de 1.2 con `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter "FullyQualifiedName~PresentacionTactil"`; pegar las medidas que registra
  Escenario `El_engranaje_en_el_borde_inferior_derecho_y_el_pie_estable` agregado a `PresentacionTactilTests.cs` (sólo líneas nuevas). Rojo sobre el código de `2ca6559` compilado (los dos archivos de `src` apartados con `git stash`, y restaurados después), 04/11 20:48:
  ```
  1024 × 768, reposo: ventana 1024 × 768, pie 28,0 px, marca hasta 1008,0 px, botones en la cabecera 1, lugar reservado False, documento 768 px, sin engranaje
  error con 1 errores [+0/x1/?0]      total: 1 · error: 1 · correcto: 0
  ```
  Verde con 1.2 (`dotnet test tests/Intelektron.INComedor.E2E.Tests --filter "FullyQualifiedName~PresentacionTactil"`, 04/11 20:49); las medidas salen del `registro.txt` de cada escenario:
  ```
  1024 × 768, reposo:             pie 57,0 px, marca hasta 952,0 px, botones en la cabecera 0, engranaje 56 × 56 px, de 960,0 a 1016,0 px, a 8,0 px del borde derecho y a 0,0 px del inferior, documento 768 px
  1024 × 768, selección de nueve: pie 57,0 px, botones en la cabecera 0, lugar reservado True, sin engranaje, documento 768 px
  Nueve opciones (existente):     9 opciones en 2 columnas, alto mínimo 56 px, última caja en 580 px, documento 768 px; a 600 de ancho, 1 columna, documento 1028 px
  correcto [+4/x0/?0] (15s 223ms)   total: 4 · error: 0 · correcto: 4
  ```
  El pie mide 57 px (56 del engranaje y 1 del borde superior) igual en reposo y en la selección. La prueba existente «Nueve opciones y las acciones entran en 1024 × 768» pasa con el pie nuevo: no hizo falta parar por el riesgo 1 del design.

## 2. Evidencia y documentos

- [ ] 2.1 Rehacer `docs/capturas/06` a `10` con el procedimiento de D4, mirarlas (engranaje en el borde inferior derecho, cabecera sin control, nada cortado a 1024 × 768) y verificar con `git diff --stat -- docs/capturas` que sólo cambian esas cinco
- [ ] 2.2 Actualizar en `readme.md` el texto que ubica el engranaje en la cabecera (línea 314 en `2ca6559`) y el texto alternativo de la captura 06; en `openspec/specs/presentacion-puesto/spec.md`, el `Purpose`, que nombra «la cabecera con los logos y el engranaje». Verificar con `git grep -n -i "engranaje de la cabecera\|cabecera con los logos y el engranaje"` sin resultados
- [ ] 2.3 Llevar los enlaces de D5 a `openspec/changes/archive/2026-11-03-presentar-atencion-tactil/` y sumar `mover-engranaje-al-pie` a la fila de accesibilidad de `docs/aplicacion-del-curso.md`. Verificar con el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso` (`enlaces rotos: 0`, con su control negativo) y con `git grep -n "openspec/changes/presentar-atencion-tactil/" -- "*.md"` sin resultados fuera de `openspec/changes/archive/`

## 3. Integración

- [ ] 3.1 Correr `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror` y `dotnet test INComedor.sln` completo contra la instancia de `INCOMEDOR_SQLSERVER`, y pegar los totales por proyecto; verificar además `git diff --diff-filter=M --stat 2ca6559 -- tests/` contra el párrafo de arriba

## Workflow follow-up

- Verificar con `/openspec-verify-change` y archivar en la misma rama antes del PR hacia `final-project-dw`.
- Actualizar `C:\AI\INComedor\ai4devs\gestion\PR-presentar-atencion-tactil-dw.md` con este change, sus commits y sus totales.
