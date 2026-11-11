## 1. El teclado general

- [x] 1.1 `TecladoEnPantalla` con la fila de símbolos como opción (Personas no la muestra); `TecladoGeneral` en la
  pantalla, que aparece cuando un campo de texto tiene el foco, escribe con `incomedor.teclear` y se oculta por la
  sesión de Configuración; `puesto.js` avisa el foco y escribe en el campo (D1 a D4 y D6). Pruebas nuevas vistas fallar
  primero. Verifica: `dotnet test --project tests/Intelektron.INComedor.Kiosk.Tests` en verde. Hecho: `TecladoGeneralTests` (3),
  dos vistas fallar contra un mutante que nunca muestra el teclado (la de Personas protege lo que ya existía). La primera
  versión registraba el JS al dibujar la pantalla y 40 pruebas existentes con JSInterop estricto cayeron: el JS pasó a
  escucharse solo desde que carga la página y a avisar por un método estático (`TecladoGeneral.CampoTocado`), sin tocar
  ninguna de ellas. Kiosk 596.

## 2. La pantalla de verdad

- [x] 2.1 E2E nueva a 1024 × 768: en Mails, el remitente escrito sólo con las teclas en pantalla; «Ocultar teclado» y
  otro campo sin teclado; cerrar y volver a abrir Configuración, otra vez con teclado; en reposo, `0001` con las teclas
  e «Ingresar» identifica, con capturas miradas (D5). Las E2E existentes que llenan campos siguen en verde o se listan
  las que el teclado cambia (decisión 13). Verifica: la E2E entera en verde. Hecho: `OperacionTactilE2ETests` en verde con
  `Input.dispatchTouchEvent`: `0001` y `p@e.com` escritos con las teclas, el teclado de y 440 a 768 con teclas de 56 px, el
  diálogo hasta y 424 y el campo arriba del teclado; con el mouse no aparece (la primera versión lo abría por un toque de
  hasta 1,5 s antes y la prueba lo mostró: ahora manda el último puntero). Ninguna prueba existente cambió.
- [x] 2.2 `readme.md` y la fila que corresponda de `docs/aplicacion-del-curso.md`. Verifica: el chequeo de enlaces con su
  control negativo, `enlaces rotos: 0`. Hecho: 0, con el control en 2.
- [ ] 2.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror
  --no-incremental`, el puesto publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c
  Release` contra ese EXE, con los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
