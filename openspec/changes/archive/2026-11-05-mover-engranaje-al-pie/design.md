# Design

## Context

En `2ca6559` el engranaje es un `button.engranaje` dentro de `header.cabecera` de `PantallaAtencion.razor`, visible sólo en reposo; fuera de reposo deja un `span.lugar-engranaje` de 56 × 56 px para que el nombre del servicio no se corra. El pie (`footer.pie`) mide 28 px de alto mínimo, con `FranjaSincronizacion` a la izquierda y `img.marca-intelektron`, de 16 px de alto, a la derecha (`justify-content: space-between`). Con Configuración abierta, cabecera, atención y pie llevan `inert` (D5 de `presentar-atencion-tactil`). El presupuesto de alto a 1024 × 768 que fija el comentario de `puesto.css` es de unos 620 px: cabecera 64, título, cinco filas de 64 con 8 de separación, barra 88 y pie 28.

Las pruebas que tocan el engranaje lo buscan por la clase `.engranaje` (`ConfiguracionModalTests`, `GuardarEstiloSinEscrituraTests`, `PanelConfiguracionTests`), sin importar dónde está; sólo `CabeceraTests.En_reposo_un_solo_boton_configuracion` lo busca dentro de `header`.

## Goals / Non-Goals

**Goals:**

- El engranaje en el borde inferior derecho, al lado de la marca Intelektron, con el mismo nombre accesible, la misma área de toque y la misma regla de reposo.
- La pantalla no salta al salir de reposo y la selección de nueve sigue entrando en 1024 × 768.
- La posición se mide en la ventana real, no sólo en el marcado.

**Non-Goals:**

- Cambiar Configuración, su diálogo, el foco atrapado o lo inerte detrás.
- La clave, las pestañas, el rebote y el ícono del ejecutable (segundo change).

## Decisions

### D1. Pie de 56 px en todos los estados, con el lugar del engranaje reservado

El pie pasa a `min-height: 56px`. En reposo lleva el `button.engranaje`; fuera de reposo, un `span.lugar-engranaje` de 56 × 56 px en el mismo lugar, como hacía la cabecera. Así el pie mide lo mismo en reposo y en la atención, y la marca no se corre. A 1024 × 768 el total pasa de unos 620 a unos 648 px, bajo los 768.

Descartadas: un pie de 28 px con el botón desbordado hacia arriba, porque su área de toque de 56 px taparía 28 px del panel central, y en la selección ahí están «Cancelar» y «Confirmar»; un pie que crece sólo en reposo, porque la pantalla saltaría 28 px al identificarse, en contra de «zonas de acción estables»; achicar el área de toque a 28 px, porque baja del mínimo de 56 que fija la spec.

### D2. Orden del pie: sincronización, marca y engranaje en el borde

La franja de sincronización queda a la izquierda. A la derecha va un grupo con la marca Intelektron y, después, el engranaje, en el borde: el grupo lleva `margin-left: auto` y un `gap` de 8 px. El engranaje conserva sus estilos actuales (56 × 56, fondo transparente, dibujo de 28 px). El padding del pie pasa a `0 8px 0 16px`, para que el borde derecho del botón quede a 8 px del de la ventana, dentro de los 16 de la spec.

Descartada: el engranaje antes de la marca, porque David pidió el engranaje en el borde y la marca a su lado.

### D3. La cabecera sin controles; una prueba existente cambia con el gate

`header.cabecera` pierde el botón y el `span.lugar-engranaje`. El nombre del servicio sigue con `flex: 1` y centrado entre los logos. `.cabecera .lugar-engranaje` sale de `puesto.css`.

`CabeceraTests.En_reposo_un_solo_boton_configuracion` pasa a afirmar que `header` no tiene botones y que `footer` tiene exactamente uno, con `aria-label` «Configuración», `type` `button` y la clase `engranaje`; su DisplayName pasa a «presentacion-puesto · El engranaje en el borde inferior derecho: la cabecera sin botones y el pie con uno, «Configuración»». Es la única prueba existente que cambia, con la aprobación del gate. `Sin_engranaje_durante_la_seleccion` no cambia: la cabecera sigue sin botones y no hay ningún «Configuración» en la página.

Descartada: dejar la prueba y escribir otra, porque la vieja quedaría roja, y apagarla está prohibido.

### D4. La posición se mide en la ventana real y las capturas se rehacen

Las pruebas bUnit (`Kiosk.Tests`) afirman el marcado: el pie con la marca seguida del engranaje en reposo y con el lugar reservado en la selección. La posición y los tamaños sólo existen en la ventana real: un escenario nuevo en `E2E.Tests/PresentacionTactilTests.cs`, con el área de 1024 × 768 que ya fija esa clase, mide por CDP con `getBoundingClientRect` el engranaje (borde derecho e inferior a ≤ 16 px de los de la ventana, ≥ 56 × 56 y a la derecha de la marca) y el alto del pie en reposo y en la selección de nueve.

Las capturas 06 a 10 se rehacen con el procedimiento de la tarea 7.1 de `presentar-atencion-tactil` (CDP, 1024 × 768, JPEG a escala CSS), porque todas muestran el pie de 28 px y la 06 y la 10, además, el engranaje en la cabecera.

Descartada: medir sólo con bUnit, que no calcula el diseño.

### D5. Los enlaces que rompió el archivado entran en la última tarea

Al archivar `presentar-atencion-tactil` (`145ccc8`) quedaron apuntando a `openspec/changes/presentar-atencion-tactil/`, que ya no existe, `docs/aplicacion-del-curso.md:70`, `docs/cobertura-entrega.md:7`, `docs/plan-academico.md:83-84` y `prompts.md:123`. La última tarea los lleva a `openspec/changes/archive/2026-11-03-presentar-atencion-tactil/`, porque es la que corre el chequeo de enlaces de D8 de `mostrar-aplicacion-del-curso` y edita la misma fila del mapa.

Descartada: dejarlos para otro change, porque el PR saldría con enlaces rotos que el chequeo detecta.

## Risks / Trade-offs

- **Más alto en la selección.** El pie suma 28 px y la selección de nueve queda en unos 648 px a 1024 × 768. La prueba E2E existente «Nueve opciones y las acciones entran en 1024 × 768» lo controla; si fallara, se para y se revisa D1 con David antes de achicar algo.
- **El dibujo del engranaje sobre el pie translúcido.** Tiene el mismo color que en la cabecera (`rgb(43 33 24 / .72)`) sobre el mismo papel; la captura 06 lo muestra para mirarlo.
- **E2E intermitente del flujo principal** (advertencia 1 de la verificación del 03/11, anterior a este change): si vuelve a fallar «1 pendiente» a los 45 s, se repite el proyecto E2E y se anotan las dos corridas, sin tocar esa prueba.
