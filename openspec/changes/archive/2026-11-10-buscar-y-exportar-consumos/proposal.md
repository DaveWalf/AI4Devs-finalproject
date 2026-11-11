# Proposal

## Why

El 10/11/2026 David pidió, entre sus mejoras para Configuración, que Consumos vaya primero «con las últimas 24 h,
reimpresión y exportación CSV y Excel a una ubicación elegida y recordada» (mejora 4) y que la pestaña Comprobantes se
vaya: «búsqueda en Consumos por persona, código, comprobante y tipo, por fecha o alfanumérico, con reimpresión virtual o
real» (mejora 6). Ese día decidió que todo entra en la final, con el gate dado (decisiones 10 y 13 del plan
`AI4DEVS-GOAL-CIERRE-FINAL-11-11.md`). Éste es el change 1b; el 1a (`ordenar-configuracion`) ya puso Consumos primero.

Hoy Consumos muestra la fecha de negocio actual y filtra por fechas, persona, servicio y opción, sin buscar por texto,
por código ni por comprobante, y sin exportar. La reimpresión y la recuperación están en otra pestaña, Comprobantes, con
su propia lista de los comprobantes del día y el código de un operador.

Origen: historias HU03.1 (reimpresión con motivo, must) y HU03.3 (consulta del supervisor), RF17 y AT45; ticket TK03.1.

## What Changes

- **Las últimas 24 horas por defecto**: Consumos abre con lo confirmado en las 24 horas anteriores, por hora de
  confirmación y no por fecha de negocio, con la fecha en cada fila. «Últimas 24 h» vuelve a ese rango después de
  elegir fechas.
- **Buscar**: un campo de texto que busca en la persona, la opción, el servicio y el número de comprobante, sin
  distinguir acentos ni mayúsculas, y un campo para el código de una persona, que filtra por ella sin mostrarse ni
  registrarse. Se combinan con las fechas y las listas de Persona, Servicio y Opción.
- **El número de comprobante en la tabla**, en una columna nueva.
- **Ver, Reimprimir y Recuperar en cada fila**: «Ver» muestra el comprobante en pantalla, marcado COPIA, sin imprimir ni
  crear una copia (la reimpresión virtual); «Reimprimir» manda una copia a la impresora con su motivo (la real), y
  «Recuperar» reenvía un original fallido. Las dos últimas piden un operador con `OPERATOR` (RB26), como hoy: si se entró
  con el código de un operador, usan su sesión; si no, Consumos pide el código.
- **Sin la pestaña Comprobantes**: Configuración queda en ocho pestañas.
- **Exportar la consulta** a CSV y a Excel (`.xlsx`), con los filtros aplicados, a una carpeta que se elige una vez y se
  recuerda; cada exportación queda en la bitácora general.

## Non-goals

- La exportación automática, por agregado o por SFTP: es el change de Exportación (6 del plan).
- Cambiar las reglas de la reimpresión (RB26), sus motivos o el comprobante impreso.
- Mostrar códigos de identificación en la tabla, la búsqueda o la exportación.
- Reimprimir o exportar desde fuera de Configuración.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `consulta-consumos`: «Los consumos del día por defecto» se reemplaza por «Las últimas 24 horas por defecto»; «Filtros
  de la consulta» suma la búsqueda por texto y por código; «La consulta es de quien entra…» se reemplaza sin la mención de
  Comprobantes; requisito nuevo, exportar a CSV y Excel.
- `comprobante-local`: «Asistencia con permiso OPERATOR» y «Recuperar o reimprimir sin tocar el consumo» pasan a las
  filas de Consumos, con «Ver».
- `configuracion-puesto`: «Configuración por pestañas» se reemplaza por ocho pestañas, sin Comprobantes.

## Impact

- **Código**: `Infrastructure.Sqlite` (`ConsultaConsumos`: el rango por instante, el texto, el código y el número de
  comprobante; una función de SQLite que normaliza acentos y mayúsculas; la lectura del comprobante de un consumo para
  «Ver»; la exportación a CSV y XLSX) y `Kiosk` (`PestanaConsumos.razor` con la búsqueda, las acciones de cada fila, la
  entrada del operador y la exportación; `PanelConfiguracion.razor` sin Comprobantes; `PestanaComprobantes.razor` se
  borra; un elector de carpeta registrado por la ventana). Sin migración: no cambia la base.
- **Pruebas**: nuevas en `Infrastructure.Sqlite.Tests`, `Kiosk.Tests` y `E2E.Tests`. **Cambian pruebas existentes**, sólo
  las que el comportamiento nuevo exige (decisión 13): las de la pestaña Comprobantes pasan a Consumos, las que afirman
  el día de negocio por defecto pasan a las 24 horas, y las que cuentan nueve pestañas pasan a ocho. La lista exacta va en
  `tasks.md` y en el PR.
- **Documentos**: `readme.md` (la pestaña Consumos y la reimpresión) y `docs/aplicacion-del-curso.md`.
- Sin dependencias nuevas: el `.xlsx` se escribe con `System.IO.Compression`. Sin cambios en la CI ni en el receptor.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David | Sin talla propia (gate dado el 10/11 hacia las 19:50) | | |
| La IA (Claude Code, Opus 5.5), 10/11/2026 | **L** | 3 a 4 h | 3,9 a 5,2 h de capacidad |

**Por qué L.** Toca la consulta en SQLite, mueve una pestaña entera con sus reglas de permiso a otra, suma dos formatos
de archivo con su elección de carpeta, y arrastra pruebas existentes de tres clases.

## Para el gate de David

El gate está dado (decisión 13 del plan, 10/11 hacia las 19:50): «Dado ya, con límites». Cada punto lleva la recomendada,
que es la que se aplica, y su alternativa.

1. **Las 24 horas** (D1). Recomendada: por la hora de confirmación, hasta el instante en que se abre la pestaña.
   Alternativa: el día de negocio de hoy y el de ayer.
2. **Qué busca el texto** (D2). Recomendada: persona, opción, servicio y número de comprobante, sin acentos ni
   mayúsculas. Alternativa: sólo la persona.
3. **El código de persona** (D3). Recomendada: un campo propio que se vacía al consultar y nunca va a la bitácora.
   Alternativa: buscar el código dentro del texto, que lo dejaría a la vista.
4. **Ver y reimprimir** (D4). Recomendada: «Ver» sin operador, porque sólo muestra lo que la tabla ya muestra, y
   «Reimprimir» y «Recuperar» con operador (RB26). Alternativa: las tres con operador.
5. **La sesión del operador** (D5). Recomendada: si se entró con el código de un operador, Consumos usa esa sesión; si no,
   la pide en la misma pestaña. Alternativa: pedirlo siempre.
6. **La exportación** (D6). Recomendada: la carpeta se elige con el diálogo de Windows la primera vez y se recuerda en
   `exportacion.json`; CSV en UTF-8 con BOM y `;`, que Excel abre en español; hasta 10 000 filas. Alternativa: una
   carpeta fija dentro de la carpeta de datos.
7. **Pruebas existentes que cambian**: sólo las del punto «Impacto», listadas en `tasks.md` y en el PR. Si una falla por
   otra razón, se para.

### Decisión del gate

Por la decisión 13 del plan del cierre final (David, 10/11/2026 hacia las 19:50, «Dado ya, con límites»), los siete
puntos quedan con la recomendada, sin talla propia de David.
