## Why

El 10/11/2026 David pidió, como mejora 8b, un «rediseño UX de kiosco: todo operable sólo con la pantalla, con las ayudas
desactivables por sesión de Configuración». Decidió que entra en la final (decisión 10 del plan
`AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`) con el gate dado (decisiones 12 y 13). Es el change 9 del §6b.

Hoy sólo dos lugares tienen un teclado en pantalla: la clave del día y las raciones de Personas. El resto de los campos
de Configuración (la búsqueda de Consumos, Opciones, los textos y los datos del comprobante, Red, Mails, Lector,
Exportación) y el código de la pantalla de reposo necesitan un teclado físico, que un puesto de autoservicio no tiene.

Origen: épica HU03 (configurar y diagnosticar el puesto sin red) y HU01.1 (la atención autónoma), el criterio de UX
táctil del 03/11/2026 (objetivos amplios, foco visible); ticket TK02 (interfaz: flujo autónomo y estados de operación).

## What Changes

- **Un teclado en pantalla para cada campo de texto**: al tocar un campo de Configuración o el código de la pantalla de
  reposo, aparece abajo un teclado con números, letras con la Ñ, «Mayús», espacio, borrar y los símbolos que piden las
  casillas, los servidores y las carpetas (`@ . - _ : / \`). Escribe en el campo tocado como si fuera un teclado
  físico, sin sacarle el foco, y el campo queda a la vista arriba del teclado.
- **Las ayudas desactivables por sesión**: el teclado tiene «Ocultar teclado»; oculto, no vuelve a aparecer hasta que
  se cierre Configuración, y al abrirla otra vez está de nuevo. Con un teclado físico conectado, un técnico lo oculta y
  sigue escribiendo como siempre.
- **Los teclados que ya existen siguen**: la clave del día conserva el suyo, y en Personas el de las raciones; el
  teclado general no aparece encima de ellos.

## Non-goals

- Los diálogos de Windows para elegir una carpeta o un archivo: son del sistema y ya se operan con el dedo.
- Un teclado en otro idioma o con más símbolos que los de las casillas y las rutas.
- Cambiar cómo se ve la atención del comensal fuera del campo del código.
- Los campos de fecha y hora (las fechas de Consumos y las franjas de Opciones) y los combos: siguen con su selector
  nativo, que se mira en el monitor del puesto en la prueba de David.

## Capabilities

### New Capabilities

- `operacion-tactil`: el teclado en pantalla para cada campo de texto, cuándo aparece, cómo escribe y cómo se oculta por
  sesión de Configuración.

### Modified Capabilities

(ninguna: los requisitos de las pestañas y de la atención no cambian; el teclado se suma encima de ellos)

## Impact

- **Código**: `Kiosk` (el aviso estático `TecladoGeneral`, el teclado en pantalla con símbolos que dibuja la pantalla, lo que escucha el foco y
  escribe en el campo en `wwwroot/js/puesto.js`, la pantalla y Configuración). Sin migración.
- **Pruebas**: nuevas en `Kiosk.Tests` (bUnit) y una E2E a 1024 × 768 que, con toques reales de la pantalla táctil,
  escribe una casilla en Mails y un código en reposo sólo con las teclas en pantalla. Ninguna prueba existente cambia: el
  teclado lo abre sólo un toque, y las existentes escriben con el teclado y el mouse.
- **Documentos**: `readme.md` y la fila que corresponda de `docs/aplicacion-del-curso.md`.

## Gate (decisión 13 del plan: dado, con límites)

Opciones recomendadas, aplicadas sin esperar:

1. «Las ayudas» son el teclado en pantalla: es lo que hace falta para operar sólo con la pantalla. Las explicaciones de
   cada pestaña siguen a la vista.
2. El teclado aparece al tocar un campo de texto con el dedo y se va al salir de los campos; no se abre solo al entrar a
   una pestaña, ni con un mouse o un teclado físico.
3. «Ocultar teclado» vale para la sesión de Configuración: al cerrarla y volver a abrirla, el teclado está otra vez. En
   la pantalla de reposo no se oculta: el comensal no tiene un teclado físico.
4. El teclado va abajo de la pantalla, y Configuración se acorta para que el campo tocado quede arriba del teclado.
5. Los teclados propios de la clave del día y de Personas siguen; el general no aparece en sus campos.

Talla: L. Si una prueba existente falla por otra razón, se para (cláusula del plan).
