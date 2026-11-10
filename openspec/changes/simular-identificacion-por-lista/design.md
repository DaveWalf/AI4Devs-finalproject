# Design

## Context

Ver `proposal.md` («Why») para la motivación y las decisiones de David. El estado de `07a2329` que este change toca:

- `IdentificacionPorCodigo.Identificar(codigo, proposito)` resuelve el código contra el snapshot activo (`Resolver`), lo clasifica con `ClasificadorCredencial` (RB25), abre la sesión con método `CODE` y escribe `SESSION_OPENED` o `IDENTIFICATION_REJECTED` en la misma transacción, sin el código.
- `MaquinaAtencion.Identificar(codigo)` sólo actúa en reposo: fuera de reposo avisa «Ocupado» y con el error de almacenamiento no hace nada.
- `PantallaAtencion.razor` tiene en reposo un `<input id="codigo">` y el botón `#ingresar`, que llama a `Ingresar()`. Enter en el campo no hace nada: `TeclaRaiz` sólo atiende Escape.
- El modo del puesto vive en `modo-puesto.json`, con `AlmacenModoPuesto` (escritura atómica; ilegible es mantenimiento). Lo usa la página, no la máquina, que no conoce los modos. `CambiarModo` registra `CONFIGURATION_CHANGED` con `Setting = "KIOSK_MODE"`, y pasar a operativo entra con `DAILY_KEY`.
- D10 de `administrar-personas-y-opciones`: ninguna pantalla muestra un código. `PersonaAdministrada` ni siquiera lo lleva.
- `tbComedorSesionesIdentidad.comsidMetodo` no tiene `CHECK` y no viaja en los lotes; la credencial `comcreTipo` sólo admite `SERIAL_CODE`.
- La semilla tiene cuatro personas (`0001` a `0004`) y cuatro operadores (`9001` a `9004`), todos con una credencial de código.

## Goals / Non-Goals

**Goals:**

- Que el combo identifique por el mismo camino que el código, sin una segunda copia de la clasificación de RB25.
- Que el código nunca llegue a la pantalla: ni en el texto ni en los valores del combo.
- Que la seguridad de «un puesto operativo nunca muestra la lista» no dependa de que un archivo esté bien escrito.
- Que ninguna prueba existente cambie.

**Non-Goals:**

- Generalizar `AlmacenModoPuesto` y el almacén nuevo en una clase común: son dos archivos chicos y el refactor tocaría pruebas existentes del modo.
- Reordenar la pantalla de identificación o cambiar sus textos actuales.

## Decisions

### D1. El combo lleva la credencial, no el código ni la persona

Cada entrada del combo tiene como valor el `comcreCodigo` (un GUID) de una credencial `SERIAL_CODE`, y como texto el nombre visible de su persona. La página sólo maneja GUID y nombres.

- **Descartada: el código como valor.** Contradice D10 y RB25: el código quedaría en el DOM, a un inspector de distancia.
- **Descartada: la persona como valor.** RB25 clasifica la credencial (vigencia, revocación), no la persona. Elegir una persona obligaría a decidir cuál de sus credenciales usar, una regla que hoy no existe.

### D2. Un solo camino de identificación

`IdentificacionPorCodigo` gana `IdentificarPorCredencial(credencialId, proposito)`. Comparte con `Identificar` todo lo que sigue a la resolución: clasificación, apertura de la sesión y registro. Sólo cambia cómo se encuentra la credencial: por `comcreCodigo` en el snapshot activo, en vez de por valor normalizado. Una credencial que no está en el snapshot activo da `IDENTITY_NOT_RESOLVED` con la causa interna `UNKNOWN_CODE`, como un código desconocido. `MaquinaAtencion` gana `IdentificarPorCredencial` con las mismas guardas que `Identificar` (reposo, ocupado, almacenamiento) y el mismo después (oferta, selección o sin opciones).

- **Descartada: que la página busque el código de la credencial y llame a `Identificar(codigo)`.** El código pasaría por la capa de pantalla, y el registro no podría decir que la identificación vino de la lista.
- **Descartada: un servicio de simulación aparte.** Duplicaría la transacción y la clasificación, y un rechazo podría divergir del código tipeado.

### D3. Quiénes entran en la lista

`IdentificacionPorCodigo.ListarParaSimulacion()` devuelve una entrada por credencial `SERIAL_CODE` del snapshot activo, con el nombre visible de su persona, en orden alfabético con comparación de la cultura `es-AR`. Entran las vigentes y también las revocadas, las vencidas, las todavía no vigentes, las de personas inactivas y las de los operadores, porque son «todas las personas cargadas» y así los rechazos se ven sin conocer códigos. El rechazo lo decide la identificación, no la lista.

- **Descartada: sólo las vigentes.** Esconde los rechazos y repite en la consulta la clasificación de RB25, que entonces podría no coincidir con la de la identificación.

### D4. El modo simulación en su propio archivo

`AlmacenSimulacion` guarda `simulacion.json` (`{"simulacion":"ENCENDIDA"}` o `"APAGADA"`) en la carpeta de datos, con la misma escritura atómica de `AlmacenModoPuesto` (temporal más `File.Move`). Sólo `"APAGADA"` la apaga: sin archivo, ilegible o con otro valor, está encendida, que es su valor por omisión desde la ampliación del 10/11 (D12), como un modo ilegible es mantenimiento, el modo por omisión. Se registra en `MainWindow` como el del modo. Sin almacén registrado, en las pruebas de componentes que no lo usan, la simulación vive en memoria y arranca apagada, así las pruebas de `07a2329` no ven el combo.

- **Descartada: una clave más en `modo-puesto.json`.** Cambia un formato que ya leen las pruebas del modo y mezcla en una escritura dos ajustes que se guardan por separado.
- **Descartada: una tabla en la base.** Pide una migración para un ajuste del equipo, y la base es de la atención.

### D5. La regla efectiva: guardada y en mantenimiento

La página calcula `SimulacionActiva = guardada && _modo == Mantenimiento`. El combo y el interruptor dependen de ese valor, no del archivo. Pasar a operativo y arrancar en operativo no tocan lo guardado: la regla efectiva la mantiene inactiva, y al volver a mantenimiento la simulación está como se la dejó.

Así quedó en la ampliación del 10/11 (D12). Antes, pasar a operativo la guardaba apagada y la registraba, y arrancar en operativo con el archivo encendido la reescribía apagada con `Access = "STARTUP"`. Las dos escrituras y el acceso `STARTUP` salen.

- **Elegida el 10/11 a las 10:36, antes descartada: ignorar el archivo en operativo sin reescribirlo.** Su contra era que la simulación reapareciera al volver a mantenimiento, y eso es justo lo que David pidió: encendida «hasta que la desactive en la configuración».
- **Descartada: confiar en el archivo.** Un archivo editado a mano o una escritura cortada dejarían la lista a la vista en operativo.

### D6. El interruptor en General, aplicado en el acto

En mantenimiento, General muestra `#modo-simulacion` («Simulación: apagada» o «Simulación: encendida») y un botón, `#encender-simulacion` o `#apagar-simulacion`, en la misma fila de acciones que «Pasar a operativo». Se aplica en el acto, como el modo, sin «Guardar». Si no se puede guardar, `#aviso-simulacion` lo dice con `MensajesAtencion.SimulacionNoGuardada`, y la simulación queda como estaba. En operativo, General no muestra la simulación.

- **Descartada: una pestaña nueva.** Cambia las dos pruebas que cuentan nueve pestañas, como en `identificar-por-puerto-serie`, por un solo botón.
- **Descartada: un «Guardar» propio.** El modo, su vecino, se aplica en el acto, y General no tiene nada más que guardar.

### D7. Lo que queda registrado

- La identificación por el combo escribe sus `SESSION_OPENED` o `IDENTIFICATION_REJECTED` de siempre, con una clave más: `Origin = "SIMULATION_LIST"`. La tipeada no la lleva, así sus registros y sus pruebas no cambian.
- La sesión sigue con método `CODE`, porque la credencial es la misma `SERIAL_CODE`.
- El interruptor escribe `CONFIGURATION_CHANGED` con `Setting = "SIMULATION_MODE"`, `Before` y `After` (`ON` u `OFF`), `Result` (`APPLIED` o `FAILED`), `Effective`, `Access`, `ActorPersonId` nulo y `ActorReason = "NO_AUTHENTICATION"`, como `KIOSK_MODE`.
- Ningún registro lleva el código ni el nombre elegido.

- **Descartada: un método `SIMULATION` en la sesión.** Cambia una columna que otras pruebas leen, sin dar más de lo que da `Origin`.

### D8. Enter en el campo del código

El `<input id="codigo">` gana `@onkeydown`: Enter llama a `Ingresar()`, el mismo método del botón. El campo ya se actualiza con `@oninput`, así que Enter envía lo tipeado hasta esa tecla. «Ingresar» se queda.

- **Descartada: un `<form>` con `submit`.** En el WebView2, un `<form>` sin `@onsubmit:preventDefault` recarga la página, y con él Enter y el botón podrían enviar dos veces.
- **Descartada: sacar «Ingresar».** Cambia pruebas existentes (35 referencias a `#ingresar` en 10 archivos) y deja la pantalla táctil atada al teclado en pantalla.

### D9. El combo es un `<select>` nativo

Es un `<select id="simulacion-persona">` con `<label>` visible («Simulación: elegí a la persona»), arriba del campo del código desde la ampliación del 10/11 (D12), al menos 56 px de alto como los otros controles táctiles, y una primera opción sin valor («Elegí a una persona…»). `@onchange` con un GUID válido llama a `Maquina.IdentificarPorCredencial`. La página vuelve a la opción vacía después de cada elección, y un valor que no es un GUID se ignora sin llamar a nada (OWASP: el valor llega del DOM).

- **Riesgo aceptado:** en el WebView2, las flechas sobre el combo cerrado cambian la elección y disparan `change`, así que con teclado una flecha identifica. Es simulación, sólo en mantenimiento y en una pantalla táctil.
- **Descartada: una lista de botones con los nombres.** Suma una segunda paginación a la pantalla de identificación y no es el combo que pidió David.

### D10. Cuándo se lee la lista

La página lee la lista al quedar en reposo con la simulación activa, al encender la simulación y al cerrar Configuración, y la guarda hasta la próxima de esas ocasiones. No la lee en cada dibujado, porque el temporizador de inactividad redibuja cada segundo. Configuración, donde se editan las personas, sólo se abre en reposo, así que la lista no queda vieja en uso. Si quedara, D2 rechaza la credencial ausente.

- **Descartada: leer en cada dibujado.** Sería una consulta por segundo con el puesto en reposo.
- **Descartada: leerla una vez al arrancar.** No vería «Persona sintética 05» después de agregarla.

### D11. Alcance y documentos

`AGENTS.md` y `openspec/config.yaml` suman, en el alcance del corte, la identificación eligiendo a la persona en un combo, sólo en modo simulación y en mantenimiento, por este change. `readme.md` dice cómo encender la simulación en el recorrido de verificación. `docs/aplicacion-del-curso.md` suma la fila del change (regla de `tasks` del `config.yaml`). Sin `prompts.md`: no deja un workflow ni un ajuste humano nuevo.

### D12. Ampliación del 10/11: encendida por omisión y el combo arriba

El 10/11 a las 10:36, después de probar el puesto publicado, David pidió la mejora 1 de su lista y eligió cómo convive con su decisión 6. Lo que cambia:

- **El valor por omisión.** Sin `simulacion.json`, o con uno ilegible, la simulación está encendida (D4); sólo `"APAGADA"` la apaga. La regla efectiva de D5 no cambia, y la seguridad de «un puesto operativo nunca muestra la lista» sigue sin depender del archivo.
- **Las escrituras que salen.** Pasar a operativo ya no guarda la simulación apagada ni registra `SIMULATION_MODE`, y el arranque en operativo ya no la reescribe con `Access = "STARTUP"` (D5). General ofrece «Apagar la simulación» en una carpeta nueva.
- **La fila del código.** El combo va arriba del campo del código. El campo e «Ingresar» van en una fila propia (`.ingreso`, flex, el campo crece y el botón queda a su derecha), los dos de al menos 56 px. «Ingresar» deja el `div.acciones` de reposo, que queda vacío y sale. Su `id` no cambia, así que las 35 referencias de las pruebas siguen encontrándolo.

### D13. El título de reposo

«Identificate para comenzar» pasa a ser «¡Hola! Identificate y disfrutá tu comida»: saluda, invita a disfrutar y conserva la instrucción. Es neutro en género y sirve para cualquier franja. El texto va en `MensajesAtencion.TituloReposo`, como los demás mensajes. «Identificación simulada por código (demostración)» se queda debajo, porque dice qué es la pantalla en este corte.

- **Descartada: una frase por franja («Disfrutá tu almuerzo»).** Suma una regla nueva, qué franja nombra el título fuera de horario o entre dos franjas, que la mejora no pide.
- **Alternativas para el gate:** «Tu comida te espera. Identificate y disfrutá» y «¡Buen provecho! Identificate y elegí lo tuyo».

## Risks / Trade-offs

- [Las flechas del teclado identifican sobre el combo cerrado (D9)] → Aceptado para la simulación; si molesta, se cambia el control sin tocar la spec, que no dice cómo se elige.
- [Dos personas con el mismo nombre visible quedan como dos entradas iguales] → Aceptado: los datos son sintéticos y únicos. El combo no puede mostrar el código para distinguirlas (D10 de administración).
- [Un puesto nuevo muestra todas las personas desde el primer arranque, porque arranca en mantenimiento con la simulación encendida (D12)] → Aceptado por la decisión de David del 10/11 a las 10:36: el operativo, el modo de servicio real, nunca la muestra (D5), y apagarla en General se conserva. Atender en mantenimiento deja la lista a la vista, que es lo que la decisión 6 del 09/11 quería evitar por omisión.
- [El combo con cientos de personas es largo de recorrer] → Aceptado: es una ayuda de demostración y prueba, no el método de identificación del producto.
- [El corte del 11/11 a las 12:00] → Recorte del punto 1 del gate: sin la tarea 6.2 (E2E) si no alcanza; sin el change entero si los grupos 1 a 6 no están verdes a las 22:00 del 10/11.

## Migration Plan

Sin migración de base: la versión de esquema sigue en 4. Un puesto existente arranca sin `simulacion.json`, es decir con la simulación apagada, y se comporta como hoy. Para volver atrás alcanza con revertir el merge: `simulacion.json` queda en la carpeta de datos sin que nadie lo lea.
