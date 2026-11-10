# Proposal

## Why

El 09/11/2026, en el repo hermano `IDSerialSim`, David postergó la identificación real por un puerto COM y decidió que, mientras tanto, la identificación se simule dentro del puesto (decisiones 5 y 6 de `IDSerialSim/AGENTS.md`). En la ventana que pide el código va un combo con todas las personas cargadas, que identifica apenas se elige una, sin otro paso, junto al código tipeado y confirmado con Enter. El combo aparece sólo con un modo simulación, apagado por omisión, porque mostrar a todas las personas expone sus nombres y deja comer a cuenta de otro. Hoy el puesto identifica sólo por el código tipeado más «Ingresar», y Enter en el campo no hace nada.

El 10/11/2026, a las 00:25, David dejó para después el change `identificar-por-puerto-serie` (propuesto en su rama, sin aplicar) y decidió dos cosas sobre éste. Primero, que entra en la final, por PR a `final-project-dw` antes del 11/11 a las 12:00, y que si no llega en verde se descarta y la entrega queda como está. Segundo, que el modo simulación funciona sólo en mantenimiento: se enciende en General, se conserva al reiniciar y pasar a operativo lo apaga, así un puesto operativo nunca muestra la lista.

Origen: historia HU01.2, que el corte redujo al código (escenario J); funcionalidad F01; tickets TK01 (la identificación por credencial en el backend) y TK02 (la pantalla de identificación y el interruptor de General).

## What Changes

- **Enter envía el código**: en la pantalla de identificación, Enter en el campo del código hace lo mismo que «Ingresar», con las mismas reglas y los mismos rechazos. «Ingresar» se queda, porque el toque lo necesita.
- **Un combo de personas en modo simulación**: con el modo simulación activo, junto al código aparece un combo con el nombre visible de cada persona cargada en la versión activa. Elegir una la identifica en el acto, sin «Ingresar», por el mismo camino que el código tipeado (RB25), con los mismos rechazos y mensajes. El combo nunca muestra ni lleva un código: lleva la credencial.
- **El modo simulación**: está apagado por omisión. Se enciende y se apaga desde General sólo en mantenimiento, y se conserva en la carpeta de datos. Pasar a operativo lo apaga, y en operativo nunca está activo, aunque el archivo diga lo contrario.
- **Lo que queda registrado**: cada encendido y apagado va a la bitácora general como `CONFIGURATION_CHANGED`. Una identificación por el combo deja sus `SESSION_OPENED` o `IDENTIFICATION_REJECTED` de siempre, con una marca de que vino de la lista; ninguno lleva el código.

## Non-goals

- La identificación por puerto serie y por IDSerialSim, que quedan en el change `identificar-por-puerto-serie`, en su rama y sin aplicar.
- La biometría y cualquier equipo real.
- El combo en «Comprobantes» o en cualquier acción privilegiada: el operador sigue tipeando su código.
- Buscar o filtrar dentro del combo, fotos de las personas y cualquier dato que no sea el nombre visible.
- Cambiar la identificación por código más allá de Enter, la base (sin migración), el receptor o la CI.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `atencion-puesto`: «Identificación simulada por código» suma Enter como forma de enviar el código, y un requisito nuevo describe la identificación eligiendo a la persona en el combo del modo simulación.
- `acceso-puesto`: dos requisitos nuevos, el modo simulación sólo en mantenimiento y su registro en la bitácora general. Los modos de mantenimiento y operativo no cambian.

`configuracion-puesto` no cambia: General ya debe mostrar el modo, las bitácoras y «Cerrar el puesto», y el interruptor se suma sin quitar nada ni agregar pestañas.

## Impact

- **Código**:
  - `Infrastructure.Sqlite`: identificar por la credencial elegida, compartiendo con el código tipeado la clasificación, la sesión y la bitácora, y la lista de personas para el combo.
  - `Kiosk`: el almacén del modo simulación, el interruptor en General, el combo y Enter en la pantalla de identificación, la entrada nueva de `MaquinaAtencion` y el registro del almacén en `MainWindow`.
  - `Domain` no cambia. Sin migración: la versión de esquema sigue en 4.
- **Pruebas**: nuevas en `Infrastructure.Sqlite.Tests`, `Kiosk.Tests` y una en `E2E.Tests`. **Ninguna prueba existente cambia** (punto 6 del gate).
- **Documentos**: `AGENTS.md`, `openspec/config.yaml`, `readme.md` (cómo encender la simulación) y `docs/aplicacion-del-curso.md`.
- Sin dependencias nuevas, sin cambios en la CI ni en el receptor.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David | La das en el gate | | |
| La IA (Claude Code, Opus 5.5), 10/11/2026 | **S** | 2 a 3 h | 2,6 a 3,9 h de capacidad |

**Por qué la IA da S y no M.** Son 3 requisitos nuevos y 1 modificado, con 16 escenarios nuevos, sobre un camino de identificación que ya existe y se reutiliza entero. Lo nuevo es una consulta, un archivo de ajuste como el del modo y un control en dos pantallas que ya tienen su patrón. Lo que lo acerca a M es la prueba E2E y el cuidado de que el código no llegue a la pantalla. Como referencia de reloj, `mover-engranaje-al-pie` (S) se aplicó en menos de una hora; éste se estima en 1 a 1,5 h de reloj de la IA, más tu prueba en el ejecutable.

## Para el gate de David

Cada punto lleva una propuesta y su alternativa; lo que decidas queda escrito acá antes del apply.

1. **El calendario contra el corte del 11/11 a las 12:00.** Propuesta: el 10/11 a la mañana, tu gate; el apply, apenas lo apruebes (1 a 1,5 h); el 10/11 a la tarde, tu prueba en el ejecutable publicado; el 10/11 a la noche, la verificación, el archivado y el PR; el 11/11 a la mañana, su CI y tu merge. **Recorte de respaldo, propuesto y sin aplicar**: si el change no está archivado y en verde el 10/11 a las 22:00, sale la tarea 6.2 (la prueba E2E) y la evidencia queda en las pruebas de componentes. Si a esa hora tampoco están verdes los grupos 1 a 6 sin esa tarea, el change entero se descarta, como decidiste, y la entrega queda como está.
2. **«Ingresar» se queda y Enter se suma** (D8). Tu decisión 5 dice «confirmado con Enter». Propuesta: Enter y «Ingresar» hacen lo mismo. Alternativa: sacar «Ingresar»; eso cambia pruebas existentes (hay 35 referencias a `#ingresar` en 10 archivos) y deja la pantalla táctil atada al teclado en pantalla de Windows.
3. **Quiénes aparecen en el combo** (D3). Propuesta: una entrada por cada credencial de código de la versión activa, con el nombre visible, en orden alfabético. Entran también las revocadas, las vencidas y los cuatro operadores, porque son «todas las personas cargadas» y así se ven los rechazos sin saber códigos. Alternativa: sólo las vigentes, que esconde los rechazos y obliga a repetir en la consulta la clasificación de RB25.
4. **El teclado sobre el combo** (D9). En el WebView2, las flechas sobre el combo cerrado cambian la elección y la identifican en el acto. Propuesta: aceptarlo, porque es simulación, sólo en mantenimiento y en una pantalla táctil. Alternativa: una lista de botones con los nombres, que suma una segunda paginación a la pantalla de identificación y no es el combo que pediste.
5. **Qué queda registrado** (D7). Propuesta: la identificación por el combo escribe sus `SESSION_OPENED` o `IDENTIFICATION_REJECTED` de siempre con una clave más, `Origin: "SIMULATION_LIST"`, que la tipeada no lleva, y la sesión sigue con método `CODE` porque la credencial es la misma. Alternativa: un método `SIMULATION` en la sesión, que cambia una columna que otras pruebas leen.
6. **Pruebas existentes que cambian: ninguna.** Medido sobre `07a2329`: el combo y el interruptor no existen con la simulación apagada, que es lo que usan todas las pruebas de hoy. La única que mira General (`PestanasConfiguracionTests.Abre_en_general_con_las_nueve_pestanas`) busca el modo y las bitácoras, no cuenta botones. La tipeada no gana claves en su registro. Si otra falla durante el apply, se para y se te pregunta.
7. **Un puesto que arranca en operativo con el archivo de simulación encendido** (D5): sólo pasa si alguien editó el archivo o se cortó la luz entre las dos escrituras. Propuesta: arranca sin combo y guarda la simulación apagada, con su registro. Alternativa: ignorar el archivo sin reescribirlo, y entonces la simulación reaparece al volver a mantenimiento.
8. **`AGENTS.md` y `openspec/config.yaml`**: en el alcance del corte, la identificación simulada suma «y, en modo simulación y sólo en mantenimiento, eligiendo a la persona en un combo (change `simular-identificacion-por-lista`)».
9. **Rama.** `feature/simular-identificacion-por-lista-dw`, desde `origin/final-project-dw` en `07a2329` y sin seguimiento remoto, para que un `git push` sin argumentos no vaya a `final-project-dw`. El PR #15 (D-C27, sólo documentación) no la toca; si se integra antes, la rama se rebasa sobre la punta nueva antes del PR.
10. **Tallas.** La de la IA es S; la tuya la das acá.

### Decisión del gate

El 10/11/2026, a las 03:29, David respondió «aprobado. Avancemos.»: los puntos 1 a 5 y 7 quedan con la propuesta de cada uno, incluido el recorte de respaldo del punto 1, y los puntos 6, 8 y 9 como están. No dio una talla propia (punto 10), así que la tabla de tallas queda con la de la IA.
