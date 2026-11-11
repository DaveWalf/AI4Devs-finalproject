## 1. La oferta

- [x] 1.1 La rama de `CatalogoCambiado` que termina sin opciones (`MaquinaAtencion.cs`, verificación de A del 04/11): con
  la opción elegida retirada por un cambio de versión y todo el cupo del día usado, confirmar deja la atención sin
  opciones, sin elegida y sin consumo. Verifica: la prueba nueva en verde, vista fallar antes con una afirmación
  imposible. Hecho: `CoberturaDeLaOfertaTests.El_catalogo_cambiado_sin_ninguna_opcion`, vista fallar con
  `Seleccion` en lugar de `SinOpciones` («Expected: Seleccion · Actual: SinOpciones»); revertida, en verde.
- [x] 1.2 El `OPTIONS_OFFERED` que se repite al cambiar el catálogo antes de confirmar (A): un segundo registro en la
  misma sesión, sin la opción retirada y con la categoría agotada. Verifica: ídem. Hecho: `CoberturaDeLaOfertaTests.La_oferta_recalculada_se_registra_otra_vez`, vista fallar con un solo
  registro («Expected: 1 · Actual: 2»); revertida, en verde.

## 2. La administración local

- [x] 2.1 El modo de la impresora con una versión de INWeb activa (C, 06/11): «Sin papel» se aplica mientras Opciones
  sigue bloqueada. Verifica: ídem. Hecho: `CoberturaDeLaAdministracionTests.Con_INWeb_el_modo_de_la_impresora_sigue_editable`, vista
  fallar con «Modo: Disponible» («Actual: Modo: Sin papel»); revertida, en verde.
- [x] 2.2 El replay con `ReasonCause` (C): una denegación fuera de horario repetida con el mismo `IntentId`, ya dentro de
  la franja, devuelve la decisión original con su causa. Verifica: ídem. Hecho: `ReplayConCausaTests.El_replay_conserva_la_causa` (denegada a las 10:00 locales, repetida a
  las 12:00, con la franja de 11:00 a 14:00), vista fallar con `Assert.Null` sobre la causa («Actual:
  OUTSIDE_SERVICE_HOURS»); revertida, en verde.
- [x] 2.3 La pantalla de la baja (C, cubierta sólo por composición): quitar a `0001`, que ya consumió, y tipear su código
  muestra el mensaje de `CREDENTIAL_REVOKED`; su consumo sigue y Personas la muestra de baja. Verifica: ídem. Hecho: `CoberturaDeLaAdministracionTests.La_baja_en_la_pantalla`, vista fallar con el mensaje de
  `CREDENTIAL_EXPIRED` («Actual: Esta credencial ya no está habilitada…»); revertida, en verde.
- [x] 2.4 La pantalla de la reconfirmación (C, cubierta sólo por composición): con «Almuerzo común» elegido, la
  administración lo lleva a 1.800,00 y «Confirmar» muestra el valor nuevo y pide reconfirmar. Verifica: ídem. Hecho: `CoberturaDeLaAdministracionTests.El_cambio_de_precio_en_la_pantalla`, vista fallar buscando
  1.500,00 en el cargo vigente; revertida, en verde.

## 3. El acceso

- [x] 3.1 El código de una persona al pasar a operativo (verificación del 10/11): `0001` en la clave de «Pasar a
  operativo» dice que un código no es la clave, el modo sigue en mantenimiento y la general tiene `DAILY_KEY_REJECTED`
  con `ACTIVATE_OPERATIVE` y `PERSON_CODE`, sin `0001`. Verifica: ídem. Hecho: `CoberturaDelAccesoTests.Un_codigo_no_pasa_a_operativo`, vista fallar con `INVALID_FORMAT`
  («Actual: ACTIVATE_OPERATIVE, PERSON_CODE»); revertida, en verde. Hallazgo, sin tocar el código: el texto de ese
  rechazo es «El código de un operador no cierra el puesto.», que no dice lo que pasa (proposal, «Non-goals»).
- [x] 3.2 Las guardas de D6 sobre el modo y la simulación con el acceso de un operador: sus botones no están en el DOM,
  así que los eventos llegan por los callbacks del panel, como en `ComboSimulacionTests` (la verificación mostró que el
  plan las daba por no probables sin serlo). Verifica: la prueba nueva en verde, vista fallar antes. Hecho:
  `CoberturaDelAccesoTests.Las_guardas_del_operador`, vista fallar con `Assert.NotEmpty` sobre el diálogo de la clave
  («Collection was empty»); revertida, en verde.

## 4. Cierre

- [x] 4.1 Sin código de producto ni pruebas existentes: `git diff --stat origin/final-project-dw -- src/` y
  `git diff --diff-filter=M --name-only origin/final-project-dw -- tests/` vacíos. Verifica: las dos salidas pegadas. Hecho: las dos salidas vacías el 11/11 a las 14:03 (sin líneas de `src/` y 0 pruebas modificadas).
- [x] 4.2 Correcciones de la verificación: las historias y los tickets de origen, los requisitos que cubre cada prueba y
  los non-goals completos en el proposal; las afirmaciones que faltaban en 1.1 (sin aviso, sin intención y con el
  `MessageCode` de `SESSION_CLOSED`), 1.2 (la retenida con `LOCAL_LIMIT_REACHED` y sin el código, fuera de los GUID) y 2.4
  (el aviso de catálogo cambiado, «Reconfirmar» y sin intención); la prueba de `Elegir` de una opción no ofrecida, vista
  fallar de verdad y sacada como hallazgo. Verifica: las ocho en verde. Hecho: Kiosk 7 y Sqlite 1 en verde.
- [x] 4.3 La evidencia: la fila de TDD de `docs/aplicacion-del-curso.md` suma este change. Verifica: el chequeo de
  enlaces con su control negativo, `enlaces rotos: 0`. Hecho: 0, con el control en 2.
- [x] 4.4 Verificación integral: `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror
  --no-incremental`, el puesto publicado con `publicar-puesto.ps1 -Version 0.0.0-local` y `dotnet test INComedor.sln -c
  Release` contra ese EXE, con los totales por proyecto pegados. Hecho: sobre el archivado, en el PR.
