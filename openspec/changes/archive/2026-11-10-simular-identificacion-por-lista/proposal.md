# Proposal

## Why

El 09/11/2026, en el repo hermano `IDSerialSim`, David postergó la identificación real por un puerto COM y decidió que, mientras tanto, la identificación se simule dentro del puesto (decisiones 5 y 6 de `IDSerialSim/AGENTS.md`). En la ventana que pide el código va un combo con todas las personas cargadas, que identifica apenas se elige una, sin otro paso, junto al código tipeado y confirmado con Enter. El combo aparece sólo con un modo simulación, apagado por omisión, porque mostrar a todas las personas expone sus nombres y deja comer a cuenta de otro. Hoy el puesto identifica sólo por el código tipeado más «Ingresar», y Enter en el campo no hace nada.

El 10/11/2026, a las 00:25, David dejó para después el change `identificar-por-puerto-serie` (propuesto en su rama, sin aplicar) y decidió dos cosas sobre éste. Primero, que entra en la final, por PR a `final-project-dw` antes del 11/11 a las 12:00, y que si no llega en verde se descarta y la entrega queda como está. Segundo, que el modo simulación funciona sólo en mantenimiento: se enciende en General, se conserva al reiniciar y pasar a operativo lo apaga, así un puesto operativo nunca muestra la lista.

El 10/11/2026, a las 10:36, después de probar el puesto publicado, David amplió este change con la mejora 1 de su lista de ese día, que el arnés `AI4DEVS-PROGRESS.md` guarda entera:
- la simulación encendida por omisión, hasta que se la apague en General;
- el combo arriba del ingreso manual, y «Ingresar» a la derecha del campo;
- un título que invite a disfrutar la comida, en lugar de «Identificate para comenzar».

Ante la pregunta de cómo convive eso con su decisión 6, eligió que en operativo siga sin verse nunca, pero que pasar a operativo no la apague: al volver a mantenimiento reaparece. Así la decisión 6 queda reemplazada en el valor por omisión, no en el operativo. Eligió también que el 1, el 2 y el 3 de esa lista entren en la final, y que el 2 y el 3 vayan como changes propios.

Origen: historia HU01.2, que el corte redujo al código (escenario J); funcionalidad F01; tickets TK01 (la identificación por credencial en el backend) y TK02 (la pantalla de identificación y el interruptor de General).

## What Changes

- **Enter envía el código**: en la pantalla de identificación, Enter en el campo del código hace lo mismo que «Ingresar», con las mismas reglas y los mismos rechazos. «Ingresar» se queda, porque el toque lo necesita.
- **Un combo de personas en modo simulación**: con el modo simulación activo, arriba del campo del código aparece un combo con el nombre visible de cada persona cargada en la versión activa. Elegir una la identifica en el acto, sin «Ingresar», por el mismo camino que el código tipeado (RB25), con los mismos rechazos y mensajes. El combo nunca muestra ni lleva un código: lleva la credencial.
- **El modo simulación**: está encendido por omisión desde la ampliación del 10/11. Se apaga y se vuelve a encender desde General sólo en mantenimiento, y se conserva en la carpeta de datos. En operativo nunca está activo, aunque el archivo diga lo contrario, y pasar a operativo no cambia lo guardado.
- **La pantalla de identificación** (ampliación del 10/11): el título «¡Hola! Identificate y disfrutá tu comida», el combo arriba del campo y «Ingresar» a la derecha del campo, en su misma fila.
- **Lo que queda registrado**: cada apagado y encendido desde General va a la bitácora general como `CONFIGURATION_CHANGED`. Una identificación por el combo deja sus `SESSION_OPENED` o `IDENTIFICATION_REJECTED` de siempre, con una marca de que vino de la lista; ninguno lleva el código.

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
- `acceso-puesto`: dos requisitos nuevos, el modo simulación encendido por omisión y sólo en mantenimiento, y su registro en la bitácora general. Los modos de mantenimiento y operativo no cambian.
- `presentacion-puesto` (ampliación del 10/11): un requisito nuevo, la pantalla de identificación que invita a comer, con el combo arriba y «Ingresar» a la derecha del campo.

`configuracion-puesto` no cambia: General ya debe mostrar el modo, las bitácoras y «Cerrar el puesto», y el interruptor se suma sin quitar nada ni agregar pestañas.

## Impact

- **Código**:
  - `Infrastructure.Sqlite`: identificar por la credencial elegida, compartiendo con el código tipeado la clasificación, la sesión y la bitácora, y la lista de personas para el combo.
  - `Kiosk`: el almacén del modo simulación, el interruptor en General, el combo y Enter en la pantalla de identificación, la entrada nueva de `MaquinaAtencion` y el registro del almacén en `MainWindow`.
  - `Domain` no cambia. Sin migración: la versión de esquema sigue en 4.
- **Pruebas**: nuevas en `Infrastructure.Sqlite.Tests`, `Kiosk.Tests` y una en `E2E.Tests`. **Ninguna prueba de `07a2329` cambia** (punto 6 del gate). La ampliación cambia pruebas de este mismo change, con la aprobación de David (punto A4).
- **Documentos**: `AGENTS.md`, `openspec/config.yaml`, `readme.md` (cómo encender la simulación) y `docs/aplicacion-del-curso.md`.
- Sin dependencias nuevas, sin cambios en la CI ni en el receptor.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David | Sin talla propia (gate del 10/11 a las 12:08) | | |
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

## Ampliación del 10/11: para el gate de David

Cada punto lleva una propuesta y su alternativa, salvo el A1, que ya decidiste. Lo que decidas queda escrito acá antes de aplicar la ampliación.

1. **A1. Lo decidido a las 10:36.** La simulación arranca encendida sin ajuste guardado, sólo «Apagar la simulación» en General la apaga, en operativo nunca se ve, y pasar a operativo no cambia lo guardado (D5, D12). Salen las dos escrituras de antes: la del pase a operativo y la del arranque en operativo con `Access = "STARTUP"`.
2. **A2. Un ajuste ilegible** (D4). Propuesta: encendida, igual que sin ajuste, como un modo ilegible arranca en mantenimiento, el modo por omisión. Alternativa: apagada, que deja dos casos de la teoría del almacén y una prueba de `ModoSimulacionTests` como están.
3. **A3. El título** (D13). Propuesta: «¡Hola! Identificate y disfrutá tu comida». Alternativas: «Tu comida te espera. Identificate y disfrutá», «¡Buen provecho! Identificate y elegí lo tuyo», o una frase tuya.
4. **A4. Pruebas de este mismo change que cambian, con tu aprobación.** Son de `b5ee4d3`, todavía sin integrar, y cada una se ve fallar contra el código de hoy antes de implementar:
   - `AlmacenSimulacionTests`: la teoría «sin archivo, ilegible o con otro valor, está apagada» pasa a «encendida», en sus cinco casos.
   - `ModoSimulacionTests`, las ocho:
     - la carpeta nueva arranca encendida;
     - «Apagarla se conserva» reemplaza a «Encenderla se conserva»;
     - «En operativo nunca se ve» reemplaza a «Pasar a operativo la apaga»;
     - el arranque en operativo deja el ajuste encendido y sin registro;
     - el ilegible queda encendido;
     - «Apagar la simulación» reemplaza a «Encender la simulación» en la bitácora;
     - «Pasar a operativo no la registra» reemplaza a «Apagada al pasar a operativo»;
     - la escritura que falla es la del apagado y la simulación sigue encendida.
   - `ComboSimulacionTests`: «Sin el modo simulación no hay combo» guarda la simulación apagada en lugar de usar una carpeta nueva.
   - `SimulacionE2ETests`: deja de tocar «Encender la simulación», que en una carpeta nueva ya no está, y suma la medida de la fila: el combo arriba del campo e «Ingresar» a su derecha.
   - De `07a2329` no cambia ninguna. Las pruebas de componentes no registran el almacén y siguen sin combo (A5). Las E2E de antes arrancan ahora con el combo en reposo. Si alguna falla por eso, se para y se te pregunta.
5. **A5. Sin almacén registrado** (D4). Propuesta: la simulación sigue apagada en memoria, así las pruebas de componentes de `07a2329` no ven el combo. Alternativa: encendida también sin almacén, que pone el combo en todas las pruebas de componentes de reposo y obliga a revisarlas una por una.
6. **A6. La decisión 6 en `IDSerialSim/AGENTS.md`.** Propuesta: sumar ahí la decisión 7, del 10/11 a las 10:36, que reemplaza el «apagado por omisión» de la 6 y conserva el «nunca en operativo». Va en un commit de ese repo, sin push. Alternativa: dejarla como está, y entonces la 6 contradice al puesto.
7. **A7. Calendario y recorte.** La ampliación se aplica apenas apruebes, en 1 a 1,5 h, y después va tu prueba en el ejecutable publicado de nuevo. Recorte propuesto: si a las 22:00 la ampliación no está en verde, se revierten sus commits y el change sale como lo probaste a las 09:46. El recorte del punto 1 sigue debajo, como último respaldo.
8. **A8. Talla de la ampliación.** La de la IA es S: 3 escenarios nuevos y 9 modificados sobre el mismo código, sin consulta nueva ni dependencias. La tuya la das acá.

### Decisión del gate de la ampliación

El 10/11/2026, a las 12:08, David respondió «Todo aprobado.» al gate de las tres propuestas del día, con las recomendadas y sin talla propia. Queda así:

- A1 como está, decidido a las 10:36.
- A2 con la propuesta: un ajuste ilegible deja la simulación encendida.
- A3 con la propuesta: el título «¡Hola! Identificate y disfrutá tu comida».
- A4 con la propuesta: cambian sólo las pruebas de su lista, cada una vista fallar antes.
- A5 con la propuesta: sin almacén registrado, la simulación sigue apagada en memoria.
- A6 con la propuesta: la decisión 7 va en `IDSerialSim/AGENTS.md`, en un commit de ese repo y sin push.
- A7 con la propuesta: el recorte a las 22:00.
- A8 sin talla propia, así que la tabla de tallas queda con la de la IA.
