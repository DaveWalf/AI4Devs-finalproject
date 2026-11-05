# Tasks

Las pruebas existentes no se tocan, salvo las tres que aprobó David en el gate (D6): `MaquinaAtencionTests.Cada_rechazo_tiene_su_mensaje`, «Un rechazo LOCAL_LIMIT_REACHED no muestra la impresora» de `ComprobanteEnPantallaTests` y los pasos 4 y 5 de `FlujoPrincipalTests`. `git diff --diff-filter=M --stat <base> -- tests/` sólo puede mostrar esos tres archivos, con `<base>` la punta de `feature/presentar-atencion-tactil-dw` sobre la que se apila esta rama. Cada prueba nueva se ve fallar antes de implementar. Si otra prueba existente falla porque su opción ya no se ofrece, se para y se le pregunta a David.

**Cuarta prueba, aprobada por David el 04/11 a las 21:08.** Al aplicar 2.3 falló, determinista, `MaquinaAtencionTests.Treinta_segundos_sin_actividad_vuelven_a_reposo`: identifica al operador `9001`, que no tiene ninguna regla, y su ayudante exige la selección; con la oferta por persona ve «No tenés opciones disponibles ahora». Se paró y se le preguntó. David aprobó darle a `9001` una regla de ALMUERZO de límite 1 dentro de esa prueba (`DarleAlmuerzoA`), sin cambiar ninguna afirmación. Son entonces cuatro las pruebas existentes cambiadas, y `git diff --diff-filter=M --stat <base> -- tests/` muestra `MaquinaAtencionTests.cs`, `ComprobanteEnPantallaTests.cs` y `FlujoPrincipalTests.cs`, con esa cuarta dentro del primero.

## 1. Una sola evaluación

- [x] 1.1 Agregar `Domain.Tests/EvaluadorDisponibilidadTests.cs`: autoriza con cupo libre; cada motivo de RB02 en su orden (`PERSON_INACTIVE` antes que `RULE_DENIED`, regla cero antes que el cupo) y `LOCAL_LIMIT_REACHED` con el uso igual al límite. Crear `EvaluadorDisponibilidad` con `throw new NotImplementedException()` y ver fallar con `dotnet test tests/Intelektron.INComedor.Domain.Tests --filter "FullyQualifiedName~EvaluadorDisponibilidad"`; pegar la salida
  `EvaluadorDisponibilidadTests.cs` con siete casos (autoriza con cupo libre; uso igual al límite, con la regla aplicable a la vista; uso menor que el límite; persona inactiva antes que la regla cero; regla cero antes que el cupo; sin reglas; credencial revocada antes que reglas y cupo) y `EvaluadorDisponibilidad` con `throw new NotImplementedException()`. Rojo, 04/11 20:56:
  ```
  dotnet test tests/Intelektron.INComedor.Domain.Tests --filter "FullyQualifiedName~EvaluadorDisponibilidad"
  con errores RB03 · Uso del día igual al límite: LOCAL_LIMIT_REACHED, y la regla aplicable queda a la vista …
  Xunit.MicrosoftTestingPlatform.XunitException: System.NotImplementedException : The method or operation is not implemented.
  (los siete con la misma excepción)    total: 7 · error: 7 · correcto: 0
  ```
- [x] 1.2 Implementar `EvaluadorDisponibilidad` con la composición de `UnidadConfirmacion.cs:80-88` (D1) y hacer que `UnidadConfirmacion` lo llame. Verificar con el filtro de 1.1 en verde y con `dotnet test tests/Intelektron.INComedor.Domain.Tests` y `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completos en verde, sin cambios en sus pruebas
  `EvaluadorDisponibilidad.Evaluar` compone `EvaluadorHabilitacion` y, sin motivo, `EvaluadorCupo`, y devuelve `ResultadoDisponibilidad(DecisionCupo, Reglas)`; `UnidadConfirmacion.Confirmar` lo llama en lugar de la composición propia. Una diferencia de forma: los usos del día se leen siempre, no sólo cuando la habilitación no deniega; la decisión es la misma.
  ```
  Domain.Tests --filter EvaluadorDisponibilidad   correcto [+7/x0/?0]    total: 7 · error: 0
  Domain.Tests completo                           correcto [+75/x0/?0]    total: 75 · error: 0
  Infrastructure.Sqlite.Tests completo            correcto [+117/x0/?0]   total: 117 · error: 0   (sin cambios en sus pruebas)
  ```
- [x] 1.3 Agregar `Infrastructure.Sqlite.Tests/ConsultaDisponibilidadTests.cs` con los escenarios «La oferta y la confirmación coinciden» y «Calcular la oferta no registra hechos» de `habilitacion-local`, y los de «La oferta queda en la bitácora» de `atencion-puesto`. Ver fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~ConsultaDisponibilidad"` contra `ConsultaDisponibilidad.Ofrecer` sin implementar; pegar la salida
  `ConsultaDisponibilidadTests.cs` con cuatro casos sobre SQLite real: la oferta y la confirmación coinciden (cinco personas, una por motivo y una con cupo libre, la oferta y `Unidad.Confirmar` con el mismo reloj fijo); calcular la oferta no registra hechos (sólo suma el registro de bitácora); oferta con una categoría agotada (`Habilitada` con un almuerzo consumido: seis ofrecidas, nueve retenidas con `LOCAL_LIMIT_REACHED`, ALMUERZO 1 de 1, y el registro `OPTIONS_OFFERED` sin el código `000123`); oferta vacía (`SinDerecho`: quince retenidas con `SERVICE_NOT_ENABLED`). `ConsultaDisponibilidad.Ofrecer` creado sin implementar. Rojo, 04/11 20:58:
  ```
  dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~ConsultaDisponibilidad"
  con errores habilitacion-local · La oferta y la confirmación coinciden: …
  con errores habilitacion-local · Calcular la oferta no registra hechos: …
  con errores atencion-puesto · Oferta con una categoría agotada: …
  con errores atencion-puesto · Oferta vacía: …
  Xunit.MicrosoftTestingPlatform.XunitException: System.NotImplementedException : The method or operation is not implemented.   (los cuatro)
  total: 4 · error: 4 · correcto: 0
  ```
- [x] 1.4 Mover las cinco lecturas de `UnidadConfirmacion` a `LecturaDecision` sin cambiarlas, e implementar `ConsultaDisponibilidad.Ofrecer` y el registro `OPTIONS_OFFERED` (D2 y D7). Verificar con el filtro de 1.3 en verde y `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completo en verde
  Las cinco lecturas de `UnidadConfirmacion` (`LeerOpcion`, `LeerPersonaActiva`, `LeerCredencialDeSesion`, `LeerReglasCandidatas`, `LeerUsos`) pasaron con su texto a `LecturaDecision` (`internal static`, con `Texto`, `CodigoDeMotivo` y los registros de la opción); de las 127 líneas borradas de `UnidadConfirmacion.cs`, 120 están tal cual en `LecturaDecision.cs` y las 7 restantes son la composición que reemplazó 1.2. `ConsultaCatalogo` suma una sobrecarga interna con la conexión del llamador, para el mismo orden. `ConsultaDisponibilidad.Ofrecer` evalúa cada opción con `EvaluadorDisponibilidad`, saca las categorías agotadas de las reglas aplicables (límite mayor que cero y uso que lo alcanza) y escribe `OPTIONS_OFFERED` (`ConfigurationVersion`, `Offered`, `Withheld`, `Exhausted`; nunca el código) en su transacción corta.
  ```
  dotnet build src/Intelektron.INComedor.Infrastructure.Sqlite -warnaserror   Compilación correcta · 0 Advertencia(s) · 0 Errores
  Infrastructure.Sqlite.Tests --filter ConsultaDisponibilidad   correcto [+4/x0/?0]     total: 4 · error: 0
  Infrastructure.Sqlite.Tests completo                          correcto [+121/x0/?0]   total: 121 · error: 0   (117 de antes y 4 nuevas)
  ```

## 2. La atención

- [x] 2.1 Cambiar las dos pruebas de `Kiosk.Tests` de D6, con la aprobación del gate, y comprobar que pasan antes de tocar el código del puesto (la condición después de identificarse también rechaza hoy) con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~Cada_rechazo_tiene_su_mensaje|FullyQualifiedName~ComprobanteEnPantalla"`; pegar la salida
  Cambiadas, con la aprobación del gate (D6) y nada más: `MaquinaAtencionTests.Cada_rechazo_tiene_su_mensaje` (los seis motivos arman la persona con una regla de ALMUERZO de límite 1 en `PrepararRechazo`; la condición se aplica en `DespuesDeIdentificar`: uso del cupo de `0001` en `tbComedorUsosCupo`, regla borrada, límite 0, baja, credencial revocada o vencida; se quitó la confirmación previa de `LOCAL_LIMIT_REACHED`; las afirmaciones no cambian: seis mensajes distintos y ningún consumo) y `ComprobanteEnPantallaTests.Un_rechazo_no_muestra_la_impresora` (identifica, elige, el uso del cupo de ALMUERZO se inserta, y recién entonces «Confirmar»; mismas afirmaciones; se sumó un `Elegir` auxiliar y `GastarElCupoDeAlmuerzoDeLaPersona`). Pasan con el código del puesto todavía sin tocar, porque la condición después de identificarse también rechaza hoy:
  ```
  dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~Cada_rechazo_tiene_su_mensaje|FullyQualifiedName~ComprobanteEnPantalla"
  Intelektron.INComedor.Kiosk.Tests.dll (net11.0|x64) correcto [+8/x0/?0] (4s 132ms)    total: 8 · error: 0 · correcto: 8
  ```
- [x] 2.2 Agregar `Kiosk.Tests/OfertaPorPersonaTests.cs`, con máquina y bUnit, para los escenarios de «Sólo las opciones disponibles para la persona» y de «Aviso claro cuando no queda ninguna opción», más `Elegir` de una opción no ofrecida, que no hace nada, y `CatalogoCambiado`, que vuelve a calcular la oferta. Ver fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~OfertaPorPersona"`; pegar la salida
  `Kiosk.Tests/OfertaPorPersonaTests.cs` con doce casos, con máquina y bUnit sobre el catálogo de nueve: el almuerzo ya consumido (cinco refrigerios, una columna, sin paginación, «Hoy ya usaste: almuerzo 1 de 1», también en la pantalla), nada usado (nueve opciones), la regla cero que no es cupo usado, la opción que se agota entre la oferta y la confirmación, `Elegir` de una opción no ofrecida, `CatalogoCambiado` que recalcula la oferta, todo el cupo usado (sin opciones, «almuerzo 1 de 1 · refrigerio 2 de 2», «Listo» y sin intención nueva; y su pantalla), persona sin habilitación, persona inactiva y los diez segundos. Para que compilen se agregó antes la superficie mínima (`EstadoAtencion.SinOpciones`, `UsoDelDia` y `MotivoSinOpciones` devolviendo `null`, y los dos textos nuevos). Rojo, 04/11 21:03, con el código del puesto sin la oferta:
  ```
  dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~OfertaPorPersona"
  Intelektron.INComedor.Kiosk.Tests.dll (net11.0|x64) error con 10 errores [+2/x10/?0]
  con errores … Todo el cupo del día usado / La opción cambió y se agotó / Persona inactiva / Diez segundos sin tocar la pantalla sin opciones /
                El almuerzo del día ya consumido / Elegir una opción que no se ofrece / Persona sin ninguna habilitación /
                Una regla cero no es cupo usado / La pantalla sin opciones / La pantalla del almuerzo ya consumido
  total: 12 · error: 10 · correcto: 2
  ```
  Las dos que pasan ya eran verdad antes de implementar y quedan como guardas: «Nada usado en el día» (el catálogo entero) y «La opción se agota entre la oferta y la confirmación» (la confirmación decide).
- [x] 2.3 Implementar en `MaquinaAtencion` la oferta, `UsoDelDia` y el estado `SinOpciones` (D3 a D5); en `PantallaAtencion.razor`, `p#uso-del-dia` y la pantalla sin opciones; en `MensajesAtencion`, los textos nuevos; en `puesto.css`, el estilo de la línea, legible sobre el fondo y sin achicar el alto de las opciones. Verificar con el filtro de 2.2 en verde y `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` completo en verde
  `MaquinaAtencion` calcula la oferta al identificarse con `ConsultaDisponibilidad.Ofrecer` (nuevo `ServicioAtencion.Disponibilidad`): `_catalogo` pasa a ser lo ofrecido, con `_retenidas` y `_agotadas`; el estado nuevo `SinOpciones` (con «Listo» o 10 s a reposo, sin intención), `UsoDelDia` («Hoy ya usaste: almuerzo 1 de 1 · refrigerio 2 de 2», categoría en minúsculas es-AR) y `MotivoSinOpciones` (el de `PERSON_INACTIVE` si todas las retenidas lo son; si no, «No tenés servicios habilitados…»); `CatalogoCambiado` vuelve a calcular la oferta en lugar de releer el catálogo y, si queda vacía, pasa a `SinOpciones`. `PantallaAtencion.razor`: `p#uso-del-dia` debajo del saludo y la pantalla sin opciones (`p#mensaje`, `p#uso-del-dia` o `p#motivo-sin-opciones`, `button#listo`, sin opciones ni «Confirmar»); `puesto.css`: `.titulo .uso-del-dia` en su propia línea, color de aviso. Una ayuda más, aprobada por David: ver «Cuarta prueba» arriba.
  ```
  dotnet build src/Intelektron.INComedor.Kiosk -warnaserror          Compilación correcta · 0 Advertencia(s) · 0 Errores
  Kiosk.Tests --filter OfertaPorPersona                              correcto [+12/x0/?0]     total: 12 · error: 0
  Kiosk.Tests completo (1.ª corrida)                                 error con 1 errores [+162/x1/?0]   MaquinaAtencionTests.Treinta_segundos_sin_actividad_vuelven_a_reposo: Expected: Seleccion · Actual: SinOpciones (3 de 3 corridas)
  Kiosk.Tests completo (tras la cuarta prueba aprobada)              correcto [+163/x0/?0] (5s 836ms)   total: 163 · error: 0 · correcto: 163   (151 de antes y 12 nuevas)
  ```

## 3. E2E, evidencia y documentos

- [ ] 3.1 Cambiar los pasos 4 y 5 de `E2E.Tests/FlujoPrincipalTests.cs` como dice el punto 1 del gate, y verificar con `dotnet test tests/Intelektron.INComedor.E2E.Tests` en verde. Si falla con «1 pendiente» a los 45 s en otro paso, repetir el proyecto y anotar las dos corridas sin tocar esa prueba
- [ ] 3.2 Sacar `docs/capturas/11-uso-del-dia.jpg` (la línea de lo usado, con cinco refrigerios) y `docs/capturas/12-sin-opciones.jpg` con el procedimiento de la tarea 7.1 de `openspec/changes/archive/2026-11-03-presentar-atencion-tactil/tasks.md`, a 1024 × 768, y mirarlas: el texto se lee entero, nada queda cortado y no hay opciones ni «Confirmar» en la 12
- [ ] 3.3 Actualizar `readme.md` (el paso 2 del recorrido, la fila de `0001` en la tabla de códigos y las dos capturas nuevas con su texto alternativo) y `docs/experiencia.md` (los dos estados nuevos en «Estados y mensajes propuestos», y el cupo utilizado como rechazo de la confirmación). Verificar con `git grep -n "otra vez .0001. con «Almuerzo común»: «Ya registraste" -- readme.md` sin resultados
- [ ] 3.4 Sumar la fila del change a `docs/aplicacion-del-curso.md`. Verificar con el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso` (`enlaces rotos: 0`, con su control negativo)

## 4. Integración

- [ ] 4.1 Correr `openspec validate --all --strict`, `dotnet build INComedor.sln -warnaserror` y `dotnet test INComedor.sln` completo contra la instancia de `INCOMEDOR_SQLSERVER`, y pegar los totales por proyecto; verificar además `git diff --diff-filter=M --stat <base> -- tests/` contra el párrafo de arriba

## Workflow follow-up

- Verificar con `/openspec-verify-change` y archivar en la misma rama antes del PR.
- Redactar el borrador del PR en `C:\AI\INComedor\ai4devs\gestion\PR-ofrecer-solo-opciones-disponibles-dw.md`, con la forma de `PR-presentar-atencion-tactil-dw.md`.
