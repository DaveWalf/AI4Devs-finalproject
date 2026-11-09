# Proposal

## Why

El puesto atiende hoy con una pantalla funcional pero sin diseño de uso táctil: una grilla de tres columnas con seis opciones por página, sin identidad visual, con los modos de la impresora simulada al pie y el comprobante sólo como archivo de texto. Entre el 01 y el 03/11 David definió en una maqueta cómo debe verse y comportarse la atención (distribución por cantidad de opciones, criterios táctiles, dirección gráfica «Cocina con sonrisas», logos, estilo configurable e impresora simulada en pantalla), y decidió el 03/11 dejar la maqueta y probar eso en el ejecutable real.

Va ahora porque David quiere una referencia real del puesto antes de decidir si entra a la entrega final del 12/11 («si vemos que va bien, lo incorporamos a la entrega final de AI4DEVS»). Este es el primero de dos changes: el aspecto y la atención. El segundo, configuración por pestañas, modos y clave del día, viene después y se propone aparte.

Origen: ticket TK02.1 (interfaz: selección y confirmación con sus estados) e historia HU01.1, con AT16 («catálogo de 15 opciones … navegación usable»); historia HU03.1 para el comprobante en pantalla. Las decisiones de David que este change lleva a requisitos están en el arnés del proyecto, fuera del repo: distribución del 02/11, criterios táctiles, paginación desde once opciones, perfil gráfico y dirección «Cocina con sonrisas» del 03/11.

## What Changes

- **Distribución por cantidad**: hasta ocho opciones, una columna; nueve y diez, dos columnas de hasta cinco, completando la primera antes de la segunda; desde once, páginas de diez, y cada página aplica la misma regla a sus opciones. Sólo con más de diez hay «Anterior» y «Siguiente». **BREAKING** para tres pruebas existentes que afirman páginas de seis: se cambian sólo con la aprobación de David en este gate.
- **Opción sin cargo**: una opción con importe cero se muestra sin importe, como en la maqueta.
- **Aspecto táctil**: fondo ilustrado de cocina con dos chefs; opciones y acciones como superficies translúcidas con el texto opaco; la fila entera de una opción es tocable; la elegida se marca también con el texto «Elegida»; las acciones miden al menos 56 px de alto; sin animaciones cuando el sistema pide movimiento reducido.
- **Cabecera y pie**: logo de la empresa, nombre del servicio, logo de la concesionaria y un engranaje como único control de la cabecera; marca Intelektron discreta y estado de sincronización en el pie.
- **Logos sin datos del cliente en el repo**: el puesto trae logos neutros de ejemplo. Si en la carpeta de datos hay `marcas\empresa.*` o `marcas\concesionaria.*` (PNG, WebP o SVG de hasta 2 MB), los muestra en su lugar; un archivo inválido se ignora.
- **Configuración del estilo visual**: el engranaje abre Configuración con «Estilo visual»: opacidad de las superficies de 72 % a 88 % (76 % por defecto) y desenfoque del fondo de 0 a 12 px (4 px por defecto), con vista previa, «Guardar» persistente y «Cancelar». Los modos de la impresora simulada pasan del pie a una sección de Configuración.
- **Comprobante en pantalla**: con la impresora simulada, una autorización muestra una impresora en pantalla de la que sale el comprobante animado, con el mismo contenido que el archivo. Tocarlo equivale a «Listo».
- **Ventana maximizada**: la ventana abre maximizada y la pantalla se adapta a su tamaño; si el ancho no admite dos columnas legibles, va una columna con desplazamiento vertical.
- **Catálogo sintético de nueve opciones**: con `INCOMEDOR_CATALOGO=nueve`, una base nueva se siembra con nueve opciones en lugar de quince, para ver en el ejecutable el caso de cinco más cuatro.

## Non-goals

- **Configuración por pestañas, modos mantenimiento y operativo, clave del día, cierre por Escape y quitar el botón de cierre de la ventana**: son del segundo change. En este, el engranaje abre Configuración sin clave.
- **Asignar y recordar el monitor, red del dispositivo, conexión con INWeb, contenido de las opciones e impresora real**: segundo change o posteriores.
- **Cargar logos desde la pantalla**: el segundo change suma la pestaña que escribe en la misma carpeta que este lee.
- **Configuración por snapshot, administración y diagnóstico** (D-C24, segunda ola): el estilo visual es presentación local del puesto, no configuración que llegue por snapshot.
- **Códigos numéricos de las opciones** (los 10, 20, … del cartel de la maqueta): el modelo de catálogo no los tiene y este change no cambia el esquema.
- **Cambios en RB06, en el contrato (`api/openapi.yaml`), en las migraciones o en el formato del cargo** («ARS 1.500,00» se conserva).
- **Modificar otras pruebas existentes** fuera de las tres que nombra el gate. Si hiciera falta otra, se para y se le pregunta a David.

## Capabilities

### New Capabilities

- `presentacion-puesto`: cómo se ve y se adapta el puesto: el aspecto táctil, la cabecera con logos y el engranaje, la ventana maximizada y la configuración del estilo visual con su persistencia.

### Modified Capabilities

- `atencion-puesto`: el requirement «Catálogo y selección sin consumo hasta confirmar» pasa de páginas de seis a la distribución por cantidad, con páginas de diez desde once opciones, y muestra sin importe la opción sin cargo.
- `comprobante-local`: se agrega «Comprobante en pantalla con la impresora simulada».
- `modo-demostracion`: se agrega «Catálogo sintético de nueve opciones».

## Impact

- **Código**: `src/Intelektron.INComedor.Kiosk/` (pantallas Razor y su hoja de estilos, `MaquinaAtencion` con páginas de diez, la impresora simulada que conserva el último comprobante, la lectura de logos y del estilo desde la carpeta de datos, la semilla de nueve opciones y `MainWindow` maximizada) y `wwwroot/` (fondo, logos de ejemplo y marca Intelektron).
- **Pruebas**: archivos nuevos en `Kiosk.Tests` y un escenario nuevo en `E2E.Tests`; tres pruebas existentes cambian, sujetas al gate.
- **Documentos**: `readme.md` («Cómo ver la demostración»: logos locales y catálogo de nueve), `.env.example`, `docs/aplicacion-del-curso.md` y `docs/cobertura-entrega.md`.
- Sin dependencias nuevas, sin migración y sin cambios en la CI.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David | La das en el gate | | |
| La IA (Claude Code, Opus 5.5), 03/11/2026 | **M** | 4 a 6 h | 5,7 a 8,6 h de capacidad |

**Por qué la IA da M.** La lógica nueva es chica (páginas de diez, lectura validada de dos archivos, una semilla y un estado más en el resultado), pero lo visual se verifica en la ventana real y a 1024 × 768, que es donde aparecieron las rondas de corrección de los changes 2 y 3. La maqueta midió 833 px de alto para «Cocina con sonrisas» a 1024 de ancho: hacerla entrar en 768 sin achicar texto ni botones es trabajo de composición, no de lógica.

No se promedia (lección 50): la diferencia, si la hay, va al gate.

## Para el gate de David

Lo que en otras exploraciones se te preguntaba; cada punto con la opción que se tomó y por qué. El detalle está en el design.

1. **Tres pruebas existentes cambian, sólo en números.** Tomada: páginas de diez desde once opciones (D1). Cambian:
   - `MaquinaAtencionTests.Recorre_las_15_opciones_paginadas`: `TotalPaginas` pasa de 3 a 2.
   - `PantallaAtencionTests.Controles_de_la_seleccion_son_botones`: los controles pasan de `6 + 4` a `10 + 4`.
   - `PantallaAtencionTests.Codigo_vigente_muestra_el_nombre_y_el_catalogo`: las opciones visibles pasan de 6 a 10.

   Tu aprobación del gate es la aprobación de esos tres cambios y de ninguno más. Descartada: conservar páginas de seis, que pondría once opciones en dos páginas cuando diez entran en una.
2. **Diez por página.** Dijiste «más de 10, debe paginar» sin fijar cuántas por página. Tomada: diez, con la misma regla de columnas en cada página (D1). Descartadas: seis, por lo anterior; ocho, que pagina antes de lo necesario.
3. **Cargo cero sin importe.** Tomada: como en la maqueta, la opción no muestra importe (D2). Descartadas: «ARS 0,00», que la maqueta descartó; «Sin cargo», un texto que no decidiste.
4. **Logos.** Tomada: logos neutros de ejemplo en el repo y los reales sólo en tu carpeta de datos (D4), porque el repo exige cliente anonimizado. La marca Intelektron va al repo desde su archivo oficial: confirmalo como dueño de la marca.
5. **El fondo de los chefs.** Tomada: la imagen generada por Codex el 03/11 entra al repo con su prompt como procedencia (D3).
6. **Engranaje sin clave en este change.** Tomada: abre Configuración directo, con «Estilo visual» y los modos de la impresora simulada (D5). Si este change entrara solo a la final, cualquiera podría cambiar el estilo o simular una impresora sin papel; la clave llega con el segundo change.
7. **Catálogo de nueve con una variable.** Tomada: `INCOMEDOR_CATALOGO=nueve`, sólo al crear la base (D6). Usa la opción «Almuerzo de demostración» que la base ya trae y suma ocho con los nombres genéricos del cartel e importes inventados; tres sin cargo.
8. **Tallas.** La de la IA es M; la de David es M.
