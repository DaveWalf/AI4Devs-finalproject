# Design

## Context

El motivo está en proposal.md («Why»). Lo que condiciona el cómo:

- **`MaquinaAtencion` pagina de a seis** (`TamanoPagina = 6`) y expone `PaginaActual`, `Pagina` y `TotalPaginas`; `PantallaAtencion.razor` dibuja una grilla fija de tres columnas con estilos dentro del componente. El catálogo llega ordenado por nombre (`ConsultaCatalogo`), y ese es el orden de las columnas.
- **El cargo llega formateado** por `MensajesAtencion.Cargo` («ARS 1.500,00»). Una prueba afirma que las opciones de la semilla empiezan con «ARS »; ninguna de esas tiene importe cero.
- **El resultado ya sabe si el comprobante salió**: `MaquinaAtencion.ComprobantePendiente` es falso sólo cuando el trabajo original quedó `SUBMITTED`. La impresora simulada escribe `TicketId-copia.txt` con la leyenda, el número, la copia y `DatosImpresion` (JSON con la opción, la fecha de negocio, la cantidad y el cargo, sin persona ni código).
- **Los modos de la impresora simulada** se eligen en `FranjaImpresora.razor`, al pie de la atención; ninguna prueba usa sus botones. `FranjaSincronizacion` sí la usa el E2E, por su rol `status` y su nombre «Sincronización con el receptor».
- **La carpeta de datos** sale de `EntornoDemostracion` (`%LOCALAPPDATA%\INComedor\demo\`, o `INCOMEDOR_DEMO_DIR`); ahí viven la base, los comprobantes y los certificados. Las variables del puesto son inertes sin valor y un valor inválido se ignora.
- **La semilla de atención** (`semilla_atencion.sql`) agrega 14 opciones a «Almuerzo de demostración», más personas, credenciales, reglas y permisos, y no corre dos veces porque mira si existe la opción `...0011`.
- **La referencia visual** son dos maquetas fuera del repo: «Cocina con sonrisas» (fondo con dos chefs, superficies al 76 % y desenfoque de 4 px) y la principal con logos y pie de marca. La primera medía 833 px de alto a 1024 de ancho, porque ponía la escena arriba del menú.

## Goals / Non-Goals

**Goals:**

- Que la distribución por cantidad se pruebe sin la ventana: la máquina dice cuántas columnas lleva la página, y la pantalla sólo lo dibuja.
- Que todo lo que entra desde la carpeta de datos (logos y estilo) se valide antes de usarse, y que un archivo malo nunca impida atender.
- Que lo visual se verifique en la ventana real: el E2E mide alturas, columnas y el ajuste a 1024 × 768.

**Non-Goals:**

- Contraste certificado y escalas de Windows de 125 y 150 %: se cuida el contraste con superficies de opacidad alta y texto oscuro opaco, y la medición fina queda para el puesto real.
- Un sistema de temas: hay un solo estilo, con dos parámetros.

## Decisions

### D1. Páginas de diez y columnas que decide la máquina

`TamanoPagina` pasa a 10. `MaquinaAtencion` suma `ColumnasPagina`: 1 si la página tiene hasta ocho opciones y 2 si tiene nueve o diez. Con 15 opciones quedan dos páginas, la primera de diez en dos columnas y la segunda de cinco en una. Los controles de página se dibujan sólo si `TotalPaginas > 1`.

La pantalla pone la lista en una grilla con `grid-auto-flow: column` y cinco filas cuando son dos columnas, de modo que la primera columna se completa antes que la segunda y la novena opción no deja un hueco en el medio. Cuando el ancho del contenido no alcanza para dos columnas de al menos 320 px, una consulta de contenedor la pasa a una columna; el desplazamiento vertical lo da la página, no la lista.

Cambian tres pruebas existentes, con la aprobación del gate y sólo en esto:

- `MaquinaAtencionTests.Recorre_las_15_opciones_paginadas`: `Assert.Equal(3, maquina.TotalPaginas)` pasa a `2`.
- `PantallaAtencionTests.Controles_de_la_seleccion_son_botones`: `6 + 4` pasa a `10 + 4`.
- `PantallaAtencionTests.Codigo_vigente_muestra_el_nombre_y_el_catalogo`: `Assert.Equal(6, …)` pasa a `10`.

Descartadas: conservar páginas de seis, que pagina once opciones cuando diez entran juntas; páginas de ocho, que pagina antes de lo que David pidió; decidir las columnas sólo con CSS, que no se puede probar en bUnit.

### D2. Cargo cero sin importe

`MensajesAtencion.Cargo` devuelve una cadena vacía cuando el importe es cero, y la pantalla omite el importe y su etiqueta si el cargo está vacío. «Confirmar» no depende del cargo. Descartadas: «ARS 0,00», que la maqueta descartó; el texto «Sin cargo», que David no decidió.

### D3. Hoja de estilos y recursos en `wwwroot`

Los estilos salen de los componentes a `wwwroot/css/puesto.css`, que enlaza `index.html`. El fondo es `wwwroot/img/fondo-cocina.jpg`, la imagen de 1536 × 1024 generada por Codex el 03/11 para «Cocina con sonrisas», con su prompt en `wwwroot/img/PROCEDENCIA.md`. Va como fondo de toda la página con `background-size: cover`, y los chefs quedan a los costados del panel central, que mide como máximo 980 px de ancho. Así la escena no ocupa alto propio: esa era la causa de los 833 px de la maqueta.

Las superficies (opciones, panel de elección, botones y diálogos) usan un color claro con `alpha` igual a la opacidad configurada y `backdrop-filter: blur()` con el desenfoque configurado, y el texto es oscuro y opaco. Los dos valores son variables CSS (`--opacidad-superficie`, `--desenfoque-fondo`) que el componente raíz fija en el elemento principal; la vista previa de Configuración cambia esas mismas variables.

Medidas para 1024 × 768: cabecera de 64 px, título de una línea, cinco filas de 64 px con 8 px de separación, barra de elección de 88 px y pie de 28 px, unos 620 px en total. Las acciones y las filas no bajan de 56 px y el texto de las opciones no baja de 20 px, quepa o no quepa.

Descartadas: estilos dentro de cada componente, que no se comparten entre la atención y la vista previa; la escena arriba del menú, como en la maqueta, que no entra en 768.

### D4. Logos: archivos locales validados y mostrados como imagen

`LogosLocales` busca `marcas\empresa` y `marcas\concesionaria` en la carpeta de datos, en el orden `.png`, `.webp`, `.svg`, y toma el primero que existe. Lo acepta si mide hasta 2 MB y su contenido corresponde a su extensión:

- PNG: los ocho bytes de firma;
- WebP: `RIFF` en los primeros cuatro bytes y `WEBP` en los bytes 8 a 11;
- SVG: XML bien formado, leído con `DtdProcessing.Prohibit` y sin `XmlResolver`, cuyo elemento raíz es `svg`.

Un archivo aceptado se entrega como URI `data:` en base64 y la pantalla lo pone en un `<img>`: dentro de una imagen, el navegador no ejecuta los scripts de un SVG. Uno rechazado deja el logo de ejemplo (`wwwroot/img/empresa-ejemplo.svg` y `concesionaria-ejemplo.svg`, dibujados para el repo) y se registra en la consola de depuración, sin interrumpir. Se leen al abrir la atención; el segundo change, que los carga desde la pantalla, vuelve a leerlos después de guardar.

La marca Intelektron es `wwwroot/img/intelektron.svg`, del archivo oficial que se bajó para la maqueta, con su origen en `PROCEDENCIA.md`.

Descartadas: mapear la carpeta de datos como un sitio local del WebView2, que expone toda la carpeta a la página; insertar el SVG en el DOM, que ejecutaría su contenido.

### D5. Configuración en este corte: un diálogo, dos secciones, sin clave

El engranaje se dibuja sólo en reposo. Abre `PanelConfiguracion.razor`, un diálogo modal (`role="dialog"`, `aria-modal="true"`) que no cambia el estado de la máquina:

- **«Estilo visual»**: dos `input type="range"` con etiqueta y valor visible, opacidad de 72 a 88 y desenfoque de 0 a 12, de a uno.
- **«Impresora simulada»**, sólo si la impresora es `ImpresoraSimulada`: los tres modos de `FranjaImpresora`, con los mismos `id` (`impresora-disponible`, `impresora-sinpapel`, `impresora-incierta`). La franja deja de dibujarse en la atención. El modo sigue en memoria y vuelve a «Disponible» al abrir el puesto, como hoy.

«Guardar» y «Cancelar» miden al menos 56 px; Escape equivale a «Cancelar».

`AlmacenEstiloVisual` lee y escribe `estilo-visual.json` en la carpeta de datos. Escribe a un archivo temporal y lo mueve encima del anterior, para no dejar uno a medias. Si al leer el archivo falta, no se puede leer o algún valor está fuera de rango, devuelve 76 y 4.

Descartadas: una pestaña por función ya en este change, que adelanta el segundo; guardar el estilo en la base SQLite, que mezcla presentación con el registro de consumos y pediría una migración; acotar cada valor fuera de rango al extremo más cercano, que esconde un archivo roto.

### D6. Catálogo de nueve: la semilla en dos partes

`semilla_atencion.sql` se parte en `semilla_atencion_comun.sql` (el tipo y la categoría REFRIGERIO, las personas, las credenciales, las reglas y los permisos) y dos archivos de opciones: `opciones_quince.sql`, con las 14 de hoy, y `opciones_nueve.sql`, con 8. `SemillaAtencion.Aplicar` corre la parte común y una de opciones en la misma transacción. La comprobación de «ya sembrada» sigue mirando la opción `...0011`, que existe en las dos variantes.

`EntornoDemostracion` suma `VariableCatalogo = "INCOMEDOR_CATALOGO"` y la propiedad `CatalogoNueve`, verdadera sólo con `nueve` sin espacios alrededor y sin distinguir mayúsculas.

Las ocho opciones, con «Almuerzo de demostración» (ALMUERZO, 1.500,00), son:

| Nombre | Tipo | Importe |
|---|---|---|
| Almuerzo opcional | ALMUERZO | 1.800,00 |
| Almuerzo con gaseosa | ALMUERZO | 2.000,00 |
| Almuerzo opcional con gaseosa | ALMUERZO | 2.300,00 |
| Gaseosa solamente | REFRIGERIO | 500,00 |
| Refrigerio | REFRIGERIO | 0 |
| Refrigerio opcional | REFRIGERIO | 900,00 |
| Merienda | REFRIGERIO | 0 |
| Yogurt | REFRIGERIO | 0 |

Los nombres son genéricos, como los del cartel de la maqueta; los importes son inventados.

Descartadas: un segundo snapshot, que exige activar snapshots (configuración por snapshot, segunda ola); sembrar las quince y borrar seis, que escribe y deshace en una base que se audita; duplicar el archivo entero, que deja las personas en dos lugares.

### D7. Comprobante en pantalla dentro del resultado

`ImpresoraSimulada` guarda el último `ComprobanteAImprimir` que aceptó como enviado. Al llegar al resultado, `MaquinaAtencion` expone `ComprobanteEnPantalla` sólo si la impresora es la simulada y `ComprobantePendiente` es falso: un registro con la leyenda, los primeros ocho caracteres del `TicketId`, la opción, la fecha de negocio y el cargo, leídos de `DatosImpresion`.

La pantalla dibuja la impresora y, saliendo de ella, el comprobante como un `button` con `id="listo"` y el nombre accesible «Retirar el comprobante»: tocarlo llama a `Listo`. Sin comprobante en pantalla, queda el botón «Listo» de hoy, con el mismo `id`, de modo que el E2E y las pruebas de bUnit siguen igual. El plazo de 10 s no cambia. La animación es un `@keyframes` de 1,2 s que desplaza el papel hacia abajo; `@media (prefers-reduced-motion: reduce)` la anula.

Descartadas: un estado nuevo en la máquina, que cambia transiciones que ya tienen pruebas; mostrar el comprobante también con «Incierta», cuando el papel pudo no haber salido.

### D8. Ventana maximizada

`MainWindow.xaml` suma `WindowState="Maximized"` y conserva 1000 × 760 como tamaño al restaurar. El E2E lo comprueba con `IsZoomed` de `user32` sobre la ventana principal del proceso, porque Playwright no ve la ventana WPF.

Descartada: pantalla completa sin bordes, que la decisión del 03/11 distingue de «maximizada» y que va, si va, con los modos del segundo change.

### D9. Cómo se verifica lo visual

- **bUnit**, sobre la base temporal: columnas por cantidad (8, 9, 10 y 15; las de 8 y 10 se arman sobre la semilla de nueve, borrando una opción o agregando una en la prueba), marca «Elegida», un solo control en la cabecera en reposo y ninguno durante la atención, cargo cero, logos válidos y rechazados, estilo guardado, cancelado y fuera de rango, comprobante en pantalla y sin papel.
- **E2E**, en la ventana real: un escenario nuevo con `INCOMEDOR_CATALOGO=nueve` que fija el área en 1024 × 768 con `Emulation.setDeviceMetricsOverride` por CDP y mide las cajas: las nueve opciones y las dos acciones dentro de 768 y de al menos 56 px; después, en 600 de ancho, una columna con el mismo tamaño de letra. Además, la ventana maximizada y el comprobante sin animación con `Emulation.setEmulatedMedia` y `prefers-reduced-motion: reduce`.

Descartada: verificar con capturas comparadas píxel a píxel, que se rompen con cualquier ajuste de color y no dicen qué falló.

### D10. Evidencia del change

La última tarea suma la fila del change en `docs/aplicacion-del-curso.md` y, en `prompts.md`, el ajuste humano nuevo: la maqueta en Codex como paso previo a la spec, con las decisiones de David volcadas a este change. El `readme.md` explica los logos locales y el catálogo de nueve, y `.env.example`, la variable.

## Risks / Trade-offs

- **[El fondo baja el contraste]** → superficies de 72 % como mínimo con texto oscuro opaco, y el desenfoque detrás. La maqueta midió 5,4:1 en el peor caso; acá no se certifica, y David lo mira en su monitor táctil.
- **[1024 × 768 no alcanza]** → el E2E lo mide. Si no entra, se achican los márgenes y la cabecera, nunca la letra de las opciones ni los 56 px.
- **[Configuración sin clave]** → aceptado para este change (gate, punto 6). Si entrara solo a la final, se declara en el README.
- **[`backdrop-filter` en equipos sin aceleración]** → WebView2 lo soporta; sin aceleración por hardware se ve sin desenfoque, y el texto sigue legible por la opacidad.
- **[Logos grandes en memoria]** → el tope de 2 MB por archivo acota la URI `data:` a unos 2,7 MB.

## Migration Plan

No hay migración de esquema. Una base de demostración existente conserva sus quince opciones; sin `estilo-visual.json`, el puesto arranca con 76 % y 4 px. Para volver atrás alcanza con no integrar la rama.
