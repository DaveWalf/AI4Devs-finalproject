# Tasks

Las pruebas existentes cambian sólo como dice el punto 8 del gate, con la aprobación de David. En cada una, el toque del engranaje en mantenimiento pasa al ayudante de D8 y, donde se mide cómo se entró, `MAINTENANCE_MODE` pasa a `DAILY_KEY`; nada más. Al cerrar cada grupo, `git diff --diff-filter=M -- tests/` contra la punta del 2 sobre la que se apila este change se revisa entero. Si una prueba existente falla por otra razón, se para y se le pregunta a David. Cada prueba nueva se ve fallar antes de implementar lo que la hace pasar, y su `DisplayName` empieza con la capability y el nombre del escenario. Las de `Kiosk.Tests` y la E2E corren con `INCOMEDOR_SQLSERVER` apuntando a la instancia del equipo, como pide `AGENTS.md`.

## 1. Saber si un valor es un código, y el actor al guardar

- [x] 1.1 Agregar a `Infrastructure.Sqlite.Tests` el archivo nuevo `CodigoConocidoTests.cs`, sobre el fixture sintético:
  - un código vigente y uno vencido son conocidos, y `4711` no;
  - ninguno de los tres escribe en la bitácora ni abre una sesión.
  
  Sumar a `AdministracionActorTests.cs`, también nuevo, que guardar personas con un actor registra su `PersonId` sin `ActorReason`, y sin actor, el nulo con `NO_AUTHENTICATION` de hoy. Verlas fallar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~CodigoConocido|FullyQualifiedName~AdministracionActor"`.
  - Contra esqueletos (`EsCodigoConocido` con `NotImplementedException` y el `actor` opcional sin usar), a las 14:05: `total: 5`, `error: 4`. Las dos de `CodigoConocidoTests` con `NotImplementedException`; en `AdministracionActorTests`, la de personas con actor (su `ActorPersonId` era nulo) y la del rechazo y las opciones (`Assert.All() Failure: 2 out of 2 items`).
  - Pasó «Lo guardado sin operador», que es la conducta de hoy y queda como guarda de que el camino sin actor no cambia.
  - Suman un borde (el valor se normaliza como al identificar: espacios del borde sí, ceros iniciales no, vacío no) y que el rechazo y las opciones también llevan el actor.
- [x] 1.2 Implementar `IdentificacionPorCodigo.EsCodigoConocido` (D5) y el `actor` opcional de `GuardarPersonas` y `GuardarOpciones` (D7). Verificar con el filtro de 1.1 que pasan, y con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests` que no hay ninguna otra falla ni prueba cambiada.
  - `Infrastructure.Sqlite.Tests` entero a las 14:06: `total: 211`, `error: 0` (las 206 de la base más las cinco nuevas). `git diff --diff-filter=M --name-only cf1b137 -- tests/`: vacío.
  - El actor se pone en el registro ya armado (`ConActor`): sin actor, el registro de cada guardado queda byte a byte como antes.

## 2. El teclado del diálogo

- [x] 2.1 Agregar a `Kiosk.Tests` el archivo nuevo `TecladoClaveTests.cs`, con estos escenarios de `acceso-puesto`, y verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~TecladoClave"`:
  - «El teclado en pantalla»: el orden de las teclas, los puntos y que «Borrar» y «Limpiar» hacen lo suyo;
  - «El teclado físico también tipea», con `inputmode="none"`;
  - «Clave vacía o con letras», con «ENTER»;
  - el borde de los 12 dígitos.
  - Salida del 10/11 a las 14:08, contra el código de `49cb93c`: `total: 5`, `error: 4` (el orden de las teclas, `Collections differ`, `Actual: []`; el teclado que abre Configuración y el borde de 12, sin `#tecla-4` ni `#tecla-1`; el teclado físico, `Expected: "none"`, `Actual: "numeric"`).
  - «Clave vacía o con letras (con «ENTER»)» pasó, porque `#aceptar-clave` ya existía; se le sumó que la tecla dice «ENTER», como nombra el escenario, y falló: `Expected: "ENTER"`, `Actual: "Aceptar"`.
  - Con D3 implementado, a las 14:09: `total: 5`, `error: 0`.
- [x] 2.2 Implementar el teclado y el campo de D3 en `DialogoClaveDelDia.razor` y su CSS en `wwwroot/css/puesto.css`. Verificar con el filtro de 2.1 que pasan, y con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~ClaveDelDia|FullyQualifiedName~AccesoPuesto"` qué pruebas existentes fallan y por qué, antes de tocar ninguna.
  - **Parado el 10/11 a las 14:09: una prueba existente falla por otra razón que D8.** D3 está implementado y el filtro de 2.1 pasa. El filtro de esta tarea da `total: 27`, `error: 1`: `ClaveDelDiaTests.Cualquier_numero_en_desarrollo` (`ClaveDelDiaTests.cs:43`), `Expected: Tuple ("password", "numeric", "off")`, `Actual: Tuple ("password", "none", "off")`.
  - Esa prueba entra en operativo, que ya pedía la clave, y no mide cómo se entró: falla porque D3 cambia `inputmode="numeric"` por `inputmode="none"`. Los dos cambios de D8 no la cubren, así que no se tocó, y el change se detiene acá por el punto 8 del gate.
  - `Kiosk.Tests` entero a las 14:11: `total: 369`, `error: 1`, la misma; `git diff --diff-filter=M --name-only cf1b137 -- tests/`, vacío.
  - Para seguir, David decide si esa línea pasa a esperar `"none"`. Visto sin correr, para la misma decisión: `AccesoPuestoTests.Mantenimiento_entra_directo` pasaría con D8, pero su nombre y su `DisplayName` seguirían diciendo «entra directo … sin clave», que es el escenario que el change retira.
  - **Decisión de David, 10/11 a las 14:24** (AskUserQuestion, sin piloto), escrita en `proposal.md`: «Aprobar las dos». La línea del `inputmode` de `ClaveDelDiaTests.cs:43` espera `"none"` y nada más; `Mantenimiento_entra_directo` cambia de nombre en 3.3, con su cambio de D8. Con esa línea, a las 14:25, el filtro de esta tarea más `TecladoClave`: `total: 32`, `error: 0`.

## 3. La clave en los dos modos y el operador

- [ ] 3.1 Agregar a `Kiosk.Tests` los ayudantes de D8 (`EntradaConfiguracion.cs`) y el archivo nuevo `AccesoOperadorTests.cs`, con estos escenarios, y verlas fallar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~AccesoOperador"`:
  - de `acceso-puesto`: «Mantenimiento también pide la clave», «Cualquier número en desarrollo» con el aviso, «Entrada de un operador», «Un operador entra con su código», «Un permiso de operador vencido», «El código de una persona sin OPERATOR», «Un operador no cierra el puesto» y «Lo guardado por un operador»;
  - de `configuracion-puesto`, «Las tres pestañas de un operador»;
  - de `consulta-consumos`, «Un operador ve los consumos».
- [ ] 3.2 Implementar D1, D4, D5, D6 y D7 en `PantallaAtencion.razor` y `PanelConfiguracion.razor`, con `MensajesAtencion.ClaveSinValidar` y `MensajesAtencion.CodigoNoEsClave`. Verificar con el filtro de 3.1 que pasan.
- [ ] 3.3 Correr `dotnet test tests/Intelektron.INComedor.Kiosk.Tests` y listar las pruebas existentes que fallan. Para cada una que falla sólo porque entra por el engranaje en mantenimiento, aplicar el cambio de D8 y nada más. Si alguna falla por otra razón, parar y preguntarle a David. Verificar que la suite de `Kiosk.Tests` no tiene fallas y pegar el `git diff --stat -- tests/`.

## 4. El puesto compilado

- [ ] 4.1 Agregar a `E2E.Tests` el ayudante `EntradaConfiguracionE2E.cs` y el archivo nuevo `AccesoOperadorE2ETests.cs`. En 1024 × 768 debe pasar esto:
  - el teclado del diálogo mide al menos 56 px por tecla y entra en la ventana;
  - `9001` abre Configuración con tres pestañas;
  - la clave del día abre las nueve, con el aviso de clave sin validar.
  
  Verla fallar contra el EXE publicado del 2, con `dotnet test tests/Intelektron.INComedor.E2E.Tests --filter "FullyQualifiedName~AccesoOperadorE2E"`.
- [ ] 4.2 Publicar con `pwsh .github/scripts/publicar-puesto.ps1 -Version 0.0.0-local`, y con `INCOMEDOR_E2E_KIOSK_EXE` apuntando al EXE publicado:
  - correr la suite de `E2E.Tests`;
  - aplicar D8 a las E2E existentes que fallen sólo por la clave en mantenimiento;
  - parar y preguntar si alguna falla por otra razón;
  - verificar que la suite pasa.

## 5. Documentos y evidencia

- [ ] 5.1 Sumar a `AGENTS.md` y a `openspec/config.yaml` el alcance de D9, al `readme.md` la clave en el recorrido y `9001` en Configuración, y a `docs/aplicacion-del-curso.md` la evidencia del change. Verificar con `openspec validate --all --strict` que no hay errores, y con el chequeo de enlaces relativos de D8 de `mostrar-aplicacion-del-curso`, con su control negativo, que hay 0 rotos.
- [ ] 5.2 Desde la raíz del repo, correr y pegar las salidas:
  - `dotnet build INComedor.sln -warnaserror`, con 0 advertencias;
  - `dotnet test INComedor.sln`, con 0 fallas;
  - `openspec validate --all --strict`, sin errores;
  - el `git diff --diff-filter=M -- tests/` del encabezado, revisado entero.

## Workflow follow-up

- La revisión de David (el gate de `proposal.md`) va antes del apply, y lo que decida queda escrito en `proposal.md`.
- Después de su prueba en el ejecutable, con los tres cambios juntos, `/openspec-verify-change` y el archivado en el mismo PR, que apunta a `feature/confirmar-eleccion-en-cartel-dw` y se integra después de ella. El push y el merge son de David.
