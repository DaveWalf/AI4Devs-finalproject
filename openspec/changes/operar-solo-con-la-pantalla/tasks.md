## 1. El teclado general

- [ ] 1.1 `TecladoEnPantalla` con la fila de símbolos como opción (Personas no la muestra); `TecladoGeneral` en la
  pantalla, que aparece cuando un campo de texto tiene el foco, escribe con `incomedor.teclear` y se oculta por la
  sesión de Configuración; `puesto.js` avisa el foco y escribe en el campo (D1 a D4 y D6). Pruebas nuevas vistas fallar
  primero. Verifica: `dotnet test --project tests/Intelektron.INComedor.Kiosk.Tests` en verde.

## 2. La pantalla de verdad

- [ ] 2.1 E2E nueva a 1024 × 768: en Mails, el remitente escrito sólo con las teclas en pantalla; «Ocultar teclado» y
  otro campo sin teclado; cerrar y volver a abrir Configuración, otra vez con teclado; en reposo, `0001` con las teclas
  e «Ingresar» identifica, con capturas miradas (D5). Las E2E existentes que llenan campos siguen en verde o se listan
  las que el teclado cambia (decisión 13). Verifica: la E2E entera en verde.
- [ ] 2.2 `readme.md` y la fila que corresponda de `docs/aplicacion-del-curso.md`. Verifica: el chequeo de enlaces con su
  control negativo, `enlaces rotos: 0`.
- [ ] 2.3 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror
  --no-incremental`, el puesto publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c
  Release` contra ese EXE, con los totales por proyecto pegados, y la lista de pruebas existentes cambiadas.
