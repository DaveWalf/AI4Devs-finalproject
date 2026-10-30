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

- [x] 3.1 Mover `InsertarBitacora` de `UnidadConfirmacion` a `RegistroBitacora` sin cambiar su conducta (D9). Verificar con `dotnet test INComedor.sln` que todas las pruebas siguen en verde y con `git diff --stat -- tests/` que no cambió ninguna prueba en este paso
- [x] 3.2 Crear `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/IdentificacionPorCodigoTests.cs`, con una prueba por escenario de «Identificación simulada por código» y «Rechazo de un código inválido» del delta de `atencion-puesto` (D3). La prueba de «Ningún rechazo registra el código» busca el código en todas las columnas de texto de `tbComedorBitacora` y `tbComedorSesionesIdentidad`. Verificar con `dotnet test … --filter "FullyQualifiedName~IdentificacionPorCodigoTests"` que fallan antes de implementar, y pegar la salida
- [x] 3.3 Implementar `IdentificacionPorCodigo` (D3 y D4). Verificar con el mismo filtro que pasan, y con `dotnet test INComedor.sln` que nada más cambió

  Salida de 3.1: `dotnet build INComedor.sln -warnaserror`: `0 Advertencia(s) 0 Errores`; `dotnet test INComedor.sln`: `total: 128, error: 0, correcto: 128`; `git diff --stat -- tests/` vacío. Junto con `InsertarBitacora` salió a `PuestoLocal` la lectura del puesto activo (`LeerPuesto` y su record), porque la bitácora necesita el Id y la zona del puesto y la identificación lo lee igual; el SQL es el mismo, carácter por carácter. El componente de cada registro lo da quien crea su `RegistroBitacora` (`Infrastructure.Sqlite.UnidadConfirmacion` sigue igual).

  Salida de 3.2 (antes de implementar): `dotnet test … --filter "FullyQualifiedName~IdentificacionPorCodigoTests"` no compiló: `error CS0246: El nombre del tipo o del espacio de nombres 'IdentificacionPorCodigo' no se encontró`, con `CS0103` para `PropositoSesion`; exit 1. Los cuatro códigos de los rechazos llevan letras fuera del hexadecimal (`SINTETICO-REVOCADO-77` y similares), para que un UUID no pueda contenerlos por azar.

  Salida de 3.3: el filtro da `total: 8, error: 0, correcto: 8`. `dotnet test INComedor.sln`: `total: 136, error: 0, correcto: 136` (128 más 8); build con `0 Advertencia(s) 0 Errores`; `git diff --stat -- tests/` vacío.

## 4. Comprobante: envío después del commit

- [x] 4.1 Crear `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/TrabajosImpresionTests.cs` y su `ImpresoraDePrueba` (disponible, sin papel, incierta y una excepción que simula la caída durante el envío), con una prueba por escenario de «Envío del comprobante después del commit» del delta de `comprobante-local` (D7). Verificar con `dotnet test … --filter "FullyQualifiedName~TrabajosImpresionTests"` que fallan antes de implementar, y pegar la salida
- [x] 4.2 Implementar `IImpresoraComprobantes`, `ComprobanteAImprimir` y `TrabajosImpresion.EnviarPendientes()`, con `UNKNOWN` antes de enviar y los registros `PRINT_JOB_UPDATED` (D7). Verificar con el mismo filtro que pasan, y con una consulta en la prueba de «Impresora sin papel» que el consumo, el uso de cupo y el comprobante son idénticos antes y después

  Salida de 4.1 (antes de implementar): `dotnet test … --filter "FullyQualifiedName~TrabajosImpresionTests"` no compiló: `error CS0246` para `ComprobanteAImprimir`, `IImpresoraComprobantes`, `RespuestaImpresora` y `TrabajosImpresion`; exit 1. `ImpresoraDePrueba` tiene los cuatro modos: disponible, sin papel (`PAPER_OUT`), incierta y caída (lanza `CaidaSimuladaException` antes de responder).

  Salida de 4.2: el filtro da `total: 5, error: 0, correcto: 5`. `Impresora_sin_papel_no_toca_el_consumo` compara, fila por fila, `tbComedorConsumos`, `tbComedorUsosCupo` y `tbComedorComprobantes` antes y después del envío, y cuenta un solo trabajo. La bitácora registra `QUEUED>UNKNOWN` y después `UNKNOWN>SUBMITTED`, `UNKNOWN>FAILED` o `UNKNOWN>UNKNOWN`. `dotnet test INComedor.sln`: `total: 141, error: 0, correcto: 141`; build con `0 Advertencia(s) 0 Errores`; `git diff --stat -- tests/` vacío.

## 5. Asistencia: permiso, recuperación y reimpresión

- [x] 5.1 Crear `tests/Intelektron.INComedor.Infrastructure.Sqlite.Tests/AsistenciaTests.cs`, con una prueba por escenario de «Asistencia con permiso OPERATOR» y «Recuperar o reimprimir sin tocar el consumo» del delta de `comprobante-local` (D8). El aviso de incertidumbre se prueba en el grupo 6, porque es de la pantalla; acá se prueba que la copia nueva lleva su número y la leyenda. Verificar con `dotnet test … --filter "FullyQualifiedName~AsistenciaTests"` que fallan antes de implementar, y pegar la salida
- [x] 5.2 Implementar `Asistencia.Entrar`, `Recuperar` y `Reimprimir`, la lista fija de motivos y la consulta de comprobantes de la fecha de negocio (D8). Verificar con el mismo filtro que pasan; con «La sesión de asistencia no habilita consumo» que `UnidadConfirmacion` responde `SesionNoUtilizable` sin escribir nada; y con `dotnet test INComedor.sln` todo en verde

  Salida de 5.1 (antes de implementar): `dotnet test … --filter "FullyQualifiedName~AsistenciaTests"` no compiló: `error CS0246: El nombre del tipo o del espacio de nombres 'Asistencia' no se encontró`, con `CS0103` para `MotivoReimpresion`; exit 1. Son 13 pruebas: los 12 escenarios de los dos requirements y una de borde de D8 (recuperar un original que no está en `FAILED` no hace nada).

  Salida de 5.2: el filtro da `total: 13, error: 0, correcto: 13`. `La_sesion_de_asistencia_no_habilita_consumo` compara `Sql.ConteoDeEscritura` antes y después de la confirmación con la sesión privilegiada y obtiene `SesionNoUtilizable`. El «Salir» del escenario queda en dos partes: `Asistencia.Salir(sesion)` cierra la sesión en la base (RB26: «se cierra al terminar»), y la misma prueba comprueba que después reimprimir da `SESSION_NOT_USABLE`; la vuelta a reposo sin el nombre se prueba en la pantalla (6.5). Los motivos se guardan como `PAPER_JAMMED`, `PAPER_ILLEGIBLE`, `TICKET_NOT_PRINTED` y `OTHER`. `dotnet test INComedor.sln`: `total: 154, error: 0, correcto: 154`; build con `0 Advertencia(s) 0 Errores`; `git diff --stat -- tests/` vacío.

## 6. Kiosk: semilla, modelo de estados y pantallas

- [x] 6.1 Crear `tests/Intelektron.INComedor.Kiosk.Tests/SemillaAtencionTests.cs` con los tres escenarios de «Conjunto sintético de atención» del delta de `modo-demostracion` (D10). Verificar con `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~SemillaAtencionTests"` que fallan antes de implementar, y pegar la salida
- [x] 6.2 Implementar `SemillaAtencion` y `semilla_atencion.sql` con los códigos y permisos de D10, sin tocar `semilla_demostracion.sql`. Verificar con el mismo filtro que pasan y con `git diff --stat -- src/Intelektron.INComedor.Kiosk/Demostracion/` vacío
- [x] 6.3 Crear `tests/Intelektron.INComedor.Kiosk.Tests/MaquinaAtencionTests.cs`, con SQLite real, un reloj manual nuevo y la `ImpresoraDePrueba` del Kiosk. Lleva una prueba por escenario de «Catálogo y selección sin consumo hasta confirmar», «Confirmación con un solo IntentId y la persona fija» y «Reconfirmación por cambio de catálogo» del delta de `atencion-puesto` (D5 y D6). El doble toque llama dos veces a `ConfirmarAsync()` antes del primer `await`. Verificar con `dotnet test … --filter "FullyQualifiedName~MaquinaAtencionTests"` que fallan antes de implementar, y pegar la salida
- [x] 6.4 Implementar `MaquinaAtencion`, `ConsultaCatalogo` y el formato del cargo en `es-AR` (D5 y D6). Verificar con el mismo filtro que pasan
- [x] 6.5 Crear `tests/Intelektron.INComedor.Kiosk.Tests/PantallaAtencionTests.cs` con bUnit sobre el servicio real. Cubre: botones nativos, campo del código vacío después de enviar, mensaje distinto por motivo, aviso de incertidumbre antes del motivo de reimpresión, «Ocupado» durante la confirmación y error de almacenamiento sin campo de código. Verificar con `dotnet test … --filter "FullyQualifiedName~PantallaAtencionTests"` que fallan antes de implementar, y pegar la salida
- [x] 6.6 Implementar las pantallas Razor de `Atencion/`, el temporizador de 1 s con `InvokeAsync`, `ImpresoraSimulada` con su franja de modos, y la raíz de `MainWindow` según `--demostracion` (D5, D7 y D11). Verificar con `dotnet build INComedor.sln -warnaserror` (0 advertencias, 0 errores) y con el mismo filtro de 6.5 en verde
- [x] 6.7 Actualizar «Cómo ver la demostración» de `readme.md` (la atención, `--demostracion`, los códigos sintéticos de D10 y la carpeta `comprobantes`) y la línea «Puesto de demostración» de `AGENTS.md`. Verificar con el chequeo de enlaces relativos sobre `readme.md` (`enlaces rotos: 0`)

  Salida de 6.1 (antes de implementar): `dotnet test tests/Intelektron.INComedor.Kiosk.Tests --filter "FullyQualifiedName~SemillaAtencionTests"` no compiló: `error CS0234: El tipo o el nombre del espacio de nombres 'Atencion' no existe en el espacio de nombres 'Intelektron.INComedor.Kiosk'` y `error CS0246` para `ServicioAtencion`; exit 1. «Atención sobre una base de la demostración en uso» crea la base con `ServicioDemostracion`, le confirma un consumo y la devuelve a la versión 1 (`DROP TABLE tbComedorPermisosOperadores; PRAGMA user_version = 1`): es la base que dejaba el código anterior, porque la `0001` no cambió.

  Salida de 6.2: el filtro da `total: 3, error: 0, correcto: 3`. En la primera corrida falló una, por la prueba: comparaba el UUID como texto (`Expected: "b42c1bd1-…"`, `Actual: "B42C1BD1-…"`, porque `Microsoft.Data.Sqlite` lo guarda en mayúsculas); se la ajustó para comparar `Guid`. `git diff --stat -- src/Intelektron.INComedor.Kiosk/Demostracion/` vacío. Las 14 opciones nuevas son 8 de ALMUERZO y 6 de REFRIGERIO, con UUID `…0011` a `…0048`. `ServicioAtencion.Preparar()` siembra la demostración, agrega la atención y envía los trabajos en cola (D7).

  Salida de 6.3 (antes de implementar): `dotnet test … --filter "FullyQualifiedName~MaquinaAtencionTests"` no compiló: `error CS0246: El nombre del tipo o del espacio de nombres 'MaquinaAtencion' no se encontró`; exit 1. Son 15 pruebas: los escenarios de los tres requirements, salvo «Opciones y navegación alcanzables con el teclado», que es de la pantalla y está en 6.5. El doble toque y la otra identificación retienen un `BEGIN IMMEDIATE` desde la prueba, para que la confirmación siga en curso de forma determinista.

  Salida de 6.4: el filtro da `total: 15, error: 0, correcto: 15`. `ConsultaCatalogo` quedó en `Infrastructure.Sqlite`, como dice el «Impacto» de proposal.md, y ordena con la cultura `es-AR`. El cargo usa `es-AR` y la escala de la opción (`ARS 1.500,00`). Los mensajes de cada motivo son los del catálogo de estados de `docs/experiencia.md`; los que el boceto no tiene (`IDENTITY_NOT_RESOLVED`, los de permiso, sesión vencida) están en `MensajesAtencion`.

  Salida de 6.5 (antes de implementar): `dotnet test … --filter "FullyQualifiedName~PantallaAtencionTests"` no compiló: `error CS0246: El nombre del tipo o del espacio de nombres 'PantallaAtencion' no se encontró`; exit 1. Son 11 pruebas, entre ellas «Opciones y navegación alcanzables con el teclado», el campo vacío, el texto de la pantalla sin el código, el aviso de incertidumbre antes de los motivos, «ocupado» durante la confirmación, sin papel, salir de la asistencia, el permiso que vence y el error de almacenamiento sin campo de código.

  Salida de 6.6: `dotnet build INComedor.sln -warnaserror`: `0 Advertencia(s) 0 Errores` (antes, un `--` dentro de un comentario XAML dio `MC3000`; se reescribió el comentario). El filtro de 6.5 da `total: 11, error: 0, correcto: 11`. En la primera corrida falló una por la prueba: buscaba el código `0002` en todo el markup y lo encontró en el color CSS `#b00020`; ahora compara el texto visible, sin las hojas de estilo, más los valores de los campos. «Anterior» y «Siguiente» usan `aria-disabled` en los bordes, y no `disabled`, para que el teclado los alcance. `dotnet test INComedor.sln`: `total: 183, error: 0, correcto: 183`; `git diff --stat -- tests/` vacío.

  Salida de 6.7: `readme.md` describe la atención, `--demostracion`, la tabla de los ocho códigos de D10 y la carpeta `comprobantes`, y su §2.3 dice qué implementa hoy el Kiosk; `AGENTS.md` actualiza la línea «Puesto de demostración». Chequeo de enlaces relativos sobre `readme.md` y `AGENTS.md`: `enlaces rotos: 0`.

## 7. Verificación integrada

- [x] 7.1 Correr `dotnet build INComedor.sln -warnaserror` (0 advertencias, 0 errores), `dotnet test INComedor.sln` (las 102 anteriores, dos de ellas cambiadas en 1.2, más las nuevas, todas en verde) y `openspec validate --all --strict` (sin errores), y pegar las tres salidas
- [x] 7.2 Comprobar que cada uno de los 52 escenarios de los cuatro deltas tiene su prueba. Listar con `rg -n "DisplayName"` sobre los archivos nuevos y las dos pruebas cambiadas, y pegar la tabla escenario → prueba, sin huecos
- [x] 7.3 Correr el puesto real (`dotnet run --project src/Intelektron.INComedor.Kiosk`), con la carpeta `%LOCALAPPDATA%\INComedor\demo\` respaldada antes y restaurada después. Recorrer:
  - `0001` con «Siguiente» y «Anterior», y confirmar: «Consumo registrado» y el archivo del comprobante;
  - `0001` otra vez: el mensaje de cupo;
  - `0003`: el mensaje de revocado;
  - la impresora en «Sin papel», confirmar en REFRIGERIO: comprobante pendiente;
  - «Asistencia» con `9001`: recuperar ese trabajo, y después reimprimir con motivo, que deja la copia 1 con DUPLICADO;
  - «Asistencia» con `9002`: denegado;
  - `--demostracion`: abre la pantalla anterior.

  Pegar cada resultado con su captura en `ai4devs/gestion/evidencias/atender-seleccion-y-confirmacion/`, fuera del repo

  Salida de 7.1: `dotnet build INComedor.sln -warnaserror`: `Compilación correcta. 0 Advertencia(s) 0 Errores`. `dotnet test INComedor.sln`: `total: 183, error: 0, correcto: 183, omitido: 0` (Domain 68, Sqlite 73, Kiosk 38, Sync.Api 4: las 102 anteriores, dos de ellas cambiadas en 1.2, más 81 nuevas). `openspec validate --all --strict`: `Totals: 6 passed, 0 failed (6 items)`, con `✓ change/atender-seleccion-y-confirmacion`.

  Salida de 7.2, escenario → prueba (`rg -n "DisplayName"` sobre los archivos nuevos y los cambiados; S = `Infrastructure.Sqlite.Tests`, K = `Kiosk.Tests`, la línea es la del atributo):

  | # | Delta · escenario | Prueba |
  |---|---|---|
  | 1 | atencion-puesto · Código vigente abre la selección | S `IdentificacionPorCodigoTests.cs:25`; pantalla K `PantallaAtencionTests.cs:52` |
  | 2 | atencion-puesto · Ceros iniciales y espacios alrededor | S `IdentificacionPorCodigoTests.cs:51` |
  | 3 | atencion-puesto · Código revocado | S `IdentificacionPorCodigoTests.cs:63`; pantalla K `PantallaAtencionTests.cs:65` |
  | 4 | atencion-puesto · Código vencido en el límite exacto | S `IdentificacionPorCodigoTests.cs:80` |
  | 5 | atencion-puesto · Revocado y vencido a la vez | S `IdentificacionPorCodigoTests.cs:93` |
  | 6 | atencion-puesto · Código todavía no vigente | S `IdentificacionPorCodigoTests.cs:103` |
  | 7 | atencion-puesto · Código desconocido | S `IdentificacionPorCodigoTests.cs:117` |
  | 8 | atencion-puesto · Ningún rechazo registra el código | S `IdentificacionPorCodigoTests.cs:129`; pantalla K `PantallaAtencionTests.cs:65` |
  | 9 | atencion-puesto · Recorrer un catálogo de 15 opciones | K `MaquinaAtencionTests.cs:28` |
  | 10 | atencion-puesto · Opciones y navegación alcanzables con el teclado | K `PantallaAtencionTests.cs:33` |
  | 11 | atencion-puesto · Cancelar antes de confirmar | K `MaquinaAtencionTests.cs:56` |
  | 12 | atencion-puesto · Treinta segundos sin actividad | K `MaquinaAtencionTests.cs:70` |
  | 13 | atencion-puesto · Un toque reinicia el plazo | K `MaquinaAtencionTests.cs:89` |
  | 14 | atencion-puesto · Autorización sin red | K `MaquinaAtencionTests.cs:110`; pantalla K `PantallaAtencionTests.cs:91` |
  | 15 | atencion-puesto · Doble toque | K `MaquinaAtencionTests.cs:130`; pantalla K `PantallaAtencionTests.cs:119` |
  | 16 | atencion-puesto · Otra identificación durante la confirmación | K `MaquinaAtencionTests.cs:157` |
  | 17 | atencion-puesto · Cada rechazo con su mensaje | K `MaquinaAtencionTests.cs:183` |
  | 18 | atencion-puesto · La sesión venció mientras elegía | K `MaquinaAtencionTests.cs:217` |
  | 19 | atencion-puesto · Vuelta a reposo después del resultado | K `MaquinaAtencionTests.cs:246` |
  | 20 | atencion-puesto · Base de una versión de esquema desconocida | K `MaquinaAtencionTests.cs:271`; pantalla K `PantallaAtencionTests.cs:219` |
  | 21 | atencion-puesto · Cambio de precio entre la selección y la confirmación | K `MaquinaAtencionTests.cs:290` |
  | 22 | atencion-puesto · Reconfirmar con el valor vigente | K `MaquinaAtencionTests.cs:307` |
  | 23 | atencion-puesto · Volver sin reconfirmar | K `MaquinaAtencionTests.cs:329` |
  | 24 | atencion-puesto · Opción retirada del catálogo | K `MaquinaAtencionTests.cs:344` |
  | 25 | comprobante-local · Impresora disponible | S `TrabajosImpresionTests.cs:13` |
  | 26 | comprobante-local · Impresora sin papel | S `TrabajosImpresionTests.cs:36`; pantalla K `PantallaAtencionTests.cs:106` |
  | 27 | comprobante-local · Respuesta incierta de la impresora | S `TrabajosImpresionTests.cs:53` |
  | 28 | comprobante-local · Caída durante el envío | S `TrabajosImpresionTests.cs:66` |
  | 29 | comprobante-local · Arranque con un trabajo en cola | S `TrabajosImpresionTests.cs:82` |
  | 30 | comprobante-local · Operador con permiso vigente | S `AsistenciaTests.cs:23` |
  | 31 | comprobante-local · Persona sin permiso | S `AsistenciaTests.cs:51`; pantalla K `PantallaAtencionTests.cs:164` |
  | 32 | comprobante-local · Permiso vencido | S `AsistenciaTests.cs:67` |
  | 33 | comprobante-local · Permiso revocado | S `AsistenciaTests.cs:81` |
  | 34 | comprobante-local · Administrador sin OPERATOR | S `AsistenciaTests.cs:92` |
  | 35 | comprobante-local · Autenticación fallida | S `AsistenciaTests.cs:103` |
  | 36 | comprobante-local · La sesión de asistencia no habilita consumo | S `AsistenciaTests.cs:118`; «Salir», K `PantallaAtencionTests.cs:177` |
  | 37 | comprobante-local · Reimpresión con motivo | S `AsistenciaTests.cs:145` |
  | 38 | comprobante-local · Segunda reimpresión | S `AsistenciaTests.cs:176` |
  | 39 | comprobante-local · Trabajo fallido por papel | S `AsistenciaTests.cs:190` |
  | 40 | comprobante-local · Resultado incierto | S `AsistenciaTests.cs:222`; aviso, K `PantallaAtencionTests.cs:140` |
  | 41 | comprobante-local · El permiso vence durante la sesión | S `AsistenciaTests.cs:239`; reposo, K `PantallaAtencionTests.cs:191` |
  | 42 | registro-consumo · Base vacía | S `AperturaBaseTests.cs:35` (cambiada en 1.2) |
  | 43 | registro-consumo · Base en la versión 1 | S `MigracionesTests.cs:23` |
  | 44 | registro-consumo · Versión de esquema desconocida | S `AperturaBaseTests.cs:61` (sin cambios) |
  | 45 | registro-consumo · Unicidad de consumo por intención | S `AperturaBaseTests.cs:75` (sin cambios) |
  | 46 | registro-consumo · Unicidad de permiso por persona y rol | S `MigracionesTests.cs:59` |
  | 47 | modo-demostracion · Primer arranque sobre una base inexistente | K `ServicioDemostracionTests.cs:18` (cambiada en 1.2) |
  | 48 | modo-demostracion · Un segundo arranque no duplica ni borra | K `ServicioDemostracionTests.cs:35` (sin cambios) |
  | 49 | modo-demostracion · Base de una versión de esquema desconocida | K `ServicioDemostracionTests.cs:54` y `PantallaDemostracionTests.cs:58` (sin cambios) |
  | 50 | modo-demostracion · Primer arranque de la atención | K `SemillaAtencionTests.cs:19` |
  | 51 | modo-demostracion · Un segundo arranque de la atención no duplica | K `SemillaAtencionTests.cs:43` |
  | 52 | modo-demostracion · Atención sobre una base de la demostración en uso | K `SemillaAtencionTests.cs:57` |

  52 de 52, sin huecos (24 de `atencion-puesto`, 17 de `comprobante-local`, 5 de `registro-consumo` y 6 de `modo-demostracion`).

  Salida de 7.3, el 30/10/2026, cerrada a las 07:31 (los comprobantes son de las 07:28 y las 07:29), con `dotnet run --project src/Intelektron.INComedor.Kiosk` (sesión elevada). `%LOCALAPPDATA%\INComedor\demo\` no existía antes: el respaldo fue esa constatación y la restauración, borrarla al final (`demo existe: False`). UI Automation no ve el contenido de este WebView2 dentro de WPF (0 elementos en cinco intentos), y la variable de CDP no rige en un proceso elevado (spike del 29/10). Por eso cada toque fue un `PostMessage` de mouse o de teclado dirigido a la ventana hija `Chrome_WidgetWin_0` del proceso del Kiosk, sin mover el cursor ni tomar el foco. Las coordenadas salen de cada captura (`PrintWindow`). Capturas `01` a `19` en `ai4devs/gestion/evidencias/atender-seleccion-y-confirmacion/`:
  - `0001`, con «Siguiente» dos veces (página 3 de 3, «Siguiente» atenuado) y «Anterior» dos veces, elige «Almuerzo común ARS 1.500,00» y confirma: «Consumo registrado. Retirá tu comprobante» (`03` a `07`). El comprobante quedó en `comprobantes\38373280-…-0.txt`, con `ORIGINAL`, la opción, la fecha de negocio `2026-10-30` y el cargo, sin persona ni código.
  - `0001` otra vez con la misma opción: «Ya registraste este servicio. Consultá al responsable del comedor» (`08`).
  - `0003`: «Esta credencial ya no está habilitada. Solicitá asistencia», con el campo vacío y sin el código en pantalla (`09`).
  - Impresora en «Sin papel» y «Café con leche» de REFRIGERIO: «Tu consumo está registrado. El comprobante queda pendiente» (`10` y `11`).
  - «Asistencia» con `9001`: los dos comprobantes del día, con «Falló» y «Recuperar» en el de café (`12`). Con la impresora en «Disponible» (el primer toque a «Disponible», desde el resultado, no tomó; se repitió, `13`), «Recuperar» da «Comprobante original enviado otra vez» y el estado «Enviado», con el mismo trabajo (`14`). «Reimprimir» muestra la lista fija de motivos (`15`), y «Papel ilegible» da «Copia 1 enviada con la leyenda DUPLICADO» (`16`), en `comprobantes\2afe495f-…-1.txt` con la línea `DUPLICADO · copia 1`.
  - «Salir» vuelve a reposo (`17`). «Asistencia» con `9002`: «Tu permiso de operador está vencido» (`18`).
  - La base al cerrar: `user_version 2`; 2 consumos; intenciones `AUTHORIZED` 2 y `DENIED` 1; trabajos de copia 0 `SUBMITTED` 2 y de copia 1 `SUBMITTED` 1; bitácora con `IDENTIFICATION_REJECTED` 1, `ASSISTANCE_GRANTED` 1, `ASSISTANCE_DENIED` 1, `PRINT_JOB_UPDATED` 8, `TICKET_RECOVERED` 1 y `TICKET_REPRINTED` 1. Ni `0003` ni `9002` aparecen en la bitácora ni en las sesiones.
  - `dotnet run --project src/Intelektron.INComedor.Kiosk -- --demostracion` abre «INComedor · pantalla de demostración» con su botón «Confirmar» (`19`).

  Cada Kiosk se cerró por PID con su árbol: 0 procesos del Kiosk y ningún `msedgewebview2` con su carpeta de datos.

## 8. Tallas y evidencia

- [x] 8.1 Llevar a la tabla de «Tallas por change y colchón» de `docs/plan-academico.md` la talla de David (L, 8 a 16 h, 11,4 a 22,9 h con el colchón) y la de la IA (S, 2 a 4 h, 2,9 a 5,7 h), tal como están en proposal.md, con las horas reales del apply medidas con el reloj de la sesión. Actualizar en `docs/aplicacion-del-curso.md` las filas «Capacidad con 30 % de margen» y «La IA estima como un par, no como única fuente» con el enlace a este change. Verificar con el chequeo del mapa (`defectuosas: 0`)
- [x] 8.2 Agregar la evidencia del change. En `docs/cobertura-entrega.md`, la fila «De 3 a 5 funcionalidades» suma la atención y el comprobante. En `docs/aplicacion-del-curso.md`, la fila del mapa que corresponda suma las pruebas nuevas. Si el gate dejó un ajuste humano (como la aprobación de las dos pruebas de D1), va su entrada en el §5 de `prompts.md`. Verificar con el chequeo de enlaces relativos sobre esos tres documentos (`enlaces rotos: 0`) y con `git status --short`, donde sólo aparecen esos documentos y el `tasks.md` de este change

  Salida de 8.1: `docs/plan-academico.md` suma las dos filas de este change a la tabla de tallas y las horas reales del apply: **1,0 h** de reloj de la IA (de las 06:29, al empezar la 1.1, a las 07:31, al cerrar la 7.3), en el piso de su estimación de 1 a 1,5 h. `docs/aplicacion-del-curso.md` actualiza «Capacidad con 30 % de margen» y «La IA estima como un par…» con el enlace a este change. Chequeo del mapa: `filas del mapa: 82 · defectuosas: 0`.

  Salida de 8.2: `docs/cobertura-entrega.md`, fila «De 3 a 5 funcionalidades», suma la atención y el comprobante (sigue `Pendiente` por `recibir-lotes-idempotente`). En `docs/aplicacion-del-curso.md` se actualizan las filas de TDD (las 81 pruebas, cada una con su salida en rojo) y «Mockear sólo en las fronteras» (la impresora ya tiene puerto y doble). En el §5 de `prompts.md` entran los puntos 8 (las dos pruebas de D1 aprobadas en el gate) y 9 (David quitó el hook de tests durante este apply, commit aparte). Chequeo de enlaces relativos sobre los cuatro documentos: `enlaces rotos: 0`. `git status --short`: `M docs/aplicacion-del-curso.md`, `M docs/cobertura-entrega.md`, `M docs/plan-academico.md` (de 8.1) y `M prompts.md`, más este `tasks.md`.

## Workflow follow-up

- `/openspec-verify-change` con su salida en la descripción del PR.
- Archivar con `openspec archive atender-seleccion-y-confirmacion -y`, que crea las capabilities `atencion-puesto` y `comprobante-local` y modifica `registro-consumo` y `modo-demostracion`. Después, corregir los enlaces a `openspec/changes/atender-seleccion-y-confirmacion/` de `docs/` (sugerencia 1 del verify del change 3) y correr `openspec validate --all --strict`.
- Push, PR y merge son de David.
