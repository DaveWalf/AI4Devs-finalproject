# Tasks

Las pruebas existentes no se tocan, salvo las tres que aprobó David en el gate (D6): `MaquinaAtencionTests.Cada_rechazo_tiene_su_mensaje`, «Un rechazo LOCAL_LIMIT_REACHED no muestra la impresora» de `ComprobanteEnPantallaTests` y los pasos 4 y 5 de `FlujoPrincipalTests`. `git diff --diff-filter=M --stat <base> -- tests/` sólo puede mostrar esos tres archivos, con `<base>` la punta de `feature/presentar-atencion-tactil-dw` sobre la que se apila esta rama. Cada prueba nueva se ve fallar antes de implementar. Si otra prueba existente falla porque su opción ya no se ofrece, se para y se le pregunta a David.

## 1. Una sola evaluación

- [ ] 1.1 Agregar `Domain.Tests/EvaluadorDisponibilidadTests.cs`: autoriza con cupo libre; cada motivo de RB02 en su orden (`PERSON_INACTIVE` antes que `RULE_DENIED`, regla cero antes que el cupo) y `LOCAL_LIMIT_REACHED` con el uso igual al límite. Crear `EvaluadorDisponibilidad` con `throw new NotImplementedException()` y ver fallar con `dotnet test tests/Intelektron.INComedor.Domain.Tests --filter "FullyQualifiedName~EvaluadorDisponibilidad"`; pegar la salida
- [ ] 1.2 Implementar `EvaluadorDisponibilidad` con la composición de `UnidadConfirmacion.cs:80-88` (D1) y hacer que `UnidadConfirmacion` lo llame. Verificar con el filtro de 1.1 en verde y con `dotnet test tests/Intelektron.INComedor.Domain.Tests` y `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completos en verde, sin cambios en sus pruebas
- [ ] 1.3 Agregar `Infrastructure.Sqlite.Tests/ConsultaDisponibilidadTests.cs` con los escenarios «La oferta y la confirmación coinciden» y «Calcular la oferta no registra hechos» de `habilitacion-local`, y los de «La oferta queda en la bitácora» de `atencion-puesto`. Ver fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~ConsultaDisponibilidad"` contra `ConsultaDisponibilidad.Ofrecer` sin implementar; pegar la salida
- [ ] 1.4 Mover las cinco lecturas de `UnidadConfirmacion` a `LecturaDecision` sin cambiarlas, e implementar `ConsultaDisponibilidad.Ofrecer` y el registro `OPTIONS_OFFERED` (D2 y D7). Verificar con el filtro de 1.3 en verde y `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` completo en verde

## 2. La atención

- [ ] 2.1 Cambiar las dos pruebas de `Kiosk.Tests` de D6, con la aprobación del gate, y comprobar que pasan antes de tocar el código del puesto (la condición después de identificarse también rechaza hoy) con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~Cada_rechazo_tiene_su_mensaje|FullyQualifiedName~ComprobanteEnPantalla"`; pegar la salida
- [ ] 2.2 Agregar `Kiosk.Tests/OfertaPorPersonaTests.cs`, con máquina y bUnit, para los escenarios de «Sólo las opciones disponibles para la persona» y de «Aviso claro cuando no queda ninguna opción», más `Elegir` de una opción no ofrecida, que no hace nada, y `CatalogoCambiado`, que vuelve a calcular la oferta. Ver fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~OfertaPorPersona"`; pegar la salida
- [ ] 2.3 Implementar en `MaquinaAtencion` la oferta, `UsoDelDia` y el estado `SinOpciones` (D3 a D5); en `PantallaAtencion.razor`, `p#uso-del-dia` y la pantalla sin opciones; en `MensajesAtencion`, los textos nuevos; en `puesto.css`, el estilo de la línea, legible sobre el fondo y sin achicar el alto de las opciones. Verificar con el filtro de 2.2 en verde y `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` completo en verde

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
