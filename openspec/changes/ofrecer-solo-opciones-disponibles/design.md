# Design

## Context

En `9b179d1`, `MaquinaAtencion.Identificar` abre la sesión y llama a `CargarCatalogo`, que lee todas las opciones del snapshot activo con `ConsultaCatalogo.Leer()`, sin mirar a la persona. La decisión se toma recién en `UnidadConfirmacion.Confirmar`, bajo el bloqueo de escritura (pasos 4 y 5): lee la opción y sus cargas (`LeerOpcion`), si la persona está activa (`LeerPersonaActiva`), la credencial de la sesión (`LeerCredencialDeSesion`), las reglas candidatas del servicio (`LeerReglasCandidatas`) y los usos de la fecha de negocio (`LeerUsos`). Después compone `EvaluadorHabilitacion.Evaluar` y, si no hay motivo, `EvaluadorCupo.Evaluar` (`UnidadConfirmacion.cs:78-88`). Esas lecturas son métodos privados de `UnidadConfirmacion`.

En los catálogos de nueve y de quince, todas las opciones son de ALMUERZO (tipo `…0005`, categoría `…0007`) o de REFRIGERIO (tipo `…0025`, categoría `…0026`), y `0001` tiene reglas de las dos. Con el día limpio, la oferta de `0001` es el catálogo entero, y por eso las pruebas de distribución no cambian. El cupo agotado dice hoy «Ya registraste este servicio. Consultá al responsable del comedor» (`MensajesAtencion.DeMotivo`).

La regla general de David del 04/11 pide que la disponibilidad la decida la misma evaluación que confirma, y que las franjas y los turnos se sumen ahí.

## Goals / Non-Goals

**Goals:**

- La persona ve sólo lo que puede consumir, y lo que falta tiene su explicación a la vista.
- Una sola evaluación del dominio para la oferta y para la confirmación, que el change C extiende con las franjas sin tocar la pantalla.
- La oferta se reconstruye desde la bitácora.

**Non-Goals:**

- Franjas, edición de datos y bitácoras externas (changes C y B).
- La pantalla de demostración anterior y los mensajes de rechazo al confirmar.

## Decisions

### D1. `EvaluadorDisponibilidad` en el dominio compone la decisión

`Domain` suma `EvaluadorDisponibilidad.Evaluar(personaActiva, credencial, ahora, servicioId, cargas, candidatas, personaId, fecha, usos)`, que devuelve la `DecisionCupo` y las reglas aplicables. Es la composición de `UnidadConfirmacion.cs:80-88` movida tal cual: primero `EvaluadorHabilitacion` y, sin motivo, `EvaluadorCupo`. `UnidadConfirmacion` pasa a llamarlo, y la oferta también. El change C le suma la franja de la opción como una condición más.

Descartada: repetir la composición en la consulta de la oferta. Serían dos copias de RB02 que con la primera condición nueva dejan de coincidir, que es justo lo que la regla general prohíbe.

### D2. Las lecturas de la decisión, compartidas; `ConsultaDisponibilidad` nueva

Los cinco métodos de lectura de `UnidadConfirmacion` pasan, sin cambios, a una clase `internal static LecturaDecision` del mismo proyecto, que reciben conexión y transacción. `ConsultaDisponibilidad.Ofrecer(sesionId, personaId)`, pública, abre una transacción de escritura corta y lee el puesto, las opciones del snapshot activo en el orden de `ConsultaCatalogo` y, para cada una, lo mismo que la confirmación. Evalúa cada opción con `EvaluadorDisponibilidad` en el instante del reloj y escribe el registro de D7. Devuelve las ofrecidas (`OpcionDeCatalogo`), las retenidas con su motivo y las categorías agotadas con nombre, uso y límite.

Una categoría está agotada si alguna regla aplicable de la persona tiene límite mayor que cero y su uso del día lo alcanza. Las categorías salen de las reglas que devuelve la evaluación de cada opción, sin repetirse, en el orden de la primera opción del catálogo que las cobra. El nombre es `comcatNombre`.

Descartada: filtrar con una consulta SQL que una opciones, reglas y usos. Repetiría RB02 en SQL, con la vigencia, la regla cero y la prioridad, y divergiría del dominio.

### D3. La línea «Hoy ya usaste:» nombra sólo lo agotado

`MaquinaAtencion` expone `UsoDelDia`: `null` sin categorías agotadas, y si no «Hoy ya usaste: » seguido de cada categoría como «{nombre en minúsculas, es-AR} {uso} de {límite}», separadas por « · ». `PantallaAtencion.razor` la muestra en un `p#uso-del-dia` debajo del saludo, en la selección y en la pantalla sin opciones. La lista de opciones es la de la oferta, y `Elegir` sólo acepta una ofrecida, como hoy acepta sólo una del catálogo.

Descartada: listar también lo usado sin agotar. Suma texto sin explicar ninguna ausencia, y la regla de David pide la explicación de lo que falta.

### D4. Estado `SinOpciones`

`EstadoAtencion` suma `SinOpciones`. Con la oferta vacía, `Identificar` deja ese estado en lugar de `Seleccion`. La pantalla muestra `p#mensaje` con «No tenés opciones disponibles ahora», la línea de D3 o el motivo, y `button#listo` «Listo», con el mismo plazo de 10 s que el resultado. Sin categoría agotada, el motivo es `MensajesAtencion.DeMotivo("PERSON_INACTIVE")` si todas las retenidas tienen ese motivo, y si no `MensajesAtencion.SinHabilitacion`, «No tenés servicios habilitados. Consultá al responsable del comedor». «Listo» y el plazo llaman a `Limpiar()`, que vuelve a reposo sin el nombre. La sesión queda sin intención y vence sola.

Descartada: volver directo a reposo con un aviso breve. La persona puede no leerlo, y David pidió que se informe «con claridad».

### D5. La confirmación sigue evaluando bajo el bloqueo

La oferta se calcula fuera del bloqueo de la confirmación y puede quedar vieja. `UnidadConfirmacion` sigue decidiendo bajo el bloqueo con D1. Un rechazo entonces conserva su mensaje, y `CatalogoCambiado` vuelve a calcular la oferta en lugar de releer el catálogo entero.

Descartada: confiar en la oferta y no reevaluar. Rompería RB06: dos atenciones sobre la misma persona podrían gastar el mismo cupo.

### D6. Cuatro pruebas existentes cambian (tres con el gate y una aprobada durante el apply)

- `MaquinaAtencionTests.Cada_rechazo_tiene_su_mensaje`: para `LOCAL_LIMIT_REACHED`, `SERVICE_NOT_ENABLED`, `RULE_DENIED` y `PERSON_INACTIVE` la persona se arma con una regla de ALMUERZO de límite 1 y la condición del rechazo se aplica en `DespuesDeIdentificar`. Esa condición es el uso de cupo (`tbComedorUsosCupo`), borrar la regla, dejar su límite en 0 y dar de baja a la persona. Las afirmaciones no cambian.
- `ComprobanteEnPantallaTests`, «Un rechazo LOCAL_LIMIT_REACHED no muestra la impresora»: el uso de cupo se inserta después de identificarse. Las afirmaciones no cambian.
- `FlujoPrincipalTests`: los pasos 4 y 5, como dice el punto 1 del gate.
- `MaquinaAtencionTests.Treinta_segundos_sin_actividad_vuelven_a_reposo`, aprobada por David el 04/11 a las 21:08 durante el apply: el operador `9001` recibe dentro de la prueba una regla de ALMUERZO de límite 1 (`DarleAlmuerzoA`), sin cambiar sus afirmaciones.

Las pruebas nuevas van en archivos nuevos: `Domain.Tests/EvaluadorDisponibilidadTests.cs`, `Infrastructure.Sqlite.Tests/ConsultaDisponibilidadTests.cs` y `Kiosk.Tests/OfertaPorPersonaTests.cs`. La de equivalencia (escenario «La oferta y la confirmación coinciden») arma las cinco personas en una base real y compara, para cada una, el motivo de la oferta con el de `UnidadConfirmacion.Confirmar` en el mismo instante del reloj falso.

Descartada: dejar las tres pruebas como están y ofrecer también lo no disponible para que pasen. Contradice el pedido.

### D7. Registro `OPTIONS_OFFERED`

`ConsultaDisponibilidad.Ofrecer` escribe con `RegistroBitacora` un registro `OPTIONS_OFFERED`, componente `Infrastructure.Sqlite.ConsultaDisponibilidad`, en su transacción corta. El contexto lleva `IdentitySessionId`, `PersonId`, la versión de configuración activa, `Offered` (lista de `MenuItemId` y `MenuVersion`), `Withheld` (lista de `MenuItemId`, `MenuVersion` y `ReasonCode`) y `Exhausted` (lista de `CategoryId`, `Name`, `Used` y `Limit`). Nunca el código.

Descartada: no registrar la oferta. El change B necesita saber qué se le mostró a cada persona para reconstruir la interacción, que es el pedido 4 de David del 04/11.

## Risks / Trade-offs

- **[Una lectura más por identificación]** → para nueve o quince opciones son unas decenas de consultas parametrizadas sobre una base local, dentro de una transacción corta. Si se notara al tocar «Ingresar», se cargan las reglas y los usos una vez por persona y no por opción, sin cambiar la evaluación.
- **[Oferta vieja]** → D5: la confirmación decide.
- **[Otra prueba existente que dependa de ver todo]** → se para y se pregunta (punto 1 del gate).
- **[Las franjas en el change C cambian la oferta otra vez]** → entran por D1, sin tocar la pantalla.
