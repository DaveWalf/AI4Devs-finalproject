# Proposal

## Why

El 04/11, al probar el ejecutable en su monitor táctil, David identificó a `0001`, eligió un almuerzo, confirmó y recibió «Ya registraste este servicio. Consultá al responsable del comedor», sin comprobante: lo tomó por una falla de la impresora. No lo era. Hoy el puesto muestra el catálogo entero a todos y recién al confirmar evalúa la habilitación y el cupo, así que una persona sin cupo elige, confirma y recién entonces se entera.

David pidió: «Debe informarme con claridad si la persona no tiene cupo. Además, sólo debe presentar como disponibles aquellas opciones que la persona sí tenga habilitado. Conservá esto como una regla general, porque afectará también luego a la disponibilidad de las opciones cuando implementemos eventuales turnos». Ante la pregunta de cómo mostrar lo no disponible, eligió «sólo las disponibles, con aviso».

Es el primero de cuatro changes (A a D) que David decidió el 04/11 llevar a la final, en ese orden y con corte el 11/11 a las 12:00: lo que para entonces no esté archivado y en verde queda para después del Typeform.

Origen: ticket TK02.1 (interfaz de la atención) e historias HU01.1 y HU01.3 (habilitación local, RB02). La regla general y la decisión de presentación del 04/11/2026 están en la base de conocimiento del proyecto, fuera del repo.

## What Changes

- **Oferta por persona.** Al identificarse, el puesto evalúa cada opción del snapshot activo con la misma evaluación de la confirmación (persona activa, credencial, regla aplicable, regla cero, prioridad y cupo) y ofrece sólo las disponibles, en el orden del catálogo. La distribución por cantidad se aplica a las ofrecidas.
- **Aviso de lo ya usado.** Si alguna categoría de cupo está agotada hoy, la selección dice «Hoy ya usaste: almuerzo 1 de 1».
- **Sin ninguna disponible.** Una pantalla «No tenés opciones disponibles ahora», con lo ya usado o con el motivo, sin opciones ni «Confirmar». «Listo» o 10 s vuelven a reposo, sin intención.
- **Una sola evaluación.** Un evaluador de disponibilidad del dominio compone habilitación y cupo, y lo usan la oferta y la confirmación. Las franjas horarias (change C) y los turnos futuros se suman ahí, no a la pantalla.
- **La oferta queda en la bitácora.** Lo ofrecido y lo retenido, con su motivo, sin el código.
- **BREAKING** para tres pruebas existentes, que provocan un rechazo eligiendo una opción que con este change ya no se ofrece. Cambian sólo con la aprobación de David en este gate.

## Non-goals

- **Franjas horarias y edición de opciones, personas y cupos**: change C.
- **Bitácoras externas**: change B. Este change registra la oferta en `tbComedorBitacora`, que B va a llevar a los archivos.
- **La pantalla de demostración anterior** (`-- --demostracion`): sigue mostrando todo y rechazando al confirmar, como pide `modo-demostracion`.
- **Cambiar los mensajes de rechazo al confirmar**: un rechazo que llega igual, porque algo cambió entre la oferta y la confirmación, conserva su mensaje de `MensajesAtencion.DeMotivo`.
- **Mostrar el cupo que queda** («te queda 1 refrigerio de 2»): la línea nombra sólo lo agotado, que es lo que explica una opción ausente.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `atencion-puesto`: «Catálogo y selección sin consumo hasta confirmar» pasa de mostrar las opciones del snapshot activo a mostrar las que el puesto le ofrece a esa persona. Se suman «Sólo las opciones disponibles para la persona», «Aviso claro cuando no queda ninguna opción» y «La oferta queda en la bitácora».
- `habilitacion-local`: se suma «Una sola evaluación ofrece y confirma».

## Impact

- **Código**: `Domain` (`EvaluadorDisponibilidad`, nuevo), `Infrastructure.Sqlite` (`ConsultaDisponibilidad`, nueva, y las lecturas de la decisión, que salen de `UnidadConfirmacion` a una clase compartida sin cambiar), `Kiosk` (`MaquinaAtencion`, `PantallaAtencion.razor`, `MensajesAtencion`, `puesto.css`).
- **Pruebas**: nuevas en `Domain.Tests`, `Infrastructure.Sqlite.Tests` y `Kiosk.Tests`; tres existentes cambian, sujetas al gate.
- **Documentos**: `readme.md` (recorrido y tabla de códigos), `docs/experiencia.md` (estados y mensajes), capturas nuevas `docs/capturas/11` y `12`, y la fila del change en `docs/aplicacion-del-curso.md`.
- Sin migración, sin dependencias nuevas y sin cambios en la CI.

## Tallas

| Quién | Talla | Horas | Con el colchón del 30 % |
|---|---|---|---|
| David | La das en el gate | | |
| La IA (Claude Code, Opus 5.5), 04/11/2026 | **S** | 2 a 3 h | 2,6 a 3,9 h de capacidad |

**Por qué la IA da S.** La evaluación ya existe y se reutiliza; lo nuevo es leerla para cada opción al identificarse, dos estados de pantalla y sus textos. El tiempo se va en las pruebas de equivalencia entre la oferta y la confirmación, en el E2E y en dos capturas por CDP.

## Para el gate de David

1. **Tres pruebas existentes cambian** (D6). Tu aprobación cubre estas tres y ninguna más:
   1. `MaquinaAtencionTests.Cada_rechazo_tiene_su_mensaje` arma hoy cuatro de sus seis rechazos (`LOCAL_LIMIT_REACHED`, `SERVICE_NOT_ENABLED`, `RULE_DENIED` y `PERSON_INACTIVE`) antes de identificarse. Con este change esa persona ya no vería el almuerzo. Pasa a armarlos después de identificarse, en `DespuesDeIdentificar`, como ya hace con los dos de credencial. Lo que afirma no cambia: seis mensajes distintos y ningún consumo.
   2. `ComprobanteEnPantallaTests`, «Un rechazo LOCAL_LIMIT_REACHED no muestra la impresora»: el cupo se gasta después de identificarse, por el mismo motivo. Lo que afirma no cambia.
   3. `E2E.Tests/FlujoPrincipalTests.cs`: el paso 4 deja de confirmar un segundo almuerzo y afirmar el mensaje de cupo, y pasa a afirmar «Hoy ya usaste: almuerzo 1 de 1», que no se ofrece ningún almuerzo y que sigue habiendo un solo consumo. El paso 5 pasa de «2 pendientes» a «1 pendiente», porque ya no hay un rechazo que sincronizar. Los pasos 6 y 7 no cambian.

   Si otra prueba existente falla porque su opción ya no se ofrece, se para y se te pregunta.
2. **La línea nombra sólo lo agotado** (D3). Tomada: «Hoy ya usaste: almuerzo 1 de 1 · refrigerio 2 de 2», con el nombre de la categoría en minúsculas. Descartada: listar todo lo usado, que suma texto sin explicar nada que falte.
3. **Texto sin ninguna opción** (D4). Tomada: «No tenés opciones disponibles ahora» y, debajo, la línea de lo usado. Sin categoría agotada, el mensaje actual de `PERSON_INACTIVE` si la persona está inactiva, y si no «No tenés servicios habilitados. Consultá al responsable del comedor».
4. **La confirmación vuelve a evaluar** (D5). Una opción ofrecida que se agota antes de confirmar se rechaza con su mensaje de siempre, dentro de RB06.
5. **La oferta en la bitácora** (D7). Cada identificación deja un registro `OPTIONS_OFFERED` en `tbComedorBitacora`, la base de lo que el change B va a escribir en los archivos externos.
6. **Capturas.** Tomada: dos nuevas, `11` (la línea de lo usado, con cinco refrigerios) y `12` (sin ninguna opción). Las 06 a 10 no cambian: las rehace `mover-engranaje-al-pie`, que se aplica antes.
7. **Rama.** `feature/ofrecer-solo-opciones-disponibles-dw`, apilada sobre `feature/presentar-atencion-tactil-dw`. Su PR apunta a esa rama hasta que la integres, y después a `final-project-dw`.
8. **Tallas.** La de la IA es S; la tuya la das acá.
