# Tasks

Las pruebas existentes no se tocan, salvo `CabeceraTests.En_reposo_un_solo_boton_configuracion`, que aprobó David en el gate (D3). `git diff --diff-filter=M --stat 2ca6559 -- tests/` sólo puede mostrar `CabeceraTests.cs` y, si la prueba E2E nueva va en ese archivo, `E2E.Tests/PresentacionTactilTests.cs` con sólo líneas agregadas. Cada prueba nueva se ve fallar antes de implementar. Si otra tarea necesitara cambiar una prueba existente, se para y se le pregunta a David.

## 1. El engranaje en el pie

- [ ] 1.1 Cambiar `CabeceraTests.En_reposo_un_solo_boton_configuracion` como dice D3, y agregar en un archivo nuevo de `Kiosk.Tests` dos casos bUnit: en reposo, dentro de `footer`, la marca Intelektron va antes del engranaje y son sus dos últimos elementos; en la selección, el pie tiene `span.lugar-engranaje` y ningún botón. Verlos fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~Cabecera|FullyQualifiedName~PieEngranaje"` antes de tocar el código, y pegar la salida
- [ ] 1.2 Mover en `PantallaAtencion.razor` el botón y su lugar reservado de la cabecera al pie, en el orden de D2, y ajustar `puesto.css` (pie de 56 px, grupo de la derecha, padding de D2, sin `.cabecera .lugar-engranaje`; actualizar el comentario del presupuesto de alto). Verificar con el filtro de 1.1 en verde y con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` completo en verde
- [ ] 1.3 Agregar a `E2E.Tests/PresentacionTactilTests.cs` el escenario de D4 (engranaje en el borde inferior derecho a 1024 × 768 y pie del mismo alto en reposo y en la selección de nueve), verlo fallar sobre `2ca6559` compilado y pasar después de 1.2 con `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter "FullyQualifiedName~PresentacionTactil"`; pegar las medidas que registra

## 2. Evidencia y documentos

- [ ] 2.1 Rehacer `docs/capturas/06` a `10` con el procedimiento de D4, mirarlas (engranaje en el borde inferior derecho, cabecera sin control, nada cortado a 1024 × 768) y verificar con `git diff --stat -- docs/capturas` que sólo cambian esas cinco
- [ ] 2.2 Actualizar en `readme.md` el texto que ubica el engranaje en la cabecera (línea 314 en `2ca6559`) y el texto alternativo de la captura 06; en `openspec/specs/presentacion-puesto/spec.md`, el `Purpose`, que nombra «la cabecera con los logos y el engranaje». Verificar con `git grep -n -i "engranaje de la cabecera\|cabecera con los logos y el engranaje"` sin resultados
- [ ] 2.3 Llevar los enlaces de D5 a `openspec/changes/archive/2026-11-03-presentar-atencion-tactil/` y sumar `mover-engranaje-al-pie` a la fila de accesibilidad de `docs/aplicacion-del-curso.md`. Verificar con el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso` (`enlaces rotos: 0`, con su control negativo) y con `git grep -n "openspec/changes/presentar-atencion-tactil/" -- "*.md"` sin resultados fuera de `openspec/changes/archive/`

## 3. Integración

- [ ] 3.1 Correr `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror` y `dotnet test INComedor.sln` completo contra la instancia de `INCOMEDOR_SQLSERVER`, y pegar los totales por proyecto; verificar además `git diff --diff-filter=M --stat 2ca6559 -- tests/` contra el párrafo de arriba

## Workflow follow-up

- Verificar con `/openspec-verify-change` y archivar en la misma rama antes del PR hacia `final-project-dw`.
- Actualizar `C:\AI\INComedor\ai4devs\gestion\PR-presentar-atencion-tactil-dw.md` con este change, sus commits y sus totales.
