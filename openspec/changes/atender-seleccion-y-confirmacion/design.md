# Design

## Context

El motivo está en proposal.md («Why»). Lo que condiciona el cómo:

- **`UnidadConfirmacion` ya resuelve la confirmación**: replay por `IntentId`, `CatalogoCambiado` cuando la versión o la opción no coinciden con el snapshot activo, y `SesionNoUtilizable` cuando la sesión venció, ya se usó, es de otra persona o no es de propósito `CONSUMPTION`. Este change no toca esa decisión: la usa.
- **El trabajo de impresión original nace en la transacción de RB06**, con copia 0 y estado `QUEUED`, y nadie lo procesa. El comprobante guarda en `comcmpDatosImpresion` un JSON con la opción, la fecha de negocio, la cantidad y el cargo, sin persona ni código.
- **La migración `0001` ya tiene lo que la identificación necesita**: `tbComedorCredencialesPersonas` (tipo `SERIAL_CODE`, vigencia y revocación, valor único por snapshot) y `tbComedorSesionesIdentidad` (método, propósito y credencial). No tiene permisos de operador: `LocalOperatorGrant` está en el modelo y en el mapa como `tbComedorPermisosOperadores`, pero no en la base.
- **`BaseComedor` conoce una sola migración** y fija `UltimaVersionConocida = 1`. Dos pruebas existentes afirman esa versión y la lista de 20 tablas: `AperturaBaseTests.cs:34-46` y `ServicioDemostracionTests.cs:18-33`.
- **La escritura de bitácora es privada de `UnidadConfirmacion`** (`InsertarBitacora`, con secuencia por puesto y generación).
- **El Kiosk abre `PantallaDemostracion` como raíz** (`MainWindow.xaml`). Las pruebas del Kiosk usan bUnit con un servicio real sobre un archivo SQLite temporal, sin dobles entre la pantalla y la base; los únicos dobles admitidos son la impresora y el lector (`AGENTS.md`).

## Goals / Non-Goals

**Goals:**

- Que cada transición de la atención y de la asistencia se pruebe sin la ventana: un modelo de estados en C# con un reloj inyectable, y las pantallas Razor que sólo lo dibujan.
- Que la impresión no pueda crear un segundo consumo ni reenviar sola un trabajo dudoso.
- Que todo lo que la atención escribe fuera de RB06 (sesiones, rechazos, estados de impresión, copias) quede en la bitácora, sin el código.

**Non-Goals:**

- Un trabajador de impresión en segundo plano: un puesto con una sola persona por vez no lo necesita (D7).
- Accesibilidad completa (contraste, escalas 125/150 %): fuera del corte. Sí botones nativos, que el teclado alcanza.

## Decisions

### D1. Migración `0002` y una lista ordenada de migraciones

`0002_permisos_operadores.sql` crea `tbComedorPermisosOperadores` con las columnas del mapa (`docs/nomenclatura-campos-er.csv:44-50`):

- columnas: `comvcCodigo`, `compopCodigo`, `comperCodigo`, `compopRol`, `compopFechaVigenciaDesdeUtc`, `compopFechaVigenciaHastaUtc` y `compopFechaRevocacionUtc`;
- PK `(comvcCodigo, compopCodigo)` y FK compuesta a `tbComedorPersonas`;
- `UQ (comvcCodigo, comperCodigo, compopRol)`;
- `CHECK (compopRol IN ('OPERATOR', 'SYSTEM_ADMINISTRATOR'))`.

`BaseComedor` pasa a una lista `(versión, recurso)` y a `UltimaVersionConocida = 2`. Al abrir, aplica cada migración cuya versión supere la de la base, cada una en su propia transacción `BEGIN IMMEDIATE`: relee `user_version` bajo el bloqueo, ejecuta el SQL y fija la versión nueva. El rechazo de una versión desconocida no cambia.

Cambian dos pruebas existentes, con la aprobación del gate y sólo en esto:

- `AperturaBaseTests.Base_vacia_queda_en_version_1_con_las_20_tablas` pasa a esperar `user_version` 2 y suma `tbComedorPermisosOperadores` a `TablasEsperadas`. Su nombre de método y su `DisplayName` dicen «versión 2» y «21 tablas».
- En `ServicioDemostracionTests.Primer_arranque_siembra_los_datos_sinteticos`, la línea 27 espera 2 y su `DisplayName` dice «esquema 2».

Descartadas:

- Agregar la tabla a la `0001`: una base ya creada en la versión 1, como la de la demostración de David, no la tendría nunca, y la `0001` deja de describir lo que se instaló.
- Guardar el permiso fuera del modelo (un archivo de configuración, o una credencial de otro tipo): contradice `LocalOperatorGrant` y RB26 («permiso del rol requerido vigente en el snapshot activo»).

### D2. Dos evaluadores puros en el dominio

- `ClasificadorCredencial.Clasificar(credencial?, instante)` aplica RB25, en este orden:
  - sin credencial: `IDENTITY_NOT_RESOLVED`, causa `UNKNOWN_CODE`;
  - con fecha de revocación: `CREDENTIAL_REVOKED`, sin mirar el reloj («desde la activación»);
  - `ValidToUtc <= instante`: `CREDENTIAL_EXPIRED`;
  - `ValidFromUtc > instante`: `IDENTITY_NOT_RESOLVED`, causa `NOT_YET_VALID`;
  - si no, vigente.

  Los extremos ausentes son abiertos, como en `habilitacion-local`.
- `EvaluadorPermisoOperador.Evaluar(permisos, rolPedido, instante)` busca el permiso del rol pedido, sin herencia (RB26):
  - si no hay: `PERMISSION_MISSING`;
  - revocado: `PERMISSION_REVOKED`;
  - `ValidToUtc <= instante`: `PERMISSION_EXPIRED`;
  - `ValidFromUtc > instante`: `PERMISSION_NOT_YET_VALID`, que la spec no nombra y se prueba sólo en el dominio.

  Devuelve también el `GrantId` y el `GrantedRole` evaluados, para la bitácora.

Descartada: reusar `EvaluadorHabilitacion`. Decide una intención, con persona y reglas, y no tiene `UNKNOWN_CODE` ni `NOT_YET_VALID`. Las precedencias de revocada y vencida coinciden con las suyas, y una prueba de dominio lo comprueba con la misma credencial en los dos evaluadores.

### D3. La identificación por código, con HU01.2-J

`IdentificacionPorCodigo.Identificar(codigo, proposito)`, en `Infrastructure.Sqlite`, hace todo en una sola transacción de escritura:

1. Normaliza el código: sólo `Trim()`, conservando los ceros iniciales (RB01).
2. Busca `(snapshot activo, 'SERIAL_CODE', valor)` y clasifica la credencial con D2.
3. Si es vigente, inserta la sesión: método `CODE`, la credencial y un vencimiento de 2 minutos, el mismo de `SesionesDePrueba`.
4. Si no, inserta un registro de bitácora `IDENTIFICATION_REJECTED`, cuyo contexto lleva el motivo, la causa interna y el `PersonId` sólo si la credencial existía.

El código nunca llega a una columna, a un mensaje de excepción ni al contexto. El resultado lleva el `IdentitySessionId`, el `PersonId` y el nombre visible, o el motivo.

Descartada: dejar HU01-J para después y resolver sólo los códigos vigentes. El resolvedor tendría que decidir igual qué hacer con un código inválido, y J sólo pide su motivo y su registro.

### D4. El código solo abre la sesión, también la de asistencia

RB25 y RB26 piden que el código sólo seleccione al candidato y que la huella 1:1 cree la sesión. La biometría es de la segunda ola (D-C24), así que en este corte el código solo abre la sesión, con método `CODE`. Lo declaran:

- la pantalla de reposo: «Identificación simulada por código (demostración)»;
- el README, con los códigos sintéticos.

Descartada: un doble de huella que siempre coincide. Presentaría como verificación biométrica algo que no verifica nada, y agregaría un contrato que el corte declaró no demostrado.

### D5. Un modelo de estados sin interfaz

`MaquinaAtencion`, en `src/Intelektron.INComedor.Kiosk/Atencion/`, es una clase C#. Recibe el `TimeProvider`, los servicios de D3, D6, D7 y D8 y la unidad de confirmación.

- **Estados**: `Reposo`, `Seleccion`, `Confirmando`, `Reconfirmacion`, `Resultado`, `Asistencia` y `ErrorAlmacenamiento`.
- **Plazos**: `RevisarInactividad()` vuelve a reposo a los 30 s del último toque en `Seleccion`, `Reconfirmacion` y `Asistencia`, y a los 10 s en `Resultado`. El borde es inclusivo: a los 30 s exactos ya vuelve.
- **Confirmación**: `ConfirmarAsync()` genera el `IntentId`, pasa a `Confirmando` antes de su primer `await` y corre la unidad en `Task.Run`. Una segunda llamada en `Confirmando` devuelve la misma tarea, así que usa el mismo `IntentId` (AT26).
- **Otra identificación**: `Identificar()` en `Confirmando` devuelve `Ocupado` y no toca la sesión.
- **Resultados**: `CatalogoCambiado` relee la opción. Si sigue en el catálogo pasa a `Reconfirmacion` con el valor vigente, y reconfirmar genera un `IntentId` nuevo. Si salió del catálogo, vuelve a `Seleccion` con el aviso. `SesionNoUtilizable` vuelve a reposo pidiendo identificarse otra vez.

Las pantallas Razor (`PantallaAtencion` y sus partes) sólo dibujan el estado y reenvían los toques. Un temporizador de 1 s llama a `RevisarInactividad()` con `InvokeAsync`.

Descartadas:

- El estado dentro de los `.razor`: cada borde de tiempo pediría una prueba con bUnit y un reloj real.
- El `Router` de Blazor con una página por estado: el estado viaja entre páginas, y prohibir una transición (confirmar dos veces, cambiar de persona) queda repartido.

### D6. Catálogo leído del snapshot activo

`ConsultaCatalogo.Leer()` devuelve las opciones del snapshot activo, ordenadas por nombre y luego por código, con versión, moneda, escala e importe.

- **Páginas**: de 6 opciones, una grilla de 3 × 2 como el boceto de `docs/experiencia.md`.
- **Cargo**: se formatea con la cultura `es-AR` y la escala de la opción, por ejemplo «ARS 1.500,00».
- **Toques**: cada opción, «Anterior», «Siguiente», «Cancelar» y «Confirmar» son `<button>`.

Descartada: una lista con desplazamiento. AT16 pide navegación, y el boceto la muestra paginada.

### D7. Impresión sincrónica después del commit, con `UNKNOWN` antes de enviar

El puerto `IImpresoraComprobantes.Enviar(ComprobanteAImprimir)` está en `Infrastructure.Sqlite` y devuelve `Enviado`, `Fallido(codigo)` o `Incierto`. `ComprobanteAImprimir` lleva el JSON del comprobante, el número de copia y, si la copia es mayor que 0, la leyenda «DUPLICADO · copia N».

`TrabajosImpresion.EnviarPendientes()` toma los trabajos en `QUEUED`. Con cada uno:

1. En una transacción, lo pasa a `UNKNOWN` y escribe en la bitácora `PRINT_JOB_UPDATED`.
2. Fuera de la transacción, llama a la impresora.
3. En otra transacción, lo pasa a `SUBMITTED` o a `FAILED` con `comimpCodigoUltimoError`, y escribe su registro. `Incierto` deja el trabajo en `UNKNOWN` y lo registra.

Un trabajo en `UNKNOWN` nunca se reenvía solo. La atención llama a `EnviarPendientes()` después de cada `AUTHORIZED` y al preparar la base, y así retoma un `QUEUED` que dejó una caída posterior al commit (RB17).

Descartadas:

- Enviar y guardar después: una caída entre las dos cosas reenvía al arrancar, y el papel puede salir dos veces sin marca.
- Un estado `SENDING`: el contrato (`PrintState`) no lo tiene.
- Un trabajador en segundo plano: suma concurrencia con la interfaz y pruebas no deterministas, sin beneficio con una persona por vez.

### D8. Asistencia: recuperar y reimprimir son acciones distintas

`Asistencia.Entrar(codigo)` identifica como D3 con propósito `PRIVILEGED_ACTION`, evalúa el permiso `OPERATOR` con D2 en la misma transacción, y hace una de dos cosas:

- con permiso, abre la sesión (2 minutos) y registra `ASSISTANCE_GRANTED`;
- sin permiso, registra `ASSISTANCE_DENIED`, sin sesión.

El contexto lleva `RequestedRole`, la causa y, sólo si existen, `PersonId`, `GrantId` y `GrantedRole`.

`Recuperar(sesion, trabajo)` y `Reimprimir(sesion, comprobante, motivo)` corren en `BEGIN IMMEDIATE`. Comprueban que la sesión sea `PRIVILEGED_ACTION` y no esté vencida, y vuelven a evaluar el permiso en el instante de la acción. Después:

- `Recuperar` acepta sólo el trabajo de copia 0 en `FAILED`: lo vuelve a `QUEUED` y registra `TICKET_RECOVERED`.
- `Reimprimir` inserta un trabajo con la copia `MAX + 1` en `QUEUED` y registra `TICKET_REPRINTED`, con el motivo, la copia y el comprobante.
- Con el permiso caído, registran `PRIVILEGED_ACTION_DENIED` sin escribir el trabajo.

Después del commit llaman a `EnviarPendientes()`. Ninguna acción toca consumo, cupo ni comprobante.

Los motivos son una lista fija: «Papel atascado», «Papel ilegible», «El comprobante no salió» y «Otro». La lista de comprobantes muestra los de la fecha de negocio actual, del más reciente al más antiguo, con hora, opción, nombre visible y estado del último trabajo.

Descartadas:

- Una sola acción «reimprimir» para todo: marcaría DUPLICADO un comprobante cuyo original nunca salió (`FAILED`).
- Reintentar solo un `FAILED`: HU03-A dice que lo pide el operador.
- Un motivo en texto libre: puede meter datos personales en la bitácora.

### D9. Bitácora compartida; el evento de impresión del outbox, después

`InsertarBitacora` sale de `UnidadConfirmacion` a una clase interna, `RegistroBitacora`, con la misma secuencia y la misma generación. La usan D3, D7 y D8. Es un movimiento sin cambio de conducta: las pruebas de `registro-consumo` y `habilitacion-local` siguen en verde sin tocarse.

Tipos nuevos: `IDENTIFICATION_REJECTED`, `ASSISTANCE_GRANTED`, `ASSISTANCE_DENIED`, `PRINT_JOB_UPDATED`, `TICKET_RECOVERED`, `TICKET_REPRINTED` y `PRIVILEGED_ACTION_DENIED`.

Descartada: escribir ya `PRINT_EVENT_RECORDED` en `tbComedorEventosSalida`. Lo consume el envío de lotes, que es `recibir-lotes-idempotente`. Escribirlo ahora agrega un contrato (`Contracts` y su prueba contra el OpenAPI) que nadie lee en este change.

### D10. Semilla de atención aparte

`SemillaAtencion` (con `semilla_atencion.sql` como recurso embebido) corre después de `SemillaDemostracion.Aplicar`, en su propia transacción. No hace nada si ya existe su primera opción (`…0011`). Agrega, con UUID fijos desde `…0011`:

- **Catálogo**: 14 opciones más, que con la de la demostración suman 15, en ALMUERZO y en un servicio REFRIGERIO con su categoría.
- **Personas con código**:

  | Código | Persona | Estado de la credencial | Reglas |
  |---|---|---|---|
  | `0001` | «Persona sintética 01» | vigente | límite 1 en ALMUERZO y 2 en REFRIGERIO |
  | `0002` | otra persona | vencida el 15/10/2026 | — |
  | `0003` | otra persona | revocada | — |
  | `0004` | otra persona | vigente desde el 01/01/2030 | — |

- **Personas con permiso**, cada una con su código:

  | Código | Permiso |
  |---|---|
  | `9001` | `OPERATOR` vigente |
  | `9002` | `OPERATOR` vencido |
  | `9003` | `OPERATOR` revocado |
  | `9004` | sólo `SYSTEM_ADMINISTRATOR` |

La persona de la demostración (`…0008`) no cambia y sigue sin credencial.

Descartada: ampliar la semilla de la demostración. Rompe sus pruebas y su spec («una opción», «una persona») sin ganar nada.

### D11. La ventana abre la atención; la demostración, con un argumento

`MainWindow` elige la raíz con `Environment.GetCommandLineArgs()`: `PantallaAtencion` por defecto y `PantallaDemostracion` con `--demostracion`. Las dos comparten la base de `%LOCALAPPDATA%\INComedor\demo\`.

`ImpresoraSimulada` escribe cada comprobante como texto en `…\demo\comprobantes\<TicketId>-<copia>.txt`. Tiene tres modos, Disponible, Sin papel e Incierta, que se eligen en una franja rotulada «Simulación de impresora (demostración)» al pie de la atención. Así la falla de papel se puede mostrar en vivo y en el E2E de la acción 3.

Descartadas:

- Sacar la pantalla de demostración de la ventana: su spec dejaría de verse en el puesto, y el spike de CDP apunta a su `#confirmar`.
- Elegir el modo de la impresora con una variable de entorno: no se ve en la demostración y el E2E no puede cambiarlo a mitad de la corrida.

### D12. Pruebas: cada escenario con la suya, vista en rojo antes

| Proyecto | Archivo nuevo | Qué prueba |
|---|---|---|
| `Domain.Tests` | `ClasificadorCredencialTests.cs` | D2 |
| `Domain.Tests` | `EvaluadorPermisoOperadorTests.cs` | D2 |
| `Infrastructure.Sqlite.Tests` | `MigracionesTests.cs` | D1 y los escenarios nuevos del delta de `registro-consumo` |
| `Infrastructure.Sqlite.Tests` | `IdentificacionPorCodigoTests.cs` | D3 |
| `Infrastructure.Sqlite.Tests` | `TrabajosImpresionTests.cs` | D7 |
| `Infrastructure.Sqlite.Tests` | `AsistenciaTests.cs` | D8 |
| `Kiosk.Tests` | `SemillaAtencionTests.cs` | D10 |
| `Kiosk.Tests` | `MaquinaAtencionTests.cs` | D5 y D6, con SQLite real y un reloj manual |
| `Kiosk.Tests` | `PantallaAtencionTests.cs` | bUnit: botones, mensajes, aviso de incertidumbre, campo del código vacío |

- El único doble es `ImpresoraDePrueba`, en cada proyecto de pruebas que la necesita. Puede fallar, quedar incierta o lanzar una excepción para simular la caída durante el envío.
- Cada `DisplayName` empieza por su AT o su regla.
- Ninguna prueba existente cambia, salvo las dos de D1.

## Risks / Trade-offs

- [Un `UNKNOWN` por caída antes de llamar a la impresora parece una impresión incierta aunque el papel nunca salió] → Se acepta: el costo es una copia marcada DUPLICADO decidida por el operador, nunca un segundo consumo ni un reenvío sin marca.
- [El código solo abre la sesión privilegiada] → Es la simulación declarada (D4): datos sintéticos, sin red y sin datos reales. La huella 1:1 de la segunda ola lo cierra.
- [El temporizador de Blazor corre en otro hilo] → `RevisarInactividad()` se llama con `InvokeAsync`, y los plazos se prueban en `MaquinaAtencion` con un reloj manual, sin temporizador.
- [La base de demostración de David está en la versión 1] → El escenario «Base en la versión 1» la cubre. Si algo falla, se borra `%LOCALAPPDATA%\INComedor\demo\`, como dice `AGENTS.md`.
- [Más superficie en la ventana real que en los changes anteriores] → La tarea de verificación integrada corre el puesto real y recorre cada pantalla con su evidencia, como en el change 2, donde así apareció el TFM.

## Migration Plan

Abrir la base aplica la `0002` y no hace falta ningún paso manual. Para volver atrás se borra la carpeta de la demostración. Sin cambios en la CI ni en el receptor.
