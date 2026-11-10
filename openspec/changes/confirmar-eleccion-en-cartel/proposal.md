# Proposal

## Why

El 10/11/2026, después de probar el puesto publicado, David pidió la mejora 2 de su lista de ese día, que el arnés `AI4DEVS-PROGRESS.md` guarda entera: «Modificar la confirmación o cancelación de lo seleccionado, por un cartel que aparezca recién una vez seleccionada la opción, y te refuerce qué seleccionaste, y si querés continuar o volver a seleccionar otra opción, así tenemos más lugar para selecciones». A las 10:36 decidió que entra en la final, como change propio.

Hoy la selección tiene, al pie y siempre visible, una barra con la elección («Confirmar: …» o «Tocá tu opción y confirmá la elección»), «Cancelar» y «Confirmar». Ocupa el alto de una fila de opciones y medio, y «Confirmar» está aunque no haya nada elegido.

Origen: historia HU01.1 (selección y confirmación) y HU01-B (cancelar antes de confirmar); ticket TK02.1, la pantalla de selección, resultado y regreso a reposo.

## What Changes

- **El cartel de la elección**: tocar una opción la marca «Elegida», como hoy, y abre un cartel sobre la selección. El cartel repite la opción y su cargo informado (sin importe si es cero), y ofrece «Confirmar» y «Elegir otra». «Confirmar» sólo existe en el cartel.
- **«Elegir otra»** cierra el cartel y deja la selección como estaba, con la marca. Tocar otra opción la mueve y vuelve a abrir el cartel. Escape con el cartel abierto es «Elegir otra».
- **Más lugar para las opciones**: sale la barra del pie de la selección. «Cancelar» pasa al bloque del título, junto al nombre de la persona, y sigue siempre a mano en la selección.
- **Lo que no cambia**: la confirmación (un solo `IntentId`, RB06, el doble toque, «Confirmando…» y el aviso de ocupado), la reconfirmación por cambio de catálogo, los 30 s de inactividad, los mensajes de cada motivo y la máquina de la atención.

## Non-goals

- Cambiar `MaquinaAtencion` o su registro: el cartel es de la pantalla.
- La reconfirmación por cambio de catálogo (RB10) dentro del cartel: sigue en su pantalla propia, con «Reconfirmar» y «Volver».
- Una cantidad por opción, un carrito o varias opciones por confirmación.
- La distribución del catálogo: ocho en una columna, diez en dos y páginas de diez desde once no cambian, aunque ahora entren con más aire.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `atencion-puesto`: un requisito nuevo, el cartel de la elección, con «Elegir otra» y Escape; «Catálogo y selección sin consumo hasta confirmar» cambia cuatro escenarios por el cartel.
- `presentacion-puesto`: «Aspecto táctil del puesto» mide «Elegir otra» y cambia lo que entra en 1024 × 768; «Cabecera con logos y el engranaje en el pie» y «Sin rebote al arrastrar» cambian el «Confirmar» de un escenario cada uno por el del cartel.

## Impact

- **Código**: sólo `Kiosk`. `PantallaAtencion.razor` (el cartel, «Cancelar» en el título y sin barra al pie) y `wwwroot/css/puesto.css`. `MaquinaAtencion`, `Domain` e `Infrastructure.Sqlite` no cambian. Sin migración.
- **Pruebas**: nuevas en `Kiosk.Tests` y en `E2E.Tests`. **Tres pruebas existentes cambian, con tu aprobación** (punto 3 del gate).
- **Documentos**: el recorrido de `readme.md` que nombra «Confirmar» y `docs/aplicacion-del-curso.md`.
- Sin dependencias nuevas, sin cambios en la CI ni en el receptor.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David | Sin talla propia (gate del 10/11 a las 12:08) | | |
| La IA (Claude Code, Opus 5.5), 10/11/2026 | **S** | 2,5 a 3 h | 3,3 a 3,9 h de capacidad |

**Por qué la IA da S y no M.** Son 4 requisitos modificados y 1 nuevo, con 3 escenarios nuevos y 9 que cambian, en una sola pantalla y sin tocar la máquina, la base ni el registro. Lo que lo acerca a M es el foco y Escape del cartel, la prueba E2E y las tres pruebas existentes que cambian.

## Para el gate de David

Cada punto lleva una propuesta y su alternativa; lo que decidas queda escrito acá antes del apply.

1. **Dónde va «Cancelar»** (D2). Propuesta: en el bloque del título, junto al «Hola, …», dentro de la pantalla y no en la cabecera, que sigue sin controles. Alternativa: un tercer botón en el cartel, «Cancelar», que obliga a elegir algo para poder irse.
2. **Escape y el foco** (D3). Propuesta: al abrirse el cartel, el foco va a «Confirmar», el resto de la pantalla queda inerte, y Escape es «Elegir otra». Alternativa: Escape no hace nada, como hoy durante una atención.
3. **Pruebas existentes que cambian, con tu aprobación.** Medido por un agente sobre `7d772c3`, con la misma pantalla que `ef63c02`, leyendo cada método entero:
   - `PantallaAtencionTests.Controles_de_la_seleccion_son_botones` y `PantallaDistribucionTests.Con_nueve_los_controles_son_botones`, de componentes: buscan «Confirmar» sin opción elegida. Suman el toque de una opción.
   - `ConfiguracionPorPestanasE2ETests.La_ventana_angosta_sigue_desplazandose`, E2E: desplaza hasta el «Confirmar» del pie. Llega a la última opción, la toca y mide «Confirmar» en el cartel.
   - En riesgo, sin cambio previsto: `PresentacionTactilTests.Nueve_opciones_en_1024_por_768_y_una_columna_en_600` mide «Cancelar» y «Confirmar» con una opción tocada, y a 600 px espera que la página se desplace. Sin la barra, las nueve opciones podrían entrar sin desplazar. Si falla, se para y se te pregunta.
   - Las demás que tocan la selección siguen pasando: el cartel conserva `id="confirmar"`, `id="eleccion"` y `id="ocupado"`.
4. **El texto del cartel** (D1). Propuesta: el título «¿Confirmás tu elección?», el nombre de la opción en grande y «Cargo informado: …» sólo si hay cargo. Alternativa: «Elegiste …» como título.
5. **Calendario y recorte.** Se aplica después de la ampliación del 1, apilado sobre ella, en 2,5 a 3 h. Tu prueba en el ejecutable va una sola vez, con los tres cambios juntos. Recorte propuesto: si el 11/11 a las 08:00 este change no está archivado y en verde, queda fuera de la final, y con él el 3, que va encima.
6. **Rama.** `feature/confirmar-eleccion-en-cartel-dw`, desde `feature/simular-identificacion-por-lista-dw` en `ef63c02` y sin seguimiento remoto. Su PR apunta a esa rama y se integra después de ella, como la pila del 09/11.
7. **Tallas.** La de la IA es S; la tuya la das acá.

### Decisión del gate

El 10/11/2026, a las 12:08, David respondió «Todo aprobado.» al gate de las tres propuestas del día, con las recomendadas y sin talla propia. Queda así:

- Punto 1 con la propuesta: «Cancelar» va en el bloque del título (`.titulo`), junto al saludo.
- Punto 2 con la propuesta: al abrirse el cartel, el foco va a «Confirmar», el resto de la pantalla queda `inert` y Escape es «Elegir otra».
- Punto 3 como está: cambian sólo las tres pruebas existentes de su lista. Si `PresentacionTactilTests.Nueve_opciones_en_1024_por_768_y_una_columna_en_600` falla, se para.
- Punto 4 con la propuesta: el título del cartel «¿Confirmás tu elección?».
- Punto 5 con la propuesta: el recorte del 11/11 a las 08:00.
- Punto 6 como está, con la base nueva: la rama se rebasó sobre la punta de la ampliación del 1, `d0da333`, en lugar de `ef63c02`. Esa punta es el `<base>` del encabezado de `tasks.md`.
- Punto 7 sin talla propia, así que la tabla de tallas queda con la de la IA.
