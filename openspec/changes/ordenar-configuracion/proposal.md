# Proposal

## Why

El 10/11/2026, al probar el EXE publicado desde `86ae3b3`, David encontró tres cosas, textuales en el arnés
`AI4DEVS-PROGRESS.md`: «Cuando tengo elegido el monitor alternativo, la lista desplegable de "Elegí a una persona" se
muestra en el monitor principal»; «En Configuración, todavía no están reflejados los cambios que pedí en el orden y formato
de las cosas, y muestra un botón Cancelar cuyo título es incorrecto, debería ser Cerrar estimo»; y «El combo de Elegí una
persona tiene un tamaño diferente al de ingresar el código que está debajo, y no entiendo esa asimetría». Ese mismo día
decidió que todo lo pedido para Configuración entra en la final («Todo entra ahora.»), en nueve changes con el gate dado
(decisiones 9 a 14 del plan `AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`). Éste es el primero, la parte 1a: el orden, «Cerrar», la
lista propia y la prolijidad.

Hoy las pestañas van en el orden General, Personas, Opciones, Consumos, Comprobantes, Identidad visual, Impresora,
Pantalla y Red, y abren en General; las mejoras 4 («Consumos primero») y 8a («General al final») del 10/11 piden otro
orden. El pie dice «Cancelar», aunque cada pestaña guarda lo suyo y el botón en realidad cierra. El combo es un `<select>`
nativo, cuya lista dibuja el WebView2 fuera de la página y ubica en el monitor principal; mide 480 px con letra de 22 px,
mientras la fila del código mide 592 px con letra de 24 px (`puesto.css:121-150`).

Origen: historias HU03.3 y U08 (mantenimiento y configuración) y HU01.2-J (identificación por código, con la simulación
de `simular-identificacion-por-lista`); ticket TK03.1, la configuración del puesto.

## What Changes

- **Orden de las pestañas**: Consumos, Personas, Opciones, Comprobantes, Identidad visual, Impresora, Pantalla, Red y
  General. Configuración abre en Consumos también con la clave del día; un operador sigue viendo sólo Consumos, Personas e
  Impresora. Comprobantes sale en el change 1b, `buscar-y-exportar-consumos`.
- **«Cerrar»** en lugar de «Cancelar» en el pie de Configuración, con la misma conducta: cierra y descarta lo no guardado.
- **Una lista desplegable propia**, dibujada dentro de la ventana del puesto, para el combo de la simulación y para los
  filtros Persona, Servicio y Opción de Consumos: se abre en el monitor donde está el puesto, con filas de 56 px, por
  toque y por teclado. Ningún `<select>` nativo queda en esas pantallas.
- **El combo alineado con la fila del código**: el mismo ancho que el campo con «Ingresar», la misma letra y el mismo
  relleno, además del alto de 56 px.
- **La regla de prolijidad visual** en `openspec/config.yaml` (decisión 14), para este change y los siguientes.

## Non-goals

- Sacar la pestaña Comprobantes, la búsqueda, la reimpresión en Consumos, las últimas 24 h y la exportación: son el
  change 1b.
- Las listas de `PanelOpciones` (servicio y categoría de carga): las rehace el change de Opciones (3 del plan).
- Avisar lo no guardado al cerrar, que nadie pidió.
- Cambiar qué muestra cada pestaña o cómo se entra a Configuración.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `configuracion-puesto`: «Configuración por pestañas» cambia el orden y la pestaña inicial; «Lo editado se conserva hasta
  cerrar» nombra «Cerrar».
- `presentacion-puesto`: «Estilo visual configurable» nombra «Cerrar»; «Pantalla de identificación que invita a comer»
  alinea el combo con la fila del código; un requisito nuevo, las listas desplegables dentro de la ventana del puesto.

## Impact

- **Código**: sólo `Kiosk`. `PanelConfiguracion.razor` (el orden, la pestaña inicial y «Cerrar»), un componente nuevo
  `ListaDesplegable.razor`, `PantallaAtencion.razor` (el combo), `PestanaConsumos.razor` (los tres filtros) y
  `wwwroot/css/puesto.css`. `Domain`, `Infrastructure.Sqlite`, la máquina de la atención y la bitácora no cambian. Sin
  migración.
- **Pruebas**: nuevas en `Kiosk.Tests` y en `E2E.Tests`. **Cambian pruebas existentes**, sólo las que el comportamiento
  nuevo exige (decisión 13): las que dan por hecho que Configuración abre en General, las que listan el orden de las
  pestañas, las que eligen en un `<select>` (el combo y los filtros de Consumos) y los `DisplayName` de los escenarios
  renombrados. La lista exacta va en `tasks.md` y en el PR.
- **Documentos**: `readme.md` donde nombra «Cancelar» o el orden de las pestañas, y `docs/aplicacion-del-curso.md`.
- Sin dependencias nuevas, sin cambios en la CI ni en el receptor.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David | Sin talla propia (gate dado el 10/11 hacia las 19:50) | | |
| La IA (Claude Code, Opus 5.5), 10/11/2026 | **M** | 2 a 3 h | 2,6 a 3,9 h de capacidad |

**Por qué M y no S.** Son 4 requisitos modificados y 1 nuevo en una pantalla que ya existe, pero la lista propia es un
componente nuevo con teclado y foco, reemplaza cuatro controles y arrastra a las pruebas que eligen en un `<select>`.

## Para el gate de David

El gate está dado (decisión 13 del plan, 10/11 hacia las 19:50): «Dado ya, con límites». Cada punto lleva la recomendada,
que es la que se aplica, y su alternativa.

1. **Pestaña inicial** (D1). Recomendada: Configuración abre en Consumos, la primera, también con la clave del día.
   Alternativa: abrir en General aunque quede última.
2. **El id de «Cerrar»** (D2). Recomendada: conservar `id="cancelar-configuracion"`, porque el comportamiento no lo exige y
   así no cambian las pruebas que sólo lo tocan. Alternativa: `cerrar-configuracion`.
3. **La lista propia** (D3). Recomendada: un botón con el valor elegido que abre una lista `role="listbox"` dentro de la
   página, debajo del botón, con filas de 56 px; flechas, Inicio, Fin, Enter y Escape; un toque afuera la cierra.
   Alternativa: dejar el `<select>` y mover la ventana, que no corrige la lista del WebView2.
4. **El ancho del combo** (D4). Recomendada: el combo ocupa la fila entera, de borde a borde con el campo y su
   «Ingresar», con la letra de 24 px y el relleno de 16 px del campo. Alternativa: el ancho del campo solo, sin «Ingresar».
5. **Pruebas existentes que cambian**: sólo las del punto «Impacto», listadas en `tasks.md` y en el PR. Si una falla por
   otra razón, se para.

### Decisión del gate

Por la decisión 13 del plan del cierre final (David, 10/11/2026 hacia las 19:50, «Dado ya, con límites»), los cinco puntos
quedan con la recomendada, sin talla propia de David.
