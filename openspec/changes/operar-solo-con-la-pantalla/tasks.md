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

## 3. Correcciones de la verificación

- [x] 3.1 El hallazgo crítico: lo escrito con las teclas llegaba al DOM pero no a los campos que Blazor lee con
  `onchange` (Opciones), y «Guardar» guardaba el valor viejo. `teclear` marca el campo y `puesto.js` dispara `change` al
  salir de él (D3). Verifica: `OperacionTactilE2ETests` en verde con una prueba que guarda, vista fallar antes. Hecho:
  vista fallar (no guardaba «Almuerzo común2»); ahora el nombre se guarda y se relee, y los datos del puesto escritos con
  las teclas (`123`, con su `maxlength` de 3, y `norte`) se guardan y se releen al volver a entrar.
- [x] 3.2 Las advertencias y sugerencias de la verificación: el teclado no saca el foco con un toque fuera de las teclas;
  las teclas con el estilo del puesto; `inputmode="none"` mientras está abierto; el teclado sólo en reposo o en
  Configuración, olvidado al salir del reposo; el campo desplazado otra vez a los 400 ms y con margen; un campo numérico
  sólo con dígitos; «Ocultar teclado» de tres teclas y con el borde del acento; las etiquetas de los símbolos y
  `tabindex="-1"` en las teclas; los escenarios de los teclados propios, del reposo y de «Ocultar teclado», y D1, D2,
  D3, D5 y D6 completados. Pruebas nuevas vistas fallar primero. Verifica: `TecladoGeneralTests` y
  `OperacionTactilE2ETests` en verde. Hecho: `TecladoGeneralTests` 4, vistas fallar sin las etiquetas, sin la clase de
  «Ocultar teclado» y con el teclado que quedaba al salir del reposo; `OperacionTactilE2ETests` 2, vista fallar con la
  tecla en Arial y, sin el cambio del contenedor, con el toque en su margen (a 13 px de la Q, porque Chromium lleva a la
  tecla un toque que cae a 3 px de ella). Medido: las teclas en `"Segoe UI", system-ui, sans-serif` con radio de 12 px,
  como «Cerrar»; las cinco filas de x 17 a 1007; la hora de la señal de y 214,5 a 270,5, arriba del teclado desde y 440;
  las unidades «1», con `A` y `.` «1», con `2` «12» y con «⌫» «1»; Personas y la clave del día sin el teclado general.
