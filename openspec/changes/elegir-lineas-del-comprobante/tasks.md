# Tasks

Rama local `feature/mejoras-2-5-dw`, en el worktree `C:\AI\INComedor\INComedor-AI4DEVS-mejoras`, sobre `d6bbb24` (la
mejora 1 aplicada). Gate respondido por David el 11/11/2026 (proposal). Cada grupo cierra con
`git diff --diff-filter=M --name-only d6bbb24 -- tests/` pegado, que tiene que salir vacío: este change no prevé tocar
pruebas existentes. Cada tarea que toca una pantalla sigue la regla de prolijidad de `config.yaml`. **Se archiva después
de `reimprimir-sin-codigo`**, porque su bloque de «Recuperar o reimprimir sin tocar el consumo» parte del de aquél.

## 1. Las pruebas existentes

- [ ] 1.1 Confirmar que ninguna prueba existente cambia. Las del papel y del comprobante en pantalla (también las que
  agregó la mejora 1, como `ConsumosSinCodigoTests` y la marca SIN VALIDEZ) corren con las tres elecciones en
  «Imprimir», que es el valor por defecto. `ResguardoEnGeneralTests` escribe un `impresora.json` sin las elecciones, que
  sigue siendo válido. Verifica: `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde después del grupo 3, y
  el `git diff --diff-filter=M` del encabezado vacío. Si alguna prueba existente se pone roja, se para y se le pide a
  David la aprobación de esa prueba.

## 2. Las elecciones en `impresora.json`

- [ ] 2.1 Pruebas nuevas en `Kiosk.Tests` (`LineasDelComprobanteTests`), vistas fallar antes: un archivo sin las
  elecciones da las tres en `true`; uno con «comensal» igual a `"no"` da el valor por defecto entero; guardar y releer
  conserva las cinco; `Serializar` escribe las cinco. Verifica: el filtro de la clase, en rojo antes y en verde después.
- [ ] 2.2 D1 en `TextosComprobante.cs`. Verifica: 2.1 en verde y `TextosComprobanteTests` sin cambios y en verde.

## 3. La pestaña Impresora, el papel y la pantalla

- [ ] 3.1 Pruebas nuevas en `Kiosk.Tests`, vistas fallar antes, una por escenario del delta:
  - «Elegir qué imprime»: el aviso, `TICKET_TEXTS` con el antes y el después, y la elección que sigue al reabrir;
  - «Un archivo de textos de antes»;
  - «Una elección fuera de forma»;
  - «Un comprobante sin el comensal»: el papel y la pantalla sin la línea, y «Ver» con el nombre;
  - «Una copia sin aviso ni operador»: «Copia: 1», sin REIMPRESIÓN, «ORIGINAL» ni «Habilitó:»;
  - «La marca de la clave del día no se apaga».

  Verifica: en rojo antes y en verde después.
- [ ] 3.2 D2 en `PanelConfiguracion.razor` y `PantallaAtencion.razor` (el guardado y `TICKET_TEXTS`), y D3 en
  `ImpresoraSimulada.cs` y `ComprobanteEnPantalla.cs`. Verifica: 3.1 en verde y
  `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` en verde.
- [ ] 3.3 Prolijidad. Por CDP, a 1024 × 768, en la pestaña Impresora se miden los bordes izquierdos de los rótulos y de
  los tres pares de botones, y el ancho, el alto y la letra de los seis botones, contra los de `#correos-activos` y
  `#correos-apagados` (tolerancia de 1 px). Queda la captura `impresora-lineas-1024x768`. Va en una prueba E2E nueva,
  `LineasDelComprobanteE2ETests`. Verifica: `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter-class
  *LineasDelComprobante*`, en primer plano, con las medidas pegadas de `registro.txt`.

## 4. Documentación y verificación completa

- [ ] 4.1 `readme.md`: las tres elecciones de Impresora, y que la marca de la clave del día no se apaga. Verifica:
  `git grep -n "No imprimir" -- readme.md` con la línea nueva.
- [ ] 4.2 La verificación completa: `dotnet build INComedor.sln -warnaserror`, `dotnet test INComedor.sln` y
  `openspec validate --all --strict`, sin errores. Verifica: las tres salidas pegadas, con el total de pruebas.
- [ ] 4.3 La evidencia: la fila nueva o actualizada en `docs/aplicacion-del-curso.md` (el pedido de David, con la
  distinción entre operador y técnico de su respuesta 3) y, si deja un workflow o un ajuste humano nuevo, su entrada en
  `prompts.md`. Verifica: el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso`, con su control
  negativo, `enlaces rotos: 0`.

## Workflow follow-up

- Archivar después de `reimprimir-sin-codigo`; `/openspec-verify-change elegir-lineas-del-comprobante` y su salida en la
  descripción del PR. Push, PR y merge a `final-project-dw` son de David.
