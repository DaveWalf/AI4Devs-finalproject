# Proposal

## Why

David pidió el 04/11 una sección de Configuración «donde pueda ver a todas las personas habilitadas, qué opciones y cupos tiene, y que permita además agregar o eliminar personas, modificar opciones y cupos», y otra para «las opciones con todos sus parámetros, sumando franjas horarias en las cuales están habilitados». Hoy el puesto sólo lee personas, opciones y reglas de un snapshot provisto por SQL, y no tiene horarios: cambiar un cupo exige tocar la base a mano.

Es el tercero de los cuatro changes (A a D) que David decidió llevar a la final, con corte el 11/11 a las 12:00, y aplica su decisión del 04/11 sobre el dueño de los datos: «Puesto hasta que haya INWeb».

Origen: historia HU03.3 («Configurar el equipo sin red», escenario HU03-F, que este change precisa con esa decisión) y los tickets TK02 (interfaz), TK01 (la franja en la evaluación) y TK03 (versión local y migración).

## What Changes

- **Dos paneles nuevos en Configuración**, abiertos desde el diálogo del engranaje: «Personas habilitadas» y «Opciones del comedor», cada uno con su «Guardar» y su «Cancelar». El «Guardar» del diálogo sigue guardando sólo el estilo.
- **Personas**: la lista con su estado, sus cupos por categoría y las opciones que esos cupos habilitan; agregar una persona con su nombre, su código y sus cupos; quitarla, que la da de baja y revoca su código sin borrar nada; y modificar su nombre y sus cupos. El código se escribe y nunca se muestra ni se registra.
- **Opciones**: la lista con todos sus parámetros (nombre, servicio, importe, cargas de cupo y franjas); agregar, quitar y modificar opciones. Cambiar el nombre, el importe, el servicio o las cargas crea una versión nueva de la opción, que dispara la reconfirmación de RB10 si alguien la tenía elegida.
- **Franjas horarias por opción**, en la hora local del puesto. Una opción sin franja sigue disponible todo el día. La franja entra a la evaluación única de A («Una sola evaluación ofrece y confirma»): fuera de ella la opción se retiene en la oferta y se deniega al confirmar, con el motivo de contrato `SERVICE_NOT_ENABLED` y la causa interna `OUTSIDE_SERVICE_HOURS`.
- **Cada guardado es una versión local nueva** de la configuración, completa, que se activa en la misma transacción y conserva las anteriores; un guardado inválido no cambia nada.
- **Con INWeb como dueño, la edición se bloquea**: si la versión activa vino de INWeb, los dos paneles sólo muestran y el guardado se deniega.
- **Cada guardado queda en la bitácora general** de B, con el antes, el después, el resultado, las dos versiones y el actor nulo con `NO_AUTHENTICATION`, en la misma transacción que la versión.
- **Migración 0004**: dos tablas nuevas, las franjas de cada opción y el origen de cada versión. Ninguna tabla existente cambia.
- **`AGENTS.md`** y el contexto de `openspec/config.yaml` dejan de nombrar la administración de personas y opciones en la segunda ola.

## Non-goals

Lo que el punto 1 del gate deja para después del Typeform: cambiar el código de una persona existente; crear tipos de servicio, categorías o ámbitos de cupo; vigencias y prioridades en las reglas que se editan; días de la semana en las franjas; una pantalla para ver o volver a una versión anterior; los permisos de operador; e importar un paquete de INWeb. Queda para D todo lo demás de Configuración: pestañas, modos, clave del día, monitor, red, conectividad, impresora, logos, el rebote y el ícono.

## Capabilities

### New Capabilities

- `administracion-local`: la edición en el puesto de personas, códigos, cupos, opciones y franjas mientras no haya INWeb, con una versión local por guardado, el bloqueo cuando la versión activa es de INWeb, la validación de lo que se escribe y su registro en la bitácora.

### Modified Capabilities

- `habilitacion-local`: la franja de la opción entra a la evaluación única (un requisito nuevo), «Una sola evaluación ofrece y confirma» la suma a su lista y «Vigencia de las reglas…» precisa que la única restricción horaria es la franja que la configuración declara.
- `atencion-puesto`: «Aviso claro cuando no queda ninguna opción» suma el motivo de fuera de horario.

`bitacoras-externas` no cambia: los registros nuevos son `CONFIGURATION_CHANGED`, que ya va a la general, y la causa interna se suma al contexto sin cambiar ningún campo.

## Impact

- **Código**: `Domain` (la franja y su lugar en `EvaluadorDisponibilidad`, con una sobrecarga que conserva la firma actual), `Infrastructure.Sqlite` (migración 0004, el servicio de administración con la versión local y la lectura de franjas) y `Kiosk` (los dos paneles y los mensajes).
- **Pruebas**: nuevas en los tres proyectos y un paso del E2E. **Cambian ocho pruebas existentes**, todas por la versión de esquema 4 (punto 4 del gate y D2); ninguna otra, según lo medido al proponer.
- **Documentos**: `AGENTS.md`, `openspec/config.yaml`, `readme.md`, `docs/administracion-y-bitacoras.md` (la precisión de la decisión del 04/11), `docs/modelo-datos.md` (las dos tablas) y `docs/aplicacion-del-curso.md`.
- Sin dependencias nuevas, sin cambios en la CI ni en el receptor: el código de contrato no cambia (D5).

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David | La das en el gate | | |
| La IA (Claude Code, Opus 5.5), 06/11/2026 | **L** | 8 a 12 h | 10,4 a 15,6 h de capacidad |

**Por qué la IA da L y no M.** B (M, 4 a 6 h) proyectaba lo que la base ya guardaba. C agrega datos editables con validación, una versión completa por guardado, una migración que mueve ocho pruebas, una condición nueva en la evaluación que decide consumos y dos paneles con formularios táctiles. Como referencia de reloj, A (S) se aplicó en 0,35 h y B (M) en 0,7 h.

## Para el gate de David

1. **El recorte que hace caber C y D antes del 11/11 a las 12:00** (lo decidís vos). Propuesta: entra el núcleo de este change tal como lo escriben las specs; queda para después del Typeform lo de «Non-goals». Calendario: gate y apply de C el 07/11, su verificación y archivado el 08/11; proponer D el 08/11 y aplicarlo el 09/11, verificarlo y archivarlo el 09 o el 10/11; el 10/11, el pase al SDK estable; el 11/11 a la mañana, sólo margen y el PR. Si C no está archivado y en verde el 08/11 a la noche, D se recorta a lo mínimo (la clave del día y el rebote) antes que mover el corte.
2. **Las franjas como regla que retiene opciones.** Los requisitos confirmados del cliente (línea 71 de esa nota, en la base de conocimiento, fuera del repo) dicen que en su primera versión las franjas «no deben convertirse silenciosamente en una regla de rechazo»; tu pedido del 04/11 las pide. Propuesta: se convierten en regla, pero no en silencio: es una capacidad configurable por opción, una opción sin franja sigue disponible todo el día, la semilla no carga ninguna, y el motivo y el mensaje dicen que es por el horario. Así la primera versión del cliente no rechaza por horario mientras nadie cargue franjas. Alternativa: franjas sólo informativas, que se ven y no deciden; contradice «en las cuales están habilitados».
3. **Cómo se representa «INWeb conectado» en un corte sin INWeb, y qué bloquea** (D3). Propuesta: es el origen de la versión activa, `LOCAL` o `INWEB`, en una tabla nueva; sin fila, `LOCAL`. Con una versión de INWeb activa se bloquean los dos paneles nuevos; el estilo y la impresora siguen editables, porque son del equipo (RB21). En este corte nada crea versiones de INWeb: el bloqueo se prueba con una versión insertada por la prueba, e importar el paquete de INWeb queda afuera. Alternativas: una variable de entorno o un archivo bandera, que dirían «conectado» sin que nada lo esté; o no representarlo, que deja la decisión del 04/11 sin especificar.
4. **Migración 0004, con las pruebas existentes que cambian** (D2). Propuesta: sí, con dos tablas nuevas y sin tocar las existentes. Cambian ocho pruebas, sólo en la versión de esquema que afirman (de 3 a 4) y, en dos, la lista de tablas o las tablas que quitan: `AperturaBaseTests.Base_vacia_queda_en_version_3_con_las_23_tablas`, `MigracionesTests.Base_en_version_1_se_actualiza_sin_perder_el_consumo`, `MigracionLotesTests.Base_en_version_2_con_un_evento_pendiente_queda_en_version_3`, `MigracionLotesTests.Base_en_version_1_queda_en_version_3_con_las_tablas_de_lotes_vacias`, `SemillaAtencionTests.Atencion_sobre_una_base_de_la_demostracion_en_version_1`, `ServicioDemostracionTests.Primer_arranque_siembra_los_datos_sinteticos`, `BitacorasDelPuestoTests.Arranque_y_cierre` y `BitacorasDelPuestoTests.Version_desconocida`. Sin migración no hay dónde guardar la franja dentro de la versión; con una columna nueva en una tabla existente fallaría además casi todo `Infrastructure.Sqlite.Tests`, porque `FixtureSintetico.cs:67` inserta por posición.
5. **El actor de Configuración hasta la clave del día de D** (D13). Propuesta: como en B, actor nulo con `NO_AUTHENTICATION`, sin inventar a nadie. D tiene que completarlo, y hasta entonces cualquiera que toque el engranaje edita personas y cupos: es aceptable en la demostración y no en un piloto.
6. **`AGENTS.md`, `openspec/config.yaml` y `docs/administracion-y-bitacoras.md`** (tareas 5.1 y 5.2). `AGENTS.md` deja de poner «administración» en la segunda ola sin matiz y nombra este change y su recorte. El contexto de `openspec/config.yaml` todavía dice que las bitácoras están fuera del corte, algo que B dejó desactualizado: se corrige junto. `docs/administracion-y-bitacoras.md` (líneas 34 a 38, INWeb maestro «sin edición local paralela») suma la precisión de tu decisión para el período sin INWeb.
7. **Rama.** `feature/administrar-personas-y-opciones-dw`, apilada sobre `feature/escribir-bitacoras-externas-dw` (`18cce75`). Su PR apunta a esa rama hasta que la integres, y después a `final-project-dw`.
8. **Tallas.** La de la IA es L; la tuya la das acá.
9. **El motivo de fuera de horario** (D5). Propuesta: el código de contrato sigue siendo `SERVICE_NOT_ENABLED` y la causa interna `OUTSIDE_SERVICE_HOURS` va a la bitácora y al mensaje, como RB25 hace con `IDENTITY_NOT_RESOLVED` y sus causas. Alternativa: un código de contrato nuevo, que el receptor hoy rechaza (`ValidadorLote.cs:63-64` enumera los válidos): un rechazo por horario dejaría su evento como inválido en el receptor, y habría que cambiar el receptor y su contrato antes del corte.
10. **La credencial de una sesión entre versiones** (hallazgo, riesgo 3 del design). `LecturaDecision.cs:66-73` busca la credencial de la sesión en la versión en que se abrió y además en la activa; si son distintas, no encuentra nada y la confirmación sigue como si la sesión no tuviera credencial. C activa versiones, pero no lo vuelve alcanzable: su única revocación es la de una baja, que deja a la persona inactiva, y `PERSON_INACTIVE` decide antes que la credencial (escenario «Baja con una sesión abierta»). Lo vuelve alcanzable lo que revoque un código dejando activa a la persona: cambiar el código o importar una versión de INWeb, los dos fuera de C. Propuesta: no tocarlo en C y anotarlo como precondición de ese change. Alternativa: corregirlo acá con una tarea chica y una prueba que arme la versión a mano.
11. **Cupos simplificados** (D9). Propuesta: el panel edita un límite por servicio y categoría de cada persona, y guardarlo deja una sola regla de prioridad 1, sin vigencia y con el ámbito del puesto, en lugar de las que había para ese par. Si había vigencias o prioridades distintas en ese par, se pierden en la versión nueva (siguen en la anterior). Alternativa: un editor de reglas completo, con prioridad y vigencia, que sube la talla y permite armar empates contradictorios desde la pantalla.
12. **Pruebas existentes y paradas.** Fuera de las ocho del punto 4, ninguna cambia. Si alguna otra falla durante el apply, se para y se te pregunta, como en A y en B.
