# Proposal

## Why

`presentar-atencion-tactil` dejó el engranaje de Configuración como único control de la cabecera, arriba a la derecha. El 04/11, al probar el ejecutable en su monitor táctil, David pidió moverlo al borde inferior derecho, al lado de la marca Intelektron: la cabecera queda para la identidad de la empresa y la concesionaria, y el acceso técnico queda junto a la marca del proveedor, en el pie.

Va ahora, en la misma rama y antes del PR hacia `final-project-dw`, porque David lo decidió el 04/11 frente a dejarlo para el segundo change: quiere verlo en el monitor sin esperar a la configuración por pestañas.

Origen: ticket TK02.1 (interfaz de la atención) e historia HU01.1, como el change anterior; la decisión de David del 04/11/2026 está en el arnés del proyecto, fuera del repo.

## What Changes

- **El engranaje pasa al pie**: en reposo, el pie muestra a la derecha la marca Intelektron y, en el borde, el engranaje, con el mismo nombre accesible «Configuración» y el área de toque de 56 px. Durante una atención sigue sin mostrarse.
- **La cabecera queda sin controles**: logo de la empresa, nombre del servicio y logo de la concesionaria.
- **El pie mide 56 px en todos los estados**: durante la atención el lugar del engranaje queda reservado, para que la pantalla no salte al salir de reposo. **BREAKING** para una prueba existente, que afirma el engranaje en la cabecera: cambia sólo con la aprobación de David en este gate.
- **Capturas 06 a 10**: se rehacen, porque muestran la cabecera con el engranaje y el pie de 28 px.

## Non-goals

- **Clave del día, Configuración por pestañas y modos**: segundo change. El engranaje sigue abriendo Configuración sin clave.
- **El rebote de la pantalla al arrastrar el dedo** y **el ícono propio del ejecutable**: segundo change, como decidió David el 04/11.
- **Cambiar qué abre el engranaje, su dibujo o su comportamiento**: Configuración, su foco atrapado y lo inerte detrás quedan como están.
- **Modificar otras pruebas existentes** fuera de la que nombra el gate. Si hiciera falta otra, se para y se le pregunta a David.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `presentacion-puesto`: «Cabecera con logos y el engranaje en el pie» reemplaza a «Cabecera con logos y un solo control»: la cabecera no tiene controles y el engranaje va en el pie, en el borde derecho, al lado de la marca Intelektron. Va como requirement nuevo y el anterior como quitado, porque su escenario «Un solo control en la cabecera» deja de valer y un requirement modificado no puede perder escenarios.

## Impact

- **Código**: `src/Intelektron.INComedor.Kiosk/Atencion/PantallaAtencion.razor` (el botón y su lugar reservado pasan de la cabecera al pie) y `wwwroot/css/puesto.css` (cabecera sin el lugar del engranaje y pie de 56 px).
- **Pruebas**: una prueba existente de `CabeceraTests` cambia, sujeta al gate; pruebas nuevas en `Kiosk.Tests` y un escenario nuevo en `E2E.Tests` que mide la posición en la ventana real.
- **Documentos**: `docs/capturas/06` a `10`, el texto del README que describe la cabecera y el pie, el propósito de la spec `presentacion-puesto` y la fila del change en `docs/aplicacion-del-curso.md`.
- Sin dependencias nuevas, sin migración y sin cambios en la CI.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David | La das en el gate | | |
| La IA (Claude Code, Opus 5.5), 04/11/2026 | **S** | 1 a 2 h | 1,3 a 2,6 h de capacidad |

**Por qué la IA da S.** El cambio de marcado y de estilos es chico; el tiempo se va en la prueba E2E de la posición, en volver a sacar cinco capturas por CDP y en la suite completa, que con el E2E ronda los dos minutos por corrida.

## Para el gate de David

1. **Una prueba existente cambia.** `CabeceraTests.En_reposo_un_solo_boton_configuracion` afirma que la cabecera tiene un solo botón, «Configuración»; pasa a afirmar que la cabecera no tiene botones y que el pie tiene uno, «Configuración», con la clase `engranaje` (D3). Tu aprobación del gate es la aprobación de ese cambio y de ninguno más. `Sin_engranaje_durante_la_seleccion` sigue igual y en verde: la cabecera sigue sin botones y no hay ningún «Configuración» en la página.
2. **Pie de 56 px siempre.** Tomada: el pie crece de 28 a 56 px en todos los estados, con el lugar del engranaje reservado durante la atención (D1). Descartadas: un pie de 28 px con el engranaje desbordado hacia arriba, cuya área de toque taparía contenido; y un pie que crece sólo en reposo, que haría saltar la pantalla. A 1024 × 768 la selección de nueve pasa de unos 620 a unos 648 px de alto y sigue entrando sin desplazar.
3. **Orden en el pie.** Tomada: sincronización a la izquierda; a la derecha la marca Intelektron y el engranaje en el borde (D2), como pediste («en el borde inferior derecho, al lado del logo de Intelektron»).
4. **Capturas.** Tomada: se rehacen las cinco, 06 a 10, con el mismo procedimiento por CDP del change anterior (D4).
5. **Enlaces que rompió el archivado.** Al archivar `presentar-atencion-tactil` (`145ccc8`) quedaron siete enlaces a una carpeta que ya no existe, en `docs/aplicacion-del-curso.md`, `docs/cobertura-entrega.md`, `docs/plan-academico.md` y `prompts.md`. Tomada: corregirlos en la última tarea de este change, que corre el chequeo de enlaces (D5). Sólo documentos.
6. **Tallas.** La de la IA es S; la tuya la das acá.
