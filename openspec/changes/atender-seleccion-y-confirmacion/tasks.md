# Tasks

Se aplica con `opus` · `medium`: es un plan diseñado con su verificador, pero cruza cuatro capas (dominio, SQLite, Razor y la ventana real) y tiene una migración. `sonnet` · `medium` puede quedarse en una capa. Los comandos corren desde la raíz del repo y su salida se pega en la tarea. Al cerrar cada grupo hay un commit por rutas, en español, con `git commit -F <archivo>`.

Las pruebas existentes no se tocan, salvo las dos de D1 que aprobó David en el gate. `git diff --stat -- tests/` sólo puede mostrar los archivos nuevos y esos dos. Si otra tarea necesitara cambiar una prueba existente, se para y se le pregunta a David. Los chequeos de enlaces relativos y del mapa son los de D8 en `openspec/changes/archive/2026-10-29-mostrar-aplicacion-del-curso/design.md`.

## 1. Migración `0002` y versión de esquema 2

- [x] 1.1 Crear `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/MigracionesTests.cs` con los escenarios nuevos del delta de `registro-consumo`. «Base en la versión 1» arma la base con el recurso `0001` del ensamblado y `user_version = 1`, y le inserta un consumo. «Unicidad de permiso por persona y rol» completa la lista. Verificar con `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~MigracionesTests"` que fallan antes de implementar, y pegar la salida
- [x] 1.2 Cambiar las dos pruebas existentes de D1, y nada más, con la aprobación de David en el gate: `AperturaBaseTests.Base_vacia_queda_en_version_1_con_las_20_tablas` (versión 2, `tbComedorPermisosOperadores` en `TablasEsperadas`, nombre y `DisplayName`) y la línea 27 y el `DisplayName` de `ServicioDemostracionTests.Primer_arranque_siembra_los_datos_sinteticos`. Verificar con `git diff -- tests/` que el diff toca sólo esas líneas, y pegarlo
- [x] 1.3 Crear `Migraciones/0002_permisos_operadores.sql` y llevar `BaseComedor` a la lista ordenada de migraciones con `UltimaVersionConocida = 2` (D1). Verificar con `dotnet test INComedor.sln` que pasan las nuevas, las dos cambiadas y las 100 restantes, y con `git diff --stat -- tests/` que sólo aparecen `MigracionesTests.cs` y las dos de 1.2

  Salida de 1.1 (antes de implementar): `dotnet test tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests --filter "FullyQualifiedName~MigracionesTests"`: `total: 4, error: 4, correcto: 0`. «Base en la versión 1»: `Expected: 2, Actual: 1`; «Unicidad de permiso»: `SqliteException : SQLite Error 1: 'no such table: tbComedorPermisosOperadores'`; columnas del mapa: `Actual: List<string> []`; rol fuera del `CHECK`: `Expected: 275, Actual: 1`. En la primera corrida esta última pasó porque «no such table» también es `SqliteException`; se la ajustó al código extendido 275 (`SQLITE_CONSTRAINT_CHECK`) para que fallara por el motivo correcto.

  Salida de 1.2: `git diff -- tests/` toca sólo las líneas aprobadas en el gate. En `AperturaBaseTests.cs`: `+ "tbComedorPermisosOperadores",` en `TablasEsperadas`; el `DisplayName` de «versión de esquema 1 … 20 tablas» a «versión de esquema 2 … 21 tablas»; `Base_vacia_queda_en_version_1_con_las_20_tablas` a `Base_vacia_queda_en_version_2_con_las_21_tablas`, y `Assert.Equal(1L, …"PRAGMA user_version")` a `2L`. En `ServicioDemostracionTests.cs`: el `DisplayName` de «esquema 1» a «esquema 2», y la línea 27 de `1L` a `2L`. `6 insertions(+), 5 deletions(-)`.

  Salida de 1.3: `dotnet build INComedor.sln -warnaserror`: `0 Advertencia(s) 0 Errores`. `dotnet test INComedor.sln`: `total: 106, error: 0, correcto: 106` (Domain 46, Sqlite 47, Kiosk 9, Sync.Api 4). `git diff --stat -- tests/`: sólo `AperturaBaseTests.cs` y `ServicioDemostracionTests.cs`, más `MigracionesTests.cs` sin versionar.

## 2. Dominio: credencial y permiso

- [x] 2.1 Crear `tests/Intelektron.INComedor.Domain.Tests/ClasificadorCredencialTests.cs` y `EvaluadorPermisoOperadorTests.cs`, con los bordes exactos de D2: revocada y vencida a la vez, `ValidToUtc` igual al instante, `ValidFromUtc` un milisegundo después, extremos ausentes, un rol sin herencia y `PERMISSION_NOT_YET_VALID`. Sumar la prueba que compara la precedencia con `EvaluadorHabilitacion` sobre la misma credencial. Verificar con `dotnet test tests/Intelektron.INComedor.Domain.Tests` que fallan o no compilan antes de implementar, y pegar la salida
- [x] 2.2 Implementar `ClasificadorCredencial`, `EvaluadorPermisoOperador` y sus modelos (D2), sin cambiar `EvaluadorHabilitacion` ni `EvaluadorCupo`. Verificar con `dotnet test tests/Intelektron.INComedor.Domain.Tests` que pasan las nuevas y las 46 anteriores

  Salida de 2.1 (antes de implementar): `dotnet test tests/Intelektron.INComedor.Domain.Tests` no compiló: `error CS0246: El nombre del tipo o del espacio de nombres 'PermisoOperador' no se encontró` y `… 'RolOperador' no se encontró`, con `CS0103` para `ClasificadorCredencial`, `MotivoIdentificacion`, `CausaIdentificacion` y `EvaluadorPermisoOperador`; exit 1.

  Salida de 2.2: `total: 68, error: 0, correcto: 68`: las 46 anteriores, 12 de `ClasificadorCredencialTests` (la comparación con `EvaluadorHabilitacion` es un `Theory` de tres casos) y 10 de `EvaluadorPermisoOperadorTests`. `EvaluadorHabilitacion.cs` y `EvaluadorCupo.cs` sin cambios (`git status --short` muestra sólo los cuatro archivos nuevos).

## 3. Bitácora compartida e identificación por código

- [ ] 3.1 Mover `InsertarBitacora` de `UnidadConfirmacion` a `RegistroBitacora` sin cambiar su conducta (D9). Verificar con `dotnet test INComedor.sln` que todas las pruebas siguen en verde y con `git diff --stat -- tests/` que no cambió ninguna prueba en este paso
- [ ] 3.2 Crear `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/IdentificacionPorCodigoTests.cs`, con una prueba por escenario de «Identificación simulada por código» y «Rechazo de un código inválido» del delta de `atencion-puesto` (D3). La prueba de «Ningún rechazo registra el código» busca el código en todas las columnas de texto de `tbComedorBitacora` y `tbComedorSesionesIdentidad`. Verificar con `dotnet test … --filter "FullyQualifiedName~IdentificacionPorCodigoTests"` que fallan antes de implementar, y pegar la salida
- [ ] 3.3 Implementar `IdentificacionPorCodigo` (D3 y D4). Verificar con el mismo filtro que pasan, y con `dotnet test INComedor.sln` que nada más cambió

## 4. Comprobante: envío después del commit

- [ ] 4.1 Crear `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/TrabajosImpresionTests.cs` y su `ImpresoraDePrueba` (disponible, sin papel, incierta y una excepción que simula la caída durante el envío), con una prueba por escenario de «Envío del comprobante después del commit» del delta de `comprobante-local` (D7). Verificar con `dotnet test … --filter "FullyQualifiedName~TrabajosImpresionTests"` que fallan antes de implementar, y pegar la salida
- [ ] 4.2 Implementar `IImpresoraComprobantes`, `ComprobanteAImprimir` y `TrabajosImpresion.EnviarPendientes()`, con `UNKNOWN` antes de enviar y los registros `PRINT_JOB_UPDATED` (D7). Verificar con el mismo filtro que pasan, y con una consulta en la prueba de «Impresora sin papel» que el consumo, el uso de cupo y el comprobante son idénticos antes y después

## 5. Asistencia: permiso, recuperación y reimpresión

- [ ] 5.1 Crear `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/AsistenciaTests.cs`, con una prueba por escenario de «Asistencia con permiso OPERATOR» y «Recuperar o reimprimir sin tocar el consumo» del delta de `comprobante-local` (D8). El aviso de incertidumbre se prueba en el grupo 6, porque es de la pantalla; acá se prueba que la copia nueva lleva su número y la leyenda. Verificar con `dotnet test … --filter "FullyQualifiedName~AsistenciaTests"` que fallan antes de implementar, y pegar la salida
- [ ] 5.2 Implementar `Asistencia.Entrar`, `Recuperar` y `Reimprimir`, la lista fija de motivos y la consulta de comprobantes de la fecha de negocio (D8). Verificar con el mismo filtro que pasan; con «La sesión de asistencia no habilita consumo» que `UnidadConfirmacion` responde `SesionNoUtilizable` sin escribir nada; y con `dotnet test INComedor.sln` todo en verde

## 6. Kiosk: semilla, modelo de estados y pantallas

- [ ] 6.1 Crear `tests/Intelektron.INComedor.Kiosk.Tests/SemillaAtencionTests.cs` con los tres escenarios de «Conjunto sintético de atención» del delta de `modo-demostracion` (D10). Verificar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~SemillaAtencionTests"` que fallan antes de implementar, y pegar la salida
- [ ] 6.2 Implementar `SemillaAtencion` y `semilla_atencion.sql` con los códigos y permisos de D10, sin tocar `semilla_demostracion.sql`. Verificar con el mismo filtro que pasan y con `git diff --stat -- src/Intelektron.INComedor.Kiosk/Demostracion/` vacío
- [ ] 6.3 Crear `tests/Intelektron.INComedor.Kiosk.Tests/MaquinaAtencionTests.cs`, con SQLite real, un reloj manual nuevo y la `ImpresoraDePrueba` del Kiosk. Lleva una prueba por escenario de «Catálogo y selección sin consumo hasta confirmar», «Confirmación con un solo IntentId y la persona fija» y «Reconfirmación por cambio de catálogo» del delta de `atencion-puesto` (D5 y D6). El doble toque llama dos veces a `ConfirmarAsync()` antes del primer `await`. Verificar con `dotnet test … --filter "FullyQualifiedName~MaquinaAtencionTests"` que fallan antes de implementar, y pegar la salida
- [ ] 6.4 Implementar `MaquinaAtencion`, `ConsultaCatalogo` y el formato del cargo en `es-AR` (D5 y D6). Verificar con el mismo filtro que pasan
- [ ] 6.5 Crear `tests/Intelektron.INComedor.Kiosk.Tests/PantallaAtencionTests.cs` con bUnit sobre el servicio real. Cubre: botones nativos, campo del código vacío después de enviar, mensaje distinto por motivo, aviso de incertidumbre antes del motivo de reimpresión, «Ocupado» durante la confirmación y error de almacenamiento sin campo de código. Verificar con `dotnet test … --filter "FullyQualifiedName~PantallaAtencionTests"` que fallan antes de implementar, y pegar la salida
- [ ] 6.6 Implementar las pantallas Razor de `Atencion/`, el temporizador de 1 s con `InvokeAsync`, `ImpresoraSimulada` con su franja de modos, y la raíz de `MainWindow` según `--demostracion` (D5, D7 y D11). Verificar con `dotnet build INComedor.sln -warnaserror` (0 advertencias, 0 errores) y con el mismo filtro de 6.5 en verde
- [ ] 6.7 Actualizar «Cómo ver la demostración» de `readme.md` (la atención, `--demostracion`, los códigos sintéticos de D10 y la carpeta `comprobantes`) y la línea «Puesto de demostración» de `AGENTS.md`. Verificar con el chequeo de enlaces relativos sobre `readme.md` (`enlaces rotos: 0`)

## 7. Verificación integrada

- [ ] 7.1 Correr `dotnet build INComedor.sln -warnaserror` (0 advertencias, 0 errores), `dotnet test INComedor.sln` (las 102 anteriores, dos de ellas cambiadas en 1.2, más las nuevas, todas en verde) y `openspec validate --all --strict` (sin errores), y pegar las tres salidas
- [ ] 7.2 Comprobar que cada uno de los 52 escenarios de los cuatro deltas tiene su prueba. Listar con `rg -n "DisplayName"` sobre los archivos nuevos y las dos pruebas cambiadas, y pegar la tabla escenario → prueba, sin huecos
- [ ] 7.3 Correr el puesto real (`dotnet run --project src/Intelektron.INComedor.Kiosk`), con la carpeta `%LOCALAPPDATA%\INComedor\demo\` respaldada antes y restaurada después. Recorrer:
  - `0001` con «Siguiente» y «Anterior», y confirmar: «Consumo registrado» y el archivo del comprobante;
  - `0001` otra vez: el mensaje de cupo;
  - `0003`: el mensaje de revocado;
  - la impresora en «Sin papel», confirmar en REFRIGERIO: comprobante pendiente;
  - «Asistencia» con `9001`: recuperar ese trabajo, y después reimprimir con motivo, que deja la copia 1 con DUPLICADO;
  - «Asistencia» con `9002`: denegado;
  - `--demostracion`: abre la pantalla anterior.

  Pegar cada resultado con su captura en `ai4devs/gestion/evidencias/atender-seleccion-y-confirmacion/`, fuera del repo

## 8. Tallas y evidencia

- [ ] 8.1 Llevar a la tabla de «Tallas por change y colchón» de `docs/plan-academico.md` la talla de David (L, 8 a 16 h, 11,4 a 22,9 h con el colchón) y la de la IA (S, 2 a 4 h, 2,9 a 5,7 h), tal como están en proposal.md, con las horas reales del apply medidas con el reloj de la sesión. Actualizar en `docs/aplicacion-del-curso.md` las filas «Capacidad con 30 % de margen» y «La IA estima como un par, no como única fuente» con el enlace a este change. Verificar con el chequeo del mapa (`defectuosas: 0`)
- [ ] 8.2 Agregar la evidencia del change. En `docs/cobertura-entrega.md`, la fila «De 3 a 5 funcionalidades» suma la atención y el comprobante. En `docs/aplicacion-del-curso.md`, la fila del mapa que corresponda suma las pruebas nuevas. Si el gate dejó un ajuste humano (como la aprobación de las dos pruebas de D1), va su entrada en el §5 de `prompts.md`. Verificar con el chequeo de enlaces relativos sobre esos tres documentos (`enlaces rotos: 0`) y con `git status --short`, donde sólo aparecen esos documentos y el `tasks.md` de este change

## Workflow follow-up

- `/openspec-verify-change` con su salida en la descripción del PR.
- Archivar con `openspec archive atender-seleccion-y-confirmacion -y`, que crea las capabilities `atencion-puesto` y `comprobante-local` y modifica `registro-consumo` y `modo-demostracion`. Después, corregir los enlaces a `openspec/changes/atender-seleccion-y-confirmacion/` de `docs/` (sugerencia 1 del verify del change 3) y correr `openspec validate --all --strict`.
- Push, PR y merge son de David.
