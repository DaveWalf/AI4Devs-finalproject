# Design

## Context

Ver `proposal.md` (Why). Lo que el código ya tiene, medido el 06/11/2026 sobre `18cce75`:

- **La configuración ya es un snapshot versionado.** Cada tabla de configuración lleva `comvcCodigo` en su clave: personas (`0001_transaccion_consumo.sql:58-67`), credenciales (`:69-83`, con el código único por versión, tipo y valor en `:81`), tipos de servicio (`:85-92`), ámbitos (`:94-101`), categorías (`:103-110`), opciones (`:113-126`, con el importe no negativo en `:125`), sus cargas (`:128-139`, unidades mayores que cero en `:138`) y reglas (`:142-166`, límite no negativo en `:163`); los permisos de operador, en `0002_permisos_operadores.sql:6-19`. La versión es `tbComedorVersionesConfiguracion` (`0001:25-38`: número de configuración único, versiones de políticas, catálogo y padrón, versión de esquema, hash, estado sin `CHECK` y fechas) y la activa la elige `tbComedorConfiguracionPuestos.comvcCodigoActiva` (`0001:44`). `PuestoLocal.Leer` (`PuestoLocal.cs:16-39`) la lee dentro de la transacción de quien la usa, y todas las lecturas filtran por ella.
- **El cupo usado no depende de la versión**: `tbComedorUsosCupo` se indexa por persona, ámbito, categoría y fecha (`0001:234-243`), y los consumos guardan su `comvcCodigo` (`0001:212`, con la FK de `:228-231`). Una versión nueva con los mismos identificadores conserva el uso del día y los consumos siguen apuntando a la suya.
- **Versión de esquema 3**, con tres migraciones (`BaseComedor.cs:9-14`) y el rechazo de una versión mayor (`:44-47`). La fijan 8 aserciones en 8 pruebas existentes (`git grep -n -E 'Equal\(3L?, .*(user_version|SchemaVersion)' -- tests` da 8 líneas): `AperturaBaseTests.cs:45` (con la lista de 23 tablas de `:11-35`), `MigracionLotesTests.cs:39` y `:57`, `MigracionesTests.cs:33`, `SemillaAtencionTests.cs:69` (que además quita a mano las tablas de las versiones 2 y 3 en `:85`), `ServicioDemostracionTests.cs:27` y `BitacorasDelPuestoTests.cs:183` y `:203`.
- **Inserciones por posición** en tablas de configuración: `FixtureSintetico.cs:67` (versión) y `:130` (opción), que usan 10 archivos de prueba; `AperturaBaseTests.cs:99` y `:108`; `CatalogoDePrueba.cs:25`; y la semilla `semilla_demostracion.sql:9`.
- **La evaluación única** es `EvaluadorDisponibilidad.Evaluar` (`EvaluadorDisponibilidad.cs:12-28`): habilitación (`EvaluadorHabilitacion`) y, sin motivo, cupo; su comentario de `:8` ya dice que las franjas se suman ahí. La llaman la oferta (`ConsultaDisponibilidad.cs:64-65`) y la confirmación (`UnidadConfirmacion.cs:81-85`), las dos con la opción que lee `LecturaDecision.LeerOpcion` (`LecturaDecision.cs:13-55`). La única prueba existente que la llama es el ayudante de `EvaluadorDisponibilidadTests` (`git grep -c` da 1).
- **Los motivos** salen de `MotivoDenegacion` (`ModelosCupo.cs:4-12`) y se traducen a código de contrato en `LecturaDecision.cs:128-137`. El receptor acepta sólo los códigos que enumera `ValidadorLote.cs:63-64`.
- **La credencial de una sesión** se lee uniendo la sesión con la credencial de la misma versión y, a la vez, de la activa (`LecturaDecision.cs:66-73`): si la sesión se abrió en otra versión, no encuentra ninguna.
- **El diálogo de Configuración** (`PanelConfiguracion.razor:9-59`) tiene «Estilo visual», la impresora simulada, el estado de las bitácoras y un solo «Guardar» que guarda el estilo en `estilo-visual.json`. La pantalla lo abre sólo en reposo y registra `CONFIGURATION_OPENED`, `CONFIGURATION_CANCELLED` y `CONFIGURATION_CHANGED` con `VISUAL_STYLE` o `PRINTER_SIMULATION_MODE`, actor nulo y `NO_AUTHENTICATION` (`PantallaAtencion.razor:340-409`).
- **Tipos de registro**: los de B (D3 y D8 de `escribir-bitacoras-externas`). Para C faltan los ajustes `PERSON` y `MENU_OPTION` de `CONFIGURATION_CHANGED` y la causa de una retención; no hace falta un tipo nuevo, y un tipo desconocido igual iría a la general.

## Goals / Non-Goals

**Goals:**

- Que lo que se edita en el puesto cambie lo que se ofrece y lo que se confirma por el mismo camino que un snapshot provisto, sin una segunda lógica.
- Que ninguna edición borre un hecho ni una versión, y que cada una se reconstruya desde la bitácora general.
- Que la franja nunca separe la oferta de la confirmación.

**Non-Goals:**

- Lo que el proposal deja en «Non-goals».
- Integridad criptográfica entre versiones: el hash identifica el contenido, no lo firma.
- Concurrencia entre dos editores: hay un solo puesto y Configuración abre en reposo; el `BEGIN IMMEDIATE` alcanza.

## Decisions

### D1. Cada guardado copia la versión entera y la activa en una transacción

`AdministracionLocal` (en `Infrastructure.Sqlite`) abre un `BEGIN IMMEDIATE`, lee la versión activa, crea una versión nueva con `comvcCodigo` nuevo, copia todas las filas de configuración de la activa con `INSERT … SELECT` y listas de columnas explícitas, en el orden de sus FK (personas, credenciales, tipos, ámbitos, categorías, opciones, cargas, reglas, permisos, franjas), aplica los cambios, cambia `comvcCodigoActiva`, pasa la anterior a `SUPERSEDED` y registra todo en la bitácora antes del commit. Las personas, credenciales y opciones que no cambian conservan sus identificadores.

- **Descartada:** editar la versión activa en su lugar. No conserva las anteriores, que pidió David, y una opción cambiada en el lugar haría que un consumo viejo apunte a un nombre y un importe que no fueron los suyos.
- **Descartada:** versionar sólo las tablas que cambian. Todas las lecturas filtran cada tabla por la versión activa (`PuestoLocal`), así que una versión parcial dejaría al puesto sin catálogo o sin personas.

### D2. Migración 0004 con dos tablas nuevas y ninguna tabla existente tocada

`tbComedorFranjasOpcionesMenu` (`comvcCodigo`, `commenCodigo`, `comfraCodigo`, `comfraHoraInicio` y `comfraHoraFin` en `HH:mm`, con FK a la opción y un `CHECK` de inicio distinto del fin) y `tbComedorOrigenesVersiones` (`comvcCodigo` con FK a la versión, `comvoOrigen` con `CHECK IN ('LOCAL', 'INWEB')`). Una versión sin fila de origen es local. La base queda en la versión de esquema 4, con 25 tablas.

Pruebas existentes que cambian, medidas sobre `18cce75`, sólo en lo que afirman de la versión y de las tablas: las 8 del contexto. `SemillaAtencionTests.Atencion_sobre_una_base_de_la_demostracion_en_version_1` además tiene que quitar las dos tablas nuevas al simular la versión 1, o la 0004 fallaría al volver a crearlas.

- **Descartada:** columnas nuevas en `tbComedorVersionesConfiguracion` o en `tbComedorOpcionesMenu`. Además de las 8, romperían las inserciones por posición del contexto: `FixtureSintetico.cs:67` y `:130` sostienen casi todo `Infrastructure.Sqlite.Tests`, y también `AperturaBaseTests` y `CatalogoDePrueba`.
- **Descartada:** sin migración, con las franjas en un archivo por versión. Ninguna prueba existente cambiaría, pero la franja quedaría fuera de la transacción de RB06 y de la versión que la decide: una caída entre el archivo y la base dejaría una versión activa con franjas que no son las suyas.

### D3. «INWeb conectado» es el origen de la versión activa

La edición se bloquea si la versión activa tiene origen `INWEB`. En este corte nada crea esas versiones: el bloqueo se prueba insertando una, y la importación de INWeb las creará con ese origen. Se bloquean los dos paneles y el guardado en el servicio; el estilo y la impresora quedan editables, porque son del equipo (RB21).

- **Descartada:** una variable de entorno o un archivo bandera que diga «INWeb conectado». Diría algo que nada comprueba, y el bloqueo quedaría separado de la versión que efectivamente decide.
- **Descartada:** dejar el bloqueo para cuando exista INWeb. La decisión de David del 04/11 quedaría sin especificar y la importación tendría que inventar después cómo convive con lo editado.

### D4. La franja va en `EvaluadorDisponibilidad`, entre la habilitación y el cupo

`Evaluar` suma una sobrecarga con las franjas de la opción y la hora local; la firma actual la llama sin franjas, que es «todo el día». El orden queda: persona, credencial, reglas (regla cero, sin regla), franja, cupo. Así una persona sin derecho no lee «fuera de horario» y las reglas que eligió la habilitación siguen llegando al cálculo de «Hoy ya usaste:». La oferta y la confirmación leen las franjas en `LecturaDecision.LeerOpcion` y calculan la hora local con la zona del puesto.

- **Descartada:** cambiar la firma existente. Cambiaría el ayudante de `EvaluadorDisponibilidadTests`, una prueba existente, sin ganar nada.
- **Descartada:** la franja en `EvaluadorHabilitacion`. Mezclaría una condición de la opción con las de la persona y sus reglas.
- **Descartada:** filtrar por horario en la pantalla. Es lo que prohíbe «Una sola evaluación ofrece y confirma».

### D5. Fuera de horario es `SERVICE_NOT_ENABLED` con la causa `OUTSIDE_SERVICE_HOURS`

El dominio suma `MotivoDenegacion.OutsideServiceHours`. El código de contrato es `SERVICE_NOT_ENABLED`, el mismo del evento de salida y del resultado guardado; la causa viaja en `ResultadoConfirmacion.ReasonCause`, en la retenida de la oferta y en el contexto de `OPTIONS_OFFERED` y `CONSUMPTION_DENIED` como `Cause`. La pantalla elige el mensaje por la causa. Es lo que hace RB25 con `IDENTITY_NOT_RESOLVED` y sus causas internas.

- **Descartada:** un código de contrato nuevo. `ValidadorLote.cs:63-64` lo rechazaría: el evento de un rechazo por horario llegaría inválido al receptor, y habría que cambiar el receptor y su contrato antes del corte. Ninguna prueba existente del receptor enumera los códigos, así que el costo es de alcance, no de pruebas.

### D6. Franja en la hora local del puesto, de inicio incluido a fin excluido

Cada franja tiene inicio y fin en `HH:mm` de la zona del puesto, la misma de la fecha de negocio. Contiene la hora `t` si el inicio es menor que el fin y `inicio ≤ t < fin`, o si el inicio es mayor y `t ≥ inicio o t < fin` (cruza la medianoche). Una opción con varias franjas está disponible si alguna la contiene. Un instante del reloj, como `ValidToUtc` en las reglas: el fin no se incluye.

- **Descartada:** franjas en UTC. Cambiarían de hora local con el horario de verano y no se leerían como se cargan.
- **Descartada:** días de la semana. No los pidió David y suben la talla; quedan en el recorte.

### D7. Qué cambia la versión de una opción

Nombre, servicio, importe y cargas suben `commenVersionMenu`, porque son lo que la persona vio y lo que RB10 manda reconfirmar. Las franjas no: la confirmación las vuelve a evaluar igual, y subir la versión sólo produciría reconfirmaciones sin cambio de importe. Las opciones sin cambios conservan su versión, así que activar una versión nueva no pide reconfirmar a nadie.

- **Descartada:** subir la versión con cualquier cambio. Una franja corregida haría reconfirmar a quien está eligiendo una opción que sigue costando lo mismo.

### D8. Quitar una persona es darla de baja y revocar su código

En la versión nueva la persona queda con `comperActivo = 0` y su credencial con `comcreFechaRevocacionUtc` en el instante del guardado. Nada se borra: el código sigue ocupado y no puede volver a asignarse (RB25, D-C20, AT55). Quitar una opción sí la saca de la versión nueva, porque una opción no es una identidad: sus consumos apuntan a la versión en que se registraron.

- **Descartada:** borrar la persona de la versión nueva. Su código quedaría libre para otra persona en la versión siguiente, que es la reasignación que AT55 rechaza, y la baja no se distinguiría de un código desconocido.

### D9. Un cupo es una regla por persona, servicio y categoría

El panel muestra y edita, por persona, un límite diario por servicio y categoría. Guardar reemplaza las reglas de ese par en la versión nueva por una sola, con prioridad 1, sin vigencia y con el ámbito del puesto (el único que hay); quitar el cupo deja el par sin reglas. Un par con varias reglas se muestra con el límite de la que hoy decide, y avisa que tiene reglas con vigencia o prioridad que se reemplazan si se edita.

- **Descartada:** un editor de reglas completo, con prioridad, vigencia y ámbito. Permitiría armar desde la pantalla el empate contradictorio que RB02 prohíbe y sube la talla; queda en el recorte.

### D10. El código se escribe y no se muestra

El panel nunca muestra un código. Al agregar una persona, el campo es de sólo dígitos, enmascarado y sin autocompletado; el valor sólo sale de la pantalla hacia el servicio, que lo guarda como `comcreValorNormalizado`. La bitácora registra `CredentialChanged` y el `CredentialId`, nunca el valor.

- **Descartada:** mostrar los códigos para administrarlos. El código no es un secreto, pero el puesto ya se compromete a no mostrarlo después de enviarlo ni escribirlo en registros (`atencion-puesto`, RB25), y una lista de códigos en una pantalla pública lo contradice.

### D11. El servicio valida; la pantalla sólo arma la edición

`AdministracionLocal.Guardar` recibe la edición entera de un panel y devuelve aplicado (con la versión nueva), rechazado (con cada campo y su código de error) o denegado. Valida largos, caracteres de control, dígitos del código, rangos de límite, importe y unidades, franjas y la unicidad del código en todas las versiones, todo con SQL parametrizado (OWASP A03) y antes de escribir. La pantalla repite las validaciones de forma sólo para avisar antes.

- **Descartada:** validar sólo en Razor. Cualquier otro llamador del servicio, como la importación futura, quedaría sin validación.

### D12. Dos paneles con su propio «Guardar»

El diálogo suma los botones «Personas habilitadas» y «Opciones del comedor», de 56 px, que abren cada panel en lugar del contenido del diálogo, con «Guardar», «Cancelar» y Escape. El «Guardar» del diálogo sigue siendo el del estilo. Las pestañas son de D.

- **Descartada:** un solo «Guardar» para todo el diálogo. Mezclaría un archivo de estilo con una versión de la base, y un estilo que no se puede escribir dejaría a medias un guardado de cupos. Ninguna prueba existente cambia con ninguna de las dos; se descarta por diseño.

### D13. Un registro por entidad, en la transacción de la versión

Cada persona u opción cambiada deja un `CONFIGURATION_CHANGED` con `Setting` (`PERSON` o `MENU_OPTION`), su identificador, `Before` y `After` (nulo al agregar), `Result` (`APPLIED`), `ConfigurationVersionBefore` y `ConfigurationVersionAfter`, `ActorPersonId` nulo y `ActorReason` `NO_AUTHENTICATION`, en la misma transacción que la versión: si la versión no se activa, no queda ningún registro que diga lo contrario. Un guardado rechazado o denegado deja uno solo, con `Result` `REJECTED` o `DENIED`, el motivo (`INVALID_FIELD`, `DUPLICATE_CODE` o `INWEB_OWNS_DATA`) y los campos señalados, sin valores de códigos. El escritor de B los lleva a la general sin cambios.

- **Descartada:** un registro por guardado con la versión entera. No dice qué cambió, que es lo que pide el diseño de administración («campos cambiados»).
- **Descartada:** un actor genérico. Es la identidad ficticia que D9 de B descarta; D completa el actor con la clave del día.

### D14. Números de versión y hash

La versión nueva toma el número de configuración siguiente y sube, de las versiones de padrón, políticas y catálogo, la de lo que cambió: personas y credenciales, reglas, y opciones, cargas o franjas. Su versión de esquema es la 4, y su hash es el SHA-256 del contenido copiado, serializado con `JsonCanonico` de `Contracts` en un orden fijo.

- **Descartada:** el hash en ceros, como la semilla. Sería un valor inventado para una versión que el puesto sí puede describir.

## Risks / Trade-offs

- [Cualquiera que toque el engranaje edita personas y cupos hasta la clave del día de D] → es el punto 5 del gate; cada edición queda en la general con su antes y su después.
- [Copiar la versión entera en cada guardado] → con decenas de personas y opciones son unos cientos de filas en una transacción; se mide en la tarea 3.3 con la semilla de quince y 200 personas sintéticas. Las versiones viejas crecen la base sin límite: purgarlas es de la conservación que B dejó afuera.
- [La credencial de una sesión abierta en otra versión no se encuentra (`LecturaDecision.cs:66-73`)] → C no lo vuelve alcanzable, porque su única revocación es la de una baja y `PERSON_INACTIVE` decide antes; lo vuelve alcanzable cambiar un código o importar una versión de INWeb (punto 10 del gate). Queda como precondición de ese change.
- [La bitácora general guarda nombres visibles] → son datos sintéticos en este corte; con datos reales, la conservación y el acceso que B dejó afuera pasan a ser previos al piloto.
- [Una franja mal cargada deja a todos sin almuerzo] → el mensaje dice que es por el horario y el panel muestra las franjas de cada opción; corregirla es otro guardado.
- [Las 8 pruebas existentes del punto 4] → cambian sólo en el número de versión y en las tablas; si otra falla, se para y se pregunta (punto 12 del gate).
- [El panel en 1024 × 768 con muchas personas] → la lista se desplaza en vertical con controles de 56 px, como la ventana angosta; se ve en las capturas de la tarea 4.3.

## Migration Plan

La 0004 crea las dos tablas en su propia transacción, como las anteriores, y lleva la base a la versión 4; una base de la versión 1, 2 o 3 pasa por las que le faltan. La semilla de la demostración no cambia: su versión no tiene fila de origen, así que es local y editable. Volver atrás exige una base nueva: un puesto de la versión 3 rechaza una base de la 4 con el error de almacenamiento («Base de una versión de esquema desconocida»), sin tocarla.
